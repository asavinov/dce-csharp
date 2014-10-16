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

    public class ExprEvaluator : ComEvaluator
    {
        protected ComColumnData columnData;

        // Loop
        protected Offset thisCurrent;
        protected ComTable thisTable;
        protected ComVariable thisVariable; // Stores current input (offset in a local set or reference to the current DataRow)

        // Output expression
        protected ExprNode outputExpr; // Can contain more specific nodes OledbExprNode to access attributes in DataRow

        //
        // ComColumnEvaluator interface
        //

        public virtual bool Next()
        {
            if (thisCurrent < thisTable.Data.Length) thisCurrent++;

            if (thisCurrent < thisTable.Data.Length) return true;
            else return false;
        }
        public virtual bool First()
        {
            thisCurrent = 0;

            if (thisCurrent < thisTable.Data.Length) return true;
            else return false;
        }
        public virtual bool Last()
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
                columnData.SetValue(thisCurrent, outputExpr.Result.GetValue());
            }

            return outputExpr.Result.GetValue();
        }

        public virtual object EvaluateUpdate() { return null; }

        public virtual bool EvaluateJoin(object output) { return false; }

        public virtual object GetResult() 
        { 
            return outputExpr.Result.GetValue(); 
        }

        public ExprEvaluator(ComColumn column)
        {
            columnData = column.Data;

            // Loop
            thisCurrent = -1;
            thisTable = column.Input;
            thisVariable = new Variable("this", thisTable.Name);
            thisVariable.TypeTable = thisTable;

            // Output expression
            if (column.Definition.Mapping != null)
            {
                if (column.Definition.IsGenerating)
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
            }

            outputExpr.Result.TypeName = column.Output.Name;
            outputExpr.Result.TypeTable = column.Output;

            outputExpr.Resolve(column.Input.Schema, new List<ComVariable>() { thisVariable });
        }

        public ExprEvaluator(ComTable table)
        {
            columnData = null;

            // Loop
            thisCurrent = -1;
            thisTable = table;
            thisVariable = new Variable("this", thisTable.Name);
            thisVariable.TypeTable = thisTable;

            // Outtput expression
            outputExpr = table.Definition.WhereExpr;
            outputExpr.Resolve(thisTable.Schema, new List<ComVariable>() { thisVariable });
        }

        public ExprEvaluator()
        {
        }
    }

    /// <summary>
    /// Notes:
    /// - distinguish between this table (where the aggregated column is defined, and a fact table which provides values to be aggregated where group and measure functions are defined.
    /// - the way of aggregation is defined as an updater expression which knows how to compute a new value given the old (current) value and a new measure.
    /// </summary>
    public class AggrEvaluator : ExprEvaluator
    {
        // base::columnData is the aggregated function to be computed

        // Facts
        // base::thisCurrent is offset in the fact table
        // base::thisTable is a fact set which is iterated in this class
        // base::thisVariable stores current fact in the loop table. is used by group expr and meausre expr

        // Groups
        protected ComVariable groupVariable; // Stores current group (input for the aggregated function)
        protected ExprNode groupExpr; // Returns a group this fact belongs to, is stored in the group variable

        // Measure
        protected ComVariable measureVariable; // Stores new value (output for the aggregated function)
        protected ExprNode measureExpr; // Returns a new value to be aggregated with the old value, is stored in the measure variable

        // Updater/aggregation function
        // base::outputExpr - updater expression. works in the context of two variables: group and measure

        //
        // ComColumnEvaluator interface
        //

        public override object Evaluate()
        {
            //
            // Evalute group and measure expressions for the current fact
            //

            // Use input value to evaluate the expression
            thisVariable.SetValue(thisCurrent);

            groupExpr.Evaluate();
            Offset groupElement = (Offset)groupExpr.Result.GetValue();
            groupVariable.SetValue(groupElement);

            measureExpr.Evaluate();
            object measureValue = measureExpr.Result.GetValue();
            measureVariable.SetValue(measureValue);

            //
            // Evaluate the update expression and store the new computed value
            //
            outputExpr.Evaluate();

            object newValue = outputExpr.Result.GetValue();
            columnData.SetValue(groupElement, newValue);

            return outputExpr.Result.GetValue();
        }

        public AggrEvaluator(ComColumn column) // Create evaluator from structured definition
        {
            columnData = column.Data;

            if (column.Definition.FormulaExpr == null) // From structured definition (parameters)
            {
                // Facts
                thisCurrent = -1;
                thisTable = column.Definition.FactTable;

                thisVariable = new Variable("this", thisTable.Name);
                thisVariable.TypeTable = thisTable;

                // Groups
                groupExpr = ExprNode.CreateReader(column.Definition.GroupPaths[0], true); // Currently only one path is used
                groupExpr = (ExprNode)groupExpr.Root;
                groupExpr.Resolve(thisTable.Schema, new List<ComVariable>() { thisVariable });

                groupVariable = new Variable("this", column.Input.Name);
                groupVariable.TypeTable = column.Input;

                // Measure
                measureExpr = ExprNode.CreateReader(column.Definition.MeasurePaths[0], true);
                measureExpr = (ExprNode)measureExpr.Root;
                measureExpr.Resolve(thisTable.Schema, new List<ComVariable>() { thisVariable });

                measureVariable = new Variable("value", column.Output.Name);
                measureVariable.TypeTable = column.Output;

                // Updater/aggregation function
                outputExpr = ExprNode.CreateUpdater(column, column.Definition.Updater);
                outputExpr.Resolve(column.Input.Schema, new List<ComVariable>() { groupVariable, measureVariable });
            }
            else // From expression
            {
                //
                // Extract all aggregation components from expression (aggregation expression cannot be resolved)
                //
                ExprNode aggExpr = column.Definition.FormulaExpr;

                // Facts
                ExprNode factsNode = aggExpr.GetChild("facts").GetChild(0);
                string thisTableName = factsNode.Name;

                thisCurrent = -1;
                thisTable = column.Input.Schema.GetSubTable(thisTableName);

                thisVariable = new Variable("this", thisTable.Name);
                thisVariable.TypeTable = thisTable;

                // Groups
                ExprNode groupsNode = aggExpr.GetChild("groups").GetChild(0);
                groupExpr = groupsNode;
                groupExpr.Resolve(thisTable.Schema, new List<ComVariable>() { thisVariable });

                groupVariable = new Variable("this", column.Input.Name);
                groupVariable.TypeTable = column.Input;

                // Measure
                ExprNode measureNode = aggExpr.GetChild("measure").GetChild(0);
                measureExpr = measureNode;
                measureExpr.Resolve(thisTable.Schema, new List<ComVariable>() { thisVariable });

                measureVariable = new Variable("value", column.Output.Name);
                measureVariable.TypeTable = column.Output;

                // Updater/aggregation function
                ExprNode updaterExpr = aggExpr.GetChild("aggregator").GetChild(0);

                outputExpr = ExprNode.CreateUpdater(column, updaterExpr.Name);
                outputExpr.Resolve(column.Input.Schema, new List<ComVariable>() { groupVariable, measureVariable });
            }
        }

    }

    public class CsvEvaluator : ExprEvaluator
    {
        protected string[] currentRecord;
        protected ConnectionCsv connectionCsv;

        //
        // ComColumnEvaluator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public override bool Next()
        {
            currentRecord = connectionCsv.CurrentRecord;

            if (currentRecord == null) return false;

            thisVariable.SetValue(currentRecord);

            connectionCsv.Next(); // We increment after iteration because csv is opened with first record initialized
            thisCurrent++;

            return true;
        }

        public override object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(currentRecord);

            // evaluate the expression
            outputExpr.Evaluate();

            // Write the result value to the function
            // columnData.SetValue(currentElement, outputExpr.Result.GetValue()); // We do not store import functions (we do not need this data)

            return null;
        }

        public CsvEvaluator(ComColumn column)
            : base(column)
        {
            // Produce a result set that can be iterated through
            connectionCsv = ((SetTopCsv)thisTable.Schema).connection;
            connectionCsv.Open((SetCsv)thisTable);

            thisCurrent = 0;
            currentRecord = connectionCsv.CurrentRecord; // Start from the first record
        }
    }

    public class OledbEvaluator : ExprEvaluator
    {
        protected DataRow currentRow;
        protected IEnumerator rows;
        protected DataTable dataTable;

        //
        // ComColumnEvaluator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public override bool Next()
        {
            thisCurrent++;

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
            outputExpr.Evaluate();

            // Write the result value to the function
            // columnData.SetValue(currentElement, outputExpr.Result.GetValue()); // We do not store import functions (we do not need this data)

            return null;
        }

        public OledbEvaluator(ComColumn column)
            : base(column)
        {
            // Produce a result set from the remote database by executing a query on the source table
            dataTable = ((SetTopOledb)thisTable.Schema).LoadTable(thisTable);
            rows = dataTable.Rows.GetEnumerator();
        }
    }

}
