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
            string scriptStr = " SET( String strVar ); SET mySet; mySet = SET( String strVar, Double dblVar, Integer intVar = 25 + 26 ); ; ";

            ast = BuildScript(scriptStr);

            Assert.AreEqual(ast.Children.Count, 3);
            Assert.AreEqual(ast.Rule, AstRule.SCRIPT);

            Assert.AreEqual(ast.Children[0].Rule, AstRule.SEXPR);
            Assert.AreEqual(ast.Children[1].Rule, AstRule.VARIABLE);
            Assert.AreEqual(ast.Children[2].Rule, AstRule.ASSIGNMENT);
        }

    }
}
