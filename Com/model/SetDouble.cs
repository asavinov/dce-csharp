using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// Formal (virtual) set of all doubles. It does not store real instance and has one formal super-dimension. 
    /// </summary>
    class SetDouble : ConceptWithInstances
    {
        public override int InstanceCount
        {
            get { return int.MaxValue; } // In fact, it has to be the number of all doubles
        }

        public override int InstanceSize
        {
            get { return sizeof(double); }
        }

        #region Constructors and initializers.

        #endregion
    }
}
