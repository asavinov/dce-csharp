using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Com.Model
{
    /// <summary>
    /// An abstract dimension with storage methods but without implementation. 
    /// Concrete implementations of the storage depending on the value type are implemented in the extensions which have to used. 
    /// Extensions also can define functions defined via a formula or a query to an external database.
    /// It is only important that a function somehow impplements a mapping from its lesser set to its greater set. 
    /// </summary>
    public class Dimension
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
        public bool Identity { get; set; }

        /// <summary>
        /// Reversed dimension has the opposite semantic interpretation (direction). It is used to resolve semantic cycles. 
        /// For example, when a department references its manager then this dimension is makred by this flag. 
        /// One use is when deciding +how to interpret input and output dimensions of sets and lesser/greater sets of dimensions.
        /// </summary>
        public bool Reversed { get; set; }

        /// <summary>
        /// Whether this dimension is supposed (able) to have instances. Some dimensions are used for conceptual purposes. 
        /// It is not about having zero instances - it is about the ability to have instances (essentially supporting the corresponding interface for working with instances).
        /// This flag is true for extensions which implement data-related methods (and in this sense it is reduntant because duplicates 'instance of').
        /// Different interpretations: the power of the domain can increase; the power of the domain is not 0; 
        /// </summary>
        private bool _instantiable;
        public bool Instantiable { get { return LesserSet.Instantiable; } private set { _instantiable = value; } }

        /// <summary>
        /// Whether this dimension to take no values.
        /// </summary>
        public bool Nullable { get; set; }

        /// <summary>
        /// Temporary dimension is discarded after it has been used for computing other dimensions.
        /// It is normally invisible (private) dimension. 
        /// It can be created in the scope of some other dimension, expression or query, and then it is automatically deleted when the process exits this scope.
        /// </summary>
        public bool Temporary { get; set; }

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
        private Dimension _parentDimension;
        public Dimension ParentDimension
        {
            get { return _parentDimension; }
            set
            {
                _parentDimension = value; // TODO: Update all influenced elements.
            }
        }
        public Dimension Root
        {
            get
            {
                Dimension root = this;
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
        public List<Dimension> Path { get; private set; }

        public int Rank
        {
            get
            {
                if (Path == null || Path.Count == 0) return 1; // Simple dimension
                int r = 0;
                foreach (Dimension dim in Path)
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

        public bool StartsWith(List<Dimension> path)
        {
            if (Path.Count < path.Count) return false;
            for (int i = 0; i < path.Count; i++ )
            {
                if (path[i] != Path[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Convert path definition (composition) to a canonical representation as a sequence of simple dimensions without complex constituents.
        /// </summary>
        public void ExpandPath()
        {
            List<Dimension> allSegments = GetAllSegments();
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

        private List<Dimension> GetAllSegments()
        {
            if (Path == null) return null;
            List<Dimension> result = new List<Dimension>();
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

        public Dimension GetSegment(int rank)
        {
            Debug.Assert(rank >= 0, "Wrong use of method parameter. Rank cannot be negative.");
            return rank < Path.Count ? Path[rank] : null; // TODO: take into account the nested structure of complex dimensions
        }

        public void Concatenate(List<Dimension> path)
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

        /// <summary>
        /// It is used to compose a remote query for loading data during population and then interpret the result set by mapping to local terms. 
        /// </summary>
        public Expression SelectExpression { get; set; }
        public string SelectDefinition { get; set; } // It is a definition of the remote column/attribute/alias like "col1+col2/3"

        #endregion

        #region Constructors and initializers.

        public Dimension(string name)
            : this(name, null, null)
        {
        }

        public Dimension(string name, Set lesserSet, Set greaterSet)
            : this(name, lesserSet, greaterSet, false, false, true)
        {
        }

        public Dimension(string name, Set lesserSet, Set greaterSet, bool isIdentity, bool isReversed, bool isInstantiable)
        {
            Id = uniqueId++;
            Name = name;

            Identity = isIdentity;
            Reversed = isReversed;
            Instantiable = isInstantiable;

            Path = new List<Dimension>();

            LesserSet = lesserSet;
            GreaterSet = greaterSet;

            // Parameterize depending on the reserved names: super
            // Parameterize depending on the greater and lesser set type. For example, dimension type must correspond to its greater set type (SetInteger <- DimInteger etc.)
        }

        public Dimension(string name, Dimension sourceDim)
            : this(name, sourceDim.LesserSet, sourceDim.GreaterSet)
        {
            // It will be a clone of the source dimension (the same function)
            SelectExpression = null;
            SelectDefinition = sourceDim.Name;
        }

        #endregion

    }
}
