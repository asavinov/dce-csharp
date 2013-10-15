using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// This super dimension leads from a normal set to a normal set. 
    /// </summary>
    public class DimSuper : DimPrimitive<int>
    {

        #region Schema methods

        public override bool IsInGreaterSet
        {
            get
            {
                if (GreaterSet == null) return true;
                var dimList = GreaterSet.SubDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
            set
            {
                if (GreaterSet == null) return;
                var dimList = GreaterSet.SubDims; // Only this line will be changed in this class extensions for other dimension types
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
                return LesserSet.SuperDim == this;
            }
            set
            {
                if (LesserSet == null) return;
                if (value == true) // Include
                {
                    if (IsInLesserSet) return;
                    LesserSet.SuperDim = this;
                }
                else // Exclude
                {
                    LesserSet.SuperDim = null;
                }
            }
        }

        #endregion

        public DimSuper(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            IsIdentity = true;
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension accordingly. It is super dimension.
	    }
    }
}
