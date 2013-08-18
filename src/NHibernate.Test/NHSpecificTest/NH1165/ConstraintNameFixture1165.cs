using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

			return mapper.CompileMappingForAllExplicitlyAddedEntities();
		}

		protected override void Configure(Configuration configuration)
		{
			base.Configure(configuration);
			this.configuration = configuration;
		}

		private Configuration configuration;


		private string GenerateScript()
		{
			StringBuilder builder = new StringBuilder();
			SchemaExport export = new SchemaExport(configuration);
			export.Execute(l => builder.AppendLine(l), false, false);

			var script = builder.ToString();
			return script;
		}


		[Test]
		public void ShouldNamePrimaryKeyConstraint()
		{
			string script = GenerateScript();

			// Primary key is named
			Assert.IsTrue(script.Contains("constraint PK_Book primary key (Id)"), "Primary Key should have name.");
		}

		
		[Test]
		public void ShouldNotNameSimpleUniqueConstraint()
		{
			string script = GenerateScript();

			// unique="true" is NOT named
			Assert.IsTrue(script.Contains("ISBN_10 NVARCHAR(255) not null unique"), "unique should output 'unique' on column.");
		}



		[Test]
		public void ShouldNameConstraintForSingleColumnUniqueKey()
		{
			string script = GenerateScript();

			// unique-key="UQ_ISBN_13" is named
			Assert.IsTrue(script.Contains("ISBN_13 NVARCHAR(255) not null"), "unique-key should output column");
			Assert.IsFalse(script.Contains("ISBN_13 NVARCHAR(255) not null unique"),
						   "unique-key should NOT output 'unique' on column");
			Assert.IsTrue(script.Contains("constraint UQ_ISBN_13 unique (ISBN_13)"), "unique-key should output named constraint");
		}


		[Test]
		public void ShouldNameConstraintForMultiColumnUniqueKey()
		{
			string script = GenerateScript();

			// compound unique-key is named
			Assert.IsTrue(script.Contains("constraint UQ_Author_Title unique (Author, Title)"),
						  "unique-key should output named constraint for compound keys");
		}
	}
}