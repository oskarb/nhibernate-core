using System.Collections.Generic;

using NHibernate.Engine;
using NHibernate.Mapping;
using NHibernate.Type;
using NHibernate.Util;
using NHibernate.SqlCommand;
using System.Runtime.CompilerServices;
using System;
using System.Data;
using NHibernate.AdoNet.Util;

namespace NHibernate.Id.Enhanced
{
	/**
	 * An enhanced version of table-based id generation.
	 * <p/>
	 * Unlike the simplistic legacy one (which, btw, was only ever intended for subclassing
	 * support) we "segment" the table into multiple values.  Thus a single table can
	 * actually serve as the persistent storage for multiple independent generators.  One
	 * approach would be to segment the values by the name of the entity for which we are
	 * performing generation, which would mean that we would have a row in the generator
	 * table for each entity name.  Or any configuration really; the setup is very flexible.
	 * <p/>
	 * In this respect it is very similar to the legacy
	 * {@link org.hibernate.id.MultipleHiLoPerTableGenerator} in terms of the
	 * underlying storage structure (namely a single table capable of holding
	 * multiple generator values).  The differentiator is, as with
	 * {@link SequenceStyleGenerator} as well, the externalized notion
	 * of an optimizer.
	 * <p/>
	 * <b>NOTE</b> that by default we use a single row for all generators (based
	 * on {@link #DefaultSegmentValue}).  The configuration parameter
	 * {@link #ConfigPreferSegmentPerEntity} can be used to change that to
	 * instead default to using a row for each entity name.
	 * <p/>
	 * Configuration parameters:
	 * <table>
	 * 	 <tr>
	 *     <td><b>NAME</b></td>
	 *     <td><b>DEFAULT</b></td>
	 *     <td><b>DESCRIPTION</b></td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #TableParam}</td>
	 *     <td>{@link #DefaultTable}</td>
	 *     <td>The name of the table to use to store/retrieve values</td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #ValueColumnParam}</td>
	 *     <td>{@link #DefaultValueColumn}</td>
	 *     <td>The name of column which holds the sequence value for the given segment</td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #SegmentColumnParam}</td>
	 *     <td>{@link #DefaultSegmentColumn}</td>
	 *     <td>The name of the column which holds the segment key</td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #SegmentValueParam}</td>
	 *     <td>{@link #DefaultSegmentValue}</td>
	 *     <td>The value indicating which segment is used by this generator; refers to values in the {@link #SEGMENT_COLUMN_PARAM} column</td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #SegmentLengthParam}</td>
	 *     <td>{@link #DefaultSegmentLength}</td>
	 *     <td>The data length of the {@link #SEGMENT_COLUMN_PARAM} column; used for schema creation</td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #InitialParam}</td>
	 *     <td>{@link #DefaltInitialValue}</td>
	 *     <td>The initial value to be stored for the given segment</td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #IncrementParam}</td>
	 *     <td>{@link #DefaultIncrementSize}</td>
	 *     <td>The increment size for the underlying segment; see the discussion on {@link Optimizer} for more details.</td>
	 *   </tr>
	 *   <tr>
	 *     <td>{@link #OptimizerParam}</td>
	 *     <td><i>depends on defined increment size</i></td>
	 *     <td>Allows explicit definition of which optimization strategy to use</td>
	 *   </tr>
	 * </table>
	 *
	 * @author Steve Ebersole
	 */
	public class TableGenerator : TransactionHelper, IPersistentIdentifierGenerator, IConfigurable
	{
		private static readonly IInternalLogger log = LoggerProvider.LoggerFor(typeof(SequenceStyleGenerator));


		public const string ConfigPreferSegmentPerEntity = "prefer_entity_table_as_segment_value";

		public const string TableParam = "table_name";
		public const string DefaultTable = "hibernate_sequences";

		public const string ValueColumnParam = "value_column_name";
		public const string DefaultValueColumn = "next_val";

		public const string SegmentColumnParam = "segment_column_name";
		public const string DefaultSegmentColumn = "sequence_name";

		public const string SegmentValueParam = "segment_value";
		public const string DefaultSegmentValue = "default";

		public const string SegmentLengthParam = "segment_value_length";
		public const int DefaultSegmentLength = 255;

