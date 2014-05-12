using System;
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

            // Determine the type of statement

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
                AstNode name = (AstNode)Visit(context.GetChild(0));
                AstNode expr = (AstNode)Visit(context.GetChild(2));

                n.AddChild(name);
                n.AddChild(expr);
            }

            return n; 
        }

        public override AstNode VisitSexpr(ScriptParser.SexprContext context) 
        {
            AstNode n = new AstNode();

            // Determine the type of expression

            if (context.GetChild(0) is ScriptParser.SexprContext)
            {
                string op = context.op.Text; // Alternatively, context.GetChild(1).GetText()
                if(op == "." || op == "->") 
                {
                    n.Rule = AstRule.PROJECTION;
                }
                else if(op == "<-")
                {
                    n.Rule = AstRule.DEPROJECTION;
                }

                AstNode expr = (AstNode)Visit(context.sexpr());
                AstNode func = (AstNode)Visit(context.GetChild(2));

                n.AddChild(expr);
                n.AddChild(func);
            }
            else if (context.GetChild(0).GetText() == "SET")
            {
                n.Rule = AstRule.PRODUCT;

                // Find all members and store them in the product node
                int mmbrCount = context.member().Count();
                for (int i = 0; i < mmbrCount; i++)
                {
                    AstNode mmbr = (AstNode)Visit(context.member(i));
                    if (mmbr == null) continue;
                    n.AddChild(mmbr);
                }
            }
            if (context.GetChild(0) is ScriptParser.NameContext) 
            {
                AstNode name = (AstNode)Visit(context.GetChild(0));
                n = name;
            }

            return n; 
        }

        public override AstNode VisitMember(ScriptParser.MemberContext context) 
        {
            AstNode n = new AstNode();
            n.Rule = AstRule.MEMBER;

            AstNode type = (AstNode)Visit(context.type());
            n.AddChild(type);

            AstNode name = (AstNode)Visit(context.name());
            n.AddChild(name);

            return n;
        }

        public override AstNode VisitName(ScriptParser.NameContext context) 
        {
            AstNode n = new AstNode();
            n.Rule = AstRule.NAME;

            if (context.DELIMITED_ID() != null)
            {
                string name = context.DELIMITED_ID().GetText();
                n.Name = name.Substring(1, name.Length - 2); // Remove delimiters
            }
            else
            {
                n.Name = context.ID().GetText();
            }

            return n; 
        }

        public override AstNode VisitType(ScriptParser.TypeContext context) 
        {
            AstNode n = new AstNode();
            n.Rule = AstRule.TYPE;

            if (context.GetChild(0) is ScriptParser.SexprContext)
            {
                AstNode sexpr = (AstNode)Visit(context.sexpr());
                n.AddChild(sexpr);
            }
            if (context.GetChild(0) is ScriptParser.Primitive_setContext)
            {
                AstNode prim = new AstNode(context.primitive_set().GetText());
                n.AddChild(prim);
            }

            return n.Children[0]; // We do not use TYPE node - this role is defined by the position
        }

    }
}
