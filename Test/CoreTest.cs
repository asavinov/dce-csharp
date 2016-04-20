using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Com.Utils;
using Com.Schema;
using Com.Schema.Rel;
using Com.Schema.Csv;
using Com.Data;
using Com.Data.Query;

using Offset = System.Int32;

// Unit test: http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class CoreTest
    {
        public static ExprBuilder ExprBuilder { get; set; }

        private static TestContext context;

        #region Initialisation and cleanup

        [ClassInitialize()]
        public static void SetUpClass(TestContext testContext)
        {
            context = testContext; 
            ExprBuilder = new ExprBuilder();
        }
        
        DcSpace space { get; set; }
        DcSchema schema { get; set; }

        [TestInitialize()]
        public void SetUp() {
            space = new Space();
            schema = space.CreateSchema("My Schema", DcSchemaKind.Dc);
            CreateSampleSchema(schema);
        }
    
        public static void CreateSampleSchema(DcSchema schema)
        {
            DcSpace space = schema.Space;

            // Table 1
            DcTable t1 = space.CreateTable("Table 1", schema.Root);

            DcColumn c11 = space.CreateColumn("Column 11", t1, schema.GetPrimitiveType("Integer"), true);
            DcColumn c12 = space.CreateColumn("Column 12", t1, schema.GetPrimitiveType("String"), true);
            DcColumn c13 = space.CreateColumn("Column 13", t1, schema.GetPrimitiveType("Double"), false);
            DcColumn c14 = space.CreateColumn("Column 14", t1, schema.GetPrimitiveType("Decimal"), false);

            // Table 2
            DcTable t2 = space.CreateTable("Table 2", schema.Root);

            DcColumn c21 = space.CreateColumn("Column 21", t2, schema.GetPrimitiveType("String"), true);
            DcColumn c22 = space.CreateColumn("Column 22", t2, schema.GetPrimitiveType("Integer"), true);
            DcColumn c23 = space.CreateColumn("Column 23", t2, schema.GetPrimitiveType("Double"), false);
            DcColumn c24 = space.CreateColumn("Table 1", t2, t1, false);
        }

        public static void CreateSampleData(DcSchema schema)
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
            DcTableWriter w1 = t1.GetData().GetTableWriter();

            vals[0] = 20;
            vals[1] = "Record 0";
            vals[2] = 20.0;
            vals[3] = 20.0;
            w1.Append(cols, vals);

            vals[0] = 10;
            vals[1] = "Record 1";
            vals[2] = 10.0;
            vals[3] = 20.0;
            w1.Append(cols, vals);

            vals[0] = 30;
            vals[1] = "Record 2";
            vals[2] = 30.0;
            vals[3] = 20.0;
            w1.Append(cols, vals);

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
            DcTableWriter w2 = t2.GetData().GetTableWriter();

            vals[0] = "Value A";
            vals[1] = 20;
            vals[2] = 40.0;
            vals[3] = 0;
            w2.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 40.0;
            vals[3] = 1;
            w2.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 50.0;
            vals[3] = 1;
            w2.Append(cols, vals);

            vals[0] = "Value B";
            vals[1] = 30;
            vals[2] = 50.0;
            vals[3] = 1;
            w2.Append(cols, vals);
        }

        #endregion

        [TestMethod]
        public void SchemaTest() // ComColumn. Manually add/remove tables/columns
        {
            DcTable t1 = schema.GetSubTable("Table 1");
            DcTable t2 = schema.GetSubTable("Table 2");

            // Finding by name and check various properties provided by the schema
            Assert.AreEqual(schema.GetPrimitiveType("Decimal").Name, "Decimal");

            Assert.AreEqual(t1.Name, "Table 1");
            Assert.AreEqual(t2.Name, "Table 2");
            Assert.AreEqual(schema.GetSubTable("Table 2"), t2);

            Assert.AreEqual(t1.GetColumn("Column 11").Name, "Column 11");
            Assert.AreEqual(t2.GetColumn("Column 21").Name, "Column 21");

            Assert.AreEqual(t2.GetColumn("Super").IsSuper, true);
            Assert.AreEqual(t2.SuperColumn.Input, t2);
            Assert.AreEqual(t2.SuperColumn.Output, schema.Root);

            // Test path enumerator
            var pathEnum = new PathEnumerator(t2, t1, ColumnType.IDENTITY_ENTITY);
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

            t1.GetData().Length = 3;

            // 2. Write/read individual column data by using column data methods (not table methods)

            Assert.AreEqual(true, c11.GetData().IsNull(1)); // Initially, all outputs must be null
            c11.GetData().SetValue(1, 10);
            c11.GetData().SetValue(0, 20);
            c11.GetData().SetValue(2, 30);
            Assert.AreEqual(false, c11.GetData().IsNull(1));
            Assert.AreEqual(10, c11.GetData().GetValue(1));

            Assert.AreEqual(true, c13.GetData().IsNull(2)); // Initially, all outputs must be null
            c13.GetData().SetValue(1, 10.0);
            c13.GetData().SetValue(0, 20.0);
            c13.GetData().SetValue(2, 30.0);
            Assert.AreEqual(false, c13.GetData().IsNull(1));
            Assert.AreEqual(10.0, c13.GetData().GetValue(1));

            t2.GetData().Length = 2;

            c21.GetData().SetValue(0, "Value A");
            c21.GetData().SetValue(1, "Value B");

            c22.GetData().SetValue(0, 10);
            c22.GetData().SetValue(1, 20);

            Assert.AreEqual(10, c22.GetData().GetValue(0));
            Assert.AreEqual(20, c22.GetData().GetValue(1));
        }

        [TestMethod]
        public void TableDataTest() // ComTableData. Manually read/write data to/from tables
        {
            CreateSampleData(schema);

            DcTable t1 = schema.GetSubTable("Table 1");

            DcColumn c11 = t1.GetColumn("Column 11");
            DcColumn c12 = t1.GetColumn("Column 12");
            DcColumn c13 = t1.GetColumn("Column 13");

            DcTableWriter w1 = t1.GetData().GetTableWriter();

            //
            // Data manipulations
            //
            Assert.AreEqual(3, t1.GetData().Length);

            Offset input = w1.Find(new DcColumn[] { c11 }, new object[] { 10 } );
            Assert.AreEqual(1, input);

            input = w1.Find(new DcColumn[] { c12 }, new object[] { "Record 1" });
            Assert.AreEqual(1, input);

            input = w1.Find(new DcColumn[] { c12 }, new object[] { "Record Does Not Exist" });
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
            DcColumn c15 = space.CreateColumn("Column 15", t1, schema.GetPrimitiveType("Double"), false);

            c15.GetData().Formula = "([Column 11]+10.0) * this.[Column 13]";

            // Evaluate column
            c15.GetData().Evaluate();

            Assert.AreEqual(600.0, c15.GetData().GetValue(0));
            Assert.AreEqual(200.0, c15.GetData().GetValue(1));
            Assert.AreEqual(1200.0, c15.GetData().GetValue(2));
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
            DcColumn c15 = space.CreateColumn("Column 15", t1, schema.GetPrimitiveType("String"), false);

            c15.GetData().Formula = "call:System.String.Substring( [Column 12], 7, 1 )";

            // Evaluate column
            c15.GetData().Evaluate();

            Assert.AreEqual("0", c15.GetData().GetValue(0));
            Assert.AreEqual("1", c15.GetData().GetValue(1));
            Assert.AreEqual("2", c15.GetData().GetValue(2));

            //
            // Define a derived column with a definition
            //
            DcColumn c16 = space.CreateColumn("Column 15", t1, schema.GetPrimitiveType("Double"), false);

            c16.GetData().Formula = "call:System.Math.Pow( [Column 11] / 10.0, [Column 13] / 10.0 )";

            c16.GetData().Evaluate();

            Assert.AreEqual(4.0, c16.GetData().GetValue(0));
            Assert.AreEqual(1.0, c16.GetData().GetValue(1));
            Assert.AreEqual(27.0, c16.GetData().GetValue(2));
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

            DcColumn link = space.CreateColumn("Column Link", t2, t1, false);

            link.GetData().Formula = "(( [Integer] [Column 11] = this.[Column 22], [Double] [Column 14] = 20.0 ))"; // Tuple structure corresponds to output table

            // Evaluate column
            link.GetData().Evaluate();

            Assert.AreEqual(0, link.GetData().GetValue(0));
            Assert.AreEqual(2, link.GetData().GetValue(1));
            Assert.AreEqual(2, link.GetData().GetValue(2));
            Assert.AreEqual(2, link.GetData().GetValue(3));
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
            DcColumn c16 = space.CreateColumn("Agg2 of Column 23", t1, schema.GetPrimitiveType("Double"), false);

            c16.GetData().Formula = "AGGREGATE(facts=[Table 2], groups=[Table 1], measure=[Column 23]*2.0 + 1, aggregator=SUM)";

            c16.GetData().SetValue(0.0);
            c16.GetData().Translate();
            c16.GetData().Evaluate(); // {40, 140, 0}

            Assert.AreEqual(81.0, c16.GetData().GetValue(0));
            Assert.AreEqual(283.0, c16.GetData().GetValue(1));
            Assert.AreEqual(0.0, c16.GetData().GetValue(2));
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
            DcTable t3 = space.CreateTable("Table 3", schema.Root);

            DcColumn c31 = space.CreateColumn(t1.Name, t3, t1, true); // {*20, 10, *30}
            DcColumn c32 = space.CreateColumn(t2.Name, t3, t2, true); // {40, 40, *50, *50}

            t3.GetData().Populate();
            Assert.AreEqual(12, t3.GetData().Length);

            //
            // Add simple where expression
            //

            t3.GetData().WhereFormula = "([Table 1].[Column 11] > 10) && this.[Table 2].[Column 23] == 50.0";

            t3.GetData().Populate();
            Assert.AreEqual(4, t3.GetData().Length);

            Assert.AreEqual(0, c31.GetData().GetValue(0));
            Assert.AreEqual(2, c32.GetData().GetValue(0));

            Assert.AreEqual(0, c31.GetData().GetValue(1));
            Assert.AreEqual(3, c32.GetData().GetValue(1));
        }

        [TestMethod]
        public void TableSubsetTest() // Define a filter to get a subset of record from one table
        {
            CreateSampleData(schema);

            DcTable t2 = schema.GetSubTable("Table 2");

            //
            // Define a new filter-set
            //
            DcTable t3 = space.CreateTable("Table 3", t2);
            t3.GetData().WhereFormula = "[Column 22] > 20.0 && this.Super.[Column 23] < 50";

            t3.GetData().Populate();
            Assert.AreEqual(1, t3.GetData().Length);
            Assert.AreEqual(1, t3.SuperColumn.GetData().GetValue(0));
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
            DcTable t3 = space.CreateTable("Table 3", schema.Root);

            DcColumn c31 = space.CreateColumn(c21.Name, t3, c21.Output, true);

            // Create a generating column
            DcColumn c24 = space.CreateColumn("Project", t2, t3, false);
            c24.GetData().Formula = "(( [String] [Column 21] = [Column 21] ))";
            c24.GetData().IsAppendData = true;

            t3.GetData().Populate();

            Assert.AreEqual(2, t3.GetData().Length);

            Assert.AreEqual(0, c24.GetData().GetValue(0));
            Assert.AreEqual(0, c24.GetData().GetValue(1));
            Assert.AreEqual(0, c24.GetData().GetValue(2));
            Assert.AreEqual(1, c24.GetData().GetValue(3));

            //
            // Defining a combination of "Column 21" and "Column 22" and project with 3 unique records in a new set
            //
            DcTable t4 = space.CreateTable("Table 4", schema.Root);

            DcColumn c41 = space.CreateColumn(c21.Name, t4, c21.Output, true);
            DcColumn c42 = space.CreateColumn(c22.Name, t4, c22.Output, true);

            DcColumn c25 = space.CreateColumn("Project", t2, t4, false);
            c25.GetData().Formula = "(( [String] [Column 21] = [Column 21], [Integer] [Column 22] = [Column 22] ))";
            c25.GetData().IsAppendData = true;

            t4.GetData().Populate();

            Assert.AreEqual(3, t4.GetData().Length);

            Assert.AreEqual(0, c25.GetData().GetValue(0));
            Assert.AreEqual(1, c25.GetData().GetValue(1));
            Assert.AreEqual(1, c25.GetData().GetValue(2));
            Assert.AreEqual(2, c25.GetData().GetValue(3));
        }

    }

}
