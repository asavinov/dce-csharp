using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Model;

using Offset = System.Int32;

namespace Com.Query
{
    /// <summary>
    /// A visitor which build an abstract syntax tree from a parse tree.
    /// Its methods are called for the nodes of a parse tree and return a node of AST.
    /// </summary>
    public class ExprBuilder : ExprBaseVisitor<ExprNode>
    {
        static bool accessAsThisNode = true; // Design alternative: access node can be represented either as a child or this node

        public override ExprNode VisitExpr(ExprParser.ExprContext context) 
        {
            ExprNode n = new ExprNode();

            // Determine the type of expression

            if (context.op != null && context.op.Text.Equals(".")) // Composition (dot) operation
            {
                n.Operation = OperationType.CALL;
                n.Action = ActionType.READ;

                ExprNode exprNode = Visit(context.expr(0));
                if (exprNode != null)
                {
                    n.AddChild(exprNode);
                }

                ExprNode accessNode = Visit(context.access());
                if (accessAsThisNode) // Represent accessor (after dot) by this node
                {
                    if (context.access().name() != null) // Name of the function
                    {
                        n.Name = accessNode.Name;
                    }
                    else // A definition of the function (lambda) is provided instead of name
                    {
                        context.access().scope();
                        n.Name = "lambda"; // Automatically generated name for a unnamed lambda
                    }
                }
                else // Access node as a child (it can be named either by real method name or as a special 'method' with the method described represented elsewhere)
                {
                    n.Name = ".";
                    if (accessNode != null)
                    {
                        n.AddChild(accessNode);
                    }
                }
            }
            else if (context.op != null) // Arithmetic operations
            {
                n.Operation = OperationType.CALL;
                string op = context.op.Text; // Alternatively, context.GetChild(1).GetText()
                n.Name = op;

                if (op.Equals("*")) n.Action = ActionType.MUL;
                else if (op.Equals("/")) n.Action = ActionType.DIV;
                else if (op.Equals("+")) n.Action = ActionType.ADD;
                else if (op.Equals("-")) n.Action = ActionType.SUB;

                else if (op.Equals("<=")) n.Action = ActionType.LEQ;
                else if (op.Equals(">=")) n.Action = ActionType.GEQ;
                else if (op.Equals(">")) n.Action = ActionType.GRE;
                else if (op.Equals("<")) n.Action = ActionType.LES;

                else if (op.Equals("==")) n.Action = ActionType.EQ;
                else if (op.Equals("!=")) n.Action = ActionType.NEQ;

                else if (op.Equals("&&")) n.Action = ActionType.AND;
                else if (op.Equals("||")) n.Action = ActionType.OR;

                else ;

                ExprNode expr1 = Visit(context.GetChild(0));
                if (expr1 != null)
                {
                    n.AddChild(expr1);
                }

                ExprNode expr2 = Visit(context.GetChild(2));
                if (expr2 != null)
                {
                    n.AddChild(expr2);
                }
            }
            else if (context.expr() != null && context.GetChild(0).GetText().Equals("(")) // Priority
            {
                n = Visit(context.expr(0)); // Skip
            }
            else if (context.GetChild(0).GetText().Equals("((") || context.GetChild(0).GetText().Equals("TUPLE")) // Tuple
            {
                n.Operation = OperationType.TUPLE;
                n.Action = ActionType.READ; // Find

                // Find all members and store them in the tuple node
                int mmbrCount = context.member().Count();
                for (int i = 0; i < mmbrCount; i++)
                {
                    ExprNode mmbr = Visit(context.member(i));
                    if (mmbr != null)
                    {
                        n.AddChild(mmbr);
                    }
                }
            }
            else if (context.literal() != null) // Literal
            {
                n.Operation = OperationType.VALUE;
                n.Action = ActionType.READ;

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
            else if (context.GetChild(0) is ExprParser.AccessContext) // Access/call
            {
                n.Operation = OperationType.CALL;
                n.Action = ActionType.READ;

                ExprNode accessNode = Visit(context.access());
                if (accessAsThisNode) // Represent accessor (after dot) by this node
                {
                    if (context.access().name() != null) // Name of the function
                    {
                        n.Name = accessNode.Name;
                    }
                    else // A definition of the function (lambda) is provided instead of name
                    {
                        context.access().scope();
                        n.Name = "lambda"; // Automatically generated name for a unnamed lambda
                    }
                }
                else // Access node as a child (it can be named either by real method name or as a special 'method' with the method described represented elsewhere)
                {
                    n.Name = ".";
                    if (accessNode != null)
                    {
                        n.AddChild(accessNode);
                    }
                }

                // TODO: Read parameters and create nodes for them: context.access().param();
            }

            return n; 
        }

        public override ExprNode VisitName(ExprParser.NameContext context) 
        {
            ExprNode n = new ExprNode();
            n.Operation = OperationType.VALUE;
            n.Action = ActionType.READ;

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

    }

}
