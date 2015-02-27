using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Offset = System.Int32;
using System.Reflection;

namespace Com.Model
{
    // Represents a function definition in terms of other functions and provides its run-time interface.
    // Main unit of the representation is a triple: (Type) Name = Value. 
    public class ExprNode : TreeNode<ExprNode>, ComJson
    {
        public CultureInfo CultureInfo = new System.Globalization.CultureInfo("en-US");
        public int ObjectToInt32(object val) { return Convert.ToInt32(val, CultureInfo); }
        public double ObjectToDouble(object val) { return Convert.ToDouble(val, CultureInfo); }
        public decimal ObjectToDecimal(object val) { return Convert.ToDecimal(val, CultureInfo); }
        public string ObjectToString(object val) { return Convert.ToString(val, CultureInfo); }
        public bool ObjectToBoolean(object val) { return Convert.ToBoolean(val, CultureInfo); }
        public DateTime ObjectToDateTime(object val) { return Convert.ToDateTime(val, CultureInfo); }

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

        // It specifies the scope where the name is defined and is used normally as a package name for native method calls
        public string NameSpace { get; set; }
        
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
        public MethodInfo Method { get; set; }
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
        public virtual void Resolve(Workspace workspace, List<ComVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                int intValue = 0;
                double doubleValue = 0.0;

                //
                // Resolve string into object and store in the result. Derive the type from the format. 
                //
                // About conversion from string: http://stackoverflow.com/questions/3965871/c-sharp-generic-string-parse-to-any-object
                if (int.TryParse(Name, NumberStyles.Integer, CultureInfo, out intValue))
                {
                    Result.TypeName = "Integer";
                    Result.SetValue(intValue);
                }
                else if (double.TryParse(Name, NumberStyles.Float, CultureInfo, out doubleValue))
                {
                    Result.TypeName = "Double";
                    Result.SetValue(doubleValue);
                }
                else // Cannot parse means string
                {
                    Result.TypeName = "String";
                    Result.SetValue(Name);
                }

                Result.Resolve(workspace);
            }
            else if (Operation == OperationType.TUPLE)
            {
                //
                // Resolve this (tuples are resolved through the parent which must be resolved before children)
                // In TUPLE, Name denotes a function from the parent (input) to this node (output) 
                //

                //
                // 1. Resolve type table name 
                //
                Result.Resolve(workspace);

                //
                // 2. Resolve Name into a column object (a function from the parent to this node)
                //
                ExprNode parentNode = (ExprNode)Parent;
                if (parentNode == null)
                {
                    ; // Nothing to do
                }
                else if (parentNode.Operation == OperationType.TUPLE) // This tuple in another tuple
                {
                    if (parentNode.Result.TypeTable != null && !string.IsNullOrEmpty(Name))
                    {
                        ComColumn col = parentNode.Result.TypeTable.GetColumn(Name);

                        if (col != null) // Column resolved 
                        {
                            Column = col;

                            // Check and process type information 
                            if (Result.TypeTable == null)
                            {
                                Result.SchemaName = col.Output.Schema.Name;
                                Result.TypeName = col.Output.Name;
                                Result.TypeSchema = col.Output.Schema;
                                Result.TypeTable = col.Output;
                            }
                            else if (Result.TypeTable != col.Output)
                            {
                                ; // ERROR: Output type of the column must be the same as this node result type
                            }
                        }
                        else // Column not found 
                        {
                            // Append a new column (schema change, e.g., if function output structure has to be propagated)
                            // TODO:
                        }
                    }
                }
                else // This tuple in some other node, e.g, argument or value
                {
                    ; // Is it a valid situation?
                }

                //
                // Resolve children (important: after the tuple itself, because this node will be used)
                //
                foreach (TreeNode<ExprNode> childNode in Children)
                {
                    childNode.Item.Resolve(workspace, variables);
                }
            }
            else if (Operation == OperationType.CALL)
            {
                //
                // Resolve children (important: before this node because this node uses children) 
                // In CALL, Name denotes a function from children (input) to this node (output) 
                //
                foreach (TreeNode<ExprNode> childNode in Children)
                {
                    childNode.Item.Resolve(workspace, variables);
                }

                //
                // 1. Resolve type table name
                //
                Result.Resolve(workspace);

                //
                // 2. Resolve Name into a column object, variable, procedure or whatever object that will return a result (children must be resolved before)
                //
                ExprNode methodChild = GetChild("method"); // Get column name
                ExprNode thisChild = GetChild("this"); // Get column input (while this node is output)
                int childCount = Children.Count;

                if( !string.IsNullOrEmpty(NameSpace) ) // External name space (java call, c# call etc.) 
                {
                    string className = NameSpace.Trim();
                    if(NameSpace.StartsWith("call:")) 
                    {
                        className = className.Substring(3).Trim();
                    }
                    Type clazz = null;
                    try {
                        clazz = Type.GetType(className);
                    } catch (Exception e) {
                        Console.WriteLine(e.StackTrace);
                    }
                
                    string methodName = Name;
                    MethodInfo[]  methods = null;
                    methods = clazz.GetMethods(); // BindingFlags.Public
                    foreach(MethodInfo m in methods) {
                        if(!m.Name.Equals(methodName)) continue;
                        if(m.IsStatic) {
                            if(m.GetParameters().Length != childCount) continue;
                        }
                        else {
                            if(m.GetParameters().Length + 1 != childCount) continue;
                        }
                    
                        Method = m;
                        break;
                    }
                }	
                else if (childCount == 0) // It is a variable (or it is a function but a child is ommited and has to be reconstructed)
                {
                    // Try to resolve as a variable (including this variable). If success then finish.
                    ComVariable var = variables.FirstOrDefault(v => StringSimilarity.SameColumnName(v.Name, Name));

                    if (var != null) // Resolved as a variable
                    {
                        Result.SchemaName = var.SchemaName;
                        Result.TypeName = var.TypeName;
                        Result.TypeSchema = var.TypeSchema;
                        Result.TypeTable = var.TypeTable;

                        Variable = var;
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

                        thisChild.Result.SchemaName = thisVar.SchemaName;
                        thisChild.Result.TypeName = thisVar.TypeName;
                        thisChild.Result.TypeSchema = thisVar.TypeSchema;
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

                        if (col != null) // Column resolved
                        {
                            Column = col;

                            // Check and process type information 
                            if (Result.TypeTable == null)
                            {
                                Result.SchemaName = col.Output.Schema.Name;
                                Result.TypeName = col.Output.Name;
                                Result.TypeSchema = col.Output.Schema;
                                Result.TypeTable = col.Output;
                            }
                            else if (Result.TypeTable != col.Output)
                            {
                                ; // ERROR: Output type of the column must be the same as this node result type
                            }

                            AddChild(path);
                        }
                        else // Column not found 
                        {
                            ; // ERROR: failed to resolve symbol 
                        }
                    }
                }
                else if (childCount == 1) // It is a column applied to previous output returned in the child
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

                    if (col != null) // Column resolved
                    {
                        Column = col;

                        // Check and process type information 
                        if (Result.TypeTable == null)
                        {
                            Result.SchemaName = col.Output.Schema.Name;
                            Result.TypeName = col.Output.Name;
                            Result.TypeSchema = col.Output.Schema;
                            Result.TypeTable = col.Output;
                        }
                        else if (Result.TypeTable != col.Output)
                        {
                            ; // ERROR: Output type of the column must be the same as this node result type
                        }
                    }
                    else // Column not found 
                    {
                        ; // ERROR: failed to resolve symbol 
                    }
                }
                else // It is a system procedure or operator (arithmetic, logical etc.)
                {
                    string methodName = this.Name;

                    // TODO: Derive return type. It is derived from arguments by using type conversion rules
                    Result.TypeName = "Double";
                    Result.Resolve(workspace);

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
                foreach (TreeNode<ExprNode> childNode in Children)
                {
                    childNode.Item.Evaluate();
                }

                if (Result.TypeTable.IsPrimitive) // Primitive TUPLE nodes are processed differently
                {
                    Debug.Assert(Children.Count == 1, "Wrong use: a primitive TUPLE node must have one child expression providing its value.");
                    ExprNode childNode = GetChild(0);
                    object val = childNode.Result.GetValue();
                    string targeTypeName = Result.TypeTable.Name;

                    // Copy result from the child expression and convert it to this node type
                    if (val is DBNull)
                    {
                        Result.SetValue(null);
                    }
                    else if (val is string && string.IsNullOrWhiteSpace((string)val))
                    {
                        Result.SetValue(null);
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Integer"))
                    {
                        Result.SetValue(childNode.ObjectToInt32(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Double"))
                    {
                        Result.SetValue(childNode.ObjectToDouble(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Decimal"))
                    {
                        Result.SetValue(childNode.ObjectToDecimal(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "String"))
                    {
                        Result.SetValue(childNode.ObjectToString(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Boolean"))
                    {
                        Result.SetValue(childNode.ObjectToBoolean(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "DateTime"))
                    {
                        Result.SetValue(childNode.ObjectToDateTime(val));
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

                ExprNode child1 = Children.Count > 0 ? (ExprNode) Children[0] : null;
                ExprNode child2 = Children.Count > 1 ? (ExprNode)Children[1] : null;
                
                int intRes;
                double doubleRes;
                bool boolRes = false;
                object objRes = null;

                int childCount = Children.Count;

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
                        ExprNode prevOutput = child1;
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
                        double arg = childNode.ObjectToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes *= arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.DIV)
                {
                    doubleRes = child1.ObjectToDouble(child1.Result.GetValue());
                    for (int i = 1; i < Children.Count; i++)
                    {
                        ExprNode childNode = (ExprNode)Children[i];
                        double arg = childNode.ObjectToDouble(childNode.Result.GetValue());
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
                        double arg = childNode.ObjectToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes += arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.SUB)
                {
                    doubleRes = child1.ObjectToDouble(child1.Result.GetValue());
                    for (int i = 1; i < Children.Count; i++)
                    {
                        ExprNode childNode = (ExprNode)Children[i];
                        double arg = childNode.ObjectToDouble(childNode.Result.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes /= arg;
                    }
                    Result.SetValue(doubleRes);
                }
                else if (Action == ActionType.COUNT)
                {
                    intRes = child1.ObjectToInt32(child1.Result.GetValue());
                    intRes += 1;
                    Result.SetValue(intRes);
                }
                //
                // LEQ, GEQ, GRE, LES,
                //
                else if (Action == ActionType.LEQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.Result.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.Result.GetValue());
                    boolRes = arg1 <= arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.GEQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.Result.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.Result.GetValue());
                    boolRes = arg1 >= arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.GRE)
                {
                    double arg1 = child1.ObjectToDouble(child1.Result.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.Result.GetValue());
                    boolRes = arg1 > arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.LES)
                {
                    double arg1 = child1.ObjectToDouble(child1.Result.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.Result.GetValue());
                    boolRes = arg1 < arg2;
                    Result.SetValue(boolRes);
                }
                //
                // EQ, NEQ
                //
                else if (Action == ActionType.EQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.Result.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.Result.GetValue());
                    boolRes = arg1 == arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.NEQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.Result.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.Result.GetValue());
                    boolRes = arg1 != arg2;
                    Result.SetValue(boolRes);
                }
                //
                // AND, OR
                //
                else if (Action == ActionType.AND)
                {
                    bool arg1 = child1.ObjectToBoolean(child1.Result.GetValue());
                    bool arg2 = child2.ObjectToBoolean(child2.Result.GetValue());
                    boolRes = arg1 && arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.OR)
                {
                    bool arg1 = child1.ObjectToBoolean(child1.Result.GetValue());
                    bool arg2 = child2.ObjectToBoolean(child2.Result.GetValue());
                    boolRes = arg1 || arg2;
                    Result.SetValue(boolRes);
                }
                else if (Action == ActionType.PROCEDURE)
                {
                    Type[] types = Method.GetParameters().Select(x => x.ParameterType).ToArray();
                
                    object thisObj = null;
                    object[] args = null;

                    // Preparing parameters for the procedure
                    if(Method.IsStatic) {
                        args = new object[childCount]; 
                        for(int i=0; i<childCount; i++) {
                            args[i] = ((ExprNode)Children[0]).Result.GetValue();
                        }
                    }
                    else {
                        if(childCount > 0) thisObj = ((ExprNode)Children[0]).Result.GetValue();

                        args = new object[childCount - 1]; 
                        for(int i=0; i<childCount-1; i++) {
                            args[i] = ((ExprNode)Children[i+1]).Result.GetValue();
                        }
                    }
                
                    // Dynamic invocation
                    try {
                        objRes = Method.Invoke(thisObj, args);
                    }
                    catch (Exception e) {
                        Console.WriteLine(e.StackTrace);
                    }            	
                
                    Result.SetValue(objRes);
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

                    child.Result.SchemaName = seg.Output.Schema.Name;
                    child.Result.TypeName = seg.Output.Name;
                    child.Result.TypeSchema = seg.Output.Schema;
                    child.Result.TypeTable = seg.Output;

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

                thisNode.Result.SchemaName = path.Input.Schema.Name;
                thisNode.Result.TypeName = path.Input.Name;
                thisNode.Result.TypeSchema = path.Input.Schema;
                thisNode.Result.TypeTable = path.Input;

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

            if (path.Input.Schema is SchemaCsv) // Access via column index
            {
                DimCsv seg = (DimCsv)path.FirstSegment;

                expr = new CsvExprNode();
                expr.Operation = OperationType.CALL;
                expr.Action = ActionType.READ;
                expr.Name = seg.Name;

                expr.Result.SchemaName = seg.Output.Schema.Name;
                expr.Result.TypeName = seg.Output.Name;
                expr.Result.TypeSchema = seg.Output.Schema;
                expr.Result.TypeTable = seg.Output;

                expr.CultureInfo = ((SetCsv)seg.Input).CultureInfo;
            }
            else if (path.Input.Schema is SchemaOledb) // Access via relational attribute
            {
                expr = new OledbExprNode();
                expr.Operation = OperationType.CALL;
                expr.Action = ActionType.READ;
                expr.Name = path.Name;

                expr.Result.SchemaName = path.Input.Schema.Name;
                expr.Result.TypeName = path.Input.Name;
                expr.Result.TypeSchema = path.Input.Schema;
                expr.Result.TypeTable = path.Input;
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

                    node.Result.SchemaName = seg.Output.Schema.Name;
                    node.Result.TypeName = seg.Output.Name;
                    node.Result.TypeSchema = seg.Output.Schema;
                    node.Result.TypeTable = seg.Output;

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

                thisNode.Result.SchemaName = path.Input.Schema.Name;
                thisNode.Result.TypeName = path.Input.Name;
                thisNode.Result.TypeSchema = path.Input.Schema;
                thisNode.Result.TypeTable = path.Input;

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

            valueNode.Result.SchemaName = column.Output.Schema.Name;
            valueNode.Result.TypeName = column.Output.Name;
            valueNode.Result.TypeSchema = column.Output.Schema;
            valueNode.Result.TypeTable = column.Output;

            //
            // A node for computing a result (updated) function value from the current value and new value
            //
            ExprNode expr = new ExprNode();
            expr.Operation = OperationType.CALL;
            expr.Action = aggregation; // SUM etc.
            expr.Name = column.Name;

            expr.Result.SchemaName = column.Output.Schema.Name;
            expr.Result.TypeName = column.Output.Name;
            expr.Result.TypeSchema = column.Output.Schema;
            expr.Result.TypeTable = column.Output;

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

                result.schema_name = Result.SchemaName;
                result.type_name = Result.TypeName;
                result.name = Result.Name;

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
                string resultSchema = (string)resultDef["schema_name"];
                string resultType = (string)resultDef["type_name"];
                string resultName = (string)resultDef["name"];

                Result = new Variable(resultSchema, resultType, resultName);
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
            CultureInfo = Utils.cultureInfo; // Default
            Result = new Variable("", "Void", "return");
        }

    }

    /// <summary>
    /// This class implements functions for accessing Oledb data source as input. 
    /// </summary>
    public class OledbExprNode : ExprNode
    {
        public override void Resolve(Workspace workspace, List<ComVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                base.Resolve(workspace, variables);
            }
            else if (Operation == OperationType.TUPLE)
            {
                base.Resolve(workspace, variables);
            }
            else if (Operation == OperationType.CALL)
            {
                base.Resolve(workspace, variables);
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
        public override void Resolve(Workspace workspace, List<ComVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                base.Resolve(workspace, variables);
            }
            else if (Operation == OperationType.TUPLE)
            {
                base.Resolve(workspace, variables);
            }
            else if (Operation == OperationType.CALL)
            {
                base.Resolve(workspace, variables);
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
