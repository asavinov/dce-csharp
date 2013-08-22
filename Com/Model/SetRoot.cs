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
    /// The root set is a predefined primitive set with no instances and no superset. 
    /// It has a number of predefined (primitive) subsets which do not have greater sets. 
    /// 
    /// The root is normally used to represent a database, connection, data source or mash up. 
    /// It may also describe how its instances are loaded (populated) in terms of source databases. It is not clear where it is described (for each set or dimension).
    /// </summary>
    public class SetRoot : Set
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

            if (externalSet.IsRoot)
            {
                return this; // Root is mapped to root by definition
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
                if (set.IsRoot) continue;
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

        public SetRoot(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            IsInstantiable = false;

            //
            // Generate all predefined primitive sets as subsets
            //
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
    /// Primitive data types used in our database system. 
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
