using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Offset = System.Int32;

namespace Com.Model
{
    // Represents a function definition in terms of other functions and provides its run-time interface.
    // Main unit of the representation is a triple: (Type) Name = Value. 
    public class ExprNode : TreeNode<ExprNode>, ComJson
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
        // It could be ComColumnEvaluator (at least for Dim storage) so that we directly access values at run-time. 
        // Alternatively, the whole node implements this interface
        public ComColumn Column { get; set; }
        public ComVariable Variable { get; set; }
        // Action type. A modifier that helps to choose the function variation
        public ActionType Action { get; set; }

        //
        // Result value computed at run-time
        //

        // Return run-time value after processing this node to be used by the parent. It must have the specified type.
        public ComVariable Result { get; set; }

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
        public virtual void Resolve(ComSchema schema, List<ComVariable> variables)
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
                            ComColumn col = parent.Result.TypeTable.GetColumn(Name);
                            Column = col;
                            Result.TypeTable = col.Output;
                            Result.TypeName = col.Output.Name;
                        }
                    }
                    else // Tuple in some other node, e.g, argument or value
                    {
                        if (parent.Result.TypeTable != null && !string.IsNullOrEmpty(Name))
                        {
                            ComColumn col = parent.Result.TypeTable.GetColumn(Name);
                            Column = col;
                            Result.TypeTable = col.Output;
                            Result.TypeName = col.Output.Name;
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
                    Result.TypeTable = schema.GetSubTable(Result.TypeName);
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
                    ComVariable var = variables.FirstOrDefault(v => StringSimilarity.SameColumnName(v.Name, Name));

                    if (var != null) // Resolved as a variable
                    {
                        Variable = var;

                        Result.TypeName = var.TypeName;
                        Result.TypeTable = var.TypeTable;
                    }
                    else // Cannot resolve as a variable - try resolve as a column name starting from 'this' table and then continue to super tables
                    {
                        //
                        // Start from 'this' node bound to 'this' variable
                        //
                        ComVariable thisVar = variables.FirstOrDefault(v => StringSimilarity.SameColumnName(v.Name, "this"));

                        thisChild = new ExprNode();
                        thisChild.Operation = OperationType.CALL;
                        thisChild.Action = ActionType.READ;
                        thisChild.Name = "this";
                        thisChild.Result.TypeName = thisVar.TypeName;
                        thisChild.Result.TypeTable = thisVar.TypeTable;
                        thisChild.Variable = thisVar;

                        ExprNode path = thisChild;
                        ComTable contextTable = thisChild.Result.TypeTable;
                        ComColumn col = null;

                        while (contextTable != null)
                        {
                            //
                            // Try to resolve name
                            //
                            col = contextTable.GetColumn(Name);

                            if (col != null) // Resolved
                            {
                                break;
                            }

                            //
                            // Iterator. Find super-column in the current context (where we have just failed to resolve the name)
                            //
                            ComColumn superColumn = contextTable.SuperColumn;
                            contextTable = contextTable.SuperTable;

                            if (contextTable == null || contextTable == contextTable.Schema.Root)
                            {
                                break; // Root. No super dimensions anymore
                            }

                            //
                            // Build next super-access node and resolve it
                            //
                            ExprNode superNode = new ExprNode();
                            superNode.Operation = OperationType.CALL;
                            superNode.Action = ActionType.READ;
                            superNode.Name = superColumn.Name;
                            superNode.Column = superColumn;

                            superNode.AddChild(path);
                            path = superNode;
                        }

                        if (col != null) // Successfully resolved. Store the results.
                        {
                            Column = col;

                            Result.TypeName = col.Output.Name;
                            Result.TypeTable = col.Output;

                            AddChild(path);
                        }
                        else // Failed to resolve symbol
                        {
                            ; // ERROR: failed to resolve symbol in this and parent contexts
                        }
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
                    ComColumn col = outputChild.Result.TypeTable.GetColumn(methodName);
                    Column = col;

                    Result.TypeName = col.Output.Name;
                    Result.TypeTable = col.Output;
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
                    if (val is DBNull || (val is string && string.IsNullOrWhiteSpace((string)val))) 
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
                        Offset input = Result.TypeTable.Data.Find(this);

                        if (input < 0 || input >= Result.TypeTable.Data.Length) // Not found
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
                        Offset input = Result.TypeTable.Data.Find(this); // Uniqueness constraint: check if it exists already

                        if (input < 0 || input >= Result.TypeTable.Data.Length) // Not found
                        {
                            input = Result.TypeTable.Data.Append(this); // Append new
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

                int intRes;
                double doubleRes;
                bool boolRes = false;

                if (Action == ActionType.READ)
                {
                    if (this is CsvExprNode) // It is easier to do it here rather than (correctly) in the extension
                    {
                        // Find current Row object
                        ExprNode thisNode = GetChild("this");
                        string[] input = (string[])thisNode.Result.GetValue();

                        // Use attribute name or number by applying it to the current Row object (offset is not used)
                        int attributeIndex = ((DimCsv)Column).ColumnIndex;
                        object output = input[attributeIndex];
                        Result.SetValue(output);
                    }
                    else if (this is OledbExprNode) // It is easier to do it here rather than (correctly) in the extension
                    {
                        // Find current Row object
                        ExprNode thisNode = GetChild("this");
                        DataRow input = (DataRow)thisNode.Result.GetValue();

                        // Use attribute name or number by applying it to the current Row object (offset is not used)
                        string attributeName = Name;
                        object output = input[attributeName];
                        Result.SetValue(output);
                    }
                    else if (Column != null) 
                    {
                        ExprNode prevOutput = GetChild(0);
                        Offset input = (Offset)prevOutput.Result.GetValue();
                        object output = Column.Data.GetValue(input);
                        Result.SetValue(output);
                    }
                    else if (Variable != null)
                    {
                        object result = Variable.GetValue();
                        Result.SetValue(result);
                    }
                }
                else if (Action == ActionType.UPDATE) // Compute new value for the specified offset using a new value in the variable
                {
                }
                //
                // MUL, DIV, ADD, SUB, 
                //
                else if (Action == ActionType.MUL)
                {
                    doubleRes = 1.0;
                    foreach (ExprNode childNode in Children)
                    {
                        double arg = Convert.ToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes *= arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.DIV)
                {
                    doubleRes = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    for (int i = 1; i < Children.Count; i++)
                    {
                        double arg = Convert.ToDouble(((ExprNode)Children[i]).Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes /= arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.ADD)
                {
                    doubleRes = 0.0;
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
                    doubleRes = Convert.ToDouble(((ExprNode)Children[0]).Result.GetValue());
                    for (int i = 1; i < Children.Count; i++)
                    {
                        double arg = Convert.ToDouble(((ExprNode)Children[i]).Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes /= arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.COUNT)
                {
                    intRes = Convert.ToInt32(((ExprNode)Children[0]).Result.GetValue());
                    intRes += 1;
                    Result.SetValue(intRes);
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
        /// Return all nodes that use the specified column. If the parameter is null then all nodes are returned that use any column. 
        /// By the use we mean dependency, that is, this expression result depends on this column as a function. 
        /// The expressions have to be resolved because we need object references rather than names.
        /// </summary>
        public List<ExprNode> Find(ComColumn column)
        {
            var res = new List<ExprNode>();

            Action<ExprNode> visitor = delegate(ExprNode node)
            {
                if(node.Column == null) return; // The node does not use a column (e.g., it is an operator or an external function)

                if (column == null || column == node.Column)
                {
                    if(!res.Contains(node)) res.Add(node);
                }
            };

            this.Traverse(visitor); // Visit all nodes 

            return res;
        }

        /// <summary>
        /// Return all nodes that use the specified table. If the parameter is null then all nodes are returned that use any table. 
        /// By the use we mean dependency, that is, this expression result depends on this table (if the table changes then this node must be re-evaluated). 
        /// The expressions have to be resolved because we need object references rather than names.
        /// </summary>
        public List<ExprNode> Find(ComTable table)
        {
            var res = new List<ExprNode>();

            Action<ExprNode> visitor = delegate(ExprNode node)
            {
                if (node.Result == null || node.Result.TypeTable == null) return; // The node does not use a table

                if (table == null || table == node.Result.TypeTable)
                {
                    if (!res.Contains(node)) res.Add(node);
                }
            };

            this.Traverse(visitor); // Visit all nodes 

            return res;
        }

        /// <summary>
        /// // Assuming that this expression is a tuple, add a new branch to the tuple tree based on the specified dimension path. 
        /// Ensure that the specified path exists in the tuple expression tree by finding and creating if not found the nodes corresponding to path segments.
        /// Return the leaf node of the tuple branch (this variable if it is requested) which correponds to the first segment in the path.
        /// </summary>
        public ExprNode AddToTuple(DimPath path, bool withThisVariable) 
        {
            // Question: what operation whould be in the leaf: TUPLE, VALUE or whatever

            Debug.Assert(path != null && path.Input == Result.TypeTable, "Wrong use: path must start from the node (output set) it is applied to.");

            if (path.Segments == null || path.Segments.Count == 0) return this;

            ExprNode node = this;
            for (int i = 0; i < path.Segments.Count; i++) // We add all segments sequentially
            {
                ComColumn seg = path.Segments[i];
                ExprNode child = node.GetChild(seg.Name); // Try to find a child corresponding to this segment

                if (child == null) // Not found. Add a new child corresponding to this segment
                {
                    child = new ExprNode();
                    child.Operation = OperationType.TUPLE;
                    child.Action = this.Action; // We inherit the action from the tuple root
                    child.Name = seg.Name;
                    child.Result.TypeTable = seg.Output;
                    child.Result.TypeName = seg.Output.Name;

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

                thisNode.Result.TypeTable = path.Input;
                thisNode.Result.TypeName = path.Input.Name;

                node.AddChild(thisNode);
                node = thisNode;
            }

            return node;
        }

        /// <summary>
        /// If this variable is requested then the return expression will create at one node. 
        /// Return the last node of the expression (this node if requested) which corresponds to the first segment of the path.
        /// </summary>
        public static ExprNode CreateReader(DimPath path, bool withThisVariable) 
        {
            ExprNode expr = null;

            if (path.Input.Schema is SetTopCsv) // Access via column index
            {
                DimCsv seg = (DimCsv)path.FirstSegment;

                expr = new CsvExprNode();
                expr.Operation = OperationType.CALL;
                expr.Action = ActionType.READ;
                expr.Name = seg.Name;
                expr.Result.TypeTable = seg.Output;
                expr.Result.TypeName = seg.Output.Name;
            }
            else if (path.Input.Schema is SetTopOledb) // Access via relational attribute
            {
                expr = new OledbExprNode();
                expr.Operation = OperationType.CALL;
                expr.Action = ActionType.READ;
                expr.Name = path.Name;
                expr.Result.TypeTable = path.Input;
                expr.Result.TypeName = path.Input.Name;
            }
            else // Access via function/column composition
            {
                for (int i = path.Segments.Count() - 1; i >= 0; i--)
                {
                    ComColumn seg = path.Segments[i];

                    ExprNode node = new ExprNode();
                    node.Operation = OperationType.CALL;
                    node.Action = ActionType.READ;
                    node.Name = seg.Name;
                    node.Result.TypeTable = seg.Output;
                    node.Result.TypeName = seg.Output.Name;

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

                thisNode.Result.TypeTable = path.Input;
                thisNode.Result.TypeName = path.Input.Name;

                if (expr != null)
                {
                    expr.AddChild(thisNode);
                    expr = thisNode;
                }
            }

            return expr;
        }

        /// <summary>
        /// Create a read expression for the specified column. 
        /// This expression will read one variables from the context: 'this' typed by the column lesser set.
        /// </summary>
        public static ExprNode CreateReader(ComColumn column, bool withThisVariable)
        {
            return CreateReader(new DimPath(column), withThisVariable);
        }

        /// <summary>
        /// Create an upate expression for the specified aggregation column and standard aggregation function. 
        /// This expression will read two variables from the context: 'this' typed by the column lesser set and 'value' typed by the column greater set.
        /// </summary>
        public static ExprNode CreateUpdater(ComColumn column, string aggregationFunction)
        {
            ActionType aggregation;
            if (aggregationFunction.Equals("COUNT"))
            {
                aggregation = ActionType.COUNT;
            }
            else if (aggregationFunction.Equals("SUM"))
            {
                aggregation = ActionType.ADD;
            }
            else if (aggregationFunction.Equals("MUL"))
            {
                aggregation = ActionType.MUL;
            }
            else
            {
                throw new NotImplementedException("Aggregation function is not implemented.");
            }

            //
            // A node for reading the current function value at the offset in 'this' variable
            //
            ExprNode currentValueNode = (ExprNode)CreateReader(column, true).Root;

            //
            // A node for reading a new function value from the well-known variable
            //
            ExprNode valueNode = new ExprNode();
            valueNode.Name = "value";
            valueNode.Operation = OperationType.CALL;
            valueNode.Action = ActionType.READ;

            valueNode.Result.TypeTable = column.Output;
            valueNode.Result.TypeName = column.Output.Name;

            //
            // A node for computing a result (updated) function value from the current value and new value
            //
            ExprNode expr = new ExprNode();
            expr.Operation = OperationType.CALL;
            expr.Action = aggregation; // SUM etc.
            expr.Name = column.Name;

            expr.Result.TypeTable = column.Output;
            expr.Result.TypeName = column.Output.Name;

            // Two arguments in child nodes
            expr.AddChild(currentValueNode);
            expr.AddChild(valueNode);

            return expr;
        }

        #region ComJson serialization

        public virtual void ToJson(JObject json)
        {
            // We do not use the base TreeNode serialization
            dynamic expr = json;

            expr.operation = Operation;
            expr.name = Name;
            expr.action = Action;

            // Result
            if (Result  != null) // Manually serialize
            {
                dynamic result = new JObject();

                result.name = Result.Name;
                result.type_name = Result.TypeName;

                expr.result = result;
            }

            // List of children
            expr.children = new JArray() as dynamic;
            foreach (var node in Children)
            {
                dynamic child = Utils.CreateJsonFromObject(node);
                ((ExprNode)node).ToJson(child);
                expr.children.Add(child);
            }
        }

        public virtual void FromJson(JObject json, Workspace ws)
        {
            // We do not use the base TreeNode serialization

            // Set its parameters
            Operation = (OperationType)(int)json["operation"];
            Name = (string)json["name"];
            Action = (ActionType)(int)json["action"];

            // Result
            JObject resultDef = (JObject)json["result"];
            if (resultDef != null)
            {
                string resultName = (string)resultDef["name"];
                string resultType = (string)resultDef["type_name"];

                Result = new Variable(resultName, resultType);
            }

            // List of children
            foreach (JObject child in json["children"])
            {
                ExprNode childNode = (ExprNode)Utils.CreateObjectFromJson(child);
                if (childNode != null)
                {
                    this.AddChild(childNode);
                    childNode.FromJson(child, ws); // Recursion
                }
            }
        }

        #endregion

        public override string ToString()
        {
            return Name;
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
        public override void Resolve(ComSchema schema, List<ComVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                base.Resolve(schema, variables);
            }
            else if (Operation == OperationType.TUPLE)
            {
                base.Resolve(schema, variables);
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

    /// <summary>
    /// This class implements functions for accessing Csv data source as input. 
    /// </summary>
    public class CsvExprNode : ExprNode
    {
        public override void Resolve(ComSchema schema, List<ComVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                base.Resolve(schema, variables);
            }
            else if (Operation == OperationType.TUPLE)
            {
                base.Resolve(schema, variables);
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

        // Arithmetics
        COUNT,
        // ADD ("SUM")
        // MUL ("MUL")
    }

}
