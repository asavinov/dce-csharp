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
    public class SetTop : Set
    {
        private DataSourceType _type=DataSourceType.LOCAL; // Where data is stored and processed (engine)
        public DataSourceType DataSourceType
        {
            get { return _type; }
        }

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

        public Set GetPrimitiveSubset(string name)
        {
            Dim dim = SubDims.FirstOrDefault(x => x.LesserSet.IsPrimitive && x.LesserSet.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            return dim != null ? dim.LesserSet : null;
        }

        public virtual string MapToLocalType(string externalType)
        {
            string localType = null;

            switch (externalType)
            {
                case "Double": // System.Data.OleDb.OleDbType.Double "Numeric"
                case "Numeric":
                    localType = "Double";
                    break;
                case "Integer": // System.Data.OleDb.OleDbType.Integer
                case "UnsignedTinyInt":
                case "SmallInt":
                    localType = "Integer";
                    break;
                case "Char": // System.Data.OleDb.OleDbType.Char
                case "VarChar": // System.Data.OleDb.OleDbType.VarChar
                case "VarWChar": // System.Data.OleDb.OleDbType.VarWChar
                case "WChar": // System.Data.OleDb.OleDbType.WChar
                    localType = "String";
                    break;
                case "Boolean":
                    localType = "Boolean";
                    break;
                case "Date":
                    localType = "String";
                    break;
                case "Currency":
                    localType = "Double";
                    break;
                default:
                    localType = externalType; // The same type name
                    break;
            }

            return localType;
        }

        public virtual Set MapToLocalSet(Set externalSet)
        {
            string externalType = externalSet.Name;
            Set localSet = null;
            string localType = null;

            if (externalSet.IsTop)
            {
                return this; // Top is mapped to top by definition
            }

            if (externalSet.IsPrimitive)
            {
                // Primitive sets never point to a remote source so we use some predefined mapping rules
                localType = MapToLocalType(externalType);
                localSet = GetPrimitiveSubset(localType);
                return localSet;
            }

            // TODO: try to find explicitly marked sets (same as)

            foreach (Set set in GetAllSubsets()) // Second, we try to find by semantics
            {
                if (set.IsTop) continue;
                if (set.IsPrimitive) continue;

                if (set.Name.Equals(externalType, StringComparison.InvariantCultureIgnoreCase))
                {
                    localSet = set; // Found.
                    return localSet;
                }
            }

            return localSet;
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

        public SetTop(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            IsInstantiable = false;

            //
            // Generate all predefined primitive sets as subsets
            //
            SetRoot setRoot = new SetRoot("Root");
            AddSubset(setRoot);

            SetInteger setInteger = new SetInteger("Integer");
            AddSubset(setInteger);

            SetDouble setDouble = new SetDouble("Double");
            AddSubset(setDouble);

            SetString setString = new SetString("String");
            AddSubset(setString);

            SetBoolean setBoolean = new SetBoolean("Boolean");
            AddSubset(setBoolean);
        }
    }

    /// <summary>
    /// Primitive data types used in our local database system. 
    /// We need to enumerate data types for each kind of database along with the primitive mappings to other databases.
    /// See also OleDb types: System.Data.OleDb.OleDbType.*
    /// </summary>
    public enum DataType
    {
        Double,
        Integer,
        String,
        Boolean
    }
}
