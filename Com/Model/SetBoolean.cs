﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// A primitive, virtual or predefined set of all elements. 
    /// It does not store real instance and has one formal super-dimension and no greater dimensions.
    /// </summary>
    class SetBoolean : Set
    {
        public override int Length
        {
            get { return 2; } // It is the number of all boolean values, that is, 0 and 1
        }

        public virtual Type SystemType
        {
            get { return typeof(bool); }
        }

        public override int Width
        {
            get { return sizeof(bool); } // It actually depends on how it is represented and depends on the dimension.
        }

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            return new DimPrimitive<bool>(name, lesserSet, this);
        }

        #region Constructors and initializers.

        public SetBoolean(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as boolean virtual set. Important: isPrimitive
            Name = "Boolean"; // So the parameter is ignored
            IsInstantiable = false;
            IsPrimitive = true;
        }

        #endregion
    }
}
