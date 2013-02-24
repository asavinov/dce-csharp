using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public override int Width
        {
            get { return sizeof(double); }
        }

        public virtual Dimension CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            return new DimPrimitive<double>(name, lesserSet, this);
        }

        #region Constructors and initializers.

        public SetDouble(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as double virtual set. Important: isPrimitive
            _name = "Double"; // So the parameter is ignored
            _instantiable = false;
        }

        #endregion
    }
}
