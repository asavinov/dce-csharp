using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Com.Schema;

namespace Com.Utils
{
    public class ColumnTree : IEnumerable<ColumnTree>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        /// <summary>
        /// It is one element of the tree. It is null for the bottom (root) and its direct children which do not have lesser columns.
        /// </summary>
        private DcColumn _col;
        public DcColumn Column { get { return _col; } set { _col = value; } }

        /// <summary>
        /// It is a set corresponding to the node. If column is present then it is equal to the greater set.  
        /// It is null only for the bottom (root). It can be set only if column is null (otherwise set the column). 
        /// </summary>
        public DcTable Set
        {
            get { return Column != null ? Column.Output : null; }
        }

        public bool IsEmpty
        {
            get { return Column == null || Column.Output == null || Column.Input == null || Column.Output == Column.Input; }
        }

        //
        // Tree methods
        //
        public ColumnTree Parent { get; set; }
        public bool IsRoot { get { return Parent == null; } }

        public List<ColumnTree> Children { get; set; }
        public bool IsLeaf { get { return Children == null || Children.Count == 0; } }
        public void AddChild(ColumnTree child)
        {
            Debug.Assert(!Children.Contains(child), "Wrong use: this child node already exists in the tree.");
            Debug.Assert(Set == null || child.IsEmpty || child.Column.Input == Set, "Wrong use: a new column must start from this set.");
            Children.Add(child);
            child.Parent = this;

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, child));
        }
        public bool RemoveChild(ColumnTree child)
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

        public bool ExistsChild(DcTable set)
        {
            return Children.Exists(c => c.Set == set);
        }
        public bool ExistsChild(DcColumn col)
        {
            return Children.Exists(c => c.Column == col);
        }
        public ColumnTree GetChild(DcColumn col)
        {
            return Children.FirstOrDefault(c => c.Column == col);
        }
        public IEnumerable<ColumnTree> Flatten() // Including this element
        {
            return new[] { this }.Union(Children.SelectMany(x => x.Flatten()));
        }

        public ColumnTree Root // Find the tree root
        {
            get
            {
                ColumnTree node = this;
                while (node.Parent != null) node = node.Parent;
                return node;
            }
        }
        public ColumnTree SetRoot
        {
            get
            {
                ColumnTree node = this;
                while (node.Parent != null && node.Parent.Set != null) node = node.Parent;
                if (node == this || node.Set == null) return null;
                return node;
            }
        }
        public int ColRank
        {
            get
            {
                int rank = 0;
                for (ColumnTree node = this; !node.IsEmpty && node.Parent != null; node = node.Parent) rank++;
                return rank;
            }
        }
        public ColumnPath ColPath
        {
            get
            {
                ColumnPath path = new ColumnPath(Set);
                if (IsEmpty) return path;
                for (ColumnTree node = this; !node.IsEmpty && node.Parent != null; node = node.Parent) path.InsertFirst(node.Column);
                return path;
            }
        }
        public int MaxLeafRank // 0 if this node is a leaf
        {
            get
            {
                var leaves = Flatten().Where(s => s.IsLeaf);
                int maxRank = 0;
                foreach (ColumnTree n in leaves)
                {
                    int r = 0;
                    for (ColumnTree t = n; t != this; t = t.Parent) r++;
                    maxRank = r > maxRank ? r : maxRank;
                }
                return maxRank;
            }
        }

        public List<List<ColumnTree>> GetRankedNodes() // Return a list where element n is a list of nodes with rank n (n=0 means a leaf with a primitive set)
        {
            int maxRank = MaxLeafRank;
            List<List<ColumnTree>> res = new List<List<ColumnTree>>();
            for (int r = 0; r < maxRank; r++) res.Add(new List<ColumnTree>());

            List<ColumnTree> all = Flatten().ToList();

            foreach (ColumnTree node in all)
            {
                res[node.MaxLeafRank].Add(node);
            }

            return res;
        }

        public List<List<DcTable>> GetRankedTables() // A list of lists where each internal list corresponds to one rank starting from 0 (primitive tables) for the first list.
        {
            List<List<ColumnTree>> rankedNodes = GetRankedNodes();

            List<List<DcTable>> rankedTabs = new List<List<DcTable>>();
            for (int r = 0; r < rankedNodes.Count; r++) rankedTabs.Add(new List<DcTable>());

            for (int r = 0; r < rankedNodes.Count; r++)
            {
                rankedTabs[r] = rankedNodes[r].Select(n => n.Set).Distinct().ToList(); // Only unique tables for each level of nodes
            }

            return rankedTabs;
        }

        public List<DcTable> GetTables()
        {
            return Flatten().Select(n => n.Set).Distinct().ToList();
        }

        //
        // IEnumerable for accessing children (is needed for the root to serve as ItemsSource)
        //
        IEnumerator<ColumnTree> IEnumerable<ColumnTree>.GetEnumerator()
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

        public ColumnTree FindPath(ColumnPath path) // Find a node corresponding to the path.
        {
            Debug.Assert(path != null && path.Input == Set, "Wrong use: path must start from the node it is added to.");

            if (path.Segments == null || path.Segments.Count == 0) return null;

            DcColumn seg;
            ColumnTree node = this;
            for (int i = 0; i < path.Segments.Count; i++) // We try to find segments sequentially
            {
                seg = path.Segments[i];
                ColumnTree child = node.GetChild(seg); // Find a child corresponding to this segment

                if (child == null) // Add a new child corresponding to this segment
                {
                    return null;
                }

                node = child;
            }

            return node;
        }

        public ColumnTree AddPath(ColumnPath path) // Find or create nodes corresponding to the path.
        {
            Debug.Assert(path != null && path.Input == Set, "Wrong use: path must start from the node it is added to.");

            if (path.Segments == null || path.Segments.Count == 0) return null;

            DcColumn seg;
            ColumnTree node = this;
            for (int i = 0; i < path.Segments.Count; i++) // We add all segments sequentially
            {
                seg = path.Segments[i];
                ColumnTree child = node.GetChild(seg); // Find a child corresponding to this segment

                if (child == null) // Add a new child corresponding to this segment
                {
                    child = (ColumnTree)Activator.CreateInstance(node.GetType());
                    child.Column = seg;
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
        /// Create and add child nodes for all greater columns of this set. 
        /// </summary>
        public void ExpandTree(bool recursively = true)
        {
            if (Set == null) // We cannot expand the set but try to expand the existing children
            {
                if (!recursively) return;
                Children.ForEach(c => c.ExpandTree(recursively));
                return;
            }

            if (Set.IsGreatest) return; // No greater tables - nothing to expand

            List<DcTable> tabs = new List<DcTable>(new[] { Set });
            tabs.AddRange(Set.AllSubTables);

            foreach (DcTable s in tabs)
            {
                foreach (DcColumn d in s.Columns)
                {
                    if (d.IsSuper) continue;
                    if (ExistsChild(d)) continue;
                    // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                    ColumnTree child = (ColumnTree)Activator.CreateInstance(this.GetType());
                    child.Column = d;
                    this.AddChild(child);
                    if (recursively) child.ExpandTree(recursively);
                }
            }
        }

        /// <summary>
        /// Whether this column node is integrated into the schema. 
        /// </summary>
        public bool IsInSchema()
        {
            bool isAdded = Column.Output.InputColumns.Contains(Column) && Column.Input.Columns.Contains(Column);
            return !IsEmpty ? !isAdded : true;
        }

        /// <summary>
        /// If a set is not included in the schema then include it. Inclusion is performed by storing all columns into the set including (a new) super-column. 
        /// TODO: In fact, all elements should have super-columns which specify the parent set or the root of the schema to include into, and then the parameter is not needed. 
        /// </summary>
        public void AddToSchema(DcSchema top)
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

            if (Column != null && Column.Input != Column.Output)
            {
                Column.Add();
            }

            foreach (ColumnTree node in Children)
            {
                if (node.IsEmpty) continue; // Root has no column

                node.AddToSchema(top); // Recursion
            }
        }

        public ColumnTree(DcColumn col, ColumnTree parent = null)
        {
            Column = col;
            Children = new List<ColumnTree>();
            if (parent != null) parent.AddChild(this);
        }

        public ColumnTree(DcTable set, ColumnTree parent = null)
        {
            Column = new Column(set);
            Children = new List<ColumnTree>();
            if (parent != null) parent.AddChild(this);
        }

        public ColumnTree()
        {
            Children = new List<ColumnTree>();
        }
    }

    
    /// <summary>
    /// It is a representative of one column and a basic element of a tree composed of columns.
    /// This class does not specify what kind of tree is created from columns and how columns are interpreted - it is specified in subclasses.
    /// It inherits from ObservableCollection because we want it to notify TreeView (alternatively, we need to implement numerous interfaces manually). 
    /// </summary>
    public class ColumnNode : ObservableCollection<ColumnNode>
    {
        /// <summary>
        /// It can be null for special nodes representing non-existing columns like bottom or top columns (normally root of the tree).
        /// </summary>
        private DcColumn _col;
        public DcColumn Column { get { return _col; } protected set { _col = value; } }

        public DcTable Output { get { return Column != null ? Column.Output : null; } }
        public DcTable Input { get { return Column != null ? Column.Input : null; } }

        //
        // Tree methods
        //

        public ColumnNode Parent { get; protected set; }
        public bool IsRoot { get { return Parent == null; } }
        public ColumnNode Root // Find the tree root
        {
            get
            {
                ColumnNode node = this;
                while (node.Parent != null) node = node.Parent;
                return node;
            }
        }

        public bool IsLeaf { get { return Count == 0; } }
        public void AddChild(ColumnNode child)
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
        public bool RemoveChild(ColumnNode child)
        {
            child.Parent = null;
            bool ret = this.Remove(child); // Notification will be sent by the base class

            // this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (object)child));

            return ret;
        }
        public bool ExistsChild(DcColumn col)
        {
            return GetChild(col) != null;
        }
        public ColumnNode GetChild(DcColumn col)
        {
            return this.FirstOrDefault(c => c.Column == col);
        }
        public IEnumerable<ColumnNode> Flatten() // All direct and indirect children (including this element)
        {
            return new[] { this }.Union(this.SelectMany(x => x.Flatten()));
        }
        public int MaxLeafRank // 0 if this node is a leaf
        {
            get
            {
                var leaves = Flatten().Where(s => s.IsLeaf);
                int maxRank = 0;
                foreach (ColumnNode n in leaves)
                {
                    int r = 0;
                    for (ColumnNode t = n; t != this; t = t.Parent) r++;
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
            if (Column.Input != null) ((Table)Column.Input).CollectionChanged -= Input_CollectionChanged;
            if (Column.Output != null) ((Table)Column.Output).CollectionChanged -= Output_CollectionChanged;
        }

        public void NotifyAllOnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            foreach (var c in this) c.NotifyAllOnPropertyChanged(propertyName);
        }

        public ColumnNode(DcColumn col, ColumnNode parent = null)
        {
            Column = col;
            if (parent != null) parent.AddChild(this);
            if (Column != null)
            {
                if (Column.Input != null) ((Table)Column.Input).CollectionChanged += Input_CollectionChanged;
                if (Column.Output != null) ((Table)Column.Output).CollectionChanged += Output_CollectionChanged;
            }
        }

    }

    /// <summary>
    /// It is an element of a subset (inclusion) tree with only direct greater columns (attributes).
    /// Two kinds of children: subtables (reference super-columns), and direct attributes (reference greater columns). 
    /// </summary>
    public class SubtableTree : ColumnNode
    {

        public IEnumerable<ColumnNode> SubsetChildren
        {
            get
            {
                return this.Where(c => ((SubtableTree)c).IsSubsetNode);
            }
        }

        public IEnumerable<ColumnNode> ColumnChildren
        {
            get
            {
                return this.Where(c => ((SubtableTree)c).IsColumnNode);
            }
        }

        public bool IsSubsetNode // Does it represent a set node in the tree?
        {
            get
            {
                return Column != null && (Column.IsSuper);
            }
        }

        public bool IsColumnNode // Does it represent a column node in the tree?
        {
            get
            {
                return !IsSubsetNode && Input.Schema == Output.Schema;
            }
        }

        protected override void Input_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) // Changes in our lesser set
        {
            if (!IsSubsetNode) return; // Child nodes are added/deleted for only super-columns (for subset trees)

            DcColumn col = null;
            if (e.Action == NotifyCollectionChangedAction.Add) // Decide if this node has to add a new child node
            {
                col = e.NewItems != null && e.NewItems.Count > 0 ? (Column)e.NewItems[0] : null;
                if (col == null) return;

                if (col.IsSuper) // Inclusion
                {
                    if (col.Output == Column.Input) // Add a subset child node (recursively)
                    {
                        if (!ExistsChild(col))
                        {
                            // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                            ColumnNode child = (ColumnNode)Activator.CreateInstance(this.GetType(), new Object[] { col, null });
                            AddChild(child);

                            if (child is SubtableTree) ((SubtableTree)child).ExpandTree(true);
                        }
                    }
                }
                else // Poset
                {
                    if (col.Input == Column.Input) // Add an attribute child node (non-recursively)
                    {
                        if (!ExistsChild(col))
                        {
                            // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                            ColumnNode child = (ColumnNode)Activator.CreateInstance(this.GetType(), new Object[] { col, null });
                            AddChild(child);
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                col = e.OldItems != null && e.OldItems.Count > 0 ? (Column)e.OldItems[0] : null;
                if (col == null) return;

                if (col.IsSuper) // Inclusion
                {
                    if (col.Output == Column.Input) // Remove a subset child node (recursively)
                    {
                        ColumnNode child = GetChild(col);
                        if (child != null)
                        {
                            RemoveChild(child);
                        }
                    }
                }
                else // Poset
                {
                    if (col.Input == Column.Input) // Add an attribute child node (non-recursively)
                    {
                        ColumnNode child = GetChild(col);
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
        /// Create and add child nodes for subtables of this node and direct greater columns. 
        /// </summary>
        public void ExpandTree(bool recursively = true)
        {
            if (Input == null) // We cannot expand the set but try to expand the existing children
            {
                if (!recursively) return;
                foreach (ColumnNode c in this)
                {
                    if (!(c is SubtableTree)) continue;
                    ((SubtableTree)c).ExpandTree(recursively);
                }
                return;
            }

            foreach (DcColumn sd in Input.SubColumns)
            {
                if (ExistsChild(sd)) continue;

                // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                ColumnNode child = (ColumnNode)Activator.CreateInstance(this.GetType(), new Object[] { sd, null });
                this.AddChild(child);

                if (recursively && (child is SubtableTree)) ((SubtableTree)child).ExpandTree(recursively);
            }

            // Add child nodes for greater column (no recursion)
            foreach (DcColumn gd in Input.Columns)
            {
                if (gd.IsSuper) continue;
                if (ExistsChild(gd)) continue;

                // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                ColumnNode child = (ColumnNode)Activator.CreateInstance(this.GetType(), new Object[] { gd, null });
                this.AddChild(child);
            }
        }

        public SubtableTree(DcColumn col, ColumnNode parent = null)
            : base(col, parent)
        {
            // Register for events from the schema (or inside constructor?)
        }

    }

}
