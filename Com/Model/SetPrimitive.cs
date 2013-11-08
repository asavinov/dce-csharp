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
    public class SetPrimitive : Set
    {
        public Enum DataType { get; protected set; }

        public override int Length
        {
            get { return -1; }
        }

/*
        public override Type SystemType
        {
            get
            {
                return Top.TypeSystemType;
            }
        }

        public override int Width
        {
            get
            {
                return Top.TypeWidth;
            }
        }
*/
        #region Constructors and initializers.

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            Dim dim;
            if (DimType != null)
            {
                dim = (Dim)Activator.CreateInstance(DimType, new object[] { name, lesserSet, this });
            }
            else
            {
                dim = base.CreateDefaultLesserDimension(name, lesserSet);
            }

            return dim;
        }

        public SetPrimitive(Enum dataType)
            : base(null)
        {
            DataType = dataType;

            Name = dataType.ToString();

            IsInstantiable = false;
            IsPrimitive = true;
        }

        #endregion
    }

}
