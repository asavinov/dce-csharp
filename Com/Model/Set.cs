using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

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
        public bool IsInstantiable { get; set; }

        /// <summary>
        /// Whether it is a primitive set. Primitive sets do not have greater dimensions.
        /// It can depend on other propoerties (it should be clarified) like instantiable, autopopulated, virtual etc.
        /// </summary>
        public bool IsPrimitive { get; set; }

        /// <summary>
        /// If this set generates all possible elements (satisfying the constraints). 
        /// </summary>
        public bool IsAutoPopulated { get; set; }

        #endregion

        #region Simple dimensions

        public List<Dim> SuperDims { get; private set; }
        public List<Dim> SubDims { get; private set; }
        public List<Dim> GreaterDims { get; private set; }
        public List<Dim> LesserDims { get; private set; }

        public List<Dim> GetDims(DimensionType dimType, DimensionDirection dimDirection) // Selector of field depending on parameters
        {
            List<Dim> result = null;

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

        public Dim SuperDim
        {
            get
            {
                Debug.Assert(SuperDims != null && SuperDims.Count < 2, "Wrong use: more than one super dimension.");
                return SuperDims.Count == 0 ? null : SuperDims[0];
            }
            set
            {
                Dim dim = SuperDim;
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

        public bool IsRoot
        {
            get { return SuperDim == null; }
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

            foreach (Dim d in SubDims)
            {
                if (set != null) break;
                set = d.LesserSet.FindSubset(name);
            }

            return set;
        }

        public List<Set> PrimitiveSubsets
        {
            get { return SubDims.Where(x => x.LesserSet.IsPrimitive).Select(x => x.LesserSet).ToList(); }
        }

        public List<Set> NonPrimitiveSubsets
        {
            get { return SubDims.Where(x => !x.LesserSet.IsPrimitive).Select(x => x.LesserSet).ToList(); }
        }

        public Set GetPrimitiveSubset(string name)
        {
            Dim dim = SubDims.FirstOrDefault(x => x.LesserSet.IsPrimitive && x.LesserSet.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            return dim != null ? dim.LesserSet : null;
        }

        #endregion

        #region Poset. Greater.

        public int IdentityArity
        {
            get
            {
                return GreaterDims.Count(x => x.IsIdentity);
            }
        }

        public int IdentityPrimitiveArity
        {
            get // It is computed recursively - we sum up greater set arities of all our identity dimensions up to the prmitive sets with arity 1
            {
                if (IsPrimitive) return 1;
                return GreaterDims.Where(x => x.IsIdentity).Sum(x => x.GreaterSet.IdentityPrimitiveArity);
            }
        }

        public List<Dim> GetGreaterLeafDims()
        {
            List<Dim> leaves = new List<Dim>();
            foreach (Dim dim in GreaterDims)
            {
                // leaves.AddRange(dim.GetLeafDimensions());
            }
            return leaves;
        }

        public void AddGreaterDim(Dim dim)
        {
            RemoveGreaterDim(dim);
            Debug.Assert(dim.GreaterSet != null && dim.LesserSet != null, "Wrong use: dimension must specify a lesser and greater sets before it can be added to a set.");
            // TODO: propagate addition of new dimension by updating higher rank dimensions and other parameters
            dim.GreaterSet.LesserDims.Add(dim);
            dim.LesserSet.GreaterDims.Add(dim);
        }
        public void RemoveGreaterDim(Dim dim)
        {
            // TODO: propagate removal of the dimension by updating higher rank dimensions and other parameters
            if (dim.GreaterSet != null) dim.GreaterSet.LesserDims.Remove(dim);
            if (dim.LesserSet != null) dim.LesserSet.GreaterDims.Remove(dim);
        }
        public void RemoveGreaterDim(string name)
        {
            Dim dim = GetGreaterDim(name);
            if (dim != null)
            {
                RemoveGreaterDim(dim);
            }
        }
        public Dim GetGreaterDim(string name)
        {
            return GreaterDims.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public List<Set> GetGreaterSets()
        {
            return GreaterDims.Select(x => x.GreaterSet).ToList();
        }
        public List<Dim> GetIdentityDims()
        {
            return GreaterDims.Where(x => x.IsIdentity).ToList();
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

        public List<Dim> SuperPaths { get; private set; }
        public List<Dim> SubPaths { get; private set; }
        public List<Dim> GreaterPaths { get; private set; }
        public List<Dim> LesserPaths { get; private set; }

        public void AddGreaterPath(Dim path)
        {
            Debug.Assert(path.GreaterSet != null && path.LesserSet != null, "Wrong use: path must specify a lesser and greater sets before it can be added to a set.");
            RemoveGreaterPath(path);
            path.GreaterSet.LesserPaths.Add(path);
            path.LesserSet.GreaterPaths.Add(path);
        }
        public void RemoveGreaterPath(Dim path)
        {
            if (path.GreaterSet != null) path.GreaterSet.LesserPaths.Remove(path);
            if (path.LesserSet != null) path.LesserSet.GreaterPaths.Remove(path);
        }
        public void RemoveGreaterPath(string name)
        {
            Dim path = GetGreaterPath(name);
            if (path != null)
            {
                RemoveGreaterPath(path);
            }
        }
        public Dim GetGreaterPath(string name)
        {
            return GreaterPaths.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public List<Dim> GetGreaterPathsStartingWith(Dim path)
        {
            if (path == null || path.Path == null) return new List<Dim>();
            return GetGreaterPathsStartingWith(path.Path);
        }
        public List<Dim> GetGreaterPathsStartingWith(List<Dim> path)
        {
            var result = new List<Dim>();
            foreach (Dim p in GreaterPaths)
            {
                if (p.Path == null) continue;
                if (p.Path.Count < path.Count) continue; // Too short path (cannot include the input path)
                if (p.StartsWith(path))
                {
                    result.Add(p);
                }
            }
            return result;
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
            Dim dim = GetGreaterDim(name);
            return dim.GetValue(offset);
        }

        public virtual void SetValue(string name, int offset, object value)
        {
            Dim dim = GetGreaterDim(name);
            dim.SetValue(offset, value);
        }

        public virtual int Append() // Append or find default values
        {
            // Delegate to all dimensions
            foreach (Dim d in GreaterDims)
            {
                d.Append(0); // Append default value for this dimension
            }

            _length++;
            return _length;
        }
        
        public virtual object AppendOrFindIdentity(Dim lesserPath)
        {
            object ret = null;
            if (IsPrimitive) // End of recursion. Here we do not compute the value from greater elemenets but rather get it from the path as final values
            {
                var paths = lesserPath.LesserSet.GetGreaterPathsStartingWith(lesserPath); // Find the path with the value
                if (paths == null || paths.Count == 0) // Not found
                {
                    // ERROR: Wrong use. We need a path in order to get primitive values. This method cannot work without input values.
                }
                else if (paths.Count > 1)
                {
                    // ERROR: Wrong use. Only one path has to provide primitivie value.
                }

                ret = paths[0].CurrentValue;

                if (lesserPath.Path.Count > 0) // If there is history in recursion where the result can be stored
                {
                    lesserPath.LastSegment.CurrentValue = ret;
                }

                return ret;
            }

            // If it is not primitive then we really find/append value in the greater dimensions and then store the found/appended in the last segment
            int[] result = Enumerable.Range(0, Length).ToArray(); // All elements (can be quite long)
            foreach(Dim dim in GreaterDims) // OPTIMIZE: the order of dimensions matters. Use statistics for ordering dimensions. First, use dimensions providing better filtering. 
            {
                // First, we need to find the value to be appended recursively (it is empty)
                lesserPath.AddSegment(dim);
                dim.GreaterSet.AppendOrFindIdentity(lesserPath); // Recursion. This will set CurrentValue for this dimension
                lesserPath.RemoveSegment();

                if (!dim.IsIdentity) continue;

                // Second, use this value to analyze a combination of values for uniqueness - only for identity dimensions
                int[] range = dim.GetOffsets(dim.CurrentValue); // Deproject the value
                result = result.Intersect(range).ToArray(); // OPTIMIZE: Write our own implementation for various operations. Assume that they are ordered.
                // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization)
            }

            if (result.Length == 0) // Not found - append
            {
                foreach (Dim dim in GreaterDims) // We have to append to all dimensions - not only identity dimensions
                {
                    // OPTIMIZE: Provide positions for the values which have been found during the search (not all positions are known if the search has been broken).
                    dim.Append(dim.CurrentValue);
                }
                _length++;
                ret = Length - 1;
            }
            else if(result.Length == 1) // Found single element - return its offset
            {
                ret = result[0];
            }
            else // Many elements satisfy these properties (non-unique identities)
            {
                ret = null; // TODO: Either return the first offset, or all offsets, or generate error
            }

            if (lesserPath.Path.Count > 0) // If there is history in recursion where the result can be stored
            {
                lesserPath.LastSegment.CurrentValue = ret;
            }

            return ret;
        }

        public virtual object AppendOrFindIdentity(Expression expr)
        {
            object ret = null;
            if (IsPrimitive) // End of recursion. Here we do not compute the value from greater elemenets but rather get it from the expression as final values
            {
                ret = expr.Output;
                return ret;
            }

            // If it is not primitive then we really find/append value in all greater dimensions and then store the found/appended
            int[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (can be quite long)
            foreach (Dim dim in GreaterDims) // OPTIMIZE: the order of dimensions matters. Use statistics for ordering dimensions. First, use dimensions providing better filtering. 
            {
                // First, find or append the value recursively. It will be in Output
                Expression childExpr = expr.GetOperand(dim.Name);
                dim.GreaterSet.AppendOrFindIdentity(childExpr);

                if (!dim.IsIdentity) continue;

                // Second, use this value to analyze a combination of values for uniqueness - only for identity dimensions
                int[] range = dim.GetOffsets(childExpr.Output); // Deproject the value
                result = result.Intersect(range).ToArray(); // OPTIMIZE: Write our own implementation for various operations. Assume that they are ordered.
                // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization)
            }

            if (result.Length == 0) // Not found - append
            {
                foreach (Dim dim in GreaterDims) // We have to append to all dimensions - not only identity dimensions
                {
                    Expression childExpr = expr.GetOperand(dim.Name);
                    // OPTIMIZE: Provide positions for the values which have been found during the search (not all positions are known if the search has been broken).
                    dim.Append(childExpr.Output);
                }
                _length++;
                ret = Length - 1;
            }
            else if (result.Length == 1) // Found single element - return its offset
            {
                ret = result[0];
            }
            else // Many elements satisfy these properties (non-unique identities)
            {
                ret = null; // TODO: Either return the first offset, or all offsets, or generate error
            }

            return ret;
        }

        public virtual int Append(Instance instance)
        {
            if (IsPrimitive)
            {
                return 0;
            }

            foreach(Dim dim in GreaterDims) // Iterate either through greater dims (by finding child instances) or by child instances (by finding greater dims)
            {
                Instance child = instance.GetChild(dim.Name); // Mapping: greaterDim -> childInstance
                dim.GreaterSet.Append(child); // Recursion. Append the child value.

                // TODO: We need to compute ranges to find if it already exists

                dim.Append(child.Value); 
            }

            instance.Value = null;


            // - we can reduce it to the same dimension method dim.Append() 
            // This method is applied to a parent dimension (of any level) and takes the corresponding node from the instance structure
            // Its task is to add/find an instance in this dimension given child instance values. After finishing it assigns it to the node value (so that parents can use it). Normally we return offsets.

            // We actualy do not need nested dimensions for that because values are inserted into normal dimensions along the paths. 
            // Nested dimensions are needed to represent and map paths. They also could be used as longer path indexes for performance.

            // Theoretically, we could simply insert new instances into leaf dimensions and forget. 
            // Then on the second step, we propagate leave dimensions into parent dimensions using the above recursieve procedure.
            return (int) instance.Value;
        }

        public virtual void Insert(int offset)
        {
            // Delegate to all dimensions
            foreach(Dim d in GreaterDims)
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

        private DataTable DataTable { get; set; }
        private int CurrentRow;

        /// <summary>
        /// Create dimensions in this set by cloning dimensions of the source set.
        /// The source set is specified in this set definition (FromExpression). 
        /// The method not only creates new dimensions by also defines them by setting their SelectExpression. 
        /// </summary>
        public virtual void ImportDimensions2()
        {
            //
            // Find the source set the dimensions have to be cloned from
            //
            if (FromDb == null || String.IsNullOrEmpty(FromSetName))
            {
                return; // Nothing to import
            }
            Set srcSet = FromDb.FindSubset(FromSetName);
            if (srcSet == null)
            {
                return; // Nothing to import
            }

            //
            // Loop through all source paths and use them these paths in the expressions
            //
            foreach (Dim srcPath in srcSet.GreaterPaths)
            {

                string columnType = Root.MapToLocalType(srcPath.GreaterSet.Name);
                Set gSet = Root.GetPrimitiveSubset(columnType);
                if (gSet == null)
                {
                    // ERROR: Cannot find the matching primitive set for the path
                }
                Dim path = null; // TODO: We should try to find this path and create a new path only if it cannot be found. Or, if found, the existing path should be deleted along with all its segments.
                if (path == null)
                {
                    path = gSet.CreateDefaultLesserDimension(srcPath.Name, this); // We do not know the type of the path
                }
                path.IsIdentity = srcPath.IsIdentity;
                path.SelectDefinition = srcPath.Name;
                path.LesserSet = this;
                path.GreaterSet = gSet;

                Set lSet = this;
                foreach (Dim srcDim in srcPath.Path)
                {
                    Dim dim = lSet.GetGreaterDim(srcDim.Name);
                    if (dim == null)
                    {
                        if (srcDim.GreaterSet == srcPath.GreaterSet)
                        {
                            gSet = srcPath.GreaterSet; // Last segment has the same greater set as the path it belongs to
                        }
                        else // Try to find this greater set in our database
                        {
                            gSet = Root.FindSubset(srcDim.GreaterSet.Name);
                            if (gSet == null)
                            {
                                gSet = new Set(srcDim.GreaterSet.Name, srcDim.GreaterSet);
                                gSet.SuperDim = new DimRoot("super", gSet, Root); // Default solution: insert the set (no dimensions)
                                gSet.ImportDimensions(); // Import its dimensions (recursively). We need at least identity dimensions
                            }
                        }

                        dim = gSet.CreateDefaultLesserDimension(srcDim.Name, lSet);
                        dim.IsIdentity = srcDim.IsIdentity;
                        dim.SelectDefinition = srcDim.Name;
                        dim.LesserSet = lSet;
                        dim.GreaterSet = gSet;
                    }

                    path.Path.Add(dim); // Add this dimension as the next segment in the path

                    lSet = gSet; // Loop iteration
                }

                AddGreaterPath(path); // Add the new dimension to this set 
                foreach (Dim dim in path.Path) // Add also all its segments if they do not exist yet
                {
                    if (dim.LesserSet.GreaterDims.Contains(dim)) continue;
                    dim.LesserSet.AddGreaterDim(dim);
                }
            }
        }

        /// <summary>
        /// Create dimensions in this set by cloning dimensions of the source set.
        /// The source set is specified in this set definition (FromExpression). 
        /// The method not only creates new dimensions by also defines them by setting their SelectExpression. 
        /// </summary>
        public virtual void ImportDimensions()
        {
            //
            // Find the source set the dimensions have to be cloned from
            //
            if (FromDb == null || String.IsNullOrEmpty(FromSetName))
            {
                return; // Nothing to import
            }
            Set srcSet = FromDb.FindSubset(FromSetName);
            if (srcSet == null)
            {
                return; // Nothing to import
            }

            //
            // Loop through all source dimensions and for each create/define one target dimension
            //
            foreach (Dim srcDim in srcSet.GreaterDims)
            {
                Dim dstDim = ImportDimension(srcDim); // Clone dimension
            }
        }

        /// <summary>
        /// Create (recursively) the same dimension tree within this set and return its reference. 
        /// Greater and super sets will be found using mapping (equivalence, same as) and created if absent.
        /// </summary>
        /// <param name="remDim"></param>
        /// <returns></returns>
        public Dim ImportDimension(Dim remDim)
        {
            Set remSet = remDim.GreaterSet;
            Set locSet = null;

            Dim locDim = GetGreaterDim(remDim.Name); // Dimensions are mapped by name
            if (locDim == null) // Not found
            {
                // Try to find local equivalent of the remote greater set using (same as)
                locSet = Root.MapToLocalSet(remSet);
                if (locSet == null) // Not found
                {

                    locSet = new Set(remSet.Name, remSet); // Clone.
                    Set locSuperSet = Root.MapToLocalSet(remSet.SuperSet);
                    locSet.SuperDim = new DimRoot("super", this, locSuperSet);
                }

                // Create a local equivalent of the dimension
                locDim = locSet.CreateDefaultLesserDimension(remDim.Name, this);
                locDim.LesserSet = this;
                locDim.GreaterSet = locSet;
                locDim.IsIdentity = remDim.IsIdentity;
                locDim.SelectExpression = new Expression(remDim);

                // Really add this new dimension to this set
                AddGreaterDim(locDim);
            }
            else // Found
            {
                locSet = locDim.GreaterSet;
            }

            // Clone recursively all greater dimensions
            foreach (Dim dim in remSet.GreaterDims) 
            {
                locSet.ImportDimension(dim);
            }

            return locDim;
        }

        public virtual void Import(Set remoteSet)
        {
            while (remoteSet.ExportCurrentValue() >= 0)
            {
                // Copy values to be imported using some mapping
                foreach (Dim path in GreaterPaths)
                {
                    Dim remotePath = remoteSet.GetGreaterPath(path.Name); // Mapping
                    if(remotePath == null) continue;

                    // Reset all intermediate values along this path
                    for (int i = 0; i < path.Path.Count; i++)
                    {
                        path.Path[i].CurrentValue = null;
                    }

                    path.CurrentValue = remotePath.CurrentValue;
                }

                ImportCurrentValue(); // Import one record (recursively)
            }
        }

        public virtual void Import(DataTable dataTable)
        {
            foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
            {
                foreach (Dim dim in GreaterDims)
                {
                    if (dim.SelectExpression == null) continue;

                    // First, initialize its expression by setting inputs (reference to the current data row)
// TODO                    dim.SelectExpression.SetInput(row);

                    // Second, evaluate each expression so that outputs at leaves will be set
                    dim.SelectExpression.Evaluate();
                }

                ImportCurrentValue(); // Import one record (recursively)
            }
// TODO           AppendOrFindIdentity(Expression expr);
        }

        public virtual DataTable Export()
        {
            // Root represents a database engine and it knows how to access data (from local dimensions, from remote db, from file etc.)
            DataTable = Root.Export(this);
            CurrentRow = -1;
            return DataTable;
        }

        public virtual int ExportCurrentValue()
        {
            CurrentRow++;
            if (CurrentRow >= DataTable.Rows.Count)
            {
                // TODO: Reset all values
                CurrentRow = -1;
                return CurrentRow;
            }

            DataRow row = DataTable.Rows[CurrentRow];

            foreach (Dim path in GreaterPaths)
            {
                // Reset all intermediate values along this path
                for (int i = 0; i < path.Path.Count; i++)
                {
                    path.Path[i].CurrentValue = null;
                }

                path.CurrentValue = row[path.Name]; // Set the primitive value for the matching column name in the source data table
            }

            return CurrentRow;
        }

        public virtual object ImportCurrentValue()
        {
            Dim path = new Dim("LesserPath", this, this); // Used for technical purposes for recursion
            object ret = AppendOrFindIdentity(path); // Recursive call
            return ret;
        }

        public virtual void Populate()
        {
            if (FromSetName == null) // Local population procedure without importing (without external extensional)
            {
                // TODO: 
            }
            else // Popoulating using externally provided values
            {
                // Export data from the remote set
                Set remoteSet = FromDb.FindSubset(FromSetName); // Find remote set
                remoteSet.Export(); // Request a (flat) result set from the remote set

                // Import data into this set
                Import(remoteSet);
            }
        }

        public virtual void Unpopulate() // Clean, Empty
        {
            // After this operation the set is empty
        }

        #endregion

        #region Overriding System.Object

        public override string ToString()
        {
            return String.Format("{0} {1}, ID: {2}", Name, IdentityArity, Id);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (Object.ReferenceEquals(this, obj)) return true;
            if (this.GetType() != obj.GetType()) return false;

            Set set = (Set)obj;
            if (Id.Equals(set.Id)) return true;

            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        #endregion

        #region Constructors and initializers.

        public virtual Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            return new DimSet(name, lesserSet, this);
        }

        public virtual Instance CreateDefaultInstance()
        {
            Instance instance = new Instance(this);
            return instance;
        }

        public Set(string name)
        {
            _id = uniqueId++;
            Name = name;

            SuperDims = new List<Dim>();
            SubDims = new List<Dim>();
            GreaterDims = new List<Dim>();
            LesserDims = new List<Dim>();

            SuperPaths = new List<Dim>();
            SubPaths = new List<Dim>();
            GreaterPaths = new List<Dim>();
            LesserPaths = new List<Dim>();

            // TODO: Parameterize depending on the reserved names: Integer, Double etc. (or exclude these names)
            IsInstantiable = true;
            IsPrimitive = false;
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
