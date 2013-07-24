using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// A primitive, virtual or predefined set of all elements. 
    /// It does not store real instance and has one formal super-dimension and no greater dimensions.
    /// </summary>
    class SetString : Set
    {
        public override int Length
        {
            get { return int.MaxValue; } // It is the number of all strings
        }

        public virtual Type SystemType
        {
            get { return typeof(string); }
        }

        public override int Width
        {
            get { return int.MaxValue; } // We assume that a string may have any length (unlimited)
        }

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            return new DimPrimitive<string>(name, lesserSet, this);
        }

        #region Constructors and initializers.

        public SetString(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as double virtual set. Important: isPrimitive
            Name = "String"; // So the parameter is ignored
            IsInstantiable = false;
            IsPrimitive = true;
        }

        #endregion
    }
}
