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
        public DimSuper(string name, Set lesserSet, Set greaterSet) 
            : this(name, lesserSet, greaterSet, true, true)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension accordingly. It is super dimension.
	    }

        public DimSuper(string name, Set lesserSet, Set greaterSet, bool isIdentity, bool isSuper)
            : base(name, lesserSet, greaterSet, isIdentity, isSuper)
        {
        }
    }
}
