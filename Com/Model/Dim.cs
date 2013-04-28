using System;
using System.Collections;
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
    public class Dim : IEnumerable
    {
        #region Properties

        private static int uniqueId;

        /// <summary>
        /// Unique id within this database or (temporary) query session.  
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        public string Name { get; set; }

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
        public List<Dim> Path { get; private set; }

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
        /// Convert path definition (composition) to a canonical representation as a sequence of simple dimensions without complex constituents.
        /// </summary>
        public void ExpandPath()
        {
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

            if (GreaterSet.IdentityPrimitiveArity == 1) // For 1-column FK, dimensino name is the only column name instead of fkName (fkName is not used).
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

        public virtual void Append(object value) { }

        public virtual void Insert(int offset, object value) { }

        public virtual object GetValue(int offset) { return null; }

        public virtual void SetValue(int offset, object value) { }

        public virtual int[] GetOffsets(object value) { return null; }

        /// <summary>
        /// It is a convenience property used to import/export data and other purposes. 
        /// </summary>
        public virtual object CurrentValue { get; set; }

        /// <summary>
        /// It is used to compose a remote query for loading data during population and then interpret the result set by mapping to local terms. 
        /// </summary>
        public Expression SelectExpression { get; set; }
        public string SelectDefinition { get; set; } // It is a definition of the remote column/attribute/alias like "col1+col2/3"

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator)new DepthDimEnumerator(this);
        }

        #endregion

        #region Overriding System.Object

        public override string ToString()
        {
            return String.Format("{0} {1}, ID: {2}", Name, 0, Id);
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
            Id = uniqueId++;
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

        public Dim(string name, Dim sourceDim)
            : this(name, sourceDim.LesserSet, sourceDim.GreaterSet)
        {
            // It will be a clone of the source dimension (the same function)
            SelectExpression = null;
            SelectDefinition = sourceDim.Name;
        }

        #endregion

    }

    public abstract class DimEnumerator : IEnumerable
    {
        public List<Dim> _Path = new List<Dim>();

       public DimEnumerator(Dim tree)
        {
            _Path.Add(tree);
        }

        // Get the explicit current node.
        public Dim Current { get { return _Path[_Path.Count - 1]; } }

/*
        // Get the implicit current node.
        object System.Collections.IEnumerator.Current
        {
            get { return _Path[_Path.Count - 1]; }
        }
*/
 
        // Increment the iterator and moves the current node to the next one
        public abstract bool MoveNext();

        // Dispose the object.
        public void Dispose()
        {
            _Path.Clear();
        }

        // Reset the iterator.
        public void Reset()
        {
            Dim segment = _Path[0];
            _Path.Clear();
            _Path.Add(segment);
        }

        // Get the underlying enumerator.
        public virtual IEnumerator GetEnumerator()
        {
            return (IEnumerator < Dim >)this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            // this calls the IEnumerator<Foo> GetEnumerator method
            // as explicit method implementations aren't used for method resolution in C#
            // polymorphism (IEnumerator<T> implements IEnumerator)
            // ensures this is type-safe
            return GetEnumerator();
        }
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
        public DepthDimEnumerator(Dim tree)
            : base(tree) { }

        public override bool MoveNext()
        {
            if (!Current.GreaterSet.IsPrimitive) // Not a leaf - go deeper
            {
                _Path.Add(Current.GreaterSet.GreaterDims[0]);
                return true;
            }

            // Return back until we find the next child
            Dim child = null; 
            do
            {
                child = Current;
                _Path.RemoveAt(_Path.Count - 1);
                if (_Path.Count == 0) // End. It was the last element.
                {
                    return false;
                }
                List<Dim> children = Current.GreaterSet.GetIdentityDims();
                int childIndex = children.IndexOf(child);
                if (childIndex + 1 < children.Count) // Use this next child
                {
                    child = children[childIndex + 1];
                    _Path.Add(child);
                    return true;
                }
                else // No next child
                {
                    child = null;
                }
            } while (child == null);

            return true;
        }
    }

}
