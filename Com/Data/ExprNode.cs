using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using System.Reflection;

using Newtonsoft.Json.Linq;

using Com.Schema;
using Com.Schema.Csv;
using Com.Schema.Rel;
using Com.Data;
using Com.Utils;

using Rowid = System.Int32;

namespace Com.Data
{
    // Represents a function definition in terms of other functions and provides its run-time interface.
    // Main unit of the representation is a triple: (Type) Name = Value. 
    public class ExprNode : TreeNode<ExprNode>, DcJson
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
        public DcColumn Column { get; set; }
        public DcVariable Variable { get; set; }
        public MethodInfo Method { get; set; }
        // Action type. A modifier that helps to choose the function variation
        public ActionType Action { get; set; }

        public DcTableWriter TableWriter { get; set; }

        // User-oriented types of columns: arithmetcis/primitive, link/non-primitive/tuple (mapping),, import/export/generation (link w. append), aggregation
        [Obsolete("This annotation is reduntant at this level. Redesign. Maybe introduce higher level user-oriented annoations with some user-value: import/export etc.")]
        public ColumnDefinitionType DefinitionType
        {
            get
            {
                // Currently, we use the following mapping:
                // TUPLE -> LINK
                // CALL AGGREGATE -> AGGREGATION
                // else -> ARITHMETIC
                if (Operation == OperationType.TUPLE)
                {
                    return ColumnDefinitionType.LINK;
                }
                else if (Operation == OperationType.CALL && Name.Equals("AGGREGATE", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ColumnDefinitionType.AGGREGATION;
                }
                else
                {
                    return ColumnDefinitionType.ARITHMETIC;
                }
            }
        } 

        //
        // Result value computed at run-time
        //

        // Return run-time value after processing this node to be used by the parent. It must have the specified type.
        public DcVariable OutputVariable { get; set; }

        /// <summary>
        /// Create or update all the schema elements that are used in this expression. 
        /// Store direct object references to these named elements so that they can be directly accessed during data evaluation without name resolution. 
        /// Element that needs to be created/updated or resolved: tables (types), columns (functions), variables, procedures, operators. 
        /// Notes:
        /// - Schema elements are created only if they are used in tuples which is treated as a table structure specification 
        /// - Resolution starts from some well-known point and then propagates along the structure. For example, it is specified for 'this' (input) variable read by CALL nodes by propagating to parent nodes, and 'result' (output) in TUPLE nodes which is then propagated to its child nodes. 
        /// - An expression can be resolved against two schemas (not one) because it connects two sets (input and output) that can belong to different schemas. Particularly, it happens for import/export dimensions. 
        ///   - Types in tuples depend on the parent type. Columns (variables, procedures etc.) depend on the children. 
        /// - Types can be already resolved during expression creation. Particularly, in the case if it is created from a mapping object. 
        /// </summary>
        public virtual void EvaluateAndResolveSchema(DcWorkspace workspace, List<DcVariable> variables)
        {
            // PROBLEM: tables in formula might have no direct indication of their schema which is needed to resolve table names
            // Schema for nodes has to be derived from other nodes (before resovling the corresponding tables)
            // Generally, schema is manually specified for 
            // - 'this' variable which is then read by CALL columns with output in their known schema and set (according to the column spec). This propagates from children to parents
            // - set in TUPLE node which is then used to resolve child nodes which provide schemas for their outputs. This propagates from parents to children. 
            // TODO: We must set right schema before resolving the table in the variable because variable itself has no idea what is the schema of its table (type)
            // Expressions (their result variable types) can belong to different schemas (for import/export). How can we specify different schemas for different nodes? 1. Inherit from parent TUPLE and eventually from root 'result' variable. 2. Inherit from child CALL and eventually from leaf 'this' variable 3. If it is constant value then we do not care what is the schema but maybe formally we have to set mashup.

            if (Operation == OperationType.VALUE)
            {
                int intValue = 0;
                double doubleValue = 0.0;

                OutputVariable.Resolve(workspace);

                //
                // Resolve string into object and store in the result. Derive the type from the format. 
                //
                // About conversion from string: http://stackoverflow.com/questions/3965871/c-sharp-generic-string-parse-to-any-object
                if (int.TryParse(Name, NumberStyles.Integer, CultureInfo, out intValue))
                {
                    OutputVariable.TypeName = "Integer";
                    OutputVariable.SetValue(intValue);
                }
                else if (double.TryParse(Name, NumberStyles.Float, CultureInfo, out doubleValue))
                {
                    OutputVariable.TypeName = "Double";
                    OutputVariable.SetValue(doubleValue);
                }
                else // Cannot parse means string
                {
                    OutputVariable.TypeName = "String";
                    OutputVariable.SetValue(Name);
                }
            }
            else if (Operation == OperationType.TUPLE)
            {
                //
                // Resolve this (tuples are resolved through the parent which must be resolved before children)
                // In TUPLE, Name denotes a function from the parent (input) to this node (output) 
                //

                //
                // Resolve type table name 
                //
                OutputVariable.Resolve(workspace);

                //
                // Resolve Name into a column object (a function from the parent to this node)
                //
                ExprNode parentNode = (ExprNode)Parent;
                if (parentNode == null)
                {
                    ; // Nothing to do
                }
                else if (parentNode.Operation == OperationType.TUPLE) // This tuple in another tuple
                {
                    if (parentNode.OutputVariable.TypeTable != null && !string.IsNullOrEmpty(Name))
                    {
                        DcColumn col = parentNode.OutputVariable.TypeTable.GetColumn(Name);

                        if (col != null) // Column resolved 
                        {
                            // Check and process type information 
                            if (OutputVariable.TypeTable == null)
                            {
                                OutputVariable.SchemaName = col.Output.Schema.Name;
                                OutputVariable.TypeName = col.Output.Name;
                                OutputVariable.TypeSchema = col.Output.Schema;
                                OutputVariable.TypeTable = col.Output;
                            }
                            else if (OutputVariable.TypeTable != col.Output)
                            {
                                ; // ERROR: Output type of the column must be the same as this node result type
                            }
                        }
                        else // Column not found 
                        {
                                // Append a new column (schema change, e.g., if function output structure has to be propagated)
                                DcTable input = parentNode.OutputVariable.TypeTable;
                                DcTable output = OutputVariable.TypeTable;
                                string columnName = Name;

                                col = input.Schema.CreateColumn(columnName, input, output, false);
                                col.Add();
                        }

                        Column = col;
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
                    childNode.Item.EvaluateAndResolveSchema(workspace, variables);
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
                    childNode.Item.EvaluateAndResolveSchema(workspace, variables);
                }

                //
                // Resolve type table name
                //
                OutputVariable.Resolve(workspace);

                //
                // Resolve Name into a column object, variable, procedure or whatever object that will return a result (children must be resolved before)
                //
                ExprNode methodChild = GetChild("method"); // Get column name
                ExprNode thisChild = GetChild("this"); // Get column input (while this node is output)
                int childCount = Children.Count;

                if (!string.IsNullOrEmpty(NameSpace)) // External name space (java call, c# call etc.) 
                {
                    string className = NameSpace.Trim();
                    if (NameSpace.StartsWith("call:"))
                    {
                        className = className.Substring(3).Trim();
                    }
                    Type clazz = null;
                    try
                    {
                        clazz = Type.GetType(className);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.StackTrace);
                    }

                    string methodName = Name;
                    MethodInfo[] methods = null;
                    methods = clazz.GetMethods(); // BindingFlags.Public
                    foreach (MethodInfo m in methods)
                    {
                        if (!m.Name.Equals(methodName)) continue;
                        if (m.IsStatic)
                        {
                            if (m.GetParameters().Length != childCount) continue;
                        }
                        else
                        {
                            if (m.GetParameters().Length + 1 != childCount) continue;
                        }

                        Method = m;
                        break;
                    }
                }
                else if (childCount == 0) // It is a variable (or it is a function but a child is ommited and has to be reconstructed)
                {
                    // Try to resolve as a variable (including this variable). If success then finish.
                    DcVariable var = variables.FirstOrDefault(v => StringSimilarity.SameColumnName(v.Name, Name));

                    if (var != null) // Resolved as a variable
                    {
                        OutputVariable.SchemaName = var.SchemaName;
                        OutputVariable.TypeName = var.TypeName;
                        OutputVariable.TypeSchema = var.TypeSchema;
                        OutputVariable.TypeTable = var.TypeTable;

                        Variable = var;
                    }
                    else // Cannot resolve as a variable - try resolve as a column name starting from 'this' table and then continue to super tables
                    {
                        //
                        // Start from 'this' node bound to 'this' variable
                        //
                        DcVariable thisVar = variables.FirstOrDefault(v => StringSimilarity.SameColumnName(v.Name, "this"));

                        thisChild = new ExprNode();
                        thisChild.Operation = OperationType.CALL;
                        thisChild.Action = ActionType.READ;
                        thisChild.Name = "this";

                        thisChild.OutputVariable.SchemaName = thisVar.SchemaName;
                        thisChild.OutputVariable.TypeName = thisVar.TypeName;
                        thisChild.OutputVariable.TypeSchema = thisVar.TypeSchema;
                        thisChild.OutputVariable.TypeTable = thisVar.TypeTable;

                        thisChild.Variable = thisVar;

                        ExprNode path = thisChild;
                        DcTable contextTable = thisChild.OutputVariable.TypeTable;
                        DcColumn col = null;

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
                            DcColumn superColumn = contextTable.SuperColumn;
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
                            if (OutputVariable.TypeTable == null)
                            {
                                OutputVariable.SchemaName = col.Output.Schema.Name;
                                OutputVariable.TypeName = col.Output.Name;
                                OutputVariable.TypeSchema = col.Output.Schema;
                                OutputVariable.TypeTable = col.Output;
                            }
                            else if (OutputVariable.TypeTable != col.Output)
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

                    DcColumn col = outputChild.OutputVariable.TypeTable.GetColumn(methodName);

                    if (col != null) // Column resolved
                    {
                        Column = col;

                        // Check and process type information 
                        if (OutputVariable.TypeTable == null)
                        {
                            OutputVariable.SchemaName = col.Output.Schema.Name;
                            OutputVariable.TypeName = col.Output.Name;
                            OutputVariable.TypeSchema = col.Output.Schema;
                            OutputVariable.TypeTable = col.Output;
                        }
                        else if (OutputVariable.TypeTable != col.Output)
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
                    OutputVariable.TypeName = "Double";
                    OutputVariable.Resolve(workspace);

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

        public virtual void EvaluateBegin()
        {
            if (Operation == OperationType.VALUE)
            {

            }
            else if (Operation == OperationType.TUPLE) // SET, TABLE, NON-PRIMITIVE, ...
            {
                //
                // Open files/databases used from within expressions.
                //
                if (OutputVariable.TypeTable != null && !OutputVariable.TypeTable.IsPrimitive && OutputVariable.TypeTable.Schema is SchemaCsv) // Prepare to writing to a csv file during evaluation
                {
                    SchemaCsv csvSchema = (SchemaCsv)OutputVariable.TypeTable.Schema;
                    SetCsv csvOutput = (SetCsv)OutputVariable.TypeTable;

                    // Ensure that all parameters are correct
                    // Set index for all columns that have to written to the file
                    int index = 0;
                    for (int i = 0; i < csvOutput.Columns.Count; i++)
                    {
                        if (!(csvOutput.Columns[i] is DimCsv)) continue;

                        DimCsv col = (DimCsv)csvOutput.Columns[i];
                        if (col.IsSuper)
                        {
                            col.ColumnIndex = -1; // Will not be written 
                        }
                        else if (col.Output.Schema != col.Input.Schema) // Import/export columns do not store data
                        {
                            col.ColumnIndex = -1;
                        }
                        else
                        {
                            col.ColumnIndex = index;
                            index++;
                        }
                    }
                }
                else if (OutputVariable.TypeTable != null && !OutputVariable.TypeTable.IsPrimitive && OutputVariable.TypeTable.Schema is SchemaOledb) // Prepare to writing to a database during evaluation
                {
                }

                // Open file for writing
                if (OutputVariable.TypeTable != null && !OutputVariable.TypeTable.IsPrimitive)
                {
                    TableWriter = OutputVariable.TypeTable.GetData().GetTableWriter();
                    TableWriter.Open();
                }

                //
                // Evaluate children
                //
                foreach (TreeNode<ExprNode> childNode in Children)
                {
                    childNode.Item.EvaluateBegin();
                }
            }
            else if (Operation == OperationType.CALL) // FUNCTION, COLUMN, PRIMITIVE, ...
            {
                //
                // Evaluate children
                //
                foreach (ExprNode childNode in Children)
                {
                    childNode.EvaluateBegin();
                }

            }
        }

        public virtual void EvaluateEnd()
        {
            if (Operation == OperationType.VALUE)
            {

            }
            else if (Operation == OperationType.TUPLE) // SET, TABLE, NON-PRIMITIVE, ...
            {
                //
                // Close files/databases
                //
                if (OutputVariable.TypeTable != null && !OutputVariable.TypeTable.IsPrimitive && OutputVariable.TypeTable.Schema is SchemaCsv)
                {
                    SchemaCsv csvSchema = (SchemaCsv)OutputVariable.TypeTable.Schema;
                    SetCsv csvOutput = (SetCsv)OutputVariable.TypeTable;
                }
                else if (OutputVariable.TypeTable != null && !OutputVariable.TypeTable.IsPrimitive && OutputVariable.TypeTable.Schema is SchemaOledb)
                {
                }

                // Close file
                if (TableWriter != null) TableWriter.Close();

                //
                // Evaluate children
                //
                foreach (TreeNode<ExprNode> childNode in Children)
                {
                    childNode.Item.EvaluateEnd();
                }
            }
            else if (Operation == OperationType.CALL) // FUNCTION, COLUMN, PRIMITIVE, ...
            {
                //
                // Evaluate children
                //
                foreach (ExprNode childNode in Children)
                {
                    childNode.EvaluateEnd();
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
            else if (Operation == OperationType.TUPLE) // SET, TABLE, NON-PRIMITIVE, ...
            {
                //
                // Evaluate children
                //
                foreach (TreeNode<ExprNode> childNode in Children)
                {
                    childNode.Item.Evaluate();
                }

                if (OutputVariable.TypeTable.IsPrimitive) // Primitive TUPLE nodes are processed differently
                {
                    Debug.Assert(Children.Count == 1, "Wrong use: a primitive TUPLE node must have one child expression providing its value.");
                    ExprNode childNode = GetChild(0);
                    object val = childNode.OutputVariable.GetValue();
                    string targeTypeName = OutputVariable.TypeTable.Name;

                    // Copy result from the child expression and convert it to this node type
                    if (val is DBNull)
                    {
                        OutputVariable.SetValue(null);
                    }
                    else if (val is string && string.IsNullOrWhiteSpace((string)val))
                    {
                        OutputVariable.SetValue(null);
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Integer"))
                    {
                        OutputVariable.SetValue(childNode.ObjectToInt32(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Double"))
                    {
                        OutputVariable.SetValue(childNode.ObjectToDouble(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Decimal"))
                    {
                        OutputVariable.SetValue(childNode.ObjectToDecimal(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "String"))
                    {
                        OutputVariable.SetValue(childNode.ObjectToString(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "Boolean"))
                    {
                        OutputVariable.SetValue(childNode.ObjectToBoolean(val));
                    }
                    else if (StringSimilarity.SameTableName(targeTypeName, "DateTime"))
                    {
                        OutputVariable.SetValue(childNode.ObjectToDateTime(val));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    // Do execute the action because it is a primitive set
                }
                else // Non-primitive/non-leaf TUPLE node is a complex value with a special operation
                {
                    // NOTE: tuple is never evaluated for relational, csv and other external tables

                    // Find, append or update an element in this set (depending on the action type)
                    if (Action == ActionType.READ) // Find the offset
                    {
                        Rowid input = TableWriter.Find(this);

                        if (input < 0 || input >= OutputVariable.TypeTable.GetData().Length) // Not found
                        {
                            OutputVariable.SetValue(null);
                        }
                        else
                        {
                            OutputVariable.SetValue(input);
                        }
                    }
                    else if (Action == ActionType.UPDATE) // Find and update the record
                    {
                    }
                    else if (Action == ActionType.APPEND) // Find, try to update and append if cannot be found
                    {
                        Rowid input = TableWriter.Find(this); // Uniqueness constraint: check if it exists already

                        if (input < 0 || input >= OutputVariable.TypeTable.GetData().Length) // Not found
                        {
                            input = TableWriter.Append(this); // Append new
                        }

                        OutputVariable.SetValue(input);
                    }
                    else
                    {
                        throw new NotImplementedException("ERROR: Other actions with tuples are not possible.");
                    }
                }
            }
            else if (Operation == OperationType.CALL) // FUNCTION, COLUMN, PRIMITIVE, ...
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
                    if (Column is DimCsv) // Access using Csv columnd in a Csv table
                    {
                        // Find current Row object
                        ExprNode thisNode = GetChild("this");
                        string[] input = (string[])thisNode.OutputVariable.GetValue();

                        // Use attribute name or number by applying it to the current Row object (offset is not used)
                        int attributeIndex = ((DimCsv)Column).ColumnIndex;
                        object output = input[attributeIndex];
                        OutputVariable.SetValue(output);
                    }
                    else if (false /* this is ExprNodeOledb */) // It is easier to do it here rather than (correctly) in the extension
                    {
                        // Find current Row object
                        ExprNode thisNode = GetChild("this");
                        DataRow input = (DataRow)thisNode.OutputVariable.GetValue();

                        // Use attribute name or number by applying it to the current Row object (offset is not used)
                        string attributeName = Name;
                        object output = input[attributeName];
                        OutputVariable.SetValue(output);
                    }
                    else if (Column != null) 
                    {
                        ExprNode prevOutput = child1;
                        Rowid input = (Rowid)prevOutput.OutputVariable.GetValue();
                        object output = Column.GetData().GetValue(input);
                        OutputVariable.SetValue(output);
                    }
                    else if (Variable != null)
                    {
                        object result = Variable.GetValue();
                        OutputVariable.SetValue(result);
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
                        double arg = childNode.ObjectToDouble(childNode.OutputVariable.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes *= arg;
                    }
                    OutputVariable.SetValue(doubleRes);
                }
                else if (Action == ActionType.DIV)
                {
                    doubleRes = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    for (int i = 1; i < Children.Count; i++)
                    {
                        ExprNode childNode = (ExprNode)Children[i];
                        double arg = childNode.ObjectToDouble(childNode.OutputVariable.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes /= arg;
                    }
                    OutputVariable.SetValue(doubleRes);
                }
                else if (Action == ActionType.ADD)
                {
                    doubleRes = 0.0;
                    foreach (ExprNode childNode in Children)
                    {
                        double arg = childNode.ObjectToDouble(childNode.OutputVariable.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes += arg;
                    }
                    OutputVariable.SetValue(doubleRes);
                }
                else if (Action == ActionType.SUB)
                {
                    doubleRes = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    for (int i = 1; i < Children.Count; i++)
                    {
                        ExprNode childNode = (ExprNode)Children[i];
                        double arg = childNode.ObjectToDouble(childNode.OutputVariable.GetValue());
                        if (double.IsNaN(arg)) continue;
                        doubleRes /= arg;
                    }
                    OutputVariable.SetValue(doubleRes);
                }
                else if (Action == ActionType.COUNT)
                {
                    intRes = child1.ObjectToInt32(child1.OutputVariable.GetValue());
                    intRes += 1;
                    OutputVariable.SetValue(intRes);
                }
                //
                // LEQ, GEQ, GRE, LES,
                //
                else if (Action == ActionType.LEQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.OutputVariable.GetValue());
                    boolRes = arg1 <= arg2;
                    OutputVariable.SetValue(boolRes);
                }
                else if (Action == ActionType.GEQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.OutputVariable.GetValue());
                    boolRes = arg1 >= arg2;
                    OutputVariable.SetValue(boolRes);
                }
                else if (Action == ActionType.GRE)
                {
                    double arg1 = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.OutputVariable.GetValue());
                    boolRes = arg1 > arg2;
                    OutputVariable.SetValue(boolRes);
                }
                else if (Action == ActionType.LES)
                {
                    double arg1 = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.OutputVariable.GetValue());
                    boolRes = arg1 < arg2;
                    OutputVariable.SetValue(boolRes);
                }
                //
                // EQ, NEQ
                //
                else if (Action == ActionType.EQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.OutputVariable.GetValue());
                    boolRes = arg1 == arg2;
                    OutputVariable.SetValue(boolRes);
                }
                else if (Action == ActionType.NEQ)
                {
                    double arg1 = child1.ObjectToDouble(child1.OutputVariable.GetValue());
                    double arg2 = child2.ObjectToDouble(child2.OutputVariable.GetValue());
                    boolRes = arg1 != arg2;
                    OutputVariable.SetValue(boolRes);
                }
                //
                // AND, OR
                //
                else if (Action == ActionType.AND)
                {
                    bool arg1 = child1.ObjectToBoolean(child1.OutputVariable.GetValue());
                    bool arg2 = child2.ObjectToBoolean(child2.OutputVariable.GetValue());
                    boolRes = arg1 && arg2;
                    OutputVariable.SetValue(boolRes);
                }
                else if (Action == ActionType.OR)
                {
                    bool arg1 = child1.ObjectToBoolean(child1.OutputVariable.GetValue());
                    bool arg2 = child2.ObjectToBoolean(child2.OutputVariable.GetValue());
                    boolRes = arg1 || arg2;
                    OutputVariable.SetValue(boolRes);
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
                            args[i] = ((ExprNode)Children[0]).OutputVariable.GetValue();
                        }
                    }
                    else {
                        if(childCount > 0) thisObj = ((ExprNode)Children[0]).OutputVariable.GetValue();

                        args = new object[childCount - 1]; 
                        for(int i=0; i<childCount-1; i++) {
                            args[i] = ((ExprNode)Children[i+1]).OutputVariable.GetValue();
                        }
                    }
                
                    // Dynamic invocation
                    try {
                        objRes = Method.Invoke(thisObj, args);
                    }
                    catch (Exception e) {
                        Console.WriteLine(e.StackTrace);
                    }            	
                
                    OutputVariable.SetValue(objRes);
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
        public List<ExprNode> Find(DcColumn column)
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
        public List<ExprNode> Find(DcTable table)
        {
            var res = new List<ExprNode>();

            Action<ExprNode> visitor = delegate(ExprNode node)
            {
                if (node.OutputVariable == null || node.OutputVariable.TypeTable == null) return; // The node does not use a table

                if (table == null || table == node.OutputVariable.TypeTable)
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

            Debug.Assert(path != null && path.Input == OutputVariable.TypeTable, "Wrong use: path must start from the node (output set) it is applied to.");

            if (path.Segments == null || path.Segments.Count == 0) return this;

            ExprNode node = this;
            for (int i = 0; i < path.Segments.Count; i++) // We add all segments sequentially
            {
                DcColumn seg = path.Segments[i];
                ExprNode child = node.GetChild(seg.Name); // Try to find a child corresponding to this segment

                if (child == null) // Not found. Add a new child corresponding to this segment
                {
                    child = new ExprNode();
                    child.Operation = OperationType.TUPLE;
                    child.Action = this.Action; // We inherit the action from the tuple root
                    child.Name = seg.Name;

                    child.OutputVariable.SchemaName = seg.Output.Schema.Name;
                    child.OutputVariable.TypeName = seg.Output.Name;
                    child.OutputVariable.TypeSchema = seg.Output.Schema;
                    child.OutputVariable.TypeTable = seg.Output;

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

                thisNode.OutputVariable.SchemaName = path.Input.Schema.Name;
                thisNode.OutputVariable.TypeName = path.Input.Name;
                thisNode.OutputVariable.TypeSchema = path.Input.Schema;
                thisNode.OutputVariable.TypeTable = path.Input;

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

                expr = new ExprNode(); // Previously: ExprNodeCsv
                expr.Operation = OperationType.CALL;
                expr.Action = ActionType.READ;
                expr.Name = seg.Name;

                expr.OutputVariable.SchemaName = seg.Output.Schema.Name;
                expr.OutputVariable.TypeName = seg.Output.Name;
                expr.OutputVariable.TypeSchema = seg.Output.Schema;
                expr.OutputVariable.TypeTable = seg.Output;

                expr.CultureInfo = ((SetCsv)seg.Input).CultureInfo;
            }
            else if (path.Input.Schema is SchemaOledb) // Access via relational attribute
            {
                expr = new ExprNode(); // Previously: ExprNodeOledb
                expr.Operation = OperationType.CALL;
                expr.Action = ActionType.READ;
                expr.Name = path.Name;

                expr.OutputVariable.SchemaName = path.Input.Schema.Name;
                expr.OutputVariable.TypeName = path.Input.Name;
                expr.OutputVariable.TypeSchema = path.Input.Schema;
                expr.OutputVariable.TypeTable = path.Input;
            }
            else // Access via function/column composition
            {
                for (int i = path.Segments.Count() - 1; i >= 0; i--)
                {
                    DcColumn seg = path.Segments[i];

                    ExprNode node = new ExprNode();
                    node.Operation = OperationType.CALL;
                    node.Action = ActionType.READ;
                    node.Name = seg.Name;

                    node.OutputVariable.SchemaName = seg.Output.Schema.Name;
                    node.OutputVariable.TypeName = seg.Output.Name;
                    node.OutputVariable.TypeSchema = seg.Output.Schema;
                    node.OutputVariable.TypeTable = seg.Output;

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

                thisNode.OutputVariable.SchemaName = path.Input.Schema.Name;
                thisNode.OutputVariable.TypeName = path.Input.Name;
                thisNode.OutputVariable.TypeSchema = path.Input.Schema;
                thisNode.OutputVariable.TypeTable = path.Input;

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
        public static ExprNode CreateReader(DcColumn column, bool withThisVariable)
        {
            return CreateReader(new DimPath(column), withThisVariable);
        }

        /// <summary>
        /// Create an upate expression for the specified aggregation column and standard aggregation function. 
        /// This expression will read two variables from the context: 'this' typed by the column lesser set and 'value' typed by the column greater set.
        /// </summary>
        public static ExprNode CreateUpdater(DcColumn column, string aggregationFunction)
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

            valueNode.OutputVariable.SchemaName = column.Output.Schema.Name;
            valueNode.OutputVariable.TypeName = column.Output.Name;
            valueNode.OutputVariable.TypeSchema = column.Output.Schema;
            valueNode.OutputVariable.TypeTable = column.Output;

            //
            // A node for computing a result (updated) function value from the current value and new value
            //
            ExprNode expr = new ExprNode();
            expr.Operation = OperationType.CALL;
            expr.Action = aggregation; // SUM etc.
            expr.Name = column.Name;

            expr.OutputVariable.SchemaName = column.Output.Schema.Name;
            expr.OutputVariable.TypeName = column.Output.Name;
            expr.OutputVariable.TypeSchema = column.Output.Schema;
            expr.OutputVariable.TypeTable = column.Output;

            // Two arguments in child nodes
            expr.AddChild(currentValueNode);
            expr.AddChild(valueNode);

            return expr;
        }

        #region DcJson serialization

        public virtual void ToJson(JObject json)
        {
            // We do not use the base TreeNode serialization
            dynamic expr = json;

            expr.operation = Operation;
            expr.name = Name;
            expr.action = Action;

            // Result
            if (OutputVariable  != null) // Manually serialize
            {
                dynamic result = new JObject();

                result.schema_name = OutputVariable.SchemaName;
                result.type_name = OutputVariable.TypeName;
                result.name = OutputVariable.Name;

                expr.result = result;
            }

            // List of children
            expr.children = new JArray() as dynamic;
            foreach (var node in Children)
            {
                dynamic child = Com.Schema.Utils.CreateJsonFromObject(node);
                ((ExprNode)node).ToJson(child);
                expr.children.Add(child);
            }
        }

        public virtual void FromJson(JObject json, DcWorkspace ws)
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

                OutputVariable = new Variable(resultSchema, resultType, resultName);
            }

            // List of children
            foreach (JObject child in json["children"])
            {
                ExprNode childNode = (ExprNode)Com.Schema.Utils.CreateObjectFromJson(child);
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
            CultureInfo = Com.Schema.Utils.cultureInfo; // Default
            OutputVariable = new Variable("", "Void", "return");
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

    public enum ColumnDefinitionType // Specific types of column formula
    {
        FREE, // No definition for the column (and cannot be defined). Example: key columns of a product table
        ANY, // Arbitrary formula without constraints which can mix many other types of expressions
        ARITHMETIC, // Column uses only other columns or paths of this same table as well as operations
        LINK, // Column is defined via a mapping represented as a tuple with paths as leaves
        AGGREGATION, // Column is defined via an updater (accumulator) function which is fed by facts using grouping and measure paths
        CASE,
    }

}
