using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// It is a special case of super-dimension.
    /// If super is many-to-one (many extensions exist for one parent) then the subset dimension is one-to-one (zero or one extension exist for one super-element).
    /// 
    /// This restriction has more efficient implementations of one-to-one mappings and therefore we introduce this class. 
    /// Also some operations can be implemented more efficiently. For example, projection size is the same as input size. 
    /// 
    /// </summary>
    public class DimSubset : DimSuper
    {

        public DimSubset(string name, Set lesserSet, Set greaterSet)
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if output is of correct type
	    }

    }
}
