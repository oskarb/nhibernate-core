using System.Collections;
using NUnit.Framework;

namespace NHibernate.Test.Generatedkeys.EnhancedTableIdentity
{
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

        //protected override bool AppliesTo(Dialect.Dialect dialect)
        //{
        //    return dialect.SupportsSequences;
        //}

		[Test]
		public void EnhancedTableGenerator()
		{
			ISession session = OpenSession();
			session.BeginTransaction();

			var e = new MyEntity{Name="entity-1"};
			session.Save(e);

            Assert.That(e.Id, Iz.EqualTo(1));

			// this insert should happen immediately!
            //Assert.AreEqual(1, e.Id, "id not generated through forced insertion");

			session.Delete(e);
			session.Transaction.Commit();
			session.Close();
		}
	}
}