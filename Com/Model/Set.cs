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

        /// <summary>
        /// Unique set id (in this database) . In C++, this Id field would be used as a reference filed
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// A set name. Note that in the general case a set has an associated structure (concept, type) which may have its own name. 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// We assume that there is only one PK (identity). Otherwise, we need a collection. 
        /// </summary>
        public string RelationalPkName { get; set; } // The same field exists also in Dim (do not confuse them)
        public string RelationalTableName { get; set; }

        /// <summary>
        /// How many _instances this set has. Cardinality. Set power. Length (height) of instance set.
        /// If instances are identified by integer offsets, then size also represents offset range.
        /// </summary>
        public virtual int Length { get; protected set; }

        public virtual Type SystemType
        {
            get { return typeof(Offset); }
        }

        /// <summary>
        /// Size of values or cells (physical identities) in bytes.
        /// </summary>
        public virtual int Width
        {
            get { return sizeof(Offset); }
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

        /// <summary>
        /// Constraints on all possible instances. Only instances satisfying these constraints can exist. 
        /// </summary>
        public Expression WhereExpression { get; set; }

        /// <summary>
        /// Ordering of the instances. Instances will be sorted according to these criteria. 
        /// </summary>
        public Expression OrderbyExpression { get; set; }

        #endregion

        #region Simple dimensions

        public List<DimSuper> SuperDims { get; private set; }
        public List<DimSuper> SubDims { get; private set; }
        public List<Dim> GreaterDims { get; private set; }
        public List<Dim> LesserDims { get; private set; }
        public List<DimImport> ExportDims { get; private set; }
        public List<DimImport> ImportDims { get; private set; }

        #region Inclusion. Super.

        public DimSuper SuperDim
        {
            get
            {
                Debug.Assert(SuperDims != null && SuperDims.Count < 2, "Wrong use: more than one super dimension.");
                return SuperDims.Count == 0 ? null : SuperDims[0];
            }
            set
            {
                DimSuper dim = SuperDim;
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

        public bool IsIn(Set parent) // Return true if this set is included in the specified set,that is, the specified set is a direct or indirect super-set of this set
        {
            for (Set set = this; set != null; set = set.SuperSet)
            {
                if (set == parent) return true;
            }
            return false;
        }

        #endregion

        #region Inclusion. Sub.

        public Set AddSubset(Set subset)
        {
            Debug.Assert(subset != null, "Wrong use of method parameter. Subset must be non-null.");
            subset.SuperDim = new DimSuper("super", subset, this);
            return subset;
        }

        public Set RemoveSubset(Set subset)
        {
            Debug.Assert(subset != null, "Wrong use of method parameter. Subset must be non-null.");

            if(!SubDims.Exists(x => x.LesserSet == subset))
            {
                return null; // Nothing to remove
            }

            subset.SuperDim = null;

            return subset;
        }

        public List<Set> SubSets
        {
            get { return NonPrimitiveSubsets; }
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

        #endregion

        #region Poset. Greater. 

        public bool IsGreatest
        {
            get
            {
                return GreaterDims.Count == 0;
            }
        }

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

        public PathEnumerator GetGreaterPrimitiveDims(DimensionType dimType)
        {
            return new PathEnumerator(this, dimType);
        }
/*
        IEnumerable<List<Dim>> GetAllPrimitiveDims()
        {
            Dim p = new Dim("");
            p.Path = new List<Dim>();
            p.LesserSet = this;
            p.GreaterSet = this;

            while (roots.Count > 0)
            {
                var node = roots.Pop();
                foreach (var child in node.GreaterSet.GreaterDims)
                    roots.Push(child);

                yield return node; // Compose a new list and return it
            }
        }
*/
        public void AddGreaterDim(Dim dim)
        {
            RemoveGreaterDim(dim);
            Debug.Assert(dim.GreaterSet != null && dim.LesserSet != null, "Wrong use: dimension must specify a lesser and greater sets before it can be added to a set.");
            dim.SetLength(this.Length);
            dim.GreaterSet.LesserDims.Add(dim);
            dim.LesserSet.GreaterDims.Add(dim);
        }
        public void RemoveGreaterDim(Dim dim)
        {
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
        public Dim GetGreaterDimByFkName(string name)
        {
            return GreaterDims.FirstOrDefault(d => d.RelationalFkName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public List<Set> GetGreaterSets()
        {
            return GreaterDims.Select(x => x.GreaterSet).ToList();
        }
        public List<Dim> GetIdentityDims()
        {
            return GreaterDims.Where(x => x.IsIdentity).ToList();
        }
        public List<Dim> GetEntityDims()
        {
            return GreaterDims.Where(x => !x.IsIdentity).ToList();
        }

        #endregion

        #region Poset. Lesser.

        public bool IsLeast
        {
            get
            {
                return LesserDims.Count == 0;
            }
        }

        public List<Set> GetLesserSets()
        {
            return LesserDims.Select(x => x.LesserSet).ToList();
        }

        #endregion

        #endregion

        #region Complex dimensions (paths)

        public List<DimSuper> SuperPaths { get; private set; }
        public List<DimSuper> SubPaths { get; private set; }
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
        public Dim GetGreaterPathByColumnName(string name)
        {
            return GreaterPaths.FirstOrDefault(d => d.RelationalColumnName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public Dim GetGreaterPath(Dim path)
        {
            if (path == null || path.Path == null) return null;
            return GetGreaterPath(path.Path);
        }
        public Dim GetGreaterPath(List<Dim> path)
        {
            if (path == null ) return null;
            foreach (Dim p in GreaterPaths)
            {
                if (p.Path == null) continue;
                if (p.Path.Count != path.Count) continue; // Different lengths => not equal

                bool equal = true;
                for (int seg=0; seg<p.Path.Count && equal; seg++)
                {
                    if (!p.Path[seg].Name.Equals(path[seg].Name, StringComparison.InvariantCultureIgnoreCase)) equal = false;
                    // if (p.Path[seg] != path[seg]) equal = false; // Compare strings as objects
                }
                if(equal) return p;
            }
            return null;
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
        
        public void AddAllNonStoredPaths()
        {
            int pathCounter = 0;

            Dim path = new Dim("");
            foreach (List<Dim> p in GetGreaterPrimitiveDims(DimensionType.IDENTITY_ENTITY))
            {
                if (p.Count < 2) continue; // All primitive paths are stored in this set. We need at least 2 segments.

                // Check if this path already exists
                path.Path = p;
                if (GetGreaterPath(path) != null) continue; // Already exists

                string pathName = "__inherited__" + ++pathCounter;

                Dim newPath = new Dim(pathName);
                newPath.Path = new List<Dim>(p);
                newPath.Name = newPath.ComplexName; // Overwrite previous pathName (so previous is not needed actually)
                newPath.RelationalColumnName = newPath.Name; // It actually will be used for relational queries
                newPath.RelationalFkName = path.RelationalFkName; // Belongs to the same FK
                newPath.RelationalPkName = null;
                newPath.LesserSet = this;
                newPath.GreaterSet = p[p.Count - 1].GreaterSet;

                AddGreaterPath(newPath);
            }
        }

        #endregion

        #region Instance manipulation (function, data) methods

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

            Length++;
            return Length;
        }

        public virtual object Append(Expression expr)
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
                dim.GreaterSet.Append(childExpr);

                if (!dim.IsIdentity) continue;

                // Second, use this value to analyze a combination of values for uniqueness - only for identity dimensions
                int[] range = dim.GetOffsets(childExpr.Output); // Deproject the value
                result = result.Intersect(range).ToArray(); // OPTIMIZE: Write our own implementation for various operations. Assume that they are ordered.
                // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization)
            }

            if (result.Length == 0) // Not found - append
            {
                foreach (Dim dim in GreaterDims)
                {
                    Expression childExpr = expr.GetOperand(dim.Name);
                    // OPTIMIZE: Provide positions for the values which have been found during the search (not all positions are known if the search has been broken).
                    dim.Append(childExpr.Output);
                }
                Length++;
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

        public virtual void Insert(int offset)
        {
            // Delegate to all dimensions
            foreach(Dim d in GreaterDims)
            {
                d.Insert(offset, null);
            }

            Length++;
        }

        public virtual void Remove(int offset)
        {
            Length--;
            // TODO: Remove it from all dimensions in loop including super-dim and special dims
            // PROBLEM: should we propagate this removal to all lesser dimensions? We need a flag for this property. 
        }

        public virtual object Aggregate(string function, object values) { return null; } // It has to dispatch it to a specific type which knows how to aggregate its values

        #endregion

        #region Overriding System.Object

        public override string ToString()
        {
            return String.Format("{0} gDims: {1}, IdArity: {2}", Name, GreaterDims.Count, IdentityArity);
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
            Id = Guid.NewGuid();
            Name = name;

            SuperDims = new List<DimSuper>();
            SubDims = new List<DimSuper>();
            GreaterDims = new List<Dim>();
            LesserDims = new List<Dim>();
            ExportDims = new List<DimImport>();
            ImportDims = new List<DimImport>();

            SuperPaths = new List<DimSuper>();
            SubPaths = new List<DimSuper>();
            GreaterPaths = new List<Dim>();
            LesserPaths = new List<Dim>();

            // TODO: Parameterize depending on the reserved names: Integer, Double etc. (or exclude these names)
            IsInstantiable = true;
            IsPrimitive = false;
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
