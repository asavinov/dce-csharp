using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Utils;
using Com.Data;

using Rowid = System.Int32;

namespace Com.Schema
{
    /// <summary>
    /// An abstract dimension with storage methods but without implementation. 
    /// Concrete implementations of the storage depending on the value type are implemented in the extensions which have to used. 
    /// Extensions also can define functions defined via a formula or a query to an external database.
    /// It is only important that a function somehow impplements a mapping from its lesser set to its greater set. 
    /// </summary>
    public class Column : INotifyPropertyChanged, DcColumn
    {
        private static int uniqueId;

        /// <summary>
        /// Unique id within this database or (temporary) query session.  
        /// </summary>
        public Guid Id { get; private set; }

        #region DcColumn interface

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        public string Name { get; set; }
        protected void ColumnRenamed(string newName)
        {
            DcSpace space = this.Input.Space;
            DcSchema schema = this.Input.Schema;
            DcColumn column = this;

            //
            // Check all elements of the schema that can store column name (tables, columns etc.)
            // Update their definition so that it uses the new name of the specified element
            //
            List<DcTable> tables = space.GetTables(schema); // schema.AllSubTables;
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.Columns)
                {
                    if (col.GetData() == null) continue;
                    DcColumnData data = col.GetData();

                    /* REFACTOR: Here essentially we want to manually find all uses and hence have to use dependencies API
                    if (data.FormulaExpr != null)
                    {
                        nodes = data.FormulaExpr.Find((DcColumn)column);
                        nodes.ForEach(x => x.Name = newName);
                    }
                    */
                }

                // Update table definitions by finding the uses of the specified column
                if (tab.GetData().WhereExpr != null)
                {
                    nodes = tab.GetData().WhereExpr.Find((DcColumn)column);
                    nodes.ForEach(x => x.Name = newName);
                }
            }