		public const string InitialParam = "initial_value";
		public const int DefaltInitialValue = 1;

		public const string IncrementParam = "increment_size";
		public const int DefaultIncrementSize = 1;

		public const string OptimizerParam = "optimizer";


		/// <summary>
		/// Type mapping for the identifier.
		/// </summary>
		public IType IdentifierType { get; private set; }


		/// <summary>
		/// The name of the table in which we store this generator's persistent state.
		/// </summary>
		public string TableName { get; private set; }


		/// <summary>
		/// The name of the column in which we store the segment to which each row
		/// belongs. The value here acts as primary key.
		/// </summary>
		public string SegmentColumnName { get; private set; }


		/// <summary>
		/// The value in the column identified by <see cref="SegmentColumnName"/> which
		/// corresponds to this generator instance.  In other words, this value
		/// indicates the row in which this generator instance will store values.
		/// </summary>
		public string SegmentValue { get; private set; }


		/// <summary>
		/// The size of the column identified by <see cref="SegmentColumnName"/>
		/// in the underlying table.
		/// </summary>
		/// <remarks>
		/// Should really have been called 'segmentColumnLength' or even better 'segmentColumnSize'.
		/// </remarks>
		public int SegmentValueLength { get; private set; }


		/// <summary>
		/// The name of the column in which we store our persistent generator value.
		/// </summary>
		public string ValueColumnName { get; private set; }


		/// <summary>
		/// The initial value to use when we find no previous state in the
		/// generator table corresponding to this instance.
		/// </summary>
		public int InitialValue { get; private set; }


		/// <summary>
		/// The amount of increment to use.  The exact implications of this
		/// depends on the optimizer being used, see <see cref="Optimizer"/>.
		/// </summary>
		public int IncrementSize { get; private set; }


		/// <summary>
		/// The optimizer being used by this generator. This mechanism
		/// allows avoiding calling the database each time a new identifier
		/// is needed.
		/// </summary>
		public IOptimizer Optimizer { get; private set; }


		/// <summary>
		/// The table access count. Only really useful for unit test assertions.
		/// </summary>
		public long TableAccessCount { get; private set; }


		private SqlString selectQuery;
		private SqlTypes.SqlType[] selectParameterTypes;
		private SqlString insertQuery;
		private SqlTypes.SqlType[] insertParameterTypes;
		private SqlString updateQuery;
		private SqlTypes.SqlType[] updateParameterTypes;




		public virtual string GeneratorKey()
		{
			return TableName;
		}


		#region Implementation of IConfigurable

		public virtual void Configure(IType type, IDictionary<string, string> parms, Dialect.Dialect dialect)
		{
			IdentifierType = type;

			TableName = determineGeneratorTableName(parms, dialect);
			SegmentColumnName = determineSegmentColumnName(parms, dialect);
			ValueColumnName = determineValueColumnName(parms, dialect);

			SegmentValue = determineSegmentValue(parms);

			SegmentValueLength = determineSegmentColumnSize(parms);
			InitialValue = determineInitialValue(parms);
			IncrementSize = determineIncrementSize(parms);

			buildSelectQuery(dialect);
			buildUpdateQuery();
			buildInsertQuery();


			// if the increment size is greater than one, we prefer pooled optimization; but we
			// need to see if the user prefers POOL or POOL_LO...
			string defaultPooledOptimizerStrategy = OptimizerFactory.Pool;
			//string defaultPooledOptimizerStrategy = PropertiesHelper.GetBoolean( Environment.PREFER_POOLED_VALUES_LO, parms, false )
			//        ? OptimizerFactory.Pool
			//        : OptimizerFactory.PoolLo;
			string defaultOptimizerStrategy = IncrementSize <= 1 ? OptimizerFactory.None : defaultPooledOptimizerStrategy;
			string optimizationStrategy = PropertiesHelper.GetString(OptimizerParam, parms, defaultOptimizerStrategy);
			Optimizer = OptimizerFactory.BuildOptimizer(
					optimizationStrategy,
					IdentifierType.ReturnedClass,
					IncrementSize,
					PropertiesHelper.GetInt32(InitialParam, parms, -1)  // Use -1 as default initial value here to signal that it's not set.
				);
		}

		#endregion


