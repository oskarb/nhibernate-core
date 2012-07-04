#if NET_4_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

// ReSharper disable CheckNamespace
namespace Iesi.Collections.Generic
// ReSharper restore CheckNamespace
{
	/// <summary>
	/// The HashedSet&lt;T&gt; class simply inherits from the BCL's HashSet&lt;T&gt;,
	/// with no added features. It is simply a compatibility measure to make it easier
	/// to support both .Net 3.5 (with the "real" HashedSet&lt;T&gt; from Iesi) and
	/// .Net 4.0 (with this compatibility shim) in the NHibernate code base.
	/// The Iesi and BCL ISet&lt;T&gt; API differs a bit, but very little of the differences
	/// are actually in use in NHibernate.
	/// </summary>
	[Serializable]
	public class HashedSet<T> : HashSet<T>
	{
		public HashedSet()
		{
		}


		public HashedSet(ICollection<T> initialValues)
			: base(initialValues)
		{
		}


		protected HashedSet(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}


	/// <summary>
	/// TODO: Complete implementation of OrderedSet&lt;T&gt; for BCL ISet&lt;T&gt;.
	/// Currently, the implementation is based on HashSet, and so does not necessarily
	/// preserve ordering.
	/// </summary>
	[Serializable]
	public class OrderedSet<T> : HashSet<T>
	{
		public OrderedSet()
		{
		}


		public OrderedSet(ICollection<T> initialValues)
			: base(initialValues)
		{
		}

	}


	/// <summary>
	/// TODO: Complete implementation of an immutable set based on HashSet().
	/// Currently, the implementation isn't actually immutable/readonly. On the
	/// other hand, currently it is only used in a single place (AssignmentSpecification.cs)
	/// so it might be entirely reasonable to just remove it. In any case, if it's
	/// implemented correctly, one should also consider which namespace/assembly it belongs to.
	/// </summary>
	[Serializable]
	public class ImmutableSet<T> : HashSet<T>
	{
		public ImmutableSet()
		{
		}


		public ImmutableSet(ICollection<T> initialValues)
			: base(initialValues)
		{
		}

	}


	public static class ISetExtensions
	{
		public static void AddAll<T>(this ISet<T> set, IEnumerable<T> items)
		{
			foreach (var item in items)
				set.Add(item);
		}


		public static void RemoveAll<T>(this ISet<T> set, IEnumerable<T> items)
		{
			foreach (var item in items)
				set.Remove(item);
		}
	}
}
#endif
