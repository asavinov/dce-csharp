using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// A set which may have subsets, geater or lesser sets, and instances. 
    /// </summary>
    public class ConceptWithInstances : Concept
    {
        #region Instance (function) methods

        public override int InstanceSize
        {
            get { return sizeof(int); }
        }

        protected int _instanceCount;
        public override int InstanceCount
        {
            get { return _instanceCount; }
        }

        public void AddInstance()
        {
            InsertInstance(InstanceCount);
        }
        public void InsertInstance(int offset)
        {
            _instanceCount++;
            // TODO: Append all dimensions in loop including super-dim and special dims
        }
        public void RemoveInstance(int offset)
        {
            _instanceCount--;
            // TODO: Remove it from all dimensions in loop including super-dim and special dims
        }
        public void SetInstance(DimAbstract dimension, int offset, int value)
        {
            // TODO: Insert integer. Delegate to the dimension.
        }
        public void SetInstance(DimDouble dimension, int offset, double value)
        {
            // TODO: Insert double. Delegate to the dimension.
        }

        #endregion

        #region Constructors and initializers.

        #endregion
    }
}
