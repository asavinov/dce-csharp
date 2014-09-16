using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Antlr4.Runtime;
//using Antlr4.Runtime.Atn;
//using Antlr4.Runtime.Dfa;
//using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
//using DFA = Antlr4.Runtime.Dfa.DFA;

using Com.Model;
using Com.Query;

using Offset = System.Int32;

// Unit test: http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class CoreTest
    {
        public static string Northwind = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test\\Northwind.accdb";
        public static string TextDbConnection = "Provider=Microsoft.ACE.OLEDB.12.0; Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test; Extended Properties='Text;Excel 12.0;HDR=Yes;FMT=CSVDelimited;'";

        public static string CsvConnection = "C:\\Users\\savinov\\git\\comcsharp\\Test\\Products.csv";

        protected ExprNode BuildExpr(string str)
        {
            ExprLexer lexer;
            ExprParser parser;
            IParseTree tree;
            string tree_str;
            ExprNode ast;

            ExprBuilder builder = new ExprBuilder();

            lexer = new ExprLexer(new AntlrInputStream(str));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

            return ast;
        }

        protected ComSchema CreateSampleSchema()
        {
            // Prepare schema
            ComSchema schema = new SetTop("My Schema");

            // Table 1
            ComTable t1 = schema.CreateTable("Table 1");
            schema.AddTable(t1, schema.Root, null);

            ComColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetPrimitive("Integer"), true);
            c11.Add();
            ComColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetPrimitive("String"), true);
            c12.Add();
            ComColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetPrimitive("Double"), false);
            c13.Add();
            ComColumn c14 = schema.CreateColumn("Column 14", t1, schema.GetPrimitive("Decimal"), false);
            c14.Add();

            // Table 2
            ComTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, schema.Root, null);

            ComColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetPrimitive("String"), true);
            c21.Add();
            ComColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetPrimitive("Integer"), true);
            c22.Add();
            ComColumn c23 = schema.CreateColumn("Column 23", t2, schema.GetPrimitive("Double"), false);
            c23.Add();
            ComColumn c24 = schema.CreateColumn("Table 1", t2, t1, false);
            c24.Add();

            return schema;
        }

        protected void CreateSampleData(ComSchema schema)
        {
            //
            // Fill sample data in "Table 1"
            //
            ComTable t1 = schema.FindTable("Table 1");

            ComColumn c11 = t1.GetGreaterDim("Column 11");
            ComColumn c12 = t1.GetGreaterDim("Column 12");
            ComColumn c13 = t1.GetGreaterDim("Column 13");
            ComColumn c14 = t1.GetGreaterDim("Column 14");

            ComColumn[] cols = new ComColumn[] { c11, c12, c13, c14 };
            object[] vals = new object[4];

            vals[0] = 20;
            vals[1] = "Record 0";
            vals[2] = 20.0;
            vals[3] = 20.0;
            t1.Data.Append(cols, vals);

            vals[0] = 10;
            vals[1] = "Record 1";
            vals[2] = 10.0;
            vals[3] = 20.0;
            t1.Data.Append(cols, vals);

            vals[0] = 30;
            vals[1] = "Record 2";
            vals[2] = 30.0;
            vals[3] = 20.0;
            t1.Data.Append(cols, vals);

            //
            // Fill sample data in "Table 2"
            //
            ComTable t2 = schema.FindTable("Table 2");

            ComColumn c21 = t2.GetGreaterDim("Column 21");
            ComColumn c22 = t2.GetGreaterDim("Column 22");
            ComColumn c23 = t2.GetGreaterDim("Column 23");
            ComColumn c24 = t2.GetGreaterDim("Table 1");

            cols = new ComColumn[] { c21, c22, c23, c24 };
            vals = new object[4];

            vals[0] = "Value A";
            vals[1] = 20;
            vals[2] = 40.0;
            vals[3] = 0;
            t2.Data.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 40.0;
            vals[3] = 1;
            t2.Data.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 50.0;
            vals[3] = 1;
            t2.Data.Append(cols, vals);

            vals[0] = "Value B";
            vals[1] = 30;
            vals[2] = 50.0;
            vals[3] = 1;
            t2.Data.Append(cols, vals);
        }

        [TestMethod]
        public void SchemaTest() // CsColumn. Manually add/remove tables/columns
        {
            //
            // Prepare schema
            //
            ComSchema schema = CreateSampleSchema();

            ComTable t1 = schema.FindTable("Table 1");
            ComTable t2 = schema.FindTable("Table 2");

            // Finding by name and check various properties provided by the schema
            Assert.AreEqual(schema.GetPrimitive("Decimal").Name, "Decimal");

            Assert.AreEqual(t1.Name, "Table 1");
            Assert.AreEqual(t2.Name, "Table 2");
            Assert.AreEqual(schema.Root.GetTable("Table 2"), t2);

            Assert.AreEqual(t1.GetGreaterDim("Column 11").Name, "Column 11");
            Assert.AreEqual(t2.GetGreaterDim("Column 21").Name, "Column 21");

            Assert.AreEqual(t2.GetGreaterDim("Super").IsSuper, true);
            Assert.AreEqual(t2.SuperDim.LesserSet, t2);
            Assert.AreEqual(t2.SuperDim.GreaterSet, schema.Root);

            // Test path enumerator
            var pathEnum = new PathEnumerator(t2, t1, DimensionType.IDENTITY_ENTITY);
            Assert.AreEqual(1, pathEnum.Count());
        }

        [TestMethod]
        public void ColumnDataTest() // CsColumnData. Manually read/write data
        {
            //
            // Prepare schema
            //
            ComSchema schema = CreateSampleSchema();

            ComTable t1 = schema.FindTable("Table 1");

            ComColumn c11 = t1.GetGreaterDim("Column 11");
            ComColumn c12 = t1.GetGreaterDim("Column 12");
            ComColumn c13 = t1.GetGreaterDim("Column 13");

            ComTable t2 = schema.FindTable("Table 2");
            ComColumn c21 = t2.GetGreaterDim("Column 21");
            ComColumn c22 = t2.GetGreaterDim("Column 22");

            //
            // Data
            //

            t1.Data.Length = 3;

            // 2. Write/read individual column data by using column data methods (not table methods)

            Assert.AreEqual(true, c11.Data.IsNull(1)); // Initially, all outputs must be null
            c11.Data.SetValue(1, 10);
            c11.Data.SetValue(0, 20);
            c11.Data.SetValue(2, 30);
            Assert.AreEqual(false, c11.Data.IsNull(1));
            Assert.AreEqual(10, c11.Data.GetValue(1));

            Assert.AreEqual(true, c13.Data.IsNull(2)); // Initially, all outputs must be null
            c13.Data.SetValue(1, 10.0);
            c13.Data.SetValue(0, 20.0);
            c13.Data.SetValue(2, 30.0);
            Assert.AreEqual(false, c13.Data.IsNull(1));
            Assert.AreEqual(10.0, c13.Data.GetValue(1));

            t2.Data.Length = 2;

            c21.Data.SetValue(0, "Value A");
            c21.Data.SetValue(1, "Value B");

            c22.Data.SetValue(0, 10);
            c22.Data.SetValue(1, 20);

            Assert.AreEqual(10, c22.Data.GetValue(0));
            Assert.AreEqual(20, c22.Data.GetValue(1));
        }

        [TestMethod]
        public void TableDataTest() // CsTableData. Manually read/write data to/from tables
        {
            //
            // Prepare schema
            //
            ComSchema schema = CreateSampleSchema();
            CreateSampleData(schema);

            ComTable t1 = schema.FindTable("Table 1");

            ComColumn c11 = t1.GetGreaterDim("Column 11");
            ComColumn c12 = t1.GetGreaterDim("Column 12");
            ComColumn c13 = t1.GetGreaterDim("Column 13");

            //
            // Data manipulations
            //
            Assert.AreEqual(3, t1.Data.Length);

            Offset input = t1.Data.Find(new ComColumn[] { c11 }, new object[] { 10 } );
            Assert.AreEqual(1, input);

            input = t1.Data.Find(new ComColumn[] { c12 }, new object[] { "Record 1" });
            Assert.AreEqual(1, input);

            input = t1.Data.Find(new ComColumn[] { c12 }, new object[] { "Record Does Not Exist" });
            Assert.AreEqual(-1, input);
        }
        
        [TestMethod]
        public void ColumnDefinitionTest() // CsColumnDefinition. Defining new columns and evaluate them
        {
            //
            // Prepare schema and fill data
            //
            ComSchema schema = CreateSampleSchema();
            CreateSampleData(schema);

            ComTable t1 = schema.FindTable("Table 1");

            ComColumn c11 = t1.GetGreaterDim("Column 11");
            ComColumn c12 = t1.GetGreaterDim("Column 12");
            ComColumn c13 = t1.GetGreaterDim("Column 13");
            ComColumn c14 = t1.GetGreaterDim("Column 14");

            //
            // Define a derived column with a definition
            //
            ComColumn c15 = schema.CreateColumn("Column 15", t1, schema.GetPrimitive("Double"), false);

            c15.Definition.DefinitionType = ColumnDefinitionType.ARITHMETIC;
            c15.Definition.Formula = BuildExpr("([Column 11]+10.0) * this.[Column 13]"); // ConceptScript source code: "[Decimal] [Column 15] <body of expression>"

            c15.Add();

            // Evaluate column
            c15.Definition.Evaluate();

            Assert.AreEqual(600.0, c15.Data.GetValue(0));
            Assert.AreEqual(200.0, c15.Data.GetValue(1));
            Assert.AreEqual(1200.0, c15.Data.GetValue(2));
        }

        [TestMethod]
        public void AggregationTest() // Defining new aggregated columns and evaluate them
        {
            //
            // Prepare schema and fill data
            //
            ComSchema schema = CreateSampleSchema();
            CreateSampleData(schema);

            ComTable t1 = schema.FindTable("Table 1");

            ComTable t2 = schema.FindTable("Table 2");

            ComColumn c23 = t2.GetGreaterDim("Column 23");
            ComColumn c24 = t2.GetGreaterDim("Table 1");

            //
            // Define aggregated column
            //
            ComColumn c15 = schema.CreateColumn("Agg of Column 23", t1, schema.GetPrimitive("Double"), false);
            c15.Definition.DefinitionType = ColumnDefinitionType.AGGREGATION;
            c15.Definition.FactTable = t2; // Fact table
            c15.Definition.GroupPaths = new List<DimPath>(new DimPath[] { new DimPath(c24) }); // One group path
            c15.Definition.MeasurePaths = new List<DimPath>(new DimPath[] { new DimPath(c23) }); // One measure path
            c15.Definition.Updater = "SUM"; // Aggregation function

            c15.Add();

            //
            // Evaluate expression
            //
            c15.Definition.Evaluate(); // {40, 140, 0}

            Assert.AreEqual(40.0, c15.Data.GetValue(0));
            Assert.AreEqual(140.0, c15.Data.GetValue(1));
            Assert.AreEqual(0.0, c15.Data.GetValue(2)); // In fact, it has to be NaN or null (no values have been aggregated)

            // TODO:
            // - initializer and finalizer for aggregation evluation (also for other evaluators but for agg it is more important)
            // - null measure, null facts

            // More complex case:
            // This set is a product of some greater sets
            // grouping function returns a tuple of several greater dims: it is either defined/evaluated independently, or it is defined via grouping expression only for aggregation
            // Question: 
            // - do we have to define this mapped grouping dim explicitly (and compute in advance) or 
            //   - explicit column will already contain/store the group outputs (evaluated and stored independently)
            // - we can define a md-grouping dimension on the fly in the definition of the aggregation function?
            //   - the column for storing values does not exist as an object or at least is not part of the schema (temporary column existing only within the expression)
            //   - so the question is: can we define/use a tuple expression without column objects?

            // How to define AGG function syntactically:
            // special aggregation function call with all necessary parameters:
            // AGG(this (fact), value (measure), accu (SUM, AVG etc.) );
            // Problem: we need to define somewhere the output function name, that is, the result (aggregated function) where everything is stored and which is computed
            // Indeed, it is a definition of some function: Double MyFunc() = ...
            // Alternatively, 
            // - it can be a definition for an updater expression (can be defined for any function along with the setter expression).
            // - in addition, we point to a feeder or provider of facts for this updater (can be shared among many updaters) which is defind independently in the fact table

        }

        [TestMethod]
        public void TableSubsetTest() // Define a filter to get a subset of record from one table
        {
            ComSchema schema = CreateSampleSchema();
            CreateSampleData(schema);

            ComTable t2 = schema.FindTable("Table 2");

            //
            // Define a new filter-set
            //
            ComTable t3 = schema.CreateTable("Table 3");

            ExprNode ast = BuildExpr("[Column 22] > 20.0 && this.Super.[Column 23] < 50");
            t3.Definition.WhereExpression = ast;
            t3.Definition.DefinitionType = TableDefinitionType.PRODUCT;
            schema.AddTable(t3, t2, null);

            t3.Definition.Populate();
            Assert.AreEqual(1, t3.Data.Length);
            Assert.AreEqual(1, t3.SuperDim.Data.GetValue(0));
        }

        [TestMethod]
        public void TableProductTest() // Define a new table and populate it
        {
            ComSchema schema = CreateSampleSchema();
            CreateSampleData(schema);

            ComTable t1 = schema.FindTable("Table 1");
            ComTable t2 = schema.FindTable("Table 2");

            //
            // Define a new product-set
            //
            ComTable t3 = schema.CreateTable("Table 3");
            t3.Definition.DefinitionType = TableDefinitionType.PRODUCT;
            schema.AddTable(t3, null, null);

            ComColumn c31 = schema.CreateColumn(t1.Name, t3, t1, true); // {*20, 10, *30}
            c31.Add();
            ComColumn c32 = schema.CreateColumn(t2.Name, t3, t2, true); // {40, 40, *50, *50}
            c32.Add();

            t3.Definition.Populate();
            Assert.AreEqual(12, t3.Data.Length);

            //
            // Add simple where expression
            //

            ExprNode ast = BuildExpr("([Table 1].[Column 11] > 10) && this.[Table 2].[Column 23] == 50.0");
            t3.Definition.WhereExpression = ast;

            t3.Definition.Populate();
            Assert.AreEqual(4, t3.Data.Length);

            Assert.AreEqual(0, c31.Data.GetValue(0));
            Assert.AreEqual(2, c32.Data.GetValue(0));

            Assert.AreEqual(0, c31.Data.GetValue(1));
            Assert.AreEqual(3, c32.Data.GetValue(1));
        }

        [TestMethod]
        public void ProjectionTest() // Defining new tables via function projection and populate them
        {
            ComSchema schema = CreateSampleSchema();
            CreateSampleData(schema);

            ComTable t2 = schema.FindTable("Table 2");

            ComColumn c21 = t2.GetGreaterDim("Column 21");
            ComColumn c22 = t2.GetGreaterDim("Column 22");
            ComColumn c23 = t2.GetGreaterDim("Column 23");

            //
            // Project "Table 2" along "Column 21" and get 2 unique records in a new set "Value A" (3 references) and "Value B" (1 reference)
            //
            ComTable t3 = schema.CreateTable("Table 3");
            t3.Definition.DefinitionType = TableDefinitionType.PROJECTION;
            schema.AddTable(t3, null, null);

            ComColumn c31 = schema.CreateColumn(c21.Name, t3, c21.GreaterSet, true);
            c31.Add();

            // Manually define a mapping as the generating column definition by using one column only. And create a generating dimension with this mapping.
            Mapping map24 = new Mapping(t2, t3);
            map24.AddMatch(new PathMatch(new DimPath(c21), new DimPath(c31)));

            // Create a generating column
            ComColumn c24 = schema.CreateColumn(map24.SourceSet.Name, map24.SourceSet, map24.TargetSet, false);
            c24.Definition.Mapping = map24;
            c24.Definition.DefinitionType = ColumnDefinitionType.LINK;
            c24.Definition.IsGenerating = true;
            c24.Add();

            t3.Definition.Populate();

            Assert.AreEqual(2, t3.Data.Length);

            Assert.AreEqual(0, c24.Data.GetValue(0));
            Assert.AreEqual(0, c24.Data.GetValue(1));
            Assert.AreEqual(0, c24.Data.GetValue(2));
            Assert.AreEqual(1, c24.Data.GetValue(3));

            //
            // Defining a combination of "Column 21" and "Column 22" and project with 3 unique records in a new set
            //
            ComTable t4 = schema.CreateTable("Table 4");
            t4.Definition.DefinitionType = TableDefinitionType.PROJECTION;
            schema.AddTable(t4, null, null);

            ComColumn c41 = schema.CreateColumn(c21.Name, t4, c21.GreaterSet, true);
            c41.Add();
            ComColumn c42 = schema.CreateColumn(c22.Name, t4, c22.GreaterSet, true);
            c42.Add();

            // Manually define a mapping as the generating column definition by using one column only. And create a generating dimension with this mapping.
            Mapping map25 = new Mapping(t2, t4);
            map25.AddMatch(new PathMatch(new DimPath(c21), new DimPath(c41)));
            map25.AddMatch(new PathMatch(new DimPath(c22), new DimPath(c42)));

            // Create generating/import column
            ComColumn c25 = schema.CreateColumn(map25.SourceSet.Name, map25.SourceSet, map25.TargetSet, false);
            c25.Definition.Mapping = map25;
            c25.Definition.DefinitionType = ColumnDefinitionType.LINK;
            c25.Definition.IsGenerating = true;
            c25.Add();

            t4.Definition.Populate();

            Assert.AreEqual(3, t4.Data.Length);

            Assert.AreEqual(0, c25.Data.GetValue(0));
            Assert.AreEqual(1, c25.Data.GetValue(1));
            Assert.AreEqual(1, c25.Data.GetValue(2));
            Assert.AreEqual(2, c25.Data.GetValue(3));
        }

        [TestMethod]
        public void OledbTest() // Load Oledb schema and data
        {
            // Connection object
            ConnectionOledb conn = new ConnectionOledb();
            conn.ConnectionString = Northwind;
            conn.Open();
            List<string> tables = conn.ReadTables();
            conn.Close();

            Assert.AreEqual(20, tables.Count);

            // Db
            SetTopOledb top = new SetTopOledb("");
            top.connection = conn;

            //
            // Load schema
            //
            top.LoadSchema();

            Assert.AreEqual(20, top.Root.SubSets.Count);
            Assert.AreEqual(11, top.FindTable("Order Details").GreaterDims.Count);

            Assert.AreEqual("Orders", top.FindTable("Order Details").GetGreaterDim("Order ID").GreaterSet.Name);

            // Load data manually
            DataTable dataTable = top.LoadTable((SetRel)top.FindTable("Order Details"));
            Assert.AreEqual(58, dataTable.Rows.Count);
            Assert.AreEqual(37, dataTable.Rows[10][2]);

            //
            // Configure import 
            //
            ComSchema schema = new SetTop("My Schema");

            ComTable orderDetailsTable = schema.CreateTable("Order Details");
            orderDetailsTable.Definition.DefinitionType = TableDefinitionType.PROJECTION;
            
            // Create mapping
            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(top.FindTable("Order Details"), orderDetailsTable, schema);
            map.Matches.ForEach(m => m.TargetPath.Path.ForEach(p => p.Add()));

            // Create generating/import column
            ComColumn dim = schema.CreateColumn(map.SourceSet.Name, map.SourceSet, map.TargetSet, false);
            dim.Definition.Mapping = map;
            dim.Definition.DefinitionType = ColumnDefinitionType.LINK;
            dim.Definition.IsGenerating = true;

            dim.Add();

            schema.AddTable(orderDetailsTable, null, null);
            orderDetailsTable.Definition.Populate();

            Assert.AreEqual(58, orderDetailsTable.Data.Length);
        }

        [TestMethod]
        public void CsvTest() // Load Csv schema and data
        {
            // Create schema for a remote db
            SetTopCsv top = new SetTopCsv("My Files");

            // Load schema
            SetCsv table = (SetCsv)top.CreateTable("Products");
            table.FilePath = CsvConnection;
            top.LoadSchema(table);

            Assert.AreEqual(1, top.Root.SubSets.Count);
            Assert.AreEqual(15, top.FindTable("Products").GreaterDims.Count);

            Assert.AreEqual("String", top.FindTable("Products").GetGreaterDim("Product Name").GreaterSet.Name);
            Assert.AreEqual("3", ((DimCsv)top.FindTable("Products").GetGreaterDim("ID")).SampleValues[1]);

            //
            // Configure import 
            //
            ComSchema schema = new SetTop("My Schema");

            ComTable productsTable = schema.CreateTable("Products");
            productsTable.Definition.DefinitionType = TableDefinitionType.PROJECTION;

            // Create mapping. 
            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(top.FindTable("Products"), productsTable, schema); // It will map source String to different target types
            map.Matches.ForEach(m => m.TargetPath.Path.ForEach(p => p.Add()));

            // Create generating/import column
            ComColumn dim = schema.CreateColumn(map.SourceSet.Name, map.SourceSet, map.TargetSet, false);
            dim.Definition.Mapping = map;
            dim.Definition.DefinitionType = ColumnDefinitionType.LINK;
            dim.Definition.IsGenerating = true;

            dim.Add();

            schema.AddTable(productsTable, null, null);
            productsTable.Definition.Populate();

            Assert.AreEqual(45, productsTable.Data.Length);
        }

        [TestMethod]
        public void JsonTest() // Serialize/deserialize schema elements
        {
            ComSchema sampleSchema = CreateSampleSchema();

            // Add table definition 
            ExprNode ast = BuildExpr("[Column 22] > 20.0 && this.Super.[Column 23] < 50");
            ComTable t = sampleSchema.FindTable("Table 2");
            t.Definition.DefinitionType = TableDefinitionType.PRODUCT;
            t.Definition.WhereExpression = ast;

            // Add column definition 
            ComColumn c = t.GetGreaterDim("Column 22");
            c.Definition.DefinitionType = ColumnDefinitionType.ARITHMETIC;
            c.Definition.Formula = BuildExpr("([Column 11]+10.0) * this.[Column 13]");

            Workspace ws = new Workspace();
            ws.Schemas.Add(sampleSchema);

            JObject workspace = Utils.CreateJsonFromObject(ws);
            ws.ToJson(workspace);

            // Serialize into json string
            string jsonWs = JsonConvert.SerializeObject(workspace, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonSerializerSettings { });

            // De-serialize from json string: http://weblog.west-wind.com/posts/2012/Aug/30/Using-JSONNET-for-dynamic-JSON-parsing
            dynamic objWs = JsonConvert.DeserializeObject(jsonWs);
            //dynamic obj = JObject/JValue/JArray.Parse(json);

            //
            // Instantiate and initialize
            //
            ws = Utils.CreateObjectFromJson(objWs);
            ws.FromJson(objWs, ws);

            Assert.AreEqual(5, ws.Schemas[0].FindTable("Table 1").GreaterDims.Count);
            Assert.AreEqual(5, ws.Schemas[0].FindTable("Table 2").GreaterDims.Count);

            Assert.AreEqual("Table 1", ws.Schemas[0].FindTable("Table 2").GetGreaterDim("Table 1").GreaterSet.Name);

            t = ws.Schemas[0].FindTable("Table 2");
            Assert.AreEqual(TableDefinitionType.PRODUCT, t.Definition.DefinitionType);
            Assert.AreEqual(2, t.Definition.WhereExpression.Children.Count);

            c = t.GetGreaterDim("Column 22");
            Assert.AreEqual(ColumnDefinitionType.ARITHMETIC, c.Definition.DefinitionType);
            Assert.AreEqual(2, c.Definition.Formula.Children.Count);

            //
            // 2. Another sample schema with several schemas and inter-schema columns
            //
            string jsonWs2 = @"{ 
'type': 'Workspace', 
'mashup': {schema_name:'My Schema'}, 
'schemas': [ 

{ 
'type': 'SetTop', 
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
'type': 'SetTopCsv', 
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
            Assert.AreEqual("My Table", ws2.Schemas[1].FindTable("Rel Table").GetGreaterDim("Import Column").GreaterSet.Name);
            */
        }
    
    }


    
    // TODO:
    // - utility method to (quickly) check type of primitive set without comparing name (with case sensititivy). Maybe introduce enumerator. 
    // - also, find primitive type using enumerator instead of string comparison.

    // Other TODOs:

    // Expr Evaluate
    // - Type inference rules for arithmetic expressions
    // - Use of these rules in computing arithmetic results, that is, double+integer has to use double plus operation

    // Evaluator interface rework:
    // OledbEvaluator opens a data sets with loading data. So we need to correctly close the resul set etc. 
    // - Probably, it could be done in Initialize and Finish methods (Next/Evaluate is called between them)
    // Import dimensions do not store data so they should not have ColumnData object (null) even though they have some local type
    // Evaluate/EvaluateUpdate etc. could be probably implemented within one Evaluate depending on the real expression and task to be performed
    // - IsUpdate - type of change of the function output: accumulate or set. Essentially, whether it is aggregation evaluator. Do we need it?
    // - LoopTable - what set to iterate (fact set for aggregation)
    // - is supposed to be from Evaluate of Column to decide what to loop and how to update etc.

    // ExprNode constructores and update code: name/operation/action constructor, type/set constructor

    // Set::Find, Set::Appenbd
    // - There are two Find. Problem is that one uses only key dims while the other all provided parameter values - Decide what is better
    // - Decide whether Append should check uniqueness and Where conditions or it has been done externally

    // There are generating dims and simply mapped dims. Generating dims are used by set population only, while normal mapped dims are used by dim population. 
    // Currently any new mapped dim created via Dim(Mapping) is marked as a generating dim. It is better to mark it explicitly as generating dim or not rather than automatically.

    // Set::Populate
    // !!! Append an element to the generating/projection column(s) if an element has been appended/found in the target set. Alternatively, we will have to evaluate this generating dimension separately (double work).
    // - This means a principle: generating dimensions are not evaluated separately - they are evaluated during their target set population.
    // - For Oledb (import/export) dims it is not needed because these dimensions do not store data.
    // - !!! For product, turn off indexing and then index the whole result set at the end. It is because we append ALL generated elements and then remove them if they do not satisfy the where condition which can be very inefficient. 
    //   - Introduce API for controling indexing (on/off, table/column etc.)
    //   - Alternativey, we could introduce 'this' value as an Expr instance similar to having DataRow for 'this' value, and then a special evaluator. But it might be more difficult and more restrictive in future for complex where conditions.

    // Dim population principles:
    // - currently we Append from the loop manually and not from the expression (as opposed ot Set::Populate) - it is bad: either all in Evaluate or all in Populate
    //   - so we should NOT use column Evaluator for populating a set 
    //      --> column evaluator of generating dims is never used from the colum population procedure (but can be if called explicitly) 
    //      --> all column evaluators NEVER change (influence) their sets (neither greater nor lesser) - it computes only this function

}
