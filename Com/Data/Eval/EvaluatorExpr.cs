using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using System.Collections;

using Com.Schema;

using Rowid = System.Int32;

// Possible evaluators: Expr (based on expression as code), MappingEvaluator (based on Mapping, essentially a tuple), AstNode evaluator, source code evaluator, external library evaluator
// Also, there are at least two types of evaluators: normal (set/assign), and aggregation (update, accumulate)
namespace Com.Data.Eval
{

    public class EvaluatorExpr : DcEvaluator
    {
        protected DcColumnData columnData;

        // Loop
        protected Rowid thisCurrent;
        protected DcTable thisTable;
        protected DcVariable thisVariable; // Stores current input (offset in a local set or reference to the current DataRow)

        // Output expression
        protected ExprNode outputExpr; // Can contain more specific nodes OledbExprNode to access attributes in DataRow

        //
        // DcIterator interface
        //

        public DcWorkspace Workspace { get; set; }

        public virtual bool NextInput()
        {
            if (thisCurrent < thisTable.Data.Length) thisCurrent++;

            if (thisCurrent < thisTable.Data.Length) return true;
            else return false;
        }
        public virtual bool FirstInput()
        {
            thisCurrent = 0;

            if (thisCurrent < thisTable.Data.Length) return true;
            else return false;
        }
        public virtual bool LastInput()
        {
            thisCurrent = thisTable.Data.Length - 1;

            if (thisCurrent >= 0) return true;
            else return false;
        }

        public virtual object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(thisCurrent);

            // evaluate the expression
            outputExpr.Evaluate();

            // Write the result value to the function
            if (columnData != null)
            {
                columnData.SetValue(thisCurrent, outputExpr.OutputVariable.GetValue());
            }

            return outputExpr.OutputVariable.GetValue();
        }

        public virtual object GetOutput() 
        { 
            return outputExpr.OutputVariable.GetValue(); 
        }

        public EvaluatorExpr(DcColumn column)
        {
            Workspace = column.Input.Schema.Workspace;
            columnData = column.Data;

            // Loop
            thisCurrent = -1;
            thisTable = column.Input;
            thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
            thisVariable.TypeSchema = thisTable.Schema;
            thisVariable.TypeTable = thisTable;

            // Output expression
            if (column.Definition.Mapping != null)
            {
                if (column.Definition.IsAppendData)
                {
                    outputExpr = column.Definition.Mapping.BuildExpression(ActionType.APPEND);
                }
                else
                {
                    outputExpr = column.Definition.Mapping.BuildExpression(ActionType.READ);
                }
            }
            else if (column.Definition.FormulaExpr != null)
            {
                outputExpr = column.Definition.FormulaExpr;

                if (column.Definition.DefinitionType == DcColumnDefinitionType.LINK)
                {
                    // Adjust the expression according to other parameters of the definition
                    if(column.Definition.IsAppendData) {
                        outputExpr.Action = ActionType.APPEND;
                    }
                    else
                    {
                        outputExpr.Action = ActionType.READ;
                    } 
                }
            }

            outputExpr.OutputVariable.SchemaName = column.Output.Schema.Name;
            outputExpr.OutputVariable.TypeName = column.Output.Name;
            outputExpr.OutputVariable.TypeSchema = column.Output.Schema;
            outputExpr.OutputVariable.TypeTable = column.Output;

            outputExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });
        }

        public EvaluatorExpr(DcTable table)
        {
            Workspace = table.Schema.Workspace;
            columnData = null;

            // Loop
            thisCurrent = -1;
            thisTable = table;
            thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");

            thisVariable.TypeSchema = thisTable.Schema;
            thisVariable.TypeTable = thisTable;

            // Outtput expression
            outputExpr = table.Definition.WhereExpr;
            outputExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });
        }

        public EvaluatorExpr()
        {
        }
    }

}
