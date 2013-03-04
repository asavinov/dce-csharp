using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        private static int uniqueId;

        /// <summary>
        /// Unique id within this database or (temporary) query session.  
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        private string _name;
        public string Name { get { return _name; } }

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
        public bool Instantiable { get { return _lesserSet.Instantiable; } private set { _instantiable = value; } }

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

        #region Schema methods.

        /// <summary>
        /// Greater (output) set.
        /// </summary>
        private Set _greaterSet;
        public Set GreaterSet
        {
            get { return _greaterSet; }
            set 
            {
                _greaterSet = value; // TODO: Update involved sets. Update this dim instances.
            }
        }

        /// <summary>
        /// Lesser (input) set. 
        /// </summary>
        private Set _lesserSet;
        public Set LesserSet
        {
            get { return _lesserSet; }
            set 
            { 
                _lesserSet = value; // TODO: Update involved sets. Update this dim instances.
            }
        }

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
        /// Child dimensions. 
        /// The child dimensions represent a continuation of this dimension along all dimensions of the greater set, that is, they point to one step higher. 
        /// </summary>
        private List<Dimension> _childDimensions;
        public List<Dimension> ChildDimensions
        {
            get { return _childDimensions; }
            set
            {
                _childDimensions = value; // TODO: Update all influenced elements.
            }
        }
        public List<Dimension> GetLeafDimensions()
        {
            List<Dimension> result = new List<Dimension>();
            if(ChildDimensions.Count == 0)
            {
                result.Add(this);
                return result;
            }

            foreach (Dimension child in ChildDimensions)
            {
                result.AddRange(child.GetLeafDimensions());
            }

            return result;
        }

        #endregion

        #region Properties of the function

        public virtual int Width // Width of instances. It depends on the implementation (and might not be the same for all dimensions of the greater set). 
        {
            get { return _greaterSet != null ? _greaterSet.Width : 0; }
        }

        protected int _length;
        public virtual int Length // How many instances. It is the same for all dimensions of the lesser set. 
        {
            get { return _length; }
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
        public Expression RemoteSelect { get; set; }
        public string RemoteName { get; set; } // It is for simplicity. It is a remote column/attribute/alias mapped to this dimension path.
        public string RemoteDefinition { get; set; } // It is a definition of the remote column/attribute/alias like "col1+col2/3"

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
            _name = name;

            Identity = isIdentity;
            Reversed = isReversed;
            Instantiable = isInstantiable;

            LesserSet = lesserSet;
            GreaterSet = greaterSet;

            // Parameterize depending on the reserved names: super
            // Parameterize depending on the greater and lesser set type. For example, dimension type must correspond to its greater set type (SetInteger <- DimInteger etc.)
        }

        #endregion

    }
}
