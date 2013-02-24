using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    /// <summary>
    /// This dimension leads from a normal set to a normal set.
    /// </summary>
    public class DimSet : DimPrimitive<int>
    {
        public DimSet(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension accordingly. It is super dimension.
	    }
    }
}
