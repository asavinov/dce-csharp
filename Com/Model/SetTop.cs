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

            foreach (CsDataType dataType in (CsDataType[])Enum.GetValues(typeof(CsDataType)))
            {
                if (dataType == CsDataType.Root) // Root has a special class
                {
                    SetRoot setRoot = new SetRoot(CsDataType.Root);
                    AddSubset(setRoot);
                    setRoot.DimType = typeof(DimTop);
                }
                else
                {
                    SetPrimitive setPrimitive = new SetPrimitive(dataType);
                    AddSubset(setPrimitive);

                    switch (dataType) // Set properties explicitly for each data type
                    {
                        case CsDataType.Void:
                        case CsDataType.Top:
                        case CsDataType.Bottom:
                            break;
                        case CsDataType.Root:
                            setPrimitive.DimType = typeof(DimTop);
                            break;
                        case CsDataType.Integer:
                            setPrimitive.DimType = typeof(DimPrimitive<int>);
                            break;
                        case CsDataType.Double:
                            setPrimitive.DimType = typeof(DimPrimitive<double>);
                            break;
                        case CsDataType.Decimal:
                            setPrimitive.DimType = typeof(DimPrimitive<decimal>);
                            break;
                        case CsDataType.String:
                            setPrimitive.DimType = typeof(DimPrimitive<string>);
                            break;
                        case CsDataType.Boolean:
                            setPrimitive.DimType = typeof(DimPrimitive<bool>);
                            break;
                        case CsDataType.DateTime:
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

}
