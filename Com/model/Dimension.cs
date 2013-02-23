using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
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
        private int _id;
        public int Id { get { return _id; } }

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        private string _name;
        public string Name { get { return _name; } }

        /// <summary>
        /// Is identity dimension.
        /// </summary>
        private bool _identity;
        public bool Identity { get { return _identity; } }

        /// <summary>
        /// Reversed dimension has the opposite semantic interpretation (direction). It is used to resolve semantic cycles. 
        /// For example, when a department references its manager then this dimension is makred by this flag. 
        /// One use is when deciding +how to interpret input and output dimensions of sets and lesser/greater sets of dimensions.
        /// </summary>
        private bool _reversed;
        public bool Reversed { get { return _reversed; } }

        /// <summary>
        /// Whether this dimension is supposed (able) to have instances. Some dimensions are used for conceptual purposes. 
        /// It is not about having zero instances - it is about the ability to have instances (essentially supporting the corresponding interface for working with instances).
        /// This flag is true for extensions which implement data-related methods (and in this sense it is reduntant because duplicates 'instance of').
        /// Different interpretations: the power of the domain can increase; the power of the domain is not 0; 
        /// </summary>
        private bool _instantiable;
        public bool Instantiable { get { return _lesserSet.Instantiable; } }

        /// <summary>
        /// Whether this dimension to take no values.
        /// </summary>
        private bool _nullable;
        public bool Nullable { get { return _nullable; } }

        /// <summary>
        /// Temporary dimension is discarded after it has been used for computing other dimensions.
        /// It is normally invisible (private) dimension. 
        /// It can be created in the scope of some other dimension, expression or query, and then it is automatically deleted when the process exits this scope.
        /// </summary>
        private bool _temporary;
        public bool Temporary { get { return _temporary; } }

        /// <summary>
        /// Here we store an intensional (computable) definition of the function. 
        /// It represents a mapping from input set to output set in terms of other already defined functions (local or remote).
        /// Note that it does not define sorting (ORDER BY), filter predicate (WHERE), or composition (FROM) - it only defines mapping and can be evaluated for one input value.
        /// </summary>
        public Expression FunctionExpression { get; set; }

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
            _id = uniqueId++;
            _name = name;

            _identity = isIdentity;
            _reversed = isReversed;
            _instantiable = isInstantiable;

            LesserSet = lesserSet;
            GreaterSet = greaterSet;

            // Parameterize depending on the reserved names: super
            // Parameterize depending on the greater and lesser set type. For example, dimension type must correspond to its greater set type (SetInteger <- DimInteger etc.)
        }

        #endregion

    }
}
