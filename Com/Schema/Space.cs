using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;
using System.Collections.Specialized;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Schema.Csv;
using Com.Schema.Rel;
using Com.Data;

namespace Com.Schema
{
    /// <summary>
    /// Workspace is a number of schemas as well as parameters for their management. 
    /// </summary>
    public class Space : DcSpace, INotifyPropertyChanged, INotifyCollectionChanged
    {
        #region DcSpace

        //
        // Schemas
        //

        protected List<DcSchema> _schemas;

        public virtual DcSchema CreateSchema(string name, DcSchemaKind schemaType)
        {
            DcSchema schema;

            if (schemaType == DcSchemaKind.Dc)
            {
                schema = new Schema(name, this);
            }
            else if (schemaType == DcSchemaKind.Csv)
            {
                schema = new SchemaCsv(name, this);
            }
            else if (schemaType == DcSchemaKind.Oledb)
            {
                schema = new SchemaOledb(name, this);
            }
            else if (schemaType == DcSchemaKind.Rel)
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _schemas.Add(schema);

            NotifyAdd(schema);

            return schema;
        }

        public virtual void DeleteSchema(DcSchema schema)
        {
            // We have to ensure that inter-schema (import/export) columns are also deleted
            List<DcTable> allTables = this.GetTables(schema); // schema.AllSubTables;
            foreach (DcTable t in allTables)
            {
                if (t.IsPrimitive) continue;
                this.DeleteTable(t);
            }

            _schemas.Remove(schema);

            NotifyRemove(schema);
        }
        public virtual List<DcSchema> GetSchemas()
        {
            return new List<DcSchema>(_schemas);
        }
        public virtual DcSchema GetSchema(string name)
        {
            return _schemas.FirstOrDefault(x => StringSimilarity.SameTableName(x.Name, name));
        }

        //
        // Tables
        //

        protected List<DcTable> _tables;

        public virtual DcTable CreateTable(string name, DcTable parent)
        {
            DcSchema schema = parent.Schema;
            DcSchemaKind schemaType = schema.GetSchemaKind();

            DcTable table;
            Column column;
            string colName;
            if (parent is DcSchema)
            {
                colName = "Top";
            }
            else
            {
                colName = "Super";
            }

            if (schemaType == DcSchemaKind.Dc)
            {
                table = new Table(name, this);
                column = new Column(colName, table, parent, true, true);
            }
            else if (schemaType == DcSchemaKind.Csv)
            {
                table = new TableCsv(name, this);
                column = new ColumnCsv(colName, table, parent, true, true);
            }
            else if (schemaType == DcSchemaKind.Oledb)
            {
                table = new TableRel(name, this);
                column = new ColumnRel(colName, table, parent, true, true);
            }
            else if (schemaType == DcSchemaKind.Rel)
            {
                table = new TableRel(name, this);
                column = new ColumnRel(colName, table, parent, true, true);
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _tables.Add(table);
            NotifyAdd(table);

            _columns.Add(column);
            NotifyAdd(column);

            return table;
        }

        public virtual void DeleteTable(DcTable table)
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: primitive tables can be deleted only along with the schema.");

            List<DcColumn> toRemove;
            toRemove = table.InputColumns.ToList();
            foreach (DcColumn col in toRemove)
            {
                this.DeleteColumn(col);
                NotifyRemove(col);
            }
            toRemove = table.Columns.ToList();
            foreach (DcColumn col in toRemove)
            {
                this.DeleteColumn(col);
                NotifyRemove(col);
            }

            _tables.Remove(table);
            NotifyRemove(table);
        }
        public virtual List<DcTable> GetTables(DcSchema schema)
        {
            if(schema == null)
            {
                return new List<DcTable>(_tables);
            }
            else
            {
                return _tables.Where(x => x.Schema == schema).ToList();
            }
        }

        //
        // Columns
        //

        protected List<DcColumn> _columns;

        public virtual DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: column name cannot be null or empty.");
            // TODO: Check constraints: 1. only one super-column can exist 2. no loops can appear

            DcSchema inSchema = input.Schema;
            DcSchemaKind inSchemaType = inSchema.GetSchemaKind();

            DcColumn column;

            if (inSchemaType == DcSchemaKind.Dc)
            {
                column = new Column(name, input, output, isKey, false);
            }
            else if (inSchemaType == DcSchemaKind.Csv)
            {
                column = new ColumnCsv(name, input, output, isKey, false);
            }
            else if (inSchemaType == DcSchemaKind.Oledb)
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }
            else if (inSchemaType == DcSchemaKind.Rel)
            {
                column = new ColumnRel(name, input, output, isKey, false);
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _columns.Add(column);

            NotifyAdd(column);

            return column;
        }

