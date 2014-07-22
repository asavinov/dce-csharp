using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using System.Collections;

using Com.Query;

using Offset = System.Int32;

// Possible evaluators: Expr (based on expression as code), MappingEvaluator (based on Mapping, essentially a tuple), AstNode evaluator, source code evaluator, external library evaluator
// Also, there are at least two types of evaluators: normal (set/assign), and aggregation (update, accumulate)
namespace Com.Model
{

    public class ExprEvaluator : CsColumnEvaluator
    {
        protected ExprNode exprNode; // Can contain more specific nodes OledbExprNode to access attributes in DataRow

        protected CsVariable thisVariable; // Stores as a value a reference to the current DataRow (incremented for each iteration)

        protected Offset currentElement;

        protected CsColumnData columnData;

        protected CsTable loopTable;

        //
        // CsColumnEvaluator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public virtual bool Next()
        {
            if (currentElement < loopTable.TableData.Length) currentElement++;

            if (currentElement < loopTable.TableData.Length) return true;
            else return false;
        }

        public virtual object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(currentElement);

            // evaluate the expression
            exprNode.Evaluate();

            // Write the result value to the function
            columnData.SetValue(currentElement, exprNode.Result.GetValue());

            return null;
        }

        public virtual object EvaluateUpdate() { return null; }

        public virtual bool EvaluateJoin(object output) { return false; }

        public virtual ExprNode GetOutput() { return exprNode; }

        public ExprEvaluator(CsColumn column)
        {
            if (column.ColumnDefinition.Mapping != null)
            {
                exprNode = column.ColumnDefinition.Mapping.BuildExpression();
            }
            else if (column.ColumnDefinition.Formula != null)
            {
                exprNode = column.ColumnDefinition.Formula;
            }

            currentElement = -1;
            loopTable = column.LesserSet;
            isUpdate = false;
            thisVariable = new Variable("this", loopTable.Name);
            thisVariable.TypeTable = loopTable;
            columnData = column.ColumnData;

            // Resolve names in the expresion by storing direct references to storage objects which will be used during valuation (names will not be used
            exprNode.Resolve(column.LesserSet.Top, new List<CsVariable>() { thisVariable });
        }
    }

    public class OledbEvaluator : ExprEvaluator
    {
        protected DataRow currentRow;
        protected IEnumerator rows;
        protected DataTable dataTable;

        //
        // CsColumnEvaluator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public override bool Next()
        {
            currentElement++;
            bool res = rows.MoveNext();
            currentRow = (DataRow)rows.Current;
            thisVariable.SetValue(currentRow);
            return res;
        }

        public override object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(currentRow);

            // evaluate the expression
            exprNode.Evaluate();

            // Write the result value to the function
            // columnData.SetValue(currentElement, exprNode.Result.GetValue()); // We do not store import functions (we do not need this data)

            return null;
        }

        public override object EvaluateUpdate() { return null; }

        public override bool EvaluateJoin(object output) { return false; }

        public OledbEvaluator(CsColumn column)
            : base(column)
        {
            // Produce a result set from the remote database by executing a query on the source table
            dataTable = ((SetTopOledb)loopTable.Top).LoadTable(loopTable);
            rows = dataTable.Rows.GetEnumerator();
        }
    }

}
