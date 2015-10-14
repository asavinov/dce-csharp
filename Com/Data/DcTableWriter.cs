using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Data.Query;
using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Data
{
    public interface DcTableWriter
    {
        /// <summary>
        /// Analyze this definition by extracting the structure of its function output. 
        /// Append these output columns from the definition of the output table. 
        ///
        /// The method guarantees that the function outputs (data) produced by this definition can be appended to the output table, that is, the output table is consistent with this definition. 
        /// This method can be called before (or within) resolution procedure which can be viewed as a schema-level analogue of data population and which ensures that we have correct schema which is consistent with all formulas/definitions. 
        /// </summary>
        object Append(); // Null if not appended
    }

    public class TableWriter
    {
        DcTable table;

        public object Append()
        {
            if (Dim == null) return;
            if (Dim.Output == null) return;
            if (Dim.Output.IsPrimitive) return; // Primitive tables do not have structure

            if (FormulaExpr == null) return;

            if (FormulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION) return;
            if (FormulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC) return;

            //
            // Analyze output structure of the definition and extract all tables that are used in its output
            //
            if (FormulaExpr.OutputVariable.TypeTable == null)
            {
                string outputTableName = FormulaExpr.Item.OutputVariable.TypeName;

                // Try to find this table and if found then assign to the column output
                // If not found then create output table in the schema and assign to the column output
            }

            //
            // Analyze output structure of the definition and extract all columns that are used in its output
            //
            if (FormulaExpr.Operation == OperationType.TUPLE)
            {
                foreach (var child in FormulaExpr.Children)
                {
                    string childName = child.Item.Name;
                }
            }

            // Append the columns extracted from the definition to the output set

        }

        public TableWriter(DcTable table)
        {
            this.table = table;
        }
    }

}
