using System;
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
    class SetInteger : Set
    {
        public override int Length
        {
            get { return int.MaxValue; } // It is the number of all integers
        }

        public virtual Type SystemType
        {
            get { return typeof(int); }
        }

        public override int Width
        {
            get { return sizeof(int); } // It is not absolutely true because there can be integers of different length like Int32 and Int64
        }

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            return new DimPrimitive<int>(name, lesserSet, this);
        }

        #region Constructors and initializers.

        public SetInteger(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as double virtual set. Important: isPrimitive
            Name = "Integer"; // So the parameter is ignored
            IsInstantiable = false;
            IsPrimitive = true;
        }

        #endregion
    }
}
