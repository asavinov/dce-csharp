using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

using Com.Model;

using Offset = System.Int32;

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
            AstNode n = new AstNode(AstRule.SCRIPT);

            // Find all statements and store them in the script
            int stmtCount = context.statement().Count();
            for (int i = 0; i < stmtCount; i++)
            {
                AstNode stmt = (AstNode)Visit(context.statement(i));
                if (stmt != null)
                {
                    n.AddChild(stmt);
                }
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
                if (expr != null)
                {
                    n.AddChild(expr);
                }
            }
            else if (context.GetChild(0).GetText() == ";")
            {
                return null; // Skip
            }
            else if (context.GetChild(0) is ScriptParser.SexprContext)
            {
                n.Rule = AstRule.SEXPR;
                AstNode expr = (AstNode)Visit(context.GetChild(0));
                if (expr != null)
                {
                    n.AddChild(expr);
                }
            }
            else if (context.ID(0) != null && context.ID(1) != null)
            {
                n.Rule = AstRule.ALLOC;

                AstNode type = new AstNode(context.ID(0).GetText());
                if (type != null)
                {
                    n.AddChild(type);
                }

                AstNode name = new AstNode(context.ID(1).GetText());
                if (name != null)
                {
                    n.AddChild(name);
                }

                if(context.GetChild(2).GetText() == "=") 
                {
                    AstNode expr = (AstNode)Visit(context.GetChild(3));
                    if (expr != null)
                    {
                        n.AddChild(expr);
                    }
                }
            }
            else if (context.ID() != null && context.GetChild(1).GetText() == "=")
            {
                n.Rule = AstRule.ASSIGNMENT;

                AstNode name = new AstNode(context.ID(0).GetText());
                if (name != null)
                {
                    n.AddChild(name);
                }

                AstNode expr = (AstNode)Visit(context.GetChild(2));
                if (expr != null)
                {
                    n.AddChild(expr);
                }
            }

            return n; 
        }

        public override AstNode VisitSexpr(ScriptParser.SexprContext context) 
        {
            AstNode n = new AstNode();

            // Determine the type of expression

            if (context.GetChild(0) is ScriptParser.SexprContext)
            {
                string op = context.op.Text;
                if (op == ".")
                {
                    n.Rule = AstRule.DOT;
                }
                else if (op == "->")
                {
                    n.Rule = AstRule.PROJECTION;
                }
                else if (op == "<-")
                {
                    n.Rule = AstRule.DEPROJECTION;
                }

                AstNode expr = (AstNode)Visit(context.sexpr());
                if (expr != null)
                {
                    n.AddChild(expr);
                }

                AstNode call = (AstNode)Visit(context.GetChild(2));
                if (call != null)
                {
                    n.AddChild(call);
                }
            }
            else if (context.GetChild(0).GetText() == "SET")
            {
                n.Rule = AstRule.PRODUCT;

                // Find all members and store them in the product node
                int mmbrCount = context.member().Count();
                for (int i = 0; i < mmbrCount; i++)
                {
                    AstNode mmbr = (AstNode)Visit(context.member(i));
                    if (mmbr != null)
                    {
                        n.AddChild(mmbr);
                    }
                }
            }
            else if (context.GetChild(0) is ScriptParser.CallContext) 
            {
                AstNode call = (AstNode)Visit(context.GetChild(0));
                n = call;
            }

            return n; 
        }

        public override AstNode VisitVexpr(ScriptParser.VexprContext context) 
        { 
            AstNode n = new AstNode();

            // Determine the type of expression

            if (context.op != null)
            {
                string op = context.op.Text; // Alternatively, context.GetChild(1).GetText()

                n.Name = op;
                if (op == ".") n.Rule = AstRule.DOT;

                else if (op == "*") n.Rule = AstRule.MUL;
                else if (op == "/") n.Rule = AstRule.DIV;
                else if (op == "+") n.Rule = AstRule.ADD;
                else if (op == "-") n.Rule = AstRule.SUB;

                else if (op == "<=") n.Rule = AstRule.LEQ;
                else if (op == ">=") n.Rule = AstRule.GEQ;
                else if (op == ">") n.Rule = AstRule.GRE;
                else if (op == "<") n.Rule = AstRule.LES;

                else if (op == "==") n.Rule = AstRule.EQ;
                else if (op == "!=") n.Rule = AstRule.NEQ;

                else if (op == "&&") n.Rule = AstRule.AND;
                else if (op == "||") n.Rule = AstRule.OR;

                else ; // ERROR: unknown operation

                AstNode expr1 = (AstNode)Visit(context.GetChild(0));
                if (expr1 != null)
                {
                    n.AddChild(expr1);
                }

                AstNode expr2 = (AstNode)Visit(context.GetChild(2));
                if (expr2 != null)
                {
                    n.AddChild(expr2);
                }
            }
            else if (context.GetChild(0).GetText() == "TUPLE")
            {
                n.Rule = AstRule.TUPLE;

                // Find all members and store them in the tuple node
                int mmbrCount = context.member().Count();
                for (int i = 0; i < mmbrCount; i++)
                {
                    AstNode mmbr = (AstNode)Visit(context.member(i));
                    if (mmbr != null)
                    {
                        n.AddChild(mmbr);
                    }
                }
            }
            else if (context.literal() != null)
            {
                n.Rule = AstRule.LITERAL;
                string name = context.literal().GetText();

                if (context.literal().INT() != null)
                {
                }
                else if (context.literal().DECIMAL() != null)
                {
                }
                else if (context.literal().STRING() != null)
                {
                    name = name.Substring(1, name.Length - 2); // Remove quotes
                }
                n.Name = name;
            }
            else if (context.GetChild(0) is ScriptParser.CallContext)
            {
                AstNode call = (AstNode)Visit(context.GetChild(0));
                n = call;
            }

            return n; 
        }

        public override AstNode VisitCall(ScriptParser.CallContext context)
        {
            AstNode n = new AstNode(AstRule.CALL);

            AstNode func = null;
            if (context.name() != null) // Call by-reference (by-name)
            {
                func = (AstNode)Visit(context.name());
            }
            else if (context.vscope() != null) // Call by-value (using in-place definition)
            {
                func = (AstNode)Visit(context.vscope());
            }
            if (func != null)
            {
                n.AddChild(func);
            }

            // Find all parameters and add them as nodes
            int paramCount = context.param().Count();
            for (int i = 0; i < paramCount; i++)
            {
                AstNode param = (AstNode)Visit(context.param(i));
                if (param != null)
                {
                    n.AddChild(param);
                }
            }

            return n;
        }

        public override AstNode VisitParam(ScriptParser.ParamContext context)
        {
            AstNode n = new AstNode(AstRule.PARAM);

            // Name of the parameter
            AstNode name = null;
            if (context.name() != null)
            {
                name = (AstNode)Visit(context.name());
            }
            if (name != null)
            {
                n.AddChild(name);
            }

            // Definition as a function. It can be one value expression or a complete function definition (body as a sequence of statements)
            AstNode val = null;
            if (context.vexpr() != null)
            {
                val = (AstNode)Visit(context.vexpr());
            }
            else if (context.vscope() != null)
            {
                val = (AstNode)Visit(context.vscope());
            }
            if (val != null)
            {
                n.AddChild(val);
            }
            
            return n;
        }

        public override AstNode VisitMember(ScriptParser.MemberContext context)
        {
            AstNode n = new AstNode(AstRule.MEMBER);

            // Type of the member
            AstNode type = (AstNode)Visit(context.type());
            if (type != null)
            {
                n.AddChild(type);
            }

            // Name of the member (attribute, field etc.)
            AstNode name = (AstNode)Visit(context.name());
            if (name != null)
            {
                n.AddChild(name);
            }

            // Definition as a function. It can be one value expression or a complete function definition (body as a sequence of statements)
            AstNode val = null;
            if (context.vexpr() != null)
            {
                val = (AstNode)Visit(context.vexpr());
            }
            else if (context.vscope() != null)
            {
                val = (AstNode)Visit(context.vscope());
            }
            if (val != null)
            {
                n.AddChild(val);
            }

            return n;
        }

        public override AstNode VisitVscope(ScriptParser.VscopeContext context)
        {
            AstNode n = new AstNode(AstRule.VSCOPE);

            // Find all statements and store them in the script
            int stmtCount = context.vexpr().Count();
            for (int i = 0; i < stmtCount; i++)
            {
                AstNode stmt = (AstNode)Visit(context.vexpr(i));
                if (stmt != null)
                {
                    n.AddChild(stmt);
                }
            }

            return n;
        }

        public override AstNode VisitName(ScriptParser.NameContext context) 
        {
            AstNode n = new AstNode(AstRule.NAME);

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
            AstNode n = new AstNode(AstRule.TYPE);

            if (context.GetChild(0) is ScriptParser.SexprContext)
            {
                AstNode sexpr = (AstNode)Visit(context.sexpr());
                if (sexpr != null)
                {
                    n.AddChild(sexpr);
                }
            }
            else if (context.GetChild(0) is ScriptParser.Prim_setContext)
            {
                AstNode prim = new AstNode(context.prim_set().GetText());
                if (prim != null)
                {
                    n.AddChild(prim);
                }
            }

            return (AstNode)n.Children[0]; // We do not use TYPE node as a parent node - this role is defined by the position
//            return n; // TYPE node include one child with the type spec
        }

    }
}
