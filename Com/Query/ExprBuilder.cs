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
        public override ExprNode VisitExpr(ExprParser.ExprContext context) 
        {
            ExprNode n = new ExprNode();

            // Determine the type of expression

            if (context.op != null && context.op.Text == ".") // Composition (dot) operation
            {
                n.Operation = OperationType.CALL;
                n.Action = ActionType.READ;

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

                // TODO:
                // This node as a method is applied to the only child
                // OR: First child 'method' is applied to the second child 'this'
            }
            else if (context.op != null) // Arithmetic operations
            {
                n.Operation = OperationType.CALL;
                string op = context.op.Text; // Alternatively, context.GetChild(1).GetText()

                if (op == "*") n.Action = ActionType.MUL;
                else if (op == "/") n.Action = ActionType.DIV;
                else if (op == "+") n.Action = ActionType.ADD;
                else if (op == "-") n.Action = ActionType.SUB;

                else if (op == "<=") n.Action = ActionType.LEQ;
                else if (op == ">=") n.Action = ActionType.GEQ;
                else if (op == ">") n.Action = ActionType.GRE;
                else if (op == "<") n.Action = ActionType.LES;

                else if (op == "==") n.Action = ActionType.EQ;
                else if (op == "!=") n.Action = ActionType.NEQ;

                else if (op == "&&") n.Action = ActionType.AND;
                else if (op == "||") n.Action = ActionType.OR;

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
            else if (context.GetChild(0).GetText() == "TUPLE") // Tuple
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
                n = Visit(context.GetChild(0));
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
