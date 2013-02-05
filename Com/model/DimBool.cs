using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// This dimension leads from a normal set to a boolean set which consists of two elements 0 (false) and 1 (true).
    /// </summary>
    public class DimBool : DimPrimitive<bool>
    {
        public DimBool(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension accordingly. It is super dimension.
	    }
    }
}
