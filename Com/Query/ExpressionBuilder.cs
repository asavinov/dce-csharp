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
    /// </summary>
    public class ExpressionBuilder : ExprBaseVisitor<Expression>
    {

        public override Expression VisitExpr(ExprParser.ExprContext context)
        {
            return Visit(context.expression()); // Skip this rule
        }

        //
        // Function
        //

        public override Expression VisitFunction(ExprParser.FunctionContext context) 
        {
            string type = GetType(context.type()); // Return type
            string name = GetName(context.name()); // Function name

            ExpressionScope e = new ExpressionScope();
            e.Name = name;
            e.Operation = Operation.FUNCTION;
            e.OutputSetName = type;

            // Find all parameter declarations and store them as operands of the expression
            int paramCount = context.parameter().Count();
            for (int i = 0; i < paramCount; i++)
            {
                Expression paramExpr = Visit(context.parameter(i));
                if (i == 0) e.Input = paramExpr;
                else e.AddOperand(paramExpr);
            }

            // Find all statements and store them in the expression
            int stmtCount = context.statement().Count();
            for (int i = 0; i < stmtCount; i++)
            {
                ExpressionScope stmtExpr = (ExpressionScope) Visit(context.statement(i));
                if (stmtExpr == null) continue;

                e.AddStatement(stmtExpr);
            }

            return e; 
        }

        public override Expression VisitParameter(ExprParser.ParameterContext context) 
        {
            string type = GetType(context.type()); // Parameter type
            string name = GetName(context.name()); // Parameter name

            Expression e = new Expression(name, Operation.PARAMETER);
            e.OutputSetName = type;

            return e; 
        }

        public override Expression VisitStatement(ExprParser.StatementContext context) 
        {
            if (context.GetChild(0).GetText() == "return")
            {
                ExpressionScope e = new ExpressionScope();
                e.Name = "return";
                e.Operation = Operation.RETURN;
                e.Input = Visit(context.expression());
                return e;
            }

            return null;
        }

        //
        // Expression
        //

        public override Expression VisitAccessPath(ExprParser.AccessPathContext context) 
        {
            Expression e = Visit(context.access()); // It is the function itself
            e.Input = Visit(context.expression()); // Prefix, input the function is applied to

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

            return e; 
        }

        public override Expression VisitMulDiv(ExprParser.MulDivContext context)
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));

            Expression e = new Expression(context.op.Text);
            e.AddOperand(left);
            e.AddOperand(right);

            if (context.op.Type == ExprParser.MUL)
            {
                e.Operation = Operation.MUL;
            }
            else if (context.op.Type == ExprParser.DIV)
            {
                e.Operation = Operation.DIV;
            }

            return e; 
        }

        public override Expression VisitAddSub(ExprParser.AddSubContext context) 
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));

            Expression e = new Expression(context.op.Text);
            e.AddOperand(left);
            e.AddOperand(right);

            if (context.op.Type == ExprParser.ADD)
            {
                e.Operation = Operation.ADD;
            }
            else if (context.op.Type == ExprParser.SUB)
            {
                e.Operation = Operation.SUB;
            }

            return e; 
        }

        public override Expression VisitCompare(ExprParser.CompareContext context) 
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));

            Expression e = new Expression(context.op.Text);
            e.AddOperand(left);
            e.AddOperand(right);

            if (context.op.Type == ExprParser.LEQ)
            {
                e.Operation = Operation.LEQ;
            }
            else if (context.op.Type == ExprParser.GEQ)
            {
                e.Operation = Operation.GEQ;
            }
            else if (context.op.Type == ExprParser.GRE)
            {
                e.Operation = Operation.GRE;
            }
            else if (context.op.Type == ExprParser.LES)
            {
                e.Operation = Operation.LES;
            }

            return e;
        }

        public override Expression VisitEqual(ExprParser.EqualContext context) 
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));

            Expression e = new Expression(context.op.Text);
            e.AddOperand(left);
            e.AddOperand(right);

            if (context.op.Type == ExprParser.EQ)
            {
                e.Operation = Operation.EQ;
            }
            else if (context.op.Type == ExprParser.NEQ)
            {
                e.Operation = Operation.NEQ;
            }

            return e;
        }

        public override Expression VisitAnd(ExprParser.AndContext context) 
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));

            Expression e = new Expression(context.op.Text, Operation.AND);
            e.AddOperand(left);
            e.AddOperand(right);

            return e; 
        }

        public override Expression VisitOr(ExprParser.OrContext context) 
        {
            Expression left = Visit(context.expression(0));
            Expression right = Visit(context.expression(1));

            Expression e = new Expression(context.op.Text, Operation.OR);
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
            return Visit(context.access());
        }

        public override Expression VisitParens(ExprParser.ParensContext context)
        {
            return Visit(context.expression()); // Skip this rule
        }

        //
        // Access
        //

        public override Expression VisitAccess(ExprParser.AccessContext context)
        {
            // It is one call with no context and no relation to previous or next calls

            string name = GetName(context.name());

            Expression e = new Expression(name, Operation.DOT); // Actually we do not know the operation

            // Find all arguments and store them as operands of the expression
            int argCount = context.arguments() != null ? context.arguments().expression().Count() : 0;
            for (int i = 0; i < argCount; i++)
            {
                Expression argExpr = Visit(context.arguments().expression(i));
                e.AddOperand(argExpr);
            }

            return e;
        }

        protected string GetType(ExprParser.TypeContext context)
        {
            string name = context.GetText();
            if (context.DELIMITED_ID() != null)
            {
                name = name.Substring(1, name.Length - 2); // Remove delimiters
            }
            return name;
        }

        protected string GetName(ExprParser.NameContext context)
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
