using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

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
        public string Name { get; set; }

        /// <summary>
        /// Whether this set is supposed (able) to have instances. Some sets are used for conceptual purposes. 
        /// It is not about having zero instances - it is about the ability to have instances (essentially supporting the corresponding interface for working with instances).
        /// This flag is true for extensions which implement data-related methods (and in this sense it is reduntant because duplicates 'instance of').
        /// </summary>
        public bool Instantiable { get; set; }

        /// <summary>
        /// Whether it is a primitive set. Primitive sets do not have greater dimensions.
        /// It can depend on other propoerties (it should be clarified) like instantiable, autopopulated, virtual etc.
        /// </summary>
        public bool Primitive { get; set; }

        /// <summary>
        /// If this set generates all possible elements (satisfying the constraints). 
        /// </summary>
        public bool AutoPopulatedSet { get; set; }

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
                while (root.SuperSet != null)
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

        public List<Set> GetAllSubsets()
        {
            int count = SubSets.Count;
            List<Set> result = new List<Set>(SubSets);
            for (int i = 0; i < count; i++)
            {
                List<Set> subsets = result[i].GetAllSubsets();
                if (subsets == null ||subsets.Count == 0)
                {
                    continue;
                }
                result.AddRange(subsets);
            }

            return result;
        }

        public Set FindSubset(string name)
        {
            Set set = null;
            if (Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
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
            return _greaterDimensions.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public List<Set> GetGreaterSets()
        {
            return _greaterDimensions.Select(x => x.GreaterSet).ToList();
        }
        public List<Dimension> GetGreaterLeafDimensions()
        {
            List<Dimension> leaves = new List<Dimension>();
            foreach(Dimension dim in GreaterDimensions)
            {
                leaves.AddRange(dim.GetLeafDimensions());
            }
            return leaves;
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

        public virtual void Append(Instance instance)
        {
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

        #endregion

        #region Remoting and population

        /// <summary>
        /// Constraints on all possible instances. Only instances satisfying these constraints will be created. 
        /// </summary>
        public Expression WhereExpression { get; set; }

        /// <summary>
        /// Ordering of the generated instances. Instances will be sorted according to these criteria. 
        /// </summary>
        public Expression OrderbyExpression { get; set; }

        /// <summary>
        /// The source of instances for this set. It can be a remote set, user input or explicit definition of the set like {1, 7, 4}.
        /// If it is empty then this set is defined purely locally via its greater dimensions and all possible elements will be instantiated.
        /// If it is not empty then only a subset of all possible values will be instantiated. 
        /// </summary>
        public Expression Instances { get; set; }
        public Set RemoteSet { get; set; } // For simplicity

        public virtual void Import(DataTable dataTable)
        {
            foreach (DataRow raw in dataTable.Rows)
            {
                // Use mappings to produce a complex instance from the current raw
                Instance instance=null;
                // Check if this instance satisfies the local expressions (local where etc.). Is it really possible and necessary?
                // Append the new instance to the set
                Append(instance);
            }
        }

        public virtual DataTable Export()
        {
            // Root represents a database engine and it knows how to access data (from local dimensions, from remote db, from file etc.)
            return Root.Export(this);
        }

        public virtual void Populate()
        {
            if (RemoteSet == null) // Local population procedure without importing (without external extensional)
            {
            }
            else // Popoulating using externally provided values
            {
                Set remoteSet = RemoteSet; // Find remote set
                DataTable export = remoteSet.Export(); // Request a (flat) result set from the remote set
                Import(export); // Import

            }
        }

        public virtual void Unpopulate() // Clean, Empty
        {
            // After this operation the set is empty
        }

        #endregion

        #region Constructors and initializers.

        public Set(string name)
        {
            _id = uniqueId++;
            Name = name;

            // TODO: Parameterize depending on the reserved names: Integer, Double etc. (or exclude these names)
            Instantiable = true;
            Primitive = false;
        }

        #endregion

        #region Deprecedate (delete)

        /// <summary>
        /// Attribute is a named primitive path leading from this set to primitive set along dimensions.
        /// </summary>
        private List<Attribute> _attributes = new List<Attribute>();
        public List<Attribute> Attributes
        {
            get { return _attributes; }
            set { _attributes = value; }
        }

        public void UpdateDimensions() // Use attribute structure to create/update dimension structure
        {
            // It is assumed that the attribute structrue is initialized and is correct

            Dimension dim;
            Set greaterSet;
            // Process FK information for generating greater dimensions
            foreach (Attribute att in Attributes)
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
                else // FK found - complex dimension
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

                // Process PK information for determing identity dimensions
                if (!String.IsNullOrEmpty(att.PkName))
                {
                    dim.Identity = true;
                }

                // Update attribute path adding the first segment referencing this new dimension
                att.Path.Clear();
                att.Path.Add(dim);

            }

            // ??? how and where ???
            // Generate complete paths for all attributes. If the first segment points to a non-primitive set then find continuation
            // It requires that all greater sets have finsihed this procedure (processed fks and pks)
            // So this method must be called in top down direction so that greater sets are processed before lesser sets.
        }

        public void UpdateAttributes() // Use dimension structure to create/update attribute structure
        {
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
