using System.Linq.Expressions;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses.StreamedData;

namespace NHibernate.Linq.ResultOperators
{
	public class NhContainsResultOperator : ContainsResultOperator
	{
		public NhContainsResultOperator(Expression item)
			: base(item)
		{
		}


		public override IStreamedDataInfo GetOutputDataInfo(IStreamedDataInfo inputInfo)
		{
			// The default ContainsResultOperator will verify that the type of the inputInfo
			// is compatible with the type of the Item expression. Due to NH rewriting, sometimes
			// this doesn't match even though the resulting SQL will be fine. 
			//
			// So overrule the check in the base class.
			
			return new StreamedScalarValueInfo(typeof(bool));
		}
	}
}