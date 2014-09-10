using System; 
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Model
{
    /// <summary>
    /// Workspace is a number of schemas as well as parameters for their management. 
    /// </summary>
    public class Workspace : ComJson
    {
        public List<ComSchema> Schemas { get; set; }
        public ComSchema GetSchema(string name)
        {
            return Schemas.FirstOrDefault(x => StringSimilarity.SameColumnName(x.Name, name));
        }

        public ComSchema Mashup { get; set; }

        #region Json serialization

        public virtual void ToJson(JObject json)
        {
            dynamic workspace = json;

            if (Mashup != null) workspace.mashup = Utils.CreateJsonRef(Mashup);

            // List of schemas
            workspace.schemas = new JArray() as dynamic;
            foreach (ComSchema comSchema in Schemas)
            {
                JObject schema = Utils.CreateJsonFromObject(comSchema);
                comSchema.ToJson(schema);
                workspace.schemas.Add(schema);
            }
        }

        public virtual void FromJson(JObject json, Workspace ws)
        {
            dynamic workspace = json;

            // List of schemas
            foreach (JObject schema in ((dynamic)workspace).schemas)
            {
                ComSchema comSchema = (ComSchema)Utils.CreateObjectFromJson(schema);
                if (comSchema != null)
                {
                    comSchema.FromJson(schema, this);
                    this.Schemas.Add(comSchema);
                }
            }

            // List of columns
            // Load them manually, not as part of schema method
            foreach (JObject schema in ((dynamic)workspace).schemas)
            {
                // List of columns
                foreach (JObject column in schema["columns"])
                {
                    ComColumn comColumn = (ComColumn)Utils.CreateObjectFromJson(column);
                    if (comColumn != null)
                    {
                        comColumn.FromJson(column, this);
                        comColumn.Add();
                    }
                }
            }

            Mashup = Utils.ResolveJsonRef(workspace.mashup, this);
        }

        #endregion

        public Workspace()
        {
            Schemas = new List<ComSchema>();
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
