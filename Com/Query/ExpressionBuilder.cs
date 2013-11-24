using System;
using System.Collections.Generic;
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
            Expression e = new Expression("init_expr", Operation.TUPLE);
            Expression child = Visit(context.expr());
            e.AddOperand(child);
            return e;
        }

        public override Expression VisitAddSub(ExprParser.AddSubContext context) 
        {
            Expression left = Visit(context.expr(0));
            Expression right = Visit(context.expr(1));
            var aaa = Visit(context.GetChild(0));
            var bbb = Visit(context.children[0]);
            Expression e = null;

            if (context.op.Type == ExprParser.ADD)
            {
                e = new Expression("add_sub_expr", Operation.PLUS);
            }
            else if (context.op.Type == ExprParser.SUB)
            {
                e = new Expression("add_sub_expr", Operation.DIVIDE);
            }

            var ccc = context.ADD();
            var ddd = context.SUB();

            e.AddOperand(left);
            e.AddOperand(right);

            return e; 
        }

        public override Expression VisitMulDiv(ExprParser.MulDivContext context) 
        {
            Expression e = new Expression("mul_div_expr", Operation.TIMES);
            Expression child = VisitChildren(context);
            e.AddOperand(child);
            return e;
        }

        public override Expression VisitInt(ExprParser.IntContext context) 
        {
            Expression e = new Expression("Integer", Operation.PRIMITIVE);
            string aaa = context.GetText();
            string bbb = context.GetChild(0).GetText();
            return e; 
        }

    }
}
