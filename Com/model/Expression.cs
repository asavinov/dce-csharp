using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// One node of a complex expression representing one operation on single or sets of values.
	/// The main task of an expression is to define a mapping from inputs to outputs.
	/// To compute output for an input we need to evaluate the expression which will lead to evaluating expressions for operands. 
    /// </summary>
    public class Expression
    {
        private Operation _operation; // Concrete operation

        private List<Expression> _operands; // These are parameters which are needed for evaluation of this expression

        private object _result; // Result of evaluation. Constant for primitive operation. 

        private Set _resultType; // Type of the result, that is, the set the values are taken from
        private bool _isSetValued=false; // Is the result a set?
        private int _minValues=1; // Minimum number of values
        private int _maxValues=1; // Maximum number of values

        public Expression() 
        {
	    }

        public object evaluate(object input)
        {
			switch(_operation)
			{
                case Operation.PRIMITIVE: // Do nothing - the result already contains the value or values, operands are not needed - it is a leaf
				{
                    break;
                }
                case Operation.PLUS: // Sum operands and store the result
				{
                    break;
                }
			}
			return null;
		}

        public object evaluate(object[] inputs)
        {
			return null;
		}
    }

    public enum Operation
    {
        PRIMITIVE, // Primitive value (literal) - a leaf of the expression tree. The value need not be evaluated - it is stored. 
        PLUS,
        MINUS,
        TIMES,
        DIVIDE,
        AND,
        OR,
        NEGATE,
        PROCEDURE, // Standard procedure
        AGGREGATION, // Standard aggregation function
    }
	
    public enum OperationType
    {
        ARITHMETIC, // sum, product etc. 
        SET, // intersect/union/negate input sets
        LOGICAL
    }
}
