using System.Collections;
using NUnit.Framework;
using NHibernate.Id.Enhanced;
using System.Collections.Generic;

namespace NHibernate.Test.Generatedkeys.EnhancedTableIdentity
{
	[TestFixture]
	public class EnhancedTableIdentityFixture2 : EnhancedTableIdentityFixture
	{
	}


	[TestFixture]
	public class EnhancedTableIdentityFixture : TestCase
	{
		protected override IList Mappings
		{
			get { return new[] { "Generatedkeys.EnhancedTableIdentity.MyEntity.hbm.xml" }; }
		}

		protected override string MappingsAssembly
		{
			get { return "NHibernate.Test"; }
		}

		protected override void AddMappings(Cfg.Configuration configuration)
		{
			// Set some properties that must be set before the mappings are added.
			// (The overridable Configure(cfg) is called AFTER AddMappings(cfg).)
			configuration.SetProperty(TableGenerator.ConfigPreferSegmentPerEntity, "true");
			//configuration.SetProperty(Cfg.Environment.PreferPooledValuesLo, "true");

			base.AddMappings(configuration);
		}


		protected override void Configure(Cfg.Configuration configuration)
		{
			base.Configure(configuration);


			//configuration.SetProperty(Cfg.Environment.DefaultSchema, "fooo");

		}


		[Test]
		public void EnhancedTableGenerator()
		{
			ISession session = OpenSession();
			session.BeginTransaction();

			for (int i = 1; i < 100; i++)
			{
				var e = new MyEntity { Name = "entity-1" };
				session.Save(e);
				Assert.That(e.Id, Iz.EqualTo(i));
			}

			var e1 = new MyEntity { Name = "entity-1" };
			session.Save(e1);
			var e2 = new MyEntity { Name = "entity-2" };
			session.Save(e2);

			Assert.That(e1.Id, Iz.EqualTo(100));
			Assert.That(e2.Id, Iz.EqualTo(101));


			// this insert should happen immediately!
			//Assert.AreEqual(1, e.Id, "id not generated through forced insertion");

			var seqVals = ReadSequences(session);
			Assert.That(seqVals.Keys, Has.Member("default"));
			Assert.That(seqVals["default"], Iz.EqualTo(11));  // 1 is assigned, pool size is 10, so next free in table should be 11, because pool optimizer.

			session.Delete(e1);
			session.Delete(e2);
			session.Transaction.Commit();
			session.Close();
		}


		protected IDictionary<string, long> ReadSequences(ISession session)
		{
			Dictionary<string, long> sequences = new Dictionary<string, long>();

			using (var cmd = session.Connection.CreateCommand())
			{
				session.Transaction.Enlist(cmd);

				cmd.CommandText = "select sequence_name, next_val from hibernate_sequences";
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
						sequences[reader.GetString(0)] = reader.GetInt64(1);
				}
			}

			return sequences;
		}
	}
}
