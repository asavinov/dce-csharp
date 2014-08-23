﻿using System;
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

    public class ExprEvaluator : ComColumnEvaluator
    {
        protected ExprNode exprNode; // Can contain more specific nodes OledbExprNode to access attributes in DataRow

        protected ComVariable thisVariable; // Stores current input (offset in a local set or reference to the current DataRow)

        protected Offset currentElement;

        protected ComColumnData columnData;

        protected ComTable loopTable;

        //
        // CsColumnEvaluator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public virtual bool Next()
        {
            if (currentElement < loopTable.Data.Length) currentElement++;

            if (currentElement < loopTable.Data.Length) return true;
            else return false;
        }
        public virtual bool First()
        {
            currentElement = 0;

            if (currentElement < loopTable.Data.Length) return true;
            else return false;
        }
        public virtual bool Last()
        {
            currentElement = loopTable.Data.Length - 1;

            if (currentElement >= 0) return true;
            else return false;
        }

        public virtual object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(currentElement);

            // evaluate the expression
            exprNode.Evaluate();

            // Write the result value to the function
            if (columnData != null)
            {
                columnData.SetValue(currentElement, exprNode.Result.GetValue());
            }

            return exprNode.Result.GetValue();
        }

        public virtual object EvaluateUpdate() { return null; }

        public virtual bool EvaluateJoin(object output) { return false; }

        public virtual object GetResult() { return exprNode.Result.GetValue(); }

        public static ComColumnEvaluator CreateColumnEvaluator(ComColumn column)
        {
            ExprEvaluator eval = new ExprEvaluator(column);
            return eval;
        }

        public static ComColumnEvaluator CreateCsvEvaluator(ComColumn column)
        {
            CsvEvaluator eval = new CsvEvaluator(column);
            return eval;
        }

        public static ComColumnEvaluator CreateOledbEvaluator(ComColumn column)
        {
            OledbEvaluator eval = new OledbEvaluator(column);
            return eval;
        }

        public static ComColumnEvaluator CreateWhereEvaluator(ComTable table)
        {
            ExprEvaluator eval = new ExprEvaluator(table);
            return eval;
        }

        public static ComColumnEvaluator CreateAggrEvaluator(ComColumn column)
        {
            AggrEvaluator eval = new AggrEvaluator(column);
            return eval;
        }

        public ExprEvaluator(ComColumn column)
        {
            if (column.Definition.Mapping != null)
            {
                if (column.Definition.IsGenerating)
                {
                    exprNode = column.Definition.Mapping.BuildExpression(ActionType.APPEND);
                }
                else
                {
                    exprNode = column.Definition.Mapping.BuildExpression(ActionType.READ);
                }
            }
            else if (column.Definition.Formula != null)
            {
                exprNode = column.Definition.Formula;
            }

            currentElement = -1;
            loopTable = column.LesserSet;
            isUpdate = false;
            thisVariable = new Variable("this", loopTable.Name);
            thisVariable.TypeTable = loopTable;
            columnData = column.Data;

            // Resolve names in the expresion by storing direct references to storage objects which will be used during valuation (names will not be used
            exprNode.Resolve(column.LesserSet.Top, new List<ComVariable>() { thisVariable });
        }

        public ExprEvaluator(ComTable table)
        {
            exprNode = table.Definition.WhereExpression;

            currentElement = -1;
            loopTable = table;
            isUpdate = false;
            thisVariable = new Variable("this", loopTable.Name);
            thisVariable.TypeTable = loopTable;
            columnData = null;

            // Resolve names in the expresion by storing direct references to storage objects which will be used during valuation (names will not be used
            exprNode.Resolve(loopTable.Top, new List<ComVariable>() { thisVariable });
        }

        public ExprEvaluator()
        {
        }
    }

    public class CsvEvaluator : ExprEvaluator
    {
        protected string[] currentRecord;
        protected ConnectionCsv connectionCsv;

        //
        // CsColumnEvaluator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public override bool Next()
        {
            currentRecord = connectionCsv.CurrentRecord;

            if (currentRecord == null) return false;

            thisVariable.SetValue(currentRecord);

            connectionCsv.Next(); // We increment after iteration because csv is opened with first record initialized
            currentElement++;

            return true;
        }

        public override object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(currentRecord);

            // evaluate the expression
            exprNode.Evaluate();

            // Write the result value to the function
            // columnData.SetValue(currentElement, exprNode.Result.GetValue()); // We do not store import functions (we do not need this data)

            return null;
        }

        public CsvEvaluator(ComColumn column)
            : base(column)
        {
            // Produce a result set that can be iterated through
            connectionCsv = ((SetTopCsv)loopTable.Top).connection;
            connectionCsv.Open((SetCsv)loopTable);

            currentElement = 0;
            currentRecord = connectionCsv.CurrentRecord; // Start from the first record
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

        public OledbEvaluator(ComColumn column)
            : base(column)
        {
            // Produce a result set from the remote database by executing a query on the source table
            dataTable = ((SetTopOledb)loopTable.Top).LoadTable(loopTable);
            rows = dataTable.Rows.GetEnumerator();
        }
    }

    /// <summary>
    /// Notes:
    /// - distinguish between this table (where the aggregated column is defined, and a fact table which provides values to be aggregated where group and measure functions are defined.
    /// - the way of aggregation is defined as an updater expression which knows how to compute a new value given the old (current) value and a new measure.
    /// </summary>
    public class AggrEvaluator : ExprEvaluator
    {
        //
        // Fact-related members
        //

        // base::thisVariable stores current fact in the loop table. is used by group expr and meausre expr
        // base::currentElement is offset in the fact table
        // base::loopTable is a fact set which is iterated in this class

        protected ExprNode groupExpr; // Returns a group this fact belongs to, is stored in the group variable

        protected ExprNode measureExpr; // Returns a new value to be aggregated with the old value, is stored in the measure variable

        //
        // Aggregation-related members
        //

        protected ComVariable groupVariable; // Stores current group (input for the aggregated function)

        protected ComVariable measureVariable; // Stores new value (output for the aggregated function)

        // base::exprNode - updater expression. works in the context of two variables: group and measure
        // base::columnData is the aggregated function to be computed

        //
        // CsColumnEvaluator interface
        //

        public override object Evaluate()
        {
            //
            // Evalute group and measure expressions for the current fact
            //

            // Use input value to evaluate the expression
            thisVariable.SetValue(currentElement);

            groupExpr.Evaluate();
            Offset groupElement = (Offset)groupExpr.Result.GetValue();
            groupVariable.SetValue(groupElement);

            measureExpr.Evaluate();
            object measureValue = measureExpr.Result.GetValue();
            measureVariable.SetValue(measureValue);

            //
            // Evaluate the update expression and store the new computed value
            //
            exprNode.Evaluate();

            object newValue = exprNode.Result.GetValue();
            columnData.SetValue(groupElement, newValue);

            return exprNode.Result.GetValue();
        }

        public AggrEvaluator(ComColumn column)
        {
            exprNode = column.Definition.Formula;

            currentElement = -1;
            loopTable = column.Definition.FactTable;
            isUpdate = true;

            thisVariable = new Variable("this", loopTable.Name);
            thisVariable.TypeTable = loopTable;

            columnData = column.Data;

            groupVariable = new Variable("this", column.LesserSet.Name);
            groupVariable.TypeTable = column.LesserSet;

            measureVariable = new Variable("value", column.GreaterSet.Name);
            measureVariable.TypeTable = column.GreaterSet;

            groupExpr = ExprNode.CreateReader(column.Definition.GroupPaths[0], true); // Currently only one path is used
            measureExpr = ExprNode.CreateReader(column.Definition.MeasurePaths[0], true);
            groupExpr = (ExprNode)groupExpr.Root;
            measureExpr = (ExprNode)measureExpr.Root;

            exprNode = ExprNode.CreateUpdater(column, column.Definition.Updater);

            // Resolve names in the expresions using appropriate variables
            exprNode.Resolve(column.LesserSet.Top, new List<ComVariable>() { groupVariable, measureVariable });

            groupExpr.Resolve(loopTable.Top, new List<ComVariable>() { thisVariable });
            measureExpr.Resolve(loopTable.Top, new List<ComVariable>() { thisVariable });
        }
    }

}
