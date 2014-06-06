using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using DFA = Antlr4.Runtime.Dfa.DFA;

using Com.Model;
using Com.Query;

namespace Test
{
    [TestClass]
    public class CoelTest
    {
        protected Expression BuildExpr(string str)
        {
            ExprLexer lexer;
            ExprParser parser;
            IParseTree tree;
            string tree_str;
            Expression ast;

            ExpressionBuilder builder = new ExpressionBuilder();

            lexer = new ExprLexer(new AntlrInputStream(str));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

            return ast;
        }

        protected Expression BuildFunction(string str)
        {
            ExprLexer lexer;
            ExprParser parser;
            IParseTree tree;
            string tree_str;
            Expression ast;

            ExpressionBuilder builder = new ExpressionBuilder();

            lexer = new ExprLexer(new AntlrInputStream(str));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.function();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

            return ast;
        }

        protected AstNode BuildScript(string str)
        {
            ScriptLexer lexer;
            ScriptParser parser;
            IParseTree tree;
            string tree_str;
            AstNode ast;

            lexer = new ScriptLexer(new AntlrInputStream(str));
            parser = new ScriptParser(new CommonTokenStream(lexer));
            tree = parser.script();
            tree_str = tree.ToStringTree(parser);

            ScriptBuilder builder = new ScriptBuilder();
            ast = builder.Visit(tree);

            return ast;
        }

        [TestMethod]
        public void ExpressionBuilderTest()
        {
            Expression ast;

            //
            // Test expressions with errors. 
            //
            string errorStr = "aaa(p1,p2) bbb()";

            ExprLexer lexer = new ExprLexer(new AntlrInputStream(errorStr));
            ExprParser parser = new ExprParser(new CommonTokenStream(lexer));
            IParseTree tree = parser.expr();

            var exception = ((ParserRuleContext)tree.GetChild(0)).exception;
            Assert.AreEqual(exception.GetType().Name, "InputMismatchException");
            Assert.AreEqual(exception.OffendingToken.Text, "bbb");

            //
            // Test all primitive values (literals) within an arithmetic expressions
            //
            string literalStr = "1 + 2 * 2.2 - (\"three three\" / 33.3)";

            ast = BuildExpr(literalStr);

            Assert.AreEqual(ast.Operation, Operation.SUB);
            Assert.AreEqual(ast.Operands[0].Operation, Operation.ADD);
            Assert.AreEqual(ast.Operands[1].Operation, Operation.DIV);
            Assert.AreEqual(ast.Operands[0].Operands[1].Operation, Operation.MUL);

            Assert.AreEqual(ast.Operands[1].Operands[0].Output, "three three");
            Assert.AreEqual(ast.Operands[1].Operands[1].Output, 33.3);
            Assert.AreEqual(ast.Operands[0].Operands[1].Operands[1].Output, 2.2);
            Assert.AreEqual(ast.Operands[0].Operands[0].Output, 1);

            //
            // Test logical operations of comparision and equaliy
            //
            string logicalStr = "1 <= 2 * 2.2 || (\"three three\" / 33.3) < 44 && 10 > 20";

            ast = BuildExpr(logicalStr);

            Assert.AreEqual(ast.Operation, Operation.OR);
            Assert.AreEqual(ast.Operands[0].Operation, Operation.LEQ);
            Assert.AreEqual(ast.Operands[1].Operation, Operation.AND);
            Assert.AreEqual(ast.Operands[1].Operands[1].Operation, Operation.GRE);

            //
            // Add simple access (function, variables, fields etc.)
            //
            string accessStr = "aaa(p1,p2) + bbb() * [bbb BBB] - ([ccc CCC](p1*p2) / ddd(p1()+p2(), p3()))";

            ast = BuildExpr(accessStr);

            Assert.AreEqual(ast.Operation, Operation.SUB);

            Assert.AreEqual(ast.Operands[0].Operation, Operation.ADD);
            Assert.AreEqual(ast.Operands[0].Operands[0].Name, "aaa");
            Assert.AreEqual(ast.Operands[0].Operands[0].Operands[0].Name, "p1");
            Assert.AreEqual(ast.Operands[0].Operands[0].Operands[1].Name, "p2");
            Assert.AreEqual(ast.Operands[0].Operands[1].Operation, Operation.MUL);
            Assert.AreEqual(ast.Operands[0].Operands[1].Operands[0].Name, "bbb");
            Assert.AreEqual(ast.Operands[0].Operands[1].Operands[1].Name, "bbb BBB");

            //
            // Add complex access (dot, projection, de-projection paths with priority/scopes)
            //
            string accessPathStr = "aaa(p1,p2) . bbb() <- [bbb BBB] + [ccc CCC](this.p1.p2()) -> ddd(p1()<-p2()->p3()) -> eee";

            ast = BuildExpr(accessPathStr);

            Assert.AreEqual(ast.Operation, Operation.ADD);

            Assert.AreEqual(ast.Operands[0].Operation, Operation.DEPROJECTION);
            Assert.AreEqual(ast.Operands[0].Name, "bbb BBB");
            Assert.AreEqual(ast.Operands[0].Input.Operation, Operation.DOT);
            Assert.AreEqual(ast.Operands[0].Input.Name, "bbb");
            Assert.AreEqual(ast.Operands[0].Input.Input.Name, "aaa");

            Assert.AreEqual(ast.Operands[1].Operation, Operation.PROJECTION);
            Assert.AreEqual(ast.Operands[1].Name, "eee");
            Assert.AreEqual(ast.Operands[1].Input.Operation, Operation.PROJECTION);
            Assert.AreEqual(ast.Operands[1].Input.Name, "ddd");
            Assert.AreEqual(ast.Operands[1].Input.Input.Name, "ccc CCC");

            Assert.AreEqual(ast.Operands[1].Input.Input.Operands[0].Name, "p2");
            Assert.AreEqual(ast.Operands[1].Input.Input.Operands[0].Input.Name, "p1");
            Assert.AreEqual(ast.Operands[1].Input.Input.Operands[0].Input.Input.Name, "this");

            Assert.AreEqual(ast.Operands[1].Input.Operands[0].Name, "p3");
            Assert.AreEqual(ast.Operands[1].Input.Operands[0].Input.Name, "p2");
            Assert.AreEqual(ast.Operands[1].Input.Operands[0].Input.Input.Name, "p1");
        }

