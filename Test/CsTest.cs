using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    public class CsTest
    {
        public static string Northwind = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test\\Northwind.accdb";
        public static string TextDbConnection = "Provider=Microsoft.ACE.OLEDB.12.0; Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test; Extended Properties='Text;Excel 12.0;HDR=Yes;FMT=CSVDelimited;'";

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

        protected CsSchema PrepareSampleSchema()
        {
            // Prepare schema
            CsSchema schema = new SetTop("My Schema");

            // Table 1
            CsTable t1 = schema.CreateTable("Table 1");
            schema.AddTable(t1, schema.Root, null);

            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetPrimitive("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetPrimitive("String"), true);
            c12.Add();
            CsColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetPrimitive("Double"), false);
            c13.Add();
            CsColumn c14 = schema.CreateColumn("Column 14", t1, schema.GetPrimitive("Decimal"), false);
            c14.Add();

            // Table 2
            CsTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, t1, null);

            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetPrimitive("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetPrimitive("Integer"), false);
            c22.Add();
            CsColumn c23 = schema.CreateColumn("Column 23", t2, schema.GetPrimitive("Double"), false);
            c23.Add();

            return schema;
        }

        protected void PrepareSampleData(CsSchema schema)
        {
            //
            // Fill sample data in "Table 1"
            //
            CsTable t1 = schema.FindTable("Table 1");

            CsColumn c11 = t1.GetGreaterDim("Column 11");
            CsColumn c12 = t1.GetGreaterDim("Column 12");
            CsColumn c13 = t1.GetGreaterDim("Column 13");
            CsColumn c14 = t1.GetGreaterDim("Column 14");

            CsColumn[] cols = new CsColumn[] { c11, c12, c13, c14 };
            object[] vals = new object[4];

            vals[0] = 20;
            vals[1] = "Record 0";
            vals[2] = 20.0;
            vals[3] = 20.0;
            t1.TableData.Append(cols, vals);

            vals[0] = 10;
            vals[1] = "Record 1";
            vals[2] = 10.0;
            vals[3] = 20.0;
            t1.TableData.Append(cols, vals);

            vals[0] = 30;
            vals[1] = "Record 2";
            vals[2] = 30.0;
            vals[3] = 20.0;
            t1.TableData.Append(cols, vals);

            //
            // Fill sample data in "Table 2"
            //
            CsTable t2 = schema.FindTable("Table 2");

            CsColumn c21 = t2.GetGreaterDim("Column 21");
            CsColumn c22 = t2.GetGreaterDim("Column 22");
            CsColumn c23 = t2.GetGreaterDim("Column 23");

            cols = new CsColumn[] { c21, c22, c23 };
            vals = new object[3];

            vals[0] = "Value A";
            vals[1] = 20;
            vals[2] = 40.0;
            t2.TableData.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 40.0;
            t2.TableData.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 50.0;
            t2.TableData.Append(cols, vals);

            vals[0] = "Value B";
            vals[1] = 30;
            vals[2] = 50.0;
            t2.TableData.Append(cols, vals);
        }

        [TestMethod]
        public void SchemaTest() // CsColumn. Manually add/remove tables/columns
        {
            //
            // Prepare schema
            //
            CsSchema schema = PrepareSampleSchema();

            CsTable t1 = schema.FindTable("Table 1");
            CsTable t2 = schema.FindTable("Table 2");

            // Finding by name and check various properties provided by the schema
            Assert.AreEqual(schema.GetPrimitive("Decimal").Name, "Decimal");

            Assert.AreEqual(t1.Name, "Table 1");
            Assert.AreEqual(t2.Name, "Table 2");
            Assert.AreEqual(t1.GetTable("Table 2"), t2);

            Assert.AreEqual(t1.GetGreaterDim("Column 11").Name, "Column 11");
            Assert.AreEqual(t2.GetGreaterDim("Column 21").Name, "Column 21");

            Assert.AreEqual(t2.GetGreaterDim("Super").IsSuper, true);
            Assert.AreEqual(t2.SuperDim.LesserSet, t2);
            Assert.AreEqual(t2.SuperDim.GreaterSet, t1);
        }

        [TestMethod]
        public void ColumnDataTest() // CsColumnData. Manually read/write data
        {
            //
            // Prepare schema
            //
            CsSchema schema = PrepareSampleSchema();

            CsTable t1 = schema.FindTable("Table 1");
            CsTable t2 = schema.FindTable("Table 2");

            CsColumn c11 = t1.GetGreaterDim("Column 11");
            CsColumn c12 = t1.GetGreaterDim("Column 12");
            CsColumn c13 = t1.GetGreaterDim("Column 13");

            CsColumn c21 = t2.GetGreaterDim("Column 21");
            CsColumn c22 = t2.GetGreaterDim("Column 22");

            //
            // Data
            //

            t1.TableData.Length = 3;

            // 2. Write/read individual column data by using column data methods (not table methods)

            Assert.AreEqual(true, c11.ColumnData.IsNull(1)); // Initially, all outputs must be null
            c11.ColumnData.SetValue(1, 10);
            c11.ColumnData.SetValue(0, 20);
            c11.ColumnData.SetValue(2, 30);
            Assert.AreEqual(false, c11.ColumnData.IsNull(1));
            Assert.AreEqual(10, c11.ColumnData.GetValue(1));

            Assert.AreEqual(true, c13.ColumnData.IsNull(2)); // Initially, all outputs must be null
            c13.ColumnData.SetValue(1, 10.0);
            c13.ColumnData.SetValue(0, 20.0);
            c13.ColumnData.SetValue(2, 30.0);
            Assert.AreEqual(false, c13.ColumnData.IsNull(1));
            Assert.AreEqual(10.0, c13.ColumnData.GetValue(1));

            t2.TableData.Length = 2;

            c21.ColumnData.SetValue(0, "Value A");
            c21.ColumnData.SetValue(1, "Value B");

            c22.ColumnData.SetValue(0, 10);
            c22.ColumnData.SetValue(1, 20);

            t2.SuperDim.ColumnData.SetValue(0, 1); // It is offset to the parent record
            t2.SuperDim.ColumnData.SetValue(1, 2);

            Assert.AreEqual(2, t2.SuperDim.ColumnData.GetValue(1));
        }

        [TestMethod]
        public void TableDataTest() // CsTableData. Manually read/write data to/from tables
        {
            //
            // Prepare schema
            //
            CsSchema schema = PrepareSampleSchema();
            PrepareSampleData(schema);

            CsTable t1 = schema.FindTable("Table 1");
            CsTable t2 = schema.FindTable("Table 2");

            CsColumn c11 = t1.GetGreaterDim("Column 11");
            CsColumn c12 = t1.GetGreaterDim("Column 12");
            CsColumn c13 = t1.GetGreaterDim("Column 13");

            //
            // Data manipulations
            //
            Assert.AreEqual(3, t1.TableData.Length);

            Offset input = t1.TableData.Find(new CsColumn[] { c11 }, new object[] { 10 } );
            Assert.AreEqual(1, input);

            input = t1.TableData.Find(new CsColumn[] { c12 }, new object[] { "Record 1" });
            Assert.AreEqual(1, input);

            input = t1.TableData.Find(new CsColumn[] { c12 }, new object[] { "Record Does Not Exist" });
            Assert.AreEqual(-1, input);
        }
        
        [TestMethod]
        public void ColumnDefinitionTest() // CsColumnDefinition. Defining new columns and evaluate them
        {
            //
            // Prepare schema and fill data
            //
            CsSchema schema = PrepareSampleSchema();
            PrepareSampleData(schema);

            CsTable t1 = schema.FindTable("Table 1");

            CsColumn c11 = t1.GetGreaterDim("Column 11");
            CsColumn c12 = t1.GetGreaterDim("Column 12");
            CsColumn c13 = t1.GetGreaterDim("Column 13");
            CsColumn c14 = t1.GetGreaterDim("Column 14");

            //
            // Define a derived column with a definition
            //
            CsColumn c15 = schema.CreateColumn("Column 15", t1, schema.GetPrimitive("Double"), false);
            c15.Add();

            // Simple expression: arithmetic operation, column access in leaves and constant value in a leaf.
            ExprNode ast = BuildExpr("([Column 11]+10.0) * this.[Column 13]"); // ConceptScript source code: "[Decimal] [Column 15] <body of expression>"
            c15.ColumnDefinition.Formula = ast;
            c15.ColumnDefinition.Evaluate(); // Evaluate the expression

            Assert.AreEqual(50.0, c15.ColumnData.GetValue(0));
            Assert.AreEqual(30.0, c15.ColumnData.GetValue(1));
            Assert.AreEqual(70.0, c15.ColumnData.GetValue(2));
        }

        [TestMethod]
        public void AggregationTest() // Defining new aggregated columns and evaluate them
        {
        }

        [TestMethod]
        public void TableDefinitionTest() // Define a new table and populate it
        {
        }

        [TestMethod]
        public void ProjectionTest() // Defining new tables via function projection and populate them
        {
            CsSchema schema = PrepareSampleSchema();
            PrepareSampleData(schema);

            CsTable t2 = schema.FindTable("Table 2");

            CsColumn c21 = t2.GetGreaterDim("Column 21");
            CsColumn c22 = t2.GetGreaterDim("Column 22");
            CsColumn c23 = t2.GetGreaterDim("Column 23");

            //
            // Project "Table 2" along "Column 21" and get 2 unique records in a new set "Value A" (3 references) and "Value B" (1 reference)
            //
            Set proj1 = new Set("Projection 1. Extraction of Column 21");
            schema.AddTable(proj1, null, null);

            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(schema.FindTable("Table 2"), proj1);
            map.Matches.ForEach(m => m.TargetPath.Path.ForEach(p => p.Add()));

            // Create generating/import column
            CsColumn col1 = new Dim(map);
            col1.Add();

            proj1.TableDefinition.Populate();
            //Assert.AreEqual(2, proj1.TableData.Length);

            //
            // Defining a combination of "Column 21" and "Column 22" and project with 3 unique records in a new set
            //
            Set proj2 = new Set("Projection 1. Extraction of Column 21 and Column 22");
            schema.AddTable(proj2, null, null);

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

            // Load schema
            top.LoadSchema();

            Assert.AreEqual(20, top.Root.SubSets.Count);
            Assert.AreEqual(11, top.FindTable("Order Details").GreaterDims.Count);

            Assert.AreEqual("Orders", top.FindTable("Order Details").GetGreaterDim("Order ID").GreaterSet.Name);

            // Load data manually
            DataTable dataTable = top.LoadTable((SetRel)top.FindTable("Order Details"));
            Assert.AreEqual(58, dataTable.Rows.Count);
            Assert.AreEqual(37, dataTable.Rows[10][2]);

            // Configure import 
            CsSchema schema = new SetTop("My Schema");
            Set orderDetailsTable = new Set("Order Details");
            schema.AddTable(orderDetailsTable, null, null);
            
            // Create mapping
            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(top.FindTable("Order Details"), orderDetailsTable);
            map.Matches.ForEach(m => m.TargetPath.Path.ForEach(p => p.Add()));

            // Create generating/import column
            CsColumn dim = new Dim(map);
            dim.Add();

            orderDetailsTable.TableDefinition.Populate();
            Assert.AreEqual(58, orderDetailsTable.TableData.Length);
            
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

            // Set::Populate
            // !!! Append an element to the generating/projection column(s) if an element has been appended/found in the target set. Alternatively, we will have to evaluate this generating dimension separately (double work).
            // - This means a principle: generating dimensions are not evaluated separately - they are evaluated during their target set population.
            // - For Oledb (import/export) dims it is not needed because these dimensions do not store data.

            // Dim population principles:
            // - currently we Append from the loop manually and not from the expression (as opposed ot Set::Populate) - it is bad: either all in Evaluate or all in Populate
            //   - so we should NOT use column Evaluator for populating a set 
            //      --> column evaluator of generating dims is never used from the colum population procedure (but can be if called explicitly) 
            //      --> all column evaluators NEVER change (influence) their sets (neither greater nor lesser) - it computes only this function

        }

    }
}
