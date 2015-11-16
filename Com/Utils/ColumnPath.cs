using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Schema;

using Rowid = System.Int32;

namespace Com.Utils
{
    /// <summary>
    /// A sequence of simple columns (segments). 
    /// </summary>
    public class ColumnPath : Column
    {
        /// <summary>
        /// A column can be defined as a sequence of other columns. For simple columns the path is empty.
        /// </summary>
        public List<DcColumn> Segments { get; set; }

        public int Size
        {
            get
            {
                return Segments.Count;
            }
        }

        public DcColumn FirstSegment
        {
            get
            {
                return Size == 0 ? null : Segments[0];
            }
        }

        public DcColumn LastSegment
        {
            get
            {
                return Size == 0 ? null : Segments[Segments.Count - 1];
            }
        }

        public int Rank
        {
            get
            {
                if (Size == 0) return 1; // Simple column
                int r = 0;
                foreach (DcColumn col in Segments)
                {
                    r += 1; // col.Rank;
                }
                return r;
            }
        }

        public bool IsComplex
        {
            get
            {
                return Segments != null && Segments.Count > 0;
            }
        }

        public int IndexOfGreater(DcTable tab) // Return index of the column with this greater table
        {
            for (int i = 0; i < Segments.Count; i++)
            {
                if (Segments[i].Output == tab) return i;
            }
            return -1;
        }
        public int IndexOfLesser(DcTable tab) // Return index of the column with this lesser table
        {
            for (int i = 0; i < Segments.Count; i++)
            {
                if (Segments[i].Input == tab) return i;
            }
            return -1;
        }
        public int IndexOf(DcColumn col) // Return index of the specified column in this path
        {
            return Segments.IndexOf(col);
        }
        public int IndexOf(ColumnPath path) // Return index of the beginning of the specified path in this path
        {
            throw new NotImplementedException();
        }

