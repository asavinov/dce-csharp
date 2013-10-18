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

    /// <summary>
    /// Mappings for many source and target sets. 
    /// </summary>
    public class Mapper
    {
        public List<SetMapping> Mappings { get; private set; }

        public SetMapping GetBestMapping(Set sourceSet, Set targetSet = null)
        {
            SetMapping bestMapping = null;
            var setMappings = Mappings.Where(m => m.SourceSet == sourceSet); // Try to find available mappings

            if (setMappings.Count() > 0)
            {
                double bestSimilarity = setMappings.Max(m => m.Similarity);
                bestMapping = setMappings.First(m => m.Similarity == bestSimilarity);
            }

            return bestMapping;
        }

        public Set GetBestTarget(Set sourceSet, Set targetSet = null)
        {
            SetMapping bestMapping = GetBestMapping(sourceSet, targetSet);
            return bestMapping == null ? null : bestMapping.TargetSet;
        }

        /// <summary>
        /// Create target mappings for the specified set and store them in the mapper. Mappings for all greater sets will be used and created if they do not exist in the mapper. 
        /// </summary>
        public void RecommendMappings(Set sourceSet, SetRoot targetSchema, double setCreationThreshold)
        {
            if (sourceSet.IsPrimitive)
            {
                Set ts = targetSchema.GetPrimitiveSubset(targetSchema.MapToLocalType(sourceSet.Name));

                SetMapping primMapping = new SetMapping(sourceSet, ts);
                primMapping.Similarity = 1.0;
                Mappings.Add(primMapping);

                return;
            }

            Dictionary<Dim, SetMapping> greaterMappings = new Dictionary<Dim, SetMapping>();

            //
            // 1. Find target greater sets. They are found among mappings and hence can contain both existing (in the schema) and new sets. 
            //
            List<Set> targetGreaterSets = new List<Set>();

            foreach (Dim sd in sourceSet.GreaterDims)
            {
                SetMapping gMapping = GetBestMapping(sd.GreaterSet);

                if (gMapping == null) // Either does not exist or cannot be built (for example, formally not possible or meaningless)
                {
                    RecommendMappings(sd.GreaterSet, targetSchema, setCreationThreshold); // Recursion up to primitive sets if not computed and stored earlier
                    gMapping = GetBestMapping(sd.GreaterSet); // Try again
                }

                greaterMappings.Add(sd, gMapping);

                targetGreaterSets.Add(gMapping != null ? gMapping.TargetSet : null);
            }

            //
            // 2. Now find the best (existing) lesser set for the target greater sets. The best set should cover most of them by its greater dimensions
            //
            List<Set> allTargetSets = targetSchema.GetAllSubsets();
            double[] coverage = new double[allTargetSets.Count];
            double maxCoverage = 0;
            int maxCoverageIndex = -1;

            for (int i = 0; i < allTargetSets.Count; i++)
            {
                // Find coverage of this target set (how many best greater target sets it covers)
                coverage[i] = 0;
                foreach (Set tgs in allTargetSets[i].GetGreaterSets())
                {
                    if (!targetGreaterSets.Contains(tgs)) continue;

                    // TODO: Compare dimension names and then use it as a weight [0,1] instead of simply incrementing
                    coverage[i] += 1;
                }
                coverage[i] /= targetGreaterSets.Count; // Normalize to [0,1]
                if (coverage[i] > 1) coverage[i] = 1; // A lesser set can use (reference, cover) a greater set more than once

                // Take into account individual similarity of the target set with the source set
                double nameSimilarity = StringSimilarity.computeNGramSimilarity(sourceSet.Name, allTargetSets[i].Name, 3);
                coverage[i] *= nameSimilarity;

                // TODO: Take into account difference in max ranks

                if (coverage[i] > maxCoverage)
                {
                    maxCoverage = coverage[i];
                    maxCoverageIndex = i;
                }
            }

            //
            // 3. Create and store a mapping (or several mappings) 
            //
            SetMapping newMapping = null;
            if (maxCoverage < setCreationThreshold) // Create new target set for mapping (and its greater dimensions) which will be accessible only via the mapping object (not via the schema)
            {
                Set ts = new Set(sourceSet.Name); // New set has the same name as the soure set

                newMapping = new SetMapping(sourceSet, ts);

                foreach (Dim sd in sourceSet.GreaterDims) // For each source dimension, create one new target dimension 
                {
                    SetMapping gMapping = greaterMappings[sd];
                    Set gts = gMapping.TargetSet;

                    Dim td = gts.CreateDefaultLesserDimension(sd.Name, ts); // Create a clone for the source dimension
                    td.IsIdentity = sd.IsIdentity;

                    newMapping.AddPaths(sd, td, gMapping); // Add a pair of dimensions as a match (with expansion using the specified greater mapping)
                }

                newMapping.Similarity = 1.0;
                Mappings.Add(newMapping);
            }
            else // Use existing target set(s) for mapping(s)
            {
                Set ts = allTargetSets[maxCoverageIndex];

                newMapping = new SetMapping(sourceSet, ts);

                foreach (Dim sd in sourceSet.GreaterDims) // For each source dimension, find best target dimension 
                {
                    SetMapping gMapping = greaterMappings[sd];
                    Set gts = gMapping.TargetSet;

                    // Find an existing dimension from ts to gts with the best similarity to source dim sd
                    Dim td = null;
                    var tDims = ts.GreaterDims.Where(d => d.GreaterSet == gts); // All target dimensions from ts to gts
                    if (tDims != null && tDims.Count() > 0)
                    {
                        // TODO: In fact, we need to choose the best dimension, for example, by comparing their names, usages, ranks and other semantic factors
                        td = tDims.ToList()[0];
                    }

                    if (td == null) // No good target dimension found (the source dimension is not covered)
                    {
                        continue; // TODO: Maybe create a new target dimension rather than simply ingnoring it
                    }

                    td.IsIdentity = sd.IsIdentity;

                    newMapping.AddPaths(sd, td, gMapping); // Add a pair of dimensions as a match (with expansion using the specified greater mapping)
                }

                newMapping.Similarity = maxCoverage;
                Mappings.Add(newMapping);
            }
        }

        /// <summary>
        /// Import the specified set along with all its greater sets as a subset of the specified parent set (normally root). 
        /// The set is not populated but is ready to be populated. 
        /// It is a convenience method simplifying a typical operation. 
        /// </summary>
        public static Set ImportSet(Set sourceSet, SetRoot targetSchema)
        {
            Mapper mapper = new Mapper();
            mapper.RecommendMappings(sourceSet, targetSchema, 1.0);
            SetMapping bestMapping = mapper.GetBestMapping(sourceSet);
            Set targetSet = bestMapping.TargetSet;
            DimTree tree = bestMapping.GetTargetTree();

            tree.IncludeInSchema(targetSchema); // Include new elements in schema

            // Configure set for import
            Expression expr = bestMapping.GetTargetExpression(); // Build a tuple tree with paths in leaves
            targetSet.ImportExpression = expr;
            string importDimName = sourceSet.Name; // The same as the source (imported) set name
            DimImport importDim = new DimImport(importDimName, targetSet, sourceSet);
            importDim.Add();

            return targetSet;
        }

        public Mapper()
        {
            Mappings = new List<SetMapping>();
        }

    }

    /// <summary>
    /// The class describes one mapping between two concrete sets as a collection of path pairs. 
    /// </summary>
    public class SetMapping
    {
        public List<PathMatch> Matches { get; private set; }

        public double Similarity { get; set; }

        private Set _sourceSet;
        public Set SourceSet 
        {
            get { return _sourceSet; }
            set
            {
                if (_sourceSet == null) { _sourceSet = value; return; }
                if (_sourceSet == value) return;
                List<DimPath> lesserSegs = value.GetGreaterPaths(_sourceSet);
                List<DimPath> greaterSegs = _sourceSet.GetGreaterPaths(value);
                if (lesserSegs != null && lesserSegs.Count > 0) // New set is a lesser set for the current
                {
                    foreach (PathMatch m in Matches) // Insert new segments
                    {
                        m.SourcePath.InsertPrefix(lesserSegs[0]);
                    }
                }
                else if (greaterSegs != null && greaterSegs.Count > 0) // New set is a greater set for the current
                {
                    foreach (PathMatch m in Matches) // Remove segments
                    {
                        if (m.SourcePath.StartsWith(greaterSegs[0])) m.SourcePath.RemovePrefix(greaterSegs[0]);
                        else Matches.Remove(m);
                    }
                }
                else // Otherwise
                {
                    Matches.Clear();
                }
                _sourceSet = value;
            }
        }

        private Set _targetSet;
        public Set TargetSet
        {
            get { return _targetSet; }
            set
            {
                // TODO: The same implementation as for the source set (think on reuse, e.g., introduce one method and then only determine what is source and what is target)
                _targetSet = value;
            }
        }

        public PathMatch GetMatchForSource(DimPath path)
        {
            throw new NotImplementedException();
        }

        public PathMatch GetMatchForTarget(DimPath path)
        {
            throw new NotImplementedException();
        }

        public void AddPaths(Dim sd, Dim td, SetMapping gMapping) // Add this pair by expanding it using the mapping
        {
            Debug.Assert(sd != null && sd.LesserSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(td != null && td.LesserSet == TargetSet, "Wrong use: target path must start from the target set.");

            Debug.Assert(sd != null && sd.GreaterSet == gMapping.SourceSet, "Wrong use: source path must end where the mapping starts.");
            Debug.Assert(td != null && td.GreaterSet == gMapping.TargetSet, "Wrong use: target path must end where the mapping starts.");

            if (gMapping.Matches.Count == 0) // If there are no continuations then add only the starting segments (for example, for mappings between primitive sets)
            {
                DimPath sp = new DimPath(); // A path consists of one segment
                sp.AppendSegment(sd);

                DimPath tp = new DimPath(); // A path consists of one segment
                tp.AppendSegment(td);

                PathMatch match = new PathMatch(sp, tp);
                Matches.Add(match);
            }

            foreach (PathMatch gMatch in gMapping.Matches)
            {
                DimPath sp = new DimPath(); // Create source path by concatenating one segment and continuation path from the mapping
                sp.AppendSegment(sd);
                sp.AppendPath(gMatch.SourcePath);

                DimPath tp = new DimPath(); // Create target path by concatenating one segment and continuation path from the mapping
                tp.AppendSegment(td);
                tp.AppendPath(gMatch.TargetPath);

                PathMatch match = new PathMatch(sp, tp);
                Matches.Add(match);
            }
        }

        public void AddPathMatch(PathMatch match)
        {
            Debug.Assert(match != null && match.SourceSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(match != null && match.TargetSet == TargetSet, "Wrong use: target path must start from the target set.");

            // TODO: Check coverage between paths and mappings (pairs of paths)
            // Rule 1: more specific (longer) path covers and merges a more general (shorter) path. So shorter paths are not needed and can be removed 
            // Rule 2: Yet, if we remove them we lose information because they represent intermediate independent mappings. For example, when the added (long, specific) mapping is again removed then we cannot find the merged mappings so the operation is not reversible. 
            // Thus we should retain more general mappings for future use. 
            // Rule 3: The question is what to do if a more general mapping is deleted? Should we delete also more specific mappings?
            // Rule 4: Is it important to have roles: primary/secondary? If yes, then we should use adding path pairs. 

            // Rule 0: What we have to delete are contradicting/incompatible matches which are overwritten by this match (where we get more than one alternative matching path for this path).
            // What incompatible means? 
            // - For one path (one means all covered or all continuations?), different other paths are present

            foreach(PathMatch m in Matches)
            {
                // - If one path is covered (but not equal) then the second path must be also covered (so this match always continues existing matches)
                //   - coverage means continuation of the *whole* path (so any path covers the root which empty path)
                //   - if one path covers then the other has to be fit into the second, that is, covered by force or by cutting it until it is covered (if the second is also covered then nothing has to be done)
                //   - if one path is covered then - ?


                // - in the case of no coverage, they have a point of intersection. Then - ? Should this point of intersection be within the seconad path or otherwise constrained?

                // if two sets (that is, paths) are matched, then only their continuations are possible
                // problem: two sets can be matched explcitly or implicitly, that is, their match logically follows from other path matches.
                // Implicit match: intersections of two paths produce a more general match

                // One possible approach is to try to add a match by adding its path segments and checking consistency conditions

            }

            Matches.Add(match);
        }

        public bool AreMatched(PathMatch match) // Determine if both paths are matched (true if there exist the same or more specific match)
        {
            foreach (PathMatch m in Matches)
            {
                if(!m.SourcePath.StartsWith(match.SourcePath)) continue;
                if(!m.TargetPath.StartsWith(match.TargetPath)) continue;
                return true;
            }

            return false; // Not found
        }

        public DimTree GetSourceTree() // Convert all source paths into a dimension tree where the source set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(SourceSet);
            Matches.ForEach(p => tree.AddPath(p.SourcePath));
            return tree;
        }

        public DimTree GetTargetTree() // Convert all target paths into a dimension tree where the target set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(TargetSet);
            Matches.ForEach(p => tree.AddPath(p.TargetPath));
            return tree;
        }

        public Expression GetSourceExpression()
        {
            throw new NotImplementedException();
        }

        public Expression GetTargetExpression()
        {
            Expression expr = new Expression(null, Operation.TUPLE, TargetSet); // Create a root tuple expression object 
            foreach (PathMatch match in Matches)
            {
                Expression varExpr = new Expression("source", Operation.VARIABLE, match.TargetSet); // Add Input of function as a variable the values of which (output) can be assigned for evaluation of the function during export/import

                Expression funcExpr = null;
                Dim srcPath = match.SourceSet.GetGreaterPath(match.SourcePath.Path); // First, we try to find a direct path/function 
                if (srcPath == null) // No direct path
                {
                    srcPath = match.SourcePath; // use a sequence of dimensions/functions
                    funcExpr = Expression.CreateProjectExpression(srcPath.Path, Operation.DOT);
                }
                else // There is a direct path (relation attribute in a relational data source). Use attribute name as function name
                {
                    funcExpr = new Expression(srcPath.Name, Operation.DOT, match.TargetPath.GreaterSet);
                }

                funcExpr.Input = varExpr;

                Expression leafTuple = expr.AddPath(match.TargetPath);
                leafTuple.Input = funcExpr;
            }

            return expr;
        }

        public SetMapping(Set sourceSet, Set targetSet)
        {
            Debug.Assert((sourceSet.IsPrimitive && targetSet.IsPrimitive) || (!sourceSet.IsPrimitive && !targetSet.IsPrimitive), "Wrong use: cannot create a mapping between a primitive set and a non-primitive set.");

            Matches = new List<PathMatch>();

            SourceSet = sourceSet;
            TargetSet = targetSet;
        }

    }

    /// <summary>
    /// A pair of matching paths.
    /// </summary>
    public class PathMatch
    {
        public DimPath SourcePath { get; private set; }
        public DimPath TargetPath { get; private set; }
        public double Similarity { get; set; }

        public Set SourceSet { get { return SourcePath == null ? null : SourcePath.LesserSet; } }
        public Set TargetSet { get { return TargetPath == null ? null : TargetPath.LesserSet; } }

        public PathMatch(DimPath sourcePath, DimPath targetPath)
        {
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Similarity = 1.0;
        }
    }

    /// <summary>
    /// It displays the current state of mapping between two sets as properties of the tree nodes depending on the role of the tree. 
    /// </summary>
    public class MatchTreeRoot : MatchTreeNode
    {
        public SetMapping Mapping { get; set; } // Where matches are stored between paths of this tree (note that paths start from the children of this root element - not from this root)

        public bool IsSource { get; set; } // Whether this tree corresponds to source paths (or target paths) in the mappings. It determines the semantics/direction of mapping (import/export). 
        public bool IsPrimary { get; set; } // Defines how the node properties are computed and displayed as well as the logic of the tree. 
        public bool OnlyPrimitive { get; set; } // Only primitive dimensions/paths can be matched (not intermediate). So intermediate elemens are not matched (but might display information about matches derived from primitive elements).
        public bool CanChangeSet { get; set; } // Whether it is possible to change the set being mapped in the mapping object.

        // This is important for generation of the current status: disabled/enabled, relevance etc.
        public MatchTreeNode SelectedNode { get; set; } // Selected in this tree
        public MatchTreeNode SelectedCounterNode { get; set; } // In another tree

        public DimPath SelectedPath // Selected in this tree
        { 
            get 
            {
                if (SelectedNode == null) return null;
                DimPath selectedPath = SelectedNode.DimPath;
                if (IsSource)
                {
                    selectedPath.TrimPrefix(Mapping.SourceSet);
                }
                else
                {
                    selectedPath.TrimPrefix(Mapping.TargetSet);
                }
                return selectedPath;
            } 
        }
        public DimPath SelectedCounterPath // In another tree
        {
            get
            {
                if (SelectedCounterNode == null) return null;
                DimPath selectedCounterPath = SelectedCounterNode.DimPath;
                if (IsSource)
                {
                    selectedCounterPath.TrimPrefix(Mapping.SourceSet);
                }
                else
                {
                    selectedCounterPath.TrimPrefix(Mapping.TargetSet);
                }
                return selectedCounterPath;
            }
        }

        /// <summary>
        /// Primary: returns true if this node has any match and false otherwise (not assigned). 
        /// Secondary: returns true if this node is matched against the currently selected primary node and false otherwise (so only one node in the whole secondary tree is true).
        /// </summary>
        /// <returns></returns>
        public bool IsMatched()
        {
            PathMatch match;
            DimPath counterPath;
            if (IsSource)
            {
                match = Mapping.GetMatchForSource(SelectedPath);
                counterPath = match != null ? match.TargetPath : null;
            }
            else
            {
                match = Mapping.GetMatchForTarget(SelectedPath);
                counterPath = match != null ? match.SourcePath : null;
            }

            if (IsPrimary)
            {
                return match != null;
            }
            else
            {
                if (match == null) return false;
                return counterPath.StartsWith(SelectedCounterPath);
            }
        }

        /// <summary>
        /// Primary: does not do anything (or calls the same method of the secondary tree). 
        /// Secondary: takes the currently selected primary node and this secondary node and adds this match to the list. Previous match is deleted. Contradictory matches are removed. Match status of nodes needs to be redrawn.
        /// </summary>
        public PathMatch AddMatch()
        {
            PathMatch match;
            if (IsSource)
            {
                match = new PathMatch(SelectedPath, SelectedCounterPath);
            }
            else
            {
                match = new PathMatch(SelectedCounterPath, SelectedPath);
            }

            match.Similarity = 1.0;
            Mapping.AddPathMatch(match); // Some existing matches (which contradict to the added one) can be removed

            return match;
        }
    }

    /// <summary>
    /// It provides methods and propoerties for a node which depend on the current mappings and role of the tree. 
    /// The class assumes that the root of the tree is a special node storing mappings, tree roles and other necessary data. 
    /// </summary>
    public class MatchTreeNode : DimTree
    {
        // For all these methods, if we really want the status of this node, then SelectedNode=this should be assigned in the root

        public bool IsMatched()
        {
            return ((MatchTreeRoot)Root).IsMatched();
        }

        public PathMatch AddMatch()
        {
            return ((MatchTreeRoot)Root).AddMatch();
        }

        /// <summary>
        /// Primary: does not do anything (or calls the same method of the secondary tree). 
        /// Secondary: remove the current match so this secondary is node (and the corresponding primary node) are not matched anymore. Works only if the two nodes are currently matched. 
        /// </summary>
        public PathMatch RemoveMatch()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Enabled/disabled status of a secondary node. Whether the current paths can be added as a new match without contradiction to the existing matches.
        /// Primary: always true. 
        /// Secondary: given a primary currently selected node, compute if a match with this secondary node does not contradict to existing matches (so it can be added). Alternatively, if relevances are precomputed then we find if relevance is higher than 0.
        /// </summary>
        public bool CanAddMatch()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Primary: either not shown or 1.0 or relevance of the current (existing) match if it exists for this element retrieved from the mapper. 
        /// Secondary: if it is already matched then relevance of existing match (the same as for primary) and if it is not chosen (not current) then the relevance computed by the recommender. 
        /// </summary>
        public double GetMatchRelevance()
        {
            throw new NotImplementedException();
        }

    }

    public class DimTree : IEnumerable<DimTree>, INotifyCollectionChanged
    {
        /// <summary>
        /// It is one element of the tree. It is null for the bottom (root) and its direct children which do not have lesser dimensions.
        /// </summary>
        private Dim _dim;
        public Dim Dim { get { return _dim; } set { _set = (value != null ? value.GreaterSet : null); _dim = value; } }

        /// <summary>
        /// It is a set corresponding to the node. If dimension is present then it is equal to the greater set.  
        /// It is null only for the bottom (root). It can be set only if dimension is null (otherwise set the dimension). 
        /// </summary>
        private Set _set;
        public Set Set { 
            get { return _set; } 
            set 
            { 
                if (value == _set) return;
                if (_dim != null) return;
                _set = value; 
            } 
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
            Debug.Assert(Set == null || child.Dim == null || child.Dim.LesserSet == Set, "Wrong use: a new dimension must start from this set.");
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
                    child = new DimTree(seg, node);
                }

                node = child;
            }

            return node;
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
                if(node == this || node.Set == null) return null;
                return  node;
            }
        }
        public int DimRank
        {
            get
            {
                int rank = 0;
                for (DimTree node = this; node.Dim != null && node.Parent != null; node = node.Parent) rank++;
                return rank;
            }
        }
        public DimPath DimPath
        {
            get
            {
                if (Dim == null) return null;
                DimPath path = new DimPath();
                for (DimTree node = this; node.Dim != null && node.Parent != null; node = node.Parent) path.InsertSegment(node.Dim);
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
            for(int r=0; r<maxRank; r++) res.Add(new List<DimTree>());

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

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, e);
            }
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

            List<Set> sets = new List<Set>( new[] {Set} );
            sets.AddRange(Set.GetAllSubsets());

            foreach (Set s in sets)
            {
                foreach (Dim d in s.GreaterDims)
                {
                    if (ExistsChild(d)) continue;
                    // New child instances need to have the type of this instance (this instance can be an extension of this class so we do not know it)
                    DimTree child = (DimTree)Activator.CreateInstance(this.GetType(), new Object[] {d, this});
                    if (recursively) child.ExpandTree(recursively);
                }
            }
        }

        /// <summary>
        /// If a set is not included in the schema then include it. Inclusion is performed by storing all dimensions into the set including (a new) super-dimension. 
        /// TODO: In fact, all elements should have super-dimensions which specify the parent set or the root of the schema to include into, and then the parameter is not needed. 
        /// </summary>
        public void IncludeInSchema(Set root)
        {
            List<Set> allSets = Set.GetAllSubsets();
            if (!allSets.Contains(Set))
            {
                root.AddSubset(Set);
            }

            foreach (DimTree node in Children)
            {
                if (node.Dim == null) continue; // Root has null dimension

                if (!node.Dim.IsInLesserSet) node.Dim.IsInLesserSet = true;
                if (!node.Dim.IsInGreaterSet) node.Dim.IsInGreaterSet = true;

                node.IncludeInSchema(root); // Recursion
            }
        }

