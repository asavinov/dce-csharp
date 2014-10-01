using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private ComColumn _dim;
        public ComColumn Dim { get { return _dim; } set { _dim = value; } }

        /// <summary>
        /// It is a set corresponding to the node. If dimension is present then it is equal to the greater set.  
        /// It is null only for the bottom (root). It can be set only if dimension is null (otherwise set the dimension). 
        /// </summary>
        public ComTable Set
        {
            get { return Dim != null ? Dim.Output : null; }
        }

        public bool IsEmpty
        {
            get { return Dim == null || Dim.Output == null || Dim.Input == null || Dim.Output == Dim.Input; }
        }

        //
        // Tree methods
        //
        public DimTree Parent { get; set; }
        public bool IsRoot { get { return Parent == null; } }

        public List<DimTree> Children { get; set; }
        public bool IsLeaf { get { return Children == null || Children.Count == 0; } }
        public void AddChild(DimTree child)
        {
            Debug.Assert(!Children.Contains(child), "Wrong use: this child node already exists in the tree.");
            Debug.Assert(Set == null || child.IsEmpty || child.Dim.Input == Set, "Wrong use: a new dimension must start from this set.");
            Children.Add(child);
            child.Parent = this;

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, child));
        }
        public bool RemoveChild(DimTree child)
        {
            int pos = Children.IndexOf(child);
            bool ret = Children.Remove(child);

            if (ret)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, child, pos));
            }

            return ret;
        }
        public void Clear()
        {
            Children.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool ExistsChild(ComTable set)
        {
            return Children.Exists(c => c.Set == set);
        }
        public bool ExistsChild(ComColumn dim)
        {
            return Children.Exists(c => c.Dim == dim);
        }
        public DimTree GetChild(ComColumn dim)
        {
            return Children.FirstOrDefault(c => c.Dim == dim);
        }
        public IEnumerable<DimTree> Flatten() // Including this element
        {
            return new[] { this }.Union(Children.SelectMany(x => x.Flatten()));
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

        public List<List<ComTable>> GetRankedSets() // A list of lists where each internal list corresponds to one rank starting from 0 (primitive sets) for the first list.
        {
            List<List<DimTree>> rankedNodes = GetRankedNodes();

            List<List<ComTable>> rankedSets = new List<List<ComTable>>();
            for (int r = 0; r < rankedNodes.Count; r++) rankedSets.Add(new List<ComTable>());

            for (int r = 0; r < rankedNodes.Count; r++)
            {
                rankedSets[r] = rankedNodes[r].Select(n => n.Set).Distinct().ToList(); // Only unique sets for each level of nodes
            }

            return rankedSets;
        }

        public List<ComTable> GetSets()
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
        // INotifyCollectionChanged Members
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

        public DimTree FindPath(DimPath path) // Find a node corresponding to the path.
        {
            Debug.Assert(path != null && path.Input == Set, "Wrong use: path must start from the node it is added to.");

            if (path.Segments == null || path.Segments.Count == 0) return null;

            ComColumn seg;
            DimTree node = this;
            for (int i = 0; i < path.Segments.Count; i++) // We try to find segments sequentially
            {
                seg = path.Segments[i];
                DimTree child = node.GetChild(seg); // Find a child corresponding to this segment

                if (child == null) // Add a new child corresponding to this segment
                {
                    return null;
                }

                node = child;
            }

            return node;
        }

        public DimTree AddPath(DimPath path) // Find or create nodes corresponding to the path.
        {
            Debug.Assert(path != null && path.Input == Set, "Wrong use: path must start from the node it is added to.");

            if (path.Segments == null || path.Segments.Count == 0) return null;

            ComColumn seg;
            DimTree node = this;
            for (int i = 0; i < path.Segments.Count; i++) // We add all segments sequentially
            {
                seg = path.Segments[i];
                DimTree child = node.GetChild(seg); // Find a child corresponding to this segment

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

        public void AddSourcePaths(Mapping mapping)
        {
            mapping.Matches.ForEach(m => AddPath(m.SourcePath));
        }

        public void AddTargetPaths(Mapping mapping)
        {
            mapping.Matches.ForEach(m => AddPath(m.TargetPath));
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

            List<ComTable> sets = new List<ComTable>(new[] { Set });
            sets.AddRange(Set.AllSubTables);

            foreach (ComTable s in sets)
            {
                foreach (ComColumn d in s.Columns)
                {
                    if (d.IsSuper) continue;
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
            bool isAdded = Dim.Output.InputColumns.Contains(Dim) && Dim.Input.Columns.Contains(Dim);
            return !IsEmpty ? !isAdded : true;
        }

        /// <summary>
        /// If a set is not included in the schema then include it. Inclusion is performed by storing all dimensions into the set including (a new) super-dimension. 
        /// TODO: In fact, all elements should have super-dimensions which specify the parent set or the root of the schema to include into, and then the parameter is not needed. 
        /// </summary>
        public void AddToSchema(ComSchema top)
        {
            if (Set.IsPrimitive)
            {
                // Check that the schema has this primitive set and add an equivalent primitive set if it is absent (determined via default mapping)
            }
            else
            {
                if (!Set.IsSubTable(top.Root))
                {
                    top.AddTable(Set, null, null);
                }
            }

            if (Dim != null && Dim.Input != Dim.Output)
            {
                Dim.Add();
            }

            foreach (DimTree node in Children)
            {
                if (node.IsEmpty) continue; // Root has no dimension

                node.AddToSchema(top); // Recursion
            }
        }

        public DimTree(ComColumn dim, DimTree parent = null)
        {
            Dim = dim;
            Children = new List<DimTree>();
            if (parent != null) parent.AddChild(this);
        }

        public DimTree(ComTable set, DimTree parent = null)
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

    /// <summary>
    /// It is a representative of one dimension and a basic element of a tree composed of dimensions.
    /// This class does not specify what kind of tree is created from dimensions and how dimensions are interpreted - it is specified in subclasses.
    /// It inherits from ObservableCollection because we want it to notify TreeView (alternatively, we need to implement numerous interfaces manually). 
    /// </summary>
    public class DimNode : ObservableCollection<DimNode>
    {
        /// <summary>
        /// It can be null for special nodes representing non-existing dimensions like bottom or top dimensions (normally root of the tree).
        /// </summary>
        private ComColumn _dim;
        public ComColumn Dim { get { return _dim; } protected set { _dim = value; } }

        public ComTable Output { get { return Dim != null ? Dim.Output : null; } }
        public ComTable Input { get { return Dim != null ? Dim.Input : null; } }

        //
        // Tree methods
        //

        public DimNode Parent { get; protected set; }
        public bool IsRoot { get { return Parent == null; } }
        public DimNode Root // Find the tree root
        {
            get
            {
                DimNode node = this;
                while (node.Parent != null) node = node.Parent;
                return node;
            }
        }

        public bool IsLeaf { get { return Count == 0; } }
        public void AddChild(DimNode child)
        {
            Debug.Assert(!Contains(child), "Wrong use: this child node already exists in the tree.");

            if (child.Parent != null)
            {
                child.Parent.RemoveChild(child);
            }

            child.Parent = this;
            this.Add(child); // Notification will be sent by the base class

            // this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (object)child));
        }
        public bool RemoveChild(DimNode child)
        {
            child.Parent = null;
            bool ret = this.Remove(child); // Notification will be sent by the base class

            // this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (object)child));

            return ret;
        }
        public bool ExistsChild(ComColumn dim)
        {
            return GetChild(dim) != null;
        }
        public DimNode GetChild(ComColumn dim)
        {
            return this.FirstOrDefault(c => c.Dim == dim);
        }
        public IEnumerable<DimNode> Flatten() // All direct and indirect children (including this element)
        {
            return new[] { this }.Union(this.SelectMany(x => x.Flatten()));
        }
        public int MaxLeafRank // 0 if this node is a leaf
        {
            get
            {
                var leaves = Flatten().Where(s => s.IsLeaf);
                int maxRank = 0;
                foreach (DimNode n in leaves)
                {
                    int r = 0;
                    for (DimNode t = n; t != this; t = t.Parent) r++;
                    maxRank = r > maxRank ? r : maxRank;
                }
                return maxRank;
            }
        }

        protected virtual void Input_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) // Changes in our lesser set
        {
        }

        protected virtual void Output_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) // Changes in our greater set
        {
        }

        public void UnregisterListeners()
        {
            if (Dim.Input != null) ((Set)Dim.Input).CollectionChanged -= Input_CollectionChanged;
            if (Dim.Output != null) ((Set)Dim.Output).CollectionChanged -= Output_CollectionChanged;
        }

        public void NotifyAllOnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            foreach (var c in this) c.NotifyAllOnPropertyChanged(propertyName);
        }

        public DimNode(ComColumn dim, DimNode parent = null)
        {
            Dim = dim;
            if (parent != null) parent.AddChild(this);
            if (Dim != null)
            {
                if (Dim.Input != null) ((Set)Dim.Input).CollectionChanged += Input_CollectionChanged;
                if (Dim.Output != null) ((Set)Dim.Output).CollectionChanged += Output_CollectionChanged;
            }
        }

    }

    /// <summary>
    /// It is an element of a subset (inclusion) tree with only direct greater dimensions (attributes).
    /// Two kinds of children: subsets (reference super-dimensions), and direct attributes (reference greater dimensions). 
    /// </summary>
    public class SubsetTree : DimNode
    {

        public IEnumerable<DimNode> SubsetChildren
        {
            get
            {
                return this.Where(c => ((SubsetTree)c).IsSubsetNode);
            }
        }

        public IEnumerable<DimNode> DimensionChildren
        {
            get
            {
                return this.Where(c => ((SubsetTree)c).IsDimensionNode);
            }
        }

        public bool IsSubsetNode // Does it represent a set node in the tree?
        {
            get
            {
                return Dim != null && (Dim.IsSuper);
            }
        }

        public bool IsDimensionNode // Does it represent a dimension node in the tree?
        {
            get
            {
                return !IsSubsetNode && Input.Schema == Output.Schema;
            }
        }

        protected override void Input_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) // Changes in our lesser set
        {
            if (!IsSubsetNode) return; // Child nodes are added/deleted for only super-dimensions (for subset trees)

            ComColumn dim = null;
            if (e.Action == NotifyCollectionChangedAction.Add) // Decide if this node has to add a new child node
            {
                dim = e.NewItems != null && e.NewItems.Count > 0 ? (Dim)e.NewItems[0] : null;
                if (dim == null) return;

                if (dim.IsSuper) // Inclusion
                {
                    if (dim.Output == Dim.Input) // Add a subset child node (recursively)
                    {
                        if (!ExistsChild(dim))
                        {
                            // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                            DimNode child = (DimNode)Activator.CreateInstance(this.GetType(), new Object[] { dim, null });
                            AddChild(child);

                            if (child is SubsetTree) ((SubsetTree)child).ExpandTree(true);
                        }
                    }
                }
                else // Poset
                {
                    if (dim.Input == Dim.Input) // Add an attribute child node (non-recursively)
                    {
                        if (!ExistsChild(dim))
                        {
                            // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                            DimNode child = (DimNode)Activator.CreateInstance(this.GetType(), new Object[] { dim, null });
                            AddChild(child);
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                dim = e.OldItems != null && e.OldItems.Count > 0 ? (Dim)e.OldItems[0] : null;
                if (dim == null) return;

                if (dim.IsSuper) // Inclusion
                {
                    if (dim.Output == Dim.Input) // Remove a subset child node (recursively)
                    {
                        DimNode child = GetChild(dim);
                        if (child != null)
                        {
                            RemoveChild(child);
                        }
                    }
                }
                else // Poset
                {
                    if (dim.Input == Dim.Input) // Add an attribute child node (non-recursively)
                    {
                        DimNode child = GetChild(dim);
                        if (child != null)
                        {
                            RemoveChild(child);
                        }
                    }
                }
            }
        }

        protected override void Output_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) // Changes in our greater set
        {
        }

        /// <summary>
        /// Create and add child nodes for subsets of this node and direct greater dimensions. 
        /// </summary>
        public void ExpandTree(bool recursively = true)
        {
            if (Input == null) // We cannot expand the set but try to expand the existing children
            {
                if (!recursively) return;
                foreach (DimNode c in this)
                {
                    if (!(c is SubsetTree)) continue;
                    ((SubsetTree)c).ExpandTree(recursively);
                }
                return;
            }

            foreach (ComColumn sd in Input.SubColumns)
            {
                if (ExistsChild(sd)) continue;

                // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                DimNode child = (DimNode)Activator.CreateInstance(this.GetType(), new Object[] { sd, null });
                this.AddChild(child);

                if (recursively && (child is SubsetTree)) ((SubsetTree)child).ExpandTree(recursively);
            }

            // Add child nodes for greater dimension (no recursion)
            foreach (ComColumn gd in Input.Columns)
            {
                if (gd.IsSuper) continue;
                if (ExistsChild(gd)) continue;

                // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                DimNode child = (DimNode)Activator.CreateInstance(this.GetType(), new Object[] { gd, null });
                this.AddChild(child);
            }
        }

        public SubsetTree(ComColumn dim, DimNode parent = null)
            : base(dim, parent)
        {
            // Register for events from the schema (or inside constructor?)
        }

    }

}
