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
                Debug.Assert(!(output != null && Operation == Operation.PARAMETER && !(output is Offset || output is Offset[] || output is DataRow)), "Wrong use: wrong type for a variable.");

                Output = output;
            }
            
            // The same for all child nodes recursively
            if (Input != null) Input.SetOutput(op, output);
            foreach (Expression child in Operands)
            {
                child.SetOutput(op, output);
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
            foreach (Expression child in Operands)
            {
                child.SetInput(op, inputOp);
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
            res = Operands.IndexOf(child);

            if (res >= 0) // Found
            {
                Operands.RemoveAt(res);
            }

            if (child.ParentExpression != null && child.ParentExpression == this) child.ParentExpression = null;

            return res;
        }
        public Expression GetOperand(Dim dim) // Non-recursive - find only in the direct children
        {
            if (Input != null) 
                if(Input.Name != null && dim.Name != null && Input.Name.Equals(dim.Name, StringComparison.InvariantCultureIgnoreCase))
                    return Input;

            foreach(Expression e in Operands) 
            {
                if (e == null) continue;
                if (e.Name != null && dim.Name != null && e.Name.Equals(dim.Name, StringComparison.InvariantCultureIgnoreCase))
                    return e;
            }

            return null;
        }
        public List<Expression> GetOperands(Operation op)
        {
            List<Expression> res = new List<Expression>();

            // Proces this element
            if (Operation == op || op == Operation.ALL) res.Add(this);

            // Recursively check all children
            if (Input != null) res.AddRange(Input.GetOperands(op));
            Operands.ForEach(e => res.AddRange(e.GetOperands(op)));

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
            foreach (Expression child in Operands)
            {
                res.AddRange(child.GetOperands(op, name));
            }

            return res;
        }
        public Expression GetInputLeaf()
        {
            Expression node = this;
            while (node.Input != null) node = node.Input;
            return node;
        }
        public List<Expression> GetLeaves(Operation op = Operation.TUPLE) // Get a list of nodes which have the specified operation but no children with this operation. It is normally used to get nodes corresponding to primitive paths in a complex tuple.
        {
            List<Expression> res = new List<Expression>();

            if (Operation != op && op != Operation.ALL)
            {
                return res; // Empty means that there are no leaves here and down the tree
            }
            // This node *can* be a leaf (provided if it has no children with the operation)

            // Recursively check all children
            if (Input != null) res.AddRange(Input.GetLeaves(op));
            foreach (Expression child in Operands)
            {
                res.AddRange(child.GetLeaves(op));
            }

            if (res.Count == 0) res.Add(this); // It is a leaf

            return res;
        }
        public bool IsLeaf { get { return Input == null && Operands.Count == 0; } }

        public List<Expression> GetNodePath() // Return a node list with nodes starting from the root (if it has a non-null dimension) and ending with this node
        {
            List<Expression> path = new List<Expression>();

            for (Expression node = this; node != null && node.Dimension != null; node = node.ParentExpression)
            {
                path.Insert(0, node);
            }

            return path;
        }

        public DimPath GetPath() // Return a dimension path with segments starting from the root and ending with this node
        {
            List<Expression> nodePath = GetNodePath();
            if (nodePath.Count == 0)
            {
                return new DimPath(OutputSet);
            }

            DimPath path = new DimPath(nodePath[0].Dimension.LesserSet);
            nodePath.ForEach(n => path.InsertLast(n.Dimension));

            return path;
        }

        public Expression GetLastNode(DimPath path) // Return a last node on the specified path starting from this node and ending with the returned node. Null if the path does not start from this expression. 
        {
            if (path.Path == null || path.Path.Count == 0) return this;

            if (OutputSet != path.LesserSet) return null;
            
            Expression node = this;
            for (int i = 0; i < path.Path.Count; i++) // We search the path segments sequentially
            {
                Dim seg = path.Path[i];

                Expression child = null;
                if (seg is DimSuper)
                {
                    if (node.Input == null || node.Input.Dimension != seg)
                    {
                        throw new NotImplementedException();
                        // Actually GetOperand method takes into account the type of dimension (Super or not) so we do not check it here
                    }
                }
                else
                {
                    child = node.GetOperand(seg); // Find a child corresponding to this segment

                    if (child == null) // No node for this path segment
                    {
                        break;
                    }
                }

                node = child;
            }

            return node;
        }

        public Expression GetNode(DimPath path) // Return a node corresonding to the specified path starting from this node and ending with the returned node
        {
            Debug.Assert(path != null && path.LesserSet == OutputSet, "Wrong use: path must start from the node (output set) it is applied to.");

            Expression e = GetLastNode(path);
            if (e == null) return null;
            Dim dim = path.LastSegment;

            // We use this comparison in GetOperand (but in future we should probably change the comparison criterion)
            if (e.Name != null && dim.Name != null && e.Name.Equals(dim.Name, StringComparison.InvariantCultureIgnoreCase))
                return e;

            return null; // Not found
        }

        public Expression AddPath(DimPath path) // Ensure that the specified path exists in the expression tuple tree by creating or finding the nodes corresponding to path segments
        {
            Debug.Assert(path != null && path.LesserSet == OutputSet, "Wrong use: path must start from the node (output set) it is applied to.");

            if (path.Path == null || path.Path.Count == 0) return this;

            Expression node = this;
            for (int i = 0; i < path.Path.Count; i++) // We add all segments sequentially
            {
                Expression child = null;
                Dim seg = path.Path[i];

                if (seg is DimSuper)
                {
                    if (node.Input == null || node.Input.Dimension != seg)
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    child = node.GetOperand(seg); // Find a child corresponding to this segment

                    if (child == null) // Add a new child corresponding to this segment
                    {
                        child = new Expression(seg.Name, Operation.TUPLE, seg.GreaterSet);
                        child.Dimension = seg;
                        node.AddOperand(child);
                    }
                }

                node = child;
            }

            return node;
        }

        /// <summary>
        /// Compute output of the expression by applying it to a row of the data table. 
        /// </summary>
        public virtual object Evaluate()
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
                    else if (Operands.Count > 0)
                    {
                        Operands[0].Evaluate();
                        Output = Operands[0].Output;
                    }
                    break;
                }
                case Operation.TUPLE:
				{
                    Debug.Assert(OutputSet != null, "Wrong use: output set must be non-null when evaluating tuple expressions.");

                    if (Input != null)
                    {
                        Input.Evaluate();
                    }

                    foreach (Expression child in Operands) // Evaluate all parameters (recursively)
                    {
                        child.Evaluate(); // Recursion. Child output will be set
                    }

                    if (OutputSet.IsPrimitive) // Leaf of the tuple structure - here we need to really find a concrete value by evaluating Input
                    {
                        Output = Input.Output; // Ignore child expression output but we have to have no children if everything is correct
                    }

                    else // It is a non-leaf tuple and its value is by definition a combination of its children
                    {
                        Dictionary<Dim, object> values = new Dictionary<Dim, object>();
                        if (Input != null)
                        {
                            values.Add(Input.Dimension, Input.Output);
                        }
                        foreach (Expression child in Operands)
                        {
                            if (child.Dimension == null || !child.Dimension.IsIdentity) continue;
                            values.Add(child.Dimension, child.Output);
                        }

                        Offset offset = OutputSet.Find(values); // Output of a tuple is offset of an existing element or null if it does not exist 
                        if (offset < 0) Output = null;
                        else Output = offset;
                    }

                    break;
                }
                case Operation.RETURN:
                {
                    if (Input != null)
                    {
                        Input.Evaluate();
                        Output = Input.Output;
                        ExpressionScope funcExpr = (ExpressionScope) Root;
                        funcExpr.Output = Output;
                    }
                    break;
                }
                case Operation.PARAMETER:
                {
                    // Parameters are not evaluated - they are declarations. Their value (output) is set from outside.
                    break;
                }
                case Operation.DOT:
                case Operation.PROJECTION:
                {
                    if (Input != null && !Input.OutputSet.IsPrimitive && Input.Output is Offset && (Offset)Input.Output < 0) // Skip evaluation for non-existing elements
                        break; // Do not evaluate some functions (e.g., they cannot be evaluated because 'this' element does not exist yet but we know their future output which can be added later).

                    if (Input != null) Input.Evaluate(); // Evaluate input object(s) before it can be used

                    // Resolve the function name (if not yet resolved). In fact, it has to be resolved by the symbol resolution procedure. 
                    if (Dimension == null)
                    {
                        if (Input != null)
                        {
                            if (Input.OutputSet != null)
                            {
                                Dimension = Input.OutputSet.GetGreaterDim(Name);
                                if (Dimension == null) Dimension = Input.OutputSet.GetGreaterPath(Name); // Alternatively, the function name could determine whether it is a dimension or a path (complex dimension)
                            }
                        }
                        else
                        {
                        }
                    }

                    // Compute the function. The way function is evaluated depends on the type of input 
                    if (Input == null) // It is a leaf
                    {
                        Output = Dimension.Value;
                    }
                    else if (Input.Output == null)
                    {
                        Output = null;
                    }
                    else if (Input.Output is DataRow) // Here we do not use resolved dimension object
                    {
                        DataRow thisRow = (DataRow)Input.Output;
                        try
                        {
                            Output = thisRow[Name];
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
                            Output = null; // (Int32)0; // TODO: We have to learn to work with NULL values as well as with non-supported values like BLOB etc.
                        }
                    }
                    else if (Input.Output is Array) // Project an array of values using this function
                    {
                        // Debug.Assert(Input.Output is Offset[], "Wrong use: projection/dot can be applied to only an array of offsets - not any other type.");
                        if (Operation == Operation.PROJECTION)
                        {
                            Output = Dimension.ProjectValues((Offset[])Input.Output);
                        }
                        else if (Operation == Operation.DOT)
                        {
                            Output = Array.CreateInstance(Dimension.SystemType, ((Array)Input.Output).Length);
                            for (int i = 0; i < ((Offset[])Input.Output).Length; i++) // Individually and independently project all inputs
                            {
                                (Output as Array).SetValue(Dimension.GetValue((Offset)(Input.Output as Array).GetValue(i)), i);
                            }
                        }
                    }
                    else if (!(Input.Output is Array)) // Project a single value. Also: Input.Operation == Operation.PRIMITIVE
                    {
                        Debug.Assert(Input.Output is Offset, "Wrong use: a function/dimension can be applied to only one offset - not any other type.");
                        Output = Dimension.GetValue((Offset)Input.Output);
                    }

                    break;
                }
                case Operation.DEPROJECTION:
                {
                    if (Input != null) Input.Evaluate(); // Evaluate 'this' object before it can be used

                    // Resolve the function name (if not yet resolved). In fact, it has to be resolved by the symbol resolution procedure. 
                    if (Dimension == null)
                    {
                        Dimension = OutputSet != null ? OutputSet.GetGreaterDim(Name) : null;
                    }

                    // Compute the function. 
                    if (Input == null)
                    {
                        Output = Dimension.Value;
                    }
                    else if (Input.Output == null)
                    {
                        Output = null;
                    }
                    else
                    {
                        Output = Dimension.DeprojectValue(Input.Output);
                    }

                    break;
                }
                case Operation.AGGREGATION:
                {
                    Debug.Assert(Input != null, "Wrong use: Aggregation expression must have group expression in Input.");
                    Debug.Assert(Operands.Count == 1, "Wrong use: Aggregation expression must have measure expression as an operand.");
                    Debug.Assert(!string.IsNullOrWhiteSpace(Name), "Wrong use: Aggregation function must be specified.");

                    Expression groupExpr = Input;
                    Expression measureExpr = Operands[0];
                    Dim measureDim = measureExpr.Dimension; // It determines the type of the result and it knows how to aggregate its values

                    groupExpr.Evaluate(); // Compute the group

                    // If group is empty then set null as output
                    if (groupExpr.Output == null || (groupExpr.Output is Array && (groupExpr.Output as Array).Length == 0))
                    {
                        Output = null;
                        break;
                    }

                    measureExpr.SetOutput(Operation.COLLECTION, groupExpr.Output); // Assign output of the group expression to the variable

                    measureExpr.Evaluate(); // Compute the measure group

                    string aggregationFunction = Name;
                    Output = measureDim.Aggregate(measureExpr.Output, aggregationFunction); // The dimension of each type knows how to aggregate its values

                    break;
                }
                case Operation.MUL:
                case Operation.DIV:
                case Operation.ADD:
                case Operation.SUB:
                {
                    Debug.Assert(Input != null, "Wrong use: Arithmetic operations must have at least one expression in Input.");

                    Input.Evaluate(); // Evaluate 'this' object before it can be used

                    double res = Convert.ToDouble(Input.Output);
                    Output = Input.Output;

                    foreach (Expression child in Operands) // Evaluate parameters and apply operation
                    {
                        child.Evaluate();

                        if (Operation == Operation.MUL) res *= Convert.ToDouble(child.Output);
                        else if (Operation == Operation.DIV) res /= Convert.ToDouble(child.Output);
                        else if (Operation == Operation.ADD) res += Convert.ToDouble(child.Output);
                        else if (Operation == Operation.SUB) res -= Convert.ToDouble(child.Output);
                    }

                    Output = res;

                    break;
                }
                case Operation.LEQ:
                case Operation.GEQ:
                case Operation.GRE:
                case Operation.LES:
                case Operation.EQ:
                case Operation.NEQ:
                {
                    Debug.Assert(Input != null, "Wrong use: Logical operations must have at least one expression in Input.");
                    Debug.Assert(Operands.Count == 1, "Wrong use: Comparison expression must have a second an operand.");

                    Expression op1 = Input;
                    Expression op2 = Operands[0];

                    op1.Evaluate();
                    op2.Evaluate();

                    if (Operation == Operation.LEQ)
                    {
                        Output = (Convert.ToDouble(op1.Output) <= Convert.ToDouble(op2.Output));
                    }
                    else if (Operation == Operation.GEQ)
                    {
                        Output = (Convert.ToDouble(op1.Output) >= Convert.ToDouble(op2.Output));
                    }
                    else if (Operation == Operation.GRE)
                    {
                        Output = (Convert.ToDouble(op1.Output) > Convert.ToDouble(op2.Output));
                    }
                    else if (Operation == Operation.LES)
                    {
                        Output = (Convert.ToDouble(op1.Output) < Convert.ToDouble(op2.Output));
                    }
                    else if (Operation == Operation.EQ)
                    {
                        Output = object.Equals(Convert.ToDouble(op1.Output), Convert.ToDouble(op2.Output));
                    }
                    else if (Operation == Operation.NEQ)
                    {
                        Output = !object.Equals(Convert.ToDouble(op1.Output), Convert.ToDouble(op2.Output));
                    }

                    break;
                }
            }

            return Output;
		}

        /// <summary>
        /// Resolve names of accessors and types (all uses) to object references representing storage elements (dimensions or variables). 
        /// These dimensions and variables can be either in the local context (arguments and variables) or in the permanent context (schema). 
        /// Validate the expression tree by checking that all used symboles exist (have been declared) and can be resolved. 
        /// </summary>
        public virtual void Resolve()
        {
            switch (Operation)
            {
                case Operation.TUPLE:
                    {
                        if (Input != null) Input.Resolve();
                        foreach (Expression child in Operands)
                        {
                            child.Resolve();
                        }

                        break;
                    }
                case Operation.DOT:
                case Operation.PROJECTION:
                case Operation.DEPROJECTION:
                    {
                        if (IsLeaf) // Any leaf must refer to the elements in the local context
                        {
                            // Find local context as function definition expression. We assume that any expression is within some function definition.
                            ExpressionScope funcDef = ((ExpressionScope)Root).RootScope;

                            Debug.Assert(funcDef.Operation == Operation.FUNCTION, "Wrong use: The root scope must be function definition.");

                            // Try to bind/resolve this expression name to a argument in the local context
                            Expression param = funcDef.Input;
                            if (Name != param.Name) { param = null; }

                            if (param == null)
                            {
                                foreach (Expression argExpr in funcDef.Operands)
                                {
                                    if (Name == argExpr.Name) { param = argExpr; break; }
                                }
                            }

                            if (param != null) // Found. Resolve to local context.
                            {
                                Dimension = param.Dimension;
                                OutputSet = param.OutputSet;
                                OutputSetName = param.OutputSetName;
                                OutputIsSetValued = param.OutputIsSetValued;
                            }
                            else // Cannot resolve to local context. Assume that 'this' is missing and add 'this' child accessor expression explicitly
                            {
                                Expression thisExpr = new Expression("this", Operation.DOT);
                                this.Input = thisExpr;

                                // Now this node is not a leaf. And we have to resolve it by using the child (but we can also fail)
                                Resolve(); // Recursive call (will be evaluted in non-leaf branch)
                            }
                        }
                        else // Resolve using 'this' context provided by the Input child expression
                        {
                            Debug.Assert(Input != null, "Wrong use: Resovling access operation cannot be done without Input expression.");

                            if (Input != null) Input.Resolve();
                            foreach (Expression child in Operands)
                            {
                                child.Resolve();
                            }

                            // Either set name (OutputSetName) or dimension name (Name) can be missing. 
                            // If one name is missing then the second is used as the primary one and determines the second. If both are present then they must be consistent. 

                            if (OutputSetName != null) // Resolve explicitly specified set name (type)
                            {
                                OutputSet = Input.OutputSet.Top.FindSubset(OutputSetName);
                            }

                            if (Operation == Operation.DEPROJECTION)
                            {
                                if (Name != null)
                                {
                                    List<Dim> lesserDims = Input.OutputSet.GetLesserDims(Name);
                                    Dimension = lesserDims[0];
                                }

                                if (OutputSet == null)
                                {
                                    OutputSet = Dimension.LesserSet;
                                }
                                if (Dimension == null)
                                {
                                    throw new NotImplementedException("Find a dimension between two sets.");
                                }

                                OutputSetName = OutputSet.Name;
                                OutputIsSetValued = true;
                            }
                            else
                            {
                                if (Name != null)
                                {
                                    Dimension = Input.OutputSet.GetGreaterDim(Name);
                                    if (Dimension == null) Dimension = Input.OutputSet.GetGreaterPath(Name);
                                }

                                Debug.Assert(OutputSet != null || Dimension != null, "Wrong use: Either dimension name or set name must be specified.");

                                // Now we will resolve a missing element: either set or dimension
                                if (OutputSet == null)
                                {
                                    OutputSet = Dimension.GreaterSet;
                                }
                                if (Dimension == null)
                                {
                                    throw new NotImplementedException("Find a dimension between two sets.");
                                }

                                Debug.Assert(OutputSet != null && Dimension != null, "Wrong use: Both dimension and set must be resolved.");

                                OutputSetName = OutputSet.Name;
                                OutputIsSetValued = Input.OutputIsSetValued;
                            }

                        }
                        break;
                    }
                case Operation.AGGREGATION:
                    {
                        if (Input != null) Input.Resolve();
                        foreach (Expression child in Operands)
                        {
                            child.Resolve();
                        }

                        OutputSet = Operands[0].OutputSet;
                        OutputSetName = OutputSet.Name;
                        OutputIsSetValued = false;

                        break;
                    }
                case Operation.MUL:
                case Operation.DIV:
                case Operation.ADD:
                case Operation.SUB:
                    {
                        List<Set> operandTypes = new List<Set>();

                        if (Input != null) 
                        { 
                            Input.Resolve();
                            operandTypes.Add(Input.OutputSet); 
                        }
                        foreach (Expression child in Operands)
                        {
                            child.Resolve();
                            operandTypes.Add(child.OutputSet);
                        }

                        OutputSet = TypeConversion(Operation, operandTypes);
                        if (OutputSet != null) OutputSetName = OutputSet.Name;
                        OutputIsSetValued = false;

                        break;
                    }
            }
        }

        protected Set TypeConversion(Operation op, List<Set> operandTypes)
        {
            if (operandTypes.Count == 0) return null;

            Set type = operandTypes[0];
            foreach (Set t in operandTypes)
            {
                if (type.Name == "Integer" && t.Name == "Double") type = t;
            }

            return type;
        }

        public static Expression CreateProjectExpression(List<Dim> greaterDims, Operation op, Expression leafExpr = null)
        {
            Set lesserSet = greaterDims[0].LesserSet;

            Debug.Assert(op == Operation.PROJECTION || op == Operation.DOT, "Wrong use: only PROJECTION or DOT operations are allowed.");
            Debug.Assert(lesserSet != null && greaterDims != null, "Wrong use: parameters cannot be null.");
            Debug.Assert(greaterDims.Count != 0, "Wrong use: at least one dimension has to be provided for projection.");
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
                    // The leaf expression produces initial value(s) to be projected and by default it is a variable
                    //if (leafExpr == null) leafExpr = new Expression("this", Operation.DOT, lesserSet);
                    //tupleExpr.Input = leafExpr;
                }

                previousExpr = expr;
            }

            return previousExpr;
        }

        // TODO: Do we actually need lesserSet? If not then delete it and use the first segment of the path. 
        public static Expression CreateDeprojectExpression(List<Dim> greaterDims)
        {
            Set lesserSet = greaterDims[0].LesserSet;

            Debug.Assert(lesserSet != null && greaterDims != null, "Wrong use: parameters cannot be null.");
            Debug.Assert(greaterDims.Count != 0, "Wrong use: at least one dimension has to be provided for projection.");
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
                    //tupleExpr.Input = new Expression("this", Operation.DOT, lesserSet); // The deproject path starts from some variable which stores the initial value(s) to be deprojected
                }

                previousExpr = expr;
            }

            return previousExpr;
        }

        public static Expression CreateAggregateExpression(string function, Expression group, Expression measure)
        {
            // Add a collection of values to be aggregated as a leaf (this collection will store intermediate results produced from grouping)
            Expression leaf = measure.GetInputLeaf();
            Expression collection = new Expression("group", Operation.COLLECTION, group.OutputSet); // It stores the result of grouping (so it is a kind of intermediate variable)
            leaf.Input = collection;

            // Make all nodes set-valued
            for (Expression node = measure; node != null; node = node.Input)
            {
                node.OutputIsSetValued = true;
            }

            // Create aggregation expression
            Expression expr = new Expression();

            expr.Output = null; // Result of evaluation
            expr.OutputSet = measure.OutputSet;
            expr.OutputSetName = measure.OutputSet.Name;
            expr.OutputIsSetValued = false;

            expr.Name = function; // Name of the function
            expr.Dimension = null; // Here we need to resolve the aggregation function

            expr.Operation = Operation.AGGREGATION;

            expr.Input = group; // Group specification is Input
            expr.AddOperand(measure); // Measure specification is an Operand

            return expr;
        }

        public Expression()
        {
            Operands = new List<Expression>();
        }

        public Expression(string name) 
            : this()
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

    public class ExpressionScope : Expression
    {
        public List<Expression> Statements { get; set; }
        public ExpressionScope ParentScope { get; set; }

        public void AddStatement(ExpressionScope stmt) 
        {
            if (stmt.ParentScope != null) stmt.ParentScope.RemoveStatement(stmt);

            Statements.Add(stmt);
            stmt.ParentScope = this;
        }

        public void RemoveStatement(ExpressionScope stmt)
        {
            Statements.Remove(stmt);
            stmt.ParentScope = null;
        }

        public ExpressionScope RootScope
        {
            get 
            {
                ExpressionScope root = this;
                while (root.ParentScope != null)
                {
                    root = root.ParentScope;
                }

                return root;
            }
        }

        public static ExpressionScope CreateFunctionDeclaration(string name, string inputSetName, string outputSetName)
        {
            // Function expression
            ExpressionScope funcExpr = new ExpressionScope();
            funcExpr.Name = name;
            funcExpr.Operation = Operation.FUNCTION;
            funcExpr.OutputSetName = outputSetName;
            funcExpr.OutputIsSetValued = false;

            // This argument
            Expression thisExpr = new Expression("this", Operation.PARAMETER);
            thisExpr.OutputSetName = inputSetName;
            thisExpr.OutputIsSetValued = false;

            funcExpr.Input = thisExpr;

            // Return statement (without expression)
            ExpressionScope stmtExpr = new ExpressionScope();
            stmtExpr.Name = "return";
            stmtExpr.Operation = Operation.RETURN;
            stmtExpr.OutputSetName = outputSetName;
            stmtExpr.OutputIsSetValued = false;

            funcExpr.AddStatement(stmtExpr);

            return funcExpr;
        }

        public override object Evaluate()
        {
            switch (Operation)
            {
                case Operation.FUNCTION:
                    {
                        foreach (Expression stmt in Statements)
                        {
                            stmt.Evaluate();
                        }
                        break;
                    }
                case Operation.RETURN:
                    {
                        Input.Evaluate();
                        Output = Input.Output;
                        ParentScope.Output = Output; // Return statement changes the parent (local) state
                        break;
                    }
            }

            return Output;
        }

        public override void Resolve()
        {
            switch (Operation)
            {
                case Operation.RETURN:
                    {
                        if (Input != null) Input.Resolve();
                        OutputSet = Input.OutputSet;
                        OutputSetName = Input.OutputSetName;
                        OutputIsSetValued = Input.OutputIsSetValued;
                        break;
                    }
                case Operation.FUNCTION:
                    {
                        foreach (Expression stmt in Statements)
                        {
                            stmt.Resolve();
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Resolve names in the function definition into schema objects.
        /// </summary>
        public void ResolveFunction(SetTop top)
        {
            Input.OutputSet = top.FindSubset(Input.OutputSetName); // this type (domain set of the function)
            if (Input.Dimension == null)
            {
                Input.OutputIsSetValued = false;
                if (!Input.OutputIsSetValued)
                {
                    Input.Dimension = new Dim(Input.Name, Input.OutputSet, Input.OutputSet); // Variable
                }
                else
                {
                    throw new NotImplementedException("Multi-valued (function-valued) arguments and types are not implemented.");
                }
            }

            foreach (Expression child in Operands)
            {
                child.OutputSet = top.FindSubset(child.OutputSetName); // Resolve argument types
                if (child.Dimension != null)
                {
                    child.OutputIsSetValued = false;
                    if (!child.OutputIsSetValued)
                    {
                        child.Dimension = new Dim(child.Name, child.OutputSet, child.OutputSet); // Variable
                    }
                    else
                    {
                        throw new NotImplementedException("Multi-valued (function-valued) arguments and types are not implemented.");
                    }
                }
            }

            if (OutputSet == null) OutputSet = top.FindSubset(OutputSetName); // Return value type (range set of the function)
            OutputIsSetValued = false;
            if (Dimension == null) Dimension = Input.OutputSet.GetGreaterDim(Name); // Resolve function name into dimension object
        }

        public ExpressionScope()
            : base()
        {
            Statements = new List<Expression>();
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

        // Statements
        RETURN,
        LOOP, // Product. It is a loop producing a new set like (Set0 super, Set1 s1, Set2 s2 | predicate | return )

        FUNCTION, // Function definition
        PARAMETER, // Local variable/parameter/field with its name in Name. It stores a reference to a dimension (static or dynamic) with real values and hence it is always typed by the schema. We should maintain a list of all variables with their values (because they can be used in multiple locations). Evaluating a variable will move this value to Output of this node.

        // Functions
        DOT, // Apply the function to an instance. The function is Name and belongs to the Input set. Note that it is applied only in special contexts like measure expression where we have the illusion that it is applied to a set. 
        PROJECTION, // Apply the function to a set but return only non-repeating outputs. The function is Name and belongs to the Input set. 
        DEPROJECTION, // Return all inputs of the function that have the specified outputs. The function is Name and belongs to the Output set.
        AGGREGATION, // Aggregation function like SUM or AVG. It is applied to a group of elements in Input and aggregates the values returned by the measure expression. 

        // Unary
        NEG,
        NOT,

        // Arithmetics
        MUL,
        DIV,
        ADD,
        SUB,

        // Logic
        LEQ,
        GEQ,
        GRE,
        LES,

        EQ,
        NEQ,

        AND,
        OR,
    }
	
    public enum OperationType
    {
        ARITHMETIC, // sum, product etc. 
        SET, // intersect/union/negate input sets
        LOGICAL
    }

}
