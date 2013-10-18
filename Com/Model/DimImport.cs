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

        public override bool IsInGreaterSet
        {
            get
            {
                if (GreaterSet == null) return true;
                var dimList = GreaterSet.ExportDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
            set
            {
                if (GreaterSet == null) return;
                var dimList = GreaterSet.ExportDims; // Only this line will be changed in this class extensions for other dimension types
                if (value == true) // Include
                {
                    if (IsInGreaterSet) return;
                    dimList.Add(this);
                }
                else // Exclude
                {
                    dimList.Remove(this);
                }
            }
        }

        public override bool IsInLesserSet
        {
            get
            {
                if (LesserSet == null) return true;
                var dimList = LesserSet.ImportDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
            set
            {
                if (LesserSet == null) return;
                var dimList = LesserSet.ImportDims; // Only this line will be changed in this class extensions for other dimension types
                if (value == true) // Include
                {
                    if (IsInLesserSet) return;
                    dimList.Add(this);
                }
                else // Exclude
                {
                    dimList.Remove(this);
                }
            }
        }

        #endregion

        #region Data methods

        public override void Populate()
        {
            // Replaced by the set population method
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
