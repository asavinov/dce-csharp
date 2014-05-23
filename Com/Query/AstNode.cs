using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Com.Model;

using Offset = System.Int32;

namespace Com.Query
{
    public class AstNode : TreeNode<AstNode>
    {
        /// <summary>
        /// Type of syntactic node which corresponds to a syntactic rule.
        /// </summary>
        public AstRule Rule { get; set; }

        /// <summary>
        /// It is normally a leaf of an AST. Its content is normally not parsed. 
        /// It normally represents names of variables, set members as well as primitive values and literals.
        /// </summary>
        public string Name { get; set; }

        public AstNode GetChild(int child) { return (AstNode)Children[child]; }

        /// <summary>
        /// Analyze this syntactic node and generate instructions in the script context.
        /// </summary>
        public List<ScriptOp> TranslateNode()
        {
            // One node can be translated into a sequence of instructions

            // So compilation is needed because of the following properties:
            // - target operations are flat - no nesting 
            // - all target operations are very simple and are mapped directly to API operations
            // - any target operation applies some operation to context and changes some context object
            //   - in other words, any operation operates (reads/writes) only variables in the context
            //   - result field in this case is not needed - we use an output variable instead (which is typed in addition and always available for next instructions)
            // - advantage of having flat operations is easier to analyze and optimize (reorganize) because we see all operations in the list while nesting hides operations and context changes

            // The simplest approach is to introduce one unique variable storing an intermediate result of each generated instruction. 
            // In particular, variables will be created for each:
            // - nested projection/de-projection/dot operation. Do it mechanically and remember this variable as a parameter of another operation which consumes the result. So projection/de-projection always use variables - no other way is supported.
            // - nested product operation. Here member types must be variables so product supports only variables its member types - nothing else is supported. The variables are assigned before this operation by extracting the type definition (in nested manner). 
            // - if some function depends on another function then the evaluation operation is simply inserted before.
            // - if some function body is refactored then we change this function definition and insert another function definition and evaluation operations.

            // Problem:
            // Generated script instructions are stored in context or within other instructions (nested instructions). 
            // Function body definitions (vexpr) are stored within script instructions as child nodes (possibly directly as value context). 
            // When these script instructions will be executed, these parameters will be directly copied to the function objects.

            List<ScriptOp> scriptOps = new List<ScriptOp>();

            if (Rule == AstRule.SCRIPT) // Sequentially process all statements and merge all generated instructions
            {
                int stmtCount = Children.Count;
                for (int s = 0; s < stmtCount; s++)
                {
                    AstNode stmt = GetChild(s);
                    List<ScriptOp> stmtOps = stmt.TranslateNode();

                    scriptOps.AddRange(stmtOps);
                }
            }
            else if (Rule == AstRule.RETURN)
            {
                throw new NotImplementedException();
            }
            else if (Rule == AstRule.ALLOC)
            {
                // Allocate variable (directly execute this instruction)
                //ContextVariable var = new ContextVariable(stmt.GetChild(0).Name, stmt.GetChild(1).Name);
                //scriptCtx.Variables.Add(var);

                // If there is initialization then process it and assign to the variable
                if (Children.Count > 2 && GetChild(2).Rule == AstRule.SEXPR)
                {
                    // Generate code for expression

                    // Assign result of execution of the previous operation to the variable
                }
            }
            else if (Rule == AstRule.ASSIGNMENT)
            {
                throw new NotImplementedException();
            }

            return scriptOps;
        }

