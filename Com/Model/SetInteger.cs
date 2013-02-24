﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    /// <summary>
    /// A primitive, virtual or predefined set of all elements. 
    /// It does not store real instance and has one formal super-dimension and no greater dimensions.
    /// </summary>
    class SetInteger : Set
    {
        public override int Length
        {
            get { return int.MaxValue; } // It is the number of all integers
        }

        public override int Width
        {
            get { return sizeof(int); } // It is not absolutely true because there can be integers of different length like Int32 and Int64
        }

        public virtual Dimension CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            return new DimPrimitive<int>(name, lesserSet, this);
        }

        #region Constructors and initializers.

        public SetInteger(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as double virtual set. Important: isPrimitive
            _name = "Integer"; // So the parameter is ignored
            _instantiable = false;
        }

        #endregion
    }
}