using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Offset = System.Int32;
using System.Globalization;

namespace Com.Model
{
    // Represents a function definition in terms of other functions and provides its run-time interface.
    // Main unit of the representation is a triple: (Type) Name = Value. 
    public class ExprNode : TreeNode<ExprNode>
    {
        public ExprNode GetChild(int child) { return (ExprNode)Children[child]; }
        public ExprNode GetChild(string name)
        {
            return (ExprNode)Children.FirstOrDefault(x => ((ExprNode)x).Name == name);
        }

        //
        // Node role. How to process this node and interpret the child nodes
        //

        // VALUE: stores a value directly. no computations are needed (maybe compile-time resolution of the name)
        // TUPLE: combination of de-projections of all child node results with names identifying dimensions (of this node type)
        // CALL: apply 'method' child to 'this' and other children
        public OperationType Operation { get; set; }

        //
        // Attribute of the result value relative to the parent
        //

        // An relative offset of the result in the parent which is interpreted by the parent node.
        // 'method' - the node represents a method, procedure (SUM, MUL), function, variable or another action to be performed by the parent
        // 'this' - the node represents a special this argument for the processing parent node
        public string Name { get; set; }

        // It is a reference to a dimension, variable or another kind of callable storage run-time object
        // Is resolved from name at compile-time if the name represents a method (dimension, function etc.)
        // It could be CsColumnEvaluator (at least for Dim storage) so that we directly access values at run-time. 
        // Alternatively, the whole node implements this interface
        protected CsColumnData column;
        protected CsVariable variable;
        // Action type. A modifier that helps to choose the function variation
        public ActionType Action { get; set; }

        //
        // Result value computed at run-time
        //

        // Return run-time value after processing this node to be used by the parent. It must have the specified type.
        public CsVariable Result { get; set; }

        //
        // Maybe we need a method for retrieving dependency information, that is, a list of other functions (with their sets) used in the formula, including maybe system functions, variables and other context objects
        // Such expressions could be generated from our own source code, and they could be translated in the native source code (or even directly to byte-code without source code)
        //

