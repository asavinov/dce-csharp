using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// A description of a set which may have subsets, geater or lesser sets, and instances. 
    /// A set is characterized by its structure which includes subsets, greater and lesser sets. 
    /// A set is also characterized by width and length of its members. 
    /// And a set provides methods for manipulating its structure and intances. 
    /// </summary>
    public class Set
    {
        private static int uniqueId; // To implement unique automatic ids

        /// <summary>
        /// Unique set id (in this database) . In C++, this Id field would be used as a reference filed
        /// </summary>
        private int _id;
        public int Id { get { return _id; } }

        /// <summary>
        /// A set name. Note that in the general case a set has an associated structure (concept, type) which may have its own name. 
        /// </summary>
        protected string _name;
        public string Name { get { return _name; } }

        /// <summary>
        /// Whether this set is supposed (able) to have instances. Some sets are used for conceptual purposes. 
        /// It is not about having zero instances - it is about the ability to have instances (essentially supporting the corresponding interface for working with instances).
        /// This flag is true for extensions which implement data-related methods (and in this sense it is reduntant because duplicates 'instance of').
        /// </summary>
        protected bool _instantiable;
        public bool Instantiable { get { return _instantiable; } }

        #region Schema methods. Inclusion (subset) hierarchy.

        /// <summary>
        /// Returns a set where this element is a subset. 
        /// </summary>
        private Dimension _superDim;
        public Dimension SuperDim
        {
            get { return _superDim; }
            set
            {
                if (value == null) // Request to remove the current super-dimension
                {
                    if (_superDim != null)
                    {
                        _superDim.GreaterSet._subDims.Remove(_superDim);
                        _superDim = null;
                    }
                    return;
                }

                if (_superDim != null) // Remove the current super-dimension if present
                {
                    this.SuperDim = null;
                }

                if (value.LesserSet != this || value.GreaterSet == null)
                {
                    // ERROR: Dimension greater and lesser sets must be set correctly
                }

                // Add new dimension
                _superDim = value;
                _superDim.GreaterSet._subDims.Add(_superDim);
            }
        }

        private List<Dimension> _subDims = new List<Dimension>();
        public List<Dimension> SubDims { get { return _subDims; } }

        public List<Set> SubSets
        {
            get { return _subDims.Select(x => x.LesserSet).ToList(); }
        }

        public Set FindSubset(string name)
        {
            Set set = null;
            if (_name == name)
            {
                set = this;
            }

            foreach (Dimension d in _subDims)
            {
                if (set != null) break;
                set = d.LesserSet.FindSubset(name);
            }

            return set;
        }

        #endregion

        #region Schema methods. Dimension structure.

        /// <summary>
        /// These are domain-specific dimensions. 
        /// </summary>
        public List<Dimension> _greaterDimensions = new List<Dimension>();
        public List<Dimension> GreaterDimensions
        {
            get { return _greaterDimensions; }
            set { _greaterDimensions = value; }
        }
        public void AddGreaterDimension(Dimension dim)
        {
            RemoveGreaterDimension(dim);
            dim.GreaterSet._lesserDimensions.Add(dim);
            dim.LesserSet._greaterDimensions.Add(dim);
        }
        public void RemoveGreaterDimension(Dimension dim)
        {
            dim.GreaterSet._lesserDimensions.Remove(dim);
            dim.LesserSet._greaterDimensions.Remove(dim);
        }
        public void RemoveGreaterDimension(string name)
        {
            Dimension dim = GetGreaterDimension(name);
            if (dim != null)
            {
                RemoveGreaterDimension(dim);
            }
        }
        public Dimension GetGreaterDimension(string name)
        {
            return _greaterDimensions.FirstOrDefault(d => d.Name == name);
        }
        public List<Set> GetGreaterSets()
        {
            return _greaterDimensions.Select(x => x.GreaterSet).ToList();
        }

        public List<Dimension> _lesserDimensions = new List<Dimension>();
        public List<Dimension> LesserDimensions
        {
            get { return _lesserDimensions; }
            set { _lesserDimensions = value; }
        }
        public List<Set> GetLesserSets()
        {
            return _lesserDimensions.Select(x => x.LesserSet).ToList();
        }

        #endregion

        #region Set characteristics

        /// <summary>
        /// Size of values or cells (physical identities) in bytes.
        /// </summary>
        public virtual int InstanceSize
        {
            get { return sizeof(int); }
        }

        /// <summary>
        /// How many _instances this set has. Cardinality. Set power. Length (height) of instance set.
        /// If instances are identified by integer offsets, then size also represents offset range.
        /// </summary>
        protected int _instanceCount;
        public virtual int InstanceCount
        {
            get { return _instanceCount; }
        }

        #endregion

        #region Instance manipulation (function) methods

        // TODO: Here we need an interface like ResultSet in JDBC with all possible types

        public void AddInstance()
        {
            InsertInstance(InstanceCount);
        }
        public void InsertInstance(int offset)
        {
            _instanceCount++;
            // TODO: Append all dimensions in loop including super-dim and special dims
        }
        public void RemoveInstance(int offset)
        {
            _instanceCount--;
            // TODO: Remove it from all dimensions in loop including super-dim and special dims
        }
        public void SetInstance(Dimension dimension, int offset, int value)
        {
            // TODO: Insert integer. Delegate to the dimension.
        }
        public void SetInstance(DimPrimitive<double> dimension, int offset, double value)
        {
            // TODO: Insert double. Delegate to the dimension.
        }

        #endregion

        #region Constructors and initializers.

        public Set(string name)
        {
            _id = uniqueId++;
            _name = name;

            // TODO: Parameterize depending on the reserved names: Integer, Double etc. (or exclude these names)
            _instantiable = true;
        }

        #endregion
    }
}
