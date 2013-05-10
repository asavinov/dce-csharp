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

        #region Export and populate data

        public virtual void Populate()
        {
             // Local population procedure without importing (without external extensional)
            if (LesserSet.Root is SetRootOledb)
            {
                // Request a (flat) result set from the remote set (data table)
                // For each row, evaluate the expression and append the new element
                DataTable dataTable = ((SetRootOledb)LesserSet.Root).Export(LesserSet); // TODO: rename Export to GetDataTable

                foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
                {
                    SelectExpression.SetInput(row); // First, initialize its expression by setting inputs (reference to the current data row)
                    SelectExpression.Evaluate(true); // Second, evaluate each expression by appending the values if absent
                }
            }
            else if (LesserSet.Root is SetRootOdata)
            {
            } 
            else // Direct access using offsets
            {
                for (Offset offset = 0; offset < LesserSet.Length; offset++)
                {
                    SelectExpression.SetInput(offset);
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

        #region Export schema

        /// <summary>
        /// Export all dimensions from the lesser set and import them into the greater set.
        /// This method also creates a definition for the export function. 
        /// All primitive dimensions paths are exported without modifications. 
        /// A default mapping (name equality) is used to match sets by finding similar sets. If not found, a new set is created. 
        /// </summary>
        public virtual void ExportDimensions()
        {
            Debug.Assert(LesserSet != null, "Wrong use: lesser set cannot be null for export.");
            GreaterSet.CloneGreaterDimensions(LesserSet); // Recursive
        }

        /// <summary>
        /// Create an expression for exporting the values, that is, for mapping from the lesser to the greater set (including their structures).
        /// The created expression must have the structure of the greater set so that the result of evaluation corresponds to the output set. 
        /// </summary>
        public virtual void ExportExpression()
        {
            Debug.Assert(LesserSet != null, "Wrong use: lesser set cannot be null for export.");
            SelectExpression = new Expression(GreaterSet);
        }

        #endregion

        #region Deprecated: export schema

        /// <summary>
        /// Create (recursively) the same dimension tree within the greater set and return its reference. 
        /// New sets will be found using name comparison and created if absent.
        /// </summary>
        /// <param name="remDim"></param>
        /// <returns></returns>
        public Dim ExportDimension(Dim remDim)
        {
            Set remSet = remDim.GreaterSet;
            Set locSet = null;

            // Clone one dimension
            Dim locDim = GetGreaterDim(remDim.Name); // Dimensions are mapped by name
            if (locDim == null) // Not found
            {
                // Try to find local equivalent of the remote greater set using (same as)
                locSet = Root.MapToLocalSet(remSet);
                if (locSet == null) // Not found
                {

                    locSet = new Set(remSet.Name, remSet); // Clone.
                    Set locSuperSet = Root.MapToLocalSet(remSet.SuperSet);
                    locSet.SuperDim = new DimRoot("super", this, locSuperSet);
                }

                // Create a local equivalent of the dimension
                locDim = locSet.CreateDefaultLesserDimension(remDim.Name, this);
                locDim.LesserSet = this;
                locDim.GreaterSet = locSet;
                locDim.IsIdentity = remDim.IsIdentity;
                locDim.SelectExpression = new Expression(remDim);

                // Really add this new dimension to this set
                AddGreaterDim(locDim);
            }
            else // Found
            {
                locSet = locDim.GreaterSet;
            }

            // Recursion: the same method for all greater dimensions of the new greater set
            foreach (Dim dim in remSet.GreaterDims)
            {
                locSet.ExportDimension(dim);
            }

            return locDim;
        }

        /// <summary>
        /// Create dimensions in this set by cloning dimensions of the source set.
        /// The source set is specified in this set definition (FromExpression). 
        /// The method not only creates new dimensions by also defines them by setting their SelectExpression. 
        /// </summary>
        public virtual void ExportDimensions2()
        {
            //
            // Find the source set the dimensions have to be cloned from
            //
            if (FromDb == null || String.IsNullOrEmpty(FromSetName))
            {
                return; // Nothing to import
            }
            Set srcSet = FromDb.FindSubset(FromSetName);
            if (srcSet == null)
            {
                return; // Nothing to import
            }

            //
            // Loop through all source paths and use them these paths in the expressions
            //
            foreach (Dim srcPath in srcSet.GreaterPaths)
            {

                string columnType = Root.MapToLocalType(srcPath.GreaterSet.Name);
                Set gSet = Root.GetPrimitiveSubset(columnType);
                if (gSet == null)
                {
                    // ERROR: Cannot find the matching primitive set for the path
                }
                Dim path = null; // TODO: We should try to find this path and create a new path only if it cannot be found. Or, if found, the existing path should be deleted along with all its segments.
                if (path == null)
                {
                    path = gSet.CreateDefaultLesserDimension(srcPath.Name, this); // We do not know the type of the path
                }
                path.IsIdentity = srcPath.IsIdentity;
                path.SelectDefinition = srcPath.Name;
                path.LesserSet = this;
                path.GreaterSet = gSet;

                Set lSet = this;
                foreach (Dim srcDim in srcPath.Path)
                {
                    Dim dim = lSet.GetGreaterDim(srcDim.Name);
                    if (dim == null)
                    {
                        if (srcDim.GreaterSet == srcPath.GreaterSet)
                        {
                            gSet = srcPath.GreaterSet; // Last segment has the same greater set as the path it belongs to
                        }
                        else // Try to find this greater set in our database
                        {
                            gSet = Root.FindSubset(srcDim.GreaterSet.Name);
                            if (gSet == null)
                            {
                                gSet = new Set(srcDim.GreaterSet.Name, srcDim.GreaterSet);
                                gSet.SuperDim = new DimRoot("super", gSet, Root); // Default solution: insert the set (no dimensions)
                                gSet.ImportDimensions(); // Import its dimensions (recursively). We need at least identity dimensions
                            }
                        }

                        dim = gSet.CreateDefaultLesserDimension(srcDim.Name, lSet);
                        dim.IsIdentity = srcDim.IsIdentity;
                        dim.SelectDefinition = srcDim.Name;
                        dim.LesserSet = lSet;
                        dim.GreaterSet = gSet;
                    }

                    path.Path.Add(dim); // Add this dimension as the next segment in the path

                    lSet = gSet; // Loop iteration
                }

                AddGreaterPath(path); // Add the new dimension to this set 
                foreach (Dim dim in path.Path) // Add also all its segments if they do not exist yet
                {
                    if (dim.LesserSet.GreaterDims.Contains(dim)) continue;
                    dim.LesserSet.AddGreaterDim(dim);
                }
            }
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
