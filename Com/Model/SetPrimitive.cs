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
        public DataType Type { get; private set; }

        public override int Length
        {
            get { return -1; }
        }

        // Built-in types in C#: http://msdn.microsoft.com/en-us/library/vstudio/ya5y69ds.aspx
        public override Type SystemType
        {
            get
            {
                switch (Type)
                {
                    case DataType.Root: 
                        return typeof(Offset);
                    case DataType.Integer:
                        return typeof(int); // System.Int32
                    case DataType.Double:
                        return typeof(double); // System.Double
                    case DataType.String:
                        return typeof(string); // System.String
                    case DataType.Boolean:
                        return typeof(bool); // System.Boolean
                    default:
                        return null;
                }
            }
        }

        public override int Width
        {
            get
            {
                switch (Type)
                {
                    case DataType.Root:
                        return sizeof(Offset);
                    case DataType.Integer:
                        return sizeof(int);
                    case DataType.Double:
                        return sizeof(double);
                    case DataType.String:
                        return IntPtr.Size; //Or we should return the length of the string itself?
                    case DataType.Boolean:
                        return sizeof(bool);
                    default:
                        return 0;
                }
            }
        }

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            switch (Type)
            {
                case DataType.Root:
                    Debug.Assert(name.Equals("Super", StringComparison.InvariantCultureIgnoreCase), "Wrong use: only super-dimensions can reference a root.");
                    return new DimPrimitive<Offset>(name, lesserSet, this); // Or DimTop ??? Actually, we do not allow for referencing the root directly (only if polymorphism is enabled)
                case DataType.Integer:
                    return new DimPrimitive<int>(name, lesserSet, this);
                case DataType.Double:
                    return new DimPrimitive<double>(name, lesserSet, this);
                case DataType.String:
                    return new DimPrimitive<string>(name, lesserSet, this);
                case DataType.Boolean:
                    return new DimPrimitive<bool>(name, lesserSet, this);
                default:
                    return null;
            }
        }

        #region Constructors and initializers.

        public SetPrimitive(DataType type)
            : base(null)
        {
            IsInstantiable = false;
            IsPrimitive = true;

            Type = type;

            switch (Type)
            {
                case DataType.Root:
                    Name = "Root";
                    break;
                case DataType.Integer:
                    Name = "Integer"; 
                    break;
                case DataType.Double:
                    Name = "Double"; 
                    break;
                case DataType.String:
                    Name = "String"; 
                    break;
                case DataType.Boolean:
                    Name = "Boolean"; 
                    break;
                default:
                    Name = null; 
                    break;
            }
        }

        #endregion
    }

    /// <summary>
    /// Primitive data types used in our local database system. 
    /// We need to enumerate data types for each kind of database along with the primitive mappings to other databases.
    /// </summary>
    public enum DataType
    {
        Root,
        Integer,
        Double,
        String,
        Boolean
    }
}
