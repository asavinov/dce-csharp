using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// An abstract dimension with storage methods but without implementation. 
    /// Concrete implementations of the storage depending on the value type are implemented in the extensions which have to used. 
    /// Extensions also can define functions defined via a formula or a query to an external database.
    /// It is only important that a function somehow impplements a mapping from its lesser set to its greater set. 
    /// </summary>
    public class Dim
    {
        #region Properties

        private static int uniqueId;

        /// <summary>
        /// Unique id within this database or (temporary) query session.  
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        public string Name { get; set; }
        public string ComplexName 
        {
            get 
            {
                if (Path == null || Path.Count == 0) return "";
                string complexName = "";
                foreach (Dim seg in Path) complexName += "_" + seg.Name;
                return complexName;
            }
        }
        public string HashName
        {
            get
            {
                if (Path == null || Path.Count == 0) return "0";
                int hash = 0;
                foreach (Dim seg in Path) hash += seg.Id.GetHashCode();

                hash = Math.Abs(hash);
                string hashName = hash.ToString("X"); // unique hash representing this path
                return hashName.Length > 6 ? hashName.Substring(0, 6) : hashName;
            }
        }

        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// These fields can be extracted into some child class if it will be created like relational dimension, path dimension etc.
        /// </summary>
        public string RelationalColumnName { get; set; } // For paths, it is the original column name used in the database (can be moved to a child class if such will be introduced for relational dimensions or for path dimensions). 
        public string RelationalFkName { get; set; } // For dimensions, which were created from FK, it stores the original FK name
        public string RelationalPkName { get; set; } // PK this column belongs to according to the schema

        public virtual int Width // Width of instances. It depends on the implementation (and might not be the same for all dimensions of the greater set). 
        {
            get { return GreaterSet != null ? GreaterSet.Width : 0; }
        }

        protected int _length;
        public virtual int Length // How many instances. It is the same for all dimensions of the lesser set. 
        {
            get { return _length; }
        }

        /// <summary>
        /// Is identity dimension.
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// Reversed dimension has the opposite semantic interpretation (direction). It is used to resolve semantic cycles. 
        /// For example, when a department references its manager then this dimension is makred by this flag. 
        /// One use is when deciding +how to interpret input and output dimensions of sets and lesser/greater sets of dimensions.
        /// </summary>
        public bool IsReversed { get; set; }

        /// <summary>
        /// Whether this dimension is supposed (able) to have instances. Some dimensions are used for conceptual purposes. 
        /// It is not about having zero instances - it is about the ability to have instances (essentially supporting the corresponding interface for working with instances).
        /// This flag is true for extensions which implement data-related methods (and in this sense it is reduntant because duplicates 'instance of').
        /// Different interpretations: the power of the domain can increase; the power of the domain is not 0; 
        /// </summary>
        private bool _instantiable;
        public bool IsInstantiable { get { return LesserSet.IsInstantiable; } private set { _instantiable = value; } }

        /// <summary>
        /// Whether this dimension to take no values.
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Temporary dimension is discarded after it has been used for computing other dimensions.
        /// It is normally invisible (private) dimension. 
        /// It can be created in the scope of some other dimension, expression or query, and then it is automatically deleted when the process exits this scope.
        /// </summary>
        public bool IsTemporary { get; set; }

        public bool IsPrimitive
        {
            get
            {
                return GreaterSet.IsPrimitive;
            }
        }

        #endregion

        #region Schema methods.

        /// <summary>
        /// Greater (output) set.
        /// </summary>
        public Set GreaterSet { get; set; }

        /// <summary>
        /// Lesser (input) set. 
        /// </summary>
        public Set LesserSet { get; set; }

        /// <summary>
        /// Parent dimension. 
        /// It is null for original complex dimensions of a set which point to a direct greater set.
        /// </summary>
        private Dim _parentDimension;
        public Dim ParentDimension
        {
            get { return _parentDimension; }
            set
            {
                _parentDimension = value; // TODO: Update all influenced elements.
            }
        }
        public Dim Root
        {
            get
            {
                Dim root = this;
                while (root.ParentDimension != null)
                {
                    root = root.ParentDimension;
                }

                return root;
            }
        }

        /// <summary>
        /// A dimension can be defined as a sequence of other dimensions. For simple dimensions the path is empty.
        /// </summary>
        public List<Dim> Path { get; set; }

        public void AddSegment(Dim dim)
        {
            if (Path == null)
            {
                Path = new List<Dim>();
            }

            Path.Add(dim);
            dim.LesserSet = GreaterSet; // We can add only to the end
            GreaterSet = dim.GreaterSet;
        }

        public Dim RemoveSegment()
        {
            if (Path == null)
            {
                Path = new List<Dim>();
                return null;
            }

            if (Path.Count == 0) return null; // Nothing to remove

            GreaterSet = Path[Path.Count - 1].LesserSet;
            Dim result = Path[Path.Count - 1];
            Path.RemoveAt(Path.Count - 1);
            return result;
        }

        public int Rank
        {
            get
            {
                if (Path == null || Path.Count == 0) return 1; // Simple dimension
                int r = 0;
                foreach (Dim dim in Path)
                {
                    r += dim.Rank;
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

        public bool StartsWith(List<Dim> path)
        {
            if (Path.Count < path.Count) return false;
            for (int i = 0; i < path.Count; i++ )
            {
                if (path[i] != Path[i]) return false;
            }
            return true;
        }

        public Dim FirstSegment
        {
            get
            {
                return Path == null || Path.Count == 0 ? null : Path[0];
            }
        }

        public Dim LastSegment
        {
            get
            {
                return Path == null || Path.Count == 0 ? null : Path[Path.Count - 1];
            }
        }

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
            if (Path == null) return null;
            List<Dim> result = new List<Dim>();
            for (int i = 0; i < Path.Count; i++)
            {
                if (Path[i].IsComplex)
                {
                    result.AddRange(Path[i].GetAllSegments());
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

        public void Concatenate(List<Dim> path)
        {
            Debug.Assert(path != null);
            if(Path[0].LesserSet == path[path.Count-1].GreaterSet) // Insert as prefix
            {
                Path.InsertRange(0, path);
                LesserSet = path[0].LesserSet;
            }
            else if (Path[Path.Count - 1].GreaterSet == path[0].LesserSet) // Append as suffix
            {
                Path.AddRange(path);
                GreaterSet = path[path.Count - 1].GreaterSet;
            }
            else
            {
                // Wrong use: two paths must be adjacent
            }
        }

        #endregion

        #region Function methods (abstract)

        public virtual void SetLength(Offset length) { } // We will not use it in public API - only a whole set length can be changed so a Set method has to be used

        public virtual void Append(object value) { } // We will not use it in public API - we can append only a whole record so a Set method has to be used

        public virtual void Insert(Offset offset, object value) { }

        public virtual object GetValue(Offset offset) { return null; }

        public virtual void SetValue(Offset offset, object value) { }

        public virtual Offset[] GetOffsets(object value) { return null; } // Accepts both a single object or an array

        public virtual object GetValues(Offset[] offsets) { return null; }

        /// <summary>
        /// It is a convenience property used to import/export data and other purposes. 
        /// </summary>
        public virtual object CurrentValue { get; set; }

        #endregion

        #region Function definition and expression evaluation

        /// <summary>
        /// It is a formula defining a function from the lesser set to the greater set. 
        /// When evaluated, it returs a (new) identity value of the greater set given an identity value of the lesser set.
        /// </summary>
        public Expression SelectExpression { get; set; }

        public virtual void Populate() { return; }

        public virtual void Unpopulate() { return; }

        #endregion

        #region Overriding System.Object and interfaces

        public override string ToString()
        {
            return String.Format("{0} From: {1}, To: {2}", Name, LesserSet.Name, GreaterSet.Name);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (Object.ReferenceEquals(this, obj)) return true;
            if (this.GetType() != obj.GetType()) return false;

            Dim dim = (Dim)obj;
            if (Id.Equals(dim.Id)) return true;

            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        #endregion

        #region Constructors and initializers.

        public Dim(string name)
            : this(name, null, null)
        {
        }

        public Dim(string name, Set lesserSet, Set greaterSet)
            : this(name, lesserSet, greaterSet, false, false, true)
        {
        }

        public Dim(string name, Set lesserSet, Set greaterSet, bool isIdentity, bool isReversed, bool isInstantiable)
        {
            Id = Guid.NewGuid();
            Name = name;

            IsIdentity = isIdentity;
            IsReversed = isReversed;
            IsInstantiable = isInstantiable;

            Path = new List<Dim>();

            LesserSet = lesserSet;
            GreaterSet = greaterSet;

            // Parameterize depending on the reserved names: super
            // Parameterize depending on the greater and lesser set type. For example, dimension type must correspond to its greater set type (SetInteger <- DimInteger etc.)
        }

        #endregion

    }

    public abstract class DimEnumerator : Dim, IEnumerator<List<Dim>> // IEnumerable<List<Dim>>
    {

        public DimEnumerator(Set set) : base("")
        {
            Path = new List<Dim>();
            LesserSet = set;
            GreaterSet = set;
        }

        // Get the explicit current node.
       public List<Dim> Current { get { return new List<Dim>(Path); } }

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
            Path.Clear();
        }

        // Get the underlying enumerator.
        public virtual IEnumerator<List<Dim>> GetEnumerator()
        {
            return (IEnumerator<List<Dim>>)this; 
        }
/*
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            // this calls the IEnumerator<Foo> GetEnumerator method
            // as explicit method implementations aren't used for method resolution in C#
            // polymorphism (IEnumerator<T> implements IEnumerator)
            // ensures this is type-safe
            return GetEnumerator();
        }
*/
    }

    /// <summary>
    /// Implementing iterators: 
    /// http://msdn.microsoft.com/en-us/magazine/cc163682.aspx
    /// http://www.codeproject.com/Articles/34352/Tree-Iterators
    /// TODO:
    /// - study how to use yield in iterators
    /// - study how to use nested classes for iterators
    /// - implement many different kinds of iterators: depth-first, bredth-first, leafs-only etc.
    /// </summary>
    public class DepthDimEnumerator : DimEnumerator
    {
        private DimensionType _dimType;

        public DepthDimEnumerator(Set set, DimensionType dimType)
            : base(set) 
        {
            _dimType = dimType;
        }

        public override bool MoveNext()
        {
            if (!GreaterSet.IsPrimitive)
            {
                bool foundPrimitive = MoveUp();
                if (foundPrimitive) return true;
                // Else move down back
            }

            // Go down (return back) until we find the next (unvisited) child
            bool foundChild = MoveDown();
            if (!foundChild) return false;

            return MoveUp();
        }
        private bool MoveUp() // true - found primitive, false - found non-primitive leaf
        {
            while (!GreaterSet.IsPrimitive) // Not a leaf - go up deeper and search for the first primitive set
            {
                List<Dim> dims = GetChildren();

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
        private bool MoveDown() // true - found next child that can be continued up, false - not found (end)
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

                List<Dim> children = GetChildren();

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

        private List<Dim> GetChildren()
        {
            switch (_dimType)
            {
                case DimensionType.IDENTITY: return GreaterSet.GetIdentityDims();
                case DimensionType.ENTITY: return GreaterSet.GetEntityDims();
                case DimensionType.IDENTITY_ENTITY: return GreaterSet.GreaterDims;
            }
            return null;
        }
    }

    public enum DimensionType
    {
        INCLUSION, // Both super and sub
        SUPER, // 
        SUB, // 

        POSET, // Both greater and lesser
        GREATER, // 
        LESSER, // 

        IDENTITY_ENTITY, // Both identity and entity
        IDENTITY, //
        ENTITY, // 

        EXPORT,
    }

    public enum DimensionDirection
    {
        GREATER, // Up
        LESSER, // Down, reverse
    }

}