        /// <summary>
        /// Analyze this syntactic node and generate instructions in the value context defining a function.
        /// </summary>
        public ValueContext TranslateFunction()
        {
            return null;
        }

/*
        /// <summary>
        /// Where this node is a child.
        /// </summary>
        public AstNode Parent { get; set; }
        public AstNode Root
        {
            get
            {
                AstNode root = this;
                while (root.Parent != null)
                {
                    root = root.Parent;
                }

                return root;
            }
        }

        /// <summary>
        /// Child nodes.
        /// </summary>
        public List<AstNode> Children { get; set; }
        public int AddChild(AstNode child)
        {
            if (child == null) return -1;
            // TODO: We have to semantically check the validity of this child expression in the context of its parent expression, for example, by checking gramma rules

            int res = Children.IndexOf(child);
            if (res >= 0 && res < Children.Count)
            {
                Debug.Assert(child.Parent == this, "Wrong use: child expression must reference its parent");
                return res; // Already exists
            }

            if (child.Parent != null) child.Parent.RemoveChild(child);

            Children.Add(child);
            child.Parent = this;

            return Children.Count - 1;
        }
        public int RemoveChild(AstNode child)
        {
            int res = -1;
            res = Children.IndexOf(child);

            if (res >= 0) // Found
            {
                Children.RemoveAt(res);
            }

            if (child.Parent != null && child.Parent == this) child.Parent = null;

            return res;
        }
        public List<AstNode> GetChildren(AstRule op)
        {
            List<AstNode> res = new List<AstNode>();

            // Proces this element
            if (Rule == op) res.Add(this);

            // Recursively check all children
            Children.ForEach(e => res.AddRange(e.GetChildren(op)));

            return res;
        }
        public List<AstNode> GetLeaves(AstRule op) // Get a list of nodes which have the specified operation but no children with this operation. It is normally used to get nodes corresponding to primitive paths in a complex tuple.
        {
            List<AstNode> res = new List<AstNode>();

            if (Rule != op)
            {
                return res; // Empty means that there are no leaves here and down the tree
            }
            // This node *can* be a leaf (provided if it has no children with the operation)

            // Recursively check all children
            foreach (AstNode child in Children)
            {
                res.AddRange(child.GetLeaves(op));
            }

            if (res.Count == 0) res.Add(this); // It is a leaf

            return res;
        }
        public bool IsLeaf { get { return Children.Count == 0; } }

        public List<AstNode> GetRootPath() // Return a node list with nodes starting from the root (if it has a non-null dimension) and ending with this node
        {
            List<AstNode> path = new List<AstNode>();

            for (AstNode node = this; node != null; node = node.Parent)
            {
                path.Insert(0, node);
            }

            return path;
        }
*/

        public AstNode(string name)
            : this()
        {
            Name = name;
            Rule = AstRule.NAME;
        }

        public AstNode()
        {
            Rule = AstRule.NONE;
        }

    }

    public enum AstRule
    {
        NONE,

        //
        // Sexpr
        //

        SCRIPT, // List of set statements. A program for set processing.

        STATEMENT, // Statement in a script
        ALLOC, // Allocation/declaration of a new variable (with optional initialization)
        FREE, // Allocation/declaration of a new variable (with optional initialization)

        ASSIGNMENT, // Assignment to an existing variable: "myVar=sexpr;"
        RETURN, // Return. Semantically, it is similar assignment: "return sexpr;"

        SEXPR, // Set-oriented expression. It evaluates to a set by applying operations (and functions) to other sets.
        PROJECTION, // Operation of applying a function to a set which evaluates to another set
        DEPROJECTION, // Deprojection
        PRODUCT, // New set is defined as a product of greater sets as well as other members. Syntactically, it is a list of members of various types. 

        //
        // Vexpr
        //

        VSCOPE, // Scope in vexpr including delimiting the whole function body: { vexpr1; vexpr2; }
        DOT, // 
        TUPLE, // Tuple

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

        //
        // Common
        //

        CALL, // Function (by-name or by-def) applied to something. Used in both vexpr (applied to one value) and sexpr (applied to all).
        PARAM, // It is a parameter of a call
        MEMBER, // It is a node in a product (set) or tuple definition: "type name=vexpr"

        NAME, // Any identifier like name of a set member, variable, argument, function etc.
        TYPE, // Value type. It is a role of a child node like member of set (product) or tuple.

        LITERAL, // Literal. A single primitive value
    }
}
