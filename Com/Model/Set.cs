using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

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
        #region Properties

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

        #endregion

        #region Simple dimensions

        public List<Dimension> SuperDims { get; private set; }
        public List<Dimension> SubDims { get; private set; }
        public List<Dimension> GreaterDims { get; private set; }
        public List<Dimension> LesserDims { get; private set; }

        public List<Dimension> GetDimensions(DimensionType dimType, DimensionDirection dimDirection) // Selector of field depending on parameters
        {
            List<Dimension> result = null;

            if (dimDirection == DimensionDirection.GREATER)
            {
                if (dimType == DimensionType.POSET)
                {
                    result = GreaterDims;
                }
                else if (dimType == DimensionType.INCLUSION)
                {
                    result = SuperDims;
                }
            }
            else if (dimDirection == DimensionDirection.LESSER)
            {
                if (dimType == DimensionType.POSET)
                {
                    result = LesserDims;
                }
                else if (dimType == DimensionType.INCLUSION)
                {
                    result = SubDims;
                }
            }

            return result;
        }

        #region Inclusion. Super.

        public Dimension SuperDim
        {
            get
            {
                Debug.Assert(SuperDims != null && SuperDims.Count < 2, "Wrong use: more than one super dimension.");
                return SuperDims.Count == 0 ? null : SuperDims[0];
            }
            set
            {
                Dimension dim = SuperDim;
                if (value == null) // Remove the current super-dimension
                {
                    if (dim != null)
                    {
                        SuperDims.Remove(dim);
                        dim.GreaterSet.SubDims.Remove(dim);
                    }
                    return;
                }

                if (dim != null) // Remove the current super-dimension if present before setting a new one (using this same method)
                {
                    SuperDim = null;
                }

                Debug.Assert(value.LesserSet == this && value.GreaterSet != null, "Wrong use: dimension greater and lesser sets must be set correctly.");

                if (false) // TODO: Check value.GreaterSet for validity - we cannot break poset relation
                {
                    // ERROR: poset relation is broken
                }

                // Really add new dimension
                SuperDims.Add(value);
                value.GreaterSet.SubDims.Add(value);
            }
        }

        public Set SuperSet
        {
            get { return SuperDim != null ? SuperDim.GreaterSet : null; }
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

        #endregion

        #region Inclusion. Sub.

        public List<Set> SubSets
        {
            get { return SubDims.Select(x => x.LesserSet).ToList(); }
        }

        public List<Set> GetAllSubsets()
        {
            int count = SubSets.Count;
            List<Set> result = new List<Set>(SubSets);
            for (int i = 0; i < count; i++)
            {
                List<Set> subsets = result[i].GetAllSubsets();
                if (subsets == null || subsets.Count == 0)
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

            foreach (Dimension d in SubDims)
            {
                if (set != null) break;
                set = d.LesserSet.FindSubset(name);
            }

            return set;
        }

        public List<Set> PrimitiveSubsets
        {
            get { return SubDims.Where(x => x.LesserSet.Primitive).Select(x => x.LesserSet).ToList(); }
        }

        public List<Set> NonPrimitiveSubsets
        {
            get { return SubDims.Where(x => !x.LesserSet.Primitive).Select(x => x.LesserSet).ToList(); }
        }

        public Set GetPrimitiveSubset(string name)
        {
            Dimension dim = SubDims.FirstOrDefault(x => x.LesserSet.Primitive && x.LesserSet.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            return dim != null ? dim.LesserSet : null;
        }

        #endregion

        #region Poset. Greater.

        public int IdentityArity
        {
            get
            {
                return GreaterDims.Count(x => x.Identity);
            }
        }

        public int IdentityPrimitiveArity
        {
            get // It is computed recursively - we sum up greater set arities of all our identity dimensions up to the prmitive sets with arity 1
            {
                if (Primitive) return 1;
                return GreaterDims.Where(x => x.Identity).Sum(x => x.GreaterSet.IdentityPrimitiveArity);
            }
        }

        public List<Dimension> GetGreaterLeafDimensions()
        {
            List<Dimension> leaves = new List<Dimension>();
            foreach (Dimension dim in GreaterDims)
            {
                //                leaves.AddRange(dim.GetLeafDimensions());
            }
            return leaves;
        }

        public void AddGreaterDimension(Dimension dim)
        {
            RemoveGreaterDimension(dim);
            // TODO: propagate addition of new dimension by updating higher rank dimensions and other parameters
            dim.GreaterSet.LesserDims.Add(dim);
            dim.LesserSet.GreaterDims.Add(dim);
        }
        public void RemoveGreaterDimension(Dimension dim)
        {
            // TODO: propagate removal of the dimension by updating higher rank dimensions and other parameters
            dim.GreaterSet.LesserDims.Remove(dim);
            dim.LesserSet.GreaterDims.Remove(dim);
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
            return GreaterDims.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public List<Set> GetGreaterSets()
        {
            return GreaterDims.Select(x => x.GreaterSet).ToList();
        }

        #endregion

        #region Poset. Lesser.

        public List<Set> GetLesserSets()
        {
            return LesserDims.Select(x => x.LesserSet).ToList();
        }

        #endregion

        #endregion

        #region Complex dimensions (paths)

        public List<Dimension> SuperPaths { get; private set; }
        public List<Dimension> SubPaths { get; private set; }
        public List<Dimension> GreaterPaths { get; private set; }
        public List<Dimension> LesserPaths { get; private set; }

        public void AddGreaterPath(Dimension path)
        {
            RemoveGreaterPath(path);
            path.GreaterSet.LesserPaths.Add(path);
            path.LesserSet.GreaterPaths.Add(path);
        }
        public void RemoveGreaterPath(Dimension path)
        {
            path.GreaterSet.LesserPaths.Remove(path);
            path.LesserSet.GreaterPaths.Remove(path);
        }
        public void RemoveGreaterPath(string name)
        {
            Dimension path = GetGreaterPath(name);
            if (path != null)
            {
                RemoveGreaterPath(path);
            }
        }
        public Dimension GetGreaterPath(string name)
        {
            return GreaterPaths.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        #endregion

        #region Deprecated dimension strucutre (replaced by DimensionManager)
        /*
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
*/
        #endregion 

        #region Instance manipulation (function) methods

        // TODO: Here we need an interface like ResultSet in JDBC with all possible types
		// Alternative: maybe define these methos in the SetRemote class where we will have one class for manually entering elements

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

        public virtual void Append() // An overloaded method could take an array/list/map of values - check how TableSet works
        {
            // Delegate to all dimensions
            foreach(Dimension d in GreaterDims)
            {
                d.Append(0); // Append default value for this dimension
            }

            _length++;
        }

        public virtual void Append(Instance instance)
        {
            // How to append (quickly)?
            // - 1. to only leaf dimensions 2. propagate automatically to root immediately (delete leaves optionally) 3. propagate later for all (optionally delete leaves)
            // - we always start from leaves (assumption for this method - generalizations can be implemented in future versions)
            // - elementary task: find/append a parent dim given its child dim values
            // - this elementary task is executed recursively (if children a not processed yet then process them recursively by finding/appending their value)
/*
            foreach(dim in GreaterDimensions) // Iterate either through greater dims (by finding child instances) or by child instances (by finding greater dims)
            {
                ChildInstace = instance.ChildInstances[dim]; // Mapping: greaterDim -> childInstance
                dim.Append(ChildInstace); // Recursion
            }
            Append(directGreaterIds[]); // Append given direct greater values (computed above). Non-recursive - all values are known.
*/

            // - we can reduce it to the same dimension method dim.Append() 
            // This method is applied to a parent dimension (of anxy level) and takes the corresponding node from the instance structure
            // Its task is to add/find an instance in this dimension given child instance values. After finishing it assigns it to the node value (so that parents can use it). Normally we return offsets.

            // We actualy do not need nested dimensions for that because values are inserted into normal dimensions along the paths. 
            // Nested dimensions are needed to represent and map paths. They also could be used as longer path indexes for performance.

            // Theoretically, we could simply insert new instances into leaf dimensions and forget. 
            // Then on the second step, we propagate leave dimensions into parent dimensions using the above recursieve procedure.
        }

        public virtual void Insert(int offset)
        {
            // Delegate to all dimensions
            foreach(Dimension d in GreaterDims)
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
        public Expression FromExpression { get; set; }
        public SetRoot FromDb { get; set; } // For simplicity. This remote set will export instances and this set will import them
        public string FromSetName { get; set; } // 

        public virtual void ImportDimensions()
        {
            // We assume that importing = cloning, that is, this set has the same structure as the remote set (at least identity).
            // Thus our task is to clone the structure by retaining the mapping. Cloning structure means reducing it to primitive elements. 
            // The structure can be cloned in two ways: by expanding dimensions, by cloning paths

            // Find from which set to import dimensions
            if (FromDb == null || String.IsNullOrEmpty(FromSetName))
            {
                return; // Nothing to import
            }
            Set srcSet = FromDb.FindSubset(FromSetName);
            if (srcSet == null)
            {
                return; // Nothing to import
            }

            foreach (Dimension srcPath in srcSet.GreaterPaths)
            {

                Dimension path = null; // TODO: We should try to find this path and create a new path only if it cannot be found. Or, if found, the existing path should be deleted along with all its segments.
                if (path == null)
                {
                    path = new Dimension(srcPath.Name, srcPath); // We also store a mapping (definition)
                }
                path.LesserSet = this;
                string columnType = Root.MapToLocalType(srcPath.GreaterSet.Name);
                Set gSet = Root.GetPrimitiveSubset(columnType);
                if (gSet == null)
                {
                    // ERROR: Cannot find the matching primitive set for the path
                }
                path.GreaterSet = gSet;

                Set lSet = this;
                foreach (Dimension srcDim in srcPath.Path)
                {
                    Dimension dim = null; // TODO: We should try to find this segment and create a new segment only if it does not exist. Or, if found, the original dimension should be deleted.
                    if (dim == null)
                    {
                        dim = new Dimension(srcDim.Name, srcDim); // We also store a mapping (definition)
                    }
                    dim.LesserSet = lSet;
                    if (srcDim.GreaterSet == srcPath.GreaterSet)
                    {
                        gSet = srcPath.GreaterSet; // Last segment has the same greater set as the path it belongs to
                    }
                    else // Try to find this greater set in our database
                    {
                        gSet = Root.FindSubset(srcDim.GreaterSet.Name);
                        if (gSet == null)
                        {
                            // ERROR: Automatic set matching failed. Manual mapping of sets is needed. 
                        }
                    }
                    dim.GreaterSet = gSet;

                    path.Path.Add(dim); // Add the new segment to the path

                    lSet = gSet; // Loop iteration
                }

                AddGreaterPath(path); // Add the new dimension to this set 
                foreach (Dimension dim in path.Path) // Add also all its segments
                {
                    dim.LesserSet.AddGreaterDimension(dim);
                }
            }
        }

        public virtual void Import(DataTable dataTable)
        {
            // Prepare a mapping for performance - we do not want to create it for each instance (associate columns in the raw with our (primitive) dimensions)
            // Mapping: columnIndex <-> dimensionIndex or dimensionReference (a leaf dimension)
            // Question: 1. we import all we get, 2. we import all we have 3. we import only identities

            Instance instance = null;
            foreach (DataRow row in dataTable.Rows)
            {
                // Use mappings to initialize complex instance from the current raw
                instance=null;

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
            if (FromSetName == null) // Local population procedure without importing (without external extensional)
            {
            }
            else // Popoulating using externally provided values
            {
                Set remoteSet = null; // Find remote set
                DataTable dataTable = remoteSet.Export(); // Request a (flat) result set from the remote set
                Import(dataTable); // Import
            }
        }

        public virtual void Unpopulate() // Clean, Empty
        {
            // After this operation the set is empty
        }

        #endregion

        #region Constructors and initializers.

        public virtual Dimension CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            return new DimSet(name, lesserSet, this);
        }

        public Set(string name)
        {
            _id = uniqueId++;
            Name = name;

            SuperDims = new List<Dimension>();
            SubDims = new List<Dimension>();
            GreaterDims = new List<Dimension>();
            LesserDims = new List<Dimension>();

            SuperPaths = new List<Dimension>();
            SubPaths = new List<Dimension>();
            GreaterPaths = new List<Dimension>();
            LesserPaths = new List<Dimension>();

            // TODO: Parameterize depending on the reserved names: Integer, Double etc. (or exclude these names)
            Instantiable = true;
            Primitive = false;
        }

        public Set(string name, Set sourceSet)
            : this(name)
        {
            // It will be a clone of the source set (the same structure, at least the same structure of identities)
            FromExpression = null;
            FromDb = sourceSet.Root; 
            FromSetName = sourceSet.Name; // The (remote) source of instances for populating this set
        }

        #endregion

        #region Deprecated (delete)
/*
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
                    AddGreaterDimension(dim); // Add the new dimension to the schema
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
                        AddGreaterDimension(dim); // Add the new dimension to the schema
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
*/
        #endregion

    }

    public enum DimensionType
    {
        INCLUSION,
        POSET,
    }

    public enum DimensionDirection
    {
        GREATER, // Up
        LESSER, // Down, reverse
    }

    public enum DataSourceType
    {
        LOCAL, // This database
        CSV,
        SQL, // Generic (standard) SQL
        EXCEL
    }
}
