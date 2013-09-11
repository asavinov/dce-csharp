﻿using System;
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
            subset.SuperDim = new DimSuper("Super", subset, this);
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
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            if (name.Equals("Super", StringComparison.InvariantCultureIgnoreCase))
            {
                return SuperDim.GetValue(offset);
            }
            else
            {
                Dim dim = GetGreaterDim(name);
                return dim.GetValue(offset);
            }
        }

        public virtual void SetValue(string name, int offset, object value)
        {
            Dim dim = GetGreaterDim(name);
            dim.SetValue(offset, value);
        }

        public virtual object Find(Expression expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            if (IsPrimitive)
            {
                Debug.Assert(expr.Operation != Operation.TUPLE, "Wrong use: cannot find TUPLE in a primitive set. Need a primitive value.");

                expr.Evaluate();
                Debug.Assert(expr.Output.GetType().IsPrimitive, "Wrong use: cannot find non-primitive type in a primitive set. Need a primitive value.");
                Debug.Assert(!expr.Output.GetType().IsArray, "Wrong use: cannot find array type in a primitive set. Need a primitive value.");
                return expr.Output; // It is assumed that the value (of correct type) exists and is found
            }

            if (expr.Operation != Operation.TUPLE) // End of recursion tuples
            {
                // Instead of finding an offset for a combination of values, we evaluate the offset as output of the expression (say, a variable or some function)
                expr.Evaluate();
                return expr.Output;
            }

            if (Length == 0) return null;

            //
            // Find a tuple in a non-primitive set recursively
            //
            Offset[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (can be quite long)

            // Super-dimension
            if (SuperSet.Length > 0 && expr.Input != null)
            {
                object childOffset = SuperSet.Find(expr.Input);

                Offset[] range = SuperDim.GetOffsets(childOffset);
                result = result.Intersect(range).ToArray();
            }
            
            // Now all other dimensions
            foreach (Dim dim in GetIdentityDims()) // OPTIMIZE: the order of dimensions matters (use statistics, first dimensins with better filtering). Also, first identity dimensions.
            {
                // First, find or append the value recursively. It will be in Output
                Expression childExpr = expr.GetOperand(dim);
                if (childExpr == null) continue;
                object childOffset = dim.GreaterSet.Find(childExpr);

                // Second, use this value to analyze a combination of values for uniqueness - only for identity dimensions
                Offset[] range = dim.GetOffsets(childOffset); // Deproject the value
                result = result.Intersect(range).ToArray(); // OPTIMIZE: Write our own implementation for various operations. Assume that they are ordered.
                // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization)

                if (result.Length < 2) break; // Found or does not exist
            }

            if (result.Length == 0) // Not found
            {
                return null;
            }
            else if (result.Length == 1) // Found single element - return its offset
            {
                return result[0];
            }
            else // Many elements satisfy these properties (non-unique identities)
            {
                Debug.Fail("Wrong use: Many elements found although only one or no elmeents are supposed to be found. Use de-projection instead.");
                return null;
            }
        }

        public virtual object Append(Expression expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
        {
            if (IsPrimitive)
            {
                Debug.Assert(expr.Operation != Operation.TUPLE, "Wrong use: cannot append TUPLE to a primitive set.");

                // Debug.Assert(expr.Output.GetType().IsPrimitive, "Wrong use: cannot append non-primitive type to a primitive set. ");
                Debug.Assert(!expr.Output.GetType().IsArray, "Wrong use: cannot append array type to a primitive set. ");
                return expr.Output; // Primitive sets are supposed to already contain all values (of correct type)
            }

            if (expr.Operation != Operation.TUPLE) // End of recursion tuples
            {
                // Instead of finding an offset for a combination of values, we evaluate the offset as output of the expression (say, a variable or some function)
                return expr.Output;
            }

            // Check if it already exists
            object offset = Find(expr);
            if (offset != null) // Already exists - no need to append
            {
                expr.Output = offset;
                return expr.Output;
            }

            //
            // Append a tuple to a non-primitive set recursively
            //

            // Super-dimension
            if (SuperSet.Length > 0 && expr.Input != null)
            {
                SuperDim.Append(expr.Input.Output);
            }

            // All other dimensions
            foreach (Dim dim in GreaterDims)
            {
                Expression childExpr = expr.GetOperand(dim);
                Debug.Assert(!(dim.IsIdentity && childExpr == null), "Wrong use: All identity dimensions must be provided for appending an element.");

                if (childExpr == null)
                {
                    dim.Append(null);
                }
                else
                {
                    dim.GreaterSet.Append(childExpr); // Recursion after which we know the offset that has to be appended to this set dimension
                    // OPTIMIZE: Provide positions for the values which have been found during the search (not all positions are known if the search has been broken).
                    dim.Append(childExpr.Output); 
                }
            }

            expr.Output = Length;
            Length++;
            return expr.Output;
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

        #region Set definition and population

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

        /// <summary>
        /// Create all instances of this set. 
        /// This method implements the main generation loop and in the body generates one instance. 
        /// </summary>
        public virtual void Populate() 
        {
            //
            // Determine the type of generation procedure. If there are remote (import/export) dimensions then it is a remote procedure. 
            //
            bool LocalGeneration = true;

            // It is an indirect generation where we build all possible instances and then check if it is what we need by evaluating a predicate
            if (LocalGeneration) // Product of local sets
            {
                //
                // Find local greater generation sets including the super-set. Create a tuple corresponding to these dimensions
                //
                var dims = new List<Dim>();
                dims.Add(SuperDim);
                dims.AddRange(GetIdentityDims());

                Expression tupleExpr = new Expression(this.Name, Operation.TUPLE, this); // Represents a record to be appended to the set
                for(int i=0; i<dims.Count; i++) 
                {
                    Dim d = dims[i];
                    Expression childExpr = new Expression(d.Name, Operation.PRIMITIVE, d.GreaterSet);

                    if (i == 0) // Super-dimension is stored in expression Input
                    {
                        tupleExpr.Input = childExpr;
                    }
                    else // All other (non-super) dimensions are in operands
                    {
                        tupleExpr.AddOperand(childExpr);
                    }
                }

                int dimCount = dims.Count();

                Offset[] offsets = new Offset[dimCount];
                for (int i = 0; i < dimCount; i++) offsets[i] = -1;

                Offset[] lengths = new Offset[dimCount];
                for (int i = 0; i < dimCount; i++) lengths[i] = dims[i].GreaterSet.Length;

                int top = -1; // The current level or top where we change the offset. Depth of recursion.
                do ++top; while (top < dimCount && lengths[top] == 0);
                // Alternative recursive iteration: http://stackoverflow.com/questions/13655299/c-sharp-most-efficient-way-to-iterate-through-multiple-arrays-list
                while (top >= 0) 
                {
                    if (top == dimCount) // Element is ready. Process new element.
                    {
                        bool satisfies = true;

                        if (WhereExpression != null)
                        {
                            // Initialize the where-expression before evaluation by using current offsets
                            for (int i = 0; i < dimCount; i++)
                            {
                                Dim d = dims[i];
                                // Find all uses of the dimension in the expression and initialize it before evaluation
                                List<Expression> dimExpressions = WhereExpression.GetOperands(Operation.DOT, d.Name);
                                foreach (Expression e in dimExpressions)
                                {
                                    if (e.Input.OutputSet != d.LesserSet) continue;
                                    if (e.OutputSet != d.GreaterSet) continue;
                                    Debug.Assert(!e.Input.OutputSet.IsPrimitive, "Wrong use: primitive set cannot be used in the product for producing a new set (too many combinations).");
                                    e.Input.Output = -1; // The function will not be evaluated (actually, it should be set only once before the loop)
                                    e.Output = offsets[i]; // Current offset (will be used as is without assignment during evaluation because Input.Output==-1
                                }

                                // Also initialize an instance for the case it has to be appended
                                Expression dimExpression = tupleExpr.GetOperand(d);
                                if (dimExpression != null) dimExpression.Output = offsets[i];
                            }

                            // Check if it satisfies the constraints by evaluating WhereExpression and append
                            WhereExpression.Evaluate();
                            satisfies = (bool)WhereExpression.Output;
                        }

                        if (satisfies)
                        {
                            // Initialize an instance for appending
                            for (int i = 0; i < dimCount; i++)
                            {
                                Dim d = dims[i];

                                Expression dimExpression = tupleExpr.GetOperand(d);
                                if (dimExpression != null) dimExpression.Output = offsets[i];
                            }

                            Append(tupleExpr);
                        }

                        do --top; while (top >= 0 && lengths[top] == 0); // Go up by skipping empty dimensions
                    }
                    else
                    {
                        offsets[top]++;
                        if (offsets[top] < lengths[top]) // Valid offset
                        {
                            do ++top; while (top < dimCount && lengths[top] == 0); // Go up by skipping empty dimensions
                        }
                        else // Invalid offset. This level is finished. 
                        {
                            offsets[top] = -1; // Reset
                            do --top; while (top >= 0 && lengths[top] == 0); // Go up by skipping empty dimensions
                        }
                    }
                }

            }
            else if (true)
            {
                // Find remote (impport/export) sets, organize the main loop and evaluate local identity dimensions. 
                // Also we might use import/export dims for user input with manual specification of records
            }
            else
            {
                // TODO: Here we might need a direct procedure by building the instances satisfying the condition as opposed to building all possible instances and then checking if they satisfy the condition. 
                // This direct procedure is used when building subsets of primitive sets or their combinations (it is not possible to generate all possible primitive values). 
                // The direct procedure can be also used as optimization technique for normal sets where we can directly produce the necessary instances.
                // We could even mix these two approaches by organizing a loop but skipping some large intervals if they are known to not to satisfy our conditions. Say, if age<30 then we could directly iterate only in this interval (in the presence of indexes). 
            }

        }

        //
        // Empty the set and all its dimensions.
        //
        public virtual void Unpopulate() 
        {
            SuperDim.Unpopulate();

            foreach(Dim d in GreaterDims) 
            {
                d.Unpopulate();
            }

            Length = 0;

            return; 
        }

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
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            if (name.Equals("Super", StringComparison.InvariantCultureIgnoreCase))
            {
                return new DimSuper(name, lesserSet, this);
            }
            else
            {
                return new DimSet(name, lesserSet, this);
            }
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
            IsAutoPopulated = true;
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
