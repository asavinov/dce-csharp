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
    /// It implements mapping recommendations. 
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
        public void RecommendMappings(Set sourceSet, SetTop targetSchema, double setCreationThreshold)
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
        public static Set ImportSet(Set sourceSet, SetTop targetSchema)
        {
            Mapper mapper = new Mapper();
            mapper.RecommendMappings(sourceSet, targetSchema, 1.0);
            SetMapping bestMapping = mapper.GetBestMapping(sourceSet);

            DimImport dimImport = new DimImport(bestMapping);
            dimImport.Add();
            
            return bestMapping.TargetSet;
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
                        m.SourcePath.InsertFirst(lesserSegs[0]);
                    }
                }
                else if (greaterSegs != null && greaterSegs.Count > 0) // New set is a greater set for the current
                {
                    foreach (PathMatch m in Matches) // Remove segments
                    {
                        if (m.SourcePath.StartsWith(greaterSegs[0])) m.SourcePath.RemoveFirst(greaterSegs[0]);
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

        public bool IsSourcePathValid(DimPath path)
        {
            if (path.LesserSet != SourceSet && !path.LesserSet.IsGreater(SourceSet)) return false;
            return true;
        }
        public bool IsTargetPathValid(DimPath path)
        {
            if (path.LesserSet != TargetSet && !path.LesserSet.IsGreater(TargetSet)) return false;
            return true;
        }

        public bool AreMatched(PathMatch match) // Determine if both paths are matched (true if there exist the same or more specific match)
        {
            foreach (PathMatch m in Matches)
            {
                if (m.Matches(match)) return true;
            }
            return false; // Not found
        }

        public PathMatch GetMatchForSource(DimPath path) // Find a match with this path
        {
            foreach(PathMatch m in Matches) 
            {
                if (m.MatchesSource(path)) return m;
            }
            return null;
        }

        public PathMatch GetMatchForTarget(DimPath path) // Find a match with this path
        {
            foreach (PathMatch m in Matches)
            {
                if (m.MatchesTarget(path)) return m;
            }
            return null;
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
                sp.InsertLast(sd);

                DimPath tp = new DimPath(); // A path consists of one segment
                tp.InsertLast(td);

                PathMatch match = new PathMatch(sp, tp);
                Matches.Add(match);
            }

            foreach (PathMatch gMatch in gMapping.Matches)
            {
                DimPath sp = new DimPath(); // Create source path by concatenating one segment and continuation path from the mapping
                sp.InsertLast(sd);
                sp.InsertLast(gMatch.SourcePath);

                DimPath tp = new DimPath(); // Create target path by concatenating one segment and continuation path from the mapping
                tp.InsertLast(td);
                tp.InsertLast(gMatch.TargetPath);

                PathMatch match = new PathMatch(sp, tp);
                Matches.Add(match);
            }
        }

        public void AddMatch(PathMatch match)
        {
            Debug.Assert(match != null && match.SourceSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(match != null && match.TargetSet == TargetSet, "Wrong use: target path must start from the target set.");


            // In the case of coverage we do nothing. We want to keep more general matches (between sets) while more specific can be added/removed independently (if they satisfy them). 
            // ??? in the case of no coverage, they have a point of intersection. Then - ? Should this point of intersection be within the seconad path or otherwise constrained?
            // Problem: two sets can be matched explcitly or implicitly, that is, their match logically follows from other path matches.
            // Implicit match: intersections of two paths produce a more general match
            // Alternative approach: Incrementally add path segments and checking consistency conditions

            List<PathMatch> toRemove = new List<PathMatch>();
            foreach (PathMatch m in Matches)
            {
                if (!m.Compatible(match))
                {
                    toRemove.Add(m);
                }
            }

            Matches.Add(match); // Now it is guaranteed to be compatible with all existing matches
        }

        public void AddMatches(List<PathMatch> matches) // Import all path matches (as new objects) that fit into this mapping
        {
            foreach (PathMatch m in matches)
            {
                PathMatch match = new PathMatch(m);
                AddMatch(match);
            }
        }

        public void RemoveMatch(DimPath sourcePath, DimPath targetPath) // Remove the specified and all more specific matches (continuations)
        {
            Debug.Assert(sourcePath.LesserSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(targetPath.LesserSet == TargetSet, "Wrong use: target path must start from the target set.");

            List<PathMatch> toRemove = new List<PathMatch>();
            foreach (PathMatch m in Matches)
            {
                // If existing match is the same or more specific than the specified match to be removed
                if (m.MatchesSource(sourcePath) && m.MatchesTarget(targetPath)) 
                {
                    toRemove.Add(m);
                }
            }

            toRemove.ForEach(m => Matches.Remove(m));
        }

        public DimTree GetSourceTree() // Convert all source paths into a dimension tree where the source set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(SourceSet);
            Matches.ForEach(p => tree.AddPath(p.SourcePath));
            return tree;
        }
        public DimTree GetSourceTree(Set set) // Convert all paths of the specified set into a dimension tree where the source set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(set);
            foreach (PathMatch m in Matches)
            {
                // if(m.SourcePath.LesserSet != set) continue; // Use this if we want to take into account only paths *starting* from this set (rather than crossing this set)
                int index = m.SourcePath.IndexOf(set);
                if (index < 0) continue; // The path does not cross this set

                tree.AddPath(m.SourcePath.SubPath(index));
            }

            return tree;
        }


        public DimTree GetTargetTree() // Convert all target paths into a dimension tree where the target set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(TargetSet);
            Matches.ForEach(p => tree.AddPath(p.TargetPath));
            return tree;
        }
        public DimTree GetTargetTree(Set set) // Convert all target paths into a dimension tree where the target set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(set);
            foreach (PathMatch m in Matches)
            {
                // if(m.TargetPath.LesserSet != set) continue; // Use this if we want to take into account only paths *starting* from this set (rather than crossing this set)
                int index = m.TargetPath.IndexOf(set);
                if (index < 0) continue; // The path does not cross this set

                tree.AddPath(m.TargetPath.SubPath(index));
            }

            return tree;
        }

        public Expression GetSourceExpression()
        {
            throw new NotImplementedException();
        }

        public Expression GetTargetExpression()
        {
            return GetTargetExpression(null, null);
        }
        public Expression GetTargetExpression(Dim sourceDim, Dim targetDim) // Prefix effectively changes the source set and will be added as a prefix to all paths. It is used for dimension mapping (type change).
        {
            Expression expr = new Expression(null, Operation.TUPLE, TargetSet); // Create a root tuple expression object 
            if (targetDim != null) expr.Dimension = targetDim;

            foreach (PathMatch match in Matches)
            {
                // Add Input of function as a variable the values of which (output) can be assigned for evaluation of the function during export/import
                Expression varExpr = new Expression("source", Operation.VARIABLE, sourceDim == null ? match.SourceSet : sourceDim.LesserSet);

                Expression funcExpr = null;
                DimPath srcPath = match.SourceSet.GetGreaterPath(match.SourcePath.Path); // First, we try to find a direct path/function 
                if (srcPath == null) // No direct path
                {
                    srcPath = match.SourcePath; // use a sequence of dimensions/functions
                    if (sourceDim != null) srcPath.InsertFirst(sourceDim);
                    funcExpr = Expression.CreateProjectExpression(srcPath.Path, Operation.DOT, varExpr);
                }
                else // There is a direct path (relation attribute in a relational data source). Use attribute name as function name
                {
                    funcExpr = new Expression(srcPath.Name, Operation.DOT, match.TargetPath.GreaterSet);
                    funcExpr.Input = varExpr;
                }

                Expression leafTuple = expr.AddPath(match.TargetPath);
                leafTuple.Input = funcExpr;
            }

            return expr;
        }

        public SetMapping(Set sourceSet, Set targetSet)
        {
            // Debug.Assert((sourceSet.IsPrimitive && targetSet.IsPrimitive) || (!sourceSet.IsPrimitive && !targetSet.IsPrimitive), "Wrong use: cannot create a mapping between a primitive set and a non-primitive set.");
            Debug.Assert(sourceSet != null && targetSet != null, "Wrong use: parametes cannot be null.");

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

        public bool MatchesSource(DimPath path) // This is more specific (longer) than argument
        {
            return SourcePath.StartsWith(path);
        }

        public bool MatchesTarget(DimPath path) // This is more specific (longer) than argument
        {
            return TargetPath.StartsWith(path);
        }

        public bool Matches(PathMatch match) // This is more specific (longer) than argument
        {
            return MatchesSource(match.SourcePath) && MatchesTarget(match.TargetPath);
        }

        public bool Compatible(PathMatch match) // The specified match is compatible with (does not contradicts to) this match
        {
            // Main rule: if a coverage condition holds for one path (in one or another direction) then it must hold for the other path (in the same direction)

            // SourcePath -> TargetPath
            if (match.SourcePath.StartsWith(SourcePath)) // The specified match continues this path
            {
                return match.TargetPath.StartsWith(TargetPath); // The same must be true for the other paths
            }
            else if (SourcePath.StartsWith(match.SourcePath)) // Opposite
            {
                return TargetPath.StartsWith(match.TargetPath); // The same must be true for the other paths
            }

            // TargetPath -> SourcePath
            if (match.TargetPath.StartsWith(TargetPath))
            {
                return match.SourcePath.StartsWith(SourcePath);
            }
            else if (TargetPath.StartsWith(match.TargetPath))
            {
                return SourcePath.StartsWith(match.SourcePath);
            }
            
            // Neither source path nor target paths cover. 
            // This means they have some intersection (meet set they both continue). 
            // This meet point defines an implicit match which we do not check but it might contradict to other matches.

            return true;
        }

        public PathMatch(PathMatch m)
        {
            SourcePath = new DimPath(m.SourcePath);
            TargetPath = new DimPath(m.TargetPath);
            Similarity = m.Similarity;
        }

        public PathMatch(DimPath sourcePath, DimPath targetPath)
        {
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Similarity = 1.0;
        }

        public PathMatch(DimPath sourcePath, DimPath targetPath, double similarity)
            : this(sourcePath, targetPath)
        {
            Similarity = 1.0;
        }
    }

    /// <summary>
    /// It stores all necessary information for editing a mapping and the current state of mapping. 
    /// </summary>
    public class MappingModel
    {
        public MatchTree SourceTree { get; private set; }
        public MatchTree TargetTree { get; private set; }

        public SetMapping Mapping { get; set; } // It is the current state of the mapping. And it is what is initialized and returned. 

        private Set _sourceSet;
        public Set SourceSet 
        {
            get { return _sourceSet; }
            set 
            {
                Debug.Assert(value != null, "Wrong use: a set in mapping cannot be null (use root instead).");
                if (_sourceSet == value) return;
                _sourceSet = value;

                Mapping.SourceSet = SourceSet; // Update mapper

                // Update tree
                SourceTree.Children.Clear();
                MatchTreeNode node = new MatchTreeNode(SourceSet);
                SourceTree.AddChild(node);
                node.ExpandTree();
                node.AddSourcePaths(Mapping);
            }
        }

        private Set _targetSet;
        public Set TargetSet
        {
            get { return _targetSet; }
            set
            {
                Debug.Assert(value != null, "Wrong use: a set in mapping cannot be null (use root instead).");
                if (_targetSet == value) return;
                _targetSet = value;

                Mapping.TargetSet = TargetSet; // Update mapper

                // Update tree
                TargetTree.Children.Clear();
                MatchTreeNode node = new MatchTreeNode(TargetSet);
                node.ExpandTree();
                node.AddTargetPaths(Mapping);
                TargetTree.AddChild(node);
            }
        }

        /// <summary>
        /// Primary: returns true if this node has any match and false otherwise (not assigned). 
        /// Secondary: returns true if this node is matched against the currently selected primary node and false otherwise (so only one node in the whole secondary tree is true).
        /// </summary>
        /// <returns></returns>
        public bool IsMatchedSource()
        {
            if (SourceTree.SelectedPath == null) return false;
            return IsMatchedSource(SourceTree.SelectedPath);
        }
        public bool IsMatchedSource(DimPath path)
        {
            PathMatch match = Mapping.GetMatchForSource(path);

            if (match == null) return false;

            if (SourceTree.IsPrimary)
            {
                return true;
            }
            else
            {
                if (TargetTree.SelectedPath == null) return false;
                return match.MatchesTarget(TargetTree.SelectedPath);
            }
        }

        /// <summary>
        /// Primary: returns true if this node has any match and false otherwise (not assigned). 
        /// Secondary: returns true if this node is matched against the currently selected primary node and false otherwise (so only one node in the whole secondary tree is true).
        /// </summary>
        /// <returns></returns>
        public bool IsMatchedTarget()
        {
            if (TargetTree.SelectedPath == null) return false;
            return IsMatchedTarget(TargetTree.SelectedPath);
        }
        public bool IsMatchedTarget(DimPath path)
        {
            PathMatch match = Mapping.GetMatchForTarget(path);

            if (match == null) return false;

            if (TargetTree.IsPrimary)
            {
                return true;
            }
            else
            {
                if (SourceTree.SelectedPath == null) return false;
                return match.MatchesSource(SourceTree.SelectedPath);
            }
        }

        /// <summary>
        /// Secondary: Enabled/disabled status of a secondary node. Whether the current paths can be added as a new match without contradiction to the existing matches.
        /// Secondary: given a primary currently selected node, compute if a match with this secondary node does not contradict to existing matches (so it can be added). Alternatively, if relevances are precomputed then we find if relevance is higher than 0.
        /// Primary: always true (if there is at least one possible secondary match). 
        /// </summary>
        public bool CanMatchTarget(DimPath path)
        {
            if (TargetTree.IsPrimary)
            {
                return true;
            }
            else
            {
                DimPath priPath = SourceTree.SelectedPath;
                if (priPath == null) return false; // Primary node is not selected

                if (!priPath.IsPrimitive || !path.IsPrimitive) return false; // Only primitive paths can be matchd

                return true;
            }
        }

        public bool CanMatchSource(DimPath path)
        {
            // TODO: Copy-paste when ready
            return true;
        }

        /// <summary>
        /// Primary: does not do anything (or calls the same method of the secondary tree). 
        /// Secondary: takes the currently selected primary node and this secondary node and adds this match to the list. Previous match is deleted. Contradictory matches are removed. Match status of nodes needs to be redrawn.
        /// </summary>
        public PathMatch AddMatch()
        {
            if (SourceTree.SelectedPath == null || TargetTree.SelectedPath == null) return null;

            PathMatch match = new PathMatch(SourceTree.SelectedPath, TargetTree.SelectedPath, 1.0);
            Mapping.AddMatch(match); // Some existing matches (which contradict to the added one) will be removed

            return match;
        }

        /// <summary>
        /// Remove the mathc corresponding to the current selections. 
        /// </summary>
        public void RemoveMatch()
        {
            if (SourceTree.SelectedPath == null || TargetTree.SelectedPath == null) return;

            Mapping.RemoveMatch(SourceTree.SelectedPath, TargetTree.SelectedPath); // Also other matches can be removed
        }
        public MappingModel(Dim sourceDim, Dim targetDim)
            : this(sourceDim.GreaterSet, targetDim.GreaterSet)
        {
            SourceTree.Children[0].Dim = sourceDim;
            TargetTree.Children[0].Dim = targetDim;
        }

        public MappingModel(Set sourceSet, Set targetSet)
        {
            Mapping = new SetMapping(sourceSet, targetSet);

            SourceTree = new MatchTree(this);
            SourceTree.IsPrimary = true;
            TargetTree = new MatchTree(this);
            TargetTree.IsPrimary = false;

            SourceSet = sourceSet; // Here also the tree will be constructed
            TargetSet = targetSet;
        }

        public MappingModel(SetMapping mapping)
        {
            Mapping = mapping;

            SourceTree = new MatchTree(this);
            SourceTree.IsPrimary = true;
            TargetTree = new MatchTree(this);
            TargetTree.IsPrimary = false;

            SourceSet = mapping.SourceSet; // Here also the tree will be constructed
            TargetSet = mapping.TargetSet;
        }
    }

    /// <summary>
    /// It displays the current state of mapping between two sets as properties of the tree nodes depending on the role of the tree. 
    /// </summary>
    public class MatchTree : MatchTreeNode
    {
        public MappingModel MappingModel { get; set; }

        public bool IsSource { get { return MappingModel.SourceTree == this;  } } // Whether this tree corresponds to source paths in the mappings.
        public bool IsTarget { get { return MappingModel.TargetTree == this; } } // Whether this tree corresponds to target paths in the mappings. 

        public bool IsPrimary { get; set; } // Defines how the node properties are computed and displayed as well as the logic of the tree. 
        public bool OnlyPrimitive { get; set; } // Only primitive dimensions/paths can be matched (not intermediate). So intermediate elemens are not matched (but might display information about matches derived from primitive elements).

        // This is important for generation of the current status: disabled/enabled, relevance etc.
        public MatchTreeNode SelectedNode { get; set; } // Selected in this tree. Bind tree view selected item to this field.
        public DimPath SelectedPath // Transform selected node into valid selected path
        {
            get
            {
                if (SelectedNode == null) return null;
                DimPath selectedPath = SelectedNode.DimPath;

                // Trimm to source or target set of the mapping in the root
                if(IsSource)
                    selectedPath.RemoveFirst(MappingModel.SourceSet);
                else
                    selectedPath.RemoveFirst(MappingModel.TargetSet);

                return selectedPath;
            }
        }

        public MatchTree CounterTree { get { return IsSource ? MappingModel.TargetTree : MappingModel.SourceTree; } } // Another tree

        public MatchTree(MappingModel model)
            : base()
        {
            MappingModel = model;
        }
    }

    /// <summary>
    /// It provides methods and propoerties for a node which depend on the current mappings and role of the tree. 
    /// The class assumes that the root of the tree is a special node storing mappings, tree roles and other necessary data. 
    /// </summary>
    public class MatchTreeNode : DimTree
    {
        public DimPath MappingPath // Trimmed to source or target set of the mapping in the root
        {
            get
            {
                MatchTree root = (MatchTree)Root;
                DimPath path = DimPath;
                if (root.IsSource)
                    path.RemoveFirst(root.MappingModel.SourceSet);
                else
                    path.RemoveFirst(root.MappingModel.TargetSet);
                return path;
            }
        }

        /// <summary>
        /// Primary: either not shown or 1.0 or relevance of the current (existing) match if it exists for this element retrieved from the mapper. 
        /// Secondary: if it is already matched then relevance of existing match (the same as for primary) and if it is not chosen (not current) then the relevance computed by the recommender. 
        /// </summary>
        public double MatchRelevance
        {
            get
            {
                return 1.0;
            }
        }

        public bool IsMatched
        {
            get
            {
                MatchTree root = (MatchTree)Root;
                MappingModel model = root.MappingModel;

                if (root.IsSource) return model.IsMatchedSource(MappingPath);
                else return model.IsMatchedTarget(MappingPath);
            }
        }

        public bool CanMatch
        {
            get
            {
                MatchTree root = (MatchTree)Root;
                MappingModel model = root.MappingModel;

                if (root.IsSource) return model.CanMatchSource(MappingPath);
                else return model.CanMatchTarget(MappingPath);
            }
        }

        public PathMatch AddMatch()
        {
            MatchTree root = (MatchTree)Root;
            return root.MappingModel.AddMatch();
        }

        /// <summary>
        /// Primary: does not do anything (or calls the same method of the secondary tree). 
        /// Secondary: remove the current match so this secondary is node (and the corresponding primary node) are not matched anymore. Works only if the two nodes are currently matched. 
        /// </summary>
        public PathMatch RemoveMatch()
        {
            throw new NotImplementedException();
        }

        public MatchTreeNode(Dim dim, DimTree parent = null)
            : base(dim, parent)
        {
        }

        public MatchTreeNode(Set set, DimTree parent = null)
            : base(set, parent)
        {
        }

        public MatchTreeNode()
            : base()
        {
        }
    }

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
                if(node == this || node.Set == null) return null;
                return  node;
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

            List<Set> sets = new List<Set>( new[] {Set} );
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
                if (!Dim.IsInLesserSet) Dim.IsInLesserSet = true;
                if (!Dim.IsInGreaterSet) Dim.IsInGreaterSet = true;
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
            if(parent != null) parent.AddChild(this);
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
}
