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
    /// </summary>
    public class ExpressionBuilder : ExprBaseVisitor<Expression>
    {
        public override Expression VisitInit_expr(ExprParser.Init_exprContext context)
        {
            return Visit(context.expression()); // Skip this rule
        }

        //
        // Expression
        //

        public override Expression VisitAccessPath(ExprParser.AccessPathContext context) 
        {
            var aaa = context.expression(); // Prefix, input
            var bbb = context.access(); // Suffix, Dimension, returned from access or build new?

            Expression e = new Expression("");

            if (context.op.Text == ".")
            {
                e.Operation = Operation.DOT;
            }
            else if (context.op.Text == "<-")
            {
                e.Operation = Operation.DEPROJECTION;
            }
            else if (context.op.Text == "->")
            {
                e.Operation = Operation.PROJECTION;
            }

            return VisitChildren(context); 
        }

        public override Expression VisitMulDiv(ExprParser.MulDivContext context)
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));
            Expression e = null;

            if (context.op.Type == ExprParser.MUL)
            {
                e = new Expression(context.op.Text, Operation.TIMES);
            }
            else if (context.op.Type == ExprParser.DIV)
            {
                e = new Expression(context.op.Text, Operation.DIVIDE);
            }

            e.AddOperand(left);
            e.AddOperand(right);

            return e;
        }

        public override Expression VisitAddSub(ExprParser.AddSubContext context) 
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));
            Expression e = null;

            if (context.op.Type == ExprParser.ADD)
            {
                e = new Expression(context.op.Text, Operation.PLUS);
            }
            else if (context.op.Type == ExprParser.SUB)
            {
                e = new Expression(context.op.Text, Operation.MINUS);
            }

            e.AddOperand(left);
            e.AddOperand(right);

            return e; 
        }

        //
        // Primary
        //

        public override Expression VisitLiteralRule(ExprParser.LiteralRuleContext context) 
        {
            // The primitive value has to be stored in the Output field so we need to do coversion
            // Conversion: http://stackoverflow.com/questions/1354924/how-do-i-parse-a-string-with-a-decimal-point-to-a-double
            // TODO: Also, we need to set the type of expression in OutputSet, so here we need to do type mapping
            string textValue = context.GetText();
            object value = null;

            if (context.literal().INT() != null)
            {
                value = int.Parse(textValue, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture);
            }
            else if (context.literal().DECIMAL() != null) 
            {
                value = double.Parse(textValue, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture);
            }
            else if (context.literal().STRING() != null)
            {
                value = textValue.Substring(1, textValue.Length - 2); // Remove quotes
            }

            Expression e = new Expression(textValue, Operation.PRIMITIVE);
            e.Output = value;

            return e; 
        }

        public override Expression VisitAccessRule(ExprParser.AccessRuleContext context) 
        {
            // It is the very first method in access path without input (this)

            string name = null;
            if (context.access().ID() != null)
            {
                name = context.access().ID().GetText();
            }
            else if (context.access().DELIMITED_ID() != null) 
            {
                name = context.access().DELIMITED_ID().GetText();
                name = name.Substring(1, name.Length - 2); // Remove delimiters
            }

            Expression e = new Expression(name, Operation.DOT);

            // Find all arguments and store them as operands of the expression
            int argCount = context.access().arguments() != null ? context.access().arguments().expression().Count() : 0;
            for (int i = 0; i < argCount; i++)
            {
                Expression argExpr = Visit(context.access().arguments().expression(i));
                e.AddOperand(argExpr);
            }

            return e; 
        }

        public override Expression VisitParens(ExprParser.ParensContext context)
        {
            return Visit(context.expression()); // Skip this rule
        }

        //
        // Access
        //

    }
}
