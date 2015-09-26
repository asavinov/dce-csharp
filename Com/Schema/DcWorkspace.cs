using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Schema
{
    public interface DcWorkspace : DcJson
    {
        ObservableCollection<DcSchema> Schemas { get; set; }

        void AddSchema(DcSchema schema);
        void RemoveSchema(DcSchema schema);

        DcSchema GetSchema(string name);

        DcSchema Mashup { get; set; }
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
    public interface DcJson
    {
        void ToJson(JObject json);
        void FromJson(JObject json, DcWorkspace ws);

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
