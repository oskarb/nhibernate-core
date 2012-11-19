using System.Linq.Expressions;

namespace NHibernate.Linq.Expressions
{
	public class NhContainsExpression : Expression
	{
		public Expression ElementsExpression { get; private set; }
		public Expression ItemExpression { get; private set; }

		public NhContainsExpression(Expression itemExpression, Expression elementsExpression)
			: base((ExpressionType)NhExpressionType.Contains, typeof(bool))
		{
			ItemExpression = itemExpression;
			ElementsExpression = elementsExpression;
		}
	}
}