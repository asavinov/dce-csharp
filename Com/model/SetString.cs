using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// A primitive, virtual or predefined set of all elements. 
    /// It does not store real instance and has one formal super-dimension and no greater dimensions.
    /// </summary>
    class SetString : Set
    {
        public override int InstanceCount
        {
            get { return int.MaxValue; } // It is the number of all strings
        }

        public override int InstanceSize
        {
            get { return int.MaxValue; } // We assume that a string may have any length (unlimited)
        }

        #region Constructors and initializers.

        public SetString(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as double virtual set. Important: isPrimitive
            _name = "String"; // So the parameter is ignored
            _instantiable = false;
        }

        #endregion
    }
}
