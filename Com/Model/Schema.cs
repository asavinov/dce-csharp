using System;
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
    public class Schema : Set, DcSchema
    {

        #region ComSchema interface

        public Workspace Workspace { get; set; }
        
        public DcTable GetPrimitive(string name)
        {
            DcColumn dim = SubColumns.FirstOrDefault(x => StringSimilarity.SameTableName(x.Input.Name, name));
            return dim != null ? dim.Input : null;
        }

        public DcTable Root { get { return GetPrimitive("Root"); } }


        //
        // Factories for tables and columns
        //

        public virtual DcTable CreateTable(String name) 
        {
            DcTable table = new Set(name);
            return table;
        }

        public virtual DcTable AddTable(DcTable table, DcTable parent, string superName)
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

        public virtual void DeleteTable(DcTable table) 
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: users do not create/delete primitive sets - they are part of the schema.");

            List<DcColumn> toRemove;
            toRemove = table.InputColumns.ToList();
            foreach (DcColumn col in toRemove) 
            {
                col.Remove();
            }
            toRemove = table.Columns.ToList();
            foreach (DcColumn col in toRemove)
            {
                col.Remove();
            }
        }

        public void RenameTable(DcTable table, string newName)
        {
            TableRenamed(table, newName); // Rename with propagation
            table.Name = newName;
        }

        public virtual DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            DcColumn dim = new Dim(name, input, output, isKey, false);

            return dim;
        }

        public virtual void DeleteColumn(DcColumn column)
        {
            Debug.Assert(!column.Input.IsPrimitive, "Wrong use: top columns cannot be created/deleted.");

            ColumnDeleted(column);
            column.Remove();
        }

        public void RenameColumn(DcColumn column, string newName)
        {
            ColumnRenamed(column, newName); // Rename with propagation
            column.Name = newName;
        }

        #endregion

        protected void TableRenamed(DcTable table, string newName)
        {
            DcSchema schema = this;

            //
            // Check all elements of the schema that can store table name (tables, columns etc.)
            // Update their definition so that it uses the new name of the specified element
            //
            List<DcTable> tables = schema.AllSubTables;
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.Columns)
                {
                    if (col.Definition == null) continue;

                    if (col.Definition.FormulaExpr != null)
                    {
                        nodes = col.Definition.FormulaExpr.Find((DcTable)table);
                        nodes.ForEach(x => x.Name = newName);
                    }
                    if (col.Definition.WhereExpr != null)
                    {
                        nodes = col.Definition.WhereExpr.Find((DcTable)table);
                        nodes.ForEach(x => x.Name = newName);
                    }
                }

                if (tab.Definition == null) continue;

                // Update table definitions by finding the uses of the specified column
                if (tab.Definition.WhereExpr != null)
                {
                    nodes = tab.Definition.WhereExpr.Find((DcTable)table);
                    nodes.ForEach(x => x.Name = newName);
                }
                if (tab.Definition.OrderbyExpr != null)
                {
                    nodes = tab.Definition.OrderbyExpr.Find((DcTable)table);
                    nodes.ForEach(x => x.Name = newName);
                }
            }

            table.Name = newName;
        }

        protected void ColumnRenamed(DcColumn column, string newName)
        {
            DcSchema schema = this;

            //
            // Check all elements of the schema that can store column name (tables, columns etc.)
            // Update their definition so that it uses the new name of the specified element
            //
            List<DcTable> tables = schema.AllSubTables;
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.Columns)
                {
                    if (col.Definition == null) continue;

                    if (col.Definition.FormulaExpr != null)
                    {
                        nodes = col.Definition.FormulaExpr.Find((DcColumn)column);
                        nodes.ForEach(x => x.Name = newName);
                    }
                    if (col.Definition.WhereExpr != null)
                    {
                        nodes = col.Definition.WhereExpr.Find((DcColumn)column);
                        nodes.ForEach(x => x.Name = newName);
                    }
                }

                if (tab.Definition == null) continue;

                // Update table definitions by finding the uses of the specified column
                if (tab.Definition.WhereExpr != null)
                {
                    nodes = tab.Definition.WhereExpr.Find((DcColumn)column);
                    nodes.ForEach(x => x.Name = newName);
                }
                if (tab.Definition.OrderbyExpr != null)
                {
                    nodes = tab.Definition.OrderbyExpr.Find((DcColumn)column);
                    nodes.ForEach(x => x.Name = newName);
                }
            }

            column.Name = newName;
        }

        protected void ColumnDeleted(DcColumn column)
        {
            DcSchema schema = this;

            //
            // Delete all expression nodes that use the deleted column and all references to this column from other objects
            //
            List<DcTable> tables = schema.AllSubTables;
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.Columns)
                {
                    if (col.Definition == null) continue;

                    if (col.Definition.FormulaExpr != null)
                    {
                        nodes = col.Definition.FormulaExpr.Find(column);
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
                if (tab.Definition.WhereExpr != null)
                {
                    nodes = tab.Definition.WhereExpr.Find(column);
                    foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                }
                if (tab.Definition.OrderbyExpr != null)
                {
                    nodes = tab.Definition.OrderbyExpr.Find(column);
                    foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                }
            }
        }

        public DataSourceType DataSourceType { get; protected set; } // Where data is stored and processed (engine). Replace class name

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["DataSourceType"] = (int)DataSourceType;

            // List of all tables
            JArray tables = new JArray();
            JArray columns = new JArray(); // One array for all columns of all tables (not within each tabel)
            foreach (DcTable comTable in this.AllSubTables)
            {
                if (comTable.IsPrimitive) continue;

                JObject table = Utils.CreateJsonFromObject(comTable);
                comTable.ToJson(table);
                tables.Add(table);

                // List of columns
                foreach (DcColumn comColumn in comTable.Columns)
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
                DcTable comTable = (DcTable)Utils.CreateObjectFromJson(table);
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

        public Schema()
            : this("")
        {
        }

        public Schema(string name)
            : base(name)
        {
            CreateDataTypes(); // Generate all predefined primitive sets as subsets
        }

    }

    public enum DataSourceType // Essentially, it a marker for a subclass of Schema
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
