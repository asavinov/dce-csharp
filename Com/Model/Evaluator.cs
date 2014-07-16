using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Com.Query;

using Offset = System.Int32;

// Possible evaluators: Expr (based on expression as code), MappingEvaluator (based on Mapping, essentially a tuple), AstNode evaluator, source code evaluator, external library evaluator
// Also, there are at least two types of evaluators: normal (set/assign), and aggregation (update, accumulate)
namespace Com.Model
{

    public class ExprEvaluator : CsColumnEvaluator
    {
        ExprNode exprNode;
        CsVariable thisVariable;

        CsColumnData columnData;

        //
        // CsColumnEvaluator interface
        //

        protected CsTable loopTable;
        public CsTable LoopTable { get { return loopTable; } }

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public object Evaluate(Offset input)
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(input);

            // evaluate the expression
            exprNode.Evaluate();

            // Write the result value to the function
            columnData.SetValue(input, exprNode.Result);

            return null;
        }

        public object EvaluateUpdate(Offset input) { return null; }

        public bool EvaluateJoin(Offset input, object output) { return false; }

        public ExprEvaluator(CsColumn column)
        {
            loopTable = column.LesserSet;
            isUpdate = false;
            exprNode = column.ColumnDefinition.Formula;
            thisVariable = new Variable("this", LoopTable.Name);
            thisVariable.TypeTable = LoopTable;
            columnData = column.ColumnData;

            // Resolve names in the expresion by storing direct references to storage objects which will be used during valuation (names will not be used
            exprNode.Resolve(column.LesserSet.Top, new List<CsVariable>() { thisVariable });
        }
    }

}
