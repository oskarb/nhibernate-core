using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq.Clauses;
using NHibernate.Linq.Expressions;
using NHibernate.Linq.Visitors;
using Remotion.Linq;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses;


namespace NHibernate.Linq.GroupBy
{
	/// <summary>
	/// Rewrite all where-clauses that reference a subquery with Contains() so
	/// that the SubQueryExpression is wrapped in an NhContainsExpression, instead
	/// of having the ContainsResultOperator inside the subquery's query model.
	/// </summary>
	/// 
	/// The ContainsResultOperator causes problems for the AggregatingGroupByRewriter, 
	/// since the GroupResultOperator it inserts generates a different type than the
	/// one the ContainsResultOperator expects, which will later cause an exception
	/// from ContainsResultOperator.GetOutputDataInfo(). (This is because we have
	/// already rewritten aggregating result operators as expressions in the select
	/// clause.)
	internal class ContainsSubQueryRewriter : NhExpressionTreeVisitor
	{
		public static void ReWrite(QueryModel queryModel)
		{
			var visitor = new ContainsSubQueryRewriter();

			var clauses = queryModel.BodyClauses.OfType<WhereClause>();
			foreach (var whereClause in clauses)
				whereClause.TransformExpressions(visitor.VisitExpression);
		}


		private ContainsSubQueryRewriter()
		{

		}


		protected override System.Linq.Expressions.Expression VisitSubQueryExpression(SubQueryExpression subQueryExpression)
		{
			var contains = subQueryExpression.QueryModel.ResultOperators.LastOrDefault() as ContainsResultOperator;
			if (contains != null)
			{
				var lastIdx = subQueryExpression.QueryModel.ResultOperators.Count - 1;
				subQueryExpression.QueryModel.ResultOperators.RemoveAt(lastIdx);
				return new NhContainsExpression(contains.Item, subQueryExpression);
			}

			return subQueryExpression;
		}
	}
}