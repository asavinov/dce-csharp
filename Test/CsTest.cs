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

        [TestMethod]
        public void SchemaTest() // CsColumn. Manually add/remove tables/columns
        {
            // Schema
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

            // Table 2
            CsTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, t1, null);

            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetPrimitive("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetPrimitive("Double"), false);
            c22.Add();


            // Finding by name and check various properties provided by the schema
            Assert.AreEqual(schema.FindTable("Table 1"), t1);
            Assert.AreEqual(schema.FindTable("Table 2"), t2);
            Assert.AreEqual(t1.GetTable("Table 2"), t2);

            Assert.AreEqual(t1.GetGreaterDim("Column 11"), c11);
            Assert.AreEqual(t2.GetGreaterDim("Column 21"), c21);

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

            // Table 2

            CsTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, t1, null);

            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetPrimitive("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetPrimitive("Double"), false);
            c22.Add();

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

            c21.ColumnData.SetValue(0, "Value 1");
            c21.ColumnData.SetValue(1, "Value 2");

            c22.ColumnData.SetValue(0, 1.0);
            c22.ColumnData.SetValue(1, 2.0);

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
            CsSchema schema = new SetTop("My Schema");

            // Tables
            CsTable t1 = schema.CreateTable("Table 1");
            schema.AddTable(t1, schema.Root, null);

            // Columns
            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetPrimitive("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetPrimitive("String"), true);
            c12.Add();
            CsColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetPrimitive("Double"), false);
            c13.Add();

            //
            // Data manipulations
            //
            CsColumn[] cols = new CsColumn[] { c11, c12, c13 };
            object[] vals = new object[3];

            vals[0] = 20;
            vals[1] = "Record 0";
            vals[2] = 20.0;
            t1.TableData.Append(cols, vals);

            vals[0] = 10;
            vals[1] = "Record 1";
            vals[2] = 10.0;
            t1.TableData.Append(cols, vals);

            vals[0] = 30;
            vals[1] = "Record 2";
            vals[2] = 30.0;
            t1.TableData.Append(cols, vals);

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
            // Prepare schema
            //
            CsSchema schema = new SetTop("My Schema");

            // Tables
            CsTable t1 = schema.CreateTable("Table 1");
            schema.AddTable(t1, schema.Root, null);

            // Columns
            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetPrimitive("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetPrimitive("String"), true);
            c12.Add();
            CsColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetPrimitive("Double"), false);
            c13.Add();
            CsColumn c14 = schema.CreateColumn("Column 14", t1, schema.GetPrimitive("Decimal"), false);
            c14.Add();

            //
            // Fill sample data
            //
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

            // Load data
            DataTable dataTable = top.LoadTable((SetRel)top.FindTable("Order Details"));
            Assert.AreEqual(58, dataTable.Rows.Count);
            Assert.AreEqual(37, dataTable.Rows[10][2]);

            // Configure import 
            CsSchema schema = new SetTop("My Schema");
            Set orderDetailsTable = new Set("Order Details");
            schema.AddTable(orderDetailsTable, null, null);
            
            Mapper mapper = new Mapper();
            Mapping map = mapper.CreatePrimitive(top.FindTable("Order Details"), orderDetailsTable);
            CsColumn dim = new Dim(map);
            dim.Add();

            //dim.GreaterSet.TableDefinition.Populate();
            // TODO next: generate correct evaluator for: various dim defs (arithm, mapping etc.), various source set/input types (local, oledb Row, etc.)
            // - finish and test evaluator for arithmetic expressions by evaluating computed columns and generation of evaluator from expression
            // - generate evaluator from mapping: either directly or first tuple expression tree and then (standard) evaluator
            // - implement evaluator for remote (oledb) input where we have to iterate through another input type (Row object as input instead of Offset)

            // Evaluator:
            // - IsUpdate - type of change of the function output: accumulate or set. Essentially, whether it is aggregation evaluator.
            // - LoopTable - what set to iterate (fact set for aggregation)
            // - is supposed to be from Evaluate of Column to decide what to loop and how to update etc.

            // Set population:
            // - greater with filter (use key dims; de-projection of several greater dims by storing all combinations of their inputs)
            //   - use only Key greater dims for looping. 
            //   - user greater sets input values as constituents for new tuples
            //   - greater dims are populated simultaniously without evaluation (they do not have defs.)
            // - lesser with filter
            //   - use only IsGenerating lesser dims for looping. 
            //   - use thier lesser sets to loop through all their combinations
            //   - !!! each lesser dim is evaluated using its formula that returns some constituent of the new set (or a new element in the case of 1 lesser dim)
            //   - set elements store a combination of lesser dims outputs
            //   - each lesser dim stores the new tuple in its output (alternatively, these dims could be evaluatd after set population - will it really work for multiple lesser dims?)

            //   - so we should NOT use column Evaluator for populating a set 
            //      --> column evaluator of generating dims is never used from the colum population procedure (but can be if called explicitly) 
            //      --> all column evaluators NEVER change (influence) their sets (neither greater nor lesser) - it computes only this function

        }

    }
}
