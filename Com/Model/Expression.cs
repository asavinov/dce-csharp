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
        public Set OutputSet { get; set; } // Type/range of the result, that is, the set the values are taken from
        public string OutputSetName { get; set; } // Set can be defined by its name rather than reference
        private object OutputClass { get; set; } // It is structure of elements (class) of the output

        private int _minValues = 0; // Static constraint on output: Minimum number of values
        private int _maxValues = 1; // Static constraint on output: Maximum number of values
        public bool OutputIsSetValued { get; set; } // Expression can produce more than one instance (set rather than a tuple)

        public void SetOutput(Operation op, object output) // Set output values recursively for the specified nodes types
        {
            if (op == Operation.ALL || Operation == op) // Assignment is needed
            {
                // Check validity of assignment
                Debug.Assert(!(output != null && Operation == Operation.PRIMITIVE && !output.GetType().IsPrimitive), "Wrong use: constant value type has to correspond to operation type.");
                Debug.Assert(!(output != null && Operation == Operation.VARIABLE && !(output is Offset || output is Offset[] || output is DataRow)), "Wrong use: wrong type for a variable.");

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
        /// This operation corresponds to the syntactic node type in the grammar and essentially the grammar has the same values for operations. 
        /// </summary>
        public Operation Operation { get; set; }

        /// <summary>
        /// It is the element for which the function (output) has to be evaluated. 
        /// It is like operands but has a special threatment as 'this' object.
        /// It can be this identity (offset), remote element (data row), an intermediate element or null/root for global expressions.
        /// The set the input value belongs to is specified in the expression. 
        /// </summary>
        protected Expression _input;
        public Expression Input
        {
            get { return _input; }
            set
            {
                // TODO: We have to semantically check the validity of this child expression in the context of its parent expression (for example, using gramma rules)

                if (_input != null) // Detach our current child
                {
                    _input.ParentExpression = null;
                    _input = null;
                }

                if (value == null) // Nullify input - done above
                {
                    return;
                }

                // Detach the child from its parent
                if (value.ParentExpression != null && value.ParentExpression != this)
                {
                    value.ParentExpression.Input = null;
                }

                // Attach a new child
                _input = value;
                _input.ParentExpression = this;
            }
        }
/*
        public void SetInput(Expression child)
        {
            // TODO: We have to semantically check the validity of this child expression in the context of its parent expression (for example, using gramma rules)

            if (Input != null) // Detach our current child
            {
                Input.ParentExpression = null;
                Input = null;
            }

            if (child == null) // Nullify input - done above
            {
                return;
            }

            // Detach the child from its parent
            if (child.ParentExpression != null && child.ParentExpression != this) child.ParentExpression.SetInput(null);

            // Attach a new child
            Input = child;
            child.ParentExpression = this;
        }
 */
        public void SetInput(Operation op, Operation inputOp)
        {
            if (op == Operation.ALL || Operation == op) // Assignment is needed
            {
                if (Input == null)
                {
                    Input = new Expression("Input", inputOp);
                }
                else
                {
                    Input.Operation = inputOp; // TODO: We cannot simply change operation - it is necessary at least to check its validity (and validity of its operands)
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
        public int AddOperand(Expression child)
        {
            // TODO: We have to semantically check the validity of this child expression in the context of its parent expression (for example, using gramma rules)

            if (Operands == null)
            {
                Operands = new List<Expression>();
            }

            int res = Operands.IndexOf(child);
            if (res >= 0 && res < Operands.Count)
            {
                Debug.Assert(child.ParentExpression == this, "Wrong use: child expression must reference its parent");
                return res; // Already exists
            }

            if (child.ParentExpression != null) child.ParentExpression.RemoveOperand(child);

            Operands.Add(child);
            child.ParentExpression = this;

            return Operands.Count - 1;
        }
        public int RemoveOperand(Expression child)
        {
            int res = -1;
            if (Operands != null)
            {
                res = Operands.IndexOf(child);
            }

            if (res >= 0) // Found
            {
                Operands.RemoveAt(res);
            }

            if (child.ParentExpression != null && child.ParentExpression == this) child.ParentExpression = null;

            return res;
        }
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
        public List<Expression> GetOperands(Operation op)
        {
            List<Expression> res = new List<Expression>();

            // Proces this element
            if (Operation == op || op == Operation.ALL) res.Add(this);

            // Recursively check all children
            if (Input != null) res.AddRange(Input.GetOperands(op));
            if (Operands != null)
            {
                Operands.ForEach(e => res.AddRange(e.GetOperands(op)));
            }

            return res;
        }
        public List<Expression> GetOperands(Operation op, string name)
        {
            List<Expression> res = new List<Expression>();

            // Proces this element
            if (Operation == op || op == Operation.ALL)
            {
                if (Name == null && name == null)
                    res.Add(this);
                else if (Name != null && name != null && Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    res.Add(this);
            }

            // Recursively check all children
            if (Input != null) res.AddRange(Input.GetOperands(op, name));
            if (Operands != null)
            {
                foreach (Expression child in Operands)
                {
                    res.AddRange(child.GetOperands(op, name));
                }
            }

            return res;
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
        public object Evaluate()
        {
			switch(Operation)
			{
                case Operation.PRIMITIVE:
				{
                    if (Input != null)
                    {
                        Input.Evaluate();
                        Output = Input.Output;
                    }
                    else if (Operands != null && Operands.Count > 0)
                    {
                        Operands[0].Evaluate();
                        Output = Operands[0].Output;
                    }
                    break;
                }
                case Operation.VARIABLE:
                {
                    // Evaluating a variable means retrieving its current value and storing in the output
                    // Output is expected to store a value like Tuple, Offset or data row
                    break;
                }
                case Operation.TUPLE:
				{
                    if (Input != null) Input.Evaluate();
                    foreach (Expression child in Operands) // Evaluate all parameters (recursively)
                    {
                        child.Evaluate(); // Recursion. Child output will be set
                    }
                    Output = null; // Reset
                    Debug.Assert(OutputSet != null, "Wrong use: output set must be non-null when evaluating tuple expressions.");

                    break;
                }
                case Operation.DOT:
                case Operation.PROJECTION:
                {
                    if (Input != null && !Input.OutputSet.IsPrimitive && Input.Output is Offset && (Offset)Input.Output < 0) // Skip evaluation for non-existing elements
                        break; // Do not evaluate some functions (e.g., they cannot be evaluated because 'this' element does not exist yet but we know their future output which can be added later).

                    if (Input != null) Input.Evaluate(); // Evaluate input object(s) before it can be used
                    if (Operands != null)
                    {
                        foreach (Expression child in Operands) // Evaluate all parameters before they can be used
                        {
                            child.Evaluate();
                        }
                    }

                    // Find the function itself
                    string functionName = Name;
                    Dim dim = null;
                    if (Input.OutputSet != null)
                    {
                        dim = Input.OutputSet.GetGreaterDim(functionName);
                        if (dim == null) dim = Input.OutputSet.GetGreaterPath(functionName); // Alternatively, the functionName could determine whether it is a dimension or a path (complex dimension)
                    }

                    // Compute the function. 
                    // The way function is evaluated depends on the type of input 
                    if (Input.Output == null)
                    {
                        Output = null;
                    }
                    else if (Input.Output is DataRow)
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

                        // Output value may have quite different type and not all types can be consumed (stored) in the system. So cast or transform or reduce to our type system
                        // Probably the best way is to use a unified type matching and value conversion API
                        // OutputSet = ???; // TODO: We have to find a primitive set corresponding to the value. For that purpose, we have to know the database the result is computed for.
                        if (Output is DBNull)
                        {
                            Output = (Int32)0; // TODO: We have to learn to work with NULL values as well as with non-supported values like BLOB etc.
                        }
                    }
                    else if (Input.Output is Array) // Project an array of values using this function
                    {
                        // Debug.Assert(Input.Output is Offset[], "Wrong use: projection/dot can be applied to only an array of offsets - not any other type.");
                        if (Operation == Operation.PROJECTION)
                        {
                            Output = dim.GetValues((Offset[])Input.Output);
                        }
                        else if (Operation == Operation.DOT)
                        {
                            Output = Array.CreateInstance(dim.SystemType, ((Array)Input.Output).Length);
                            for (int i = 0; i < ((Offset[])Input.Output).Length; i++) // Individually and independently project all inputs
                            {
                                (Output as Array).SetValue(dim.GetValue((Offset)(Input.Output as Array).GetValue(i)), i);
                            }
                        }
                    }
                    else if (!(Input.Output is Array)) // Project a single value. Also: Input.Operation == Operation.PRIMITIVE
                    {
                        Debug.Assert(Input.Output is Offset, "Wrong use: a function/dimension can be applied to only one offset - not any other type.");
                        Output = dim.GetValue((Offset)Input.Output);
                    }

                    break;
                }
                case Operation.DEPROJECTION:
                {
                    if (Input != null) Input.Evaluate(); // Evaluate 'this' object before it can be used
                    if (Operands != null)
                    {
                        foreach (Expression child in Operands) // Evaluate all parameters before they can be used
                        {
                            child.Evaluate();
                        }
                    }

                    // Find the function itself
                    string functionName = Name;
                    Dim dim = null;
                    dim = OutputSet != null ? OutputSet.GetGreaterDim(functionName) : null;

                    // Compute the function. 
                    Output = dim.GetOffsets(Input.Output);

                    break;
                }
                case Operation.AGGREGATION:
                {
                    Debug.Assert(Input != null, "Wrong use: Aggregation expression must have group expression in Input.");
                    Debug.Assert(Operands != null && Operands.Count == 1, "Wrong use: Aggregation expression must have measure expression as an operand.");
                    Debug.Assert(!string.IsNullOrWhiteSpace(Name), "Wrong use: Aggregation function must be specified.");

                    Expression groupExpr = Input;
                    Expression measureExpr = Operands[0];
                    string aggregationFunction = Name;
                    Dim measureDim = measureExpr.Dimension; // It determines the type of the result and it knows how to aggregate its values

                    groupExpr.Evaluate(); // Compute the group

                    // If group is empty then set null as output
                    if (groupExpr.Output == null || (groupExpr.Output is Array && (groupExpr.Output as Array).Length == 0))
                    {
                        Output = null;
                        break;
                    }

                    measureExpr.SetOutput(Operation.VARIABLE, groupExpr.Output); // Assign output of the group expression to the variable

                    measureExpr.Evaluate(); // Compute the measure group

                    Output = measureDim.Aggregate(measureExpr.Output, aggregationFunction); // The dimension of each type knows how to aggregate its values

                    break;
                }
                case Operation.PLUS:
                case Operation.MINUS:
                case Operation.TIMES:
                case Operation.DIVIDE:
                {
                    Debug.Assert(Input != null, "Wrong use: Arithmetic operations must have at least one expression in Input.");

                    Input.Evaluate(); // Evaluate 'this' object before it can be used

                    double res = (double)Input.Output;
                    Output = Input.Output;

                    if (Operands != null)
                    {
                        foreach (Expression child in Operands) // Evaluate parameters and apply operation
                        {
                            child.Evaluate();

                            if (Operation == Operation.PLUS) res += Convert.ToDouble(child.Output);
                            else if (Operation == Operation.MINUS) res -= Convert.ToDouble(child.Output);
                            else if (Operation == Operation.TIMES) res *= Convert.ToDouble(child.Output);
                            else if (Operation == Operation.DIVIDE) res /= Convert.ToDouble(child.Output);
                        }
                    }

                    Output = res;

                    break;
                }
                case Operation.LESS:
                case Operation.GREATER:
                case Operation.EQUAL:
                {
                    Debug.Assert(Input != null, "Wrong use: Logical operations must have at least one expression in Input.");
                    Debug.Assert(Operands != null && Operands.Count == 1, "Wrong use: Comparison expression must have a second an operand.");

                    Expression op1 = Input;
                    Expression op2 = Operands[0];

                    op1.Evaluate();
                    op2.Evaluate();

                    if (Operation == Operation.LESS)
                    {
                        Output = ((double)op1.Output < (double)op2.Output);
                    }
                    else if (Operation == Operation.GREATER)
                    {
                        Output = ((double)op1.Output > (double)op2.Output);
                    }
                    else if (Operation == Operation.EQUAL)
                    {
                        Output = object.Equals(op1.Output, op2.Output);
                    }

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
                    superSet.AddSubset(set);
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

        private static Expression CreateImportExpression(Dim dim, Expression parent) // It is recursive part of the public method
        {
            Expression expr = new Expression();

            expr.Output = null; // Output stores a result of each evaluation (of input value)
            expr.OutputSet = dim.GreaterSet;
            expr.OutputSetName = dim.GreaterSet.Name;

            expr.Name = dim.Name; // Name of the function
            expr.Dimension = dim;

            if (parent != null)
            {
                parent.AddOperand(expr);
            }

            if (dim.IsPrimitive) // End of recursion
            {
                expr.Operation = Operation.PRIMITIVE; // Leaf of tuple structure is primitive element (which can be computed)
                expr.Name = dim.Name;

                List<Dim> path = expr.GetPath();
                Set sourceSet = expr.Root.OutputSet; // Or simply expr.Root.OutputSet.ImportDims[0].LesserSet
                Dim srcPath = sourceSet.GetGreaterPath(path);
                Debug.Assert(srcPath != null, "Import path not found. Something wrong with import.");

                Expression funcExpr = new Expression(srcPath != null ? srcPath.Name : null, Operation.DOT, dim.GreaterSet);
                // Add Input of function as a variable the values of which (output) can be assigned during export
                funcExpr.Input = new Expression("source", Operation.VARIABLE, dim.LesserSet);

                // Add function to this expression
                expr.Input = funcExpr;
            }
            else // Recursion on greater dimensions
            {
                expr.Operation = Operation.TUPLE;
                expr.Operands = new List<Expression>();
                expr.Input = null;

                Set gSet = dim.GreaterSet;
                foreach (Dim gDim in gSet.GreaterDims) // Only identity dimensions?
                {
                    CreateImportExpression(gDim, expr);
                }
            }

            return expr;
        }

        public static Expression CreateImportExpression(Set set)
        {
            Dim dim = new Dim("", null, set); // Workaround - create an auxiliary object

            Expression expr = CreateImportExpression(dim, null); // and then use an existing method

            expr.Name = ""; // Reset unknown parameters
            expr.Dimension = null;

            return expr;
        }

        // TODO: Do we actually need lesserSet? If not then delete it and use the first segment of the path. 
        public static Expression CreateProjectExpression(Set lesserSet, List<Dim> greaterDims, Operation op)
        {
            Debug.Assert(op == Operation.PROJECTION || op == Operation.DOT, "Wrong use: only PROJECTION or DOT operations are allowed.");
            Debug.Assert(lesserSet != null && greaterDims != null, "Wrong use: parameters cannot be null.");
            Debug.Assert(greaterDims.Count != 0, "Wrong use: at least one dimension has to be provided for projection.");
            Debug.Assert(lesserSet == greaterDims[0].LesserSet, "Wrong use: first dimension must be a greater dimension of the lesser set.");
            for (int i = 1; i < greaterDims.Count; i++)
            {
                Debug.Assert(greaterDims[i].LesserSet == greaterDims[i - 1].GreaterSet, "Wrong use: only sequential dimensions are allowded");
            }

            Expression previousExpr = null;
            for (int i = 0; i < greaterDims.Count; i++)
            {
                Dim dim = greaterDims[i];

                Expression expr = new Expression();

                expr.Output = null; // Result of evaluation
                expr.OutputSet = dim.GreaterSet;
                expr.OutputSetName = dim.GreaterSet.Name;
                expr.OutputIsSetValued = false;

                expr.Name = dim.Name; // Name of the function
                expr.Dimension = dim;

                expr.Operation = op;

                if(previousExpr != null) // Define the expression to which this expression will be applied
                {
                    expr.Input = previousExpr; // What will be produced by the previous segment
                }
                else // First segments in the path is a leaf of the expression tree - will be evaluated first
                {
                    expr.Input = new Expression("this", Operation.VARIABLE, lesserSet); // The project path starts from some variable which stores the initial value(s) to be projected
                }

                previousExpr = expr;
            }

            return previousExpr;
        }

        // TODO: Do we actually need lesserSet? If not then delete it and use the first segment of the path. 
        public static Expression CreateDeprojectExpression(Set lesserSet, List<Dim> greaterDims)
        {
            Debug.Assert(lesserSet != null && greaterDims != null, "Wrong use: parameters cannot be null.");
            Debug.Assert(greaterDims.Count != 0, "Wrong use: at least one dimension has to be provided for projection.");
            Debug.Assert(lesserSet == greaterDims[0].LesserSet, "Wrong use: first dimension must be a greater dimension of the lesser set.");
            for (int i = 1; i < greaterDims.Count; i++)
            {
                Debug.Assert(greaterDims[i].LesserSet == greaterDims[i - 1].GreaterSet, "Wrong use: only sequential dimensions are allowded");
            }

            Expression previousExpr = null;
            for (int i = greaterDims.Count-1; i >= 0; i--)
            {
                Dim dim = greaterDims[i];

                Expression expr = new Expression();

                expr.Output = null; // Result of evaluation
                expr.OutputSet = dim.LesserSet;
                expr.OutputSetName = dim.LesserSet.Name;
                expr.OutputIsSetValued = true;

                expr.Name = dim.Name; // Name of the function
                expr.Dimension = dim;

                expr.Operation = Operation.DEPROJECTION;

                if (previousExpr != null) // Define the expression to which this expression will be applied
                {
                    expr.Input = previousExpr; // What will be produced by the previous segment
                }
                else
                {
                    expr.Input = new Expression("this", Operation.VARIABLE, lesserSet); // The deproject path starts from some variable which stores the initial value(s) to be deprojected
                }

                previousExpr = expr;
            }

            return previousExpr;
        }

        public static Expression CreateAggregateExpression(string function, Expression group, Expression measure)
        {
            // Debug.Assert(group.OutputSet == measure.Input.OutputSet, "Wrong use: Measure is a property of group elements and has to start where groups end.");

            // Modify measure: accept many values (not single value by default)
            List<Expression> nodes = measure.GetOperands(Operation.VARIABLE);
            Debug.Assert(nodes != null && nodes.Count == 1, "Wrong use: Input nodes (variable) in measure expression must be 1.");
            nodes[0].OutputIsSetValued = true;

            // Create aggregation expression
            Expression expr = new Expression();

            expr.Output = null; // Result of evaluation
            expr.OutputSet = measure.OutputSet;
            expr.OutputSetName = measure.OutputSet.Name;
            expr.OutputIsSetValued = false;

            expr.Name = function; // Name of the function
            expr.Dimension = null;

            expr.Operation = Operation.AGGREGATION;

            expr.Input = group; // Group specification is Input
            expr.AddOperand(measure); // Measure specification is an Operand

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

        public Expression(string name, Operation op)
            : this(name)
        {
            Operation = op;
        }

        public Expression(string name, Operation op, Set outputSet)
            : this(name, op)
        {
            OutputSet = outputSet;
            OutputSetName = OutputSet != null ? OutputSet.Name : null;
        }

    }

    public enum Operation
    {
        // Patterns or collections of operations (used for querying or imposing constraints)
        NONE,
        ALL,

        // Primitive values. 
        PRIMITIVE, // Primitive value (literal) - a leaf of tuple tree and constants in expressions

        // Complex values. Structural composition
        TUPLE, // It is a combination of operands like [thing=[Integer age=21, color="red"], size=3] Names of operands identify tuple members. The output set specifies the set this tuple is from. 
        COLLECTION, // It is a set of operands like { [Integer age=21, weight=55], [Integer 1, 3] }. The members are stored in operands.
        LOOP, // It is a loop producing a new set like (Set0 super, Set1 s1, Set2 s2 | predicate | return )

        // Variables, references
        VARIABLE, // Local variable with its name in Name. We should maintain a list of all variables with their values (because they can be used in multiple locations). Evaluating a variable will move this value to Output of this node.

        // Functions
        DOT, // Apply the function to an instance. The function is Name and belongs to the Input set. Note that it is applied only in special contexts like measure expression where we have the illusion that it is applied to a set. 
        PROJECTION, // Apply the function to a set but return only non-repeating outputs. The function is Name and belongs to the Input set. 
        DEPROJECTION, // Return all inputs of the function that have the specified outputs. The function is Name and belongs to the Output set.
        AGGREGATION, // Aggregation function like SUM or AVG. It is applied to a group of elements in Input and aggregates the values returned by the measure expression. 
        PROCEDURE, // Standard procedure which does not use 'this' (input) object and depends only on parameters

        // Arithmetics
        PLUS,
        MINUS,
        TIMES,
        DIVIDE,

        // Logic
        LESS,
        GREATER,
        EQUAL,
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

}
