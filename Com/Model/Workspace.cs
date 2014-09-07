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
    public class Workspace
    {
        public List<ComSchema> Schemas { get; set; }
        public ComSchema GetSchema(string name)
        {
            return Schemas.FirstOrDefault(x => StringSimilarity.SameColumnName(x.Name, name));
        }

        public ComSchema Mashup { get; set; }

        public JObject ToJson()
        {
            dynamic workspace = new JObject();

            if(Mashup != null) workspace.mashup =  Mashup.Name;

            //
            // Schemas
            //
            workspace.schemas = new JArray() as dynamic;
            foreach (ComSchema comSchema in Schemas)
            {
                dynamic schema = new JObject();

                schema.name = comSchema.Name;

                //
                // Tables
                //
                schema.tables = new JArray() as dynamic;
                schema.columns = new JArray() as dynamic;
                foreach (ComTable comTable in comSchema.GetAllSubsets())
                {
                    if (comTable.IsPrimitive) continue;
                    dynamic table = new JObject();

                    table.name = comTable.Name;
                    table.type = comTable.GetType().Name;

                    // Table definition
                    if (comTable.Definition != null)
                    {
                        dynamic tableDef = new JObject();

                        tableDef.definition_type = comTable.Definition.DefinitionType;
                        // TODO: Store formulas

                        table.definition = tableDef;
                    }

                    //
                    // Columns
                    //
                    foreach (ComColumn comColumn in comTable.GreaterDims)
                    {
                        dynamic column = new JObject();

                        column.name = comColumn.Name;
                        column.type = comColumn.GetType().Name;
                        column.key = comColumn.IsIdentity ? "true" : "false";
                        column.super = comColumn.IsSuper ? "true" : "false";

                        column.lesser_set = comColumn.LesserSet.Name;
                        column.lesser_schema = comColumn.LesserSet.Top.Name;

                        column.greater_set = comColumn.GreaterSet.Name;
                        column.greater_schema = comColumn.GreaterSet.Top.Name;

                        // Column definition
                        if (comColumn.Definition != null)
                        {
                            dynamic columnDef = new JObject();

                            columnDef.definition_type = comColumn.Definition.DefinitionType;
                            columnDef.generating = comColumn.Definition.IsGenerating ? "true" : "false";

                            // TODO: Store formulas

                            column.definition = columnDef;
                        }

                        schema.columns.Add(column);
                    }

                    schema.tables.Add(table);
                }

                workspace.schemas.Add(schema);
            }

            return workspace;
        }

        public void FromJson(JObject workspace)
        {
            //
            // Schemas and their tables
            //
            foreach (dynamic schema in ((dynamic)workspace).schemas)
            {
                string schemaName = schema.name;
                string schemaType = schema.type != null ? schema.type : "SetTop";

                ComSchema comSchema = null;
                if (schemaType == "SetTop")
                {
                    comSchema = new SetTop(schemaName);
                }
                else if (schemaType == "SetTopCsv")
                {
                    comSchema = new SetTopCsv(schemaName);
                }
                else
                {
                    throw new NotImplementedException("Unknown schema type");
                }
                this.Schemas.Add(comSchema);

                //
                // Tables
                //
                foreach (dynamic table in schema.tables)
                {
                    string tableName = table.name;
                    string tableType = table.type != null ? table.type : "Set";
                    // TODO: definition

                    ComTable comTable = null;
                    if (tableType == "Set")
                    {
                        comTable = new Set(tableName);
                    }
                    else if (tableType == "SetRel") 
                    {
                        comTable = new SetRel(tableName);
                    }
                    else
                    {
                        throw new NotImplementedException("Unknown table type");
                    }
                    comSchema.AddTable(comTable, null, null);

                    //
                    // Table definition
                    //
                    dynamic tableDef = table.definition;
                    if (tableDef != null)
                    {
                        comTable.Definition.DefinitionType = tableDef.definition_type;

                        // TODO: Restore formulas
                    }
                }
            }

            //
            // Columns
            //
            foreach (dynamic schema in ((dynamic)workspace).schemas)
            {
                string schemaName = schema.name;

                foreach (dynamic column in schema.columns)
                {
                    string columnName = column.name;
                    string columnType = column.type != null ? column.type : "Dim";
                    bool isKey = column.key != null ? StringSimilarity.JsonTrue(column.key) : false;
                    bool isSuper = column.super != null ? StringSimilarity.JsonTrue(column.super) : false;

                    string lesserSetName = column.lesser_set;
                    string lesserSchemaName = column.lesser_schema != null ? column.lesser_schema : schemaName;

                    string greaterSetName = column.greater_set;
                    string greaterSchemaName = column.greater_schema != null ? column.greater_schema : schemaName;

                    // Resolve
                    ComSchema lesserSchema = this.GetSchema(lesserSchemaName);
                    ComTable lesserTable = lesserSchema.FindTable(lesserSetName);

                    ComSchema greaterSchema = this.GetSchema(greaterSchemaName);
                    ComTable greaterTable = greaterSchema.FindTable(greaterSetName);

                    ComColumn comColumn; 
                    if (columnType == "Dim")
                    {
                        comColumn = new Dim(columnName, lesserTable, greaterTable, isKey, isSuper);
                    }
                    else if (columnType == "DimRel") 
                    {
                        comColumn = new DimRel(columnName, lesserTable, greaterTable, isKey, isSuper);
                    }
                    else
                    {
                        throw new NotImplementedException("Unknown column type");
                    }

                    comColumn.Add();

                    //
                    // Column definition
                    //
                    dynamic columnDef = column.definition;
                    if (columnDef != null)
                    {
                        comColumn.Definition.DefinitionType = columnDef.definition_type;
                        comColumn.Definition.IsGenerating = columnDef.generating != null ? StringSimilarity.JsonTrue(columnDef.generating) : false;

                        // TODO: Restore formulas
                    }
                }
            }

            string mashupName = ((dynamic)workspace).mashup;
            if (mashupName != null)
            {
                Mashup = this.GetSchema(mashupName);
            }

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

        public Workspace()
        {
            Schemas = new List<ComSchema>();
        }
    }

}
