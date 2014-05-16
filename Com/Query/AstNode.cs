using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Offset = System.Int32;

namespace Com.Query
{
    public class AstNode
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

        public AstNode(string name)
            : this()
        {
            Name = name;
            Rule = AstRule.NAME;
        }

        public AstNode()
        {
            Children = new List<AstNode>();
            Rule = AstRule.NONE;
        }

    }

    public enum AstRule
    {
        SCRIPT, // List of set statements. A program for set processing.
        STATEMENT, // Statement in a script
        ALLOC, // Allocation/declaration of a new variable (with optional initialization)
        ASSIGNMENT, // Assignment to an existing variable
        RETURN, // Return a set

        SEXPR, // Set-oriented expression. It evaluates to a set. 
        DOT, // 
        PROJECTION, // Operation of applying a function to a set which evaluates to another set
        DEPROJECTION, // Deprojection
        PRODUCT, // New set is defined as a product of greater sets as well as other members. Syntactically, it is a list of members of various types. 

        MEMBER, // It is a node in a set/product node definition
        CALL, // Any call including by-reference (using ID) or by-value (using definition)
        PARAM, // It is a parameter of a call

        SCOPE, // Scope

        TUPLE, // Tuple

        LITERAL, // Literal. A single primitive value

        NAME, // Name of a set member, variable, argument, function etc.
        TYPE, // It is a role of the child node.

        NONE,
    }
}
