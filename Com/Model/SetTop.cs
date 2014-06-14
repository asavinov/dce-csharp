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

        #region CsSchema

        public CsTable GetPrimitive(string name)
        {
            CsColumn dim = SubDims.FirstOrDefault(x => x.LesserSet.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            return dim != null ? dim.LesserSet : null;
        }

        public CsTable Root { get { return GetPrimitive("Root"); } }


        //
        // Factories for tables and columns
        //

        public CsTable CreateTable(String name) 
        {
            CsTable table = new Set(name);
            return table;
        }

        public CsTable AddTable(CsTable table, CsTable parent)
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: users do not create/delete primitive sets - they are part of the schema.");

            if (parent == null)
            {
                parent = Root;
            }

            Dim dim = new DimSuper("Super", (Set)table, this, true, true);

            dim.Add();

            return table;
        }

        public CsTable RemoveTable(CsTable table) 
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: users do not create/delete primitive sets - they are part of the schema.");
            foreach(CsColumn col in LesserDims) 
            {
                col.Remove();
            }
            foreach (CsColumn col in GreaterDims)
            {
                col.Remove();
            }

            return table; 
        }

        public CsColumn CreateColumn(string name, CsTable input, CsTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            CsColumn dim = null;

            if (output.Name.Equals("Void", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new Dim(name, (Set)input, (Set)output, isKey, true);
            }
            else if (output.Name.Equals("Top", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new Dim(name, (Set)input, (Set)output, isKey, true);
            }
            else if (output.Name.Equals("Bottom", StringComparison.InvariantCultureIgnoreCase)) // Not possible by definition
            {
            }
            else if (output.Name.Equals("Root", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new DimSuper(name, (Set)input, (Set)output, isKey, true);
            }
            else if (output.Name.Equals("Integer", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new DimPrimitive<int>(name, (Set)input, (Set)output, isKey, false);
            }
            else if (output.Name.Equals("Double", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new DimPrimitive<double>(name, (Set)input, (Set)output, isKey, false);
            }
            else if (output.Name.Equals("Decimal", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new DimPrimitive<decimal>(name, (Set)input, (Set)output, isKey, false);
            }
            else if (output.Name.Equals("String", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new DimPrimitive<string>(name, (Set)input, (Set)output, isKey, false);
            }
            else if (output.Name.Equals("Boolean", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new DimPrimitive<bool>(name, (Set)input, (Set)output, isKey, false);
            }
            else if (output.Name.Equals("DateTime", StringComparison.InvariantCultureIgnoreCase)) 
            {
                dim = new DimPrimitive<DateTime>(name, (Set)input, (Set)output, isKey, false);
            }
            else if (output.Name.Equals("Set", StringComparison.InvariantCultureIgnoreCase))
            {
            }
            else // User (non-primitive) set
            {
                dim = new DimSuper(name, (Set)input, (Set)output, isKey, false);
            }

            return dim;
        }

        #endregion

        public override int Width
        {
            get { return 0; }
        }

        public override int Length
        {
            get { return 0; }
        }

        public DataSourceType DataSourceType { get; protected set; } // Where data is stored and processed (engine). Replace class name

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

        private void CreateDataTypes() // Create all primitive data types from some specification like Enum, List or XML
        {
            Set set;
            DimTop dim;

            set = new Set("Root");
            set.DimType = typeof(DimTop);
            dim = new DimTop("Top", set, this, true, true);
            dim.Add();

            set = new Set("Integer");
            set.DimType = typeof(DimPrimitive<int>);
            dim = new DimTop("Top", set, this, true, true);
            dim.Add();

            set = new Set("Double");
            set.DimType = typeof(DimPrimitive<double>);
            dim = new DimTop("Top", set, this, true, true);
            dim.Add();

            set = new Set("Decimal");
            set.DimType = typeof(DimPrimitive<decimal>);
            dim = new DimTop("Top", set, this, true, true);
            dim.Add();

            set = new Set("String");
            set.DimType = typeof(DimPrimitive<string>);
            dim = new DimTop("Top", set, this, true, true);
            dim.Add();

            set = new Set("Boolean");
            set.DimType = typeof(DimPrimitive<bool>);
            dim = new DimTop("Top", set, this, true, true);
            dim.Add();

            set = new Set("DateTime");
            set.DimType = typeof(DimPrimitive<DateTime>);
            dim = new DimTop("Top", set, this, true, true);
            dim.Add();
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
    public enum CsDataType
    {
        // Built-in types in C#: http://msdn.microsoft.com/en-us/library/vstudio/ya5y69ds.aspx
        Void, // Null, Nothing, Empty no value. Can be equivalent to Top or Top.
        Top,
        Bottom,
        Root, // It is surrogate or reference
        Integer,
        Double,
        Decimal,
        String,
        Boolean,
        DateTime,
        Set, // It is any set that is not root (non-primititve type). Arbitrary user-defined name.
    }

}
