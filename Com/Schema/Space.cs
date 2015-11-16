using System; 
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Schema
{
    /// <summary>
    /// Workspace is a number of schemas as well as parameters for their management. 
    /// </summary>
    public class Space : DcSpace
    {

        #region DcWorkspace

        public ObservableCollection<DcSchema> Schemas { get; set; }

        public void AddSchema(DcSchema schema)
        {
            Schemas.Add(schema);
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

        public DcSchema GetSchema(string name)
        {
            return Schemas.FirstOrDefault(x => StringSimilarity.SameColumnName(x.Name, name));
        }

        protected DcSchema _mashup;
        public DcSchema Mashup 
        {
            get { return _mashup; }
            set { _mashup = value; }
        }

        #endregion

        #region Json serialization

        public virtual void ToJson(JObject json)
        {
            if (Mashup != null) json["mashup"] = Utils.CreateJsonRef(Mashup);

            // List of schemas
            JArray schemas = new JArray();
            foreach (DcSchema comSchema in Schemas)
            {
                JObject schema = Utils.CreateJsonFromObject(comSchema);
                comSchema.ToJson(schema);
                schemas.Add(schema);
            }
            json["schemas"] = schemas;
        }

        public virtual void FromJson(JObject json, DcSpace ws)
        {
            // List of schemas
            foreach (JObject schema in json["schemas"])
            {
                DcSchema comSchema = (DcSchema)Utils.CreateObjectFromJson(schema);
                if (comSchema != null)
                {
                    comSchema.FromJson(schema, this);
                    this.Schemas.Add(comSchema);
                }
            }

            // Load all columns from all schema (now all tables are present)
            foreach (JObject schema in json["schemas"])
            {
                foreach (JObject column in schema["columns"]) // List of columns
                {
                    DcColumn comColumn = (DcColumn)Utils.CreateObjectFromJson(column);
                    if (comColumn != null)
                    {
                        comColumn.FromJson(column, this);
                        comColumn.Add();
                    }
                }
            }

            // Second pass on all columns with the purpose to load their definitions (now all columns are present)
            foreach (JObject schema in json["schemas"])
            {
                foreach (JObject column in schema["columns"]) // List of columns
                {
                    DcColumn comColumn = (DcColumn)Utils.CreateObjectFromJson(column);
                    if (comColumn != null)
                    {
                        comColumn.FromJson(column, this);

                        // Find the same existing column (possibly without a definition)
                        DcColumn existing = comColumn.Input.GetColumn(comColumn.Name);

                        // Copy the definition
                        existing.FromJson(column, this);
                    }
                }
            }

            Mashup = (DcSchema)Utils.ResolveJsonRef((JObject)json["mashup"], this);
        }

        #endregion

        public Space()
        {
            Schemas = new ObservableCollection<DcSchema>();
        }
    }

}
