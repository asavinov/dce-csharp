using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Rowid = System.Int32;

namespace Com.Model
{

    /// <summary>
    /// This classe implements high-level API for accessing schemas, tables, columns and other elements.
    /// These elements can be then manipulated using their own interface. 
    /// It implements an environment for managing schema and data elements.
    /// </summary>
    public class DataCommandr 
    {
        private List<DcScriptSchema> _schemas { get; set; }

        //
        // Schema factory.
        //
        public DcScriptSchema CreateSchema(string json)
        {
            // Parameters: name, ..., local or remote or remote type (db-type), remote connection (sub-object)
            // options of the methods: add_immediately=true (otherwise it will hang and need to be added using a super-dim or Add method of the table)

            DcScriptSchema schema = new DcScriptSchema();
            _schemas.Add(schema);
            return schema;
        }

        //
        // Configuration of the environment itself.
        //

        public DataCommandr() // We need a constructor accepting configuration parameters
        {
            _schemas = new List<DcScriptSchema>();
        }
    }

    public class DcScriptSchema
    {
        protected DcSchema _schema { get; set; } // The represented object
        // TODO: some important properties of the object stored by-value in this smart reference
        // TODO: status of the reference: is_resolved, is_broken, etc.

        //
        // Remote methods
        //
        public DcScriptTable ImportSchema(string tables, string parameters)
        {
            // tables is a list of concrete table objects (as json objects), it also might be a name pattern but it should be another object (name pattern or name constraint) or in parameters
            // Parameters: import_fks=yes (otherwise fks are ignored), expand_fks_in_tables (referenced tables will be also recursively imported if not found in the schema), all_primitive=yes (import also entity atts of referenced tables)

            return null;
        }

        //
        // Population
        //
        public void Populate(string parameters)
        {
            // Parameters: dependencies, ...
        }

        //
        // Table factory
        //
        public DcScriptTable CreateTable(string json)
        {
            // Parameters: name, ..., definition, 
            // options of the methods: add_immediately=true (otherwise it will hang and need to be added using a super-dim or Add method of the table)
            return null;
        }

        //
        // Column factory
        //
        public DcScriptColumn CreateColumn(string json, DcScriptTable input, DcScriptTable output)
        {
            // Parameters: name, ..., table/type, definition, 
            // options of the methods: add_immediately=true (otherwise we will have to call Add method)
            return null;
        }

    }

    public class DcScriptColumn
    {
        protected DcColumn _column { get; set; } // The represented object
        // TODO: some important properties of the object stored by-value in this smart reference
        // TODO: status of the reference: is_resolved, is_broken, etc.

        //
        // Properties: name, isPrimitive, isKey, isSuper etc. path relative to this object
        //
        public void GetProperty(string property)
        {
        }
        public void SetProperty(string property, string json)
        {
        }
        public void AddProperty(string property, string json)
        {
        }
        public void RemoveProperty(string property, string json)
        {
        }

        //
        // Convenience property direct access
        //
        public string Name { get; set; } 

        public DcScriptTable Input { get; set; }
        public DcScriptTable Output { get; set; }

        //
        // Actions
        //
        public void Populate(string parameters)
        {
            // Parameters: dependencies, ...
        }
    }

    public class DcScriptTable
    {
        protected DcTable _table { get; set; } // The represented object
        // TODO: some important properties of the object stored by-value in this smart reference
        // TODO: status of the reference: is_resolved, is_broken, etc.

        //
        // Properties: name, isPrimitive, etc. path relative to this object
        //
        public void GetProperty(string property)
        {
            // System.Runtime.Serialization.Json.DataContractJsonSerializer - .net 4.5
            // System.Web.Script.Serialization.JavaScriptSerializer - .net 4.5
            //var serializer = new JavaScriptSerializer();
            //var result = serializer.Deserialize<dynamic>(json);
            //foreach (var item in result)
            //{
            //    Console.WriteLine(item["body"]["message"]);
            //}


            // JSON.NET: http://json.codeplex.com/
            // Package name: Newtonsoft.Json
            // dynamic obj = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString);
        }
        public void SetProperty(string property, string json)
        {
        }
        public void AddProperty(string property, string json)
        {
        }
        public void RemoveProperty(string property, string json)
        {
        }

        //
        // Convenience property direct access
        //
        public string Name { get; set; }

        //
        // Actions
        //
        public void Populate(string parameters)
        {
            // Parameters: dependencies, ...
        }
    }

    // Maybe introduce two separate interfaces:
    // - Core API interfaces: internal interface, prefixed by Dn - we use it so do not change
    // - JSON-based interface for scripting, prefixed by Dns (DnScript)
    //   - text parameters, excepting main objects providing their own API
    //   - immutable proxy objects (smart references) for de-coupling
    // - proxy interfaces and objects prefixed by Ref 
    //   - we need proxies for serialization, for missing/unresolved/broken status of the reference, 

    // Choose the format of parameter passing: JSON, key-value (n1.n2.n3=value), XML, etc.
    // Maybe first line of parameter string specifies the format.
    // Find JSON parser for C#
    // Define first parameters, say, for ImportSchema method
    // { tables = { [name="first table", expand_references = yes], [name="second table", expand_references = no] }, expand_references = yes }

    // Format for specifying schema elements: 
    // [ schema_name="", table_name="", column_name="", type_name=""]
    // Column parameters: is_generating, is_key, is_super, ...
    // Table parameters: 

    // JSON objects:
    // schema: kind (class), list of columns, (do we need list of tables? do we need primitive types)
    // table: schema, name, definition, 
    // column: name, table, type, definition, 
    // connection:
    // definition/formula: (used in table and in column, so either different names or determine it from context)
    // - fields: kind (more specific constraint), what represents (table, column, where, order_by etc.)
    // coel: (simply arbitrary expression in coel without any other information - it can be parsed, normally part of a definition)
    // - we encode all operation in syntactic form as coel. so no special objects for composition, arithmetics, aggregation etc.

    // General notes:
    // - many parameters have a value 'auto' which can mean: use default, inherit, derive from other params, 
    // - many object have a field kind/class/type which is interpreted as a more specific class of the object

    // What properties do I need for the first version:
    // - names for objects (mostly, when creating objects)
    // - coel expression as a formula. it is enough for all, if we can encode aggregation function (AGG node), case expressions, tuples (instead of mapping)
    //  - ? do we need a special property/representation for mapping?
    //  - ? do we need a special property/representation for aggregation?
    //  - ? do we need a special property/representation for case?
    //  - ? do we need a special property/representation for evaluator?
    // - 

    // What convenience methods we need: 
    // - Recommending mappings is not very important for scripting, if we can explicitly set types for schema population proceudre (for importing column structure from another schema). 
    //   - More specifically, we need to be able to define types for tuple members returned by a function (in an expression), say, (( (type_name) [column name]=<expr> ))
    //     Here conceptual difficulty is that this casting operator has two interpretations: convert operator (used for data population), and indication/declaration of the type (used for the structure population)
    //     SOLUTION: use this for both purposes: data population (casting), and schema population (creation desired column types)
    //     If type info is missing then it is derived from the expression as usual
    //   - Defining default and explicit type/column mappings that can be used for schema/data population operations and type inference. 
    //     - column mapping is used if we want to assign specific type mappings for concrete columns
    //     - type mapping is used if we want to map tables
    //     PROBLEM: we do not have a generic theoretical framework for using such mappings

}
