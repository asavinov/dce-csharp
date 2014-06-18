﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Com.Model
{
    /// <summary>
    /// The class describes one mapping between two concrete sets as a collection of path pairs. 
    /// </summary>
    public class Mapping
    {
        public List<PathMatch> Matches { get; private set; }

        public double Similarity { get; set; }

        private CsTable _sourceSet;
        public CsTable SourceSet
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
                        m.SourcePath.RemoveFirst(greaterSegs[0].GreaterSet);
                    }
                }
                else // Otherwise
                {
                    Matches.Clear();
                }

                _sourceSet = value;
            }
        }

        private CsTable _targetSet;
        public CsTable TargetSet
        {
            get { return _targetSet; }
            set
            {
                if (_targetSet == null) { _targetSet = value; return; }
                if (_targetSet == value) return;

                List<DimPath> lesserSegs = value.GetGreaterPaths(_targetSet);
                List<DimPath> greaterSegs = _targetSet.GetGreaterPaths(value);

                if (lesserSegs != null && lesserSegs.Count > 0) // New set is a lesser set for the current
                {
                    foreach (PathMatch m in Matches) // Insert new segments
                    {
                        m.TargetPath.InsertFirst(lesserSegs[0]);
                    }
                }
                else if (greaterSegs != null && greaterSegs.Count > 0) // New set is a greater set for the current
                {
                    foreach (PathMatch m in Matches) // Remove segments
                    {
                        m.TargetPath.RemoveFirst(greaterSegs[0].GreaterSet);
                    }
                }
                else // Otherwise
                {
                    Matches.Clear();
                }

                _targetSet = value;
            }
        }

        public bool IsSourcePathValid(DimPath path)
        {
            if (path.LesserSet != SourceSet && !SourceSet.IsLesser(path.LesserSet)) return false;
            return true;
        }
        public bool IsTargetPathValid(DimPath path)
        {
            if (path.LesserSet != TargetSet && !TargetSet.IsLesser(path.LesserSet)) return false;
            return true;
        }

        public bool Compatible(PathMatch match) // Determine if the specified match does not contradict to this mapping and can be added to it without removing existing matches
        {
            foreach (PathMatch m in Matches)
            {
                if (!m.Compatible(match)) return false;
            }
            return true; // Compatible and can be added
        }

        public double ComputeSimilarity()
        {
            double sum = 0.0;
            foreach (PathMatch m in Matches)
            {
                m.Similarity = StringSimilarity.ComputePathSimilarity(m.SourcePath, m.TargetPath);
                sum += m.Similarity;
            }

            if (Matches.Count > 0) Similarity = sum / Matches.Count;
            else Similarity = 0.0;

            return Similarity;
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
            foreach (PathMatch m in Matches)
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

        public void AddPaths(CsColumn sd, CsColumn td, Mapping gMapping) // Add this pair by expanding it using the mapping
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

        public void InsertFirst(DimPath sourcePath, DimPath targetPath)
        {
            if (sourcePath != null)
            {
                _sourceSet = sourcePath.LesserSet;
                Matches.ForEach(m => m.SourcePath.InsertFirst(sourcePath));
            }
            if (targetPath != null)
            {
                _targetSet = targetPath.LesserSet;
                Matches.ForEach(m => m.TargetPath.InsertFirst(targetPath));
            }
        }
        public void RemoveFirst(DimPath sourcePath, DimPath targetPath)
        {
            if (sourcePath != null)
            {
                _sourceSet = sourcePath.GreaterSet;
                Matches.ForEach(m => m.SourcePath.RemoveFirst(sourcePath));
            }
            if (targetPath != null)
            {
                _targetSet = targetPath.GreaterSet;
                Matches.ForEach(m => m.TargetPath.RemoveFirst(targetPath));
            }
        }

        public void Invert() // Invert source and target
        {
            var tempSet = _sourceSet;
            _sourceSet = _targetSet;
            _targetSet = tempSet;

            Matches.ForEach(m => m.Invert());
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
                int index = m.SourcePath.IndexOfLesser(set);
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
                int index = m.TargetPath.IndexOfLesser(set);
                if (index < 0) continue; // The path does not cross this set

                tree.AddPath(m.TargetPath.SubPath(index));
            }

            return tree;
        }

        public Expression GetSourceExpression()
        {
            throw new NotImplementedException();
        }

        public Expression GetTargetExpression() // Build tuple expression where target paths define a tuple and source paths are used leaf expressions in this tuple applied to the source set
        {
            return GetTargetExpression(null);
        }
        public Expression GetTargetExpression(CsColumn dim) // Build tuple expression for the specified mapped dimension
        {
            // It is mapping from LesserSet to GreaterSet of the dimension
            Debug.Assert(dim == null || dim.LesserSet == SourceSet, "Wrong use: lesser set of the mapped dimension corresponds to the source set of the mapping.");
            Debug.Assert(dim == null || dim.GreaterSet == TargetSet, "Wrong use: greater set of the mapped dimension corresponds to the target set of the mapping.");

            Expression tupleExpr = new Expression(null, Operation.TUPLE, TargetSet);
            if (dim != null)
            {
                tupleExpr.Name = dim.Name;
                tupleExpr.Dimension = dim;
            }

            foreach (PathMatch match in Matches)
            {
                // Leaf expression for accessing a variable (parameter of the function)
                Expression thisExpr = new Expression("this", Operation.DOT, match.SourceSet);

                Expression srcExpr = null;
                DimPath srcPath = match.SourceSet.GetGreaterPath(match.SourcePath.Path); // First, we try to find a direct path/function 
                if (srcPath == null) // No direct path. Use a sequence of simple dimensions
                {
                    srcExpr = Expression.CreateProjectExpression(match.SourcePath.Path, Operation.DOT);
                }
                else // There is a direct path (relation attribute in a relational data source). Use attribute name as function name
                {
                    srcExpr = new Expression(srcPath.Name, Operation.DOT, match.TargetPath.GreaterSet);
                }
                srcExpr.GetInputLeaf().Input = thisExpr;

                Expression leafTuple = tupleExpr.AddPath(match.TargetPath);
                leafTuple.Input = srcExpr;
            }

            return tupleExpr;
        }

        public void AddSourceToSchema(CsSchema schema = null)
        {
            throw new NotImplementedException();
        }
        public void AddTargetToSchema(CsSchema schema = null) // Ensure that all target elements exist in the specified schema
        {
            // The mapping can reference new elements which are not in the schema yet so we try to find them and add if necessary

            if (schema == null) // Find the schema from the mapping elements
            {
                PathMatch match = Matches.FirstOrDefault(m => m.TargetPath.GreaterSet.IsPrimitive);
                schema = match != null ? match.TargetPath.GreaterSet.Top : null; // We assume that primitive sets always have root defined (other sets might not have been added yet).
            }

            DimTree tree = GetTargetTree();
            tree.AddToSchema(schema);
        }

        public override string ToString()
        {
            return String.Format("{0} -> {1}. Similarity={2}. Matches={3}", SourceSet.Name, TargetSet.Name, Similarity, Matches.Count);
        }

        public Mapping(CsTable sourceSet, CsTable targetSet)
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

        public CsTable SourceSet { get { return SourcePath == null ? null : SourcePath.LesserSet; } }
        public CsTable TargetSet { get { return TargetPath == null ? null : TargetPath.LesserSet; } }

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

        public void Invert() // Invert source and target
        {
            var tempPath = SourcePath;
            SourcePath = TargetPath;
            TargetPath = tempPath;
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

}