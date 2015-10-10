using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;

using Rowid = System.Int32;

namespace Com.Data.Eval
{
    /// <summary>
    /// Notes:
    /// - distinguish between this table (where the aggregated column is defined, and a fact table which provides values to be aggregated where group and measure functions are defined.
    /// - the way of aggregation is defined as an updater expression which knows how to compute a new value given the old (current) value and a new measure.
    /// </summary>
    public class EvaluatorAggr : EvaluatorExpr
    {
        // base::columnData is the aggregated function to be computed

        // Facts
        // base::thisCurrent is offset in the fact table
        // base::thisTable is a fact set which is iterated in this class
        // base::thisVariable stores current fact in the loop table. is used by group expr and meausre expr

        // Groups
        protected DcVariable groupVariable; // Stores current group (input for the aggregated function)
        protected ExprNode groupExpr; // Returns a group this fact belongs to, is stored in the group variable

        // Measure
        protected DcVariable measureVariable; // Stores new value (output for the aggregated function)
        protected ExprNode measureExpr; // Returns a new value to be aggregated with the old value, is stored in the measure variable

        // Updater/aggregation function
        // base::outputExpr - updater expression. works in the context of two variables: group and measure

        //
        // DcIterator interface
        //

        public override object Evaluate()
        {
            //
            // Evalute group and measure expressions for the current fact
            //

            // Use input value to evaluate the expression
            thisVariable.SetValue(thisCurrent);

            groupExpr.Evaluate();
            Rowid groupElement = (Rowid)groupExpr.OutputVariable.GetValue();
            groupVariable.SetValue(groupElement);

            measureExpr.Evaluate();
            object measureValue = measureExpr.OutputVariable.GetValue();
            measureVariable.SetValue(measureValue);

            //
            // Evaluate the update expression and store the new computed value
            //
            outputExpr.Evaluate();

            object newValue = outputExpr.OutputVariable.GetValue();
            columnData.SetValue(groupElement, newValue);

            return outputExpr.OutputVariable.GetValue();
        }

        public EvaluatorAggr(DcColumn column) // Create evaluator from structured definition
        {
            Workspace = column.Input.Schema.Workspace;
            columnData = column.Data;

            if(column.Definition.FormulaExpr != null) // From expression
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

                thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
                thisVariable.TypeSchema = thisTable.Schema;
                thisVariable.TypeTable = thisTable;

                // Groups
                ExprNode groupsNode = aggExpr.GetChild("groups").GetChild(0);
                groupExpr = groupsNode;
                groupExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

                groupVariable = new Variable(column.Input.Schema.Name, column.Input.Name, "this");
                groupVariable.TypeSchema = column.Input.Schema;
                groupVariable.TypeTable = column.Input;

                // Measure
                ExprNode measureNode = aggExpr.GetChild("measure").GetChild(0);
                measureExpr = measureNode;
                measureExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

                measureVariable = new Variable(column.Output.Schema.Name, column.Output.Name, "value");
                measureVariable.TypeSchema = column.Output.Schema;
                measureVariable.TypeTable = column.Output;

                // Updater/aggregation function
                ExprNode updaterExpr = aggExpr.GetChild("aggregator").GetChild(0);

                outputExpr = ExprNode.CreateUpdater(column, updaterExpr.Name);
                outputExpr.Resolve(Workspace, new List<DcVariable>() { groupVariable, measureVariable });
            }
        }

    }

}
