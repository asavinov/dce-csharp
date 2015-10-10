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

        public Mapping Mapping { get; set; }

        //
        // Aggregation
        //

        public DcTable FactTable { get; set; }

        public List<DimPath> GroupPaths { get; set; }

        public List<DimPath> MeasurePaths { get; set; }

        public string Updater { get; set; }

        // Get an object which is used to compute the function values according to the formula
        protected DcEvaluator GetIterator()
        {
            DcEvaluator evaluator = null;

            if (FormulaExpr == null || FormulaExpr.DefinitionType == ColumnDefinitionType.FREE)
            {
                ; // Nothing to do
            }
            else if (Dim.Input.Schema is SchemaCsv) // Import from CSV
            {
                evaluator = new EvaluatorCsv(Dim);
            }
            else if (Dim.Input.Schema is SchemaOledb) // Import from OLEDB
            {
                evaluator = new EvaluatorOledb(Dim);
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                evaluator = new EvaluatorAggr(Dim);
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC)
            {
                evaluator = new EvaluatorExpr(Dim);
            }
            else if (FormulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                evaluator = new EvaluatorExpr(Dim);
            }
            else
            {
                throw new NotImplementedException("This type of column definition is not implemented.");
            }

            return evaluator;
        }

        private void EvaluateBegin()
        {
            // Aassert: FactTable.GroupFormula + ThisSet.ThisFunc = FactTable.MeasureFormula
            // Aassert: if LoopSet == ThisSet then GroupCode = null, ThisFunc = MeasureCode


            //
            // Open files/databases
            //
            if (Dim.Output.Schema is SchemaCsv) // Prepare to writing to a csv file during evaluation
            {
                SchemaCsv csvSchema = (SchemaCsv)Dim.Output.Schema;
                SetCsv csvOutput = (SetCsv)Dim.Output;

                // Ensure that all parameters are correct
                // Set index for all columns that have to written to the file
                int index = 0;
                for (int i = 0; i < csvOutput.Columns.Count; i++)
                {
                    if (!(csvOutput.Columns[i] is DimCsv)) continue;

                    DimCsv col = (DimCsv)csvOutput.Columns[i];
                    if (col.IsSuper)
                    {
                        col.ColumnIndex = -1; // Will not be written 
                    }
                    else if (col.Output.Schema != col.Input.Schema) // Import/export columns do not store data
                    {
                        col.ColumnIndex = -1;
                    }
                    else
                    {
                        col.ColumnIndex = index;
                        index++;
                    }
                }

                // Open file for writing
                if (csvSchema.connection != null)
                {
                    csvSchema.connection.OpenWriter(csvOutput);

                    // Write header
                    if (csvOutput.HasHeaderRecord)
                    {
                        var header = csvOutput.GetColumnNamesByIndex();
                        csvSchema.connection.WriteNext(header);
                    }
                }
            }
            else if (Dim.Output.Schema is SchemaOledb) // Prepare to writing to a database during evaluation
            {
            }

            Dim.Data.AutoIndex = false;
            //Dim.Data.Nullify();
        }

        public void Evaluate()
        {
            DcEvaluator evaluator = GetIterator();
            if (evaluator == null) return;

            try
            {
                EvaluateBegin();

                while (evaluator.NextInput())
                {
                    evaluator.Evaluate();
                }
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
            finally
            {
                EvaluateEnd();
            }
        }

        private void EvaluateEnd()
        {
            Dim.Data.Reindex();
            Dim.Data.AutoIndex = true;

            //
            // Close files/databases
            //
            if (Dim.Output.Schema is SchemaCsv)
            {
                SchemaCsv csvSchema = (SchemaCsv)Dim.Output.Schema;
                SetCsv csvOutput = (SetCsv)Dim.Output;

                // Close file
                if (csvSchema.connection != null)
                {
                    csvSchema.connection.CloseWriter();
                }
            }
            else if (Dim.Output.Schema is SchemaOledb)
            {
            }
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

            GroupPaths = new List<DimPath>();
            MeasurePaths = new List<DimPath>();

            Dependencies = new List<Dim>();
        }

    }

}
