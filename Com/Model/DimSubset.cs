using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// 
    /// Is it a subset? Is it (should it be) implemented via super dimension or a normal special dimension? If it is a special super dimension then probably we need to extend standard super.
    /// If standard super is one-to-many then the subset dimension is one-to-one.
    /// 
    /// A special implementation of int-int mapping with the following features:
    /// - only one dimension with special meaning
    /// - this dimension supports one-to-one mapping (project and then de-project produces the same elements) 
    /// - projection has the same size as input 
    /// - de-projection are smaller because not all elements are referenced 
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