        [TestMethod]
        public void FunctionExpressionTest()
        {
            Expression ast;

            //
            // Test function expression parsing
            //
            string functionStr = "Double [My Function]([My Set] this, [Integer] [param 2]) { return a + this.b(); } ";

            ast = BuildFunction(functionStr);

            Assert.AreEqual(ast.Name, "My Function");
            Assert.AreEqual(ast.OutputSetName, "Double");

            Assert.AreEqual(ast.Input.Name, "this");
            Assert.AreEqual(ast.Input.OutputSetName, "My Set");

            Assert.AreEqual(ast.Operands[0].Name, "param 2");
            Assert.AreEqual(ast.Operands[0].OutputSetName, "Integer");

            Assert.AreEqual(((ExpressionScope)ast).Statements.Count, 1);
            Assert.AreEqual(((ExpressionScope)ast).Statements[0].Operation, Operation.RETURN);
            Assert.AreEqual(((ExpressionScope)ast).Statements[0].Input.Operation, Operation.ADD);
        }

        [TestMethod]
        public void SymbolResolutionTest()
        {
            ExpressionScope ast;

            //
            // Test schema. "Set A" -> b/c -> "Set B" -> d/i -> "Double"/"Integer"
            //
            SetTop top = new SetTop("Top");
            Set setInteger = top.GetPrimitiveSubset("Integer");
            Set setDouble = top.GetPrimitiveSubset("Double");

            Set setA = new Set("Set A");
            top.Root.AddSubset(setA);

            Set setB = new Set("Set B");
            top.Root.AddSubset(setB);

            Dim dimB = setB.CreateDefaultLesserDimension("b", setA);
            dimB.Add();

            Dim dimC = setB.CreateDefaultLesserDimension("Function c", setA);
            dimC.Add();

            Dim dimD = setDouble.CreateDefaultLesserDimension("d", setB);
            dimD.Add();

            Dim dimI = setInteger.CreateDefaultLesserDimension("i", setB);
            dimI.Add();

            //
            // Test symbol resolution, type resolution and validation
            //
            string functionStr = "[Set B] [Function c]([Set A] this, [Integer] [param 2], [Set B] [param 3]) { return b().d() * this.b().d() + [param 2] / [param 3] <- b(); } ";

            ast = (ExpressionScope) BuildFunction(functionStr);

            // Bind ast to the real schema
            ast.ResolveFunction(top);

            Assert.AreEqual(ast.Input.OutputSet, setA);
            Assert.AreEqual(ast.Operands[0].OutputSet, setInteger);
            Assert.AreEqual(ast.Operands[1].OutputSet, setB);

            Assert.AreEqual(ast.OutputSet, setB);
            Assert.AreEqual(ast.Dimension, dimC);

            // Resolve all symboles used in the function definition
            ast.Resolve();

            Assert.AreEqual(ast.Statements[0].Input.Operands[0].Operands[0].OutputSet, setDouble);
            Assert.AreEqual(ast.Statements[0].Input.Operands[0].Operands[0].Input.OutputSet, setB);
            Assert.AreEqual(ast.Statements[0].Input.Operands[0].Operands[0].Input.Input.OutputSet, setA);

            Assert.AreEqual(ast.Statements[0].Input.Operands[0].Operands[1].Input.Input.OutputSet, setA);

            Assert.AreEqual(ast.Statements[0].Input.Operands[1].Operands[0].OutputSet, setInteger);

            Assert.AreEqual(ast.Statements[0].Input.Operands[1].Operands[1].OutputSet, setA);
            Assert.AreEqual(ast.Statements[0].Input.Operands[1].Operands[1].Input.OutputSet, setB);
        }

