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

using Newtonsoft.Json.Linq;

using Com.Query;
using Com.Data;
using Com.Utils;

using Rowid = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// A description of a set which may have subsets, geater or lesser sets, and instances. 
    /// A set is characterized by its structure which includes subsets, greater and lesser sets. 
    /// A set is also characterized by width and length of its members. 
    /// And a set provides methods for manipulating its structure and intances. 
    /// </summary>
    public class Set : INotifyCollectionChanged, INotifyPropertyChanged, DcTable, DcTableData, DcTableDefinition
    {
        /// <summary>
        /// Unique set id (in this database) . In C++, this Id field would be used as a reference filed
        /// </summary>
        public Guid Id { get; private set; }

        #region ComTable interface

        public string Name { get; set; }

        public bool IsPrimitive { get { return SuperTable is DcSchema; } } // If its super-set is Top

        //
        // Outputs
        //
        protected List<DcColumn> greaterDims;
        public List<DcColumn> Columns { get { return greaterDims; } } // Outgoing up arrows. Outputs

        public DcColumn SuperColumn { get { return Columns.FirstOrDefault(x => x.IsSuper); } }
        public DcTable SuperTable { get { return SuperColumn != null ? SuperColumn.Output : null; } }
        public DcSchema Schema
        {
            get
            {
                DcTable set = this;
                while (set.SuperTable != null) set = set.SuperTable;
                return set is Schema ? (Schema)set : null; // A set which is not integrated in the schema does not have top
            }
        }

        //
        // Inputs
        //

        protected List<DcColumn> lesserDims;
        public List<DcColumn> InputColumns { get { return lesserDims; } } // Incoming arrows. Inputs

        public List<DcColumn> SubColumns { get { return InputColumns.Where(x => x.IsSuper).ToList(); } }
        public List<DcTable> SubTables { get { return SubColumns.Select(x => x.Input).ToList(); } }
        public List<DcTable> AllSubTables // Should be solved using general enumerator? Other: get all lesser, get all greater
        {
            get 
            {
                List<DcTable> result = new List<DcTable>(SubTables);
                int count = result.Count;
                for (int i = 0; i < count; i++)
                {
                    List<DcTable> subsets = result[i].AllSubTables;
                    if (subsets == null || subsets.Count == 0)
                    {
                        continue;
                    }
                    result.AddRange(subsets);
                }

                return result;
            }
        }

        //
        // Poset relation
        //

        // TODO: Maybe rewrite as one method IsLess with parameter as bit mask {Super, Key, Nonkey}
        // And then define shortcut methods via this general methods. In fact, IsLess *is* already defined via enumeator

        // Return true if this set is included in the specified set, that is, the specified set is a direct or indirect super-set of this set
        public bool IsSubTable(DcTable parent) // IsSub
        {
            for (DcTable set = this; set != null; set = set.SuperTable)
            {
                if (set == parent) return true;
            }
            return false;
        }

        public bool IsInput(DcTable set) // IsLess
        {
            var paths = new PathEnumerator(this, set, DimensionType.IDENTITY_ENTITY);
            return paths.Count() > 0;
        }

        public bool IsLeast { get { return InputColumns.Count(x => x.Input.Schema == x.Output.Schema) == 0; } } // Direct to bottom

        public bool IsGreatest // TODO: Direct to top
        {
            get
            {
                return IsPrimitive || Columns.Count(x => x.Input.Schema == x.Output.Schema) == 0; // All primitive sets are greatest by definition
            }
        }

        //
        // Name methods
        //

        public DcColumn GetColumn(string name)
        {
            return Columns.FirstOrDefault(d => StringSimilarity.SameColumnName(d.Name, name));
        }

        public DcTable GetTable(string name) 
        { 
            DcColumn col = Columns.FirstOrDefault(d => StringSimilarity.SameColumnName(d.Output.Name, name));
            return col == null ? null : col.Input; 
        }

        public DcTable GetSubTable(string name)
        {
            DcTable set = null;
            if (StringSimilarity.SameTableName(Name, name))
            {
                set = this;
            }

            foreach (Dim d in SubColumns)
            {
                if (set != null) break;
                set = d.Input.GetSubTable(name);
            }

            return set;
        }

        public DcTableData Data { get { return this; } }

        public DcTableDefinition Definition { get { return this; } }

        #endregion

        public List<DimPath> GetOutputPaths(Set output) // Differences between this set and the specified set
        {
            if (output == null) return null;
            var paths = new PathEnumerator(this, output, DimensionType.IDENTITY_ENTITY);
            var ret = new List<DimPath>();
            foreach (var p in paths)
            {
                ret.Add(new DimPath(p)); // Create a path for each list of dimensions
            }

            return ret;
        }

        #region ComTableData interface

        /// <summary>
        /// How many instances this set has. Cardinality. Set power. Length (height) of instance set.
        /// </summary>
        protected Rowid length;
        public Rowid Length 
        { 
            get { return length; }
            set // Uniqueness of keys is not (and cannot be) checked and can be broken
            {
                length = value;
                foreach (DcColumn col in Columns)
                {
                    col.Data.Length = value;
                }
            }
        }

        public bool AutoIndex 
        {
            set 
            { 
                foreach(DcColumn column in Columns) 
                {
                    column.Data.AutoIndex = value;
                }
            }
        }
        public bool Indexed
        {
            get
            {
                foreach (DcColumn column in Columns)
                {
                    if (!column.Data.Indexed) return false;
                }
                return true;
            }
        }
        public void Reindex()
        {
            foreach (DcColumn column in Columns)
            {
                column.Data.Reindex();
            }
        }

        public object GetValue(string name, Rowid offset)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            DcColumn col = GetColumn(name);
            return col.Data.GetValue(offset);
        }

        public void SetValue(string name, Rowid offset, object value)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            DcColumn col = GetColumn(name);
            col.Data.SetValue(offset, value);
        }

        public Rowid Find(DcColumn[] dims, object[] values) // Type of dimensions (super, id, non-id) is not important and is not used
        {
            Debug.Assert(dims != null && values != null && dims.Length == values.Length, "Wrong use: for each dimension, there has to be a value specified.");

            Rowid[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (can be quite long)

            bool hasBeenRestricted = false; // For the case where the Length==1, and no key columns are really provided, so we get at the end result.Length==1 which is misleading. Also, this fixes the problem of having no key dimensions.
            for (int i = 0; i < dims.Length; i++)
            {
                hasBeenRestricted = true;
                Rowid[] range = dims[i].Data.Deproject(values[i]); // Deproject one value
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

        public Rowid Append(DcColumn[] dims, object[] values) // Identity dims must be set (for uniqueness). Entity dims are also used when appending. Possibility to append (CanAppend) is not checked. 
        {
            Debug.Assert(dims != null && values != null && dims.Length == values.Length, "Wrong use: for each dimension, there has to be a value specified.");
            Debug.Assert(!IsPrimitive, "Wrong use: cannot append to a primitive set. ");

            for (int i = 0; i < dims.Length; i++)
            {
                dims[i].Data.Append(values[i]);
            }

            length++;
            return length - 1;
        }

        public void Remove(Rowid input) // Propagation to lesser (referencing) sets is not checked - it is done by removal/nullifying by de-projection (all records that store some value in some function are removed)
        {
            foreach (DcColumn col in Columns)
            {
                col.Data.Remove(input);
            }

            length--;
        }

        public virtual Rowid Find(ExprNode expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            // Find the specified tuple but not its nested tuples (if nested tuples have to be found before then use recursive calls, say, a visitor or recursive epxression evaluation)

            Debug.Assert(!IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.Result.TypeTable == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");

            Rowid[] result = Enumerable.Range(0, Length).ToArray(); // All elements of this set (can be quite long)
            bool hasBeenRestricted = false; // For the case where the Length==1, and no key columns are really provided, so we get at the end result.Length==1 which is misleading. Also, this fixes the problem of having no key dimensions.

            List<DcColumn> dims = new List<DcColumn>();
            dims.AddRange(Columns.Where(x => x.IsKey));
            dims.AddRange(Columns.Where(x => !x.IsKey));

            foreach (DcColumn dim in dims) // OPTIMIZE: the order of dimensions matters (use statistics, first dimensins with better filtering). Also, first identity dimensions.
            {
                ExprNode childExpr = expr.GetChild(dim.Name);
                if (childExpr != null)
                {
                    object val = null;
                    val = childExpr.Result.GetValue();

                    hasBeenRestricted = true;
                    Rowid[] range = dim.Data.Deproject(val); // Deproject the value
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
        }

        public virtual bool CanAppend(ExprNode expr) // Determine if this expression (it has to be evaluated) can be added into this set as a new instance
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

        public virtual Rowid Append(ExprNode expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
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
            foreach (DcColumn dim in Columns) // We must append one value to ALL greater dimensions (possibly null)
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
        }

        #endregion

        #region ComTableDefinition

        public DcTableDefinitionType DefinitionType { get; set; }

        public string WhereFormula { get; set; }
        public ExprNode WhereExpr { get; set; }

        public ExprNode OrderbyExpr { get; set; }

        public DcIterator GetWhereEvaluator()
        {
            DcIterator evaluator = new IteratorExpr(this);
            return evaluator;
        }

        public void Populate() 
        {
            if (DefinitionType == DcTableDefinitionType.FREE)
            {
                return; // Nothing to do
            }

            Length = 0;

            if (DefinitionType == DcTableDefinitionType.PRODUCT) // Product of local sets (no project/de-project from another set)
            {
                //
                // Evaluator for where expression which will be used to check each new record before it is added
                //
                DcIterator eval = null;
                if (Definition.WhereExpr != null)
                {
                    eval = GetWhereEvaluator();
                }

                //
                // Find all local greater dimensions to be varied (including the super-dim)
                //
                DcColumn[] dims = Columns.Where(x => x.IsKey).ToArray();
                int dimCount = dims.Length; // Dimensionality - how many free dimensions
                object[] vals = new object[dimCount]; // A record with values for each free dimension being varied

                //
                // The current state of the search procedure
                //
                Rowid[] lengths = new Rowid[dimCount]; // Size of each dimension being varied (how many offsets in each dimension)
                for (int i = 0; i < dimCount; i++) lengths[i] = dims[i].Output.Data.Length;

                Rowid[] offsets = new Rowid[dimCount]; // The current point/offset for each dimensions during search
                for (int i = 0; i < dimCount; i++) offsets[i] = -1;

                int top = -1; // The current level/top where we change the offset. Depth of recursion.
                do ++top; while (top < dimCount && lengths[top] == 0);

                // Alternative recursive iteration: http://stackoverflow.com/questions/13655299/c-sharp-most-efficient-way-to-iterate-through-multiple-arrays-list
                while (top >= 0)
                {
                    if (top == dimCount) // New element is ready. Process it.
                    {
                        // Initialize a record and append it
                        for (int i = 0; i < dimCount; i++)
                        {
                            vals[i] = offsets[i];
                        }
                        Rowid input = Append(dims, vals);

                        // Now check if this appended element satisfies the where expression and if not then remove it
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
                            while (top < dimCount && lengths[top] == 0); // Go up (forward) by skipping empty dimensions
                        }
                        else // Level is finished. Go back.
                        {
                            do { offsets[top--] = -1; }
                            while (top >= 0 && lengths[top] == 0); // Go down (backward) by skipping empty dimensions and reseting 
                        }
                    }
                }

            }
            else if (DefinitionType == DcTableDefinitionType.PROJECTION) // There are import dimensions so copy data from another set (projection of another set)
            {
                DcColumn projectDim = InputColumns.Where(d => d.Definition.IsAppendData).ToList()[0];
                DcTable sourceSet = projectDim.Input;
                DcTable targetSet = projectDim.Output; // this set

                // Delegate to column evaluation - it will add records from column expression
                projectDim.Definition.Evaluate();
            }
            else
            {
                throw new NotImplementedException("This table definition type is not implemented and cannot be populated.");
            }

        }

        public void Unpopulate()
        {
            // TODO: SuperDim.Length = 0;

            foreach(Dim d in Columns) 
            {
                // TODO: d.Length = 0;
            }

            Length = 0;

            return; 
        }

        //
        // Dependencies
        //

        public List<DcTable> UsesTables(bool recursive) // This element depends upon
        {
            // Assume that we have just added this table. It uses and depends on other tables. We need to list them.

            List<DcTable> res = new List<DcTable>();

            foreach (DcColumn col in Columns) // If a greater (key) set has changed then this set has to be populated
            {
                if (!col.IsKey) continue;
                res.Add(col.Output);
            }

            foreach (DcColumn col in InputColumns) // If a generating source set has changed then this set has to be populated
            {
                if (!col.Definition.IsAppendData) continue;
                res.Add(col.Input);
            }

            // TODO: Add tables used in the Where expression

            // Recursion
            if (recursive)
            {
                foreach (DcTable tab in res.ToList())
                {
                    var list = tab.Definition.UsesTables(recursive); // Recusrion
                    foreach (DcTable table in list)
                    {
                        Debug.Assert(!res.Contains(table), "Cyclic dependence in tables.");
                        res.Add(table);
                    }
                }
            }

            return res;
        }
        public List<DcTable> IsUsedInTables(bool recursive) // Dependants
        {
            List<DcTable> res = new List<DcTable>();

            foreach (DcColumn col in InputColumns) // If this set has changed then all its lesser (key) sets have to be populated
            {
                if (!col.IsKey) continue;
                res.Add(col.Input);
            }

            foreach (DcColumn col in Columns) // If this table has changed then output tables of generating dimensions have to be populated
            {
                if (!col.Definition.IsAppendData) continue;
                res.Add(col.Output);
            }

            // Recursion
            if (recursive)
            {
                foreach (DcTable tab in res.ToList())
                {
                    var list = tab.Definition.IsUsedInTables(recursive); // Recusrion
                    foreach (DcTable table in list)
                    {
                        Debug.Assert(!res.Contains(table), "Cyclic dependence in tables.");
                        res.Add(table);
                    }
                }
            }

            return res;
        }


        public List<DcColumn> UsesColumns(bool recursive) // This element depends upon
        {
            // Assume that we have just added this table. It uses and depends on other columns. We need to list them.

            List<DcColumn> res = new List<DcColumn>();

            foreach (DcColumn col in InputColumns) // If a generating source column (definition) has changed then this set has to be updated
            {
                if (!col.Definition.IsAppendData) continue;
                res.Add(col);
            }

            // TODO: Add columns used in the Where expression

            // Recursion
            if (recursive)
            {
                foreach (DcColumn col in res.ToList())
                {
                    var list = col.Definition.UsesColumns(recursive); // Recursion
                    foreach (DcColumn column in list)
                    {
                        Debug.Assert(!res.Contains(column), "Cyclic dependence in columns.");
                        res.Add(column);
                    }
                }
            }

            return res;
        }
        public List<DcColumn> IsUsedInColumns(bool recursive) // Dependants
        {
            List<DcColumn> res = new List<DcColumn>();

            foreach (DcColumn col in InputColumns) // If this set has changed then all lesser columns have to be updated
            {
                res.Add(col);
            }

            foreach (DcColumn col in Columns) // If this set has changed then all greater generating columns have to be updated
            {
                if (!col.Definition.IsAppendData) continue;
                res.Add(col);
            }

            // TODO: Find columns with expressions which use this table

            // Recursion
            if (recursive)
            {
                foreach (DcColumn col in res.ToList())
                {
                    var list = col.Definition.IsUsedInColumns(recursive); // Recursion
                    foreach (DcColumn column in list)
                    {
                        Debug.Assert(!res.Contains(column), "Cyclic dependence in columns.");
                        res.Add(column);
                    }
                }
            }

            return res;
        }
        
        #endregion

        #region ComJson serialization

        public virtual void ToJson(JObject json)
        {
            // No super-object

            json["name"] = Name;

            // Table definition
            if (Definition != null)
            {
                JObject tableDef = new JObject();

                tableDef["definition_type"] = (int)Definition.DefinitionType;

                if (Definition.WhereExpr != null)
                {
                    tableDef["where"] = Utils.CreateJsonFromObject(Definition.WhereExpr);
                    Definition.WhereExpr.ToJson((JObject)tableDef["where"]);
                }

                json["definition"] = tableDef;
            }
        }

        public virtual void FromJson(JObject json, DcWorkspace ws)
        {
            // No super-object

            Name = (string)json["name"];

            // Table definition
            JObject tableDef = (JObject)json["definition"];
            if (tableDef != null && Definition != null)
            {
                Definition.DefinitionType = tableDef["definition_type"] != null ? (DcTableDefinitionType)(int)tableDef["definition_type"] : DcTableDefinitionType.FREE;

                if (tableDef["where"] != null)
                {
                    ExprNode node = (ExprNode)Utils.CreateObjectFromJson((JObject)tableDef["where"]);
                    if (node != null)
                    {
                        node.FromJson((JObject)tableDef["where"], ws);
                        Definition.WhereExpr = node;
                    }
                }
            }
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
        public virtual void NotifyPropertyChanged(String propertyName = "") // Convenience method: notifying all about property change
        {
            OnPropertyChanged(propertyName);
        }

        public override string ToString()
        {
            return String.Format("{0} Columns: {1}", Name, Columns.Count);
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

        public Set()
            : this("")
        {
        }

        public Set(string name)
        {
            Id = Guid.NewGuid();

            Name = name;

            greaterDims = new List<DcColumn>(); // Up arrows
            lesserDims = new List<DcColumn>();

            DefinitionType = DcTableDefinitionType.FREE;
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

        public DcColumn GetGreaterDimByFkName(string name)
        {
            return Columns.FirstOrDefault(d => StringSimilarity.SameColumnName(((DimRel)d).RelationalFkName, name));
        }

        #region Paths = relational attributes

        public List<DimAttribute> SuperPaths { get; private set; }
        public List<DimAttribute> SubPaths { get; private set; }
        public List<DimAttribute> GreaterPaths { get; private set; }
        public List<DimAttribute> LesserPaths { get; private set; }

        public void AddGreaterPath(DimAttribute path)
        {
            Debug.Assert(path.Output != null && path.Input != null, "Wrong use: path must specify a lesser and greater sets before it can be added to a set.");
            RemoveGreaterPath(path);
            if (path.Output is SetRel) ((SetRel)path.Output).LesserPaths.Add(path);
            if (path.Input is SetRel) ((SetRel)path.Input).GreaterPaths.Add(path);
        }
        public void RemoveGreaterPath(DimAttribute path)
        {
            Debug.Assert(path.Output != null && path.Input != null, "Wrong use: path must specify a lesser and greater sets before it can be removed from a set.");
            if (path.Output is SetRel) ((SetRel)path.Output).LesserPaths.Remove(path);
            if (path.Input is SetRel) ((SetRel)path.Input).GreaterPaths.Remove(path);
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
            if (path == null || path.Segments == null) return null;
            return GetGreaterPath(path.Segments);
        }
        public DimAttribute GetGreaterPath(List<DcColumn> path)
        {
            if (path == null) return null;
            foreach (DimAttribute p in GreaterPaths)
            {
                if (p.Segments == null) continue;
                if (p.Segments.Count != path.Count) continue; // Different lengths => not equal

                bool equal = true;
                for (int seg = 0; seg < p.Segments.Count && equal; seg++)
                {
                    if (!StringSimilarity.SameColumnName(p.Segments[seg].Name, path[seg].Name)) equal = false;
                    // if (p.Path[seg] != path[seg]) equal = false; // Compare strings as objects
                }
                if (equal) return p;
            }
            return null;
        }
        public List<DimAttribute> GetGreaterPathsStartingWith(DimAttribute path)
        {
            if (path == null || path.Segments == null) return new List<DimAttribute>();
            return GetGreaterPathsStartingWith(path.Segments);
        }
        public List<DimAttribute> GetGreaterPathsStartingWith(List<DcColumn> path)
        {
            var result = new List<DimAttribute>();
            foreach (DimAttribute p in GreaterPaths)
            {
                if (p.Segments == null) continue;
                if (p.Segments.Count < path.Count) continue; // Too short path (cannot include the input path)
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
                path.Segments = p.Segments;
                if (GetGreaterPath(path) != null) continue; // Already exists

                string pathName = "__inherited__" + ++pathCounter;

                DimAttribute newPath = new DimAttribute(pathName);
                newPath.Segments = new List<DcColumn>(p.Segments);
                newPath.RelationalColumnName = newPath.Name; // It actually will be used for relational queries
                newPath.RelationalFkName = path.RelationalFkName; // Belongs to the same FK
                newPath.RelationalPkName = null;
                //newPath.Input = this;
                //newPath.Output = p.Path[p.Length - 1].Output;

                AddGreaterPath(newPath);
            }
        }

        #endregion

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["RelationalTableName"] = RelationalTableName;
            json["RelationalPkName"] = RelationalPkName;

            // List of greater paths (relational attributes)
            if (GreaterPaths != null)
            {
                JArray greater_paths = new JArray();
                foreach (var path in GreaterPaths)
                {
                    JObject greater_path = Utils.CreateJsonFromObject(path);
                    path.ToJson(greater_path);
                    greater_paths.Add(greater_path);
                }
                json["greater_paths"] = greater_paths;
            }

        }

        public override void FromJson(JObject json, DcWorkspace ws)
        {
            base.FromJson(json, ws); // Set

            RelationalTableName = (string)json["RelationalTableName"];
            RelationalPkName = (string)json["RelationalPkName"];

            // List of greater paths (relational attributes)
            if (json["greater_paths"] != null)
            {
                if (GreaterPaths == null) GreaterPaths = new List<DimAttribute>();
                foreach (JObject greater_path in json["greater_paths"])
                {
                    DimAttribute path = (DimAttribute)Utils.CreateObjectFromJson(greater_path);
                    if (path != null)
                    {
                        path.FromJson(greater_path, ws);
                        GreaterPaths.Add(path);
                    }
                }
            }
        }

        #endregion

        #region Constructors and initializers.

        public SetRel()
            : this("")
        {
        }

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

        public DcColumn[] GetColumnsByIndex() // Return an array of columns with indexes starting from 0 and ending with last index
        {
            var columns = new List<DcColumn>();
            int columnCount = 0;

            foreach (DcColumn col in Columns)
            {
                if (!(col is DimCsv)) continue;

                int colIdx = ((DimCsv)col).ColumnIndex;
                if (colIdx < 0) continue;

                if (colIdx >= columns.Count) // Ensure that this index exists 
                {
                    columns.AddRange(new DcColumn[colIdx - columns.Count + 1]);
                }

                columns[colIdx] = col;
                columnCount = Math.Max(columnCount, colIdx);
            }

            return columns.ToArray();
        }
        public string[] GetColumnNamesByIndex()
        {
            var columns = GetColumnsByIndex();
            var columnNames = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++) columnNames[i] = columns[i].Name;
            return columnNames;
        }

        #region ComTableData interface

        public override Rowid Find(ExprNode expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            return -1; // Not found 
        }

        public override bool CanAppend(ExprNode expr) // Determine if this expression (it has to be evaluated) can be added into this set as a new instance
        {
            return true;
        }

        public override Rowid Append(ExprNode expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
        {
            Debug.Assert(!IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.Result.TypeTable == this, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");


            var columns = GetColumnsByIndex();
            string[] record = new string[columns.Length];

            //
            // Prepare a record with all fields. Here we choose the columns to be written
            //

            for (int i = 0; i < columns.Length; i++) // We must append one value to ALL greater dimensions even if a child expression is absent
            {
                DcColumn col = columns[i];
                ExprNode childExpr = expr.GetChild(col.Name);
                object val = null;
                if (childExpr != null) // Found. Value is present.
                {
                    val = childExpr.Result.GetValue();
                    if (val != null)
                    {
                        record[i] = val.ToString();
                    }
                    else
                    {
                        record[i] = "";
                    }
                }
            }

            // Really append record to the file
            ConnectionCsv conn = ((SchemaCsv)this.Schema).connection;
            conn.WriteNext(record);

            length++;
            return Length - 1;
        }
        
        #endregion

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["file_path"] = FilePath;

            json["HasHeaderRecord"] = this.HasHeaderRecord;
            json["Delimiter"] = this.Delimiter;
            json["CultureInfo"] = this.CultureInfo.Name;
            json["Encoding"] = this.Encoding.EncodingName;
        }

        public override void FromJson(JObject json, DcWorkspace ws)
        {
            base.FromJson(json, ws); // Set

            FilePath = (string)json["file_path"];

            HasHeaderRecord = (bool)json["HasHeaderRecord"];
            Delimiter = (string)json["Delimiter"];
            CultureInfo = new CultureInfo((string)json["CultureInfo"]);

            string encodingName = (string)json["Encoding"];
            if (string.IsNullOrEmpty(encodingName)) Encoding = System.Text.Encoding.Default;
            else if (encodingName.Contains("ASCII")) Encoding = System.Text.Encoding.ASCII;
            else if (encodingName == "Unicode") Encoding = System.Text.Encoding.Unicode;
            else if (encodingName.Contains("UTF-32")) Encoding = System.Text.Encoding.UTF32; // "Unicode (UTF-32)"
            else if (encodingName.Contains("UTF-7")) Encoding = System.Text.Encoding.UTF7; // "Unicode (UTF-7)"
            else if (encodingName.Contains("UTF-8")) Encoding = System.Text.Encoding.UTF8; // "Unicode (UTF-8)"
            else Encoding = System.Text.Encoding.Default;
        }

        #endregion

        #region Constructors and initializers.

        public SetCsv()
            : this("")
        {
        }

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
