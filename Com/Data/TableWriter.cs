﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Com.Utils;
using Com.Schema;
using Com.Data.Query;
using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Data
{
    public class TableWriter : DcTableWriter
    {
        DcTable table;
        Rowid rowid = -1;

        public virtual void Open()
        {
            rowid = -1;
        }

        public virtual void Close()
        {
            rowid = table.Data.Length;
        }

        public virtual Rowid Find(ExprNode expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            // Find the specified tuple but not its nested tuples (if nested tuples have to be found before then use recursive calls, say, a visitor or recursive epxression evaluation)

            Debug.Assert(!table.IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.OutputVariable.TypeTable == table, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");

            Rowid[] result = Enumerable.Range(0, table.Data.Length).ToArray(); // All elements of this set (can be quite long)
            bool hasBeenRestricted = false; // For the case where the Length==1, and no key columns are really provided, so we get at the end result.Length==1 which is misleading. Also, this fixes the problem of having no key dimensions.

            List<DcColumn> dims = new List<DcColumn>();
            dims.AddRange(table.Columns.Where(x => x.IsKey));
            dims.AddRange(table.Columns.Where(x => !x.IsKey));

            foreach (DcColumn dim in dims) // OPTIMIZE: the order of dimensions matters (use statistics, first dimensins with better filtering). Also, first identity dimensions.
            {
                ExprNode childExpr = expr.GetChild(dim.Name);
                if (childExpr != null)
                {
                    object val = null;
                    val = childExpr.OutputVariable.GetValue();

                    hasBeenRestricted = true;
                    Rowid[] range = dim.Data.Deproject(val); // Deproject the value
                    result = result.Intersect(range).ToArray(); // Intersect with previous de-projections
                    // OPTIMIZE: Write our own implementation for intersection and other operations. Assume that they are ordered.
                    // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization)

                    if (result.Length == 0) break; // Not found
                }
            }

            if (result.Length == 0) // Not found
            {
                return -1;
            }
            else if (result.Length == 1) // Found single element - return its offset
            {
                if (hasBeenRestricted) return result[0];
                else return -result.Length;
            }
            else // Many elements satisfy these properties (non-unique identities). Use other methods for getting these records (like de-projection)
            {
                return -result.Length;
            }
        }

        public virtual bool CanAppend(ExprNode expr) // Determine if this expression (it has to be evaluated) can be added into this set as a new instance
        {
            // CanAppend: Check if the whole tuple can be added without errors
            // We do not check existence (it is done before). If tuple exists then no check is done and return false. If null then we check general criterial for adding (presence of all necessary data).

            //Debug.Assert(expr.OutputSet == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");

            //
            // Check that real (non-null) values are available for all identity dimensions
            //
            PathEnumerator primPaths = new PathEnumerator(table, DimensionType.IDENTITY);
            foreach (DimPath path in primPaths) // Find all primitive identity paths
            {
                // Try to find at least one node with non-null value on the path
                bool valueFound = false;
                /*
                for (Expression node = expr.GetLastNode(path); node != null; node = node.ParentExpression)
                {
                    if (node.Output != null) { valueFound = true; break; }
                }
                */

                if (!valueFound) return false; // This primitive path does not provide a value so the whole instance cannot be created
            }

            //
            // Check that it satisfies the constraints (where expression)
            //

            // TODO: it is a problem because for that purpose we need to have this instance in the set appended. 
            // Then we can check and remove but nested removal is difficult because we have to know which nested tuples were found and which were really added.
            // Also, we need to check if nested inserted instances satsify their set constraints - this should be done during insertion and the process broken if any nested instance does not satsify the constraints.

            return true;
        }

        public virtual Rowid Append(ExprNode expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
        {
            // Append: append *this* tuple to the set 
            // Greater tuples are not processed  - it is the task of the interpreter. If it is necessary to append (or do something else) with a greater tuple then it has to be done in the interpreter (depending on the operation, action and other parameters)
            // This method is intended for only appending one row while tuple is used only as a data structure (so its usage for interpretation is not used)
            // So this method expects that child nodes have been already evaluated and store the values in the result. 
            // So this method is equivalent to appending using other row representations.
            // The offset of the appended element is stored as a result in this tuple and also returned.
            // If the tuple already exists then it is found and its current offset is returned.
            // It is assumed that child expressions represent dimensions.
            // It is assumed that the column names are resolved.

            Debug.Assert(!table.IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.OutputVariable.TypeTable == table, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");

            //
            // TODO: Check existence (uniqueness of the identity)
            //

            //
            // Really append a new element to the set
            //
            foreach (DcColumn dim in table.Columns) // We must append one value to ALL greater dimensions (possibly null)
            {
                ExprNode childExpr = expr.GetChild(dim.Name); // TODO: replace by accessor by dimension reference (has to be resolved in the tuple)
                object val = null;
                if (childExpr != null) // A tuple contains a subset of all dimensions
                {
                    val = childExpr.OutputVariable.GetValue();
                }
                dim.Data.Append(val);
            }

            //
            // TODO: Check other constraints (for example, where constraint). Remove if not satisfies and return status.
            //

            table.Data.Length = table.Data.Length + 1;
            return table.Data.Length - 1;
        }

        /*
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
        */

        public TableWriter(DcTable table)
        {
            this.table = table;
        }
    }

}