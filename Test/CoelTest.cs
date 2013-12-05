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
        [TestMethod]
        public void ExpressionBuilderTest()
        {
            ExprLexer lexer;
            ExprParser parser;
            IParseTree tree;
            string tree_str;
            Expression ast;

            ExpressionBuilder builder = new ExpressionBuilder();

            //
            // Test all primitive values (literals) within an arithmetic expressions
            //
            string literalStr = "1 + 2 * 2.2 - (\"three three\" / 33.3)";

            lexer = new ExprLexer(new AntlrInputStream(literalStr));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.init_expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

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

            lexer = new ExprLexer(new AntlrInputStream(logicalStr));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.init_expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

            Assert.AreEqual(ast.Operation, Operation.OR);
            Assert.AreEqual(ast.Operands[0].Operation, Operation.LEQ);
            Assert.AreEqual(ast.Operands[1].Operation, Operation.AND);
            Assert.AreEqual(ast.Operands[1].Operands[1].Operation, Operation.GRE);

            //
            // Add simple access (function, variables, fields etc.)
            //
            string accessStr = "aaa(p1,p2) + bbb() * [bbb BBB] - ([ccc CCC](p1*p2) / ddd(p1()+p2(), p3()))";

            lexer = new ExprLexer(new AntlrInputStream(accessStr));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.init_expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

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

            lexer = new ExprLexer(new AntlrInputStream(accessPathStr));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.init_expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

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

            //
            // Test expressions with errors. 
            //
            string errorStr = "aaa(p1,p2) bbb()";

            lexer = new ExprLexer(new AntlrInputStream(errorStr));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.init_expr();
            tree_str = tree.ToStringTree(parser);

            var exception = ((ParserRuleContext)tree.GetChild(0)).exception;
            Assert.AreEqual(exception.GetType().Name, "InputMismatchException");
            Assert.AreEqual(exception.OffendingToken.Text, "bbb");
        }

    }
}