/*
        /// <summary>
        /// Find first element in the specified match tree (exclusive) that references this element. Start from children and skip null matches. 
        /// </summary>
        public MatchTree FindFirstMatchFor(MatchTree matchTree)
        {
            foreach (MatchTree c in matchTree.Children)
            {
                if (c.DimMatches.SelectedObject == null) // Recursion if null match
                {
                    MatchTree childTree = this.FindFirstMatchFor(c);
                    if (childTree != null) return childTree;
                }
                else if (c.DimMatches.SelectedObject == this)
                {
                    return c;
                }
            }
            return null; // No element from this subtree references (matches) the specified element
        }
*/
/*
        /// <summary>
        /// Create an equivalent expression for matching from this tree to the specified match tree. 
        /// The expression will have the structure of this dimension tree (not the specified match tree).
        /// </summary>
        /// <returns></returns>
        public Expression GetInverseExpression(MatchTree matchTree)
        {
            //
            // Create a tuple expression object for this node of the tree only
            //
            Expression expr = new Expression(Dim != null ? Dim.Name : null, Operation.TUPLE, Set);
            expr.Dimension = Dim;

            MatchTree source = FindFirstMatchFor(matchTree); // Who references us from match tree?

            // For a leaf, define the primitive value in terms of the matching tree (it is a DOT expression representing a primitive path in the target tree)
            if (IsLeaf && source != null)
            {
                Debug.Assert(Set.IsPrimitive, "Wrong structure: Leaves of the match tree have to be primitive sets.");

                // Build function expression for computing a primitive value from the matched target tree
                DimPath targetPath = source.DimPath;
                Expression funcExpr = Expression.CreateProjectExpression(targetPath.Path, Operation.DOT);
                funcExpr.Input = new Expression("source", Operation.VARIABLE, targetPath.LesserSet); // Add Input of function as a variable the values of which (output) can be assigned during export

                expr.Input = funcExpr; // Add function to the leaf expression
            }
            else // Recursion: create a tuple expressions for all children and add them to the parent tuple
            {
                foreach (MatchTree c in Children)
                {
                    if (c.Dim != null && c.Dim is DimSuper)
                        expr.Input = c.GetInverseExpression(source == null ? matchTree : source);
                    else
                        expr.AddOperand(c.GetInverseExpression(source == null ? matchTree : source));
                }
            }


            return expr;
        }
*/
        public DimTree(Dim dim, DimTree parent = null)
        {
            Dim = dim;
            Children = new List<DimTree>();
            if(parent != null) parent.AddChild(this);
        }

        public DimTree(Set set, DimTree parent = null)
        {
            Set = set;
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

    public enum MappingDirection
    {
        SOURCE, // Data flow in the direction FROM this set to a target set
        TARGET, // Data flow in the direction TO this element from a source set
    }


    class StringSimilarity
    {
        public static double computeNGramSimilarity(string source, string target, int gramlength)
        {
            if (source == null || target == null || source.Length == 0 || target.Length == 0) return 0;

            List<string> sourceGrams = generateNGrams(source, gramlength);
            List<string> targetGrams = generateNGrams(target, gramlength);

            int similarGrams = 0;
            for (int i = 0; i < sourceGrams.Count; i++)
            {
                string s1 = sourceGrams[i];
                for (int j = 0; j < targetGrams.Count; j++)
                {
                    if (s1.Equals(targetGrams[j], StringComparison.InvariantCultureIgnoreCase))
                    {
                        similarGrams++;
                        break;
                    }
                }
            }
            return (2.0 * similarGrams) / (sourceGrams.Count + targetGrams.Count);
        }

        private static List<string> generateNGrams(string str, int gramlength)
        {
            if (str == null || str.Length == 0) return null;

            int length = str.Length;
            List<string> grams;
            string gram;
            if (length < gramlength)
            {
                grams = new List<string>(length + 1);
                for (int i = 1; i <= length; i++)
                {
                    gram = str.Substring(0, i - 0);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
                gram = str.Substring(length - 1, length - (length - 1));
                if (grams.IndexOf(gram) == -1) grams.Add(gram);
            }
            else
            {
                grams = new List<string>(length - gramlength + 1);
                for (int i = 1; i <= gramlength - 1; i++)
                {
                    gram = str.Substring(0, i - 0);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
                for (int i = 0; i < length - gramlength + 1; i++)
                {
                    gram = str.Substring(i, i + gramlength - i);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
                for (int i = length - gramlength + 1; i < length; i++)
                {
                    gram = str.Substring(i, length - i);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
            }
            return grams;
        }

    }

/*
    /// <summary>
    /// The class describes a mapping from one set to another set.
    /// </summary>
    public class SetMapping
    {
        public Set SourceSet { get; private set; }
        public Set TargetSet { get; private set; }

        public double Similarity { get; private set; }

        public List<DimPath> SourcePaths { get; private set; }
        public List<DimPath> TargetPaths { get; private set; }

        public void Add(DimPath sourcePath, DimPath targetPath)
        {
            Debug.Assert(sourcePath.LesserSet == SourceSet, "Wrong use: Source path must have its lesser set equal to the source set of the mapping.");
            Debug.Assert(targetPath.LesserSet == TargetSet, "Wrong use: Target path must have its lesser set equal to the target set of the mapping.");

            // TODO: Check if these paths already exists or they cover some existing paths

            SourcePaths.Add(sourcePath);
            TargetPaths.Add(targetPath);
        }

        public void Remove(int index)
        {
        }

        public void Remove(DimPath path) // Either source or target
        {
        }

        public void Reverse() // Changa the direction
        {
        }

        // TODO: we need to have a direction (semantics) somewhere: either in parameters or in how the result is stored
        public static List<SetMapping> RecommendSetMappings(Set sourceSet, SetRoot targetSets, double setCreationThreshold)
        {
            var mappings = new List<SetMapping>();

            return mappings;
        }

        public SetMapping(Set sourceSet, Set targetSet)
        {
            Debug.Assert(sourceSet != targetSet, "Wrong use: mapping to the same set is not permitted.");
            SourceSet = sourceSet;
            TargetSet = targetSet;
        }

    }
*/
}