        [TestMethod]
        public void ScriptBuilderTest()
        {
            AstNode ast;

            //
            // Script with statements
            //
            string scriptStr = " SET( String strVar ); Set mySet; mySet = SET( String strVar, Double dblVar ); ; ";
            ast = BuildScript(scriptStr);

            Assert.AreEqual(ast.Children.Count, 3);
            Assert.AreEqual(ast.Rule, AstRule.SCRIPT);

            Assert.AreEqual(ast.GetChild(0).Rule, AstRule.SEXPR);
            Assert.AreEqual(ast.GetChild(1).Rule, AstRule.ALLOC);
            Assert.AreEqual(ast.GetChild(2).Rule, AstRule.ASSIGNMENT);

            //
            // Types as set expressions
            //
            string exprStr = " mySet1 = startSet -> func1 -> func2; SET( SET(mySet1 var1, String var2) -> func2 varWithComputedType, Double doubleVar ); ";
            ast = BuildScript(exprStr);

            Assert.AreEqual(ast.Children.Count, 2);

            Assert.AreEqual(ast.GetChild(0).GetChild(1).Rule, AstRule.PROJECTION);
            Assert.AreEqual(ast.GetChild(1).GetChild(0).GetChild(0).GetChild(0).Rule, AstRule.PROJECTION);
            Assert.AreEqual(ast.GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(0).Rule, AstRule.PRODUCT);

            //
            // Function definition via value expressions or function body
            //
            string funcStr = " SET( Integer primFunc = doubleVar + f1.{f2+f3;}, MySet tupleFunc = TUPLE( Integer att1=f1.f2, MySet att2=TUPLE(Double aaa=primFunc+25, Integer bbb=f3+f4 ) ) ); ";
            ast = BuildScript(funcStr);

            Assert.AreEqual(ast.GetChild(0).GetChild(0).GetChild(0).GetChild(2).Name, "+");
            Assert.AreEqual(ast.GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(1).GetChild(1).Rule, AstRule.CALL);
            Assert.AreEqual(ast.GetChild(0).GetChild(0).GetChild(1).GetChild(2).Rule, AstRule.TUPLE);
            Assert.AreEqual(ast.GetChild(0).GetChild(0).GetChild(1).GetChild(2).GetChild(0).GetChild(1).Name, "att1");


            //
            // Test commands and procedures
            //
            string commandStr = " SCHEMA mySchema = OpenOledbCsv(param1=\"FileName\", param2=25.5); Set mySet = mySchema.Load(\"MyTable\"); mySet.Eval(); mySchema.Store(set=mySet, \"NewTable\"); ";
            ast = BuildScript(commandStr);

            Assert.AreEqual(ast.GetChild(0).GetChild(2).Children.Count, 3);
            Assert.AreEqual(ast.GetChild(0).GetChild(2).GetChild(0).Name, "OpenOledbCsv");
            Assert.AreEqual(ast.GetChild(0).GetChild(2).GetChild(1).GetChild(0).Name, "param1");
            Assert.AreEqual(ast.GetChild(0).GetChild(2).GetChild(1).GetChild(1).Name, "FileName");
        }

