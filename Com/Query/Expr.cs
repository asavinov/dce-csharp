using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Com.Model;

using Offset = System.Int32;

namespace Com.Query
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
        // Type of the result value
        //

        // Type name of the result
        public string Type { get; set; }
        public CsTable TypeSet { get; set; }

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



        // Resolve the name and type name against the storage elements (schema, variables) 
        // The resolved object references are stored in the fields and will be used during evaluation without name resolution
        public void Resolve(CsSchema schema, List<CsVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {

            }
            else if (Operation == OperationType.TUPLE)
            {
                //
                // Resolve this (assuming the parents are resolved)
                //
                if (string.IsNullOrEmpty(Type))
                {
                    ExprNode parent = (ExprNode)Parent;
                    if (parent.Operation == OperationType.TUPLE) // Tuple in another tuple
                    {
                        if (parent.TypeSet != null && !string.IsNullOrEmpty(Name))
                        {
                            CsColumn col = parent.TypeSet.GetGreaterDim(Name);
                            column = col.ColumnData;
                            TypeSet = col.GreaterSet;
                        }
                    }
                    else // Tuple in some other node, e.g, argument or value
                    {
                        if (parent.TypeSet != null && !string.IsNullOrEmpty(Name))
                        {
                            CsColumn col = parent.TypeSet.GetGreaterDim(Name);
                            column = col.ColumnData;
                            TypeSet = col.GreaterSet;
                        }
                    }
                }
                else
                {
                    TypeSet = schema.FindSubset(Type);
                }


                //
                // Now resolve children
                //

            }
            if (Operation == OperationType.CALL)
            {
                //
                // Resolve children
                //


                // TODO: Set correct Action everywhere if not set

                //
                // Resolve this (assuming the children have been resolved)
                //
                ExprNode methodChild = GetChild("method"); // Get column name
                ExprNode thisChild = GetChild("this"); // Get column lesser set
                int childCount = Children.Count;
                if (!string.IsNullOrEmpty(Type))
                {
                    TypeSet = schema.FindSubset(Type);
                }
                else if (methodChild != null && thisChild != null) // Function access
                {
                    string methodName = methodChild.GetChild(0).Name;
                    CsColumn col = thisChild.TypeSet.GetGreaterDim(methodName);
                    column = col.ColumnData;
                    TypeSet = col.GreaterSet;
                    Type = col.GreaterSet.Name;
                }
                else if (methodChild != null && childCount == 1) // Variable
                {
                    string methodName = methodChild.GetChild(0).Name;
                    CsVariable var = variables.FirstOrDefault(v => v.Name.Equals(methodName));
                    variable = var;
                    Type = variable.TypeName;
                    TypeSet = variable.TypeTable;
                }
                else // System procedure or operator (arithmetic, logical etc.)
                {
                    string methodName = methodChild.GetChild(0).Name;
                    // TODO: Type has to be derived from arguments by using type conversion rules
                    Type = "Double";
                    TypeSet = schema.GetPrimitive(Type);
                    if (methodName == "*")
                    {
                        Action = ActionType.MUL;
                    }
                    else if (methodName == "/")
                    {
                        Action = ActionType.DIV;
                    }
                    else if (methodName == "+")
                    {
                        Action = ActionType.ADD;
                    }
                    else if (methodName == "-")
                    {
                        Action = ActionType.SUB;
                    }
                    else // Some procedure. Find its API specification or retrieve via reflection
                    {

                    }
                }
            }
        }

        public void Evaluate()
        {
            //
            // Evaluate children so that we have all their return values
            //

            if (Operation == OperationType.VALUE)
            {

            }
            else if (Operation == OperationType.TUPLE)
            {
                // Find, append or update an element in this set (depending on the action type)
                TypeSet.TableData.Find(this);
                Offset offset = (Offset)Result.GetValue();
                if (offset >= 0 && offset < TypeSet.TableData.Length) Result.SetValue(offset);
            }
            else if (Operation == OperationType.CALL)
            {
                if (column != null) // Read/write function
                {
                    ExprNode thisNode = GetChild("this");
                    Offset input = (Offset)thisNode.Result.GetValue();
                    object output = column.GetValue(input);
                    Result.SetValue(output);
                }
                else if (variable != null) // Read/write a variable
                {
                    object result = variable.GetValue();
                    Result.SetValue(result);
                }
                if (Action == ActionType.MUL) // Read all arguments (except for 'method'), reduce to our type, and add to the result
                {
                    double res = 0;
                    for (int i = 0; i < Children.Count; i++)
                    {
                        res += (double)GetChild(i).Result.GetValue();
                    }
                }
                else if (Action == ActionType.DIV)
                {
                }
                else if (Action == ActionType.ADD)
                {
                }
                else if (Action == ActionType.SUB)
                {
                }
                else // Some procedure. Find its API specification or retrieve via reflection
                {

                }
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

        string TypeName { get; set; }
        CsTable TypeTable { get; set; }

        string Name { get; set; }

        bool IsNull()
        {
            return isNull;
        }

        object GetValue()
        {
            return isNull ? null : Value;
        }

        void SetValue(object value)
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

        void NullifyValue()
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
