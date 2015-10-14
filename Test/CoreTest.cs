using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Com.Utils;
using Com.Schema;
using Com.Schema.Rel;
using Com.Schema.Csv;
using Com.Data;
using Com.Data.Query;
using Com.Data.Eval;

using Offset = System.Int32;

// Unit test: http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class CoreTest
    {
        public static string Northwind = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\dce-csharp\\Test\\Northwind.accdb";
        public static string TextDbConnection = "Provider=Microsoft.ACE.OLEDB.12.0; Data Source=C:\\Users\\savinov\\git\\dce-csharp\\Test; Extended Properties='Text;Excel 12.0;HDR=Yes;FMT=CSVDelimited;'";

        public static string CsvRead = "C:\\Users\\savinov\\git\\dce-csharp\\Test\\Products.csv";
        public static string CsvWrite = "C:\\Users\\savinov\\git\\dce-csharp\\Test\\_temp_test_output.csv";

        public static ExprBuilder ExprBuilder { get; set; }

        private static TestContext context;

        #region Initialisation and cleanup

        [ClassInitialize()]
        public static void SetUpClass(TestContext testContext)
        {
            context = testContext; 
            ExprBuilder = new ExprBuilder();
        }
        
        DcWorkspace workspace { get; set; }
        DcSchema schema { get; set; }

        [TestInitialize()]
        public void SetUp() {
            workspace = new Workspace();

            //
            // Prepare schema
            //
            schema = CreateSampleSchema();
            workspace.Schemas.Add(schema);
            schema.Workspace = workspace;
        }
    
        protected DcSchema CreateSampleSchema()
        {
            // Prepare schema
            DcSchema schema = new Schema("My Schema");

            // Table 1
            DcTable t1 = schema.CreateTable("Table 1");
            schema.AddTable(t1, schema.Root, null);

            DcColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetPrimitive("Integer"), true);
            c11.Add();
            DcColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetPrimitive("String"), true);
            c12.Add();
            DcColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetPrimitive("Double"), false);
            c13.Add();
            DcColumn c14 = schema.CreateColumn("Column 14", t1, schema.GetPrimitive("Decimal"), false);
            c14.Add();

            // Table 2
            DcTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, schema.Root, null);

            DcColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetPrimitive("String"), true);
            c21.Add();
            DcColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetPrimitive("Integer"), true);
            c22.Add();
            DcColumn c23 = schema.CreateColumn("Column 23", t2, schema.GetPrimitive("Double"), false);
            c23.Add();
            DcColumn c24 = schema.CreateColumn("Table 1", t2, t1, false);
            c24.Add();

            return schema;
        }

        protected void CreateSampleData(DcSchema schema)
        {
            //
            // Fill sample data in "Table 1"
            //
            DcTable t1 = schema.GetSubTable("Table 1");

            DcColumn c11 = t1.GetColumn("Column 11");
            DcColumn c12 = t1.GetColumn("Column 12");
            DcColumn c13 = t1.GetColumn("Column 13");
            DcColumn c14 = t1.GetColumn("Column 14");

            DcColumn[] cols = new DcColumn[] { c11, c12, c13, c14 };
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
            DcTable t2 = schema.GetSubTable("Table 2");

            DcColumn c21 = t2.GetColumn("Column 21");
            DcColumn c22 = t2.GetColumn("Column 22");
            DcColumn c23 = t2.GetColumn("Column 23");
            DcColumn c24 = t2.GetColumn("Table 1");

            cols = new DcColumn[] { c21, c22, c23, c24 };
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

        #endregion

        [TestMethod]
        public void SchemaTest() // ComColumn. Manually add/remove tables/columns
        {
            DcTable t1 = schema.GetSubTable("Table 1");
            DcTable t2 = schema.GetSubTable("Table 2");

            // Finding by name and check various properties provided by the schema
            Assert.AreEqual(schema.GetPrimitive("Decimal").Name, "Decimal");

            Assert.AreEqual(t1.Name, "Table 1");
            Assert.AreEqual(t2.Name, "Table 2");
            Assert.AreEqual(schema.GetSubTable("Table 2"), t2);

            Assert.AreEqual(t1.GetColumn("Column 11").Name, "Column 11");
            Assert.AreEqual(t2.GetColumn("Column 21").Name, "Column 21");

            Assert.AreEqual(t2.GetColumn("Super").IsSuper, true);
            Assert.AreEqual(t2.SuperColumn.Input, t2);
            Assert.AreEqual(t2.SuperColumn.Output, schema.Root);

            // Test path enumerator
            var pathEnum = new PathEnumerator(t2, t1, DimensionType.IDENTITY_ENTITY);
            Assert.AreEqual(1, pathEnum.Count());
        }

        [TestMethod]
        public void ColumnDataTest() // ComColumnData. Manually read/write data
        {
            DcTable t1 = schema.GetSubTable("Table 1");

            DcColumn c11 = t1.GetColumn("Column 11");
            DcColumn c12 = t1.GetColumn("Column 12");
            DcColumn c13 = t1.GetColumn("Column 13");

            DcTable t2 = schema.GetSubTable("Table 2");
            DcColumn c21 = t2.GetColumn("Column 21");
            DcColumn c22 = t2.GetColumn("Column 22");

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
        public void TableDataTest() // ComTableData. Manually read/write data to/from tables
        {
            CreateSampleData(schema);

            DcTable t1 = schema.GetSubTable("Table 1");

            DcColumn c11 = t1.GetColumn("Column 11");
            DcColumn c12 = t1.GetColumn("Column 12");
            DcColumn c13 = t1.GetColumn("Column 13");

            //
            // Data manipulations
            //
            Assert.AreEqual(3, t1.Data.Length);

            Offset input = t1.Data.Find(new DcColumn[] { c11 }, new object[] { 10 } );
            Assert.AreEqual(1, input);

            input = t1.Data.Find(new DcColumn[] { c12 }, new object[] { "Record 1" });
            Assert.AreEqual(1, input);

            input = t1.Data.Find(new DcColumn[] { c12 }, new object[] { "Record Does Not Exist" });
            Assert.AreEqual(-1, input);
        }
        
        [TestMethod]
        public void ArithmeticTest() // ComColumnDefinition. Defining new columns and evaluate them
        {
            CreateSampleData(schema);

            DcTable t1 = schema.GetSubTable("Table 1");

            DcColumn c11 = t1.GetColumn("Column 11");
            DcColumn c12 = t1.GetColumn("Column 12");
            DcColumn c13 = t1.GetColumn("Column 13");
            DcColumn c14 = t1.GetColumn("Column 14");

            //
            // Define a derived column with a definition
            //
            DcColumn c15 = schema.CreateColumn("Column 15", t1, schema.GetPrimitive("Double"), false);

            c15.Definition.Formula = "([Column 11]+10.0) * this.[Column 13]";

            c15.Add();

            // Evaluate column
            c15.Definition.Evaluate();

            Assert.AreEqual(600.0, c15.Data.GetValue(0));
            Assert.AreEqual(200.0, c15.Data.GetValue(1));
            Assert.AreEqual(1200.0, c15.Data.GetValue(2));
        }

        [TestMethod]
        public void NativeFunctionTest() // Call native function in column definition
        {
            CreateSampleData(schema);

            DcTable t1 = schema.GetSubTable("Table 1");

            DcColumn c11 = t1.GetColumn("Column 11");
            DcColumn c12 = t1.GetColumn("Column 12");
            DcColumn c13 = t1.GetColumn("Column 13");
            DcColumn c14 = t1.GetColumn("Column 14");

            //
            // Define a derived column with a definition
            //
            DcColumn c15 = schema.CreateColumn("Column 15", t1, schema.GetPrimitive("String"), false);

            c15.Definition.Formula = "call:System.String.Substring( [Column 12], 7, 1 )";

            c15.Add();

            // Evaluate column
            c15.Definition.Evaluate();

            Assert.AreEqual("0", c15.Data.GetValue(0));
            Assert.AreEqual("1", c15.Data.GetValue(1));
            Assert.AreEqual("2", c15.Data.GetValue(2));

            //
            // Define a derived column with a definition
            //
            DcColumn c16 = schema.CreateColumn("Column 15", t1, schema.GetPrimitive("Double"), false);

            c16.Definition.Formula = "call:System.Math.Pow( [Column 11] / 10.0, [Column 13] / 10.0 )";

            c16.Add();

            c16.Definition.Evaluate();

            Assert.AreEqual(4.0, c16.Data.GetValue(0));
            Assert.AreEqual(1.0, c16.Data.GetValue(1));
            Assert.AreEqual(27.0, c16.Data.GetValue(2));
        }

        [TestMethod]
        public void LinkTest()
        {
            CreateSampleData(schema);

            DcTable t1 = schema.GetSubTable("Table 1");
            DcColumn c11 = t1.GetColumn("Column 11"); // 20, 10, 30

            DcTable t2 = schema.GetSubTable("Table 2");
            DcColumn c22 = t2.GetColumn("Column 22"); // 20, 30, 30, 30

            //
            // Define a derived column with a definition
            //

            DcColumn link = schema.CreateColumn("Column Link", t2, t1, false);

            link.Definition.Formula = "(( [Integer] [Column 11] = this.[Column 22], [Double] [Column 14] = 20.0 ))"; // Tuple structure corresponds to output table

            link.Add();

            // Evaluate column
            link.Definition.Evaluate();

            Assert.AreEqual(0, link.Data.GetValue(0));
            Assert.AreEqual(2, link.Data.GetValue(1));
            Assert.AreEqual(2, link.Data.GetValue(2));
            Assert.AreEqual(2, link.Data.GetValue(2));
        }

        [TestMethod]
        public void AggregationTest() // Defining new aggregated columns and evaluate them
        {
            CreateSampleData(schema);

            DcTable t1 = schema.GetSubTable("Table 1");

            DcTable t2 = schema.GetSubTable("Table 2");

            DcColumn c23 = t2.GetColumn("Column 23");
            DcColumn c24 = t2.GetColumn("Table 1");

            //
            // Define aggregated column
            //
            /* We do not use non-syntactic (object) formulas
            DcColumn c15 = schema.CreateColumn("Agg of Column 23", t1, schema.GetPrimitive("Double"), false);
            c15.Definition.DefinitionType = DcColumnDefinitionType.AGGREGATION;

            c15.Definition.FactTable = t2; // Fact table
            c15.Definition.GroupPaths = new List<DimPath>(new DimPath[] { new DimPath(c24) }); // One group path
            c15.Definition.MeasurePaths = new List<DimPath>(new DimPath[] { new DimPath(c23) }); // One measure path
            c15.Definition.Updater = "SUM"; // Aggregation function

            c15.Add();

            //
            // Evaluate expression
            //
            c15.Data.SetValue(0.0);
            c15.Definition.Evaluate(); // {40, 140, 0}

            Assert.AreEqual(40.0, c15.Data.GetValue(0));
            Assert.AreEqual(140.0, c15.Data.GetValue(1));
            Assert.AreEqual(0.0, c15.Data.GetValue(2)); // In fact, it has to be NaN or null (no values have been aggregated)
            */

            //
            // Aggregation via a syntactic formula
            //
            DcColumn c16 = schema.CreateColumn("Agg2 of Column 23", t1, schema.GetPrimitive("Double"), false);

            c16.Definition.Formula = "AGGREGATE(facts=[Table 2], groups=[Table 1], measure=[Column 23]*2.0 + 1, aggregator=SUM)";

            c16.Add();

            c16.Data.SetValue(0.0);
            c16.Definition.Evaluate(); // {40, 140, 0}

            Assert.AreEqual(81.0, c16.Data.GetValue(0));
            Assert.AreEqual(283.0, c16.Data.GetValue(1));
            Assert.AreEqual(0.0, c16.Data.GetValue(2));
        }

        [TestMethod]
        public void TableProductTest() // Define a new table and populate it
        {
            CreateSampleData(schema);

            DcTable t1 = schema.GetSubTable("Table 1");
            DcTable t2 = schema.GetSubTable("Table 2");

            //
            // Define a new product-set
            //
            DcTable t3 = schema.CreateTable("Table 3");
            t3.Definition.DefinitionType = DcTableDefinitionType.PRODUCT;
            schema.AddTable(t3, null, null);

            DcColumn c31 = schema.CreateColumn(t1.Name, t3, t1, true); // {*20, 10, *30}
            c31.Add();
            DcColumn c32 = schema.CreateColumn(t2.Name, t3, t2, true); // {40, 40, *50, *50}
            c32.Add();

            t3.Definition.Populate();
            Assert.AreEqual(12, t3.Data.Length);

            //
            // Add simple where expression
            //

            ExprNode ast = ExprBuilder.Build("([Table 1].[Column 11] > 10) && this.[Table 2].[Column 23] == 50.0");
            t3.Definition.WhereExpr = ast;

            t3.Definition.Populate();
            Assert.AreEqual(4, t3.Data.Length);

            Assert.AreEqual(0, c31.Data.GetValue(0));
            Assert.AreEqual(2, c32.Data.GetValue(0));

            Assert.AreEqual(0, c31.Data.GetValue(1));
            Assert.AreEqual(3, c32.Data.GetValue(1));
        }

        [TestMethod]
        public void TableSubsetTest() // Define a filter to get a subset of record from one table
        {
            CreateSampleData(schema);

            DcTable t2 = schema.GetSubTable("Table 2");

            //
            // Define a new filter-set
            //
            DcTable t3 = schema.CreateTable("Table 3");

            ExprNode ast = ExprBuilder.Build("[Column 22] > 20.0 && this.Super.[Column 23] < 50");
            t3.Definition.WhereExpr = ast;
            t3.Definition.DefinitionType = DcTableDefinitionType.PRODUCT;

            schema.AddTable(t3, t2, null);

            t3.Definition.Populate();
            Assert.AreEqual(1, t3.Data.Length);
            Assert.AreEqual(1, t3.SuperColumn.Data.GetValue(0));
        }

        [TestMethod]
        public void ProjectionTest() // Defining new tables via function projection and populate them
        {
            CreateSampleData(schema);

            DcTable t2 = schema.GetSubTable("Table 2");

            DcColumn c21 = t2.GetColumn("Column 21");
            DcColumn c22 = t2.GetColumn("Column 22");
            DcColumn c23 = t2.GetColumn("Column 23");

            //
            // Project "Table 2" along "Column 21" and get 2 unique records in a new set "Value A" (3 references) and "Value B" (1 reference)
            //
            DcTable t3 = schema.CreateTable("Table 3");
            t3.Definition.DefinitionType = DcTableDefinitionType.PROJECTION;
            schema.AddTable(t3, null, null);

            DcColumn c31 = schema.CreateColumn(c21.Name, t3, c21.Output, true);
            c31.Add();

            // Manually define a mapping as the generating column definition by using one column only. And create a generating dimension with this mapping.
            /*
            Mapping map24 = new Mapping(t2, t3);
            map24.AddMatch(new PathMatch(new DimPath(c21), new DimPath(c31)));

            // Create a generating column
            DcColumn c24 = schema.CreateColumn(map24.SourceSet.Name, map24.SourceSet, map24.TargetSet, false);
            c24.Definition.Mapping = map24;
            c24.Definition.DefinitionType = DcColumnDefinitionType.LINK;
            c24.Definition.IsAppendData = true;
            c24.Add();

            t3.Definition.Populate();

            Assert.AreEqual(2, t3.Data.Length);

            Assert.AreEqual(0, c24.Data.GetValue(0));
            Assert.AreEqual(0, c24.Data.GetValue(1));
            Assert.AreEqual(0, c24.Data.GetValue(2));
            Assert.AreEqual(1, c24.Data.GetValue(3));
            */

            //
            // Defining a combination of "Column 21" and "Column 22" and project with 3 unique records in a new set
            //
            DcTable t4 = schema.CreateTable("Table 4");
            t4.Definition.DefinitionType = DcTableDefinitionType.PROJECTION;
            schema.AddTable(t4, null, null);

            DcColumn c41 = schema.CreateColumn(c21.Name, t4, c21.Output, true);
            c41.Add();
            DcColumn c42 = schema.CreateColumn(c22.Name, t4, c22.Output, true);
            c42.Add();

            // Manually define a mapping as the generating column definition by using one column only. And create a generating dimension with this mapping.
            /*
            Mapping map25 = new Mapping(t2, t4);
            map25.AddMatch(new PathMatch(new DimPath(c21), new DimPath(c41)));
            map25.AddMatch(new PathMatch(new DimPath(c22), new DimPath(c42)));

            // Create generating/import column
            DcColumn c25 = schema.CreateColumn(map25.SourceSet.Name, map25.SourceSet, map25.TargetSet, false);
            c25.Definition.Mapping = map25;
            c25.Definition.DefinitionType = DcColumnDefinitionType.LINK;
            c25.Definition.IsAppendData = true;
            c25.Add();

            t4.Definition.Populate();

            Assert.AreEqual(3, t4.Data.Length);

            Assert.AreEqual(0, c25.Data.GetValue(0));
            Assert.AreEqual(1, c25.Data.GetValue(1));
            Assert.AreEqual(1, c25.Data.GetValue(2));
            Assert.AreEqual(2, c25.Data.GetValue(3));
            */
        }

        [TestMethod]
        public void OledbTest() // Load Oledb schema and data
        {
            // Important: this test uses Oledb engine which is architecture dependent (32 or 64) and hence the test can fail depending on what kind of Oledb engine is present (installed)
            // The test executable has to have the same architecture as the installed Oledb engine (and it can depend on, say, the MS Office architecture)
            // In VS, this can be set in: Menu | Test | Test Settings | Default Processor Architecture 

            // Connection object
            ConnectionOledb conn = new ConnectionOledb();
            conn.ConnectionString = Northwind;
            conn.Open();
            List<string> tables = conn.ReadTables();
            conn.Close();

            Assert.AreEqual(20, tables.Count);

            DcWorkspace workspace = new Workspace();

            // Db
            SchemaOledb top = new SchemaOledb("");
            top.connection = conn;
            workspace.Schemas.Add(top);
            top.Workspace = workspace;

            //
            // Load schema
            //
            top.LoadSchema();

            Assert.AreEqual(20, top.Root.SubTables.Count);
            Assert.AreEqual(11, top.GetSubTable("Order Details").Columns.Count);

            Assert.AreEqual("Orders", top.GetSubTable("Order Details").GetColumn("Order ID").Output.Name);

            // Load data manually
            DataTable dataTable = top.LoadTable((SetRel)top.GetSubTable("Order Details"));
            Assert.AreEqual(58, dataTable.Rows.Count);
            Assert.AreEqual(37, dataTable.Rows[10][2]);

            //
            // Configure import 
            //
            DcSchema schema = new Schema("My Schema");
            workspace.Schemas.Add(schema);
            schema.Workspace = workspace;

            DcTable orderDetailsTable = schema.CreateTable("Order Details");
            orderDetailsTable.Definition.DefinitionType = DcTableDefinitionType.PROJECTION;
            
            // Create mapping
            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(top.GetSubTable("Order Details"), orderDetailsTable, schema);
            map.Matches.ForEach(m => m.TargetPath.Segments.ForEach(p => p.Add()));

            // Create generating/import column
            /*
            DcColumn dim = schema.CreateColumn(map.SourceSet.Name, map.SourceSet, map.TargetSet, false);
            dim.Definition.Mapping = map;
            dim.Definition.DefinitionType = DcColumnDefinitionType.LINK;
            dim.Definition.IsAppendData = true;

            dim.Add();

            schema.AddTable(orderDetailsTable, null, null);
            orderDetailsTable.Definition.Populate();

            Assert.AreEqual(58, orderDetailsTable.Data.Length);
            */
        }

        [TestMethod]
        public void CsvReadTest() // Load Csv schema and data as a result of evaluation
        {
            DcWorkspace workspace = new Workspace();

            // Create schema for a remote db
            SchemaCsv top = new SchemaCsv("My Files");
            workspace.Schemas.Add(top);
            top.Workspace = workspace;

            // Create a remote file description
            SetCsv table = (SetCsv)top.CreateTable("Products");
            table.FilePath = CsvRead;
            var columns = top.LoadSchema(table);
            columns.ForEach(x => x.Add());
            top.AddTable(table, null, null);

            Assert.AreEqual(1, top.Root.SubTables.Count);
            Assert.AreEqual(15, top.GetSubTable("Products").Columns.Count);

            Assert.AreEqual("String", top.GetSubTable("Products").GetColumn("Product Name").Output.Name);
            Assert.AreEqual("3", ((DimCsv)top.GetSubTable("Products").GetColumn("ID")).SampleValues[1]);

            //
            // Configure import 
            //
            DcSchema schema = new Schema("My Schema");
            workspace.Schemas.Add(schema);
            schema.Workspace = workspace;

            DcTable productsTable = schema.CreateTable("Products");
            schema.AddTable(productsTable, null, null);
            productsTable.Definition.DefinitionType = DcTableDefinitionType.PROJECTION;

            // Manually create column to be imported (we need an automatic mechanism for appending missing columns specified in the formula)
            DcColumn p1 = schema.CreateColumn("ID", productsTable, schema.GetPrimitive("Integer"), true);
            p1.Add();
            DcColumn p2 = schema.CreateColumn("Product Code", productsTable, schema.GetPrimitive("String"), false);
            p2.Add();
            DcColumn p3 = schema.CreateColumn("Custom Product Name", productsTable, schema.GetPrimitive("String"), false);
            p3.Add();
            DcColumn p4 = schema.CreateColumn("List Price", productsTable, schema.GetPrimitive("Double"), false);
            p4.Add();
            DcColumn p5 = schema.CreateColumn("Constant Column", productsTable, schema.GetPrimitive("Double"), false);
            p5.Add();

            // Define import column
            DcColumn dim = schema.CreateColumn("Import", top.GetSubTable("Products"), productsTable, false);
            dim.Add();
            dim.Definition.IsAppendData = true;
            dim.Definition.Formula = "(( [Integer] [ID] = this.[ID], [String] [Product Code] = [Product Code], [String] [Custom Product Name] = [Product Name], [Double] [List Price] = [List Price], [Double] [Constant Column] = 20.0 ))"; // Tuple structure corresponds to output table
            dim.Definition.IsAppendData = true;
            dim.Definition.IsAppendSchema = true;

            // NOTE: Maybe evaluate dim instead??? Do we need Populate for a table at all???
            productsTable.Definition.Populate();

            Assert.AreEqual(45, productsTable.Data.Length);
            Assert.AreEqual("Northwind Traders Dried Pears", p3.Data.GetValue(5));
            Assert.AreEqual(20.0, p5.Data.GetValue(5));
        }

        [TestMethod]
        public void CsvWriteTest() // Store schema and data to a CSV file as a result of evaluation
        {
            DcWorkspace workspace = new Workspace();

            DcSchema schema = CreateSampleSchema();
            workspace.Schemas.Add(schema);
            schema.Workspace = workspace;

            CreateSampleData(schema);

            DcTable t2 = schema.GetSubTable("Table 2");

            DcColumn c21 = t2.GetColumn("Column 21");
            DcColumn c22 = t2.GetColumn("Column 22");
            DcColumn c23 = t2.GetColumn("Column 23");

            //
            // Create schema for a remote db
            //
            SchemaCsv top = new SchemaCsv("My Files");
            workspace.Schemas.Add(top);
            top.Workspace = workspace;

            // Create a remote file description
            SetCsv table = (SetCsv)top.CreateTable("Table_1");
            table.FilePath = CsvWrite;
            table.Definition.DefinitionType = DcTableDefinitionType.PROJECTION;
            top.AddTable(table, null, null);

            //
            // Configure import 
            //

            // Create mapping. 
            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(schema.GetSubTable("Table 1"), table, top); // It will map source String to different target types
            map.Matches.ForEach(m => m.TargetPath.Segments.ForEach(p => p.Add()));

            // Create generating/import column
            /*
            DcColumn dim = schema.CreateColumn(map.SourceSet.Name, map.SourceSet, map.TargetSet, false);
            dim.Definition.Mapping = map;
            dim.Definition.DefinitionType = DcColumnDefinitionType.LINK;
            dim.Definition.IsAppendData = true;

            dim.Add();

            table.Definition.Populate();
            // 1. Create evaluator by generating Expr 
            //    --> Here we need mapping and output (csv table) column structure. Do we need them? How can we generate them? 
            //    --> Maybe here we need AppendStructure property and the corresponding method?
            // 2. Iterate (through normal set) by calling Expr evaluate 
            // 3. From Expr TUPLE (root), write records to the file by calling Append (and Find)
            //    --> These Append/Find are overriden in the SetCsv

            // TODO: We could test by reading the file manually
            */
        }

        [TestMethod]
        public void JsonTest() // Serialize/deserialize schema elements
        {
            DcSchema schema = CreateSampleSchema();
            DcWorkspace sampleWs = new Workspace();
            sampleWs.Schemas.Add(schema);
            schema.Workspace = sampleWs;

            // Add table definition 
            DcTable t = schema.GetSubTable("Table 2");
            t.Definition.DefinitionType = DcTableDefinitionType.PRODUCT;
            ExprNode ast = ExprBuilder.Build("[Column 22] > 20.0 && this.Super.[Column 23] < 50");
            t.Definition.WhereExpr = ast;

            // Add column definition 
            DcColumn c = t.GetColumn("Column 22");
            c.Definition.Formula = "([Column 11]+10.0) * this.[Column 13]";

            DcWorkspace ws = new Workspace();
            ws.Schemas.Add(schema);

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
            ws = (Workspace)Utils.CreateObjectFromJson(objWs);
            ((Workspace)ws).FromJson(objWs, ws);

            Assert.AreEqual(5, ws.Schemas[0].GetSubTable("Table 1").Columns.Count);
            Assert.AreEqual(5, ws.Schemas[0].GetSubTable("Table 2").Columns.Count);

            Assert.AreEqual("Table 1", ws.Schemas[0].GetSubTable("Table 2").GetColumn("Table 1").Output.Name);

            t = ws.Schemas[0].GetSubTable("Table 2");
            Assert.AreEqual(DcTableDefinitionType.PRODUCT, t.Definition.DefinitionType);
            Assert.AreEqual(2, t.Definition.WhereExpr.Children.Count);

            c = t.GetColumn("Column 22");
            //Assert.AreEqual(DcColumnDefinitionType.ARITHMETIC, c.Definition.FormulaExpr.DefinitionType);
            Assert.AreEqual(2, c.Definition.FormulaExpr.Children.Count);

            //
            // 2. Another sample schema with several schemas and inter-schema columns
            //
            string jsonWs2 = @"{ 
'type': 'Workspace', 
'mashup': {schema_name:'My Schema'}, 
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
