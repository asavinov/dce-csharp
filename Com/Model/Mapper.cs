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
        public Set SourceSet { get; private set; }
        public Set TargetSet { get; private set; }
        public List<PathMatch> Mapping { get; private set; }
        public double Similarity { get; set; }

        public void AddPaths(Dim sd, Dim td, SetMapping gMapping) // Add this pair by expanding it using the mapping
        {
            Debug.Assert(sd != null && sd.LesserSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(td != null && td.LesserSet == TargetSet, "Wrong use: target path must start from the target set.");

            Debug.Assert(sd != null && sd.GreaterSet == gMapping.SourceSet, "Wrong use: source path must end where the mapping starts.");
            Debug.Assert(td != null && td.GreaterSet == gMapping.TargetSet, "Wrong use: target path must end where the mapping starts.");

            if (gMapping.Mapping.Count == 0) // If there are no continuations then add only the starting segments (for example, for mappings between primitive sets)
            {
                DimPath sp = new DimPath(); // A path consists of one segment
                sp.AppendSegment(sd);

                DimPath tp = new DimPath(); // A path consists of one segment
                tp.AppendSegment(td);

                PathMatch match = new PathMatch(sp, tp);
                Mapping.Add(match);
            }

            foreach (PathMatch gMatch in gMapping.Mapping)
            {
                DimPath sp = new DimPath(); // Create source path by concatenating one segment and continuation path from the mapping
                sp.AppendSegment(sd);
                sp.AppendPath(gMatch.SourcePath);

                DimPath tp = new DimPath(); // Create target path by concatenating one segment and continuation path from the mapping
                tp.AppendSegment(td);
                tp.AppendPath(gMatch.TargetPath);

                PathMatch match = new PathMatch(sp, tp);
                Mapping.Add(match);
            }
        }

        public void AddPathMatch(PathMatch match)
        {
            Debug.Assert(match != null && match.SourceSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(match != null && match.TargetSet == TargetSet, "Wrong use: target path must start from the target set.");

            // TODO: Check that the added paths do not cover existing paths and are not covered by existing paths

            Mapping.Add(match);
        }

        public DimTree GetSourceTree() // Convert all source paths into a dimension tree where the source set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(SourceSet);
            Mapping.ForEach(p => tree.AddPath(p.SourcePath));
            return tree;
        }

        public DimTree GetTargetTree() // Convert all target paths into a dimension tree where the target set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(TargetSet);
            Mapping.ForEach(p => tree.AddPath(p.TargetPath));
            return tree;
        }

        public Expression GetSourceExpression()
        {
            throw new NotImplementedException();
        }

        public Expression GetTargetExpression()
        {
            Expression expr = new Expression(null, Operation.TUPLE, TargetSet); // Create a root tuple expression object 
            foreach (PathMatch match in Mapping)
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

            Mapping = new List<PathMatch>();

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

        public int IsInSourcePath(Set set)
        {
            int rank = -1;
            return rank;
        }
        public int IsInTargetPath(Set set)
        {
            int rank = -1;
            return rank;
        }

        public bool AreMatched(Set sourceSet, Set targetSet)
        {
            return IsInSourcePath(sourceSet) >= 0 && IsInTargetPath(targetSet) >= 0;
        }

        public PathMatch(DimPath sourcePath, DimPath targetPath)
        {
            SourcePath = sourcePath;
            TargetPath = targetPath;
        }
    }


    //
    // ====================================================================================================
    // ======================================== OLD =======================================================
    // ====================================================================================================
    //