		/**
         * Determine the table name to use for the generator values.
         * <p/>
         * Called during {@link #configure configuration}.
         *
         * @see #getTableName()
         * @param params The params supplied in the generator config (plus some standard useful extras).
         * @param dialect The dialect in effect
         * @return The table name to use.
         */
		protected string determineGeneratorTableName(IDictionary<string, string> parms, Dialect.Dialect dialect)
		{
			string name = PropertiesHelper.GetString(TableParam, parms, DefaultTable);
			bool isGivenNameUnqualified = name.IndexOf('.') < 0;
			if (isGivenNameUnqualified)
			{
				//ObjectNameNormalizer normalizer = ( ObjectNameNormalizer ) params.get( IDENTIFIER_NORMALIZER );
				//name = normalizer.normalizeIdentifierQuoting( name );
				//// if the given name is un-qualified we may neen to qualify it
				//string schemaName = normalizer.normalizeIdentifierQuoting( params.getProperty( SCHEMA ) );
				//string catalogName = normalizer.normalizeIdentifierQuoting( params.getProperty( CATALOG ) );

				string schemaName;
				string catalogName;
				parms.TryGetValue(PersistentIdGeneratorParmsNames.Schema, out schemaName);
				parms.TryGetValue(PersistentIdGeneratorParmsNames.Catalog, out catalogName);
				name = Table.Qualify(catalogName, schemaName, name);
			}
			else
			{
				// if already qualified there is not much we can do in a portable manner so we pass it
				// through and assume the user has set up the name correctly.
			}
			return name;
		}

		/**
		 * Determine the name of the column used to indicate the segment for each
		 * row.  This column acts as the primary key.
		 * <p/>
		 * Called during {@link #configure configuration}.
		 *
		 * @see #getSegmentColumnName()
		 * @param params The params supplied in the generator config (plus some standard useful extras).
		 * @param dialect The dialect in effect
		 * @return The name of the segment column
		 */
		protected string determineSegmentColumnName(IDictionary<string, string> parms, Dialect.Dialect dialect)
		{
			//ObjectNameNormalizer normalizer = ( ObjectNameNormalizer ) params.get( IDENTIFIER_NORMALIZER );
			string name = PropertiesHelper.GetString(SegmentColumnParam, parms, DefaultSegmentColumn);
			return name;
			//return dialect.quote( normalizer.normalizeIdentifierQuoting( name ) );
		}

		/**
		 * Determine the name of the column in which we will store the generator persistent value.
		 * <p/>
		 * Called during {@link #configure configuration}.
		 *
		 * @see #getValueColumnName()
		 * @param params The params supplied in the generator config (plus some standard useful extras).
		 * @param dialect The dialect in effect
		 * @return The name of the value column
		 */
		protected string determineValueColumnName(IDictionary<string, string> parms, Dialect.Dialect dialect)
		{
			//ObjectNameNormalizer normalizer = ( ObjectNameNormalizer ) params.get( IDENTIFIER_NORMALIZER );
			string name = PropertiesHelper.GetString(ValueColumnParam, parms, DefaultValueColumn);
			//return dialect.quote( normalizer.normalizeIdentifierQuoting( name ) );
			return name;
		}

		/**
		 * Determine the segment value corresponding to this generator instance.
		 * <p/>
		 * Called during {@link #configure configuration}.
		 *
		 * @see #getSegmentValue()
		 * @param params The params supplied in the generator config (plus some standard useful extras).
		 * @return The name of the value column
		 */
		protected string determineSegmentValue(IDictionary<string, string> parms)
		{
			string segmentValue = PropertiesHelper.GetString(SegmentValueParam, parms, "");
			if (string.IsNullOrEmpty(segmentValue))
				segmentValue = determineDefaultSegmentValue(parms);
			return segmentValue;

			//string segmentValue = params.getProperty( SEGMENT_VALUE_PARAM );
			//if ( StringHelper.isEmpty( segmentValue ) ) {
			//    segmentValue = determineDefaultSegmentValue( params );
			//}
			//return segmentValue;
		}

