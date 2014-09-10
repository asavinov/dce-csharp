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

            if(Mashup != null) workspace.mashup =  Mashup.Name;

            // List of schemas
            workspace.schemas = new JArray() as dynamic;
            foreach (ComSchema comSchema in Schemas)
            {
                dynamic schema = Utils.CreateJsonFromObject(comSchema);
                ((SetTop)comSchema).ToJson(schema);
                workspace.schemas.Add(schema);
            }
        }

        public virtual void FromJson(JObject json, Workspace ws)
        {
            dynamic workspace = json;

            Mashup = this.GetSchema(workspace.mashup); // Resolve

            // List of schemas
            foreach (dynamic schema in ((dynamic)workspace).schemas)
            {
                ComSchema comSchema = Utils.CreateObjectFromJson(schema);
                if (comSchema != null)
                {
                    ((SetTop)comSchema).FromJson(schema, this);
                    this.Schemas.Add(comSchema);
                }
            }

            // List of columns
            // Load them manually, not as part of schema method
            foreach (dynamic schema in ((dynamic)workspace).schemas)
            {
                // List of columns
                foreach (dynamic column in schema.columns)
                {
                    ComColumn comColumn = Utils.CreateObjectFromJson(column);
                    if (comColumn != null)
                    {
                        ((Dim)comColumn).FromJson(column, this);
                        comColumn.Add();
                    }
                }
            }


            // Issues:
            // ToJson:
            // - we want to automatically deal with extended objects
            // - only extension adds its 'type' field, so a ToJson method has to know whether it is the last one
            // - either an instance of JObject is produced with ToJson or it is provided as a parameter
            // Solution:
            // - JObject CreateJson() creates a JObject with a type field from this object (implemented once)
            // - virtual InitJson(JObject) calls the super-method and then writes all extension (this) fields to the same json object passed as parameter

            // FromJson:
            // - either it is an initializer (so an instance created outside), or it is a factory (and it creates an instance)
            // - to create an instance, we must know the type which is read from Json string
            // - json string can contain extended and base fields which should be processed (initialized) by different methods in the hierarchy
            // Solution: 
            // - static CreateFromJson(json) is implemented once as a switch on all types either for all possible classes or for each branch in the hierarchy
            // - virtual InitFromJson(string, Workspace) calls first its super-method and then initializes its own extended fields (with possible resolution) from the same json string
            


            // Notes:
            // It is important that next objects might depend on the existence of the previous objects (or we need to introduce new name-references). For example, import column assumes that two tables in different schemas already exist.
            // An inter-column is stored in both schemas or only once? How do we distinguish the mash-up schema from external schemas (role)?
            // REQUEIREMENT to all serialized classes:
            // We need an empty constructor for all classes, and possibility to change their properties individually. 
            // Each serialized class should have an initiation procedure which sets its (empty) properties from dynamic object.

            
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

        #endregion

        public Workspace()
        {
            Schemas = new List<ComSchema>();
        }
    }

    // TODO: Add to all necessary classes. Remove casting To/FromJson methods
    public interface ComJson
    {
        void ToJson(JObject json);
        void FromJson(JObject json, Workspace ws);
    }

}
