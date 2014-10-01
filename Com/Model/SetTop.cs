﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

using Newtonsoft.Json.Linq;

namespace Com.Model
{
    /// <summary>
    /// Top set in a poset of all sets. It is a parent for all primitive sets.
    /// 
    /// Top set is used to represent a whole database like a local mash up or a remote database. 
    /// It also can describe how its instances are loaded from a remote source and stored.
    /// </summary>
    public class SetTop : Set, ComSchema
    {

        #region ComSchema interface

        public ComTable GetPrimitive(string name)
        {
            ComColumn dim = SubColumns.FirstOrDefault(x => StringSimilarity.SameTableName(x.Input.Name, name));
            return dim != null ? dim.Input : null;
        }

        public ComTable Root { get { return GetPrimitive("Root"); } }


        //
        // Factories for tables and columns
        //

        public virtual ComTable CreateTable(String name) 
        {
            ComTable table = new Set(name);
            return table;
        }

        public virtual ComTable AddTable(ComTable table, ComTable parent, string superName)
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

        public virtual void DeleteTable(ComTable table) 
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: users do not create/delete primitive sets - they are part of the schema.");

            foreach (ComColumn col in table.InputColumns.ToList()) 
            {
                col.Remove();
            }
            foreach (ComColumn col in table.Columns.ToList())
            {
                col.Remove();
            }
        }

        public void RenameTable(ComTable table, string newName)
        {
            RenameElement(table, newName);
        }

        public virtual ComColumn CreateColumn(string name, ComTable input, ComTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            ComColumn dim = new Dim(name, input, output, isKey, false);

            return dim;
        }

        public virtual void DeleteColumn(ComColumn column)
        {
            Debug.Assert(!column.Input.IsPrimitive, "Wrong use: top columns cannot be created/deleted.");

            ComSchema schema = this;

            //
            // Delete all expression nodes that use the deleted column and all references to this column from other objects
            //
            List<ComTable> tables = schema.AllSubTables;
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.Columns)
                {
                    if (col.Definition == null) continue;

                    if (col.Definition.Formula != null)
                    {
                        nodes = col.Definition.Formula.Find(column);
                        foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                    }
                    if (col.Definition.WhereExpr != null)
                    {
                        nodes = col.Definition.WhereExpr.Find(column);
                        foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                    }

                    if (col.Definition.Mapping != null)
                    {
                        foreach (var match in col.Definition.Mapping.Matches.ToList())
                        {
                            if (match.SourcePath.IndexOf(column) >= 0 || match.TargetPath.IndexOf(column) >= 0)
                            {
                                col.Definition.Mapping.Matches.Remove(match);
                            }
                        }
                    }
                    if (col.Definition.GroupPaths != null)
                    {
                        foreach (var path in col.Definition.GroupPaths.ToList())
                        {
                            if (path.IndexOf(column) >= 0)
                            {
                                col.Definition.GroupPaths.Remove(path);
                            }
                        }
                    }
                    if (col.Definition.MeasurePaths != null)
                    {
                        foreach (var path in col.Definition.MeasurePaths.ToList())
                        {
                            if (path.IndexOf(column) >= 0)
                            {
                                col.Definition.MeasurePaths.Remove(path);
                            }
                        }
                    }

                }

                if (tab.Definition == null) continue;

                // Update table definitions by finding the uses of the specified column
                if (tab.Definition.WhereExpression != null)
                {
                    nodes = tab.Definition.WhereExpression.Find(column);
                    foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                }
                if (tab.Definition.OrderbyExpression != null)
                {
                    nodes = tab.Definition.OrderbyExpression.Find(column);
                    foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                }
            }

            column.Remove();
        }

        public void RenameColumn(ComColumn column, string newName)
        {
            RenameElement(column, newName);
        }

        protected void RenameElement(object element, string newName)
        {
            ComSchema schema = this;

            //
            // Check all elements of the schema that can store column or table name (tables, columns etc.)
            // Update their definition so that it uses the new name of the specified element
            //
            List<ComTable> tables = schema.AllSubTables;
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.Columns)
                {
                    if (col.Definition == null) continue;

                    if (col.Definition.Formula != null)
                    {
                        if(element is ComTable) nodes = col.Definition.Formula.Find((ComTable)element);
                        else if (element is ComColumn) nodes = col.Definition.Formula.Find((ComColumn)element);
                        nodes.ForEach(x => x.Name = newName);
                    }
                    if (col.Definition.WhereExpr != null)
                    {
                        if (element is ComTable) nodes = col.Definition.WhereExpr.Find((ComTable)element);
                        else if (element is ComColumn) nodes = col.Definition.WhereExpr.Find((ComColumn)element);
                        nodes.ForEach(x => x.Name = newName);
                    }
                }

                if (tab.Definition == null) continue;

                // Update table definitions by finding the uses of the specified column
                if (tab.Definition.WhereExpression != null)
                {
                    if (element is ComTable) nodes = tab.Definition.WhereExpression.Find((ComTable)element);
                    else if (element is ComColumn) nodes = tab.Definition.WhereExpression.Find((ComColumn)element);
                    nodes.ForEach(x => x.Name = newName);
                }
                if (tab.Definition.OrderbyExpression != null)
                {
                    if (element is ComTable) nodes = tab.Definition.OrderbyExpression.Find((ComTable)element);
                    else if (element is ComColumn) nodes = tab.Definition.OrderbyExpression.Find((ComColumn)element);
                    nodes.ForEach(x => x.Name = newName);
                }
            }

            if (element is ComTable) ((ComTable)element).Name = newName;
            else if (element is ComColumn) ((ComColumn)element).Name = newName;
        }

        #endregion

        public DataSourceType DataSourceType { get; protected set; } // Where data is stored and processed (engine). Replace class name

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["DataSourceType"] = (int)DataSourceType;

            // List of all tables
            JArray tables = new JArray();
            JArray columns = new JArray(); // One array for all columns of all tables (not within each tabel)
            foreach (ComTable comTable in this.AllSubTables)
            {
                if (comTable.IsPrimitive) continue;

                JObject table = Utils.CreateJsonFromObject(comTable);
                comTable.ToJson(table);
                tables.Add(table);

                // List of columns
                foreach (ComColumn comColumn in comTable.Columns)
                {
                    JObject column = Utils.CreateJsonFromObject(comColumn);
                    comColumn.ToJson(column);
                    columns.Add(column);
                }
            }
            json["tables"] = tables;
            json["columns"] = columns;
        }

        public override void FromJson(JObject json, Workspace ws)
        {
            base.FromJson(json, ws); // Set

            DataSourceType = json["DataSourceType"] != null ? (DataSourceType)(int)json["DataSourceType"] : DataSourceType.LOCAL;

            // List of tables
            JArray tables = (JArray)json["tables"];
            foreach (JObject table in tables)
            {
                ComTable comTable = (ComTable)Utils.CreateObjectFromJson(table);
                if (comTable != null)
                {
                    comTable.FromJson(table, ws);
                    this.AddTable(comTable, null, null);
                }
            }

            // List of columns
            // Columns cannot be loaded because not all schemas might have been loaded (so it is a problem with import columns)
        }

        #endregion

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

        public SetTop()
            : this("")
        {
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

    public enum DataSourceType // Essentially, it a marker for a subclass of SetTop (Schema)
    {
        LOCAL, // This database
        ACCESS,
        OLEDB,
        SQL, // Generic (standard) SQL
        CSV,
        ODATA,
        EXCEL
    }
}
