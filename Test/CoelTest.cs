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

            Assert.AreEqual(ast.Operation, Operation.MINUS);
            Assert.AreEqual(ast.Operands[0].Operation, Operation.PLUS);
            Assert.AreEqual(ast.Operands[1].Operation, Operation.DIVIDE);
            Assert.AreEqual(ast.Operands[0].Operands[1].Operation, Operation.TIMES);

            Assert.AreEqual(ast.Operands[1].Operands[0].Output, "three three");
            Assert.AreEqual(ast.Operands[1].Operands[1].Output, 33.3);
            Assert.AreEqual(ast.Operands[0].Operands[1].Operands[1].Output, 2.2);
            Assert.AreEqual(ast.Operands[0].Operands[0].Output, 1);

            //
            // Add simple access (function, variables, fields etc.)
            //
            string accessStr = "aaa(p1,p2) + bbb() * [bbb BBB] - ([ccc CCC](p1*p2) / ddd(p1()+p2(), p3()))";

            lexer = new ExprLexer(new AntlrInputStream(accessStr));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.init_expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

            // TODO: Asserts
            // TODO: Find assert for internal built-in exceptions during parsing in Context object?

            //
            // Add complex access (dot, projection, de-projection paths with priority/scopes)
            //
            string accessPathStr = "aaa(p1,p2) . bbb() <- [bbb BBB] + [ccc CCC](this.p1.p2()) -> ddd(p1()<-p2()->p3()) -> eee";

            lexer = new ExprLexer(new AntlrInputStream(accessPathStr));
            parser = new ExprParser(new CommonTokenStream(lexer));
            tree = parser.init_expr();
            tree_str = tree.ToStringTree(parser);

            ast = builder.Visit(tree);

            // PrimaryRule - AccessRule - Access (id, Arguments[5])
        }

    }
}