		/**
		 * Used in the cases where {@link #determineSegmentValue} is unable to
		 * determine the value to use.
		 *
		 * @param params The params supplied in the generator config (plus some standard useful extras).
		 * @return The default segment value to use.
		 */
		protected string determineDefaultSegmentValue(IDictionary<string, string> parms)
		{
			bool preferSegmentPerEntity = PropertiesHelper.GetBoolean(ConfigPreferSegmentPerEntity, parms, false);
			string defaultToUse = preferSegmentPerEntity ? parms[PersistentIdGeneratorParmsNames.Table] : DefaultSegmentValue;

			//LOG.usingDefaultIdGeneratorSegmentValue(tableName, segmentColumnName, defaultToUse);
			return defaultToUse;
		}

		/**
		 * Determine the size of the {@link #getSegmentColumnName segment column}
		 * <p/>
		 * Called during {@link #configure configuration}.
		 *
		 * @see #getSegmentValueLength()
		 * @param params The params supplied in the generator config (plus some standard useful extras).
		 * @return The size of the segment column
		 */
		protected int determineSegmentColumnSize(IDictionary<string, string> parms)
		{
			return PropertiesHelper.GetInt32(SegmentLengthParam, parms, DefaultSegmentLength);
		}

		protected int determineInitialValue(IDictionary<string, string> parms)
		{
			return PropertiesHelper.GetInt32(InitialParam, parms, DefaltInitialValue);
		}

		protected int determineIncrementSize(IDictionary<string, string> parms)
		{
			return PropertiesHelper.GetInt32(IncrementParam, parms, DefaultIncrementSize);
		}

		protected void buildSelectQuery(Dialect.Dialect dialect)
		{
			const string alias = "tbl";
			SqlStringBuilder selectBuilder = new SqlStringBuilder(100);
			selectBuilder.Add("select ").Add(StringHelper.Qualify(alias, ValueColumnName))
				.Add(" from " + TableName + " " + alias + " where ")
				.Add(StringHelper.Qualify(alias, SegmentColumnName) + " = ")
				.Add(Parameter.Placeholder).Add("  ");
			//string query = "select " + StringHelper.Qualify( alias, ValueColumnName ) +
			//        " from " + TableName + ' ' + alias +
			//        " where " + StringHelper.Qualify( alias, SegmentColumnName ) + "=?";
			Dictionary<string, LockMode> lockOptions = new Dictionary<string, LockMode>();
			//LockOptions lockOptions = new LockOptions( LockMode.PESSIMISTIC_WRITE );
			lockOptions[alias] = LockMode.Upgrade;
			//lockOptions.setAliasSpecificLockMode( alias, LockMode.PESSIMISTIC_WRITE );
			Dictionary<string, string[]> updateTargetColumnsMap = new Dictionary<string, string[]> { { alias, new[] { ValueColumnName } } };
			//Map updateTargetColumnsMap = Collections.singletonMap( alias, new string[] { valueColumnName } );
			selectQuery = dialect.ApplyLocksToSql(selectBuilder.ToSqlString(), lockOptions, updateTargetColumnsMap);

			selectParameterTypes = new[] { SqlTypes.SqlTypeFactory.GetAnsiString(SegmentValueLength) };
		}

		protected void buildUpdateQuery()
		{
			SqlStringBuilder builder = new SqlStringBuilder(100);
			builder.Add("update " + TableName)
				.Add(" set ").Add(ValueColumnName).Add(" = ").AddParameter()
				.Add(" where ").Add(ValueColumnName).Add(" = ").AddParameter()
				.Add(" and ").Add(SegmentColumnName).Add(" = ").AddParameter();
			//return "update " + TableName +
			//        " set " + ValueColumnName + "=? " +
			//        " where " + ValueColumnName + "=? and " + SegmentColumnName + "=?";
			updateQuery = builder.ToSqlString();
			updateParameterTypes = new[] { SqlTypes.SqlTypeFactory.Int64, SqlTypes.SqlTypeFactory.Int64, SqlTypes.SqlTypeFactory.GetAnsiString(SegmentValueLength) };
		}

		protected void buildInsertQuery()
		{
			SqlStringBuilder builder = new SqlStringBuilder(100);
			builder.Add("insert into ").Add(TableName).Add(" (" + SegmentColumnName + ", " + ValueColumnName + ") values (").AddParameter()
				.Add(", ").AddParameter().Add(")");
			//return "insert into " + TableName + " (" + SegmentColumnName + ", " + ValueColumnName + ") " + " values (?,?)";
			insertQuery = builder.ToSqlString();
			insertParameterTypes = new[] { SqlTypes.SqlTypeFactory.GetAnsiString(SegmentValueLength), SqlTypes.SqlTypeFactory.Int64 };
		}

