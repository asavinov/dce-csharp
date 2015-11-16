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

        #region ComColumn interface

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether it is an identity dimension.
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

        /// <summary>
        /// Add (attach) to its lesser and greater sets if not added yet. 
        /// Dimension type is important because different columns are stored in different collections.
        /// </summary>
        public virtual void Add()
        {
            if (IsSuper) // Only one super-dim per table can exist
            {
                if (Input != null && Input.SuperColumn != null)
                {
                    Input.SuperColumn.Remove(); // Replace the existing column by the new one
                }
            }

            if (Output != null) Output.InputColumns.Add(this);
            if (Input != null) Input.Columns.Add(this);

            // Notify that a new child has been added
            if (Input != null) ((Table)Input).NotifyAdd(this);
            if (Output != null) ((Table)Output).NotifyAdd(this);
        }

        /// <summary>
        /// Remove (detach) from its lesser and greater sets if it is there. Depends on the dimension type.
        /// </summary>
        public virtual void Remove()
        {
            if (Output != null) Output.InputColumns.Remove(this);
            if (Input != null) Input.Columns.Remove(this);

            // Notify that a new child has been removed
            if (Input != null) ((Table)Input).NotifyRemove(this);
            if (Output != null) ((Table)Output).NotifyRemove(this);
        }


        protected DcColumnData _data;
        public virtual DcColumnData GetData() { return _data; }

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

        public Column(string name, DcTable input, DcTable output, bool isIdentity, bool isSuper)
        {
            Id = Guid.NewGuid();

            Name = name;

            IsKey = isIdentity;
            IsSuper = isSuper;

            Input = input;
            Output = output;

            //
            // Creae storage for the function and its definition depending on the output set type
            //
            _data = CreateColumnData(output, this);
        }

        #endregion

    }

}
