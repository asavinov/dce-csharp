using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// This dimension describes a function for exporting data from the lesser set and importing to the greater set. 
    /// It is not supposed to store this function.
    /// The export-import procedure will iterate through the identities of the lesser set and the result of evaluation will be stored in the greater set. 
    /// Theoretically, it is possible to import data from such artificial lesser sets as user input, message channels and other unusual sources.
    /// </summary>
    public class DimExport : Dim
    {
        #region Schema methods

        /// <summary>
        /// Create (clone) an expression for exporting lesser (input) set values into greater (output) set values.
        /// The created experession describes both structure and values.
        /// </summary>
        public virtual void BuildExpression()
        {
            Debug.Assert(LesserSet != null, "Wrong use: lesser set cannot be null for export.");

            SelectExpression = Expression.CreateExpression(LesserSet);

            SelectExpression.Name = "DimExport";
            SelectExpression.Dimension = this;
        }

        /// <summary>
        /// Use the expression to create/clone output (greater) set structure.  
        /// A default mapping (name equality) is used to match sets by finding similar sets. If not found, a new set is created. 
        /// </summary>
        public virtual void ExportDimensions()
        {
            Debug.Assert(SelectExpression != null, "Wrong use: exprssion cannot be null for export.");
            Debug.Assert(GreaterSet != null, "Wrong use: greater set cannot be null for export.");

            Set set = SelectExpression.FindOrCreateSet(GreaterSet.Root);

            GreaterSet = set;
        }

        #endregion

        #region Data methods

        public virtual void Populate()
        {
            // Local population procedure without importing (without external extensional)
            if (LesserSet.Root is SetRootOledb)
            {
                // Request a (flat) result set from the remote set (data table)
                // For each row, evaluate the expression and append the new element
                DataTable dataTable = ((SetRootOledb)LesserSet.Root).Export(LesserSet);

                SelectExpression.SetInput(Operation.FUNCTION, Operation.DATA_ROW); // Set the necessary input expression

                foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
                {
                    // Reset
                    SelectExpression.SetOutput(Operation.ALL, null);

                    // Set the constant values in the expression
                    SelectExpression.SetOutput(Operation.DATA_ROW, row);

                    // Evaluate the expression tree by appending the elements into the sets if absent
                    SelectExpression.Evaluate(true);
                }
            }
            else if (LesserSet.Root is SetRootOdata)
            {
            }
            else // Direct access using offsets
            {
                for (Offset offset = 0; offset < LesserSet.Length; offset++)
                {
                    SelectExpression.SetOutput(Operation.OFFSET, offset);
                    SelectExpression.Evaluate(true);
                }
            }

        }

        public virtual void Unpopulate() // Clean, Empty
        {
            // Simply empty the greater set
            // After this operation the greater set is empty
        }

        #endregion

        #region Deprecated/obsolete: export schema

        /// <summary>
        /// Create (recursively) the same dimension tree within the greater set and return its reference. 
        /// New sets will be found using name comparison and created if absent.
        /// 
        /// It will not work because recursion will not work. Recursion requires that every intermediate set in the tree is connected via ExportDim while only the root of two trees are connected. 
        /// Solution: 1. Build an expression within this DimExport which describes (clones) the source schema, 2. Build the target schema from this expression
        /// </summary>
        /// <param name="remDim"></param>
        /// <returns></returns>
        [Obsolete("Deprecated. First, create expression, and then use this expression to create the target schema.", true)]
        public Dim ExportDimension(Dim remDim)
        {
            Set remSet = remDim.GreaterSet;
            Set locSet = null;

            // Clone one dimension
            Dim locDim = GreaterSet.GetGreaterDim(remDim.Name); // Dimensions are mapped by name
            if (locDim == null) // Not found
            {
                // Try to find local equivalent of the remote greater set using (same as)
                locSet = LesserSet.Root.MapToLocalSet(remSet);
                if (locSet == null) // Not found
                {
                    locSet = new Set(remSet.Name); // Clone.
                    Set locSuperSet = LesserSet.Root.MapToLocalSet(remSet.SuperSet);
                    locSet.SuperDim = new DimRoot("super", GreaterSet, locSuperSet);
                }

                // Create a local equivalent of the dimension
                locDim = locSet.CreateDefaultLesserDimension(remDim.Name, GreaterSet);
                locDim.LesserSet = GreaterSet;
                locDim.GreaterSet = locSet;
                locDim.IsIdentity = remDim.IsIdentity;
                locDim.SelectExpression = Expression.CreateExpression(remDim, null);

                // Really add this new dimension to this set
                GreaterSet.AddGreaterDim(locDim);
            }
            else // Found
            {
                locSet = locDim.GreaterSet;
            }

            // Recursion: the same method for all greater dimensions of the new greater set
            foreach (Dim dim in remSet.GreaterDims)
            {
//                locSet.ExportDimension(dim);
                // PROBLEM: recursion does not work here because not all tree nodes have defs as ExportDim. Recursion will work in Expressions or simply using the original structures (without ExportDim)
                // Question: How to define and import structure? Via Expressions (tree)? 
            }

            return locDim;
        }

        #endregion

        public DimExport(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension according to its purpose.
	    }
    }
}
