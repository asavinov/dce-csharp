using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using DFA = Antlr4.Runtime.Dfa.DFA;

using Com.Query;

namespace Test
{
    [TestClass]
    public class CoelTest
    {
        [TestMethod]
        public void ExprTest()
        {
            AntlrInputStream input = new AntlrInputStream("1 + 2");
            ExprLexer lexer = new ExprLexer(input);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            ExprParser parser = new ExprParser(tokens);
            IParseTree tree = parser.init_expr();
            string t = tree.ToStringTree(parser);
            Console.WriteLine(t);
        }

    }
}