/*
    /// <summary>
    /// The class describes a mapping from one set to another set.
    /// </summary>
    public class SetMapping_OLD
    {
        public MatchTree SourceTree { get; private set; }

        public MatchTree TargetTree { get; private set; }

        public void AddSet(Set set, MappingDirection direction) // Add and expand set
        {
            MatchTree setTree = null;
            if (direction == MappingDirection.SOURCE)
            {
                setTree = new MatchTree(set);
                setTree.ExpandTree();
                SourceTree.AddChild(setTree);
            }
            else if(direction == MappingDirection.TARGET)
            {
                setTree = new MatchTree(set);
                setTree.ExpandTree();
                TargetTree.AddChild(setTree);
            }
        }

        public void AddSets(List<Set> sets, MappingDirection direction) // Add and expand all sets from the list
        {
        }

        public void AddAllSets(MappingDirection direction) // Add and expand all sets in the schema
        {
        }

        public void RecommendCreation(MappingDirection direction) // Create new sets in the target tree identical to all source sets in the source tree
        {
            if (direction == MappingDirection.SOURCE)
            {
            }
            else if (direction == MappingDirection.TARGET)
            {
            }
        }

        /// <summary>
        /// For each source set, create a new target set.
        /// </summary>
        public void ImportSourceTree()
        {
            Debug.Assert(SourceTree != null, "Wrong use: Source tree cannot be null.");
            Debug.Assert(TargetTree != null, "Wrong use: Target tree cannot be null.");

        }

        public void RecommendTargets(MatchTree source) {

            MatchTree target = source.DimMatches.SelectedObject;

            Debug.Assert(target != null && SourceTree != null, "Wrong use: the source node has to be matched.");

            foreach (MatchTree n in source.Children)
            {
                n.DimMatches.Alternatives.Clear();

                // Find target nodes with the best matching set
                var targetNodes = TargetTree.Flatten().Where(x => x.Set == ((MatchTree)n).SetMatches.SelectedObject).ToList();

                // Add all possible target nodes to the list of alternatives - 
                // !!! we cannot do it because nodes represent dimension matches rather than set matches, so we need to reconstruct matches between dimensions of similar sets

                // Select the best alternative
            }

        }

        public void AddToSchema(MappingDirection direction) // Really integrate new elements into the schema by adding new dimensions (including super) to the existing and new sets so that all new elements are reachable
        {
            if (direction == MappingDirection.SOURCE)
            {
            }
            else if (direction == MappingDirection.TARGET)
            {
                // Traverse through the children
                List<DimTree> allNodes = TargetTree.Flatten().ToList();

                // Check only selected elements (used in mapping). Skip non-selected nodes
                List<DimTree> selectedNodes = allNodes.Where(n => ((MatchTree)n).DimMatches.IsSelected).ToList();

                foreach(MatchTree n in selectedNodes)
                {
                    Dim dim = n.Dim; // Or SelectedObject ???? 

                    // Add hanging dimensions to the sets they reference
                    if (!dim.GreaterSet.LesserDims.Contains(dim))
                    {
                        dim.GreaterSet.LesserDims.Add(dim);
                    }
                    if (!dim.LesserSet.GreaterDims.Contains(dim))
                    {
                        dim.LesserSet.GreaterDims.Add(dim);
                    }

                    // Any set must have a super-set with super-dimension. By default (if not specified) a set is included directly in the root. 
                    if (dim.GreaterSet.SuperDim == null)
                    {
//                        TargetTop.AddSubset(dim.GreaterSet);
                    }
                    if (dim.LesserSet.SuperDim == null)
                    {
//                        TargetTop.AddSubset(dim.LesserSet);
                    }
                }

            }
        }

        public Expression GetExpression(MappingDirection direction) // Get expression according to the current selected mappings
        {
            Expression expr = null;
            if (direction == MappingDirection.SOURCE)
            {
                expr = SourceTree.GetExpression();
            }
            else if (direction == MappingDirection.TARGET)
            {
                expr = TargetTree.GetExpression();
            }
            return expr;
        }

        /// <summary>
        /// Import the specified set along with all its greater sets as a subset of the specified parent set (normally root). 
        /// It is a convenience method simplifying a typical operation. The set is not populated but is ready to be populated. 
        /// </summary>
        public static Set ImportSet(Set sourceSet, Set parentSet)
        {
            // Configure mapper
            var map = new SetMapping_OLD(sourceSet.Root, parentSet.Root);
            map.AddSet(sourceSet, MappingDirection.SOURCE);
            map.RecommendCreation(MappingDirection.TARGET);
            map.AddToSchema(MappingDirection.TARGET);

            // Parameterize created set
            Expression importExpr = map.GetExpression(MappingDirection.TARGET);
            Set targetSet = ((MatchTree)map.SourceTree.Children[0]).DimMatches.SelectedObject.Set;
            targetSet.ImportExpression = importExpr;

            string importDimName = sourceSet.Name; // The same as the source (imported) set name
            DimImport importDim = new DimImport(importDimName, targetSet, sourceSet);
            importDim.Add();

            return targetSet;
        }

        /// <summary>
        /// Return several possible mappings from the specified set to the target schema.  
        /// </summary>
        public static List<SetMapping_OLD> RecommendMappings(Set sourceSet, SetRoot targetSchema, double setCreationThreshold)
        {
            var mappings = new List<SetMapping_OLD>();

            // Content DELETED

            return mappings;
        }

        public SetMapping_OLD()
        {
            SourceTree = new MatchTree();
            TargetTree = new MatchTree();
        }

        public SetMapping_OLD(Set sourceSet, Set targetSet)
        {
            Debug.Assert(sourceSet != targetSet, "Wrong use: mapping to the same set is not permitted.");

            SourceTree = new MatchTree(sourceSet);
            TargetTree = new MatchTree(targetSet);
        }

        public SetMapping_OLD(Set sourceSet, SetRoot targetSchema)
        {
            SourceTree = new MatchTree(sourceSet);

            TargetTree = new MatchTree();
            foreach (Set s in targetSchema.GetLeastSubsets()) 
            {
                MatchTree n = new MatchTree(s, TargetTree);
                n.ExpandTree();
            }
        }
    }
*/

    /// <summary>
    /// It it represents one dimension in a dimension tree with associated alternative mappings. 
    /// </summary>
    public class MatchTree : DimTree
    {
        // TO_DELETE: not needed because we use direction in a mapping structure with two trees (which anyway is represented by field names source and target)
//        public MappingDirection Direction { get; set; }

        public Fragments<MatchTree> DimMatches { get; set; } // Which dimension are similar to this dimension

        public Fragments<Set> SetMatches { get; set; } // Which sets are similar to this set

        //
        // Tree methods
        //

        public MatchTree DimSelectedParent // First parent with selected dimension
        {
            get
            {
                if (IsRoot) return null;
                MatchTree node = (MatchTree)Parent;
                for (; !node.IsRoot && !node.DimMatches.IsSelected; node = (MatchTree)node.Parent) ;
                return node;
            }
        }

        public MatchTree SetSelectedParent // First parent with selected set
        {
            get
            {
                if (IsRoot) return null;
                MatchTree node = (MatchTree)Parent;
                for (; !node.IsRoot && !node.SetMatches.IsSelected; node = (MatchTree)node.Parent) ;
                return node;
            }
        }

/*
        public int ParentMatchRank // Number of segments from this node up to the next matched (non-skipped) parent. 0 for root and 1 or more for all other nodes.
        {
            get
            {
                if (IsRoot) return 0;
                int rank = 1;
                for (MatchTree node = (MatchTree)Parent; !node.IsRoot && !node.DimMatches.IsSelected; node = (MatchTree)node.Parent) { rank++; }
                return rank;
            }
        }
        public DimPath ParentMatchPath // Path from the first matching parent to this node
        {
            get
            {
                if (IsRoot) return null;
                DimPath path = new DimPath();
                path.AppendSegment(this.Dim);
                for (MatchTree node = (MatchTree)Parent; !node.IsRoot && !node.DimMatches.IsSelected; node = (MatchTree)node.Parent) path.AppendSegment(node.Dim);
                return path;
            }
        }
*/
        /// <summary>
        /// Check the validity of the formal structure of this and all child nodes. Used for testing. 
        /// </summary>
        /// <returns></returns>
        public string IsValid()
        {
            Debug.Assert(IsRoot || Dim != null, "Wrong structure: Only root has null dimension.");
            Debug.Assert(Root.Dim == null, "Wrong structure: Root must have null dimension (???).");

            // Check children
            // 1. Children must have non-null dimension and set (since they have a parent)

            // Check matching structure. 
            // 1. A matched node must reference this node (pairwise symmetric matching). 
            // 2. A matched node must be a child of the our parent matching node. 
            // 3. What about root? Do we require that the root is always matched and always another root? 
            return null;
        }

/*
        /// <summary>
        /// Find alternative similar (matching) sets for this set with their relevance. 
        /// A new set will be created (but not added to the schema) if existing sets have relevance lower than the specified threashold.  
        /// </summary>
        public void RecommendOrCreateSets(double setCreationThreshold = 0)
        {
            MatchTree targetNode = DimMatches.SelectedObject;
            Debug.Assert(targetNode != null, "To fit a tree into another tree (match), this tree (root) has to be matched.");

            MatchTree targetRoot = targetNode;
            Debug.Assert(targetRoot.IsRoot, "For recomending sets, target tree has to be represented by its root node (it will be used for inserting new nodes).");

            // We find matches (fit) for each rank starting from 0 (primitive) and ending with this set
            var rankedSets = GetRankedSets();
            var targetRankedSets = targetNode.GetRankedSets();

            var alternatives = new Dictionary<Set, List<RecommendedFragment<Set>>>();

            for (int sr = 0; sr < rankedSets.Count; sr++)
            {
                foreach(Set ss in rankedSets[sr]) 
                {
                    alternatives.Add(ss, new List<RecommendedFragment<Set>>());
                    for (int tr = sr; tr < targetRankedSets.Count; tr++) // We assume that sets with lower rank do not match
                    {
                        if (sr == 0 && tr > 0) break; // For primitive sets with rank 0 we consider only target sets with rank 0 (primitive)

                        double bestSetSimilarity = 0.0;

                        foreach (Set ts in targetRankedSets[tr])
                        {
                            double nameSimilarity = StringSimilarity.computeNGramSimilarity(ss.Name, ts.Name, 3);
                            double rankSimilarity = Math.Pow(0.5, tr - sr);
                            double similarity = nameSimilarity * rankSimilarity;
                            bestSetSimilarity = Math.Max(bestSetSimilarity, similarity);
                            alternatives[ss].Add(new RecommendedFragment<Set>(ts, similarity));
                        }

                        if (bestSetSimilarity < setCreationThreshold && targetRoot.Direction == MappingDirection.TARGET) // Modify the dimension tree by adding new nodes
                        {
                            //
                            // Add a new set
                            //
                            Set newSet = new Set(ss.Name);
                            MatchTree newSetNode = new MatchTree(newSet, targetRoot); // Add this set to the dimension tree (root) as a new node

                            //
                            // Add new dimensions
                            //
                            foreach (Dim d in ss.GreaterDims)
                            {
                                // Find the best target greater set
                                Set sourceGreaterSet = d.GreaterSet;
                                double bestGreaterRelevance = alternatives[sourceGreaterSet].Max(f => f.Relevance);
                                RecommendedFragment<Set> bestGreaterFragment = alternatives[sourceGreaterSet].First(f => f.Relevance == bestGreaterRelevance);
                                Set targetGreaterSet = bestGreaterFragment.Fragment;

                                // Create a cloned dimension
                                Dim newDim = targetGreaterSet.CreateDefaultLesserDimension(d.Name, newSet);
                                newSet.AddGreaterDim(newDim); // We add only to the lesser set (for the possibility to expand and build a tree) and not to the greater set (in order not to add to the schema)

                                // Find bottom (unused) or create a node for this target greater set
                                MatchTree targetGreaterNode = (MatchTree) targetRoot.Children.Find(c => c.Set == targetGreaterSet);
                                if (targetGreaterNode == null) // Create a new dimension tree for the (new usage of the) greater set
                                {
                                    targetGreaterNode = new MatchTree(newDim, targetRoot);
                                    targetGreaterNode.ExpandTree();
                                }
                                else // Reuse the existing (bottom) set with its dimension tree
                                {
                                    targetGreaterNode.Dim = newDim;
                                }
                                
                            }

                            // TODO: Add this new set to our analyzed list of sets (taking into account the rank - the same as the source set) in order to take it into account in the next similarity checks

                            alternatives[ss].Add(new RecommendedFragment<Set>(newSet, 1.0));
                        }
                    }
                }
            }

            // Now store these alternatives for all nodes
            foreach (MatchTree n in Flatten())
            {
                n.SetMatches.Alternatives.Clear();
                n.SetMatches.Alternatives.AddRange(alternatives[n.Set]);
                n.SetMatches.SelectBest();
            }
        }
*/

        /// <summary>
        /// Find alternative similar (matching) dimensions for this node which are compatible with the current set selections but independent of the dim selections. 
        /// </summary>
        public void RecommendDims(double dimCreationThreshold = 0)
        {
            var targetSet = SetMatches.SelectedObject; // Any alternative dimension must end in this selected set
            if (targetSet == null)
            {
                // If no selection then empty list of dimensions (skipped)
            }

            var parentTargetSetNode = SetSelectedParent;
            if (parentTargetSetNode == null)
            {
                // If no parent with selected set then empty list (or all sets)
            }
            var parentTargetSet = parentTargetSetNode.Set; // Any alternative dimension must start from first parent with selected set
            

        }

        /// <summary>
        /// Find all formally possible matching sets for this set and compute their relevance taking into account matches of other sets in this tree.
        /// </summary>
        public void Recommend(double setCreationThreshold = 0)
        {
            RecommendTargets(setCreationThreshold);
        }

        /// <summary>
        /// Find all formally possible matching alternative target sets for this source set. 
        /// Compute relevance for each alternative target taking into account matches of other sets in this tree.
        /// Add new target set if the existing alternatives have similarity lower than the specified threshold. Threshold 1.0 means always create new target sets independent of the existing alternatives. 
        /// </summary>
        public void RecommendTargets(double setCreationThreshold = 0)
        {
//            Debug.Assert(Direction == MappingDirection.SOURCE, "This method can be called only for SOURCE tree nodes.");

            MatchTree previousSelection = DimMatches.SelectedObject; // In order to restore the previous selection after new alternatives are generated at the end

            if (IsRoot) // We do not change the root because it is a fixed parameter set at construction time. It has to be mapped to the root of the target tree. 
            {
                foreach (DimTree c in Children) ((MatchTree)c).Recommend();
                return;
            }

            //
            //  Generate a list of all target sets that can be formally assigned to this set
            //
            var alternatives = new List<RecommendedFragment<MatchTree>>();
            double bestSetSimilarity = 0.0;
            MatchTree bestMatch = null;

            var parentMatch = DimSelectedParent.DimMatches.SelectedObject;
            Debug.Assert(parentMatch != null, "Some parent must have a selected match. At least the root must be always matched (to the root of the target tree).");

            // This set can be mapped to any nested child of our (first matched) parent target (excluding it).
            foreach (MatchTree match in parentMatch.Flatten())
            {
                if (match == parentMatch) continue; // This element is already a target of our parent and cannot be used

                if (Set.IsPrimitive) // This set is primitive. 
                {
                    // This set is primitive. Alternatives can include only primitive target sets (non-primitive targets excluded) that is leaves of the target primitive tree.
                    if (!match.Set.IsPrimitive) continue;
                }
                else // This set is primitive. 
                {
                    // Alternatives cannot include primitive target sets (only non-primitive).
                    if (match.Set.IsPrimitive) continue;
                }

                // Our longest child path must have place (be shorter than) somewhere among target child paths
                // TODO: In fact, it is not so if we can add additional dimensions and sets in the target on next steps
                // if (MaxLeafRank > match.MaxLeafRank) continue;

                double setSimilarity = StringSimilarity.computeNGramSimilarity(Set.Name, match.Set.Name, 3);
                if (setSimilarity > bestSetSimilarity)
                {
                    bestSetSimilarity = setSimilarity;
                    bestMatch = match;
                }

                alternatives.Add(new RecommendedFragment<MatchTree>(match, setSimilarity)); 
            }

            // If no good alternatives have been found then add them as new elements
            if (bestSetSimilarity <= setCreationThreshold || alternatives.Count == 0)
            {
                // Create a new node for new matching element with the same parameters as this element (clone)

                Set newSet = new Set(Set.Name); // Clone of this set (we cannot clone primitive sets - this possibility must be excluded)

                Dim newDim = newSet.CreateDefaultLesserDimension(Dim.Name, Set); // Clone this dimension (it is not included in its lesser/greater sets yet)

                // Create a new target node and add it to the parent node
                MatchTree newMatch = new MatchTree(newDim, parentMatch);

                // Add the new element to alternatives
                alternatives.Add(new RecommendedFragment<MatchTree>(newMatch, 1.0));
            }

            //
            // Convert alternatives into a list of fragments with preserving the current selection if possible
            //
            DimMatches.Alternatives.Clear();
            DimMatches.Alternatives.AddRange(alternatives);
            DimMatches.SelectedObject = previousSelection;

            //
            // Recommend alternatives for the children recursively
            //
            foreach (DimTree c in Children) ((MatchTree)c).Recommend();
        }

        /// <summary>
        /// Assign best matchs from the available alternatives. 
        /// The parameters specify thresholds for preferring new elements instead of using existing alternatives (with low relevance). 
        /// 0 means no creation of new elements and 1 means always create new elements (do not use any alternatives). 
        /// </summary>
        public void SelectBestMatches(double dimCreation = 0, double setCreation = 0)
        {
            // Choose the best match for this element. Theoretically, we need to consider and choose from all combinations of matches at least among siblings.
            double maxRelevance = DimMatches.Alternatives.Max(f => f.Relevance);
            RecommendedFragment<MatchTree> maxFragment = DimMatches.Alternatives.First(f => f.Relevance == maxRelevance);
            if (maxRelevance < dimCreation) // Or no alternative exists at all
            {
                // Create new dimension instead of reusing the proposed match
            }
            if (maxRelevance < setCreation) // Or no alternative exists at all
            {
                // Create new set instead of reusing the proposed match
            }

            // After each selection we need to update recommended alternatives (for children).
            DimMatches.SelectedFragment = maxFragment;
            UpdateSelection();

            // Choose the best match for child elements
            foreach (MatchTree c in Children) c.SelectBestMatches(dimCreation, setCreation);
        }

        /// <summary>
        /// This node has changed its selection. Propagate this selection to other nodes by either generating new alternative matches or by adjusting relevance factors for thier existing alternatives. 
        /// </summary>
        public void UpdateSelection()
        {
            // Generate new alternatives for all our children
            foreach (DimTree c in Children) ((MatchTree)c).Recommend();

            // TODO: theoretically, we should also adjust relevance of alternatives for our siblings
        }

        // Compute similarity by assuming that this node has the specified match
        private double ComputeRelevance(DimTree target)
        {
            DimTree tree = new DimTree();

            // Compute own similarity of two pairs <dim, gSet> or, alternatively, two matching dims
            double own = 1.0;
            // tree.Dim vs tree.MatchingNode.Dim
            // tree.Set vs tree.MatchingNode.Set - can be primitive
            // A new (non-existing) dimension or set should be qualified as a perfect match because it is supposed to be created by the user or otherwise for this concrete purpose (independent of its name)

            // Aggregate similarities between children. If primitive then a predefined similarity
            double relevance = 0.0;
            foreach (DimTree child in tree.Children) // Aggregate all child matches
            {
                //relevance += child.ComputeRelevance(); // Recursion
            }

            // relevance /= tree.Children.Count(); // Average
            relevance = (relevance + own) / 2;

            return 1.0;
        }

        /// <summary>
        /// Create an equivalent expression using matching from this tree to the referenced dimensions tree. 
        /// The expression will have the structure of this match tree (not the referenced dimension tree).
        /// </summary>
        /// <returns></returns>
        public Expression GetExpression()
        {
            //
            // Create a tuple expression object for this node of the tree only
            //
            Expression expr = new Expression(Dim != null ? Dim.Name : null, Operation.TUPLE, Set);
            expr.Dimension = Dim;

            DimTree target = DimMatches.SelectedObject;

            // For a leaf, define the primitive value in terms of the matching tree (it is a DOT expression representing a primitive path in the target tree)
            if (IsLeaf && target != null)
            {
                Debug.Assert(Set.IsPrimitive, "Wrong structure: Leaves of the match tree have to be primitive sets.");

                // Build function expression for computing a primitive value from the matched target tree
                DimPath targetPath = target.DimPath;
                Expression funcExpr = Expression.CreateProjectExpression(targetPath.Path, Operation.DOT);
                funcExpr.Input = new Expression("source", Operation.VARIABLE, targetPath.LesserSet); // Add Input of function as a variable the values of which (output) can be assigned during export

                expr.Input = funcExpr; // Add function to the leaf expression
            }
            else // Recursion: create a tuple expression for each child and add them to the parent tuple
            {
                foreach (MatchTree c in Children)
                {
                    if (c.Dim != null && c.Dim is DimSuper)
                        expr.Input = c.GetExpression();
                    else
                        expr.AddOperand(c.GetExpression());
                }
            }

            return expr;
        }

        public MatchTree CreateEmptyTree(SetRoot topSet)
        {
            MatchTree bottomSet = new MatchTree(null);

            return bottomSet;
        }

        public MatchTree CreatePrimitiveTree(SetRoot topSet)
        {
            MatchTree bottomSet = new MatchTree(null);

            return bottomSet;
        }

        public MatchTree(Dim dim, DimTree parent = null) 
            : base(dim, parent)
        {
            DimMatches = new Fragments<MatchTree>();
        }

        public MatchTree(Set set, DimTree parent = null)
            : base(set, parent)
        {
            DimMatches = new Fragments<MatchTree>();
        }

        public MatchTree(MatchTree target)
            : base()
        {
            // We assume that it is a root node. Some target must be provided
            DimMatches = new Fragments<MatchTree>();
            DimMatches.Name = "Matches";
            RecommendedFragment<MatchTree> targetFragment = new RecommendedFragment<MatchTree>(target, 1.0);
            DimMatches.Alternatives.Add(targetFragment);
            DimMatches.SelectedFragment = targetFragment;
            DimMatches.Readonly = true; // The root node should never be shown or edited in any case

            // TODO: Initialize SetMatches
        
        }

        public MatchTree()
            : base()
        {
            // We assume that it is a root node. Some target must be provided
            DimMatches = new Fragments<MatchTree>();
            DimMatches.Name = "Matches";
            DimMatches.Readonly = true; // The root node should never be shown or edited in any case

            // TODO: Initialize SetMatches

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
