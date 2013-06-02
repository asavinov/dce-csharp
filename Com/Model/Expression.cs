using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// One node of a complex expression representing one operation on a single or sets of values.
	/// The main task of an expression is to define a function: a mapping from inputs to outputs.
    /// An expression algebraically defines a function and can be used to compute a function as opposed to storing a function. 
    /// In other words, an expression describes a computation as a program or query while dimensions simply store a function. 
    /// Yet, the algebraic definition is expressed in terms of existing functions (dimensions). To store functions, dimensions should be used.
	/// An expression can be evaluated either for a single input or for a set of inputs. 
    /// </summary>
    public class Expression
    {
        /// <summary>
        /// Result of evaluation. Return value of the funciton computed for the input value.
        /// </summary>
        public object Output { get; set; }
        public string OutputSetName { get; set; } // Set can be defined by its name rather than reference
        public Set OutputSet { get; set; } // Type/range of the result, that is, the set the values are taken from

        public bool IsOutputSetValued { get; set; } // Expression can produce either a single instance or a set of instances
        private int _minValues=1; // Static constraint on output: Minimum number of values
        private int _maxValues = 1; // Static constraint on output: Maximum number of values

        /// <summary>
        /// Expressin name or alias. 
        /// It can be or must be eqaul to the dimension/function name the expression defines. 
        /// </summary>
        public string Name { get; set; }
        public Dim Dimension { get; set; } // Dimension/functin this expression defines. Its lesser and greater sets should correspond to this expression intput and output sets.

        /// <summary>
        /// Operation for this node. What do we do in order to compute the output using input value and operand values. 
        /// </summary>
        public Operation Operation { get; set; }

        /// <summary>
        /// It is the element for which the function (output) has to be evaluated. 
        /// It is like operands but has a special threatment as 'this' object.
        /// It can be this identity (offset), remote element (data row), an intermediate element or null/root for global expressions.
        /// The set the input value belongs to is specified in the expression. 
        /// </summary>
        public Expression Input { get; set; }
        public void SetInput(object input)
        {
            if (input is DataRow)
            {
                Input = new Expression();
                Input.Operation = Operation.DATA_ROW;
                Input.Output = (DataRow)input;
            }
            if (input is Offset)
            {
                Input = new Expression();
                Input.Operation = Operation.DATA_ROW;
                Input.Output = (Offset)input;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// These are parameters which are needed for evaluation of this expression
        /// </summary>
        public List<Expression> Operands { get; set; }
        public Expression GetOperand(string name)
        {
            if (name == null)
            {
                return Operands.FirstOrDefault(i => i.Name == null);
            }
            else
            {
                return Operands.FirstOrDefault(i => i.Name != null && i.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        /// <summary>
        /// Where this expression is a child operand.
        /// </summary>
        public Expression ParentExpression { get; set; }
        public Expression Root
        {
            get
            {
                Expression root = this;
                while (root.ParentExpression != null)
                {
                    root = root.ParentExpression;
                }

                return root;
            }
        }
        public List<Dim> GetPath() // Return a list of dimensions from the root till this expression
        {
            List<Dim> ret = new List<Dim>();
            for (Expression exp = this; exp != null; exp = exp.ParentExpression)
            {
                ret.Insert(0, exp.Dimension);
            }

            return ret;
        }

        /// <summary>
        /// Compute output of the expression by applying it to a row of the data table. 
        /// </summary>
        public object Evaluate(bool append)
        {
			switch(Operation)
			{
                case Operation.PRIMITIVE:
				{
                    // Nothing to compute - it is a leaf expression containing a terminal value/atom/literal. 
                    // Output = Output; // The result already stores the value(s), operands are not needed. The set has to correspond to the value.
                    break;
                }
                case Operation.DATA_ROW:
                {
                    // Nothing to compute - it is a leaf expression containing a reference to a DataRow as a terminal value.
                    // Output = Output; // The result already stores the value(s), operands are not needed. The set has to correspond to the value.
                    break;
                }
                case Operation.TUPLE:
				{
                    foreach (Expression child in Operands) // Evaluate all parameters
                    {
                        if (child.Input == null) // The rule of input inheritance
                        {
                            child.Input = Input;
                        }
                        child.Evaluate(append);
                    }
                    Output = null; // The result is a combination of its operand results
                    if (append) //  Find the offset of this combination and append it if absent
                    {
                        // Output = AppendOrFind();
//                    GreaterSet.Append(Expression expr);
//                    GreaterSet.Append(path);
                    }
                    break;
                }
                case Operation.FUNCTION:
                case Operation.PATH:
                {
                    Input.Evaluate(append); // Evaluate 'this' object before it can be used
                    foreach (Expression child in Operands) // Evaluate all parameters before they can be used
                    {
                        child.Evaluate(append);
                    }

                    // Now we can compute the function using input and operands
                    string functionName = Name;

                    // How to evaluate a function depends on the type of input
                    if (Input.Operation == Operation.DATA_ROW)
                    {
                        DataRow thisRow = (DataRow)Input.Output;
                        Output = thisRow[functionName];
                    }
                    else
                    {
                        Dim dim = null;
                        if (Operation == Operation.FUNCTION)
                        {
                            dim = Input.OutputSet.GetGreaterDim(functionName);
                        }
                        else if (Operation == Operation.PATH)
                        {
                            dim = Input.OutputSet.GetGreaterPath(functionName);
                        }
                        Output = dim.GetValue((Offset)Input.Output); // Read the value of the function
                    }

                    OutputSet = null; // TODO: We have to find a primitive set corresponding to the value. For that purpose, we have to know the database the result is computed for.
                    break;
                }
            }

            return Output;
		}

        public Set FindOrCreateSet(SetRoot root)
        {
            Set lesserSet = ParentExpression != null ? ParentExpression.OutputSet : null;
            Debug.Assert(lesserSet.Root == root, "Wrong use: parent expression set hast to be within the specified root.");

            // 1. Find all possible matching sets
            Set set = null;
            if (Operation == Operation.TUPLE)
            {
                set = OutputSet != null ? root.MapToLocalSet(OutputSet) : root.FindSubset(OutputSetName);

                if (set == null) // No matching sets have been found. Create a new set
                {
                    Set superSet = null;
                    superSet = OutputSet != null ? root.MapToLocalSet(OutputSet.SuperSet) : root;

                    set = new Set(OutputSetName); // Or OutputSet.Name
                    set.SuperDim = new DimSuper("super", set, superSet);
                }
            }
            else
            {
                set = root.GetPrimitiveSubset(OutputSetName);
            }

            // 2. Find a matching dimension leading from the lesser set among the matching sets
            Dim dim = null;
            if (lesserSet != null)
            {
                foreach(Dim gDim in lesserSet.GreaterDims) 
                {
                    if (gDim.Name.Equals(Name, StringComparison.InvariantCultureIgnoreCase)) // Or Dimension.Name
                    {
                        dim = gDim;
                        break;
                    }
                }
            }

            if (dim == null)
            {
                dim = set.CreateDefaultLesserDimension(Name, lesserSet);
                // Clone all parametes
                dim.IsIdentity = Dimension.IsIdentity;
            }

            //
            // It is the main result of this method: update this expression with found/created elements. 
            //
            OutputSet = set;
            OutputSetName = set.Name;

            Name = dim.Name;
            Dimension = dim;

            // Recursively process all child expressions (only for non-primitive sets)
            if (Operation == Operation.TUPLE)
            {
                foreach (Expression child in Operands)
                {
                    child.FindOrCreateSet(root);
                }
            }

            return set;
        }

        public static Expression BuildExpression(Dim dim, Expression parent)
        {
            Expression expr = new Expression();

            expr.Output = null; // Output stores a result of each evaluation (of input value)
            expr.OutputSet = dim.GreaterSet;
            expr.OutputSetName = dim.GreaterSet.Name;

            expr.Name = dim.Name; // Name of the function
            expr.Dimension = dim;

            expr.Input = null; // Input will be a constant (value, data row etc.) defined for each evaluation

            expr.ParentExpression = parent;
            if (parent != null)
            {
                parent.Operands.Add(expr);
            }

            if (dim.IsPrimitive) // End of recursion
            {
                expr.Operation = Operation.FUNCTION; // Or PATH

                // Replace a function by path
                List<Dim> path = expr.GetPath();
                List<Dim> remPaths = dim.LesserSet.GetGreaterPathsStartingWith(path);
                if (remPaths.Count == 1)
                {
                    expr.Name = remPaths[0].Name; // Name of the path (attribute name) denoting a sequence of segments
                }
                else if (remPaths.Count > 1) // Found many
                {
                }
                else // Not found
                {
                }
            }
            else // Recursion on greater dimensions
            {
                expr.Operation = Operation.TUPLE;
                expr.Operands = new List<Expression>();

                Set gSet = dim.GreaterSet;
                foreach (Dim gDim in gSet.GetIdentityDims()) // Only identity dimensions?
                {
                    BuildExpression(gDim, expr);
                }
            }

            return expr;
        }

        public static Expression BuildExpression(Set set)
        {
            Dim dim = new Dim("", null, set); // Workaround - create an auxiliary object
            Expression expr = BuildExpression(dim, null); // and then use an existing method

            expr.Name = ""; // Reset unknown parameters
            expr.Dimension = null;

            return expr;
        }

        public Expression()
            : this("")
        {
        }

        public Expression(string name)
        {
            Name = name;
        }

    }

    public enum Operation
    {
        PRIMITIVE, // Primitive value (literal) - a leaf of the expression tree. The value need not be evaluated - it is stored. 
        DATA_ROW, // Output stores a reference to a DataRow. It is a sort of primitive (predefined, non-evaluatable) values which have internal structure
        TUPLE, // This expression is a tuple composed of its operands as members. Names of operands identify tuple members. A tuple can belong to a set and then it has its structure. 
        FUNCTION, // Compute a function. The function is specified in the operand. The function is applied to... Parameter of the function are in... 
        PATH, // In contrast to functions, it denotes a sequence of segments. So it is a matter of representation: either dimensions (functions) or paths (named sequences of dimensions). 
        PROCEDURE, // Standard procedure which does not use 'this' (input) object and depends only on parameters
        PLUS,
        MINUS,
        TIMES,
        DIVIDE,
        AND,
        OR,
        NEGATE,
        AGGREGATION, // Aggregation function like SUM or AVG. It is a procedure applied to a group of other objects (normally values but might be more complex expressions). 
    }
	
    public enum OperationType
    {
        ARITHMETIC, // sum, product etc. 
        SET, // intersect/union/negate input sets
        LOGICAL
    }
}