        /// <summary>
        /// Resolve the name and type name against the storage elements (schema, variables).
        /// The resolved object references are stored in the fields and will be used during evaluation without name resolution.
        /// Notes:
        /// - Names that need to be resolved: type names, function (column) names, variable names, system procedure names, operator names (plus, minus etc.)
        /// - An expression can be resolved against two schemas (not one) because it connects two sets (input and output) that can belong to different schemas. Particularly, it happens for import/export dimensions. 
        /// - Types can be already resolved during expression creation. Particularly, in the case if it is created from a mapping object. 
        /// - Resolution starts from some well-known point and then propagates along the structure.
        /// - Types in tuples depend on the parent type. Columns (variables, procedures etc.) depend on the children. 
        /// </summary>
        /// <param name="variables"></param>
        public virtual void Resolve(CsSchema schema, List<CsVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                //
                // Resolve string into object and store in the result. Derive the type from the format. 
                //
                int intValue;
                // About conversion from string: http://stackoverflow.com/questions/3965871/c-sharp-generic-string-parse-to-any-object
                if (int.TryParse(Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                {
                    Result.TypeName = "Integer";
                    Result.SetValue(intValue);
                }
                double doubleValue;
                if (double.TryParse(Name, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue))
                {
                    Result.TypeName = "Double";
                    Result.SetValue(doubleValue);
                }
                else // Cannot parse means string
                {
                    Result.TypeName = "String";
                    Result.SetValue(Name);
                }
                Result.TypeTable = schema.GetPrimitive(Result.TypeName);
            }
            else if (Operation == OperationType.TUPLE)
            {
                //
                // Resolve this (assuming the parents are resolved)
                //
                if (string.IsNullOrEmpty(Result.TypeName))
                {
                    ExprNode parent = (ExprNode)Parent;
                    if (parent == null)
                    {
                        ;
                    }
                    else if (parent.Operation == OperationType.TUPLE) // Tuple in another tuple
                    {
                        if (parent.Result.TypeTable != null && !string.IsNullOrEmpty(Name))
                        {
                            CsColumn col = parent.Result.TypeTable.GetGreaterDim(Name);
                            column = col.ColumnData;
                            Result.TypeTable = col.GreaterSet;
                            Result.TypeName = col.GreaterSet.Name;
                        }
                    }
                    else // Tuple in some other node, e.g, argument or value
                    {
                        if (parent.Result.TypeTable != null && !string.IsNullOrEmpty(Name))
                        {
                            CsColumn col = parent.Result.TypeTable.GetGreaterDim(Name);
                            column = col.ColumnData;
                            Result.TypeTable = col.GreaterSet;
                            Result.TypeName = col.GreaterSet.Name;
                        }
                    }
                }
                else if (Result.TypeTable == null || !StringSimilarity.SameTableName(Result.TypeTable.Name, Result.TypeName))
                {
                    // There is name without table, so we need to resolve this table name but against correct schema
                    throw new NotImplementedException();
                }

                //
                // Resolve children (important: after the tuple itself, because this node will be used)
                //
                foreach (ExprNode childNode in Children)
                {
                    childNode.Resolve(schema, variables);
                }
            }
            else if (Operation == OperationType.CALL)
            {
                //
                // Resolve children (important: before this node because this node uses children)
                //
                foreach (ExprNode childNode in Children)
                {
                    childNode.Resolve(schema, variables);
                }
                
                // Resolve type name
                if (!string.IsNullOrEmpty(Result.TypeName))
                {
                    Result.TypeTable = schema.FindTable(Result.TypeName);
                }

                //
                // Resolve this (assuming the children have been resolved)
                //
                ExprNode methodChild = GetChild("method"); // Get column name
                ExprNode thisChild = GetChild("this"); // Get column lesser set
                int childCount = Children.Count;

                if (childCount == 0) // Resolve variable (or add a child this variable assuming that it has been omitted)
                {
                    // Try to resolve as a variable (including this variable). If success then finish.
                    CsVariable var = variables.FirstOrDefault(v => StringSimilarity.SameColumnName(v.Name, Name));

                    if (var != null)
                    {
                        variable = var;

                        Result.TypeName = var.TypeName;
                        Result.TypeTable = var.TypeTable;
                    }
                    else // Cannot resolve as a variable - try resolve as a column name of 'this'
                    {
                        // Add expression node representing 'this' variable (so we apply a function to this variable)
                        thisChild = new ExprNode();
                        thisChild.Name = "this";
                        thisChild.Operation = OperationType.CALL;
                        thisChild.Action = ActionType.READ;
                        AddChild(thisChild);

                        // Call this method again. It will resolve this same node as a column applied to this variabe (just added as a child) 
                        this.Resolve(schema, variables);
                    }
                }
                else if (childCount == 1) // Function applied to previous output (resolve column)
                {
                    string methodName = this.Name;
                    ExprNode outputChild = null;
                    if (thisChild != null) // Function applied to 'this' (resolve column)
                    {
                        outputChild = thisChild;
                    }
                    else // Function applied to previous function output (resolve column)
                    {
                        outputChild = GetChild(0);
                    }
                    CsColumn col = outputChild.Result.TypeTable.GetGreaterDim(methodName);
                    column = col.ColumnData;

                    Result.TypeName = col.GreaterSet.Name;
                    Result.TypeTable = col.GreaterSet;
                }
                else // System procedure or operator (arithmetic, logical etc.)
                {
                    string methodName = this.Name;

                    // TODO: Derive return type. It is derived from arguments by using type conversion rules
                    Result.TypeName = "Double";
                    Result.TypeTable = schema.GetPrimitive(Result.TypeName);

                    switch (Action)
                    {
                        case ActionType.MUL:
                        case ActionType.DIV:
                        case ActionType.ADD:
                        case ActionType.SUB:
                            break;
                        default: // Some procedure. Find its API specification or retrieve via reflection
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Evaluate()
        {
            //
            // Evaluate children so that we have all their return values
            //

            if (Operation == OperationType.VALUE)
            {

            }
            else if (Operation == OperationType.TUPLE)
            {
                //
                // Evaluate children
                //
                foreach (ExprNode childNode in Children)
                {
                    childNode.Evaluate();
                }

                if (Result.TypeTable.IsPrimitive) // Primitive TUPLE nodes are processed differently
                {
                    Debug.Assert(Children.Count == 1, "Wrong use: a primitive TUPLE node must have one child expression providing its value.");
                    ExprNode childNode = GetChild(0);
                    object val = childNode.Result.GetValue();

                    // Copy result from the child expression and convert it to this node type
                    if (val is DBNull) 
                    {
                        Result.SetValue(null);
                    }
                    else if (StringSimilarity.SameTableName(Result.TypeTable.Name, "Integer"))
                    {
                        Result.SetValue(Convert.ToInt32(val));
                    }
                    else if (StringSimilarity.SameTableName(Result.TypeTable.Name, "Double"))
                    {
                        Result.SetValue(Convert.ToDouble(val));
                    }
                    else if(StringSimilarity.SameTableName(Result.TypeTable.Name, "Decimal"))
                    {
                        Result.SetValue(Convert.ToDecimal(val));
                    }
                    else if (StringSimilarity.SameTableName(Result.TypeTable.Name, "String"))
                    {
                        Result.SetValue(Convert.ToString(val));
                    }
                    else if (StringSimilarity.SameTableName(Result.TypeTable.Name, "Boolean"))
                    {
                        Result.SetValue(Convert.ToBoolean(val));
                    }
                    else if (StringSimilarity.SameTableName(Result.TypeTable.Name, "DateTime"))
                    {
                        Result.SetValue(Convert.ToDateTime(val));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    // Do execute the action because it is a primitive set
                }
                else // Non-primitive/non-leaf TUPLE node is a complex value with a special operation
                {
                    // Find, append or update an element in this set (depending on the action type)
                    if (Action == ActionType.READ) // Find the offset
                    {
                        Offset input = Result.TypeTable.TableData.Find(this);

                        if (input < 0 || input >= Result.TypeTable.TableData.Length) // Not found
                        {
                            Result.SetValue(null);
                        }
                        else
                        {
                            Result.SetValue(input);
                        }
                    }
                    else if (Action == ActionType.UPDATE) // Find and update the record
                    {
                    }
                    else if (Action == ActionType.APPEND) // Find, try to update and append if cannot be found
                    {
                        Offset input = Result.TypeTable.TableData.Find(this); // Uniqueness constraint: check if it exists already

                        if (input < 0 || input >= Result.TypeTable.TableData.Length) // Not found
                        {
                            input = Result.TypeTable.TableData.Append(this); // Append new
                        }

                        Result.SetValue(input);
                    }
                    else
                    {
                        throw new NotImplementedException("ERROR: Other actions with tuples are not possible.");
                    }
                }
            }
            else if (Operation == OperationType.CALL)
            {
                //
                // Evaluate children
                //
                foreach (ExprNode childNode in Children)
                {
                    childNode.Evaluate();
                }

                double doubleRes = 0;
                bool boolRes = false;

                if (Action == ActionType.READ)
                {
                    if(this is OledbExprNode) // It is easier to do it here rather than (correctly) in the extension
                    {
                        // Find current Row object
                        ExprNode thisNode = GetChild("this");
                        DataRow input = (DataRow)thisNode.Result.GetValue();

                        // Use attribute name or number by applying it to the current Row object (offset is not used)
                        string attributeName = Name;
                        object output = input[attributeName];
                        Result.SetValue(output);
                    }
                    else if (column != null) 
                    {
                        ExprNode prevOutput = GetChild(0);
                        Offset input = (Offset)prevOutput.Result.GetValue();
                        object output = column.GetValue(input);
                        Result.SetValue(output);
                    }
                    else if (variable != null)
                    {
                        object result = variable.GetValue();
                        Result.SetValue(result);
                    }
                }
                if (Action == ActionType.UPDATE) // Compute new value for the specified offset using a new value in the variable
                {
                    ExprNode thisNode = GetChild("this");
                    ExprNode valueNode = GetChild("value");

                    Offset group = (Offset)groupNode.Result.GetValue();
                    object value = measureNode.Result.GetValue();

                    object currentValue = column.GetValue(group);
                    switch(Name) 
                    {
                        case "SUM":
                            doubleRes = Convert.ToDouble(currentValue) + Convert.ToDouble(value);
                            break;
                    }
                    Result.SetValue(doubleRes);

                }
                //
                // MUL, DIV, ADD, SUB, 
                //
                else if (Action == ActionType.MUL)
                {
                    foreach (ExprNode childNode in Children)
                    {
                        double arg = Convert.ToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes += arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.DIV)
                {
                    foreach (ExprNode childNode in Children)
                    {
                        double arg = Convert.ToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes /= arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.ADD)
                {
                    foreach (ExprNode childNode in Children)
                    {
                        double arg = Convert.ToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes += arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.SUB)
                {
                    foreach (ExprNode childNode in Children)
                    {
                        double arg = Convert.ToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes /= arg;
                    }
                    Result.SetValue(doubleRes);
                }
                //
                // LEQ, GEQ, GRE, LES,
                //
                else if (Action == ActionType.LEQ)
                {

                    double arg1 = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    double arg2 = Convert.ToDouble(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 <= arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.GEQ)
                {

                    double arg1 = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    double arg2 = Convert.ToDouble(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 >= arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.GRE)
                {

                    double arg1 = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    double arg2 = Convert.ToDouble(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 > arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.LES)
                {

                    double arg1 = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    double arg2 = Convert.ToDouble(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 < arg2;
                    Result.SetValue(boolRes);
                }
                //
                // EQ, NEQ
                //
                else if (Action == ActionType.EQ)
                {
                    double arg1 = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    double arg2 = Convert.ToDouble(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 == arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.NEQ)
                {
                    double arg1 = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    double arg2 = Convert.ToDouble(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 != arg2;
                    Result.SetValue(boolRes);
                }
                //
                // AND, OR
                //
                else if (Action == ActionType.AND)
                {
                    bool arg1 = Convert.ToBoolean(((ExprNode)Children[0]).Result.GetValue());
                    bool arg2 = Convert.ToBoolean(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 && arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.OR)
                {
                    bool arg1 = Convert.ToBoolean(((ExprNode)Children[0]).Result.GetValue());
                    bool arg2 = Convert.ToBoolean(((ExprNode)Children[1]).Result.GetValue());
                    boolRes = arg1 || arg2;
                    Result.SetValue(boolRes);
                }
                else // Some procedure. Find its API specification or retrieve via reflection
                {
                }
            }
        }

        /// <summary>
        /// // Assuming that this expression is a tuple, add a new branch to the tuple tree based on the specified dimension path. 
        /// Ensure that the specified path exists in the tuple expression tree by finding and creating if not found the nodes corresponding to path segments.
        /// Return the leaf node of the tuple branch (this variable if it is requested) which correponds to the first segment in the path.
        /// </summary>
        public ExprNode AddToTuple(DimPath path, bool withThisVariable) 
        {
            // Question: what operation whould be in the leaf: TUPLE, VALUE or whatever

            Debug.Assert(path != null && path.LesserSet == Result.TypeTable, "Wrong use: path must start from the node (output set) it is applied to.");

            if (path.Path == null || path.Path.Count == 0) return this;

            ExprNode node = this;
            for (int i = 0; i < path.Path.Count; i++) // We add all segments sequentially
            {
                CsColumn seg = path.Path[i];
                ExprNode child = node.GetChild(seg.Name); // Try to find a child corresponding to this segment

                if (child == null) // Not found. Add a new child corresponding to this segment
                {
                    child = new ExprNode();
                    child.Operation = OperationType.TUPLE;
                    child.Action = this.Action; // We inherit the action from the tuple root
                    child.Name = seg.Name;
                    child.Result.TypeTable = seg.GreaterSet;
                    child.Result.TypeName = seg.GreaterSet.Name;

                    node.AddChild(child);
                }

                node = child;
            }

            //
            // Create the last node corresponding to this variable and append it to the expression
            //
            if (withThisVariable)
            {
                ExprNode thisNode = null;

                thisNode = new ExprNode();
                thisNode.Name = "this";
                thisNode.Operation = OperationType.CALL;
                thisNode.Action = ActionType.READ;

                thisNode.Result.TypeTable = path.LesserSet;
                thisNode.Result.TypeName = path.LesserSet.Name;

                node.AddChild(thisNode);
                node = thisNode;
            }

            return node;
        }

        /// <summary>
        /// If this variable is requested then the return expression will create at one node. 
        /// Return the last node of the expression (this node if requested) which corresponds to the first segment of the path.
        /// </summary>
        public static ExprNode CreateCall(DimPath path, bool withThisVariable) 
        {
            ExprNode expr = null;

            if (path.LesserSet.Top is SetTopOledb) // Access via relational attribute
            {
                expr = new OledbExprNode();
                expr.Operation = OperationType.CALL;
                expr.Action = ActionType.READ;
                expr.Name = path.Name;
                expr.Result.TypeTable = path.LesserSet;
                expr.Result.TypeName = path.LesserSet.Name;
            }
            else // Access via function/column composition
            {
                for (int i = path.Path.Count() - 1; i >= 0; i--)
                {
                    CsColumn seg = path.Path[i];

                    ExprNode node = new ExprNode();
                    node.Operation = OperationType.CALL;
                    node.Action = ActionType.READ;
                    node.Name = seg.Name;
                    node.Result.TypeTable = seg.LesserSet;
                    node.Result.TypeName = seg.LesserSet.Name;

                    if (expr != null)
                    {
                        expr.AddChild(node);
                    }

                    expr = node;
                }
            }

            //
            // Create the last node corresponding to this variable and append it to the expression
            //
            ExprNode thisNode = null;
            if (withThisVariable)
            {
                thisNode = new ExprNode();
                thisNode.Name = "this";
                thisNode.Operation = OperationType.CALL;
                thisNode.Action = ActionType.READ;

                thisNode.Result.TypeTable = path.LesserSet;
                thisNode.Result.TypeName = path.LesserSet.Name;
            }

            if (expr != null)
            {
                expr.AddChild(thisNode);
                expr = thisNode;
            }

            return expr;
        }

        /// <summary>
        /// Create an upate expression for the specified aggregation column and standard aggregation function. 
        /// This expression will read two variables from the context: 'this' typed by the column lesser set and 'value' typed by the column greater set.
        /// </summary>
        public static ExprNode CreateUpdater(CsColumn column, ActionType aggregation)
        {
            //
            // A node for reading the current function value at the offset in 'this' variable
            //
            ExprNode currentValueNode = CreateReader(column);

            //
            // A node for reading a new function value from the well-known variable
            //
            ExprNode valueNode = new ExprNode();
            valueNode.Name = "value";
            valueNode.Operation = OperationType.CALL;
            valueNode.Action = ActionType.READ;

            valueNode.Result.TypeTable = column.GreaterSet;
            valueNode.Result.TypeName = column.GreaterSet.Name;

            //
            // A node for computing a result (updated) function value from the current value and new value
            //
            ExprNode expr = new ExprNode();
            expr.Operation = OperationType.CALL;
            expr.Action = aggregation; // SUM etc.
            expr.Name = column.Name;

            expr.Result.TypeTable = column.GreaterSet;
            expr.Result.TypeName = column.GreaterSet.Name;

            // Two arguments in child nodes
            expr.AddChild(currentValueNode);
            expr.AddChild(valueNode);

            return expr;
        }

        /// <summary>
        /// Create a read expression for the specified column. 
        /// This expression will read one variables from the context: 'this' typed by the column lesser set.
        /// </summary>
        public static ExprNode CreateReader(CsColumn column)
        {
            //
            // A node for reading the offset to be read from the well-known variable
            //
            ExprNode thisNode = new ExprNode();
            thisNode.Name = "this";
            thisNode.Operation = OperationType.CALL;
            thisNode.Action = ActionType.READ;

            thisNode.Result.TypeTable = column.LesserSet;
            thisNode.Result.TypeName = column.LesserSet.Name;

            //
            // A node for reading the current function value at the offset in 'this' variable
            //
            ExprNode expr = new ExprNode();
            expr.Name = column.Name;
            expr.Operation = OperationType.CALL;
            expr.Action = ActionType.READ;

            expr.Result.TypeTable = column.GreaterSet;
            expr.Result.TypeName = column.GreaterSet.Name;

            expr.AddChild(thisNode);

            return expr;
        }

        public ExprNode()
        {
            Result = new Variable("return", "Void");
        }

    }

    /// <summary>
    /// This class implements functions for accessing Oledb data source as input. 
    /// </summary>
    public class OledbExprNode : ExprNode
    {
        public override void Resolve(CsSchema schema, List<CsVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                base.Evaluate();
            }
            else if (Operation == OperationType.TUPLE)
            {
                base.Evaluate();
            }
            else if (Operation == OperationType.CALL)
            {
                base.Resolve(schema, variables);
                // Resolve attribute names by preparing them for access - use directly the name for accessting the row object found in the this child
            }
        }

        public override void Evaluate()
        {
            if (Operation == OperationType.VALUE)
            {
                base.Evaluate();
            }
            else if (Operation == OperationType.TUPLE)
            {
                throw new NotImplementedException("ERROR: Wrong use: tuple is never evaluated for relational table.");
            }
            else if (Operation == OperationType.CALL)
            {
                base.Evaluate();
            }
        }

    }

    public enum OperationType
    {
        // - (VALUE): do we need this? could be modeled by tuples with no children or by type. A primitive value (literal) represented here by-value
        VALUE, // this node stores a value. no computations

        // - TUPLE/DOWN/ROW_OP: access to the named function input given a combination of outputs.
        //   - name is a function from a (single) parent node set (input) to this node set (output) - but computed in inverse direction from this node to the parent when processing the parent (this node processes children's functions)
        //   - node type = input type of any child function (surrogate), child type(s) = output(s) type of the child function
        //   - TUPLE always means moving down in poset by propagating greater surrogates to lesser surrogates
        TUPLE, // children are tuple attributes corresponding to dimensions. this node de-projects their values and find the input for this node set

        // Operation type. What is in this node and how to interpret the name
        // - ACCESS/UP/COLUMN_OP: access to named function (variable, arithmetic, system procedure) output given intput(s) in children
        //   - name is a function from several children (inputs) to this node set (output) - computed when processing this node
        //   - node type = output type (surrogate), child type(s) = input(s) type
        //   - it means moving up in the poset from lesser values to greater values
        CALL, // this node processes children and produces output for this node. children are named arguments. the first argument is 'method'
    }

    public enum ActionType
    {
        // Variable or column or tuple
        READ, // Read accossor or getter. Read value from a variable/function or find a surrogate for a tuple. Normally used in method parameters.
        WRITE, // Assignment, write accessor or setter. Write value to an existing variable/function (do nothing if it does not exist). Normally is used for assignment. 
        UPDATE, // Update value by applying some operation. Normally is used for affecting directly a target rather than loading it, changing and then storing.
        APPEND, // Append a value if it does not exist and write it if does exist. The same as write except that a new element can be added
        INSERT, // The same as append except that a position is specified
        ALLOC, // For variables and functions as a whole storage object in the context. Is not it APPEND/INSERT?
        FREE,

        PROCEDURE, // Generic procedure call including system calls

        OPERATION, // Built-in operation like plus and minus

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

    public class Variable : CsVariable
    {
        protected bool isNull;
        object Value;


        //
        // CsVariable interface
        //

        public string TypeName { get; set; }
        public CsTable TypeTable { get; set; }

        public string Name { get; set; }

        public bool IsNull()
        {
            return isNull;
        }

        public object GetValue()
        {
            return isNull ? null : Value;
        }

        public void SetValue(object value)
        {
            if (value == null)
            {
                Value = null;
                isNull = true;
            }
            else
            {
                Value = value;
                isNull = false;
            }
        }

        public void NullifyValue()
        {
            isNull = true;
        }

        public Variable(string name, string type)
        {
            Name = name;
            TypeName = type;

            isNull = true;
            Value = null;
        }

        public Variable(string name, CsTable type)
        {
            Name = name;
            TypeName = type.Name;
            TypeTable = type;

            isNull = true;
            Value = null;
        }
    }

}
