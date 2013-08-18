using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Cfg;
using NHibernate.Cfg.MappingSchema;
using NHibernate.Connection;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Linq;
using NHibernate.Mapping.ByCode;
using NHibernate.Tool.hbm2ddl;
using NUnit.Framework;

namespace NHibernate.Test.NHSpecificTest.NH1165
{
	public class ConstraintNameFixture1165 : TestCaseMappingByCode
	{
		protected override HbmMapping GetMappings()
		{
			var mapper = new ModelMapper();
			mapper.Class<Book>(rc =>
				{
					rc.Id(x => x.Id, m => m.Generator(Generators.Native));
					rc.Property(x => x.ISBN_10, map =>
						{
							map.Unique(true);
							map.NotNullable(true);
						});
					rc.Property(x => x.ISBN_13, map =>
						{
							map.UniqueKey("UQ_ISBN_13");
							map.NotNullable(true);
						});
					rc.Property(x => x.Author, map => map.UniqueKey("UQ_Author_Title"));
					rc.Property(x => x.Title, map => map.UniqueKey("UQ_Author_Title"));
				});


			mapper.Class<Thing>(rc =>
			{
				rc.Id(x => x.Id, m => m.Generator(Generators.Identity));
				rc.Table("`Thing`");
				rc.Property(x => x.Name, map => map.UniqueKey("UQ_THING_NAME"));
			});


			mapper.Class<Animal>(rc =>
				{
					rc.Id(x => x.Id, m => m.Generator(Generators.Assigned));
					rc.Property(x => x.Name, map => map.UniqueKey("UQ_ANIMAL_NAME"));
				});

			mapper.UnionSubclass<Pig>(rc =>
				{
					rc.Table("`Pig`");
				});
			mapper.UnionSubclass<Sheep>(rc =>
				{
					rc.Schema("foo");
					rc.Table("Animal");
				});

			return mapper.CompileMappingForAllExplicitlyAddedEntities();
		}

		protected override void Configure(Configuration configuration)
		{
			base.Configure(configuration);
			_configuration = configuration;
		}

		private Configuration _configuration;


		private string GenerateScript()
		{
			var builder = new StringBuilder();
			var export = new SchemaExport(_configuration);
			export.Execute(l => builder.AppendLine(l), false, false);

			var script = builder.ToString();
			return script;
		}


		[Test]
		public void ShouldNamePrimaryKeyConstraint()
		{
			string script = GenerateScript();

			// Primary key is named
			Assert.That(script, Is.StringContaining("constraint PK_Book primary key (Id)"), "Primary Key should have name.");
		}


		[Test]
		public void ShouldQuotePrimaryKeyConstraintNameInQuotedTable()
		{
			string script = GenerateScript();

			// Primary key is named
			Assert.That(script, Is.StringContaining("constraint " + Dialect.QuoteForTableName("PK_Thing") + " primary key (Id)"),
			            "Primary Key should have name.");
		}

		
		[Test]
		public void ShouldNotNameSimpleUniqueConstraint()
		{
			string script = GenerateScript();

			// unique="true" is NOT named
			Assert.That(script, Is.StringContaining("ISBN_10 NVARCHAR(255) not null unique"),
			            "unique should output 'unique' on column.");
		}



		[Test]
		public void ShouldNameConstraintForSingleColumnUniqueKey()
		{
			string script = GenerateScript();

			// unique-key="UQ_ISBN_13" is named
			Assert.That(script, Is.StringContaining("ISBN_13 NVARCHAR(255) not null"), "unique-key should output column");
			Assert.That(script, Is.Not.StringContaining("ISBN_13 NVARCHAR(255) not null unique"),
			            "unique-key should NOT output 'unique' on column");
			Assert.That(script, Is.StringContaining("constraint UQ_ISBN_13 unique (ISBN_13)"),
			            "unique-key should output named constraint");
		}


		[Test]
		public void ShouldNameConstraintForMultiColumnUniqueKey()
		{
			string script = GenerateScript();

			// compound unique-key is named
			Assert.That(script, Is.StringContaining("constraint UQ_Author_Title unique (Author, Title)"),
			            "unique-key should output named constraint for compound keys");
		}


		[Test]
		public void ShouldAppendTableNameToInheritedConstraintNames()
		{
			string script = GenerateScript();

			// In the table for the base class, the constraint name is used directly.
			Assert.That(script, Is.StringContaining("constraint UQ_ANIMAL_NAME unique (Name)"),
			            "unique-key should output named constraint");
			
			// In the table for the Pig subclass, the constraint name should be suffixed with the table name.
			// Since the subclass is quoted, the constraint name must also be quoted.
			Assert.That(script,
			            Is.StringContaining("constraint " + Dialect.QuoteForTableName("UQ_ANIMAL_NAME_Pig") + " unique (Name)"),
			            "unique-key should output named constraint");

			// The table for the Sheep subclass is in a different schema, and for "inexplicable" reason is named the
			// same as the table for the base class. The constraint name should be suffixed with the table name, even
			// though it might not be absolutely required.
			Assert.That(script, Is.StringContaining("constraint UQ_ANIMAL_NAME_Animal unique (Name)"),
			            "unique-key should output named constraint");
		}
	}
}