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

        #region Instance (function) methods.

        public virtual int Count // How many instances. It is the same for all dimensions of the lesser set. 
        {
            get { return _lesserSet != null ? _lesserSet.InstanceCount : 0; }
        }

        public virtual int Size // Width of instances. It depends on the implementation (and might not be the same for all dimensions of the greater set). 
        {
            get { return _greaterSet != null ? _greaterSet.InstanceSize : 0; }
        }

        // TODO: Add such methods as getValueType() returing what the real class says and getValue overriden by the real class
        // These methods are convenient because we can check what kind of function work with

        /// <summary>
        /// A boolean dimension can be used as a selector and then we can specify a condition. 
        /// This condition can be then used to populate this dimension given some other dimension of the same type.  
        /// </summary>
//        private String predicate;

        /// <summary>
        /// Dimension can store default physical sorting of its elements (the order they are stored). 
        /// It is either primitive (local) ascending/descending/no or more complex query involving also other sets.
        /// </summary>
//        private String sorting;

        /**
         * On one hand, a function is able to map inputs to outputs but on the other hand it is simply a set of existing _instances identified by their references, that is, a domain.
         * A function is not only a domain (a set of existing _instances) but also the definition of the entity part for one or a few its properties.
         *
         * It is one possible implementation of a function. 
         * Input values are only integers (or long?).
         * Output values may have arbitrary size (number of bytes). This means that non-long output values can be used only in primitive domains.
         * Theoretically, output values could contain several values which is useful for row-store. 
         * Here we use physical value types only. The real domain-specific type of values is determined by the schema.
         * Any (edge) implementation must support both direct and reverse functions. However, it can be done using two representations/implementations or one (not efficient). Therefore, we have to distinguish between edge (dimension) implementation and lower-level storages and indexes.
         * 
         * Other implementations: SparseArray, CompressedArray
         * 
         * An array of values where index of the array is offset (input, reference) while cell stores the output of the function.
         * In the general case, it should be an interface with different implementations of the direct function. 
         */
        //	int[] _cells; // Implemented in extensions
        /**
         * It is a sorted array of _offsets (sort is by the value in cell at the offset).
         * Here is how to sort indexes: http://stackoverflow.com/questions/951848/java-array-sort-quick-way-to-get-a-sorted-list-of-indices-of-an-array
         * In the general case, it should be an interface with different implementations of the reverse function. 
         */
        //	int[] _offsets; // Implemented in extensions
        /**
         * Each value has some size as the number of bytes, for example, 4 bytes.
         * It is a parameter of each object which must be taken into account by the users of this object.
         * Since our implementations are made for each system type, the size is fixed. 
         */

        /**
         * If instances cannot be represented as objects with built-in functions (record-oriented representation), 
         * then the parent can return an object which (efficiently) implements the mapping for one function (column-oriented representation).
         * For example, it can be an in-memory array. Or it can be a key-value mapping  with an interface for getting output values for input values.
         * Important is only that it is efficient and is an independent representation.
         * The return object can be then used to get outputs given inputs according to its common interface. 
         * If fact, this method can be viewed as returning a special representation of some direct function.
         * 
         *  Currently not needed. Earlier we assumed that complex manipulations will be done by functions and reverse functions as special objects.
         */
        //	public Object getFunc(boolean isReversed) { return null; }

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
