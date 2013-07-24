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

            SelectExpression = Expression.CreateExportExpression(LesserSet);

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

        public override void Populate()
        {
            // Local population procedure without importing (without external extensional)
            if (LesserSet.Root is SetRootOledb)
            {
                // Request a (flat) result set from the remote set (data table)
                // For each row, evaluate the expression and append the new element
                DataTable dataTable = ((SetRootOledb)LesserSet.Root).ExportAll(LesserSet);

                SelectExpression.SetInput(Operation.PROJECTION, Operation.DATA_ROW); // Set the necessary input expression for all functions

                foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
                {
                    // Reset
                    SelectExpression.SetOutput(Operation.ALL, null);

                    // Set the constant values in the expression
                    SelectExpression.SetOutput(Operation.DATA_ROW, row);

                    // Evaluate the expression tree by appending the elements into the sets if absent
                    SelectExpression.Evaluate(EvaluationMode.APPEND);
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
                    SelectExpression.Evaluate(EvaluationMode.APPEND);
                }
            }

        }

        public override void Unpopulate() // Clean, Empty
        {
            // Simply empty the greater set
            // After this operation the greater set is empty
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
