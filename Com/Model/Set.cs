using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
        public bool IsPrimitive { get { return SuperSet is CsSchema; } } // If its super-set is Top

        //
        // Outputs
        //
        protected List<CsColumn> greaterDims;
        public List<CsColumn> GreaterDims { get { return greaterDims; } } // Outgoing up arrows. Outputs
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

        protected List<CsColumn> lesserDims;
        public List<CsColumn> LesserDims { get { return lesserDims; } } // Incoming arrows. Inputs
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
            for (CsTable set = this; set != null; set = set.SuperSet)
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
            return GreaterDims.FirstOrDefault(d => StringSimilarity.SameColumnName(d.Name, name));
        }

        public CsTable GetTable(string name) { return LesserDims.FirstOrDefault(d => StringSimilarity.SameColumnName(d.LesserSet.Name, name)).LesserSet; }

        public CsTable FindTable(string name)
        {
            CsTable set = null;
            if (StringSimilarity.SameTableName(Name, name))
            {
                set = this;
            }

            foreach (Dim d in SubDims)
            {
                if (set != null) break;
                set = d.LesserSet.FindTable(name);
            }

            return set;
        }

        public CsTableData Data { get { return this; } }

        public CsTableDefinition Definition { get { return this; } }

        #endregion

        #region CsTableData interface

        /// <summary>
        /// How many instances this set has. Cardinality. Set power. Length (height) of instance set.
        /// </summary>
        protected Offset length;
        public Offset Length 
        { 
            get { return length; }
            set // Uniqueness of keys is not (and cannot be) checked and can be broken
            {
                length = value;
                foreach (CsColumn col in GreaterDims)
                {
                    col.Data.Length = value;
                }
            }
        }

        public object GetValue(string name, int offset)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            CsColumn dim = GetGreaterDim(name);
            return dim.Data.GetValue(offset);
        }

        public void SetValue(string name, int offset, object value)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            CsColumn dim = GetGreaterDim(name);
            dim.Data.SetValue(offset, value);
        }

        public Offset Find(CsColumn[] dims, object[] values) // Type of dimensions (super, id, non-id) is not important and is not used
        {
            Debug.Assert(dims != null && values != null && dims.Length == values.Length, "Wrong use: for each dimension, there has to be a value specified.");

            Offset[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (can be quite long)
            bool hasBeenRestricted = false; // For the case where the Length==1, and no key columns are really provided, so we get at the end result.Length==1 which is misleading. Also, this fixes the problem of having no key dimensions.
            for (int i = 0; i < dims.Length; i++)
            {
                hasBeenRestricted = true;
                Offset[] range = dims[i].Data.DeprojectValue(values[i]); // Deproject one value
                result = result.Intersect(range).ToArray(); 
                // OPTIMIZE: Write our own implementation for various operations (intersection etc.). Use the fact that they are ordered.
                // OPTIMIZE: Use statistics for column distribution to choose best order of de-projections. Alternatively, the order of dimensions can be set by the external procedure taking into account statistics. Say, there could be a special utility method like SortDimensionsAccordingDiscriminationFactor or SortDimsForFinding tuples.
                // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion. Maybe store it in a static field as part of last operation.

                if (result.Length == 0) break; // Not found
            }

            if (result.Length == 0) // Not found
            {
                return -1;
            }
            else if (result.Length == 1) // Found single element - return its offset
            {
                if (hasBeenRestricted) return result[0];
                else return -result.Length;
            }
            else // Many elements satisfy these properties (non-unique identities). Use other methods for getting these records (like de-projection)
            {
                return -result.Length;
            }
        }

        public Offset Append(CsColumn[] dims, object[] values) // Identity dims must be set (for uniqueness). Entity dims are also used when appending. Possibility to append (CanAppend) is not checked. 
        {
            Debug.Assert(dims != null && values != null && dims.Length == values.Length, "Wrong use: for each dimension, there has to be a value specified.");
            Debug.Assert(!IsPrimitive, "Wrong use: cannot append to a primitive set. ");

            for (int i = 0; i < dims.Length; i++)
            {
                dims[i].Data.Append(values[i]);
            }

            length++;
            return Length-1;
        }

        public void Remove(Offset input) // Propagation to lesser (referencing) sets is not checked - it is done by removal/nullifying by de-projection (all records that store some value in some function are removed)
        {
            for (int i = 0; i < GreaterDims.Count; i++)
            {
                GreaterDims[i].Data.Remove(input);
            }

            length--;
        }

        public Offset Find(ExprNode expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            // Find the specified tuple but not its nested tuples (if nested tuples have to be found before then use recursive calls, say, a visitor or recursive epxression evaluation)

            Debug.Assert(!IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.Result.TypeTable == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");

            Offset[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (as long as this set length - can be quite long)
            bool hasBeenRestricted = false; // For the case where the Length==1, and no key columns are really provided, so we get at the end result.Length==1 which is misleading. Also, this fixes the problem of having no key dimensions.

            List<CsColumn> dims = new List<CsColumn>();
            dims.AddRange(KeyColumns);
            dims.AddRange(NonkeyColumns);

            foreach (Dim dim in dims) // OPTIMIZE: the order of dimensions matters (use statistics, first dimensins with better filtering). Also, first identity dimensions.
            {
                ExprNode childExpr = expr.GetChild(dim.Name);
                if (childExpr != null)
                {
                    object val = null;
                    val = childExpr.Result.GetValue();

                    hasBeenRestricted = true;
                    Offset[] range = dim.Data.DeprojectValue(val); // Deproject the value
                    result = result.Intersect(range).ToArray(); // Intersect with previous de-projections
                    // OPTIMIZE: Write our own implementation for intersection and other operations. Assume that they are ordered.
                    // OPTIMIZE: Remember the position for the case this value will have to be inserted so we do not have again search for this positin during insertion (optimization)

                    if (result.Length == 0) break; // Not found
                }
            }

            if (result.Length == 0) // Not found
            {
                return -1;
            }
            else if (result.Length == 1) // Found single element - return its offset
            {
                if (hasBeenRestricted) return result[0];
                else return -result.Length;
            }
            else // Many elements satisfy these properties (non-unique identities). Use other methods for getting these records (like de-projection)
            {
                return -result.Length;
            }

           
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
            return 0;
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

        public Offset Append(ExprNode expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
        {
            // Append: append *this* tuple to the set 
            // Greater tuples are not processed  - it is the task of the interpreter. If it is necessary to append (or do something else) with a greater tuple then it has to be done in the interpreter (depending on the operation, action and other parameters)
            // This method is intended for only appending one row while tuple is used only as a data structure (so its usage for interpretation is not used)
            // So this method expects that child nodes have been already evaluated and store the values in the result. 
            // So this method is equivalent to appending using other row representations.
            // The offset of the appended element is stored as a result in this tuple and also returned.
            // If the tuple already exists then it is found and its current offset is returned.
            // It is assumed that child expressions represent dimensions.
            // It is assumed that the column names are resolved.

            Debug.Assert(!IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.Result.TypeTable == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");

            //
            // TODO: Check existence (uniqueness of the identity)
            //

            //
            // Really append a new element to the set
            //
            foreach (Dim dim in GreaterDims) // We must append one value to ALL greater dimensions (possibly null)
            {
                ExprNode childExpr = expr.GetChild(dim.Name); // TODO: replace by accessor by dimension reference (has to be resolved in the tuple)
                object val = null;
                if (childExpr != null) // A tuple contains a subset of all dimensions
                {
                    val = childExpr.Result.GetValue();
                }
                dim.Data.Append(val);
            }

            //
            // TODO: Check other constraints (for example, where constraint). Remove if not satisfies and return status.
            //

            length++;
            return Length - 1;

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

            return 0;
        }

        #endregion

        #region CsTableDefinition

        public TableDefinitionType DefinitionType { get; set; }

        public ExprNode WhereExpression { get; set; }

        public List<CsColumn> GeneratingDimensions // Output tuples of these dimensions are appended to this set (other tuples are excluded). Alternatively, this element must be referenced by at one lesser element COUNT(this<-proj_dim<-(Set)) > 0
        {
            get { return LesserDims.Where(d => d.Definition.IsGenerating).ToList(); }
        }

        public ExprNode OrderbyExpression { get; set; }

        public CsColumnEvaluator GetWhereEvaluator()
        {
            CsColumnEvaluator evaluator;
            evaluator = ExprEvaluator.CreateWhereEvaluator(this);
            return evaluator;
        }

        public void Populate() 
        {
            if (DefinitionType == TableDefinitionType.NONE)
            {
                return; // Nothing to do
            }

            Length = 0; // Empty the table (reset). Maybe should be done in Initialize?

            if (DefinitionType == TableDefinitionType.PRODUCT) // Product of local sets (no project/de-project from another set)
            {
                // - greater with filter (use key dims; de-projection of several greater dims by storing all combinations of their inputs)
                //   - use only Key greater dims for looping. 
                //   - user greater sets input values as constituents for new tuples
                //   - greater dims are populated simultaniously without evaluation (they do not have defs.)

                // Find all local greater key dimensions including the super-dim.
                CsColumn[] dims = KeyColumns.ToArray();
                int dimCount = dims.Length;
                object[] vals = new object[dimCount];

                // Evaluator for where expression
                CsColumnEvaluator eval = null;
                if (Definition.WhereExpression != null)
                {
                    eval = GetWhereEvaluator();
                }

                // For building all combinations
                Offset[] lengths = new Offset[dimCount];
                for (int i = 0; i < dimCount; i++) lengths[i] = dims[i].GreaterSet.Data.Length;

                Offset[] offsets = new Offset[dimCount]; // Here we store the current state of choices for each dimensions
                for (int i = 0; i < dimCount; i++) offsets[i] = -1;

                int top = -1; // The current level/top where we change the offset. Depth of recursion.
                do ++top; while (top < dimCount && lengths[top] == 0);

                // Alternative recursive iteration: http://stackoverflow.com/questions/13655299/c-sharp-most-efficient-way-to-iterate-through-multiple-arrays-list
                while (top >= 0)
                {
                    if (top == dimCount) // New element is ready. Process it.
                    {
                        // Initialize an instance and append it
                        for (int i = 0; i < dimCount; i++)
                        {
                            vals[i] = offsets[i];
                        }
                        Offset input = Append(dims, vals);

                        // Now check if this appended element satsifies the where expression and if not then remove it
                        if (eval != null)
                        {
                            bool satisfies = true;

                            eval.Last();
                            eval.Evaluate();
                            satisfies = (bool)eval.GetResult();

                            if (!satisfies)
                            {
                                Length = Length - 1;
                            }
                        }


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
            else if (DefinitionType == TableDefinitionType.PROJECTION) // There are import dimensions so copy data from another set (projection of another set)
            {
                // - lesser with filter
                //   - use only IsGenerating lesser dims for looping. 
                //   - use thier lesser sets to loop through all their combinations
                //   - !!! each lesser dim is evaluated using its formula that returns some constituent of the new set (or a new element in the case of 1 lesser dim)
                //   - set elements store a combination of lesser dims outputs
                //   - each lesser dim stores the new tuple in its output (alternatively, these dims could be evaluatd after set population - will it really work for multiple lesser dims?)

                // Use only generating lesser dimensions (IsGenerating)
                // Organize a loop on combinations of their inputs
                // For each combination, evaluate all the lesser generating dimensions by finding their outputs
                // Use these outputs to create a new tuple and try to append it to this set
                // If appended or found, then set values of the greater generating dimensions
                // If cannot be added (does not satisfy constraints) then set values of the greater generating dimensions to null

                CsColumn projectDim = Definition.GeneratingDimensions[0];
                CsTable sourceSet = projectDim.LesserSet;
                CsTable targetSet = projectDim.GreaterSet; // this set

                // Prepare the expression from the mapping
                CsColumnEvaluator evaluator = projectDim.Definition.GetColumnEvaluator();

                while (evaluator.Next()) 
                {
                    evaluator.Evaluate();
                }
            }
            else
            {
                throw new NotImplementedException("This table definition type is not implemented and cannot be populated.");
            }

        }

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

        //
        // Dependencies
        //

        public List<CsTable> UsesTables(bool recursive) // This element depends upon
        {
            // Assume that we have just added this table. It uses and depends on other tables. We need to list them.

            List<CsTable> res = new List<CsTable>();

            foreach (CsColumn col in GreaterDims) // If a greater (key) set has changed then this set has to be populated
            {
                if (!col.IsIdentity) continue;
                res.Add(col.GreaterSet);
            }

            foreach (CsColumn col in LesserDims) // If a generating source set has changed then this set has to be populated
            {
                if (!col.Definition.IsGenerating) continue;
                res.Add(col.LesserSet);
            }

            // TODO: Add tables used in the Where expression

            // Recursion
            if (recursive)
            {
                foreach (CsTable tab in res.ToList())
                {
                    var list = tab.Definition.UsesTables(recursive); // Recusrion
                    foreach (CsTable table in list)
                    {
                        Debug.Assert(!res.Contains(table), "Cyclic dependence in tables.");
                        res.Add(table);
                    }
                }
            }

            return res;
        }
        public List<CsTable> IsUsedInTables(bool recursive) // Dependants
        {
            List<CsTable> res = new List<CsTable>();

            foreach (CsColumn col in LesserDims) // If this set has changed then all its lesser (key) sets have to be populated
            {
                if (!col.IsIdentity) continue;
                res.Add(col.LesserSet);
            }

            foreach (CsColumn col in GreaterDims) // If this table has changed then output tables of generating dimensions have to be populated
            {
                if (!col.Definition.IsGenerating) continue;
                res.Add(col.GreaterSet);
            }

            // Recursion
            if (recursive)
            {
                foreach (CsTable tab in res.ToList())
                {
                    var list = tab.Definition.IsUsedInTables(recursive); // Recusrion
                    foreach (CsTable table in list)
                    {
                        Debug.Assert(!res.Contains(table), "Cyclic dependence in tables.");
                        res.Add(table);
                    }
                }
            }

            return res;
        }


        public List<CsColumn> UsesColumns(bool recursive) // This element depends upon
        {
            // Assume that we have just added this table. It uses and depends on other columns. We need to list them.

            List<CsColumn> res = new List<CsColumn>();

            foreach (CsColumn col in LesserDims) // If a generating source column (definition) has changed then this set has to be updated
            {
                if (!col.Definition.IsGenerating) continue;
                res.Add(col);
            }

            // TODO: Add columns used in the Where expression

            // Recursion
            if (recursive)
            {
                foreach (CsColumn col in res.ToList())
                {
                    var list = col.Definition.UsesColumns(recursive); // Recursion
                    foreach (CsColumn column in list)
                    {
                        Debug.Assert(!res.Contains(column), "Cyclic dependence in columns.");
                        res.Add(column);
                    }
                }
            }

            return res;
        }
        public List<CsColumn> IsUsedInColumns(bool recursive) // Dependants
        {
            List<CsColumn> res = new List<CsColumn>();

            foreach (CsColumn col in LesserDims) // If this set has changed then all lesser columns have to be updated
            {
                res.Add(col);
            }

            foreach (CsColumn col in GreaterDims) // If this set has changed then all greater generating columns have to be updated
            {
                if (!col.Definition.IsGenerating) continue;
                res.Add(col);
            }

            // TODO: Find columns with expressions which use this table

            // Recursion
            if (recursive)
            {
                foreach (CsColumn col in res.ToList())
                {
                    var list = col.Definition.IsUsedInColumns(recursive); // Recursion
                    foreach (CsColumn column in list)
                    {
                        Debug.Assert(!res.Contains(column), "Cyclic dependence in columns.");
                        res.Add(column);
                    }
                }
            }

            return res;
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

        #region Constructors and initializers.

        public Set(string name)
        {
            Id = Guid.NewGuid();

            Name = name;

            greaterDims = new List<CsColumn>(); // Up arrows
            lesserDims = new List<CsColumn>();

            DefinitionType = TableDefinitionType.NONE;
        }

        #endregion
    }

    /// <summary>
    /// A relational table.
    /// </summary>
    public class SetRel : Set
    {
        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// We assume that there is only one PK (identity). Otherwise, we need a collection. 
        /// </summary>
        public string RelationalTableName { get; set; }
        public string RelationalPkName { get; set; } // Note that the same field exists also in Dim

        public CsColumn GetGreaterDimByFkName(string name)
        {
            return GreaterDims.FirstOrDefault(d => StringSimilarity.SameColumnName(((DimRel)d).RelationalFkName, name));
        }

        #region Paths = relational attributes

        public List<DimAttribute> SuperPaths { get; private set; }
        public List<DimAttribute> SubPaths { get; private set; }
        public List<DimAttribute> GreaterPaths { get; private set; }
        public List<DimAttribute> LesserPaths { get; private set; }

        public void AddGreaterPath(DimAttribute path)
        {
            Debug.Assert(path.GreaterSet != null && path.LesserSet != null, "Wrong use: path must specify a lesser and greater sets before it can be added to a set.");
            RemoveGreaterPath(path);
            if (path.GreaterSet is SetRel) ((SetRel)path.GreaterSet).LesserPaths.Add(path);
            if (path.LesserSet is SetRel) ((SetRel)path.LesserSet).GreaterPaths.Add(path);
        }
        public void RemoveGreaterPath(DimAttribute path)
        {
            Debug.Assert(path.GreaterSet != null && path.LesserSet != null, "Wrong use: path must specify a lesser and greater sets before it can be removed from a set.");
            if (path.GreaterSet is SetRel) ((SetRel)path.GreaterSet).LesserPaths.Remove(path);
            if (path.LesserSet is SetRel) ((SetRel)path.LesserSet).GreaterPaths.Remove(path);
        }
        public void RemoveGreaterPath(string name)
        {
            DimAttribute path = GetGreaterPath(name);
            if (path != null)
            {
                RemoveGreaterPath(path);
            }
        }
        public DimAttribute GetGreaterPath(string name)
        {
            return GreaterPaths.FirstOrDefault(d => StringSimilarity.SameColumnName(d.Name, name));
        }
        public DimAttribute GetGreaterPathByColumnName(string name)
        {
            return GreaterPaths.FirstOrDefault(d => StringSimilarity.SameColumnName(d.RelationalColumnName, name));
        }
        public DimAttribute GetGreaterPath(DimAttribute path)
        {
            if (path == null || path.Path == null) return null;
            return GetGreaterPath(path.Path);
        }
        public DimAttribute GetGreaterPath(List<CsColumn> path)
        {
            if (path == null) return null;
            foreach (DimAttribute p in GreaterPaths)
            {
                if (p.Path == null) continue;
                if (p.Path.Count != path.Count) continue; // Different lengths => not equal

                bool equal = true;
                for (int seg = 0; seg < p.Path.Count && equal; seg++)
                {
                    if (!StringSimilarity.SameColumnName(p.Path[seg].Name, path[seg].Name)) equal = false;
                    // if (p.Path[seg] != path[seg]) equal = false; // Compare strings as objects
                }
                if (equal) return p;
            }
            return null;
        }
        public List<DimAttribute> GetGreaterPathsStartingWith(DimAttribute path)
        {
            if (path == null || path.Path == null) return new List<DimAttribute>();
            return GetGreaterPathsStartingWith(path.Path);
        }
        public List<DimAttribute> GetGreaterPathsStartingWith(List<CsColumn> path)
        {
            var result = new List<DimAttribute>();
            foreach (DimAttribute p in GreaterPaths)
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

        [System.Obsolete("What was the purpose of this method?", true)]
        public void AddAllNonStoredPaths()
        {
            // The method adds entity (non-PK) columns from referenced (by FK) tables (recursively).
            int pathCounter = 0;

            DimAttribute path = new DimAttribute("");
            PathEnumerator primPaths = new PathEnumerator(this, DimensionType.IDENTITY_ENTITY);
            foreach (DimAttribute p in primPaths)
            {
                if (p.Size < 2) continue; // All primitive paths are stored in this set. We need at least 2 segments.

                // Check if this path already exists
                path.Path = p.Path;
                if (GetGreaterPath(path) != null) continue; // Already exists

                string pathName = "__inherited__" + ++pathCounter;

                DimAttribute newPath = new DimAttribute(pathName);
                newPath.Path = new List<CsColumn>(p.Path);
                newPath.Name = newPath.ColumnNamePath; // Overwrite previous pathName (so previous is not needed actually)
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

        public SetRel(string name) 
            : base(name)
        {
            SuperPaths = new List<DimAttribute>();
            SubPaths = new List<DimAttribute>();
            GreaterPaths = new List<DimAttribute>();
            LesserPaths = new List<DimAttribute>();
        }

        #endregion
    }

    /// <summary>
    /// A table stored as a text file.
    /// </summary>
    public class SetCsv : Set
    {
        /// <summary>
        /// Complete file name for this table (where this table is stored).
        /// </summary>
        public string FilePath { get; set; }

        public string FileName { get { return Path.GetFileNameWithoutExtension(FilePath); } }

        //
        // Storage and format parameters for this table which determine how it is serialized
        //
        public bool HasHeaderRecord { get; set; }
        public string Delimiter { get; set; }
        public CultureInfo CultureInfo { get; set; }
        public Encoding Encoding { get; set; }


        #region Constructors and initializers.

        public SetCsv(string name)
            : base(name)
        {
            HasHeaderRecord = true;
            Delimiter = ",";
            CultureInfo = System.Globalization.CultureInfo.CurrentCulture;
            Encoding = Encoding.UTF8;
        }

        #endregion
    }

}