        public bool StartsWith(DcColumn col)
        {
            if(Size == 0) return false;
            return Segments[0] == col;
        }
        public bool StartsWith(ColumnPath path)
        {
            return StartsWith(path.Segments);
        }
        public bool StartsWith(List<DcColumn> path)
        {
            if (Segments.Count < path.Count) return false;
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] != Segments[i]) return false;
            }
            return true;
        }

        public bool SamePath(ColumnPath path) // Equals (the same segments)
        {
            return SamePath(path.Segments);
        }
        public bool SamePath(List<DcColumn> path) // Equals (the same segments)
        {
            if (path == null) return false;

            if (Segments.Count != path.Count) return false;

            for (int i = 0; i < path.Count; i++)
            {
                if (!path[i].Equals(Segments[i])) return false;
            }
            return true;
        }

        public ColumnPath SubPath(int index, int count = 0) // Return a new path consisting of the specified segments
        {
            ColumnPath ret = new ColumnPath();

            if (count == 0) count = Segments.Count - index;

            for (int i = 0; i < count; i++)
            {
                ret.Segments.Add(Segments[index + i]);
            }

            ret.Output = ret.Segments[0].Input;
            ret.Input = ret.Segments[ret.Segments.Count - 1].Output;

            return ret;
        }

        #region Add segments

        public void InsertAt(DcColumn col) // Insert a new segment at the specified position
        {
            throw new NotImplementedException();
        }
        public void InsertAt(ColumnPath path) // Insert a new segment at the specified position
        {
            throw new NotImplementedException();
        }

        public void InsertFirst(DcColumn col) // Insert a new segment at the beginning of the path
        {
            Debug.Assert(Size == 0 || col.Output == Input, "A path must continue the first segment inserted in the beginning.");

            Segments.Insert(0, col);
            Input = col.Input;
            if (Output == null) Output = col.Output;
        }
        public void InsertFirst(ColumnPath path) // Insert new segments from the specified path at the beginning of the path
        {
            Debug.Assert(Size == 0 || path.Output == Input, "A path must continue the first segment inserted in the beginning.");

            Segments.InsertRange(0, path.Segments);
            Input = path.Input;
            if (Output == null) Output = path.Output;
        }

        public void InsertLast(DcColumn col) // Append a new segment to the end of the path
        {
            Debug.Assert(Size == 0 || col.Input == Output, "A new segment appended to a path must continue the previous segments");

            Segments.Add(col);
            Output = col.Output;
            if (Input == null) Input = col.Input;
        }
        public void InsertLast(ColumnPath path) // Append all segments of the specified path to the end of this path
        {
            Debug.Assert(Size == 0 || path.Input == Output, "A an appended path must continue this path.");

            if (path == null || path.Size == 0) return;

            for (int i = 0; i < path.Segments.Count; i++)
            {
                Segments.Add(path.Segments[i]);
            }

            Output = path.Output;
            if (Input == null) Input = path.Input;
        }

        #endregion // Add segments

        #region Remove segments

        private DcColumn RemoveAt(int index)
        {
            if (Size == 0) return null; // Nothing to remove
            if (index < 0 || index >= Segments.Count) return null; // Bad index

            DcColumn result = Segments[index];
            Segments.RemoveAt(index);

            if (Segments.Count != 0)
            {
                Input = Segments[0].Input;
                Output = Segments[Segments.Count - 1].Output;
            }
            else
            {
                // Note: Input table and Output table are not table - this must be done in public methods and depends on whether it is removed as first or last segment (it is important for some algorithms)
            }

            return result;
        }

        public DcColumn RemoveFirst()
        {
            return RemoveFirstAt(0);
        }
        public DcColumn RemoveFirstAt(int index) // TODO: Implement an additional argument with the number of segments to remove
        {
            DcColumn result = RemoveAt(index);
            if (result == null) return result;

            if (Segments.Count == 0) // This where removal of the first and the last segments is different
            {
                Input = result.Output;
                Output = result.Output;
            }

            return result;
        }
        public void RemoveFirst(ColumnPath path) // Remove first segments
        {
            if (Segments.Count < path.Segments.Count) return; // Nothing to remove
            if (!this.StartsWith(path)) return;

            Segments.RemoveRange(0, path.Segments.Count);

            if (Segments.Count > 0) Input = Segments[0].Input;
            else Input = Output;
        }
        public void RemoveFirst(DcTable tab) // Remove first segments till this table (the new path will start from the specified table if trimmed)
        {
            if (Input == tab) return; // Already here

            // Find a path to the specified table
            int index = this.IndexOfGreater(tab);
            if (index < 0) return;

            Segments.RemoveRange(0, index+1);

            if (Segments.Count > 0) Input = Segments[0].Input;
            else Input = Output;
        }

        public DcColumn RemoveLast() // Remove last segment
        {
            return RemoveLastAt(Segments.Count - 1);
        }
        public DcColumn RemoveLastAt(int index) // TODO: Implement an additional argument with the number of segments to remove
        {
            DcColumn result = RemoveAt(index);
            if (result == null) return result;

            if (Segments.Count == 0) // This where removal of the first and the last segments is different
            {
                Input = result.Input;
                Output = result.Input;
            }

            return result;
        }
        public void RemoveLast(ColumnPath path) // Remove last segments (suffix)
        {
            throw new NotImplementedException();
        }
        public void RemoveLast(DcTable tab) // Remove last segments starting from this table (the new path will end with the specified table if trimmed)
        {
            throw new NotImplementedException();
        }

        #endregion // Remove segments

        protected List<DcColumn> GetAllSegments()
        {
            List<DcColumn> result = new List<DcColumn>();
            for (int i = 0; i < Segments.Count; i++)
            {
                if (Segments[i] is ColumnPath && ((ColumnPath)Segments[i]).IsComplex)
                {
                    result.AddRange(((ColumnPath)Segments[i]).GetAllSegments());
                }
                else // Simple segment - nothing to expand
                {
                    result.Add(Segments[i]);
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

        public DcColumn GetSegment(int rank)
        {
            Debug.Assert(rank >= 0, "Wrong use of method parameter. Rank cannot be negative.");
            return rank < Segments.Count ? Segments[rank] : null; // TODO: take into account the nested structure of complex columns
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

        #region DcJson Serialization

        public override void ToJson(JObject json) // Write fields to the json object
        {
            base.ToJson(json); // Column

            JArray segments = (JArray)json["segments"];
            if (segments == null)
            {
                segments = new JArray();
            }

            for (int i = 0; i < Size; i++)
            {
                JObject segRef = Com.Schema.Utils.CreateJsonRef(Segments[i]);
                segments.Add(segRef);
            }

            json["segments"] = segments;
        }
        public override void FromJson(JObject json, DcSpace ws) // Init this object fields by using json object
        {
            base.FromJson(json, ws); // Column

            var segs = new List<DcColumn>();
            JArray segments = (JArray)json["segments"];
            for (int i = 0; i < segments.Count; i++)
            {
                JObject segRef = (JObject)segments[i];
                DcColumn column = (DcColumn)Com.Schema.Utils.ResolveJsonRef(segRef, ws);
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

        public string NamePath // A list of segment column names (dot separated)
        {
            get
            {
                if (Size == 0) return "";
                string complexName = "";
                foreach (DcColumn seg in Segments) complexName += "[" + seg.Name + "].";
                complexName = complexName.Substring(0, complexName.Length - 1); // Remove last dot
                return complexName;
            }
        }

        public override string ToString()
        {
            string path = "";
            for (int i = 0; i < Size; i++)
            {
                path += "[" + Segments[i].Name + "].";
            }
            if (Size > 0) path = path.Substring(0, path.Length-1);

            return String.Format("{0}: {1} -> {2}, {3}", Name, Input.Name, Output.Name, path);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (Object.ReferenceEquals(this, obj)) return true;
            //if (this.GetType() != obj.GetType()) return false;

            if (obj is ColumnPath)
            {
                List<DcColumn> objSegs = ((ColumnPath)obj).Segments;

                if (Size != objSegs.Count) return false;
                for (int i = 0; i < Size; i++) if (Segments[i] != objSegs[i]) return false;
                return true;
            }
            else if (obj is DcColumn)
            {
                DcColumn objSeg = (DcColumn)obj;

                if (Size != 1) return false;
                if (FirstSegment != objSeg) return false;
                return true;
            }
            else if (obj is IList) // List of objects that implement interface
            {
                var objSegs = (IList)obj;

                if (Size != objSegs.Count) return false;
                for (int i = 0; i < Size; i++) if (Segments[i] != objSegs[i]) return false;
                return true;
            }
            else if(obj.GetType().IsGenericType && obj.GetType().GetGenericTypeDefinition() == typeof(IList<>)) 
            {
                List<object> objSegs = (obj as IEnumerable<object>).Cast<object>().ToList();
                //List<object> objSegs = (obj as IEnumerable).OfType<object>().ToList();

                if (Size != objSegs.Count) return false;
                for (int i = 0; i < Size; i++) if (Segments[i] != objSegs[i]) return false;
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

        public ColumnPath()
        {
            Segments = new List<DcColumn>();
        }

        public ColumnPath(DcTable tab)
            : this()
        {
            Input = tab;
            Output = tab;
        }

        public ColumnPath(string name)
            : base(name)
        {
            Segments = new List<DcColumn>();
        }

        public ColumnPath(DcColumn seg)
            : this()
        {
            if (seg == null) return;

            Segments.Add(seg);
            Input = Segments[0].Input;
            Output = Segments[Segments.Count - 1].Output;
        }

        public ColumnPath(List<DcColumn> segs)
            : this()
        {
            if(segs == null || segs.Count == 0) return;

            Segments.AddRange(segs);
            Input = Segments[0].Input;
            Output = Segments[Segments.Count - 1].Output;
        }

        public ColumnPath(ColumnPath path)
            : base(path)
        {
            Segments = new List<DcColumn>();
            Segments.AddRange(path.Segments);
        }

        public ColumnPath(string name, DcTable input, DcTable output)
            : base(name, input, output)
        {
            Segments = new List<DcColumn>();
        }

        #endregion

    }

    // TODO: We probably should introduce a bit mask instead of the enumerator
    // Bits: isIdentity, isPoset (exclude isSuper), isInclusion (exclude isPoset), isInterschema (greater != lesser), isInverse, Definition.isGenerating
    public enum ColumnType
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
