using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Com.Query;

using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// A description of a set which may have subsets, geater or lesser sets, and instances. 
    /// A set is characterized by its structure which includes subsets, greater and lesser sets. 
    /// A set is also characterized by width and length of its members. 
    /// And a set provides methods for manipulating its structure and intances. 
    /// </summary>
    public class Set : INotifyCollectionChanged, INotifyPropertyChanged, CsTable, CsTableData, CsTableDefinition
    {
        /// <summary>
        /// Unique set id (in this database) . In C++, this Id field would be used as a reference filed
        /// </summary>
        public Guid Id { get; private set; }

        #region CsTable interface

        /// <summary>
        /// A set name. Note that in the general case a set has an associated structure (concept, type) which may have its own name. 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether it is a primitive set. Primitive sets do not have greater dimensions.
        /// It can depend on other propoerties (it should be clarified) like instantiable, autopopulated, virtual etc.
        /// </summary>
        public bool IsPrimitive { get { return SuperDim.GreaterSet is CsSchema; } }

        //
        // Outputs
        //
        public List<CsColumn> GreaterDims { get; private set; } // Outgoing up arrows. Outputs
        public CsColumn SuperDim { get { return GreaterDims.FirstOrDefault(x => x.IsSuper); } }
        public List<CsColumn> KeyColumns { get { return GreaterDims.Where(x => x.IsIdentity).ToList(); } }
        public List<CsColumn> NonkeyColumns { get { return GreaterDims.Where(x => !x.IsIdentity).ToList(); } }
        public CsSchema Top
        {
            get
            {
                CsTable set = this;
                while (set.SuperSet != null) set = set.SuperSet;
                return set is SetTop ? (SetTop)set : null; // A set which is not integrated in the schema does not have top
            }
        }
        public List<CsTable> GetGreaterSets() { return GreaterDims.Select(x => x.GreaterSet).ToList(); }
        public CsTable SuperSet { get { return SuperDim != null ? SuperDim.GreaterSet : null; } }

        //
        // Inputs
        //

        public List<CsColumn> LesserDims { get; private set; } // Incoming arrows. Inputs
        public List<CsColumn> SubDims { get { return LesserDims.Where(x => x.IsSuper).ToList(); } }
        public List<CsTable> SubSets { get { return SubDims.Select(x => x.LesserSet).ToList(); } }
        public List<CsTable> GetAllSubsets() // Should be solved using general enumerator? Other: get all lesser, get all greater
        {
            int count = SubSets.Count;
            List<CsTable> result = new List<CsTable>(SubSets);
            for (int i = 0; i < count; i++)
            {
                List<CsTable> subsets = ((Set)result[i]).GetAllSubsets();
                if (subsets == null || subsets.Count == 0)
                {
                    continue;
                }
                result.AddRange(subsets);
            }

            return result;
        }

        //
        // Poset relation
        //

        // TODO: Maybe rewrite as one method IsLess with parameter as bit mask {Super, Key, Nonkey}
        // And then define shortcut methods via this general methods. In fact, IsLess *is* already defined via enumeator

        // Return true if this set is included in the specified set, that is, the specified set is a direct or indirect super-set of this set
        public bool IsIn(CsTable parent) // IsSub
        {
            for (CsTable set = this; set != null; set = set.SuperDim.GreaterSet)
            {
                if (set == parent) return true;
            }
            return false;
        }

        public bool IsLesser(CsTable set) // IsLess
        {
            var paths = new PathEnumerator(this, set, DimensionType.IDENTITY_ENTITY);
            return paths.Count() > 0;
        }

        public bool IsLeast { get { return LesserDims.Count(x => x.LesserSet.Top == x.GreaterSet.Top) == 0; } } // Direct to bottom

        public bool IsGreatest // TODO: Direct to top
        {
            get
            {
                return IsPrimitive || GreaterDims.Count(x => x.LesserSet.Top == x.GreaterSet.Top) == 0; // All primitive sets are greatest by definition
            }
        }

        //
        // Name methods
        //

        public CsColumn GetGreaterDim(string name)
        {
            return GreaterDims.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        public CsTable getTable(string name) { return LesserDims.FirstOrDefault(d => d.LesserSet.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)).LesserSet; }

        public CsTable FindSubset(string name)
        {
            CsTable set = null;
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

        public CsTableData TableData { get { return this; } }

        public CsTableDefinition TableDefinition { get { return this; } }

        #endregion

        #region CsTableData interface

        /// <summary>
        /// How many _instances this set has. Cardinality. Set power. Length (height) of instance set.
        /// If instances are identified by integer offsets, then size also represents offset range.
        /// </summary>
        public Offset Length { get; protected set; }

        public object GetValue(string name, int offset)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            CsColumn dim = GetGreaterDim(name);
            return dim.ColumnData.GetValue(offset);
        }

        public void SetValue(string name, int offset, object value)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            CsColumn dim = GetGreaterDim(name);
            dim.ColumnData.SetValue(offset, value);
        }

        public Offset Find(Dictionary<Dim, object> values)
        {
            Offset[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (can be quite long)
            foreach (var pair in values)
            {
                Dim dim = pair.Key;
                object val = pair.Value;

                Offset[] range = dim.ColumnData.DeprojectValue(val); // Deproject one value
                result = result.Intersect(range).ToArray(); // OPTIMIZE: Write our own implementation for various operations. Assume that they are ordered.
                // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization). Maybe store it back to the dictionary.

                if (result.Length == 0) break; // Not found
            }

            if (result.Length == 0) // Not found
            {
                return -1;
            }
            else if (result.Length == 1) // Found single element - return its offset
            {
                return result[0];
            }
            else // Many elements satisfy these properties (non-unique identities)
            {
                return -result.Length;
            }
        }

        public bool Find(ExprNode expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            // Find: Find the tuple and all nested tuples. Is applied only if the value is null - otherwise it assumed existing and no recursion is made. 
            // Value: Output is set to offset for found tuples and (remains) null if not found.

            /*
            Debug.Assert(expr.OutputSet == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");

            if (IsPrimitive)
            {
                Debug.Assert(expr.Output == null || expr.Output.GetType().IsPrimitive, "Wrong use: cannot find non-primitive type in a primitive set. Need a primitive value.");
                Debug.Assert(expr.Output == null || !expr.Output.GetType().IsArray, "Wrong use: cannot find array type in a primitive set. Need a primitive value.");
                return true; // It is assumed that the value (of correct type) exists and is found
            }

            if (expr.Operation != Operation.TUPLE) // End of recursion tuples
            {
                // Instead of finding an offset for a combination of values, we evaluate the offset as output of the expression (say, a variable or some function)
                return true;
            }

            if (expr.Output != null) // Already found - not need to search
            {
                return true;
            }

            if (Length == 0) return false;

            //
            // Find a tuple in a non-primitive set recursively
            //
            Offset[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (can be quite long)

            // Super-dimension
            if (SuperSet.TableData.Length > 0 && expr.Input != null)
            {
                SuperSet.TableData.Find(expr.Input);
                object childOffset = expr.Input.Output;

                Offset[] range = SuperDim.ColumnData.DeprojectValue(childOffset);
                result = result.Intersect(range).ToArray();
            }
            
            // Now all other dimensions
            foreach (Dim dim in KeyColumns) // OPTIMIZE: the order of dimensions matters (use statistics, first dimensins with better filtering). Also, first identity dimensions.
            {
                // First, find or append the value recursively. It will be in Output
                Expression childExpr = expr.GetOperand(dim);
                if (childExpr == null) continue;
                dim.GreaterSet.TableData.Find(childExpr);
                object childOffset = childExpr.Output; 

                // Second, use this value to analyze a combination of values for uniqueness - only for identity dimensions
                Offset[] range = dim.ColumnData.DeprojectValue(childOffset); // Deproject the value
                result = result.Intersect(range).ToArray(); // OPTIMIZE: Write our own implementation for various operations. Assume that they are ordered.
                // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization)

                if (result.Length < 2) break; // Found or does not exist
            }

            if (result.Length == 0) // Not found
            {
                expr.Output = null;
                return false;
            }
            else if (result.Length == 1) // Found single element - return its offset
            {
                expr.Output = result[0];
                return true;
            }
            else // Many elements satisfy these properties (non-unique identities)
            {
                Debug.Fail("Wrong use: Many elements found although only one or no elmeents are supposed to be found. Use de-projection instead.");
                expr.Output = result;
                return true;
            }
            */
            return true;
        }

        public bool CanAppend(ExprNode expr) // Determine if this expression (it has to be evaluated) can be added into this set as a new instance
        {
            // CanAppend: Check if the whole tuple can be added without errors
            // We do not check existence (it is done before). If tuple exists then no check is done and return false. If null then we check general criterial for adding (presence of all necessary data).

            //Debug.Assert(expr.OutputSet == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");

            //
            // Check that real (non-null) values are available for all identity dimensions
            //
            PathEnumerator primPaths = new PathEnumerator(this, DimensionType.IDENTITY);
            foreach (DimPath path in primPaths) // Find all primitive identity paths
            {
                // Try to find at least one node with non-null value on the path
                bool valueFound = false;
                /*
                for (Expression node = expr.GetLastNode(path); node != null; node = node.ParentExpression)
                {
                    if (node.Output != null) { valueFound = true; break; }
                }
                */

                if (!valueFound) return false; // This primitive path does not provide a value so the whole instance cannot be created
            }

            //
            // Check that it satisfies the constraints (where expression)
            //

            // TODO: it is a problem because for that purpose we need to have this instance in the set appended. 
            // Then we can check and remove but nested removal is difficult because we have to know which nested tuples were found and which were really added.
            // Also, we need to check if nested inserted instances satsify their set constraints - this should be done during insertion and the process broken if any nested instance does not satsify the constraints.

            return true;
        }

        public object Append(ExprNode expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
        {
            // Append: append *this* tuple to the set and, if necessary, all greater tuples. If necessary means "if no value for dimension is provided"  which means does not exist. 
            // In particular, if all child expressions have values then only this set will be appended. 
            // In particular, if this set has a value then it will not be appended (because it exists).
            // Value: offset of new appended instance. 

            /*
            Debug.Assert(expr.OutputSet == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");

            if (IsPrimitive)
            {
                Debug.Assert(expr.Output == null || !expr.Output.GetType().IsArray, "Wrong use: cannot append array type to a primitive set. ");
                return expr.Output; // Primitive sets are supposed to already contain all values (of correct type)
            }

            if (expr.Operation != Operation.TUPLE) // End of recursion tuples
            {
                // Instead of finding an offset for a combination of values, we evaluate the offset as output of the expression (say, a variable or some function)
                return expr.Output;
            }

            if (expr.Output != null) // Already exists - no need to append
            {
                return expr.Output;
            }

            if (!CanAppend(expr)) // Cannot be appended (identity not defined completely, integrity constraints etc.)
            {
                return expr.Output; // It must be null in this case
            }

            //
            // Append a complex value to a non-primitive set recursively
            //

            if (SuperSet.IsInstantiable && expr.Input != null) // Super-dimension
            {
                SuperDim.ColumnData.Append(expr.Input.Output);
            }

            foreach (Dim dim in GreaterDims) // All other dimensions
            {
                Expression childExpr = expr.GetOperand(dim);

                object val = null;
                if (childExpr != null)
                {
                    if (childExpr.Output == null)
                    {
                        dim.GreaterSet.TableData.Append(childExpr); // Recursive insertion
                    }
                    val = childExpr.Output;
                }
                dim.ColumnData.Append(val);
            }

            expr.Output = Length;
            Length++;
            return expr.Output;
            */

            return null;
        }

        public void Remove(int offset)
        {
            Length--;
            // TODO: Remove it from all dimensions in loop including super-dim and special dims
            // PROBLEM: should we propagate this removal to all lesser dimensions? We need a flag for this property. 
        }

        #endregion

        #region CsTableDefinition

        /// <summary>
        /// Constraints on all possible instances. 
        /// Currently, it is written in terms of and is applied to source (already existing) instances - not instances of this set. Only instances satisfying these constraints are used for populating this set. 
        /// In future, we should probabyl apply these constraints to this set elements while the source set has its own constraints.
        /// </summary>
        public ExprNode WhereExpression { get; set; }
        public List<CsColumn> ProjectDimensions { get; set; } // Output tuples of these dimensions are appended to this set (other tuples are excluded). Alternatively, this element must be referenced by at one lesser element COUNT(this<-proj_dim<-(Set)) > 0

        /// <summary>
        /// Ordering of the instances. 
        /// Here again we have a choice: it is how source elements are sorted or it is how elements of this set have to be sorted. 
        /// </summary>
        public ExprNode OrderbyExpression { get; set; }

        /// <summary>
        /// Create all instances of this set. 
        /// </summary>
        public void Populate() 
        {
            if (TableDefinition.ProjectDimensions == null || TableDefinition.ProjectDimensions.Count == 0) // Product of local sets (no project/de-project from another set)
            {
                //
                // Find local greater generation sets including the super-set. Create a tuple corresponding to these dimensions
                //
                var dims = new List<CsColumn>();
                dims.Add(SuperDim);
                dims.AddRange(KeyColumns);

                ExprNode tupleExpr = new ExprNode(); // Represents a record to be appended to the set (argument for Append method)
                // TODO: Configure it as a one-level tuple with values in leaves

                int dimCount = dims.Count();

                Offset[] lengths = new Offset[dimCount];
                for (int i = 0; i < dimCount; i++) lengths[i] = dims[i].GreaterSet.TableData.Length;

                Offset[] offsets = new Offset[dimCount]; // Here we store the current state of choices for each dimensions
                for (int i = 0; i < dimCount; i++) offsets[i] = -1;

                int top = -1; // The current level/top where we change the offset. Depth of recursion.
                do ++top; while (top < dimCount && lengths[top] == 0);

                // Alternative recursive iteration: http://stackoverflow.com/questions/13655299/c-sharp-most-efficient-way-to-iterate-through-multiple-arrays-list
                while (top >= 0) 
                {
                    if (top == dimCount) // New element is ready. Process it.
                    {
                        bool satisfies = true;

                        // TODO: Reset the tuple to be appended
                        // tupleExpr.SetOutput(Operation.ALL, null);

                        if (TableDefinition.WhereExpression != null)
                        {
                            // REWORK:
                            // Probably, we should first append an instance (without indexing), and then the Where predicate will be either checked automatically (with exception), or we have to check it manually by applying to the newly appended element (then the automatic check has to be turned off).
                            // In other words, we do not apply Where expression (boolean function) to a tuple or another object - we apply it to a normal element with some offset.
                            // Alternatively, we could provide a tuple to Append method, and the tuple has Action=Append (not Find). So it is the Append method that knows how to add records and that knows how to check the predicate. This Populate method simply organizes a loop with tuple generation.
                            // Prepare Where expression for evaluation. Check if it uses our columns and bind these nodes to our columns
                            // Initialize the where-expression before evaluation by using current offsets
                            /*
                            for (int i = 0; i < dimCount; i++)
                            {
                                CsColumn d = dims[i];
                                // Find all uses of the dimension in the expression and initialize it before evaluation
                                List<Expression> dimExpressions = TableDefinition.WhereExpression.GetOperands(Operation.DOT, d.Name);
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

                            //
                            // Check if it satisfies the constraints by evaluating WhereExpression and append
                            //
                            TableDefinition.WhereExpression.Evaluate();
                            satisfies = (bool)TableDefinition.WhereExpression.Output;
                            */
                        }

                        // REWORK
                        /*
                        if (satisfies)
                        {
                            // Initialize an instance for appending
                            for (int i = 0; i < dimCount; i++)
                            {
                                CsColumn d = dims[i];
                                Expression dimExpression = tupleExpr.GetOperand(d);
                                if (dimExpression != null) dimExpression.Output = offsets[i];
                            }

                            Append(tupleExpr);
                        }
                        */

                        top--;
                        while (top >= 0 && lengths[top] == 0) // Go up by skipping empty dimensions and reseting 
                        { offsets[top--] = -1; } 
                    }
                    else
                    {
                        // Find the next valid offset
                        offsets[top]++;

                        if (offsets[top] < lengths[top]) // Offset chosen
                        {
                            do ++top; 
                            while (top < dimCount && lengths[top] == 0); // Go up (foreward) by skipping empty dimensions
                        }
                        else // Level is finished. Go back.
                        {
                            do { offsets[top--] = -1; } 
                            while (top >= 0 && lengths[top] == 0); // Go down (backward) by skipping empty dimensions and reseting 
                        }
                    }
                }

            }
            else if (true) // There are import dimensions so copy data from another set (projection of another set)
            {
                CsColumn projectDim = TableDefinition.ProjectDimensions[0];
                CsTable sourceSet = projectDim.LesserSet;
                CsTable targetSet = projectDim.GreaterSet; // this set

                //
                // Prepare the expression from the mapping
                //
                CsColumnEvaluator evaluator = projectDim.ColumnDefinition.GetColumnEvaluator();

                //
                // Loop over all function inputs with evaluation and using the output tuple for appending
                //
                for (Offset input = 0; input < sourceSet.TableData.Length; input++)
                {
                    evaluator.Evaluate(input);
                    //targetSet.TableData.Append(tupleExpression);
                    // TODO: append also an instance to the function (the function has to be nullifed before the procedure)
                    //SetValue(offset, SelectExpression.Output); // Store the final result
                }
            }

        }

        /// <summary>
        /// Remove all instances.
        /// </summary>
        public void Unpopulate()
        {
            // TODO: SuperDim.Length = 0;

            foreach(Dim d in GreaterDims) 
            {
                // TODO: d.Length = 0;
            }

            Length = 0;

            return; 
        }

        #endregion

        #region System interfaces

        public event NotifyCollectionChangedEventHandler CollectionChanged; // Operations with dimensions (of any kind)
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, e);
            }
        }
        public virtual void NotifyAdd(Dim dim) // Convenience method: notifying about adding
        {
            if (dim == null) return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, dim));
        }
        public virtual void NotifyRemove(Dim dim) // Convenience method: notifying about removing
        {
            if (dim == null) return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, dim));
        }

        //
        // INotifyPropertyChanged Members
        //
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public override string ToString()
        {
            return String.Format("{0} gDims: {1}, IdArity: {2}", Name, GreaterDims.Count, KeyColumns.Count);
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

        #region Relational properties and methods

        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// We assume that there is only one PK (identity). Otherwise, we need a collection. 
        /// </summary>
        public string RelationalTableName { get; set; }
        public string RelationalPkName { get; set; } // The same field exists also in Dim (do not confuse them)

        public CsColumn GetGreaterDimByFkName(string name)
        {
            return GreaterDims.FirstOrDefault(d => ((Dim)d).RelationalFkName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        #endregion

        #region Complex dimensions (paths) - move to a subclass (relational?)

        public List<DimPath> SuperPaths { get; private set; }
        public List<DimPath> SubPaths { get; private set; }
        public List<DimPath> GreaterPaths { get; private set; }
        public List<DimPath> LesserPaths { get; private set; }

        public void AddGreaterPath(DimPath path)
        {
            Debug.Assert(path.GreaterSet != null && path.LesserSet != null, "Wrong use: path must specify a lesser and greater sets before it can be added to a set.");
            RemoveGreaterPath(path);
            ((Set)path.GreaterSet).LesserPaths.Add(path);
            ((Set)path.LesserSet).GreaterPaths.Add(path);
        }
        public void RemoveGreaterPath(DimPath path)
        {
            if (path.GreaterSet != null) ((Set)path.GreaterSet).LesserPaths.Remove(path);
            if (path.LesserSet != null) ((Set)path.LesserSet).GreaterPaths.Remove(path);
        }
        public void RemoveGreaterPath(string name)
        {
            DimPath path = GetGreaterPath(name);
            if (path != null)
            {
                RemoveGreaterPath(path);
            }
        }
        public DimPath GetGreaterPath(string name)
        {
            return GreaterPaths.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public DimPath GetGreaterPathByColumnName(string name)
        {
            return GreaterPaths.FirstOrDefault(d => d.RelationalColumnName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        public List<DimPath> GetGreaterPaths(Set greaterSet) // Differences between this set and the specified set
        {
            if (greaterSet == null) return null;
            var paths = new PathEnumerator(this, greaterSet, DimensionType.IDENTITY_ENTITY);
            var ret = new List<DimPath>();
            foreach (var p in paths)
            {
                ret.Add(new DimPath(p)); // Create a path for each list of dimensions
            }

            return ret;
        }
        public DimPath GetGreaterPath(DimPath path)
        {
            if (path == null || path.Path == null) return null;
            return GetGreaterPath(path.Path);
        }
        public DimPath GetGreaterPath(List<CsColumn> path)
        {
            if (path == null) return null;
            foreach (DimPath p in GreaterPaths)
            {
                if (p.Path == null) continue;
                if (p.Path.Count != path.Count) continue; // Different lengths => not equal

                bool equal = true;
                for (int seg = 0; seg < p.Path.Count && equal; seg++)
                {
                    if (!p.Path[seg].Name.Equals(path[seg].Name, StringComparison.InvariantCultureIgnoreCase)) equal = false;
                    // if (p.Path[seg] != path[seg]) equal = false; // Compare strings as objects
                }
                if (equal) return p;
            }
            return null;
        }
        public List<DimPath> GetGreaterPathsStartingWith(DimPath path)
        {
            if (path == null || path.Path == null) return new List<DimPath>();
            return GetGreaterPathsStartingWith(path.Path);
        }
        public List<DimPath> GetGreaterPathsStartingWith(List<CsColumn> path)
        {
            var result = new List<DimPath>();
            foreach (DimPath p in GreaterPaths)
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

            DimPath path = new DimPath("");
            PathEnumerator primPaths = new PathEnumerator(this, DimensionType.IDENTITY_ENTITY);
            foreach (DimPath p in primPaths)
            {
                if (p.Length < 2) continue; // All primitive paths are stored in this set. We need at least 2 segments.

                // Check if this path already exists
                path.Path = p.Path;
                if (GetGreaterPath(path) != null) continue; // Already exists

                string pathName = "__inherited__" + ++pathCounter;

                DimPath newPath = new DimPath(pathName);
                newPath.Path = new List<CsColumn>(p.Path);
                newPath.Name = newPath.ComplexName; // Overwrite previous pathName (so previous is not needed actually)
                newPath.RelationalColumnName = newPath.Name; // It actually will be used for relational queries
                newPath.RelationalFkName = path.RelationalFkName; // Belongs to the same FK
                newPath.RelationalPkName = null;
                //newPath.LesserSet = this;
                //newPath.GreaterSet = p.Path[p.Length - 1].GreaterSet;

                AddGreaterPath(newPath);
            }
        }

        #endregion

        #region Constructors and initializers.

        public Set(string name)
        {
            Id = Guid.NewGuid();

            Name = name;

            GreaterDims = new List<CsColumn>(); // Up arrows
            LesserDims = new List<CsColumn>();

            SuperPaths = new List<DimPath>();
            SubPaths = new List<DimPath>();
            GreaterPaths = new List<DimPath>();
            LesserPaths = new List<DimPath>();

            ProjectDimensions = new List<CsColumn>();
        }

        #endregion

    }

}
