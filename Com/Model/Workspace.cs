using System; 
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Model
{
    /// <summary>
    /// Workspace is a number of schemas as well as parameters for their management. 
    /// </summary>
    public class Workspace : ComJson
    {
        public ObservableCollection<ComSchema> Schemas { get; set; }
        public ComSchema GetSchema(string name)
        {
            return Schemas.FirstOrDefault(x => StringSimilarity.SameColumnName(x.Name, name));
        }

        public ComSchema Mashup { get; set; }

        #region Json serialization

        public virtual void ToJson(JObject json)
        {
            if (Mashup != null) json["mashup"] = Utils.CreateJsonRef(Mashup);

            // List of schemas
            JArray schemas = new JArray();
            foreach (ComSchema comSchema in Schemas)
            {
                JObject schema = Utils.CreateJsonFromObject(comSchema);
                comSchema.ToJson(schema);
                schemas.Add(schema);
            }
            json["schemas"] = schemas;
        }

        public virtual void FromJson(JObject json, Workspace ws)
        {
            // List of schemas
            foreach (JObject schema in json["schemas"])
            {
                ComSchema comSchema = (ComSchema)Utils.CreateObjectFromJson(schema);
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
                    ComColumn comColumn = (ComColumn)Utils.CreateObjectFromJson(column);
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
                    ComColumn comColumn = (ComColumn)Utils.CreateObjectFromJson(column);
                    if (comColumn != null)
                    {
                        comColumn.FromJson(column, this);

                        // Find the same existing column (possibly without a definition)
                        ComColumn existing = comColumn.Input.GetColumn(comColumn.Name);

                        // Copy the definition
                        existing.FromJson(column, this);
                    }
                }
            }

            Mashup = (ComSchema)Utils.ResolveJsonRef((JObject)json["mashup"], this);
        }

        #endregion

        public Workspace()
        {
            Schemas = new ObservableCollection<ComSchema>();
        }
    }

    /// <summary>
    /// Serialize and de-serialize.
    /// Notes:
    /// - We do not work with JSON strings. Instead, we work with JSON object representation (a tree).
    /// - Instantiation of JSON-object or com-object is performed separately by using special type field. Empy constructor is requried for all classes.
    /// - Initialization of instantiated objects is performed by separate initialization methods of this interface.
    /// - Serialization (initialization) methods are virtual methods and every extension can attach the fields of the extended class.
    /// - Since we may have inter-schema (import) columns, the columns have to be processed after all tables in all schemas.
    /// - Some fields store references to com-objects and they have to be resolved during de-serialization. Therefore, the corresponding referenced objects have to be created before.
    /// </summary>
    public interface ComJson
    {
        void ToJson(JObject json);
        void FromJson(JObject json, Workspace ws);

        //
        // Json.Net usage
        //

        //string name = obj.aaa.name; // The value is of JToken type
        //string firstDrive = (string)o["Drives"][0]; // indexers can be used for both fields and arrays

        //IList<string> list = (obj.aaa).Where(t => t.bbb == "bbb").ToList();
        //IList<string> propertyNames = TheJObject.Properties().Select(p => p.Name).ToList();

        /*
        JArray ar = obj.aaa.ddd;
        Func<JToken, string> f = x => ((JToken)x).SelectToken("a").ToString();
        IList<string> list = ar.Select(f).ToList();
        */

        /*
        foreach (var x in o)
        {
            string n = x.Key;
            JToken value = x.Value;//jarray
            var jt = JToken.Parse(value.ToString());
            //List<Video> vv = jt.ToObject<List<Video>>();
        }
        */
    }

}
