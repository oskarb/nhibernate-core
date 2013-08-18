using System;

namespace NHibernate.Test.NHSpecificTest.NH1165
{
	public class Book
	{
		public virtual int Id { get; set; }
		public virtual string ISBN_10 { get; set; }
		public virtual string ISBN_13 { get; set; }
		public virtual string Author { get; set; }
		public virtual string Title { get; set; }
	}

	public class Thing
	{
		public virtual int Id { get; set; }
		public virtual string Name { get; set; }
	}

	public class Animal
	{
		public virtual int Id { get; set; }
		public virtual string Name { get; set; }
	}

	public class Pig : Animal
	{
	}

	public class Sheep : Animal
	{
	}
}