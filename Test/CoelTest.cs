using System;
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

            Assert.AreEqual(ast.Children[0].Rule, AstRule.SEXPR);
            Assert.AreEqual(ast.Children[1].Rule, AstRule.ALLOC);
            Assert.AreEqual(ast.Children[2].Rule, AstRule.ASSIGNMENT);

            //
            // Types as set expressions
            //
            string exprStr = " mySet1 = startSet -> func1 -> func2; SET( SET(mySet1 var1, String var2) -> func2 varWithComputedType, Double doubleVar ); ";
            ast = BuildScript(exprStr);

            Assert.AreEqual(ast.Children.Count, 2);

            Assert.AreEqual(ast.Children[0].Children[1].Rule, AstRule.PROJECTION);
            Assert.AreEqual(ast.Children[1].Children[0].Children[0].Children[0].Rule, AstRule.PROJECTION);
            Assert.AreEqual(ast.Children[1].Children[0].Children[0].Children[0].Children[0].Rule, AstRule.PRODUCT);

            //
            // Function definition via value expressions or function body
            //
            string funcStr = " SET( Integer primFunc = doubleVar + f1.{f2+f3;}, MySet tupleFunc = TUPLE( Integer att1=f1.f2, MySet att2=TUPLE(Double aaa=primFunc+25, Integer bbb=f3+f4 ) ) ); ";
            ast = BuildScript(funcStr);

            Assert.AreEqual(ast.Children[0].Children[0].Children[0].Children[2].Name, "+");
            Assert.AreEqual(ast.Children[0].Children[0].Children[0].Children[2].Children[1].Children[1].Rule, AstRule.CALL);
            Assert.AreEqual(ast.Children[0].Children[0].Children[1].Children[2].Rule, AstRule.TUPLE);
            Assert.AreEqual(ast.Children[0].Children[0].Children[1].Children[2].Children[0].Children[1].Name, "att1");


            //
            // Test commands and procedures
            //
            string commandStr = " SCHEMA mySchema = OpenOledbCsv(param1=\"FileName\", param2=25.5); Set mySet = mySchema.Load(\"MyTable\"); mySet.Eval(); mySchema.Store(set=mySet, \"NewTable\"); ";
            ast = BuildScript(commandStr);

            Assert.AreEqual(ast.Children[0].Children[2].Children.Count, 3);
            Assert.AreEqual(ast.Children[0].Children[2].Children[0].Name, "OpenOledbCsv");
            Assert.AreEqual(ast.Children[0].Children[2].Children[1].Children[0].Name, "param1");
            Assert.AreEqual(ast.Children[0].Children[2].Children[1].Children[1].Name, "FileName");
        }

        [TestMethod]
        public void ScriptSchemaOperationsTest()
        {
            AstNode ast;

            string commandStr = @"Set mySet = SET( String Name = ""MySet"", Root Super, String f1, Double f2, Double f3 = f2+100.0 ); ";
            // It is equivalent to AddSet("MySet") followed by a sequence of AddFunction
            // Markups: super for super-function, key for identity dimension. 
            // If Name is not specified then some default (autoincremented) name like "MySet 1"

            ast = BuildScript(commandStr);

            // Translate and execute ast
            //Script script = new Script("Top"); // Create executable context

            //script.Execute(ast);

            // What do we need:
            // 1. Convert source string into script AST (AstNode) representing syntactic structure (what the user has written) with mixed script- and value-ops.
            // 2. Translate script AST (AstNode) to executable form (separately ScriptOp and ValueOp). Here we guarantee only executable operations and some optimization by defining/instantiating temporary functinos/sets/values/variables.
            // 3. Attach generated code to a ScriptContext for executation and resolve. For test purposes, we can attach function code to ValueContext and resolve.
            // 4. Evaluate script which will result in evaluation of functions by creating ValueContext for the function code. For test purposes, we can evaluate function code individually with dedicated ValueContext.

            // The most difficult part is translation where we create new intermediate functions and need to provide a definition for them.
            // Alternative: either we provide definition in source form as AstNode (and translate later) or in compiled form (ValueOp) directly. 

            // We can compile AstNode for functions into ValueOp and test separately in ValueContext.

            // While executing a set-op program, some instructions (Eval) result in creating a loop in which a val-op program is iteratively executed.

            // Problem. AstNode contains val-op function definitions which can be translated into one expression, a sequence of val-op, or some fragments could be extracted and translated into a new function (which replaces this fragment)
            // We can reserve a special field in Dim for storing a translated val-op program (optimized with use of intermediate functions)
            // This translated program is also bound to objects (dimensions and variables) and is ready for execution.
            // In this case, such a function definition cannot be executed only using schema - it might need temporary (extracted) dimensions. 
            // We could bind this translated/optimized definition to a context (script or another) which stores all the necessary objects. 
            // Another option is to store references to all used dimension objects (temporary or schema) in local context as variables and then use only these variables.

            commandStr = @"AddMember( ""f3"", mySet, Double, f2+100.0 ); ";
        }

    }
}