		//@Override
		[MethodImpl(MethodImplOptions.Synchronized)]
		public virtual object Generate(ISessionImplementor session, object obj)
		{
			//public synchronized Serializable generate(final SessionImplementor session, Object obj) {
			//final SqlStatementLogger statementLogger = session
			//        .getFactory()
			//        .getServiceRegistry()
			//        .getService( JdbcServices.class )
			//        .getSqlStatementLogger();

			return Optimizer.Generate(new TableAccessCallback(session, this));
		}

		private class TableAccessCallback : IAccessCallback
		{
			private TableGenerator owner;
			private readonly ISessionImplementor session;

			public TableAccessCallback(ISessionImplementor session, TableGenerator owner)
			{
				this.session = session;
				this.owner = owner;
			}
			#region IAccessCallback Members

			public long GetNextValue()
			{
				return Convert.ToInt64(owner.DoWorkInNewTransaction(session));
			}

			#endregion
		}

		public override object DoWorkInCurrentTransaction(ISessionImplementor session, System.Data.IDbConnection conn, System.Data.IDbTransaction transaction)
		{
			//new AccessCallback() {
			//    @Override
			//    public IntegralDataTypeHolder getNextValue() {
			//return session.getTransactionCoordinator().getTransaction().createIsolationDelegate().delegateWork(
			//        new AbstractReturningWork<IntegralDataTypeHolder>() {
			//@Override
			//public IntegralDataTypeHolder execute(Connection connection) throws SQLException {
			//IntegralDataTypeHolder value = IdentifierGeneratorHelper.getIntegralDataTypeHolder( identifierType.getReturnedClass() );
			long result;
			int rows;
			do
			{
				//statementLogger.logStatement( selectQuery, FormatStyle.BASIC.getFormatter() );

				//PreparedStatement selectPS = conn.prepareStatement( selectQuery );
				//try {
				//    selectPS.setString( 1, segmentValue );
				//    ResultSet selectRS = selectPS.executeQuery();
				//SqlTypes.SqlType[] selectParameterTypes = { SqlTypes.SqlTypeFactory.GetAnsiString(SegmentValueLength) };

				IDbCommand cmd = session.Factory.ConnectionProvider.Driver.GenerateCommand(CommandType.Text, selectQuery, selectParameterTypes);
				using (cmd)
				{
					cmd.Connection = conn;
					cmd.Transaction = transaction;
					//cmd.CommandText = selectQuery;
					//var dbParam = cmd.CreateParameter();
					//dbParam.Value = SegmentValue;
					//cmd.Parameters.Add(dbParam);
					string s = cmd.CommandText;
					((IDataParameter)cmd.Parameters[0]).Value = SegmentValue;
					PersistentIdGeneratorParmsNames.SqlStatementLogger.LogCommand(cmd, FormatStyle.Basic);

					object objVal = cmd.ExecuteScalar();
					//if ( !selectRS.next() ) {
					if (objVal == null)
					{
						//value.initialize( initialValue );
						result = InitialValue;
						IDbCommand insertCmd = session.Factory.ConnectionProvider.Driver.GenerateCommand(CommandType.Text, insertQuery, insertParameterTypes);
						using (insertCmd)
						{
							insertCmd.Connection = conn;
							insertCmd.Transaction = transaction;
							//insertCmd.CommandText = insertQuery;
							//var param1 = insertCmd.CreateParameter();
							//param1.Value = SegmentValue;
							//var param2 = insertCmd.CreateParameter();
							//param2.Value = result;
							((IDataParameter)insertCmd.Parameters[0]).Value = SegmentValue;
							((IDataParameter)insertCmd.Parameters[1]).Value = result;

							PersistentIdGeneratorParmsNames.SqlStatementLogger.LogCommand(insertCmd, FormatStyle.Basic);
							insertCmd.ExecuteNonQuery();
						}
						//PreparedStatement insertPS = null;
						//try {
						//    statementLogger.logStatement( insertQuery, FormatStyle.BASIC.getFormatter() );
						//    insertPS = connection.prepareStatement( insertQuery );
						//    insertPS.setString( 1, segmentValue );
						//    value.bind( insertPS, 2 );
						//    insertPS.execute();
						//}
						//finally {
						//    if ( insertPS != null ) {
						//        insertPS.close();
						//    }
						//}
					}
					else
					{
						result = Convert.ToInt64(objVal);
						//value.initialize( selectRS, 1 );
					}
					//selectRS.close();
				}
				//catch ( SQLException e ) {
				//    LOG.unableToReadOrInitHiValue(e);
				//    throw e;
				//}
				//finally {
				//    selectPS.close();
				//}

				IDbCommand updateCmd = session.Factory.ConnectionProvider.Driver.GenerateCommand(CommandType.Text, updateQuery, updateParameterTypes);
				using (updateCmd)
				{
					updateCmd.Connection = conn;
					updateCmd.Transaction = transaction;
					//updateCmd.CommandText = updateQuery;

					int increment = Optimizer.ApplyIncrementSizeToSourceValues ? IncrementSize : 1;
					((IDataParameter)updateCmd.Parameters[0]).Value = result + increment;
					((IDataParameter)updateCmd.Parameters[1]).Value = result;
					((IDataParameter)updateCmd.Parameters[2]).Value = SegmentValue;
					PersistentIdGeneratorParmsNames.SqlStatementLogger.LogCommand(updateCmd, FormatStyle.Basic);
					rows = updateCmd.ExecuteNonQuery();
				}

				//statementLogger.logStatement( updateQuery, FormatStyle.BASIC.getFormatter() );
				//PreparedStatement updatePS = connection.prepareStatement( updateQuery );
				//try {
				//    final IntegralDataTypeHolder updateValue = value.copy();
				//    if ( optimizer.applyIncrementSizeToSourceValues() ) {
				//        updateValue.add( incrementSize );
				//    }
				//    else {
				//        updateValue.increment();
				//    }
				//    updateValue.bind( updatePS, 1 );
				//    value.bind( updatePS, 2 );
				//    updatePS.setString( 3, segmentValue );
				//    rows = updatePS.executeUpdate();
				//}
				//catch ( SQLException e ) {
				//    LOG.unableToUpdateQueryHiValue(tableName, e);
				//    throw e;
				//}
				//finally {
				//    updatePS.close();
				//}
			}
			while (rows == 0);

			TableAccessCount++;

			return result;
		}





