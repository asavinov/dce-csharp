using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Data;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Schema
{
    /// <summary>
    /// Top set in a poset of all sets. It is a parent for all primitive sets.
    /// 
    /// Top set is used to represent a whole database like a local mash up or a remote database. 
    /// It also can describe how its instances are loaded from a remote source and stored.
    /// </summary>
    public class Schema : Table, DcSchema
    {

        #region DcSchema interface

        protected DcSchemaKind _schemaKind;
        public virtual DcSchemaKind GetSchemaKind() { return _schemaKind; }

        public DcSpace Space { get; set; }
        
        public DcTable GetPrimitive(string name)
        {
            DcColumn col = SubColumns.FirstOrDefault(x => StringSimilarity.SameTableName(x.Input.Name, name));
            return col != null ? col.Input : null;
        }

        public DcTable Root { get { return GetPrimitive("Root"); } }


        //
        // Factories for tables and columns
        //

        public virtual DcTable CreateTable(String name) 
        {
            DcTable table = new Table(name);
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

            Column col = new Column(superName, table, parent, true, true);

            col.Add();

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

            DcColumn col = new Column(name, input, output, isKey, false);

            return col;
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
                    if (col.GetData() == null) continue;
                    DcColumnData data = col.GetData();

                    if (data.FormulaExpr != null)
                    {
                        nodes = data.FormulaExpr.Find((DcTable)table);
                        nodes.ForEach(x => x.Name = newName);
                    }
                }

                // Update table definitions by finding the uses of the specified column
                if (tab.GetData().WhereExpr != null)
                {
                    nodes = tab.GetData().WhereExpr.Find((DcTable)table);
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
                    if (col.GetData() == null) continue;
                    DcColumnData data = col.GetData();

                    if (data.FormulaExpr != null)
                    {
                        nodes = data.FormulaExpr.Find((DcColumn)column);
                        nodes.ForEach(x => x.Name = newName);
                    }
                }

                // Update table definitions by finding the uses of the specified column
                if (tab.GetData().WhereExpr != null)
                {
                    nodes = tab.GetData().WhereExpr.Find((DcColumn)column);
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
                    if (col.GetData() == null) continue;
                    DcColumnData data = col.GetData();

                    if (data.FormulaExpr != null)
                    {
                        nodes = data.FormulaExpr.Find(column);
                        foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                    }

                }

                // Update table definitions by finding the uses of the specified column
                if (tab.GetData().WhereExpr != null)
                {
                    nodes = tab.GetData().WhereExpr.Find(column);
                    foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                }
            }
        }

        #region DcJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["SchemaKind"] = (int)_schemaKind;

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

        public override void FromJson(JObject json, DcSpace ws)
        {
            base.FromJson(json, ws); // Set

            _schemaKind = json["SchemaKind"] != null ? (DcSchemaKind)(int)json["SchemaKind"] : DcSchemaKind.Dc;

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
            Table tab;
            Column col;

            tab = new Table("Root");
            col = new Column("Top", tab, this, true, true);
            col.Add();

            tab = new Table("Integer");
            col = new Column("Top", tab, this, true, true);
            col.Add();

            tab = new Table("Double");
            col = new Column("Top", tab, this, true, true);
            col.Add();

            tab = new Table("Decimal");
            col = new Column("Top", tab, this, true, true);
            col.Add();

            tab = new Table("String");
            col = new Column("Top", tab, this, true, true);
            col.Add();

            tab = new Table("Boolean");
            col = new Column("Top", tab, this, true, true);
            col.Add();

            tab = new Table("DateTime");
            col = new Column("Top", tab, this, true, true);
            col.Add();
        }

        public Schema()
            : this("")
        {
        }

        public Schema(string name)
            : base(name)
        {
            _schemaKind = DcSchemaKind.Dc;

            CreateDataTypes(); // Generate all predefined primitive sets as subsets
        }

    }

}
