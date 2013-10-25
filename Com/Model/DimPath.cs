using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// A sequence of simple dimensions (segments). 
    /// </summary>
    public class DimPath : Dim
    {
        public string ComplexName
        {
            get
            {
                if (Length == 0) return "";
                string complexName = "";
                foreach (Dim seg in Path) complexName += "_" + seg.Name;
                return complexName;
            }
        }
        public string HashName
        {
            get
            {
                if (Length == 0) return "0";
                int hash = 0;
                foreach (Dim seg in Path) hash += seg.Id.GetHashCode();

                hash = Math.Abs(hash);
                string hashName = hash.ToString("X"); // unique hash representing this path
                return hashName.Length > 6 ? hashName.Substring(0, 6) : hashName;
            }
        }

        /// <summary>
        /// A dimension can be defined as a sequence of other dimensions. For simple dimensions the path is empty.
        /// </summary>
        public List<Dim> Path { get; set; }

        public int Length
        {
            get
            {
                return Path.Count;
            }
        }

        public Dim FirstSegment
        {
            get
            {
                return Length == 0 ? null : Path[0];
            }
        }

        public Dim LastSegment
        {
            get
            {
                return Length == 0 ? null : Path[Path.Count - 1];
            }
        }

        public int Rank
        {
            get
            {
                if (Length == 0) return 1; // Simple dimension
                int r = 0;
                foreach (Dim dim in Path)
                {
                    r += 1; // dim.Rank;
                }
                return r;
            }
        }

        public bool IsComplex
        {
            get
            {
                return Path != null && Path.Count > 0;
            }
        }

        public int IndexOf(Set set) // Return index of the set in the path
        {
            for (int i = 0; i < Path.Count; i++)
            {
                if (Path[i].LesserSet == set) return i;
            }
            return -1;
        }
        public int IndexOf(Dim dim) // Return index of the specified dimension in this path
        {
            return Path.IndexOf(dim);
        }
        public int IndexOf(DimPath path) // Return index of the beginning of the specified path in this path
        {
            throw new NotImplementedException();
        }

        public bool StartsWith(Dim dim)
        {
            if(Length == 0) return false;
            return Path[0] == dim;
        }
        public bool StartsWith(DimPath path)
        {
            return StartsWith(path.Path);
        }
        public bool StartsWith(List<Dim> path)
        {
            if (Path.Count < path.Count) return false;
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] != Path[i]) return false;
            }
            return true;
        }

        public bool SamePath(DimPath path) // Equals (the same segments)
        {
            return SamePath(path.Path);
        }
        public bool SamePath(List<Dim> path) // Equals (the same segments)
        {
            if (path == null) return false;

            if (Path.Count != path.Count) return false;

            for (int i = 0; i < path.Count; i++)
            {
                if (!path[i].Equals(Path[i])) return false;
            }
            return true;
        }

        public DimPath SubPath(int index, int count = 0) // Return a new path consisting of the specified segments
        {
            DimPath ret = new DimPath();

            if (count == 0) count = Path.Count - index;

            for (int i = 0; i < count; i++)
            {
                ret.Path.Add(Path[index + i]);
            }

            ret.GreaterSet = ret.Path[0].LesserSet;
            ret.LesserSet = ret.Path[ret.Path.Count - 1].GreaterSet;

            return ret;
        }

        #region Add segments

        public void InsertAt(Dim dim) // Insert a new segment at the specified position
        {
            throw new NotImplementedException();
        }
        public void InsertAt(DimPath path) // Insert a new segment at the specified position
        {
            throw new NotImplementedException();
        }

        public void InsertFirst(Dim dim) // Insert a new segment at the beginning of the path
        {
            Debug.Assert(Length == 0 || dim.GreaterSet == LesserSet, "A path must continue the first segment inserted in the beginning.");

            Path.Insert(0, dim);
            LesserSet = dim.LesserSet;
            if (GreaterSet == null) GreaterSet = dim.GreaterSet;
        }
        public void InsertFirst(DimPath path) // Insert new segments from the specified path at the beginning of the path
        {
            Debug.Assert(Length == 0 || path.GreaterSet == LesserSet, "A path must continue the first segment inserted in the beginning.");

            Path.InsertRange(0, path.Path);
            LesserSet = path.LesserSet;
            if (GreaterSet == null) GreaterSet = path.GreaterSet;
        }

        public void InsertLast(Dim dim) // Append a new segment to the end of the path
        {
            Debug.Assert(Length == 0 || dim.LesserSet == GreaterSet, "A new segment appended to a path must continue the previous segments");

            Path.Add(dim);
            GreaterSet = dim.GreaterSet;
            if (LesserSet == null) LesserSet = dim.LesserSet;
        }
        public void InsertLast(DimPath path) // Append all segments of the specified path to the end of this path
        {
            Debug.Assert(Length == 0 || path.LesserSet == GreaterSet, "A an appended path must continue this path.");

            if (path == null || path.Length == 0) return;

            for (int i = 0; i < path.Path.Count; i++)
            {
                Path.Add(path.Path[i]);
            }

            GreaterSet = path.GreaterSet;
            if (LesserSet == null) LesserSet = path.LesserSet;
        }

        #endregion // Add segments

        #region Remove segments

        private Dim RemoveAt(int index)
        {
            if (Length == 0) return null; // Nothing to remove
            if (index < 0 || index >= Path.Count) return null; // Bad index

            Dim result = Path[index];
            Path.RemoveAt(index);

            if (Path.Count != 0)
            {
                LesserSet = Path[0].LesserSet;
                GreaterSet = Path[Path.Count - 1].GreaterSet;
            }
            else
            {
                // Note: LesserSet and GreaterSets are not set - this must be done in public methods and depends on whether it is removed as first or last segment (it is important for some algorithms)
            }

            return result;
        }

        public Dim RemoveFirst()
        {
            return RemoveFirstAt(0);
        }
        public Dim RemoveFirstAt(int index) // TODO: Implement an additional argument with the number of segments to remove
        {
            Dim result = RemoveAt(index);
            if (result == null) return result;

            if (Path.Count == 0) // This where removal of the first and the last segments is different
            {
                LesserSet = result.GreaterSet;
                GreaterSet = result.GreaterSet;
            }

            return result;
        }
        public void RemoveFirst(DimPath path) // Remove first segments
        {
            if (Path.Count < path.Path.Count) return; // Nothing to remove
            if (!this.StartsWith(path)) return;

            Path.RemoveRange(0, path.Path.Count);

            if (Path.Count > 0) LesserSet = Path[0].LesserSet;
            else LesserSet = GreaterSet;
        }
        public void RemoveFirst(Set set) // Remove first segments till this set (the new path will start from the specified set if trimmed)
        {
            // Find a path to the specified set
            int index = IndexOf(set);
            if (index < 0) return;

            Path.RemoveRange(0, index - 1);

            if (Path.Count > 0) LesserSet = Path[0].LesserSet;
            else LesserSet = GreaterSet;
        }

        public Dim RemoveLast() // Remove last segment
        {
            return RemoveLastAt(Path.Count - 1);
        }
        public Dim RemoveLastAt(int index) // TODO: Implement an additional argument with the number of segments to remove
        {
            Dim result = RemoveAt(index);
            if (result == null) return result;

            if (Path.Count == 0) // This where removal of the first and the last segments is different
            {
                LesserSet = result.LesserSet;
                GreaterSet = result.LesserSet;
            }

            return result;
        }
        public void RemoveLast(DimPath path) // Remove last segments (suffix)
        {
            throw new NotImplementedException();
        }
        public void RemoveLast(Set set) // Remove last segments starting from this set (the new path will end with the specified set if trimmed)
        {
            throw new NotImplementedException();
        }

        #endregion // Remove segments

        /// <summary>
        /// Convert nested path to a flat canonical representation as a sequence of simple dimensions which do not contain other dimensions.
        /// Initially, paths are pairs <this set dim, greater set path>. We recursively replace all nested paths by dimensions.
        /// Also, adjust path names in special cases like empty name or simple structure. 
        /// </summary>
        public void ExpandPath()
        {
            //
            // Flatten all paths by converting <dim, greater-path> pairs by sequences of dimensions <dim, dim2, dim3,...>
            //
            List<Dim> allSegments = GetAllSegments();
            Path.Clear();
            if (allSegments != null && allSegments.Count != 0)
            {
                Path.AddRange(allSegments);
            }
            else
            {
                // ERROR: Wrong use: The path does not have the corresponding dimension
            }

            //
            // Adding missing paths. Particularly, non-stored paths (paths returning values which are stored only in the greater sets but not in this set).
            //
            if (!String.IsNullOrEmpty(RelationalFkName) && GreaterSet.IdentityPrimitiveArity == 1)
            {
                Path[0].Name = Name; // FK-name is overwritten and lost - attribute name is used instead
            }

            //
            // Dim name adjustment: for 1-column FK dimensions, we prefer to use its only column name instead of the FK-name (fkName is not used)
            //
            if (!String.IsNullOrEmpty(RelationalFkName) && GreaterSet.IdentityPrimitiveArity == 1)
            {
                Path[0].Name = Name; // FK-name is overwritten and lost - attribute name is used instead
            }
        }

        private List<Dim> GetAllSegments()
        {
            List<Dim> result = new List<Dim>();
            for (int i = 0; i < Path.Count; i++)
            {
                if (Path[i] is DimPath && ((DimPath)Path[i]).IsComplex)
                {
                    result.AddRange(((DimPath)Path[i]).GetAllSegments());
                }
                else // Simple segment - nothing to expand
                {
                    result.Add(Path[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Check if the path is correct and all its segments are consequitive.
        /// Returns the segment number where the sequence breaks. If the path is correct then it returns the last segment number (rank).
        /// </summary>
        private int ValidateSegmentSequence()
        {
            return Rank; // TODO
        }

        public Dim GetSegment(int rank)
        {
            Debug.Assert(rank >= 0, "Wrong use of method parameter. Rank cannot be negative.");
            return rank < Path.Count ? Path[rank] : null; // TODO: take into account the nested structure of complex dimensions
        }

        /// <summary>
        /// Check the validity of this formal structure. Used for testing. 
        /// </summary>
        /// <returns></returns>
        public string IsValid()
        {
            if (Length == 0) return null;
            return null;
        }

        public DimPath()
        {
            Path = new List<Dim>();
        }

        public DimPath(Set set)
            : this()
        {
            LesserSet = set;
            GreaterSet = set;
        }

        public DimPath(string name)
            : base(name)
        {
            Path = new List<Dim>();
        }

        public DimPath(Dim segment)
            : this()
        {
            if (segment == null) return;

            Path.Add(segment);
            LesserSet = Path[0].LesserSet;
            GreaterSet = Path[Path.Count - 1].GreaterSet;
        }

        public DimPath(List<Dim> segments)
            : this()
        {
            if(segments == null && segments.Count == 0) return;

            Path.AddRange(segments);
            LesserSet = Path[0].LesserSet;
            GreaterSet = Path[Path.Count - 1].GreaterSet;
        }

        public DimPath(DimPath path)
            : base(path)
        {
            Path = new List<Dim>();
            Path.AddRange(path.Path);
        }
    }

    /// <summary>
    /// Abstract base class for all kinds of path enumerators without complex constraints.
    /// 
    /// Implementing iterators: 
    /// http://msdn.microsoft.com/en-us/magazine/cc163682.aspx
    /// http://www.codeproject.com/Articles/34352/Tree-Iterators
    /// TODO:
    /// - study how to use yield in iterators
    /// - study how to use nested classes for iterators 
    /// - implement many different kinds of iterators: depth-first, bredth-first, leafs-only etc.
    /// </summary>
    public abstract class DimEnumerator : DimPath, IEnumerator<DimPath>, IEnumerable<DimPath>
    {

        public DimEnumerator(Set set)
            : base(set)
        {
            Path = new List<Dim>();
        }

        // Get the explicit current node.
        public DimPath Current { get { return new DimPath(Path); } }

        // Get the implicit current node.
        object System.Collections.IEnumerator.Current
        {
            get { return Current; }
        }

        // Increment the iterator and moves the current node to the next one
        public abstract bool MoveNext();

        // Dispose the object.
        public void Dispose()
        {
            LesserSet = null;
            GreaterSet = null;

            Path.Clear();
            Path = null;
        }

        // Reset the iterator.
        public void Reset()
        {
            if (Path != null) Path.Clear();
        }

        // Get the underlying enumerator.
        public virtual IEnumerator<DimPath> GetEnumerator()
        {
            return (IEnumerator<DimPath>)this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }

    /// <summary>
    /// Abstract base class for path enumerators with complex constraints.
    /// </summary>
    public abstract class DimComplexEnumerator : DimEnumerator
    {
        protected List<Set> lesserSets;
        protected List<Set> greaterSets;
        protected bool isInverse;
        protected DimensionType dimType; // The path will be composed of only these types of segments
        protected bool allowIntermediateSets = false; // Not implemented. lesser and greater sets only as source and destination - not in the middle of the path (default). Otherwise, they can appear in the middle of the path (say, one greater set and then the final greater set).

        public DimComplexEnumerator(List<Set> _lesserSets, List<Set> _greaterSets, bool _isInverse, DimensionType _dimType)
            : base(null)
        {
            lesserSets = _lesserSets;
            greaterSets = _greaterSets;

            isInverse = _isInverse;
            dimType = _dimType;

            if (!isInverse)
            {
                LesserSet = lesserSets[0];
                GreaterSet = lesserSets[0];
            }
            else
            {
                LesserSet = greaterSets[0];
                GreaterSet = greaterSets[0];
            }
        }

        public DimComplexEnumerator(Set set)
            : base(set)
        {
            lesserSets = new List<Set>(new Set[] { set }); // One source set
            greaterSets = new List<Set>(new Set[] { set.Root }); // All destination sets from this schema

            isInverse = false;
        }
    }

    /// <summary>
    /// Enumerate all different paths between specified sets. 
    /// </summary>
    public class PathEnumerator : DimComplexEnumerator
    {
        public PathEnumerator(Set set, DimensionType _dimType) // All primitive paths
            : this(new List<Set>(new Set[] { set }), null, false, _dimType)
        {
        }

        public PathEnumerator(Set lesserSet, Set greaterSet, DimensionType _dimType) // Between two sets
            : this(new List<Set>(new Set[] { lesserSet }), new List<Set>(new Set[] { greaterSet }), false, _dimType)
        {
        }

        public PathEnumerator(List<Set> _lesserSets, List<Set> _greaterSets, bool _isInverse, DimensionType _dimType)
            : base(_lesserSets, _greaterSets, _isInverse, _dimType)
        {
        }

        public override bool MoveNext()
        {
            // TODO: We need also loop over all source sets

            if (!AtDestination()) // Try to move forward by attaching a new segment
            {
                bool destinationFound = MoveForward(); // Move several steps forward until next destination is found
                if (destinationFound) return true; // A destination was really found
                // else // No valid destination was found by moving forward. We have to move backward
            }

            bool continuationFound = MoveBackward(); // Try to move backward by removing segments until a previous set with an unvisited path forward is found
            if (!continuationFound) return false;

            if (AtDestination()) return true;

            return MoveNext(); // Recursive
        }
        private bool MoveForward() // return true - valid destination set found, false - no valid destination found (and cannot move forward anymore)
        {
            while (!AtDestination()) // Not a destination - move one more step forward
            {
                List<Dim> dims = GetContinuations();

                if (dims.Count != 0) // Continue depth-first by adding the very first dimension
                {
                    AddLastSegment(dims[0]);
                }
                else
                {
                    return false; // Not a destination but no possibility to move forward
                }
            }
            return true;
        }
        private bool MoveBackward() // return true - found a set with a possibility to continued (with unvisited continuation), false - not found a set with possibility to continue(end, go to next source set)
        {
            Dim segment = null;
            do // A loop for removing last segment and moving backward
            {
                if (Length == 0) // Nothing to remove. End.
                {
                    return false;
                }

                segment = RemoveLastSegment(); // Remove last segment

                List<Dim> nextSegs = GetContinuations();

                int segIndex = nextSegs.IndexOf(segment);
                if (segIndex + 1 < nextSegs.Count) // Continuation found. Use it
                {
                    segment = nextSegs[segIndex + 1];
                    AddLastSegment(segment); // Add next last segment
                    return true;
                }
                else // Continuation not found. Continue removing.
                {
                    segment = null;
                }
            } while (segment == null);

            return false; // All segments removed but no continuation found in any of the previous sets including the source one
        }

        private List<Dim> GetContinuations()
        {
            if (!isInverse) // Move up from lesser to greater
            {
                switch (dimType)
                {
                    case DimensionType.IDENTITY: return GreaterSet.GetIdentityDims();
                    case DimensionType.ENTITY: return GreaterSet.GetEntityDims();
                    case DimensionType.IDENTITY_ENTITY: return GreaterSet.GreaterDims;
                }
            }
            else
            {
                switch (dimType)
                {
                    case DimensionType.IDENTITY: return LesserSet.LesserDims.Where(x => x.IsIdentity).ToList();
                    case DimensionType.ENTITY: return LesserSet.LesserDims.Where(x => !x.IsIdentity).ToList();
                    case DimensionType.IDENTITY_ENTITY: return LesserSet.LesserDims;
                }
            }

            return null;
        }
        private Dim RemoveLastSegment()
        {
            if (Length == 0) return null; // Nothing to remove

            Dim segment = null;
            if (!isInverse)
            {
                segment = RemoveLast();
            }
            else
            {
                segment = RemoveFirst();
            }
            return segment;
        }
        private void AddLastSegment(Dim segment)
        {
            if (!isInverse)
            {
                InsertLast(segment);
            }
            else
            {
                InsertFirst(segment);
            }
        }
        private bool AtDestination()
        {
            List<Set> destinations = !isInverse ? greaterSets : lesserSets;
            Set dest = !isInverse ? GreaterSet : LesserSet;

            if (destinations == null) // 
            {
                // Destinations are primitive sets
                if (!isInverse) return GreaterSet.IsPrimitive;
                else return LesserSet.IsLeast; // Just least set because primitive sets do not exist for least sets
            }
            else if (destinations.Count == 0)
            {
                // Destinations are terminal sets (greatest or least). Check possibility to continue.
                if (!isInverse) return GreaterSet.IsGreatest;
                else return LesserSet.IsLeast;
            }
            else // Concrete destinations are specified
            {
                foreach (Set set in destinations) if (dest.IsIn(set)) return true;
                return false;
            }
        }
    }

    /*
        public class DepthDimEnumerator : DimEnumerator
        {
            private DimensionType dimType;

            public DepthDimEnumerator(Set set, DimensionType _dimType)
                : base(set) 
            {
                dimType = _dimType;
            }

            public override bool MoveNext()
            {
                if (!GreaterSet.IsPrimitive)
                {
                    bool primitiveFound = MoveForward();
                    if (primitiveFound) return true;
                    // Else move down back
                }

                // Go down (return back) until we find the next (unvisited) child
                bool childFound = MoveBackward();
                if (!childFound) return false;

                return MoveForward();
            }
            private bool MoveForward() // true - found primitive, false - found non-primitive leaf
            {
                while (!GreaterSet.IsPrimitive) // Not a leaf - go up deeper and search for the first primitive set
                {
                    List<Dim> dims = GetContinuations();

                    if (dims.Count != 0)
                    {
                        Path.Add(dims[0]); // Continue depth-first by adding the very first dimension
                        GreaterSet = LastSegment.GreaterSet;
                    }
                    else
                    {
                        return false; // Non-primitive set but no possibility to continue
                    }
                }
                return true;
            }
            private bool MoveBackward() // true - found next child that can be continued up, false - not found (end)
            {
                Dim child = null;
                do // It is only down loop (removing last segment)
                {
                    if (Path.Count == 0) // Nothing to remove. End.
                    {
                        return false;
                    }

                    child = LastSegment;
                    Path.RemoveAt(Path.Count - 1);
                    GreaterSet = Path.Count == 0 ? LesserSet : LastSegment.GreaterSet;

                    List<Dim> children = GetContinuations();

                    int childIndex = children.IndexOf(child);
                    if (childIndex + 1 < children.Count) // Good child. Use it
                    {
                        child = children[childIndex + 1];
                        Path.Add(child);
                        GreaterSet = LastSegment.GreaterSet;
                        return true;
                    }
                    else // Bad child. Continue removing.
                    {
                        child = null;
                    }
                } while (child == null);

                return false; // Unreachable
            }

            private List<Dim> GetContinuations()
            {
                switch (dimType)
                {
                    case DimensionType.IDENTITY: return GreaterSet.GetIdentityDims();
                    case DimensionType.ENTITY: return GreaterSet.GetEntityDims();
                    case DimensionType.IDENTITY_ENTITY: return GreaterSet.GreaterDims;
                }
                return null;
            }
        }
    */

}
