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
    class SetDouble : Set
    {
        public override int Length
        {
            get { return int.MaxValue; } // In fact, it has to be the number of all doubles
        }

        public virtual Type SystemType
        {
            get { return typeof(double); }
        }

        public override int Width
        {
            get { return sizeof(double); }
        }

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            return new DimPrimitive<double>(name, lesserSet, this);
        }

        #region Constructors and initializers.

        public SetDouble(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as double virtual set. Important: isPrimitive
            Name = "Double"; // So the parameter is ignored
            IsInstantiable = false;
            IsPrimitive = true;
        }

        #endregion
    }
}
