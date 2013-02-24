using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
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

        /// <summary>
        /// If this set generates all possible elements (satisfying the constraints). 
        /// </summary>
        protected bool _autoPopulatedSet;
        public bool IsAutoPopulatedSet { get { return _autoPopulatedSet; } }

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

        public Set SuperSet
        {
            get { return _superDim != null ? _superDim.GreaterSet : null; }
        }

        public SetRoot Root
        {
            get
            {
                Set root = this;
                while (this != null)
                {
                    root = root.SuperSet;
                }

                return (SetRoot)root;
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

        public List<Set> GetSubsets()
        {
            if(SubSets.Count == 0) 
            {
                return null;
            }

            List<Set> sets = new List<Set>();
            int count = sets.Count;
            for(int i=0; i<count; i++)
            {
                List<Set> subsets = sets[i].GetSubsets();
                if (subsets == null)
                {
                    continue;
                }
                sets.AddRange(subsets);
            }

            return sets;
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

        public virtual Dimension CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            return new DimSet(name, lesserSet, this);
        }

        /// <summary>
        /// Attribute is a named primitive path leading from this set to primitive set along dimensions.
        /// </summary>
        public List<Attribute> Attributes { get; set; }

        public void UpdateDimensions() // Use attribute structure to create/update dimension structure
        {
            // It is assumed that the attribute structrue is initialized and is correct

            Dimension dim;
            Set greaterSet;
            foreach(Attribute att in Attributes) 
            {
                if (String.IsNullOrEmpty(att.FkName)) // No FK - primitive dimension
                {
                    // Get primitive set corresonding to this data type (overwritten by root set and specific to the data source). For example, it can return SetDouble
                    greaterSet = Root.GetPrimitiveSet(att);
                    // Create a new primitive dimension
                    dim = greaterSet.CreateDefaultLesserDimension(att.FkName, this); // The primitive set knows what dimension type to use (by default), say, DimPrimitive<double> (overwritten)
                    // Set dimension properties
                    this.AddGreaterDimension(dim); // Add the new dimension to the schema
                }
                else // Complex dimension
                {
                    // Check if a dimension with this FK-name already exists
                    dim = GetGreaterDimension(att.FkName);
                    if (dim == null)
                    {
                        // Find greater set using FK target table name
                        greaterSet = Root.FindSubset(att.FkTargetTableName);
                        // Create a new complex dimension
                        dim = greaterSet.CreateDefaultLesserDimension(att.FkName, this);// Say, DimSet
                        // Set dimension properties
                        this.AddGreaterDimension(dim); // Add the new dimension to the schema
                    }
                    else
                    {
                        // Update existing complex dimension (or check consistency of this attribute with this dimension)
                    }
                }
                // Update attribute path adding the first segment referencing this new dimension
            }

            // Generate complex dimensions from primitive attributes (it should belong to Set if Attribute is independent of data source)
            // For each FK create one dimension referencing a target set. For each FK attribute add the first segment as this dimension. 
            // For non-FK attributes create primitive dimension and add a single segment in the attribute path

            // Generate complete paths for all attributes. If the first segment points to a non-primitive set then find continuation

            // First, define PKs
        }

        public void UpdateAttributes() // Use dimension structure to create/update attribute structure
        {
        }

        #endregion

        #region Set characteristics

        /// <summary>
        /// How many _instances this set has. Cardinality. Set power. Length (height) of instance set.
        /// If instances are identified by integer offsets, then size also represents offset range.
        /// </summary>
        protected int _length;
        public virtual int Length
        {
            get { return _length; }
        }

        /// <summary>
        /// Size of values or cells (physical identities) in bytes.
        /// </summary>
        public virtual int Width
        {
            get { return sizeof(int); }
        }

        #endregion

        #region Instance manipulation (function) methods

        // TODO: Here we need an interface like ResultSet in JDBC with all possible types
		// Alternative: maybe define these methos in the SetRemote class where we will have one class for manually entering elements

        public virtual void Append() // An overloaded method could take an array/list/map of values - check how TableSet works
        {
            // Delegate to all dimensions
            foreach(Dimension d in _greaterDimensions)
            {
                d.Append(0); // Append default value for this dimension
            }

            _length++;
        }

        public virtual void Insert(int offset)
        {
            // Delegate to all dimensions
            foreach(Dimension d in _greaterDimensions)
            {
                d.Insert(offset, 0);
            }

            _length++;
        }

        public virtual void Remove(int offset)
        {
            _length--;
            // TODO: Remove it from all dimensions in loop including super-dim and special dims
            // PROBLEM: should we propagate this removal to all lesser dimensions? We need a flag for this property. 
        }

        public virtual object GetValue(string name, int offset)
        {
            Dimension dim = GetGreaterDimension(name);
            return dim.GetValue(offset);
        }

        public virtual void SetValue(string name, int offset, object value)
        {
            Dimension dim = GetGreaterDimension(name);
            dim.SetValue(offset, value);
        }

        public virtual void Populate()
        {
            // Generate the set by instantiating all its elements. They way of population depends on the set properties (importing, autogeneration etc.)
            if (Root.DataSourceType != DataSourceType.LOCAL)
            {
                // Load and then iterate by appending values to dimensions depending the dimension properties and expression
            }

            // Iterate through all created elements in the set and compute all locally defined dimensions in appropriate sequence determined by their dependency graph
        }

        public virtual void Unpopulate()
        {
            // Free all elements if they were stored somewhere (cache, dimensions etc.)
            if (Root.DataSourceType != DataSourceType.LOCAL)
            {
                // Load and then iterate by appending values to dimensions depending the dimension properties and expression
            }

            // Iterate through all created elements in the set and compute all locally defined dimensions in appropriate sequence determined by their dependency graph
        }

        #endregion

        #region Expressions: WHERE (filter), ORDER BY (sorting), WHERE (composition) 
        /// <summary>
        /// Logical expression which is used to select a subset of element during auto-population. 
        /// </summary>
        public Expression WhereExpression  { get; set; }

        /// <summary>
        /// Two-place predicate for comparison of values or a complex type where primitive types use natural ordering. 
        /// </summary>
        public Expression OrderbyExpression  { get; set; }
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

    public enum DataSourceType
    {
        LOCAL, // This database
        CSV,
        SQL, // Generic (standard) SQL
        EXCEL
    }
}
