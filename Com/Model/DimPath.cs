using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Offset = System.Int32;

using Newtonsoft.Json.Linq;

namespace Com.Model
{
    /// <summary>
    /// A sequence of simple dimensions (segments). 
    /// </summary>
    public class DimPath : Dim
    {

        public string ColumnNamePath // A list of segment column names (dot separated)
        {
            get
            {
                if (Size == 0) return "";
                string complexName = "";
                foreach (ComColumn seg in Path) complexName += "[" + seg.Name + "].";
                complexName = complexName.Substring(0, complexName.Length-1); // Remove last dot
                return complexName;
            }
        }
        public string HashName
        {
            get
            {
                if (Size == 0) return "0";
                int hash = 0;
                foreach (ComColumn seg in Path) hash += ((Dim)seg).Id.GetHashCode();

                hash = Math.Abs(hash);
                string hashName = hash.ToString("X"); // unique hash representing this path
                return hashName.Length > 6 ? hashName.Substring(0, 6) : hashName;
            }
        }

        /// <summary>
        /// A dimension can be defined as a sequence of other dimensions. For simple dimensions the path is empty.
        /// </summary>
        public List<ComColumn> Path { get; set; }

        public int Size
        {
            get
            {
                return Path.Count;
            }
        }

        public ComColumn FirstSegment
        {
            get
            {
                return Size == 0 ? null : Path[0];
            }
        }

        public ComColumn LastSegment
        {
            get
            {
                return Size == 0 ? null : Path[Path.Count - 1];
            }
        }

