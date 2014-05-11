﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Com.Model;

namespace Com.Query
{
    /// <summary>
    /// A visitor which build an abstract syntax tree from a parse tree.
    /// Its methods are called for the nodes of a parse tree and return a node of AST.
    /// 
    /// An AST node represents one (abstract) syntactic rule without any unnecessary syntactic constructs like delimiters and tokens.
    /// </summary>
    public class ScriptBuilder : ScriptBaseVisitor<AstNode>
    {

        public override AstNode VisitScript(ScriptParser.ScriptContext context) 
        {
            AstNode n = new AstNode();
            n.Rule = AstRule.SCRIPT;

            // Find all statements and store them in the script
            int stmtCount = context.statement().Count();
            for (int i = 0; i < stmtCount; i++)
            {
                AstNode stmt = (AstNode)Visit(context.statement(i));
                if (stmt == null) continue;

                n.AddChild(stmt);
            }

            return n; 
        }

        public override AstNode VisitStatement(ScriptParser.StatementContext context) 
        {
            AstNode n = new AstNode();

            if (context.GetChild(0).GetText() == "RETURN")
            {
                n.Rule = AstRule.RETURN;
                AstNode expr = (AstNode)Visit(context.GetChild(1));
                n.AddChild(expr);
            }
            else if (context.GetChild(0).GetText() == ";")
            {
                return null; // Skip
            }
            else if (context.GetChild(0) is ScriptParser.SexprContext)
            {
                n.Rule = AstRule.SEXPR;
                AstNode expr = (AstNode)Visit(context.GetChild(0));
                n.AddChild(expr);
            }
            else if (context.GetChild(0).GetText() == "SET")
            {
                n.Rule = AstRule.VARIABLE;
                AstNode type = new AstNode("Set");
                AstNode name = new AstNode(context.GetChild(1).GetText());

                n.AddChild(type);
                n.AddChild(name);
            }
            else if (context.GetChild(0) is ScriptParser.NameContext && context.GetChild(1).GetText() == "=")
            {
                n.Rule = AstRule.ASSIGNMENT;
                AstNode name = new AstNode(context.GetChild(0).GetText());
                AstNode expr = new AstNode(context.GetChild(2).GetText());

                n.AddChild(name);
                n.AddChild(expr);
            }

            return n; 
        }

        public virtual AstNode VisitSexpr(ScriptParser.SexprContext context) 
        {
            AstNode n = new AstNode();
            n.Rule = AstRule.SEXPR;

            return n; 
        }

        protected string GetType(ScriptParser.TypeContext context)
        {
            string name = context.GetText();
            if (context.DELIMITED_ID() != null)
            {
                name = name.Substring(1, name.Length - 2); // Remove delimiters
            }
            return name;
        }

        protected string GetName(ScriptParser.NameContext context)
        {
            string name = context.GetText();
            if (context.DELIMITED_ID() != null)
            {
                name = name.Substring(1, name.Length - 2); // Remove delimiters
            }
            return name;
        }

    }
}
