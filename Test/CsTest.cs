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

            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetTable("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetTable("String"), true);
            c12.Add();
            CsColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetTable("Double"), false);
            c13.Add();

            // Table 2
            CsTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, t1, null);
            
            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetTable("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetTable("Double"), false);
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

            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetTable("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetTable("String"), true);
            c12.Add();
            CsColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetTable("Double"), false);
            c13.Add();

            // Table 2

            CsTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, t1, null);

            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetTable("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetTable("Double"), false);
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
            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetTable("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetTable("String"), true);
            c12.Add();
            CsColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetTable("Double"), false);
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
            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetTable("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetTable("String"), true);
            c12.Add();
            CsColumn c13 = schema.CreateColumn("Column 13", t1, schema.GetTable("Double"), false);
            c13.Add();
            CsColumn c14 = schema.CreateColumn("Column 14", t1, schema.GetTable("Decimal"), false);
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
            CsColumn c15 = schema.CreateColumn("Column 15", t1, schema.GetTable("Decimal"), false);
            c15.Add();
            // TODO: Check that the length is set to the table length during adding

            // Manually Expr to be used by Evaluator: new ExprNode()
            // ConceptScript source code: [Decimal] [Column 14] ([Column 11]+[Column 13]) * 10.0
            // Construct C# source code (to be compile to Evaluator)
            ExprNode ast = BuildExpr("[Column 11]+[Column 13] * 10.0");
            // Important to get a node with arithmetic operation, calls in leaves and constant value in a leaf.

            c15.ColumnDefinition.Formula = ast;

            // Type name = expr
            // Type name = aaa + bbb / 2
            // Type name = (Type name = 25.0, Type name = aaa-bbb, Type name = (T1 n1=10, T2 n2=...) )
            // Conclusion: tuple operator (either TUPLE() or simply ()) is a normal operator that can be met in expressions where we expect a value.
            // The difference is only in type: normal expressions return primitive values while a tuple returns a combination which belongs to some set, and this type (set) has to be somehow determined.
            // Exception: we can use tuple operator only in assignment to a member - not where primitive types are used. 
            // The main reason is that use in the context of assignment within a tuple provides type-name prefix, which is needed for the tuple. 
            // What is type-name prefixe is absent? For example, definition is: (T1 n1=..., T2 n2=...)
            // 1. It could be restored from the context (say, the set where this expression has been defined)
            //  Indeed, if we use primitive expression like "(c11+c13) * 10.0" then it also needs type with name like "Double f = (c11+c13) * 10.0" which is however absent but can be restored from context.
            // 2. Maybe it is not needed, say, it is anonymous function (name) and the set does not exist but is rather defined by the tuple itself.
            // It is important to understand that two definitions are equivalent:
            // Short: "(c11+c13) * 10.0" Full: "Double f = (c11+c13) * 10.0"
            // Short: "(T1 n1=..., T2 n2=...)" Full: "MySet f = (T1 n1=..., T2 n2=...)"

            // One solution is to introduce syntax for primitive expressions, where each part (subexpression) may have Type and Name information
            // Example: 2.0 -> Double myName=2.0
            // Example: a*(c+f1.f2/2.0))

            // Parentheses are used for:
            // Priority in expressions: a*(c+d)
            // Tuples: (m1, m2), alternatively, T() or (())


            //
            // Evaluate the column
            //

            //c15.ColumnDefinition.Formula.Resolve();
            //c15.ColumnDefinition.Evaluate();

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
            Assert.AreEqual("37", dataTable.Rows[10][2]);
        }

    }
}
