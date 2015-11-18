using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Schema.Rel;

using Offset = System.Int32;

namespace Test
{
    //[TestClass]
    public class OledbTest
    {
        public static string Northwind = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\dce-csharp\\Test\\Northwind.accdb";
        public static string TextDbConnection = "Provider=Microsoft.ACE.OLEDB.12.0; Data Source=C:\\Users\\savinov\\git\\dce-csharp\\Test; Extended Properties='Text;Excel 12.0;HDR=Yes;FMT=CSVDelimited;'";

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
        public void OledbReadTest() // Load Oledb schema and data
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

            DcSpace space = new Space();

            // Db

            SchemaOledb top = (SchemaOledb)space.CreateSchema("", DcSchemaKind.Oledb);
            top.connection = conn;

            //
            // Load schema
            //
            /*
            top.LoadSchema();

            Assert.AreEqual(20, top.Root.SubTables.Count);

            Assert.AreEqual(11, top.GetSubTable("Order Details").Columns.Count);
            Assert.AreEqual("Orders", top.GetSubTable("Order Details").GetColumn("Order ID").Output.Name);

            // Load data manually
            System.Data.DataTable dataTable = top.LoadTable((TableRel)top.GetSubTable("Order Details"));
            Assert.AreEqual(58, dataTable.Rows.Count);
            Assert.AreEqual(37, dataTable.Rows[10][2]);
            */

            //
            // Configure import 
            //
            /*
            DcSchema schema = space.CreateSchema("My Schema", DcSchemaKind.Dc);

            DcTable orderDetailsTable = space.CreateTable("Order Details", schema.Root);
            
            // Create mapping
            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(top.GetSubTable("Order Details"), orderDetailsTable, schema);
            map.Matches.ForEach(m => m.TargetPath.Segments.ForEach(p => p.Add()));

            // Create generating/import column
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

    }
}
