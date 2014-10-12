using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    /// <summary>
    /// Generic tree. Copied from: http://stackoverflow.com/questions/66893/tree-data-structure-in-c-sharp
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TreeNode<T>
    {
        private readonly T _value;

        private readonly List<TreeNode<T>> _children = new List<TreeNode<T>>();


        public TreeNode()
        {
            if (this is T) // If 'this' can be stored as a value (by casting) then store it
            {
                _value = (T)(object)this; // Works only via intermediate casting to object
                //_value = (T)Convert.ChangeType(this, typeof(T)); // Does not work
            }
        }

        public TreeNode(T value)
        {
            _value = value;
        }

        public TreeNode<T> this[int i]
        {
            get { return _children[i]; }
        }

        public TreeNode<T> Parent { get; private set; }
        public TreeNode<T> Root { get { TreeNode<T> node = this; while (node.Parent != null) node = node.Parent; return node; } }

        public T Value { get { return _value; } }

        public System.Collections.ObjectModel.ReadOnlyCollection<TreeNode<T>> Children
        {
            get { return _children.AsReadOnly(); }
        }

        public TreeNode<T> AddChild(T value)
        {
            //if (value == null) return null;

            TreeNode<T> node = null;

            if (value is TreeNode<T>) // The child IS already a node so we do not create a new one
            {
                node = value as TreeNode<T>;
                node.Parent = this;
            }
            else
            {
                node = new TreeNode<T>(value) { Parent = this };
            }

            _children.Add(node);
            return node;
        }

        public TreeNode<T>[] AddChildren(params T[] values)
        {
            return values.Select(AddChild).ToArray();
        }

        public bool RemoveChild(TreeNode<T> node)
        {
            return _children.Remove(node);
        }

        public void ClearChildren()
        {
            _children.Clear();
        }

        public void Traverse(Action<T> action)
        {
            action(Value);
            foreach (var child in _children)
                child.Traverse(action);
        }

        public IEnumerable<T> Flatten()
        {
            return new[] { Value }.Union(_children.SelectMany(x => x.Flatten()));
        }
    }

}
