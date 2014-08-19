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

        #region CsSchema interface

        public CsTable GetPrimitive(string name)
        {
            CsColumn dim = SubDims.FirstOrDefault(x => StringSimilarity.SameTableName(x.LesserSet.Name, name));
            return dim != null ? dim.LesserSet : null;
        }

        public CsTable Root { get { return GetPrimitive("Root"); } }


        //
        // Factories for tables and columns
        //

        public virtual CsTable CreateTable(String name) 
        {
            CsTable table = new Set(name);
            return table;
        }

        public virtual CsTable AddTable(CsTable table, CsTable parent, string superName)
        {
            if (parent == null)
            {
                parent = Root;
            }
            if (string.IsNullOrEmpty(superName))
            {
                superName = "Super";
            }

            Dim dim = new Dim(superName, table, parent, true, true);

            dim.Add();

            return table;
        }

        public virtual CsTable RemoveTable(CsTable table) 
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: users do not create/delete primitive sets - they are part of the schema.");
            foreach (CsColumn col in table.LesserDims.ToList()) 
            {
                col.Remove();
            }
            foreach (CsColumn col in table.GreaterDims.ToList())
            {
                col.Remove();
            }

            return table; 
        }

        public void RenameTable(CsTable table, string newName)
        {
            RenameElement(table, newName);
        }

        public virtual CsColumn CreateColumn(string name, CsTable input, CsTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            CsColumn dim = new Dim(name, input, output, isKey, false);

            return dim;
        }

        public void RenameColumn(CsColumn column, string newName)
        {
            RenameElement(column, newName);
        }

        protected void RenameElement(object element, string newName)
        {
            CsSchema schema = this;

            //
            // Check all elements of the schema that can store column or table name (tables, columns etc.)
            // Update their definition so that it uses the new name of the specified element
            //
            List<CsTable> tables = schema.GetAllSubsets();
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.GreaterDims)
                {
                    if (col.Definition == null) continue;

                    if (col.Definition.Formula != null)
                    {
                        if(element is CsTable) nodes = col.Definition.Formula.Find((CsTable)element);
                        else if (element is CsColumn) nodes = col.Definition.Formula.Find((CsColumn)element);
                        nodes.ForEach(x => x.Name = newName);
                    }
                    if (col.Definition.WhereExpression != null)
                    {
                        if (element is CsTable) nodes = col.Definition.WhereExpression.Find((CsTable)element);
                        else if (element is CsColumn) nodes = col.Definition.WhereExpression.Find((CsColumn)element);
                        nodes.ForEach(x => x.Name = newName);
                    }
                }

                if (tab.Definition == null) continue;

                // Update table definitions by finding the uses of the specified column
                if (tab.Definition.WhereExpression != null)
                {
                    if (element is CsTable) nodes = tab.Definition.WhereExpression.Find((CsTable)element);
                    else if (element is CsColumn) nodes = tab.Definition.WhereExpression.Find((CsColumn)element);
                    nodes.ForEach(x => x.Name = newName);
                }
                if (tab.Definition.OrderbyExpression != null)
                {
                    if (element is CsTable) nodes = tab.Definition.OrderbyExpression.Find((CsTable)element);
                    else if (element is CsColumn) nodes = tab.Definition.OrderbyExpression.Find((CsColumn)element);
                    nodes.ForEach(x => x.Name = newName);
                }
            }

            if (element is CsTable) ((CsTable)element).Name = newName;
            else if (element is CsColumn) ((CsColumn)element).Name = newName;
        }

        #endregion

        public DataSourceType DataSourceType { get; protected set; } // Where data is stored and processed (engine). Replace class name

        protected virtual void CreateDataTypes() // Create all primitive data types from some specification like Enum, List or XML
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
