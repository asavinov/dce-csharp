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

            Dim dim = new Dim("Super", table, this, true, true);

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

            CsColumn dim = new Dim(name, input, output, isKey, false);

            return dim;
        }

        #endregion

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
            Dim dim;

            set = new Set("Root");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            set = new Set("Integer");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            set = new Set("Double");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            set = new Set("Decimal");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            set = new Set("String");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            set = new Set("Boolean");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();

            set = new Set("DateTime");
            dim = new Dim("Top", set, this, true, true);
            dim.Add();
        }

        public SetTop(string name)
            : base(name)
        {
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
