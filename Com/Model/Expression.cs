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

        public void SetOutput(Operation op, object output) // Set constant values recursively
        {
            if (op == Operation.ALL || Operation == op) // Assignment is needed
            {
                // Check validity of assignment
                Debug.Assert(!(output != null && Operation == Operation.PRIMITIVE && !output.GetType().IsPrimitive), "Wrong use: constant value type has to correspond to operation type.");
                Debug.Assert(!(output != null && Operation == Operation.DATA_ROW && !(output is DataRow)), "Wrong use: constant value type has to correspond to operation type.");
                Debug.Assert(!(output != null && Operation == Operation.OFFSET && !(output is Offset)), "Wrong use: constant value type has to correspond to operation type.");
                Debug.Assert(!(output != null && Operation == Operation.TUPLE && !(output is Offset)), "Wrong use: constant value type has to correspond to operation type.");

                Output = output;
            }
            
            // The same for all child nodes recursively
            if (Input != null) Input.SetOutput(op, output);
            if (Operands != null)
            {
                foreach (Expression child in Operands)
                {
                    child.SetOutput(op, output);
                }
            }
        }

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
        public Expression Input
        {
            get;
            set;
        }
/*
        public Expression GetInput() // Input inheritance strategy
        {
            return Input != null ? Input : (ParentExpression != null ? ParentExpression.Input : null);
        }
*/
        public void SetInput(Operation op, Operation inputOp)
        {
            if (op == Operation.ALL || Operation == op) // Assignment is needed
            {
                if (Input == null)
                {
                    Input = new Expression("Input");
                    Input.ParentExpression = this;
                    Input.Operation = inputOp;
                }
                else
                {
                    Input.Operation = inputOp;
                }
            }

            // The same for all child nodes recursively
            if (Operands != null)
            {
                foreach (Expression child in Operands)
                {
                    child.SetInput(op, inputOp);
                }
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
        public List<Dim> GetPath()
        {
            // Return a list of dimensions from the root till this expression
            // We ignore the segment that corresponds to the root expression because it is normally corresponds to the special export dimension

            List<Dim> ret = new List<Dim>();
            for (Expression exp = this; exp.ParentExpression != null; exp = exp.ParentExpression)
            {
                ret.Insert(0, exp.Dimension);
            }

            return ret;
        }

        /// <summary>
        /// Compute output of the expression by applying it to a row of the data table. 
        /// </summary>
        public object Evaluate(EvaluationMode evaluationMode)
        {
			switch(Operation)
			{
                case Operation.PRIMITIVE:
				{
                    if (Input != null)
                    {
                        Input.Evaluate(evaluationMode);
                        Output = Input.Output;
                    }
                    else if (Operands != null && Operands.Count > 0)
                    {
                        Operands[0].Evaluate(evaluationMode);
                        Output = Operands[0].Output;
                    }
                    break;
                }
                case Operation.DATA_ROW:
                {
                    // Output is expected to store DataRow or null
                    // Nothing to compute - it is a leaf expression containing a reference to a DataRow as a terminal value.
                    // Output = Output; // The result already stores the value(s), operands are not needed. The set has to correspond to the value.
                    break;
                }
                case Operation.OFFSET:
                {
                    // Output is expected to store Offset or null
                    // Nothing to compute - it is a leaf expression containing a reference to a DataRow as a terminal value.
                    // Output = Output; // The result already stores the value(s), operands are not needed. The set has to correspond to the value.
                    break;
                }
                case Operation.TUPLE:
				{
                    foreach (Expression child in Operands) // Evaluate all parameters (recursively)
                    {
                        child.Evaluate(evaluationMode); // Recursion. Child output will be set
                    }
                    Output = null; // Reset
                    Debug.Assert(OutputSet != null, "Wrong use: output set must be non-null when evaluating tuple expressions.");
                    switch(evaluationMode) 
                    {
                        case EvaluationMode.FIND:// TODO: Only find with no update and no append 
                            // Output = OutputSet.Find(); // Only identities will be used
                            break;
                        case EvaluationMode.UPDATE: // If an element exists (is found) then update its attributes, otherwise nothing to do
                            break;
                        case EvaluationMode.APPEND: // Try to find the element (identity). If not found then append (new identity). Update its attributes.
                            Output = OutputSet.Append(this); 
                            break;
                    }

                    break;
                }
                case Operation.FUNCTION:
                case Operation.PATH:
                {
                    if (Input != null) Input.Evaluate(evaluationMode); // Evaluate 'this' object before it can be used
                    if (Operands != null)
                    {
                        foreach (Expression child in Operands) // Evaluate all parameters before they can be used
                        {
                            child.Evaluate(evaluationMode);
                        }
                    }

                    // Now we can compute the function using input and operands
                    string functionName = Name;

                    // How to evaluate a function depends on the type of input
                    if (Input.Operation == Operation.DATA_ROW)
                    {
                        DataRow thisRow = (DataRow)Input.Output;
                        try
                        {
                            Output = thisRow[functionName];
                        }
                        catch (Exception e)
                        {
                            Output = null;
                        }
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

                    // Output value may have quite different type and not all types can be consumed (stored) in the system. So cast or transform or reduce to our type system
                    // Probably the best way is to use a unified type matching and value conversion API
                    if (Output is DBNull)
                    {
                        Output = (Int32) 0; // TODO: We have to learn to work with NULL values as well as with non-supported values like BLOB etc.
                    }

                    // OutputSet = ???; // TODO: We have to find a primitive set corresponding to the value. For that purpose, we have to know the database the result is computed for.
                    break;
                }
            }

            return Output;
		}

        public Set FindOrCreateSet(SetRoot root)
        {
            Set lesserSet = ParentExpression != null ? ParentExpression.OutputSet : null;
            Debug.Assert(root != null, "Wrong use: The root set parameter cannot be null.");
            Debug.Assert(lesserSet == null || lesserSet.Root == root, "Wrong use: parent expression set hast to be within the specified root.");

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
            else // Operation.FUNCTION or similar
            {
                set = OutputSet != null ? root.MapToLocalSet(OutputSet) : root.GetPrimitiveSubset(root.MapToLocalType(OutputSetName));
            }

            // 2. Find a matching dimension leading from the lesser set among the matching sets
            Dim dim = null;
            if (lesserSet != null)
            {
                dim = lesserSet.GetGreaterDim(Name); // Or Dimension.Name

                if (dim == null) // Matching dimension not found
                {
                    dim = set.CreateDefaultLesserDimension(Name, lesserSet);
                    // Clone all parametes
                    dim.IsIdentity = Dimension.IsIdentity;
                    lesserSet.AddGreaterDim(dim); // Really add
                }
            }

            //
            // Update this expression so that it points to the new created elements (set and dimension). 
            //
            OutputSet = set;
            OutputSetName = set.Name;

            if (dim != null)
            {
                Name = dim.Name;
                Dimension = dim;
            }

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

        public static Expression CreateExpression(Dim dim, Expression parent)
        {
            Expression expr = new Expression();

            expr.Output = null; // Output stores a result of each evaluation (of input value)
            expr.OutputSet = dim.GreaterSet;
            expr.OutputSetName = dim.GreaterSet.Name;

            expr.Name = dim.Name; // Name of the function
            expr.Dimension = dim;

            expr.ParentExpression = parent;
            if (parent != null)
            {
                parent.Operands.Add(expr);
            }

            if (dim.IsPrimitive) // End of recursion
            {
                expr.Operation = Operation.PRIMITIVE; // Or PATH or FUNCTION
                expr.Name = dim.Name;

                Expression funcExpr = new Expression();
                funcExpr.Operation = Operation.FUNCTION;
                List<Dim> path = expr.GetPath();
                Set sourceSet = expr.Root.OutputSet; // Or simply expr.Root.OutputSet.ImportDims[0].LesserSet
                Dim srcPath = sourceSet.GetGreaterPath(path);
                Debug.Assert(srcPath != null, "Import path not found. Something wrong with import.");
                funcExpr.Name = srcPath != null ? srcPath.Name : null;

                // Add Input of function as DATA_ROW
                funcExpr.Input = new Expression();
                funcExpr.Input.Operation = Operation.DATA_ROW;
                funcExpr.Input.ParentExpression = expr;

                // Add function to this expression
                expr.Input = funcExpr;
                expr.Input.ParentExpression = expr;
            }
            else // Recursion on greater dimensions
            {
                expr.Operation = Operation.TUPLE;
                expr.Operands = new List<Expression>();
                expr.Input = null;

                Set gSet = dim.GreaterSet;
                foreach (Dim gDim in gSet.GreaterDims) // Only identity dimensions?
                {
                    CreateExpression(gDim, expr);
                }
            }

            return expr;
        }

        public static Expression CreateExpression(Set set)
        {
            Dim dim = new Dim("", null, set); // Workaround - create an auxiliary object

            Expression expr = CreateExpression(dim, null); // and then use an existing method

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
        // Patterns or collections of operations (used for querying or imposing constraints)
        NONE,
        ALL,

        // Constants. These operations are not evaluated because they are supposed to store the result in the output field. 
        PRIMITIVE, // Primitive value (literal) - a leaf of the expression tree
        DATA_ROW, // Output stores a reference to a DataRow
        OFFSET, // Output stores an offset to an element

        // Structural composition
        TUPLE, // This expression is a tuple composed of its operands as members. Names of operands identify tuple members. A tuple can belong to a set and then it has its structure. 

        // Calls
        FUNCTION, // Compute a function. The function is specified in the operand. The function is applied to... Parameter of the function are in... 
        PATH, // In contrast to functions, it denotes a sequence of segments. So it is a matter of representation: either dimensions (functions) or paths (named sequences of dimensions). 
        PROCEDURE, // Standard procedure which does not use 'this' (input) object and depends only on parameters

        // Arithmetics
        PLUS,
        MINUS,
        TIMES,
        DIVIDE,

        // Aggregation function like SUM or AVG. It is a procedure applied to a group of other objects (normally values but might be more complex expressions). 
        AGGREGATION,

        // Logic
        AND,
        OR,
        NEGATE,
    }
	
    public enum OperationType
    {
        ARITHMETIC, // sum, product etc. 
        SET, // intersect/union/negate input sets
        LOGICAL
    }

    public enum EvaluationMode
    {
        FIND, // Only find 
        UPDATE, // Find and update the found element
        APPEND, // Find and append and then update
    }
}
