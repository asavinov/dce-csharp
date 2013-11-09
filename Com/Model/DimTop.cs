﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// This super dimension leads to an empty set (root) and does not store any elements. 
    /// </summary>
    public class DimTop : DimSuper
    {
        public DimTop(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension accordingly. It is super dimension.
	    }
    }
}