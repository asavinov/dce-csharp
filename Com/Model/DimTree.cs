using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Com.Model
{
    public class DimTree : IEnumerable<DimTree>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        /// <summary>
        /// It is one element of the tree. It is null for the bottom (root) and its direct children which do not have lesser dimensions.
        /// </summary>
        private Dim _dim;
        public Dim Dim { get { return _dim; } set { _dim = value; } }

        /// <summary>
        /// It is a set corresponding to the node. If dimension is present then it is equal to the greater set.  
        /// It is null only for the bottom (root). It can be set only if dimension is null (otherwise set the dimension). 
        /// </summary>
        public Set Set
        {
            get { return Dim != null ? Dim.GreaterSet : null; }
        }

        public bool IsEmpty
        {
            get { return Dim == null || Dim.GreaterSet == null || Dim.LesserSet == null || Dim.GreaterSet == Dim.LesserSet; }
        }

        //
        // Tree methods
        //
        public bool IsRoot { get { return Parent == null; } }
        public bool IsLeaf { get { return Children == null || Children.Count == 0; } }
        public DimTree Parent { get; set; }
        public List<DimTree> Children { get; set; }
        public void AddChild(DimTree child)
        {
            Debug.Assert(!Children.Contains(child), "Wrong use: this child node already exists in the tree.");
            Debug.Assert(Set == null || child.IsEmpty || child.Dim.LesserSet == Set, "Wrong use: a new dimension must start from this set.");
            Children.Add(child);
            child.Parent = this;

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, child));
        }
        public bool RemoveChild(DimTree child)
        {
            bool ret = Children.Remove(child);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, child));

            return ret;
        }
        public bool ExistsChild(Set set)
        {
            return Children.Exists(c => c.Set == set);
        }
        public bool ExistsChild(Dim dim)
        {
            return Children.Exists(c => c.Dim == dim);
        }
        public DimTree GetChild(Dim dim)
        {
            return Children.FirstOrDefault(c => c.Dim == dim);
        }

        public DimTree AddPath(DimPath path) // Find or create nodes corresponding to the path.
        {
            Debug.Assert(path != null && path.LesserSet == Set, "Wrong use: path must start from the node it is added to.");

            if (path.Path == null || path.Path.Count == 0) return null;

            Dim seg;
            DimTree node = this;
            for (int i = 0; i < path.Path.Count; i++) // We add all segments sequentially
            {
                seg = path.Path[i];
                DimTree child = GetChild(seg); // Find a child corresponding to this segment

                if (child == null) // Add a new child corresponding to this segment
                {
                    child = (DimTree)Activator.CreateInstance(node.GetType());
                    child.Dim = seg;
                    node.AddChild(child);
                }

                node = child;
            }

            return node;
        }

        public void AddSourcePaths(SetMapping mapping)
        {
            mapping.Matches.ForEach(m => AddPath(m.SourcePath));
        }

        public void AddTargetPaths(SetMapping mapping)
        {
            mapping.Matches.ForEach(m => AddPath(m.TargetPath));
        }

        public DimTree Root // Find the tree root
        {
            get
            {
                DimTree node = this;
                while (node.Parent != null) node = node.Parent;
                return node;
            }
        }
        public DimTree SetRoot
        {
            get
            {
                DimTree node = this;
                while (node.Parent != null && node.Parent.Set != null) node = node.Parent;
                if (node == this || node.Set == null) return null;
                return node;
            }
        }
        public int DimRank
        {
            get
            {
                int rank = 0;
                for (DimTree node = this; !node.IsEmpty && node.Parent != null; node = node.Parent) rank++;
                return rank;
            }
        }
        public DimPath DimPath
        {
            get
            {
                DimPath path = new DimPath(Set);
                if (IsEmpty) return path;
                for (DimTree node = this; !node.IsEmpty && node.Parent != null; node = node.Parent) path.InsertFirst(node.Dim);
                return path;
            }
        }
        public int MaxLeafRank // 0 if this node is a leaf
        {
            get
            {
                var leaves = Flatten().Where(s => s.IsLeaf);
                int maxRank = 0;
                foreach (DimTree n in leaves)
                {
                    int r = 0;
                    for (DimTree t = n; t != this; t = t.Parent) r++;
                    maxRank = r > maxRank ? r : maxRank;
                }
                return maxRank;
            }
        }

        public IEnumerable<DimTree> Flatten() // Including this element
        {
            return new[] { this }.Union(Children.SelectMany(x => x.Flatten()));
        }

        public List<List<DimTree>> GetRankedNodes() // Return a list where element n is a list of nodes with rank n (n=0 means a leaf with a primitive set)
        {
            int maxRank = MaxLeafRank;
            List<List<DimTree>> res = new List<List<DimTree>>();
            for (int r = 0; r < maxRank; r++) res.Add(new List<DimTree>());

            List<DimTree> all = Flatten().ToList();

            foreach (DimTree node in all)
            {
                res[node.MaxLeafRank].Add(node);
            }

            return res;
        }

        public List<List<Set>> GetRankedSets() // A list of lists where each internal list corresponds to one rank starting from 0 (primitive sets) for the first list.
        {
            List<List<DimTree>> rankedNodes = GetRankedNodes();

            List<List<Set>> rankedSets = new List<List<Set>>();
            for (int r = 0; r < rankedNodes.Count; r++) rankedSets.Add(new List<Set>());

            for (int r = 0; r < rankedNodes.Count; r++)
            {
                rankedSets[r] = rankedNodes[r].Select(n => n.Set).Distinct().ToList(); // Only unique sets for each level of nodes
            }

            return rankedSets;
        }

        public List<Set> GetSets()
        {
            return Flatten().Select(n => n.Set).Distinct().ToList();
        }

        //
        // IEnumerable for accessing children (is needed for the root to serve as ItemsSource)
        //
        IEnumerator<DimTree> IEnumerable<DimTree>.GetEnumerator()
        {
            return Children.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        //
        // NotifyCollectionChanged Members
        //
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, e);
            }
        }

        //
        // INotifyPropertyChanged Members
        //
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public void NotifyAllOnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
            Children.ForEach(c => c.NotifyAllOnPropertyChanged(propertyName));
        }

        /// <summary>
        /// Create and add child nodes for all greater dimensions of this set. 
        /// </summary>
        public void ExpandTree(bool recursively = true)
        {
            if (Set == null) // We cannot expand the set but try to expand the existing children
            {
                if (!recursively) return;
                Children.ForEach(c => c.ExpandTree(recursively));
                return;
            }

            if (Set.IsGreatest) return; // No greater sets - nothing to expand

            List<Set> sets = new List<Set>(new[] { Set });
            sets.AddRange(Set.GetAllSubsets());

            foreach (Set s in sets)
            {
                foreach (Dim d in s.GreaterDims)
                {
                    if (ExistsChild(d)) continue;
                    // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                    DimTree child = (DimTree)Activator.CreateInstance(this.GetType());
                    child.Dim = d;
                    this.AddChild(child);
                    if (recursively) child.ExpandTree(recursively);
                }
            }
        }

        /// <summary>
        /// Whether this dimension node is integrated into the schema. 
        /// </summary>
        public bool IsInSchema()
        {
            return !IsEmpty ? !Dim.IsHanging : true;
        }

        /// <summary>
        /// If a set is not included in the schema then include it. Inclusion is performed by storing all dimensions into the set including (a new) super-dimension. 
        /// TODO: In fact, all elements should have super-dimensions which specify the parent set or the root of the schema to include into, and then the parameter is not needed. 
        /// </summary>
        public void IncludeInSchema(SetTop top)
        {
            if (Set.IsPrimitive)
            {
                // Check that the schema has this primitive set and add an equivalent primitive set if it is absent (determined via default mapping)
            }
            else
            {
                if (!Set.IsIn(top.Root))
                {
                    top.Root.AddSubset(Set);
                }
            }

            if (Dim != null && Dim.LesserSet != Dim.GreaterSet)
            {
                Dim.Add();
            }

            foreach (DimTree node in Children)
            {
                if (node.IsEmpty) continue; // Root has no dimension

                node.IncludeInSchema(top); // Recursion
            }
        }

        public DimTree(Dim dim, DimTree parent = null)
        {
            Dim = dim;
            Children = new List<DimTree>();
            if (parent != null) parent.AddChild(this);
        }

        public DimTree(Set set, DimTree parent = null)
        {
            Dim = new Dim(set);
            Children = new List<DimTree>();
            if (parent != null) parent.AddChild(this);
        }

        public DimTree()
        {
            Children = new List<DimTree>();
        }
    }

    /// <summary>
    /// Generic tree. Copied from: http://stackoverflow.com/questions/66893/tree-data-structure-in-c-sharp
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TreeNode<T>
    {
        private readonly T _value;
        private readonly List<TreeNode<T>> _children = new List<TreeNode<T>>();

        public TreeNode(T value)
        {
            _value = value;
        }

        public TreeNode<T> this[int i]
        {
            get { return _children[i]; }
        }

        public TreeNode<T> Parent { get; private set; }

        public T Value { get { return _value; } }

        public System.Collections.ObjectModel.ReadOnlyCollection<TreeNode<T>> Children
        {
            get { return _children.AsReadOnly(); }
        }

        public TreeNode<T> AddChild(T value)
        {
            var node = new TreeNode<T>(value) { Parent = this };
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
