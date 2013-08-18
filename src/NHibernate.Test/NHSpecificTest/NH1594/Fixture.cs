using System.Reflection;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NUnit.Framework;

namespace NHibernate.Test.NHSpecificTest.NH1594
{
    [TestFixture]
    public class Fixture
    {
        [Test]
        public void ShouldUseCorrectPrecisionAndScaleForDecimalColumn()
        {
            Configuration cfg = new Configuration();
            Assembly assembly = Assembly.GetExecutingAssembly();
            cfg.AddResource("NHibernate.Test.NHSpecificTest.NH1594.Mappings.hbm.xml", assembly);

            string[] script = cfg.GenerateSchemaCreationScript(new MsSql2000Dialect());

            Assert.That(script[0],
                        Is.StringContaining(
                            "create table A (id INT IDENTITY NOT NULL, Foo DECIMAL(4, 2) null, constraint PK_A primary key (id))").IgnoreCase,
                        "when using decimal(precision,scale) Script should contain the correct create table statement");
        }
    }
}