		public virtual string[] SqlCreateStrings(Dialect.Dialect dialect)
		{
			return new string[] {dialect.CreateTableString + " " + TableName         + " ("
            +SegmentColumnName+" "+dialect.GetTypeName(SqlTypes.SqlTypeFactory.GetAnsiString( SegmentValueLength)) + " not null, "
            + ValueColumnName+" "+dialect.GetTypeName(SqlTypes.SqlTypeFactory.Int64)+", "
            +dialect.PrimaryKeyString+" ( "+SegmentColumnName+") "
            +")"};
			//        new StringBuffer()
			//                .append( dialect.getCreateTableString() )
			//                .append( ' ' )
			//                .append( tableName )
			//                .append( " ( " )
			//                .append( segmentColumnName )
			//                .append( ' ' )
			//                .append( dialect.getTypeName( Types.VARCHAR, segmentValueLength, 0, 0 ) )
			//                .append( " not null " )
			//                .append( ",  " )
			//                .append( valueColumnName )
			//                .append( ' ' )
			//                .append( dialect.getTypeName( Types.BIGINT ) )
			//                .append( ", primary key ( " )
			//                .append( segmentColumnName )
			//                .append( " ) ) " )
			//                .toString()
			//};
		}


		public virtual string[] SqlDropString(Dialect.Dialect dialect)
		{
			return new[] { dialect.GetDropTableString(TableName) };

			//StringBuffer sqlDropString = new StringBuffer().append( "drop table " );
			//if ( dialect.supportsIfExistsBeforeTableName() ) {
			//    sqlDropString.append( "if exists " );
			//}
			//sqlDropString.append( tableName ).append( dialect.getCascadeConstraintsString() );
			//if ( dialect.supportsIfExistsAfterTableName() ) {
			//    sqlDropString.append( " if exists" );
			//}
			//return new string[] { sqlDropString.toString() };
		}
	}
}