        [TestMethod]
        public void ScriptExecutionTest()
        {
            AstNode ast;
            ScriptContext script = new ScriptContext();

            //
            // 1. LOAD tables from external data sources
            //
            string loadStr = @" 
Connection myConnection; 
myConnection = OpenOledb(connection=""Provider=Microsoft.ACE.OLEDB.12.0; Data Source=C:\Users\savinov\git\comcsharp\Test; Extended Properties='Text;Excel 12.0;HDR=Yes;FMT=CSVDelimited;'"", param1=someVar); 
Set mySet; 
mySet = myConnection.Load(table=""Products#csv""); 
";
            ast = BuildScript(loadStr);
            script.Generate(ast);
            script.Execute();

            Assert.AreEqual(((SetTop)script.GetVariable("myConnection").Value).Name, "Test");
            Assert.AreEqual(((Set)script.GetVariable("mySet").Value).Name, "Products#csv");
            Assert.AreEqual(((Set)script.GetVariable("mySet").Value).GreaterDims.Count, 14);

            //
            // 2. AddFunction - add a function to a set (Later: DelFunction, UpdateFunction etc.)
            //
            // What about flags: key, super, etc.
            // In general, we may have numerous parameters. Maybe use key-value pairs like JSON and parse JSON? At least, this could be used in a special argument named 'parameters'.
            string functionStr = @" mySet.AddFunction( name = ""MyFunc"", type = ""Double"", formula = { [List Price] * [Standard Cost] + 100.0; } ); ";
            ast = BuildScript(functionStr);
            script.ClearChildren();
            script.Generate(ast);
            script.Execute();

            Assert.AreEqual(((Set)script.GetVariable("mySet").Value).GreaterDims.Count, 15);

            //
            // 2. PRODUCT - define a new set as a product of existing sets
            //
            string productStr = @" Set mySet; mySet = SET( String Name = ""MySet"", Root Super, String f1, Double f2 = {f1+100.0;}, Boolean Where = {f2>0;} ); ";
            ast = BuildScript(productStr);
            script.ClearChildren();
            //script.Generate(ast);
            //script.Execute();




            //
            // 4. Eval calls - for a set, for a function (do not use dependencies)
            //


            //
            // 5. Store data to an external data source
            //
            
        }

        [TestMethod]
        public void ConceptScriptTest()
        {
            ConceptScript cs = new ConceptScript("My Script");

            CsTable t = cs.CreateTable("My Table");

            //
            // Create connection
            // Load one table only using a universal API method. 
            // Types are chosen automatically. Keys are read if available (but not FKs). In future we could use selected attributes, mapping, conversion, filters and other options.
            // Table update (Eval) has to load data again by using the correct set populatino specification. 
            //

            //
            // Define a new product set using a custom super set and filter. Defining a simple subset with predicate. 
            //

            //
            // Define a relationship between tables (projection function) using tuple as output
            // It is a new column between two tables with a definition as a return tuple corresponding to the output set structure
            // ( Products product = (Integer ID = this.[Product ID]) )
            // The function is populated by finding tuples in the output set (null if not found). Output set is not changed.
            //

            //
            // Define a relationship between tables (projection function) using a predicate (join)
            // It is a function that returns true if input and output are mapped
            //

            //
            // Extract a new set via projection. API operation could return a new set (like a constructor).
            // - Define a new empty set manually. Define a projection function between two existing sets as a tuple. Populate the function (append if not found). The set is populated as a result of function population.
            // ( Categories category = (String [Category] = this.[Product Category]) )
            // - Use projection operation which analyzes the output tuple of the function, create a new set and configures it via projection function.
            // [Products] -> ( Categories category = (String [Category] = this.[Product Category]) )
            //

            //
            // Project by creating a subset. Here we create a subset by using an existing function to an existing super-set.
            // [Order Details] -> [Order ID]
            //

            //
            // Defining a multidimensional grouping function with several paths (tuple) to an existing set.
            // ( CategoryRegion group = (Category cat = this.f1.f2, Region reg = this.f3.f4) )
            // Filtering of facts and intermediate elements?
            // The same as extraction but with existing set (extraction creates a new set according to the function output tuple).

            //
            // Aggregated function. Special case with separate set of parameters:
            // Fact set, Grouping function, Aggregation function, Counting function, Update (aggregation) function (in future, can be custom), Initialization function/value.
            //
        
            // ======================================================================
            // Our strategy: 1. define sets and columns 2. evaluate sets and columns
            // Evaluation strategy:
            // - depends on how the set/function has been defined (which pattern has been used)
            // - use dependencies to build a sequence and evaluate individual sets/functions independently according to this sequence -> evaluation functions do not check dependencies, they assume that all needed functions have been computed
            // - strategíes of function population: - get function formula object (evaluator), - get function storage object, - get necessary set objects for looping
            //   - loop on input set (direct evaluation), 
            //   - loop on output set (inverse evaluation), 
            //   - loop on input-output sets (join), 
            //   - loop on input of projection function (set population via import/copy), 
            //   - loop on the fact set (aggregated function)
            // - strategies of set population: - generation loop, - generate a new tuple, - append the tuple, - evaluate all other functions of this set (needed for filter), - evaluate the filter, - remove tuple if false
            //   - loop on all key output sets surrogates by building a tuple, evaluating filter, append if the tuple is true
            //   - loop on the input set of projection function, get return tuple, evaluating filter, append if the tuple is true
            //   - request data from external set, loop on the output data set, get return record/tuple, evaluate filter, append if the tuple is true
            // - QUESTION: is it true that set population means populating all its functinos simultaniously?

            // Function definition and population patterns:
            // - function predicate is normal set predicate (no any difference), optional component, determines where output is null before real evaluation. is used in combination with other function definitions. if not null, then the function is really computed.
            // - primitve, arithmetic including surrogates
            // - complex, tuple -> only this can be used for copying (set population), essentially a combination of primitive functions
            // - join, predicate on intput, output. Is evaluated in a loop for all pairs (not in one loop for all inputs)
            // - aggregation -> normally primitive output
            // - inverse, determine input for the given output -> loop on the output set -> QUESTION: single-valued or also multi-valued? If multi-valued then how to represent, as function storage or certain (special - only surrogates) type with surrogates?
            //   - inverse can return surrogate or tuple
            //
            // Set definition and population patterns. How dependencies are represented? For each new element, evaluate the set predicate.
            // - all possible identities (filter is applied as usual)
            //   - subset is a particular case or a separate pattern? Or an automatically detected separate pattern, say, with storage optimization?
            // - outputs from a (lesser) function (filter is applied as usual). Can be viewed as loading/copying data -> to which columns?
            //   -> QUESTION: Which functions are set and which are not? Require identity tuple be present in?
            // - loading external set. Is also viewed as copying via projection. 
            //   - QUESTION: is the representation the same as for normal projection? Output format can be different (rows). Function specification can be different. 
        }

    }
}
