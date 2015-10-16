using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Utils;
using Com.Schema.Csv;
using Com.Schema.Rel;
using Com.Data;
using Com.Data.Query;
using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Schema
{
    public class ColumnDefinition : DcColumnDefinition
    {
        protected DcColumn Dim { get; set; }

        #region ComColumnDefinition interface

        //
        // COEL (language) representation
        //

        protected string formula;
        public String Formula
        {
            get { return formula; }
            set
            {
                formula = value;

                if (string.IsNullOrWhiteSpace(formula)) return;

                ExprBuilder exprBuilder = new ExprBuilder();
                ExprNode expr = exprBuilder.Build(formula);

                FormulaExpr = expr;
            }
        }

        //
        // Structured (object) representation
        //

        public bool IsAppendData { get; set; }

        public bool IsAppendSchema { get; set; }

        public ExprNode FormulaExpr { get; set; }

        public void Evaluate()
        {
            if (FormulaExpr == null || FormulaExpr.DefinitionType == ColumnDefinitionType.FREE)
            {
                return; // Nothing to evaluate
            }

            // Aassert: FactTable.GroupFormula + ThisSet.ThisFunc = FactTable.MeasureFormula
            // Aassert: if LoopSet == ThisSet then GroupCode = null, ThisFunc = MeasureCode

            // NOTE: This should be removed or moved to the expression. Here we store non-syntactic part of the definition in columndef and then set the expression. Maybe we should have syntactic annotation for APPEND flag (output result annotation, what to do with the output). 
            if (FormulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                // Adjust the expression according to other parameters of the definition
                if (IsAppendData)
                {
                    FormulaExpr.Action = ActionType.APPEND;
                }
                else
                {
                    FormulaExpr.Action = ActionType.READ;
                }
            }

            //
            // Evaluate loop depends on the type of definition
            //

            // General parameters
            DcWorkspace Workspace = Dim.Input.Schema.Workspace;
            DcColumnData columnData = Dim.Data;

            Dim.Data.AutoIndex = false;
            //Dim.Data.Nullify();

            object thisCurrent = null;

            if (Dim.Input.Schema is SchemaCsv) // Import from CSV
            {
				// Prepare parameter variables for the expression 
				DcTable thisTable = Dim.Input;
				DcVariable thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
				thisVariable.TypeSchema = thisTable.Schema;
				thisVariable.TypeTable = thisTable;

				// Parameterize expression and resolve it (bind names to real objects) 
				FormulaExpr.OutputVariable.SchemaName = Dim.Output.Schema.Name;
				FormulaExpr.OutputVariable.TypeName = Dim.Output.Name;
				FormulaExpr.OutputVariable.TypeSchema = Dim.Output.Schema;
				FormulaExpr.OutputVariable.TypeTable = Dim.Output;
				FormulaExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

				FormulaExpr.EvaluateBegin();
				DcTableReader tableReader = thisTable.GetTableReader();
                tableReader.Open();
                while ((thisCurrent = tableReader.Next()) != null)
                {
                    thisVariable.SetValue(thisCurrent); // Set parameters of the expression

                    FormulaExpr.Evaluate(); // Evaluate the expression

                    if (columnData != null) // We do not store import functions (we do not need this data)
                    {
                        object newValue = FormulaExpr.OutputVariable.GetValue();
                        //columnData.SetValue((Rowid)thisCurrent, newValue);
                    }
                }
                tableReader.Close();
				FormulaExpr.EvaluateEnd();
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC || FormulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
				// Prepare parameter variables for the expression 
				DcTable thisTable = Dim.Input;
				DcVariable thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
				thisVariable.TypeSchema = thisTable.Schema;
				thisVariable.TypeTable = thisTable;

				// Parameterize expression and resolve it (bind names to real objects) 
				FormulaExpr.OutputVariable.SchemaName = Dim.Output.Schema.Name;
				FormulaExpr.OutputVariable.TypeName = Dim.Output.Name;
				FormulaExpr.OutputVariable.TypeSchema = Dim.Output.Schema;
				FormulaExpr.OutputVariable.TypeTable = Dim.Output;
				FormulaExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

				FormulaExpr.EvaluateBegin();
				DcTableReader tableReader = thisTable.GetTableReader();
				tableReader.Open();
                while ((thisCurrent = tableReader.Next()) != null)
                {
                    thisVariable.SetValue(thisCurrent); // Set parameters of the expression

                    FormulaExpr.Evaluate(); // Evaluate the expression

                    // Write the result value to the function
                    // NOTE: We want to implement write operations with functions in the expression itself, particularly, because this might be done by intermediate nodes each of them having also APPEND flag
                    // NOTE: when writing or find/append output to a table, an expression needs a TableWriter (or reader) object which is specific to the expression node (also intermediate)
                    // NOTE: it could be meaningful to implement separately TUPLE (DOWN, NON-PRIMITIVE) nodes and CALL (UP, PRIMITIVE) expression classes since their general logic/purpose is quite different, particularly, for table writing. 
                    // NOTE: where expression (in tables) is evaluated without writing to column
                    if (columnData != null)
                    {
                        object newValue = FormulaExpr.OutputVariable.GetValue();
                        columnData.SetValue((Rowid)thisCurrent, newValue);
                    }
                }
                tableReader.Close();
				FormulaExpr.EvaluateEnd();
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                // Facts
                ExprNode factsNode = FormulaExpr.GetChild("facts").GetChild(0);

                // This table and variable
				string thisTableName = factsNode.Name;
                DcTable thisTable = Dim.Input.Schema.GetSubTable(thisTableName);
                DcVariable thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
                thisVariable.TypeSchema = thisTable.Schema;
                thisVariable.TypeTable = thisTable;

                // Groups
                ExprNode groupExpr; // Returns a group this fact belongs to, is stored in the group variable
                ExprNode groupsNode = FormulaExpr.GetChild("groups").GetChild(0);
                groupExpr = groupsNode;
                groupExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

                DcVariable groupVariable; // Stores current group (input for the aggregated function)
                groupVariable = new Variable(Dim.Input.Schema.Name, Dim.Input.Name, "this");
                groupVariable.TypeSchema = Dim.Input.Schema;
                groupVariable.TypeTable = Dim.Input;

                // Measure
                ExprNode measureExpr; // Returns a new value to be aggregated with the old value, is stored in the measure variable
                ExprNode measureNode = FormulaExpr.GetChild("measure").GetChild(0);
                measureExpr = measureNode;
                measureExpr.Resolve(Workspace, new List<DcVariable>() { thisVariable });

                DcVariable measureVariable; // Stores new value (output for the aggregated function)
                measureVariable = new Variable(Dim.Output.Schema.Name, Dim.Output.Name, "value");
                measureVariable.TypeSchema = Dim.Output.Schema;
                measureVariable.TypeTable = Dim.Output;

                // Updater/aggregation function
                ExprNode updaterExpr = FormulaExpr.GetChild("aggregator").GetChild(0);

                ExprNode outputExpr;
                outputExpr = ExprNode.CreateUpdater(Dim, updaterExpr.Name);
                outputExpr.Resolve(Workspace, new List<DcVariable>() { groupVariable, measureVariable });

				FormulaExpr.EvaluateBegin();
				DcTableReader tableReader = thisTable.GetTableReader();
                tableReader.Open();
                while ((thisCurrent = tableReader.Next()) != null)
                {
                    thisVariable.SetValue(thisCurrent); // Set parameters of the expression

                    groupExpr.Evaluate();
                    Rowid groupElement = (Rowid)groupExpr.OutputVariable.GetValue();
                    groupVariable.SetValue(groupElement);

                    measureExpr.Evaluate();
                    object measureValue = measureExpr.OutputVariable.GetValue();
                    measureVariable.SetValue(measureValue);

                    outputExpr.Evaluate(); // Evaluate the expression

                    // Write the result value to the function
                    if (columnData != null)
                    {
                        object newValue = outputExpr.OutputVariable.GetValue();
                        columnData.SetValue(groupElement, newValue);
                    }
                }
                tableReader.Close();
				FormulaExpr.EvaluateEnd();
            }
            else
            {
                throw new NotImplementedException("This type of column definition is not implemented.");
            }

            Dim.Data.Reindex();
            Dim.Data.AutoIndex = true;
        }

        //
        // Dependencies
        //

        public List<Dim> Dependencies { get; set; } // Other functions this function directly depends upon. Computed from the definition of this function.
        // Find and store all outputs of this function by evaluating (executing) its definition in a loop for all input elements of the fact set (not necessarily this set)

        public List<DcTable> UsesTables(bool recursive) // This element depends upon
        {
            List<DcTable> res = new List<DcTable>();

            if (FormulaExpr == null)
            {
                ;
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.ANY || FormulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC || FormulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                if (FormulaExpr != null) // Dependency information is stored in expression (formula)
                {
                    res = FormulaExpr.Find((DcTable)null).Select(x => x.OutputVariable.TypeTable).ToList();
                }
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                /*
                res.Add(FactTable); // This column depends on the fact table

                // Grouping and measure paths are used in this column
                if (GroupPaths != null)
                {
                    foreach (DimPath path in GroupPaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }
                if (MeasurePaths != null)
                {
                    foreach (DimPath path in MeasurePaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }
                */
            }

            return res;
        }
        public List<DcTable> IsUsedInTables(bool recursive) // Dependants
        {
            List<DcTable> res = new List<DcTable>();

            // TODO: Which other sets use this function for their content? Say, if it is a generating function. Or it is a group/measure function.
            // Analyze other function definitions and check if this function is used there directly. 
            // If such a function has been found, then make the same call for it, that is find other functins where it is used.

            // A function can be used in Filter expression and Sort expression

            return res;
        }

        public List<DcColumn> UsesColumns(bool recursive) // This element depends upon
        {
            List<DcColumn> res = new List<DcColumn>();

            if (FormulaExpr == null)
            {
                ;
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.ANY || FormulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC || FormulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                if (FormulaExpr != null) // Dependency information is stored in expression (formula)
                {
                    res = FormulaExpr.Find((DcColumn)null).Select(x => x.Column).ToList();
                }
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                /*
                // Grouping and measure paths are used in this column
                if (GroupPaths != null)
                {
                    foreach (var path in GroupPaths)
                    {
                        foreach (var seg in path.Segments)
                        {
                            if (!res.Contains(seg)) res.Add(seg);
                        }
                    }
                }
                if (MeasurePaths != null)
                {
                    foreach (var path in MeasurePaths)
                    {
                        foreach (var seg in path.Segments)
                        {
                            if (!res.Contains(seg)) res.Add(seg);
                        }
                    }
                }
                */
            }

            return res;
        }
        public List<DcColumn> IsUsedInColumns(bool recursive) // Dependants
        {
            List<DcColumn> res = new List<DcColumn>();

            // TODO: Find which other columns use this column in the definition

            return res;
        }

        #endregion

        public ColumnDefinition(DcColumn dim)
        {
            Dim = dim;

            IsAppendData = false;
            IsAppendSchema = true;

            Dependencies = new List<Dim>();
        }

    }

}