            column.Name = newName;
        }

        /// <summary>
        /// Whether it is a key columns.
        /// </summary>
        public bool IsKey { get; set; }

        /// <summary>
        /// This dimension belongs to the inclusion hierarchy (super-dimension).
        /// </summary>
        public bool IsSuper { get; protected set; }

        /// <summary>
        /// Whether this function is has a primitive range (greater set). 
        /// </summary>
        public bool IsPrimitive { get { return Output == null ? false : Output.IsPrimitive; } }

        /// <summary>
        /// Lesser (input) set. 
        /// </summary>
        protected DcTable _input;
        public DcTable Input 
        {
            get { return _input; }
            set 
            {
                if (_input == value) return;
                _input = value; 
            }
        }

        /// <summary>
        /// Greater (output) set.
        /// </summary>
        protected DcTable _output;
        public DcTable Output
        {
            get { return _output; }
            set
            {
                if (_output == value) return;
                _output = value;
                _data = CreateColumnData(value, this);
            }
        }

        protected DcColumnData _data;
        public virtual DcColumnData GetData() { return _data; }

        public virtual DcColumnStatus Status
        {
            // TODO: Status implementation to take into account recirsive dependencies in both directions so that Status for all columns changes if one column is updated (formula or evaluate)

            get
            {
                if (GetData() == null) return DcColumnStatus.Green;

                // Problems with formula translation
                if (!GetData().HasValidSchema) return DcColumnStatus.Red;

                // Dirty data. Evaluation can be performed. 
                if (!GetData().HasValidData) return DcColumnStatus.Yellow;

                // Formula is ok. Data has been evaluated. 
                return DcColumnStatus.Green;
            }
        }

        public void TranslateRecursive()
        {
            // TODO: Translate recursive with status updates etc (as opposed to individual column translate)
            // Use this method when creating/updating columns instead of (lower-level) ColumnData interface which is treated as individual formula translation

            // Normally, we translate a formula automatically after each change. 
            // However, if it fails (the column is red), and then we create/change another column which depends on it, then it also cannot be translated. 
            // So the new formula is correct (it can be parsed and bound) but it is red recursively because the necessary column is red. 

        }
        public void EvaluateRecursive()
        {
            // TODO: Evaluate recursive with status updates etc (as opposed to individual column evalutate)
            // Use this method when evaluating columns instead of (lower-level) ColumnData interface which is treated as individual formula translation

            // Normally, we evaluate formulas manually (if they are not red). It is also possible to trigger evaluation automatically.
            // All direct necessary columns must be already green (successfully evaluated). 
            // If they are not, then we need to try to evaluate them recursively (if we have such a flag) or mark this column accordingly (either red, or a new color). 

        }
        public List<List<DcColumn>> GetDependencies()
        {
            // The first list has independent tables which do not have incoming columns (remote or product)
            // Each next list has columns which can be evaluted after the previous list
            // After the last list, this column can be evaluated. 

            var res = new List<List<DcColumn>>();
            res.Add(new List<DcColumn>(new DcColumn[] { this })); // Start from this column

            while (true)
            {
                // Compute new dependencies for each column in the previous dependencies 
                var nextDeps = new List<DcColumn>();
                var prevDeps = res[res.Count - 1];
                foreach (DcColumn col in prevDeps)
                {
                    List<DcColumn> newDeps = GetDirectDependencies();
                    nextDeps.AddRange(newDeps.Except(nextDeps)); // Add only new columns
                }

                if (nextDeps.Count > 0)
                {
                    // TODO: Here we need to check for possible cycles if a new column exists among previous columns
                    res.Add(nextDeps);
                }
                else
                {
                    break; // No dependencies anymore
                }
            }

            return res;
        }
        public List<DcColumn> GetDirectDependencies()
        {
            DcColumnData columnData = GetData();
            var res = new List<DcColumn>();

            if (columnData == null || string.IsNullOrEmpty(columnData.Formula)) // Free columns with no formula
            {
                //
                // Product column. No input column with formula (even indirectly). 
                // There is no any lesser column which writes to this column. 
                // 1. [product is flat - currently it is so implemented] We can assume that we depend on greater tables and hence it our task to fill them. 
                //    The product operation works only at one level (flat) and it can work/be started only if all greater tables are green.
                // 2. [product is hierarchical like tuple append] We can think of it as equivalent to appending *all* tuples which will simultaniously fill greater tables before. 
                //    So the product procedure itself can recursievely fill the greater table which are not filled. 
                //    Note that appending tuples does the same (recursive filling) but for each individual tuple

                //
                // Depends on input column with formula (also indirectly). 
                // There is a lesser column which writes/influences this column
                // Evaluation of lesser (append) columns can result in green status for many (covered) columns including this one

            }
            else // Formula uses/reads other (this.greater) columns and writes/fills (output.greater) columns 
            {
                List<DcColumn> usesColumns = columnData.UsesColumns();

                // Depends on this.greater columns
                List<DcColumn> readsColumns;


                // Depends on this.lesser columns (aggregation)

            }

            return res;
        }

        #endregion

        #region DcJson serialization

        public virtual void ToJson(JObject json) // Write fields to the json object
        {
            // No super-object

            json["name"] = Name;
            json["key"] = IsKey ? "true" : "false";
            json["super"] = IsSuper ? "true" : "false";

            json["lesser_table"] = Utils.CreateJsonRef(Input);
            json["greater_table"] = Utils.CreateJsonRef(Output);

        }
        public virtual void FromJson(JObject json, DcSpace ws) // Init this object fields by using json object
        {
            // No super-object

            Name = (string)json["name"];
            IsKey = json["key"] != null ? StringSimilarity.JsonTrue((string)json["key"]) : false;
            IsSuper = json["super"] != null ? StringSimilarity.JsonTrue((string)json["super"]) : false;

            Input = (DcTable)Utils.ResolveJsonRef((JObject)json["lesser_table"], ws);
            Output = (DcTable)Utils.ResolveJsonRef((JObject)json["greater_table"], ws);

        }

        #endregion

        #region Overriding System.Object and interfaces

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
            return String.Format("{0}: {1} -> {2}", Name, Input.Name, Output.Name);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (Object.ReferenceEquals(this, obj)) return true;

            if (obj is List<Column>)
            {
                // ***
            }

            if (this.GetType() != obj.GetType()) return false;

            Column col = (Column)obj;
            if (Id.Equals(col.Id)) return true;

            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        #endregion

        #region Constructors and initializers.

        /// <summary>
        /// Creae storage for the function and its definition depending on the output set type.
        /// </summary>
        /// <returns></returns>
        public static DcColumnData CreateColumnData(DcTable type, DcColumn column)
        {
            DcColumnData colData = new ColumnDataEmpty();

            /*
            if (column.Input != null && column.Input.Schema != null && column.Input.Schema.GetType() != typeof(Schema)) // Import col
            {
            }
            else if (column.Output != null && column.Output.Schema != null && column.Output.Schema.GetType() != typeof(Schema)) // Output col
            {
            }
            */
            if (column.Input == null || column.Output == null)
            {
            }
            if (type == null || string.IsNullOrEmpty(type.Name))
            {
            }
            else if (StringSimilarity.SameTableName(type.Name, "Void"))
            {
            }
            else if (StringSimilarity.SameTableName(type.Name, "Top"))
            {
            }
            else if (StringSimilarity.SameTableName(type.Name, "Bottom")) // Not possible by definition
            {
            }
            else if (StringSimilarity.SameTableName(type.Name, "Root"))
            {
            }
            else if (StringSimilarity.SameTableName(type.Name, "Integer"))
            {
                colData = new ColumnData<int>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Double"))
            {
                colData = new ColumnData<double>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Decimal"))
            {
                colData = new ColumnData<decimal>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "String"))
            {
                colData = new ColumnData<string>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Boolean"))
            {
                colData = new ColumnData<bool>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "DateTime"))
            {
                colData = new ColumnData<DateTime>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Set"))
            {
            }
            else // User (non-primitive) set
            {
                colData = new ColumnData<int>(column);
            }

            return colData;
        }

        public Column(Column col)
            : this()
        {
            Name = col.Name;

            IsKey = col.IsKey;

            Input = col.Input;
            Output = col.Output;

            _data = CreateColumnData(_output, this);
        }

        public Column(DcTable tab) // Empty column
            : this("", tab, tab)
        {
        }

        public Column()
            : this("")
        {
        }

        public Column(string name)
            : this(name, null, null)
        {
        }

        public Column(string name, DcTable input, DcTable output)
            : this(name, input, output, false, false)
        {
        }

        public Column(string name, DcTable input, DcTable output, bool isKey, bool isSuper)
        {
            Id = Guid.NewGuid();

            Name = name;

            IsKey = isKey;
            IsSuper = isSuper;

            Input = input;
            Output = output;

            //
            // Creae storage for the function and its definition depending on the output set type
            //
            _data = CreateColumnData(output, this);
            _data.Translate();
        }

        #endregion

    }

}