        /* TODO: Notify after adding
        public virtual void Add()
        {
            if (IsSuper) // Only one super-dim per table can exist
            {
                if (Input != null && Input.SuperColumn != null)
                {
                    Input.SuperColumn.Remove(); // Replace the existing column by the new one
                }
            }

            if (Output != null) Output.InputColumns.Add(this);
            if (Input != null) Input.Columns.Add(this);

            // Notify that a new child has been added
            if (Input != null) ((Table)Input).NotifyAdd(this);
            if (Output != null) ((Table)Output).NotifyAdd(this);
        }
        */
        public virtual void DeleteColumn(DcColumn column)
        {
            Debug.Assert(!column.Input.IsPrimitive, "Wrong use: top columns cannot be created/deleted.");
            // TODO: Check constraints: deleting a super-column means deleting the corresponding table (with all its columns)

            ColumnDeleted(column);

            _columns.Remove(column);

            NotifyRemove(column);
        }
        /* TODO: Notify after deleting
        public virtual void Remove()
        {
            if (Output != null) Output.InputColumns.Remove(this);
            if (Input != null) Input.Columns.Remove(this);

            // Notify that a new child has been removed
            if (Input != null) ((Table)Input).NotifyRemove(this);
            if (Output != null) ((Table)Output).NotifyRemove(this);
        }
        */
        protected void ColumnDeleted(DcColumn column)
        {
            DcSchema schema = column.Input.Schema;

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
        public virtual List<DcColumn> GetColumns(DcTable table)
        {
            if (table == null)
            {
                return new List<DcColumn>(_columns);
            }
            else
            {
                return _columns.Where(x => x.Input == table).ToList();
            }
        }
        public virtual List<DcColumn> GetInputColumns(DcTable table)
        {
            if (table == null)
            {
                return new List<DcColumn>(_columns);
            }
            else
            {
                return _columns.Where(x => x.Output == table).ToList();
            }
        }

        #endregion

        #region Json serialization

        public virtual void ToJson(JObject json)
        {
            // List of schemas
            JArray schemas = new JArray();
            foreach (DcSchema sch in GetSchemas())
            {
                JObject schema = Utils.CreateJsonFromObject(sch);
                sch.ToJson(schema);
                schemas.Add(schema);
            }
            json["schemas"] = schemas;

            // List of tables
            JArray tables = new JArray();
            foreach (DcTable comTable in _tables)
            {
                if (comTable.IsPrimitive) continue;

                JObject table = Utils.CreateJsonFromObject(comTable);
                comTable.ToJson(table);
                tables.Add(table);
            }
            json["tables"] = tables;


            // List of tables
            JArray columns = new JArray();
            foreach (DcColumn comColumn in _columns)
            {
                JObject column = Utils.CreateJsonFromObject(comColumn);
                comColumn.ToJson(column);
                columns.Add(column);
            }
            json["columns"] = columns;
        }

        public virtual void FromJson(JObject json, DcSpace ws)
        {
            // List of schemas
            foreach (JObject schema in json["schemas"])
            {
                DcSchema sch = (DcSchema)Utils.CreateObjectFromJson(schema);
                if (sch != null)
                {
                    sch.FromJson(schema, this);
                    _schemas.Add(sch);
                }
            }

            // List of tables
            foreach (JObject table in json["tables"])
            {
                DcTable tab = (DcTable)Utils.CreateObjectFromJson(table);
                if (tab != null)
                {
                    tab.FromJson(table, this);
                    _tables.Add(tab);
                }
            }

            // Load all columns from all schema (now all tables are present)
            foreach (JObject schema in json["schemas"])
            {
                foreach (JObject column in schema["columns"]) // List of columns
                {
                    DcColumn col = (DcColumn)Utils.CreateObjectFromJson(column);
                    if (col != null)
                    {
                        col.FromJson(column, this);
                        _columns.Add(col);
                    }
                }
            }

            // Second pass on all columns with the purpose to load their definitions (now all columns are present)
            foreach (JObject schema in json["schemas"])
            {
                foreach (JObject column in schema["columns"]) // List of columns
                {
                    DcColumn col = (DcColumn)Utils.CreateObjectFromJson(column);
                    if (col != null)
                    {
                        col.FromJson(column, this);

                        // Find the same existing column (possibly without a definition)
                        DcColumn existing = col.Input.GetColumn(col.Name);

                        // Copy the definition
                        existing.FromJson(column, this);
                    }
                }
            }

        }

        #endregion

        #region System interfaces

        public event NotifyCollectionChangedEventHandler CollectionChanged; // Operations with collections (schemas, tables, columns)
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, e);
            }
        }
        public virtual void NotifyAdd(object elem) // Convenience method: notifying about adding
        {
            if (elem == null) return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, elem));
        }
        public virtual void NotifyRemove(object elem) // Convenience method: notifying about removing
        {
            if (elem == null) return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, elem));
        }

        //
        // INotifyPropertyChanged Members
        //
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public virtual void NotifyPropertyChanged(String propertyName = "") // Convenience method: notifying all about property change
        {
            OnPropertyChanged(propertyName);
        }

        #endregion

        public Space()
        {
            _schemas = new List<DcSchema>();
            _tables = new List<DcTable>();
            _columns = new List<DcColumn>();
        }

    }

}
