using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// Top set in a poset of all sets. It is a parent for all primitive sets.
    /// 
    /// Top set is used to represent a whole database like a local mash up or a remote database. 
    /// It also can describe how its instances are loaded from a remote source and stored.
    /// </summary>
    public class SetTop : Set, CsSchema
    {
        public DataSourceType DataSourceType { get; protected set; } // Where data is stored and processed (engine). Replace class name

        public override int Width
        {
            get { return 0; }
        }

        public override int Length
        {
            get { return 0; }
        }

        public List<Set> PrimitiveSubsets
        {
            get { return SubDims.Where(x => x.LesserSet.IsPrimitive).Select(x => x.LesserSet).ToList(); }
        }

        public SetPrimitive GetPrimitiveSubset(string name)
        {
            Dim dim = SubDims.FirstOrDefault(x => x.LesserSet.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            Debug.Assert(dim.LesserSet.IsPrimitive, "Wrong use: Immediate subsets of top must be primitive sets.");
            return dim != null ? (SetPrimitive)dim.LesserSet : null;
        }

        public virtual DataTable Export(Set set)
        {
            // Check if this set is our child
            DataTable dataTable = new DataTable(set.Name);
            // Add rows by reading them from this set local dimensions
            return null;
        }

        public virtual DataTable ExportAll(Set set)
        {
            // Check if this set is our child
            DataTable dataTable = new DataTable(set.Name);
            // Add rows by reading them from this set local dimensions
            return null;
        }

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            Debug.Assert(name.Equals("Super", StringComparison.InvariantCultureIgnoreCase), "Wrong use: only super-dimensions can reference a root.");

            return new DimTop(name, lesserSet, this);
        }

        private void CreateDataTypes() // Create all primitive data types from some specification like Enum, List or XML
        {

            foreach (DataType dataType in (DataType[])Enum.GetValues(typeof(DataType)))
            {
                if (dataType == DataType.Root) // Root has a special class
                {
                    SetRoot setRoot = new SetRoot(DataType.Root);
                    AddSubset(setRoot);
                    setRoot.DimType = typeof(DimTop);
                }
                else
                {
                    SetPrimitive setPrimitive = new SetPrimitive(dataType);
                    AddSubset(setPrimitive);

                    switch (dataType) // Set properties explicitly for each data type
                    {
                        case DataType.Void:
                        case DataType.Top:
                        case DataType.Bottom:
                            break;
                        case DataType.Root:
                            setPrimitive.DimType = typeof(DimTop);
                            break;
                        case DataType.Integer:
                            setPrimitive.DimType = typeof(DimPrimitive<int>);
                            break;
                        case DataType.Double:
                            setPrimitive.DimType = typeof(DimPrimitive<double>);
                            break;
                        case DataType.Decimal:
                            setPrimitive.DimType = typeof(DimPrimitive<decimal>);
                            break;
                        case DataType.String:
                            setPrimitive.DimType = typeof(DimPrimitive<string>);
                            break;
                        case DataType.Boolean:
                            setPrimitive.DimType = typeof(DimPrimitive<bool>);
                            break;
                        case DataType.DateTime:
                            setPrimitive.DimType = typeof(DimPrimitive<DateTime>);
                            break;
                        default:
                            Debug.Fail("No definition for this type provided. Update configuration of primitive types.");
                            break;
                    }
                }
            }
        }

        #region CsSchema

        public CsTable T(string name)
        {
            Set set = FindSubset(name);
            return set;
        }

        #endregion

        public SetTop()
            : base()
        {
            DataSourceType = DataSourceType.LOCAL;
        }

        public SetTop(string name)
            : base(name)
        {
            IsInstantiable = false;

            CreateDataTypes(); // Generate all predefined primitive sets as subsets
        }

    }

    /// <summary>
    /// Primitive data types used in our local database system. 
    /// We need to enumerate data types for each kind of database along with the primitive mappings to other databases.
    /// </summary>
    public enum DataType
    {
        // Built-in types in C#: http://msdn.microsoft.com/en-us/library/vstudio/ya5y69ds.aspx
        Void, // Nothing, no value. Can be equivalent to Top or Null.
        Top,
        Bottom,
        Root, // It is surrogate or reference
        Integer,
        Double,
        Decimal,
        String,
        Boolean,
        DateTime,
    }

}
