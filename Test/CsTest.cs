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
            schema.AddTable(t2, schema.Root, null);

            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetPrimitive("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetPrimitive("Integer"), true);
            c22.Add();
            CsColumn c23 = schema.CreateColumn("Column 23", t2, schema.GetPrimitive("Double"), false);
            c23.Add();
            CsColumn c24 = schema.CreateColumn("Table 1", t2, t1, false);
            c24.Add();

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
            CsColumn c24 = t2.GetGreaterDim("Table 1");

            cols = new CsColumn[] { c21, c22, c23, c24 };
            vals = new object[4];

            vals[0] = "Value A";
            vals[1] = 20;
            vals[2] = 40.0;
            vals[3] = 0;
            t2.TableData.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 40.0;
            vals[3] = 1;
            t2.TableData.Append(cols, vals);

            vals[0] = "Value A";
            vals[1] = 30;
            vals[2] = 50.0;
            vals[3] = 1;
            t2.TableData.Append(cols, vals);

            vals[0] = "Value B";
            vals[1] = 30;
            vals[2] = 50.0;
            vals[3] = 1;
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
            Assert.AreEqual(schema.Root.GetTable("Table 2"), t2);

            Assert.AreEqual(t1.GetGreaterDim("Column 11").Name, "Column 11");
            Assert.AreEqual(t2.GetGreaterDim("Column 21").Name, "Column 21");

            Assert.AreEqual(t2.GetGreaterDim("Super").IsSuper, true);
            Assert.AreEqual(t2.SuperDim.LesserSet, t2);
            Assert.AreEqual(t2.SuperDim.GreaterSet, schema.Root);
        }

        [TestMethod]
        public void ColumnDataTest() // CsColumnData. Manually read/write data
        {
            //
            // Prepare schema
            //
            CsSchema schema = PrepareSampleSchema();

            CsTable t1 = schema.FindTable("Table 1");

            CsColumn c11 = t1.GetGreaterDim("Column 11");
            CsColumn c12 = t1.GetGreaterDim("Column 12");
            CsColumn c13 = t1.GetGreaterDim("Column 13");

            CsTable t2 = schema.FindTable("Table 2");
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

            Assert.AreEqual(10, c22.ColumnData.GetValue(0));
            Assert.AreEqual(20, c22.ColumnData.GetValue(1));
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

            // Define simple expression
            c15.ColumnDefinition.Formula = BuildExpr("([Column 11]+10.0) * this.[Column 13]"); // ConceptScript source code: "[Decimal] [Column 15] <body of expression>"

            // Evaluate column
            c15.ColumnDefinition.Evaluate();

            Assert.AreEqual(50.0, c15.ColumnData.GetValue(0));
            Assert.AreEqual(30.0, c15.ColumnData.GetValue(1));
            Assert.AreEqual(70.0, c15.ColumnData.GetValue(2));
        }

        [TestMethod]
        public void AggregationTest() // Defining new aggregated columns and evaluate them
        {
            //
            // Prepare schema and fill data
            //
            CsSchema schema = PrepareSampleSchema();
            PrepareSampleData(schema);

            CsTable t1 = schema.FindTable("Table 1");

            CsTable t2 = schema.FindTable("Table 2");

            CsColumn c23 = t2.GetGreaterDim("Column 23");
            CsColumn c24 = t2.GetGreaterDim("Table 1");

            //
            // Define aggregated column
            //
            CsColumn c15 = schema.CreateColumn("Agg of Column 23", t1, schema.GetPrimitive("Double"), false);
            c15.Add();

            c15.ColumnDefinition.Formula = ExprNode.CreateUpdater(c15, ActionType.ADD); // Update expression
            c15.ColumnDefinition.FactTable = t2; // Fact table
            c15.ColumnDefinition.GroupFormula = (ExprNode)ExprNode.CreateReader(c24).Root; // Group expression
            c15.ColumnDefinition.MeasureFormula = (ExprNode)ExprNode.CreateReader(c23).Root; // Measure expression

            //
            // Evaluate expression
            //
            c15.ColumnDefinition.Evaluate(); // {40, 140, 0}

            Assert.AreEqual(40.0, c15.ColumnData.GetValue(0));
            Assert.AreEqual(140.0, c15.ColumnData.GetValue(1));
            Assert.AreEqual(0.0, c15.ColumnData.GetValue(2)); // In fact, it has to be NaN or null (no values have been aggregated)

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
        }

        [TestMethod]
        public void TableProductTest() // Define a new table and populate it
        {
            CsSchema schema = PrepareSampleSchema();
            PrepareSampleData(schema);

            CsTable t1 = schema.FindTable("Table 1");
            CsTable t2 = schema.FindTable("Table 2");

            //
            // Define a new product-set
            //
            CsTable t3 = schema.CreateTable("Table 3");
            schema.AddTable(t3, null, null);

            CsColumn c31 = schema.CreateColumn(t1.Name, t3, t1, true); // {*20, 10, *30}
            c31.Add();
            CsColumn c32 = schema.CreateColumn(t2.Name, t3, t2, true); // {40, 40, *50, *50}
            c32.Add();

            t3.TableDefinition.Populate();
            Assert.AreEqual(12, t3.TableData.Length);

            //
            // Add simple where expression
            //

            ExprNode ast = BuildExpr("([Table 1].[Column 11] > 10) && this.[Table 2].[Column 23] == 50.0");
            t3.TableDefinition.WhereExpression = ast;

            t3.TableDefinition.Populate();
            Assert.AreEqual(4, t3.TableData.Length);

            Assert.AreEqual(0, c31.ColumnData.GetValue(0));
            Assert.AreEqual(2, c32.ColumnData.GetValue(0));

            Assert.AreEqual(0, c31.ColumnData.GetValue(1));
            Assert.AreEqual(3, c32.ColumnData.GetValue(1));
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
            CsTable t3 = schema.CreateTable("Table 3");
            schema.AddTable(t3, null, null);

            CsColumn c31 = schema.CreateColumn(c21.Name, t3, c21.GreaterSet, true);
            c31.Add();

            // Manually define a mapping as the generating column definition by using one column only. And create a generating dimension with this mapping.
            Mapping map24 = new Mapping(t2, t3);
            map24.AddMatch(new PathMatch(new DimPath(c21), new DimPath(c31)));

            // !!! TODO: we cannot use directly a constructor - replace by schema API like schema.CreateColumn(c21.Name, t3, c21.GreaterSet, true)
            CsColumn c24 = new Dim(map24); // Create generating/import column
            c24.Add();

            t3.TableDefinition.Populate();
            Assert.AreEqual(2, t3.TableData.Length);

            Assert.AreEqual(0, c24.ColumnData.GetValue(0));
            Assert.AreEqual(0, c24.ColumnData.GetValue(1));
            Assert.AreEqual(0, c24.ColumnData.GetValue(2));
            Assert.AreEqual(1, c24.ColumnData.GetValue(3));

            //
            // Defining a combination of "Column 21" and "Column 22" and project with 3 unique records in a new set
            //
            CsTable t4 = schema.CreateTable("Table 4");
            schema.AddTable(t4, null, null);

            CsColumn c41 = schema.CreateColumn(c21.Name, t4, c21.GreaterSet, true);
            c41.Add();
            CsColumn c42 = schema.CreateColumn(c22.Name, t4, c22.GreaterSet, true);
            c42.Add();

            // Manually define a mapping as the generating column definition by using one column only. And create a generating dimension with this mapping.
            Mapping map25 = new Mapping(t2, t4);
            map25.AddMatch(new PathMatch(new DimPath(c21), new DimPath(c41)));
            map25.AddMatch(new PathMatch(new DimPath(c22), new DimPath(c42)));

            // !!! TODO: we cannot use directly a constructor - replace by schema API like schema.CreateColumn(c21.Name, t3, c21.GreaterSet, true)
            CsColumn c25 = new Dim(map25); // Create generating/import column
            c25.Add();

            t4.TableDefinition.Populate();
            Assert.AreEqual(3, t4.TableData.Length);

            Assert.AreEqual(0, c25.ColumnData.GetValue(0));
            Assert.AreEqual(1, c25.ColumnData.GetValue(1));
            Assert.AreEqual(1, c25.ColumnData.GetValue(2));
            Assert.AreEqual(2, c25.ColumnData.GetValue(3));
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

    }
}
