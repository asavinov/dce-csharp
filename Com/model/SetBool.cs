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
    class SetBool : Set
    {
        public override int InstanceCount
        {
            get { return 2; } // It is the number of all boolean values, that is, 0 and 1
        }

        public override int InstanceSize
        {
            get { return sizeof(bool); } // It actually depends on how it is represented and depends on the dimension.
        }

        #region Constructors and initializers.

        public SetBool(string name)
            : base(name)
        {
            // TODO: Parameterize this instance as boolean virtual set. Important: isPrimitive
            _name = "Bool"; // So the parameter is ignored
            _instantiable = false;
        }

        #endregion
    }
}
