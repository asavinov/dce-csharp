using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Com.Schema;

namespace Test
{
    [TestClass]
    public class JsonTest
    {
        private static TestContext context;

        #region Initialisation and cleanup

        [ClassInitialize()]
        public static void SetUpClass(TestContext testContext)
        {
            context = testContext;
        }

        DcSpace space { get; set; }
        DcSchema schema { get; set; }

        [TestInitialize()]
        public void SetUp()
        {
            space = new Space();
            schema = space.CreateSchema("My Schema", DcSchemaKind.Dc);
            CoreTest.CreateSampleSchema(schema);
        }

        #endregion

        [TestMethod]
        public void JsonReadTest() // Serialize/deserialize schema elements
        {
            DcSpace ds = new Space();
            DcSchema schema = ds.CreateSchema("My Schema", DcSchemaKind.Dc);
            CoreTest.CreateSampleSchema(schema);

            // Add table definition 
            DcTable t = schema.GetSubTable("Table 2");
            t.GetData().WhereFormula = "[Column 22] > 20.0 && this.Super.[Column 23] < 50";

            // Add column definition 
            DcColumn c = t.GetColumn("Column 22");
            c.GetData().Formula = "([Column 11]+10.0) * this.[Column 13]";

            JObject space = Utils.CreateJsonFromObject(ds);
            ds.ToJson(space);

            // Serialize into json string
            string jsonDs = JsonConvert.SerializeObject(space, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonSerializerSettings { });

            // De-serialize from json string: http://weblog.west-wind.com/posts/2012/Aug/30/Using-JSONNET-for-dynamic-JSON-parsing
            dynamic objDs = JsonConvert.DeserializeObject(jsonDs);
            //dynamic obj = JObject/JValue/JArray.Parse(json);

            //
            // Instantiate and initialize
            //
            ds = (Space)Utils.CreateObjectFromJson(objDs);
            ((Space)ds).FromJson(objDs, ds);

            Assert.AreEqual(5, ds.GetSchemas()[0].GetSubTable("Table 1").Columns.Count);
            Assert.AreEqual(5, ds.GetSchemas()[0].GetSubTable("Table 2").Columns.Count);

            Assert.AreEqual("Table 1", ds.GetSchemas()[0].GetSubTable("Table 2").GetColumn("Table 1").Output.Name);

            c = t.GetColumn("Column 22");
            //Assert.AreEqual(DcColumnDefinitionType.ARITHMETIC, c.Definition.FormulaExpr.DefinitionType);
            //Assert.AreEqual(2, c.GetData().FormulaExpr.Children.Count);

            //
            // 2. Another sample schema with several schemas and inter-schema columns
            //
            string jsonDs2 = @"{ 
'type': 'Space', 
'schemas': [ 

{ 
'type': 'Schema', 
'name': 'My Schema', 
'tables': [
  { 'type': 'Set', 'name': 'My Table' }
], 
'columns': [
  { 'type': 'Dim', 'name': 'My Column', 'lesser_table': {schema_name:'My Schema', table_name:'My Table'}, 'greater_table': {schema_name:'My Schema', table_name:'Double'} }, 
  { 'type': 'Dim', 'name': 'Import Column', 'lesser_table': {schema_name: 'Rel Schema', table_name:'Rel Table'}, 'greater_table': {schema_name: 'My Schema', table_name: 'My Table'} }
] 
}, 

{ 
'type': 'SchemaCsv', 
'name': 'Rel Schema', 
'tables': [
  { 'type': 'SetRel', 'name': 'Rel Table' }
], 
'columns': [
  { 'type': 'DimRel', 'name': 'My Column', 'lesser_table': {schema_name:'Rel Schema', table_name:'Rel Table'}, 'greater_table': {schema_name:'Rel Schema', table_name:'String'} }, 
] 
} 

] 
}";
            /*
            dynamic objWs2 = JsonConvert.DeserializeObject(jsonWs2);

            Workspace ws2 = Utils.CreateObjectFromJson(objWs2);
            ws2.FromJson(objWs2, ws2);

            Assert.AreEqual(2, ws2.Schemas.Count);
            Assert.AreEqual("My Schema", ws2.Mashup.Name);
            Assert.AreEqual("My Table", ws2.Schemas[1].FindTable("Rel Table").GetGreaterDim("Import Column").Output.Name);
            */
        }

    }
}
