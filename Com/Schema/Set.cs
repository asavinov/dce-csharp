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

using Com.Utils;
using Com.Data;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Schema
{
    /// <summary>
    /// A description of a set which may have subsets, geater or lesser sets, and instances. 
    /// A set is characterized by its structure which includes subsets, greater and lesser sets. 
    /// A set is also characterized by width and length of its members. 
    /// And a set provides methods for manipulating its structure and intances. 
    /// </summary>
    public class Set : INotifyCollectionChanged, INotifyPropertyChanged, DcTable, DcTableData 
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

        public DcTableData GetData() { return this; }

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

        #region DcTableData interface

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
                    col.GetData().Length = value;
                }
            }
        }

        public bool AutoIndex 
        {
            set 
            { 
                foreach(DcColumn column in Columns) 
                {
                    column.GetData().AutoIndex = value;
                }
            }
        }
        public bool Indexed
        {
            get
            {
                foreach (DcColumn column in Columns)
                {
                    if (!column.GetData().Indexed) return false;
                }
                return true;
            }
        }
        public void Reindex()
        {
            foreach (DcColumn column in Columns)
            {
                column.GetData().Reindex();
            }
        }

        public virtual DcTableReader GetTableReader()
        {
            return new TableReader(this);
        }

        public virtual DcTableWriter GetTableWriter()
        {
            return new TableWriter(this);
        }

        #endregion

        #region The former DcTableDefinition - Now part of DcTableData

        public TableDefinitionType DefinitionType {
            get
            {
                if (IsPrimitive) return TableDefinitionType.FREE;

                // Try to find incoming generating (append) columns. If they exist then table instances are populated as this dimension output tuples.
                List<DcColumn> inColumns = InputColumns.Where(d => d.GetData().GetDefinition().IsAppendData).ToList();
                if(inColumns != null && inColumns.Count > 0)
                {
                    return TableDefinitionType.PROJECTION;
                }

                // Try to find outgoing key non-primitive columns. If they exist then table instances are populated as their combinations.
                List<DcColumn> outColumns = Columns.Where(x => x.IsKey && !x.IsPrimitive).ToList();
                if(outColumns != null && outColumns.Count > 0)
                {
                    return TableDefinitionType.PRODUCT;
                }

                // No instances can be created automatically. 
                return TableDefinitionType.FREE;
            }
        }

        protected string whereFormula;
        public string WhereFormula
        {
            get { return whereFormula; }
            set
            {
                whereFormula = value;

                if (string.IsNullOrWhiteSpace(whereFormula)) return;

                ExprBuilder exprBuilder = new ExprBuilder();
                ExprNode expr = exprBuilder.Build(whereFormula);

                WhereExpr = expr;
            }
        }
        public ExprNode WhereExpr { get; set; }

        public string OrderbyFormula { get; set; }

        public void Populate() 
        {
            if (DefinitionType == TableDefinitionType.FREE)
            {
                return; // Nothing to do
            }

            Length = 0;

            if (DefinitionType == TableDefinitionType.PROJECTION) // There are import dimensions so copy data from another set (projection of another set)
            {
                List<DcColumn> inColumns = InputColumns.Where(d => d.GetData().GetDefinition().IsAppendData).ToList();

                foreach(DcColumn inColumn in inColumns)
                {
                    inColumn.GetData().GetDefinition().Evaluate(); // Delegate to column evaluation - it will add records from column expression
                }
            }
            else if (DefinitionType == TableDefinitionType.PRODUCT) // Product of local sets (no project/de-project from another set)
            {
                // Input variable for where formula
                DcVariable thisVariable = new Variable(this.Schema.Name, this.Name, "this");
                thisVariable.TypeSchema = this.Schema;
                thisVariable.TypeTable = this;

                // Evaluator expression for where formula
                ExprNode outputExpr = this.WhereExpr;
                if(outputExpr != null)
                {
                    outputExpr.OutputVariable.SchemaName = this.Schema.Name;
                    outputExpr.OutputVariable.TypeName = "Boolean";
                    outputExpr.OutputVariable.TypeSchema = this.Schema;
                    outputExpr.OutputVariable.TypeTable = this.Schema.GetPrimitive("Boolean");
                    outputExpr.Resolve(this.Schema.Workspace, new List<DcVariable>() { thisVariable });

                    outputExpr.EvaluateBegin();
                }

                DcTableWriter tableWriter = this.GetTableWriter();
                tableWriter.Open();

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
                for (int i = 0; i < dimCount; i++) lengths[i] = dims[i].Output.GetData().Length;

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
                        Rowid input = tableWriter.Append(dims, vals);

                        //
                        // Now check if this appended element satisfies the where expression and if not then remove it
                        //
                        if (outputExpr != null)
                        {
                            // Set 'this' variable to the last elements (that has been just appended) which will be read by the expression
                            thisVariable.SetValue(this.GetData().Length - 1);

                            // Evaluate expression
                            outputExpr.Evaluate();

                            bool satisfies = (bool)outputExpr.OutputVariable.GetValue();

                            if (!satisfies)
                            {
                                Length = Length - 1; // Remove elements
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

                if(tableWriter != null)
                {
                    tableWriter.Close();
                }
                if (outputExpr != null)
                {
                    outputExpr.EvaluateEnd();
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
                if (!col.GetData().GetDefinition().IsAppendData) continue;
                res.Add(col.Input);
            }

            // TODO: Add tables used in the Where expression

            // Recursion
            if (recursive)
            {
                foreach (DcTable tab in res.ToList())
                {
                    var list = tab.GetData().UsesTables(recursive); // Recusrion
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
                if (!col.GetData().GetDefinition().IsAppendData) continue;
                res.Add(col.Output);
            }

            // Recursion
            if (recursive)
            {
                foreach (DcTable tab in res.ToList())
                {
                    var list = tab.GetData().IsUsedInTables(recursive); // Recusrion
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
                if (!col.GetData().GetDefinition().IsAppendData) continue;
                res.Add(col);
            }

            // TODO: Add columns used in the Where expression

            // Recursion
            if (recursive)
            {
                foreach (DcColumn col in res.ToList())
                {
                    var list = col.GetData().GetDefinition().UsesColumns(recursive); // Recursion
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
                if (!col.GetData().GetDefinition().IsAppendData) continue;
                res.Add(col);
            }

            // TODO: Find columns with expressions which use this table

            // Recursion
            if (recursive)
            {
                foreach (DcColumn col in res.ToList())
                {
                    var list = col.GetData().GetDefinition().IsUsedInColumns(recursive); // Recursion
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

        #region DcJson serialization

        public virtual void ToJson(JObject json)
        {
            // No super-object

            json["name"] = Name;

            json["where"] = WhereFormula;
        }

        public virtual void FromJson(JObject json, DcWorkspace ws)
        {
            // No super-object

            Name = (string)json["name"];

            WhereFormula = (string)json["where"];
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
        }

        #endregion
    }

}
