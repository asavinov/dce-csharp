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

    public class ExprIterator : DcIterator
    {
        protected DcColumnData columnData;

        // Loop
        protected Offset thisCurrent;
        protected DcTable thisTable;
        protected DcVariable thisVariable; // Stores current input (offset in a local set or reference to the current DataRow)

        // Output expression
        protected ExprNode outputExpr; // Can contain more specific nodes OledbExprNode to access attributes in DataRow

        //
        // ComColumnEvaluator interface
        //

        public Workspace Workspace { get; set; }

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

        public virtual object GetResult() 
        { 
            return outputExpr.Result.GetValue(); 
        }

        public ExprIterator(DcColumn column)
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

                if (column.Definition.DefinitionType == ColumnDefinitionType.LINK)
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

            outputExpr.Result.SchemaName = column.Output.Schema.Name;
            outputExpr.Result.TypeName = column.Output.Name;
            outputExpr.Result.TypeSchema = column.Output.Schema;
            outputExpr.Result.TypeTable = column.Output;

            outputExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });
        }

        public ExprIterator(DcTable table)
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

        public ExprIterator()
        {
        }
    }

    /// <summary>
    /// Notes:
    /// - distinguish between this table (where the aggregated column is defined, and a fact table which provides values to be aggregated where group and measure functions are defined.
    /// - the way of aggregation is defined as an updater expression which knows how to compute a new value given the old (current) value and a new measure.
    /// </summary>
    public class AggrIterator : ExprIterator
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

        public AggrIterator(DcColumn column) // Create evaluator from structured definition
        {
            Workspace = column.Input.Schema.Workspace;
            columnData = column.Data;

            if (column.Definition.FormulaExpr == null) // From structured definition (parameters)
            {
                // Facts
                thisCurrent = -1;
                thisTable = column.Definition.FactTable;

                thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
                thisVariable.TypeSchema = thisTable.Schema;
                thisVariable.TypeTable = thisTable;

                // Groups
                groupExpr = ExprNode.CreateReader(column.Definition.GroupPaths[0], true); // Currently only one path is used
                groupExpr = (ExprNode)groupExpr.Root;
                groupExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

                groupVariable = new Variable(column.Input.Schema.Name, column.Input.Name, "this");
                groupVariable.TypeSchema = column.Input.Schema;
                groupVariable.TypeTable = column.Input;

                // Measure
                measureExpr = ExprNode.CreateReader(column.Definition.MeasurePaths[0], true);
                measureExpr = (ExprNode)measureExpr.Root;
                measureExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

                measureVariable = new Variable(column.Output.Schema.Name, column.Output.Name, "value");
                measureVariable.TypeSchema = column.Output.Schema;
                measureVariable.TypeTable = column.Output;

                // Updater/aggregation function
                outputExpr = ExprNode.CreateUpdater(column, column.Definition.Updater);
                outputExpr.Resolve(Workspace, new List<DcVariable>() { groupVariable, measureVariable });
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

    public class CsvEvaluator : ExprIterator
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

            connectionCsv.ReadNext(); // We increment after iteration because csv is opened with first record initialized
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

        public CsvEvaluator(DcColumn column)
            : base(column)
        {
            // Produce a result set that can be iterated through
            connectionCsv = ((SchemaCsv)thisTable.Schema).connection;
            connectionCsv.OpenReader((SetCsv)thisTable);

            thisCurrent = 0;
            currentRecord = connectionCsv.CurrentRecord; // Start from the first record
        }
    }

    public class OledbEvaluator : ExprIterator
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

        public OledbEvaluator(DcColumn column)
            : base(column)
        {
            // Produce a result set from the remote database by executing a query on the source table
            dataTable = ((SchemaOledb)thisTable.Schema).LoadTable(thisTable);
            rows = dataTable.Rows.GetEnumerator();
        }
    }

}
