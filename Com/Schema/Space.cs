using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Schema.Csv;
using Com.Schema.Rel;

namespace Com.Schema
{
    /// <summary>
    /// Workspace is a number of schemas as well as parameters for their management. 
    /// </summary>
    public class Space : DcSpace
    {
        #region DcSpace (OLD)
        /*
        public ObservableCollection<DcSchema> Schemas { get; set; }

        public virtual void AddSchema(DcSchema schema)
        {
            if(!_schemas.Contains(schema))
            {
                _schemas.Add(schema);
            }
        }
        public void RemoveSchema(DcSchema schema)
        {
            // We have to ensure that inter-schema (import/export) columns are also deleted
            List<DcTable> allTables = schema.AllSubTables;
            foreach (DcTable t in allTables)
            {
                if (t.IsPrimitive) continue;
                schema.DeleteTable(t);
            }

            Schemas.Remove(schema);
        }

        protected DcSchema _mashup;
        public DcSchema Mashup 
        {
            get { return _mashup; }
            set { _mashup = value; }
        }
        */
        #endregion

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
                schema = new Schema(name);
            }
            else if (schemaType == DcSchemaKind.Csv)
            {
                schema = new SchemaCsv(name);
            }
            else if (schemaType == DcSchemaKind.Oledb)
            {
                schema = new SchemaOledb(name);
            }
            else if (schemaType == DcSchemaKind.Rel)
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            schema.Space = this;

            _schemas.Add(schema);

            return schema;
        }

        public virtual void DeleteSchema(DcSchema schema)
        {

        }
        public virtual List<DcSchema> GetSchemas()
        {
            return new List<DcSchema>(_schemas);
        }
        public DcSchema GetSchema(string name)
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

            if (schemaType == DcSchemaKind.Dc)
            {
                table = new Table(name);
            }
            else if (schemaType == DcSchemaKind.Csv)
            {
                table = new TableCsv(name);
            }
            else if (schemaType == DcSchemaKind.Oledb)
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }
            else if (schemaType == DcSchemaKind.Rel)
            {
                table = new TableRel(name);
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _tables.Add(table);

            return table;
        }

        public virtual void DeleteTable(DcTable table)
        {

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

        public virtual DcColumn CreateColumn(string name, DcTable input, DcTable output)
        {
            DcSchema inSchema = input.Schema;
            DcSchemaKind inSchemaType = inSchema.GetSchemaKind();

            DcColumn column;

            if (inSchemaType == DcSchemaKind.Dc)
            {
                column = new Column(name, input, output);
            }
            else if (inSchemaType == DcSchemaKind.Csv)
            {
                column = new ColumnCsv(name, input, output);
            }
            else if (inSchemaType == DcSchemaKind.Oledb)
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }
            else if (inSchemaType == DcSchemaKind.Rel)
            {
                column = new ColumnRel(name, input, output);
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _columns.Add(column);

            return column;
        }

        public virtual void DeleteColumn(DcColumn column)
        {

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
                    this._schemas.Add(sch);
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
                        col.Add();
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

        public Space()
        {
            _schemas = new List<DcSchema>();
            _tables = new List<DcTable>();
            _columns = new List<DcColumn>();
        }

    }

}