        public int Rank
        {
            get
            {
                if (Size == 0) return 1; // Simple dimension
                int r = 0;
                foreach (ComColumn dim in Path)
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

        public int IndexOfGreater(ComTable set) // Return index of the dimension with this greater set
        {
            for (int i = 0; i < Path.Count; i++)
            {
                if (Path[i].GreaterSet == set) return i;
            }
            return -1;
        }
        public int IndexOfLesser(ComTable set) // Return index of the dimension with this lesser set
        {
            for (int i = 0; i < Path.Count; i++)
            {
                if (Path[i].LesserSet == set) return i;
            }
            return -1;
        }
        public int IndexOf(ComColumn dim) // Return index of the specified dimension in this path
        {
            return Path.IndexOf(dim);
        }
        public int IndexOf(DimPath path) // Return index of the beginning of the specified path in this path
        {
            throw new NotImplementedException();
        }

        public bool StartsWith(ComColumn dim)
        {
            if(Size == 0) return false;
            return Path[0] == dim;
        }
        public bool StartsWith(DimPath path)
        {
            return StartsWith(path.Path);
        }
        public bool StartsWith(List<ComColumn> path)
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
        public bool SamePath(List<ComColumn> path) // Equals (the same segments)
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

        public void InsertAt(ComColumn dim) // Insert a new segment at the specified position
        {
            throw new NotImplementedException();
        }
        public void InsertAt(DimPath path) // Insert a new segment at the specified position
        {
            throw new NotImplementedException();
        }

        public void InsertFirst(ComColumn dim) // Insert a new segment at the beginning of the path
        {
            Debug.Assert(Size == 0 || dim.GreaterSet == LesserSet, "A path must continue the first segment inserted in the beginning.");

            Path.Insert(0, dim);
            LesserSet = dim.LesserSet;
            if (GreaterSet == null) GreaterSet = dim.GreaterSet;
        }
        public void InsertFirst(DimPath path) // Insert new segments from the specified path at the beginning of the path
        {
            Debug.Assert(Size == 0 || path.GreaterSet == LesserSet, "A path must continue the first segment inserted in the beginning.");

            Path.InsertRange(0, path.Path);
            LesserSet = path.LesserSet;
            if (GreaterSet == null) GreaterSet = path.GreaterSet;
        }

        public void InsertLast(ComColumn dim) // Append a new segment to the end of the path
        {
            Debug.Assert(Size == 0 || dim.LesserSet == GreaterSet, "A new segment appended to a path must continue the previous segments");

            Path.Add(dim);
            GreaterSet = dim.GreaterSet;
            if (LesserSet == null) LesserSet = dim.LesserSet;
        }
        public void InsertLast(DimPath path) // Append all segments of the specified path to the end of this path
        {
            Debug.Assert(Size == 0 || path.LesserSet == GreaterSet, "A an appended path must continue this path.");

            if (path == null || path.Size == 0) return;

            for (int i = 0; i < path.Path.Count; i++)
            {
                Path.Add(path.Path[i]);
            }

            GreaterSet = path.GreaterSet;
            if (LesserSet == null) LesserSet = path.LesserSet;
        }

        #endregion // Add segments

        #region Remove segments

        private ComColumn RemoveAt(int index)
        {
            if (Size == 0) return null; // Nothing to remove
            if (index < 0 || index >= Path.Count) return null; // Bad index

            ComColumn result = Path[index];
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

        public ComColumn RemoveFirst()
        {
            return RemoveFirstAt(0);
        }
        public ComColumn RemoveFirstAt(int index) // TODO: Implement an additional argument with the number of segments to remove
        {
            ComColumn result = RemoveAt(index);
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
        public void RemoveFirst(ComTable set) // Remove first segments till this set (the new path will start from the specified set if trimmed)
        {
            if (LesserSet == set) return; // Already here

            // Find a path to the specified set
            int index = this.IndexOfGreater(set);
            if (index < 0) return;

            Path.RemoveRange(0, index+1);

            if (Path.Count > 0) LesserSet = Path[0].LesserSet;
            else LesserSet = GreaterSet;
        }

        public ComColumn RemoveLast() // Remove last segment
        {
            return RemoveLastAt(Path.Count - 1);
        }
        public ComColumn RemoveLastAt(int index) // TODO: Implement an additional argument with the number of segments to remove
        {
            ComColumn result = RemoveAt(index);
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
        public void RemoveLast(ComTable set) // Remove last segments starting from this set (the new path will end with the specified set if trimmed)
        {
            throw new NotImplementedException();
        }

        #endregion // Remove segments

        protected List<ComColumn> GetAllSegments()
        {
            List<ComColumn> result = new List<ComColumn>();
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

        public ComColumn GetSegment(int rank)
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
            if (Size == 0) return null;
            return null;
        }

        #region ComJson Serialization

        public override void ToJson(JObject json) // Write fields to the json object
        {
            base.ToJson(json); // Dim

            JArray segments = (JArray)json["segments"];
            if (segments == null)
            {
                segments = new JArray();
            }

            for (int i = 0; i < Size; i++)
            {
                JObject segRef = Utils.CreateJsonRef(Path[i]);
                segments.Add(segRef);
            }

            json["segments"] = segments;
        }
        public override void FromJson(JObject json, Workspace ws) // Init this object fields by using json object
        {
            base.FromJson(json, ws); // Dim

            var segs = new List<ComColumn>();
            JArray segments = (JArray)json["segments"];
            for (int i = 0; i < segments.Count; i++)
            {
                JObject segRef = (JObject)segments[i];
                ComColumn column = (ComColumn)Utils.ResolveJsonRef(segRef, ws);
                if (column == null) // Failed to resolve the segment
                {
                    segs.Clear(); // Empty path because some segment is absent
                    break;
                }
                segs.Add(column);
            }

            for (int i = 0; i < segs.Count; i++)
            {
                InsertLast(segs[i]);
            }
        }

        #endregion

        #region Overriding System.Object and interfaces

        public override string ToString()
        {
            string path = "";
            for (int i = 0; i < Size; i++)
            {
                path += "[" + Path[i].Name + "].";
            }
            if (Size > 0) path = path.Substring(0, path.Length-1);

            return String.Format("{0}: {1} -> {2}, {3}", Name, LesserSet.Name, GreaterSet.Name, path);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (Object.ReferenceEquals(this, obj)) return true;
            //if (this.GetType() != obj.GetType()) return false;

            if (obj is DimPath)
            {
                List<ComColumn> objSegs = ((DimPath)obj).Path;

                if (Size != objSegs.Count) return false;
                for (int i = 0; i < Size; i++) if (Path[i] != objSegs[i]) return false;
                return true;
            }
            else if (obj is ComColumn)
            {
                ComColumn objSeg = (ComColumn)obj;

                if (Size != 1) return false;
                if (FirstSegment != objSeg) return false;
                return true;
            }
            else if (obj is IList) // List of objects that implement interface
            {
                var objSegs = (IList)obj;

                if (Size != objSegs.Count) return false;
                for (int i = 0; i < Size; i++) if (Path[i] != objSegs[i]) return false;
                return true;
            }
            else if(obj.GetType().IsGenericType && obj.GetType().GetGenericTypeDefinition() == typeof(IList<>)) 
            {
                List<object> objSegs = (obj as IEnumerable<object>).Cast<object>().ToList();
                //List<object> objSegs = (obj as IEnumerable).OfType<object>().ToList();

                if (Size != objSegs.Count) return false;
                for (int i = 0; i < Size; i++) if (Path[i] != objSegs[i]) return false;
                return true;

                // Alternatively, we can get the generic type and check if it is a column object
                //Type lt = obj.GetType().GetGenericArguments()[0];
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        #endregion

        #region Constructors and initializers.

        public DimPath()
        {
            Path = new List<ComColumn>();
        }

        public DimPath(ComTable set)
            : this()
        {
            LesserSet = set;
            GreaterSet = set;
        }

        public DimPath(string name)
            : base(name)
        {
            Path = new List<ComColumn>();
        }

        public DimPath(ComColumn segment)
            : this()
        {
            if (segment == null) return;

            Path.Add(segment);
            LesserSet = Path[0].LesserSet;
            GreaterSet = Path[Path.Count - 1].GreaterSet;
        }

        public DimPath(List<ComColumn> segments)
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
            Path = new List<ComColumn>();
            Path.AddRange(path.Path);
        }

        public DimPath(string name, ComTable lesserSet, ComTable greaterSet)
            : base(name, lesserSet, greaterSet)
        {
            Path = new List<ComColumn>();
        }

        #endregion

    }

    /// <summary>
    /// Relational attribute to be used in relational schemas (relational table and column classes). It is a primitive path - a sequence of normal dimensions leading to a primitive type. 
    /// </summary>
    public class DimAttribute : DimPath
    {
        #region CsColumn interface

        public override void Add()
        {
            //if (GreaterSet != null) ((SetRel)GreaterSet).AddLesserPath(this);
            if (LesserSet != null) ((SetRel)LesserSet).AddGreaterPath(this);

            // Notify that a new child has been added
            //if (LesserSet != null) ((Set)LesserSet).NotifyAdd(this);
            //if (GreaterSet != null) ((Set)GreaterSet).NotifyAdd(this);
        }

        public override void Remove()
        {
            //if (GreaterSet != null) ((SetRel)GreaterSet).RemoveLesserPath(this);
            if (LesserSet != null) ((SetRel)LesserSet).RemoveGreaterPath(this);

            // Notify that a new child has been removed
            //if (LesserSet != null) ((Set)LesserSet).NotifyRemove(this);
            //if (GreaterSet != null) ((Set)GreaterSet).NotifyRemove(this);
        }

        #endregion

        /// <summary>
        /// Additional names specific to the relational model and imported from a relational schema. 
        /// </summary>
        public string RelationalColumnName { get; set; } // Original column name used in the database
        public string RelationalPkName { get; set; } // PK this column belongs to according to the relational schema
        public string RelationalFkName { get; set; } // Original FK name this column belongs to
        public string RelationalTargetTableName { get; set; }
        public string RelationalTargetColumnName { get; set; }

        /// <summary>
        /// Expand one attribute by building its path segments as dimension objects. 
        /// Use the provided list of attributes for expansion recursively. This list essentially represents a schema.
        /// Also, adjust path names in special cases like empty name or simple structure. 
        /// </summary>
        public void ExpandAttribute(List<DimAttribute> attributes, List<ComColumn> columns) // Add and resolve attributes by creating dimension structure from FKs
        {
            DimAttribute att = this;

            if(att.Path.Count > 0) return; // Already expanded (because of recursion)

            bool isKey = !string.IsNullOrEmpty(att.RelationalPkName) || att.IsIdentity;

            if (string.IsNullOrEmpty(att.RelationalFkName)) // No FK - primitive column - end of recursion
            {
                // Find or create a primitive dim segment
                ComColumn seg = columns.FirstOrDefault(c => c.LesserSet == att.LesserSet && StringSimilarity.SameColumnName(((DimRel)c).RelationalFkName, att.RelationalFkName));
                if (seg == null)
                {
                    seg = new DimRel(att.RelationalColumnName, att.LesserSet, att.GreaterSet, isKey, false); // Maybe copy constructor?
                    ((DimRel)seg).RelationalFkName = att.RelationalFkName;
                    columns.Add(seg);
                }

                att.InsertLast(seg); // add it to this attribute as a single segment
            }
            else { // There is FK - non-primitive column
                // Find target set and target attribute (name resolution)
                DimAttribute tailAtt = attributes.FirstOrDefault(a => StringSimilarity.SameTableName(a.LesserSet.Name, att.RelationalTargetTableName) && StringSimilarity.SameColumnName(a.Name, att.RelationalTargetColumnName));
                ComTable gSet = tailAtt.LesserSet;

                // Find or create a dim segment
                ComColumn seg = columns.FirstOrDefault(c => c.LesserSet == att.LesserSet && StringSimilarity.SameColumnName(((DimRel)c).RelationalFkName, att.RelationalFkName));
                if (seg == null)
                {
                    seg = new DimRel(att.RelationalFkName, att.LesserSet, gSet, isKey, false);
                    ((DimRel)seg).RelationalFkName = att.RelationalFkName;
                    columns.Add(seg);
                }

                att.InsertLast(seg); // add it to this attribute as first segment

                //
                // Recursion. Expand tail attribute and add all segments from the tail attribute (continuation)
                //
                tailAtt.ExpandAttribute(attributes, columns);
                att.InsertLast(tailAtt);

                // Adjust name. How many attributes belong to the same FK as this attribute (FK composition)
                List<DimAttribute> fkAtts = attributes.Where(a => a.LesserSet == att.LesserSet && StringSimilarity.SameColumnName(att.RelationalFkName, a.RelationalFkName)).ToList();
                if(fkAtts.Count == 1) 
                {
                    seg.Name = att.RelationalColumnName; // Adjust name. For 1-column FK, name of the FK-dim is the column name (not the FK name)
                }
            }
        }


        /// <summary>
        /// Convert nested path to a flat canonical representation as a sequence of simple dimensions which do not contain other dimensions.
        /// Initially, paths are pairs <this set dim, greater set path>. We recursively replace all nested paths by dimensions.
        /// Also, adjust path names in special cases like empty name or simple structure. 
        /// </summary>
        [System.Obsolete("Use ExpandAttribute(s)", true)]
        public void ExpandPath()
        {
            //
            // Flatten all paths by converting <dim, greater-path> pairs by sequences of dimensions <dim, dim2, dim3,...>
            //
            List<ComColumn> allSegments = GetAllSegments();
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
            if (!String.IsNullOrEmpty(RelationalFkName) /*&& GreaterSet.IdentityPrimitiveArity == 1*/)
            {
                Path[0].Name = Name; // FK-name is overwritten and lost - attribute name is used instead
            }

            //
            // Dim name adjustment: for 1-column FK dimensions, we prefer to use its only column name instead of the FK-name (fkName is not used)
            //
            if (!String.IsNullOrEmpty(RelationalFkName) /*&& GreaterSet.IdentityPrimitiveArity == 1*/)
            {
                Path[0].Name = Name; // FK-name is overwritten and lost - attribute name is used instead
            }
        }

        #region ComJson Serialization

        public override void ToJson(JObject json) // Write fields to the json object
        {
            base.ToJson(json); // DimPath

            json["RelationalColumnName"] = RelationalColumnName;
            json["RelationalPkName"] = RelationalPkName;
            json["RelationalFkName"] = RelationalFkName;
            json["RelationalTargetTableName"] = RelationalTargetTableName;
            json["RelationalTargetColumnName"] = RelationalTargetColumnName;
        }
        public override void FromJson(JObject json, Workspace ws) // Init this object fields by using json object
        {
            base.FromJson(json, ws); // DimPath

            RelationalColumnName = (string)json["RelationalColumnName"];
            RelationalPkName = (string)json["RelationalPkName"];
            RelationalFkName = (string)json["RelationalFkName"];
            RelationalTargetTableName = (string)json["RelationalTargetTableName"];
            RelationalTargetColumnName = (string)json["RelationalTargetColumnName"];
        }

        #endregion

        public DimAttribute()
            : base()
        {
        }

        public DimAttribute(string name)
            : base(name)
        {
        }

        public DimAttribute(DimPath path)
            : base(path)
        {
        }

        public DimAttribute(string name, ComTable lesserSet, ComTable greaterSet)
            : base(name, lesserSet, greaterSet)
        {
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

        public DimEnumerator(ComTable set)
            : base(set)
        {
            Path = new List<ComColumn>();
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
        protected List<ComTable> lesserSets;
        protected List<ComTable> greaterSets;
        protected bool isInverse;
        protected DimensionType dimType; // The path will be composed of only these types of segments
        protected bool allowIntermediateSets = false; // Not implemented. lesser and greater sets only as source and destination - not in the middle of the path (default). Otherwise, they can appear in the middle of the path (say, one greater set and then the final greater set).

        // Here we store the current child number for the next element
        protected int[] childNumber = new int[1024];
        // Whether the current set is valid or not. If it is valid then all parents/previous are invalid in the case of coverage condition.
        protected bool[] isValidSet = new bool[1024];

        public DimComplexEnumerator(List<ComTable> _lesserSets, List<ComTable> _greaterSets, bool _isInverse, DimensionType _dimType)
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

            childNumber = Enumerable.Repeat(-1, 1024).ToArray();
            isValidSet = Enumerable.Repeat(false, 1024).ToArray();
        }

        public DimComplexEnumerator(ComTable set)
            : base(set)
        {
            lesserSets = new List<ComTable>(new ComTable[] { set }); // One source set
            greaterSets = new List<ComTable>(new ComTable[] { set.Top.Root }); // All destination sets from this schema

            isInverse = false;

            childNumber = Enumerable.Repeat(-1, 1024).ToArray();
            isValidSet = Enumerable.Repeat(false, 1024).ToArray();
        }
    }

    /// <summary>
    /// Enumerate all different paths between specified sets. 
    /// Notes:
    /// - Consider only segments that satisfy constraints imposed on segment types (entity, identity etc.) Only these segments are used to traverse the poset.
    /// - Consider only sets that satisfy the constraints imposed on possible lesser and greater sets (only primitive, all, selected sets etc.) Only paths starting/ending with these sets can be returned (and the paths involve only segments of the specified type).
    /// - Currently we return only maximum length paths. 
    /// </summary>
    public class PathEnumerator : DimComplexEnumerator
    {
        public PathEnumerator(ComTable set, DimensionType _dimType) // All primitive paths
            : this(new List<ComTable>(new ComTable[] { set }), null, false, _dimType)
        {
        }

        public PathEnumerator(ComTable lesserSet, ComTable greaterSet, DimensionType _dimType) // Between two sets
            : this(new List<ComTable>(new ComTable[] { lesserSet }), new List<ComTable>(new ComTable[] { greaterSet }), false, _dimType)
        {
        }

        public PathEnumerator(List<ComTable> _lesserSets, List<ComTable> _greaterSets, bool _isInverse, DimensionType _dimType)
            : base(_lesserSets, _greaterSets, _isInverse, _dimType)
        {
        }

        public override bool MoveNext()
        {
            // TODO: We need also loop over all source sets

            // It is a traverse algorithm. We move either one step forward or one step back on each iteration of the loop.
            // Various conditions are checked after we arrive at a new child (forward) or before we leave a node and go to the parent. 
            // The algorithm returns if either a good node is found (true) or no nodes exist anymore (false).
            while (true)
            {
                var nextSegs = GetNextValidSegments();
                int childNo = childNumber[Size];

                if (childNo + 1 < nextSegs.Count) // Can move to the next child
                {
                    childNumber[Size] = childNo + 1;
                    AddLastSegment(nextSegs[childNo + 1]);

                    // Process a new node
                    isValidSet[Size] = TargetSetValid();
                    // Coverage. We are interested only in longest paths. If we need to return all (even shorter) paths then remove this block.
                    if (isValidSet[Size])
                    {
                        for (int i = 0; i < Size; i++) isValidSet[i] = false;
                    }

                    // Process the child node just added on the next iteration.
                }
                else // No children anymore - return to the previous parent
                {
                    // Before leaving this node, we are able to decide whether it is what we have been looking for because all its children have been processed
                    // If it is what we need then return true
                    if (isValidSet[Size])
                    {
                        isValidSet[Size] = false; // During next call, we have to skip this block
                        return true;
                    }

                    if (Size == 0) // Detect finish condition
                    {
                        return false; // All nodes have been visited - no nodes anymore
                    }

                    // Really leave this node
                    childNumber[Size] = -1;
                    ComColumn column = RemoveLastSegment();

                    // Process the parent node we have just returned to on the next iteration.
                }

            }
                
        }

        [System.Obsolete("Not needed.", true)]
        private bool MoveForward() // return true - valid destination set found, false - no valid destination found and cannot continue (terminal)
        {
            // Move using only valid segments. 
            List<ComColumn> nextSegs;
            bool moved = false;
            while (true)
            {
                nextSegs = GetNextValidSegments();
                if (nextSegs.Count == 0) return moved;

                moved = true;
                AddLastSegment(nextSegs[0]); // always attach first possible segment (next segments will be attached while moving back)
            }
        }
        [System.Obsolete("Not needed.", true)]
        private bool MoveBackward() // return true - found a continuation from some parent, false - not found a set with possibility to continue(end, go to next source set)
        {
            ComColumn segment = null;
            while(true) // A loop for removing last segment and moving backward
            {
                segment = RemoveLastSegment(); // Remove last segment
                if (segment == null)
                {
                    return false; // All segments removed but no continuation found in any of the previous sets including the source one
                }

                List<ComColumn> nextSegs = GetNextValidSegments();

                int segIndex = nextSegs.IndexOf(segment);
                if (segIndex + 1 < nextSegs.Count) // Continuation found. Use it
                {
                    segment = nextSegs[segIndex + 1];
                    AddLastSegment(segment); // Add next last segment

                    return true;
                }
                else // Continuation from the parent not found. Continue removing to the next parent. 
                {
                }
            }
        }

        private List<ComColumn> GetNextValidSegments() // Here we find continuation segments that satisfy our criteria on columns
        {
            if (!isInverse) // Move up from lesser to greater
            {
                if (GreaterSet.IsPrimitive) return new List<ComColumn>(); // We exclude the top element
                switch (dimType)
                {
                    case DimensionType.IDENTITY_ENTITY: return GreaterSet.GreaterDims.Where(x => x.LesserSet.Top == x.GreaterSet.Top).ToList();
                    case DimensionType.IDENTITY: return GreaterSet.GreaterDims.Where(x => x.IsIdentity && x.LesserSet.Top == x.GreaterSet.Top).ToList();
                    case DimensionType.ENTITY: return GreaterSet.GreaterDims.Where(x => !x.IsIdentity && x.LesserSet.Top == x.GreaterSet.Top).ToList();

                    case DimensionType.GENERATING: return GreaterSet.GreaterDims.Where(x => (x.Definition != null && x.Definition.IsGenerating) && x.LesserSet.Top == x.GreaterSet.Top).ToList();
                }
            }
            else
            {
                switch (dimType)
                {
                    case DimensionType.IDENTITY_ENTITY: return LesserSet.LesserDims.Where(x => x.LesserSet.Top == x.GreaterSet.Top).ToList();
                    case DimensionType.IDENTITY: return LesserSet.LesserDims.Where(x => x.IsIdentity && x.LesserSet.Top == x.GreaterSet.Top).ToList();
                    case DimensionType.ENTITY: return LesserSet.LesserDims.Where(x => !x.IsIdentity && x.LesserSet.Top == x.GreaterSet.Top).ToList();

                    case DimensionType.GENERATING: return LesserSet.LesserDims.Where(x => (x.Definition != null && x.Definition.IsGenerating) && x.LesserSet.Top == x.GreaterSet.Top).ToList();
                }
            }

            return null;
        }
        private ComColumn RemoveLastSegment()
        {
            if (Size == 0) return null; // Nothing to remove

            ComColumn segment = null;
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
        private void AddLastSegment(ComColumn segment)
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
        private bool TargetSetValid() // Here we determined of the set satisfy our criteria on tables
        {
            List<ComTable> destinations = !isInverse ? greaterSets : lesserSets;
            ComTable dest = !isInverse ? GreaterSet : LesserSet;

            if (destinations == null) // Destinations are primitive and only primitive sets
            {
                if (!isInverse) return GreaterSet.IsPrimitive;
                else return LesserSet.IsLeast; // Just least set because primitive sets do not exist for least sets
            }
            else if (destinations.Count == 0) // Destinations are terminal sets (greatest or least). Check possibility to continue.
            {
                return true;
/*
                if (!isInverse) return GreaterSet.IsGreatest;
                else return LesserSet.IsLeast;
*/
            }
            else // Concrete destinations are specified
            {
                foreach (ComTable set in destinations) if (dest.IsIn(set)) return true;
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

    // TODO: We probably should introduce a bit mask instead of the enumerator
    // Bits: isIdentity, isPoset (exclude isSuper), isInclusion (exclude isPoset), isInterschema (greater != lesser), isInverse, Definition.isGenerating
    public enum DimensionType
    {
        INCLUSION, // Both super and sub
        SUPER, // 
        SUB, // 

        POSET, // Both greater and lesser
        GREATER, // 
        LESSER, // 

        IDENTITY_ENTITY, // Both identity and entity
        IDENTITY, // IsKey
        ENTITY, // !IsKey

        GENERATING,
    }

}
