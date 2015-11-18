using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Schema.Csv;

using Offset = System.Int32;

namespace Test
{
    [TestClass]
    public class CsvTest
    {
        public static string CsvRead = "C:\\Users\\savinov\\git\\dce-csharp\\Test\\Products.csv";
        public static string CsvWrite = "C:\\Users\\savinov\\git\\dce-csharp\\Test\\_temp_test_output.csv";

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
        public void CsvReadTest() // Load Csv schema and data as a result of evaluation
        {
            DcSpace space = new Space();

            // Create schema for a remote db
            SchemaCsv top = (SchemaCsv)space.CreateSchema("My Files", DcSchemaKind.Csv);

            // Create a remote file description
            TableCsv table = (TableCsv)space.CreateTable("Products", top.Root);
            table.FilePath = CsvRead;
            var columns = top.LoadSchema(table);

            Assert.AreEqual(1, top.Root.SubTables.Count);
            Assert.AreEqual(15, top.GetSubTable("Products").Columns.Count);

            Assert.AreEqual("String", top.GetSubTable("Products").GetColumn("Product Name").Output.Name);
            Assert.AreEqual("3", ((ColumnCsv)top.GetSubTable("Products").GetColumn("ID")).SampleValues[1]);

            //
            // Configure import 
            //
            DcSchema schema = space.CreateSchema("My Schema", DcSchemaKind.Dc);

            DcTable productsTable = space.CreateTable("Products", schema.Root);

            // Manually create column to be imported (we need an automatic mechanism for appending missing columns specified in the formula)
            DcColumn p1 = space.CreateColumn("ID", productsTable, schema.GetPrimitive("Integer"), true);
            DcColumn p2 = space.CreateColumn("Product Code", productsTable, schema.GetPrimitive("String"), false);
            DcColumn p3 = space.CreateColumn("Custom Product Name", productsTable, schema.GetPrimitive("String"), false);
            DcColumn p4 = space.CreateColumn("List Price", productsTable, schema.GetPrimitive("Double"), false);
            DcColumn p5 = space.CreateColumn("Constant Column", productsTable, schema.GetPrimitive("Double"), false);

            // Define import column
            DcColumn col = space.CreateColumn("Import", top.GetSubTable("Products"), productsTable, false);
            col.GetData().IsAppendData = true;
            col.GetData().Formula = "(( [Integer] [ID] = this.[ID], [String] [Product Code] = [Product Code], [String] [Custom Product Name] = [Product Name], [Double] [List Price] = [List Price], [Double] [Constant Column] = 20.02 ))"; // Tuple structure corresponds to output table
            col.GetData().IsAppendData = true;
            col.GetData().IsAppendSchema = true;

            productsTable.GetData().Populate();

            Assert.AreEqual(45, productsTable.GetData().Length);
            Assert.AreEqual("Northwind Traders Dried Pears", p3.GetData().GetValue(5));
            Assert.AreEqual(20.02, p5.GetData().GetValue(5));
        }

        [TestMethod]
        public void CsvWriteTest() // Store schema and data to a CSV file as a result of evaluation
        {
            DcSpace space = new Space();
            DcSchema schema = space.CreateSchema("My Schema", DcSchemaKind.Dc);
            CoreTest.CreateSampleSchema(schema);

            CoreTest.CreateSampleData(schema);

            DcTable t2 = schema.GetSubTable("Table 2");

            DcColumn c21 = t2.GetColumn("Column 21");
            DcColumn c22 = t2.GetColumn("Column 22");
            DcColumn c23 = t2.GetColumn("Column 23");

            //
            // Create schema for a remote db
            //
            SchemaCsv top = (SchemaCsv)space.CreateSchema("My Files", DcSchemaKind.Csv);

            // Create a remote file description
            TableCsv table = (TableCsv)space.CreateTable("Table_1", top.Root);
            table.FilePath = CsvWrite;

            // Manually create column to be imported (we need an automatic mechanism for appending missing columns specified in the formula)
            DcColumn p1 = space.CreateColumn("Column 11", table, top.GetPrimitive("String"), true);
            DcColumn p2 = space.CreateColumn("Column 12", table, top.GetPrimitive("String"), true);
            DcColumn p3 = space.CreateColumn("Custom Column 13", table, top.GetPrimitive("String"), true);
            DcColumn p4 = space.CreateColumn("Constant Column", table, top.GetPrimitive("String"), true);

            // Define export column
            DcColumn col = space.CreateColumn("Export", schema.GetSubTable("Table 1"), table, false);
            col.GetData().IsAppendData = true;
            col.GetData().Formula = "(( [String] [Column 11] = this.[Column 11], [String] [Column 12] = [Column 12], [String] [Custom Column 13] = [Column 13], [String] [Constant Column] = 20.02 ))"; // Tuple structure corresponds to output table
            col.GetData().IsAppendData = true;
            col.GetData().IsAppendSchema = true;

            table.Populate();
        }

    }
}
