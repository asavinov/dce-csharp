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
    /// This dimension describes a function for importing data from the greater set and to the lesser set. 
    /// It is not supposed to store this function.
    /// The import procedure will iterate through the identities of the greater set and the result of evaluation will be stored in the lesser set. 
    /// Theoretically, it is possible to import data from such artificial greater sets as user input, message channels and other unusual sources.
    /// </summary>
    public class DimImport : Dim
    {
        #region Schema methods

        /// <summary>
        /// Create (clone) an expression for importing greater (input) set values into lesser (output) set values.
        /// The created experession describes both structure and values.
        /// </summary>
        public virtual void BuildImportExpression()
        {
            Debug.Assert(GreaterSet != null, "Wrong use: greater set cannot be null for import.");

            SelectExpression = Expression.CreateImportExpression(GreaterSet);

            SelectExpression.Name = "import";
            SelectExpression.Dimension = this;
        }

        /// <summary>
        /// Use the expression to create/clone output (lesser) set structure.  
        /// A default mapping (name equality) is used to match sets by finding similar sets. If not found, a new set is created. 
        /// </summary>
        public virtual void ImportDimensions()
        {
            Debug.Assert(SelectExpression != null, "Wrong use: exprssion cannot be null for import.");
            Debug.Assert(LesserSet != null, "Wrong use: lesser set cannot be null for import.");

            Set set = SelectExpression.FindOrCreateSet(LesserSet.Root);

            LesserSet = set;
        }

        #endregion

        #region Data methods

        public override void Populate()
        {
            // Local population procedure without importing (without external extensional)
            if (GreaterSet.Root is SetRootOledb)
            {
                // Request a (flat) result set from the remote set (data table)
                // For each row, evaluate the expression and append the new element
                DataTable dataTable = ((SetRootOledb)GreaterSet.Root).ExportAll(GreaterSet);

                SelectExpression.SetInput(Operation.PROJECTION, Operation.VARIABLE); // ??? CHECK: Set the necessary input expression for all functions

                foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
                {
                    // Reset
                    SelectExpression.SetOutput(Operation.ALL, null);

                    // Set the input variable 'source'
                    SelectExpression.SetOutput(Operation.VARIABLE, row);

                    // Evaluate the expression tree by appending the elements into the sets if absent
                    SelectExpression.OutputSet.Append(SelectExpression);
                }
            }
            else if (GreaterSet.Root is SetRootOdata)
            {
            }
            else // Direct access using offsets
            {
                for (Offset offset = 0; offset < GreaterSet.Length; offset++)
                {
                    SelectExpression.SetOutput(Operation.VARIABLE, offset); // Assign value of 'this' variable
                    SelectExpression.OutputSet.Append(SelectExpression);
                }
            }

        }

        public override void Unpopulate() // Clean, Empty
        {
            // Simply empty the greater set
            // After this operation the greater set is empty
        }

        #endregion

        public DimImport(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension according to its purpose.
	    }
    }
}
