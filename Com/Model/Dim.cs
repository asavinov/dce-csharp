using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// An abstract dimension with storage methods but without implementation. 
    /// Concrete implementations of the storage depending on the value type are implemented in the extensions which have to used. 
    /// Extensions also can define functions defined via a formula or a query to an external database.
    /// It is only important that a function somehow impplements a mapping from its lesser set to its greater set. 
    /// </summary>
    public class Dim : ComColumn
    {
        private static int uniqueId;

        /// <summary>
        /// Unique id within this database or (temporary) query session.  
        /// </summary>
        public Guid Id { get; private set; }

        #region CsColumn interface

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether it is an identity dimension.
        /// </summary>
        public bool IsIdentity { get; protected set; }

        /// <summary>
        /// This dimension belongs to the inclusion hierarchy (super-dimension).
        /// </summary>
        public bool IsSuper { get; protected set; }

        /// <summary>
        /// Whether this function is has a primitive range (greater set). 
        /// </summary>
        public bool IsPrimitive { get { return GreaterSet == null ? false : GreaterSet.IsPrimitive; } }

        /// <summary>
        /// Lesser (input) set. 
        /// </summary>
        protected ComTable lesserSet;
        public ComTable LesserSet 
        {
            get { return lesserSet; }
            set 
            {
                if (lesserSet == value) return;
                lesserSet = value; 
            }
        }

        /// <summary>
        /// Greater (output) set.
        /// </summary>
        protected ComTable greaterSet;
        public ComTable GreaterSet
        {
            get { return greaterSet; }
            set
            {
                if (greaterSet == value) return;
                greaterSet = value;
                columnData = CreateColumnData(greaterSet, this);
            }
        }

        /// <summary>
        /// Add (attach) to its lesser and greater sets if not added yet. 
        /// Dimension type is important because different dimensions are stored in different collections.
        /// </summary>
        public virtual void Add()
        {
            if (IsSuper) // Only one super-dim per table can exist
            {
                if (LesserSet != null && LesserSet.SuperColumn != null)
                {
                    LesserSet.SuperColumn.Remove(); // Replace the existing column by the new one
                }
            }

            if (GreaterSet != null) GreaterSet.InputColumns.Add(this);
            if (LesserSet != null) LesserSet.Columns.Add(this);

            // Notify that a new child has been added
            if (LesserSet != null) ((Set)LesserSet).NotifyAdd(this);
            if (GreaterSet != null) ((Set)GreaterSet).NotifyAdd(this);
        }

        /// <summary>
        /// Remove (detach) from its lesser and greater sets if it is there. Depends on the dimension type.
        /// </summary>
        public virtual void Remove()
        {
            if (GreaterSet != null) GreaterSet.InputColumns.Remove(this);
            if (LesserSet != null) LesserSet.Columns.Remove(this);

            // Notify that a new child has been removed
            if (LesserSet != null) ((Set)LesserSet).NotifyRemove(this);
            if (GreaterSet != null) ((Set)GreaterSet).NotifyRemove(this);
        }


        protected ComColumnData columnData;
        public virtual ComColumnData Data { get { return columnData; } }

        protected ComColumnDefinition columnDefinition;
        public virtual ComColumnDefinition Definition { get { return columnDefinition; } }

        #endregion

        #region ComJson serialization

        public virtual void ToJson(JObject json) // Write fields to the json object
        {
            // No super-object

            json["name"] = Name;
            json["key"] = IsIdentity ? "true" : "false";
            json["super"] = IsSuper ? "true" : "false";

            json["lesser_table"] = Utils.CreateJsonRef(LesserSet);
            json["greater_table"] = Utils.CreateJsonRef(GreaterSet);

            // Column definition
            if (Definition != null)
            {
                JObject columnDef = new JObject();

                columnDef["generating"] = Definition.IsGenerating ? "true" : "false";
                columnDef["definition_type"] = (int)Definition.DefinitionType;

                if (Definition.Formula != null)
                {
                    columnDef["formula"] = Utils.CreateJsonFromObject(Definition.Formula);
                    Definition.Formula.ToJson((JObject)columnDef["formula"]);
                }

                if (Definition.Mapping != null)
                {
                    columnDef["mapping"] = Utils.CreateJsonFromObject(Definition.Mapping);
                    Definition.Mapping.ToJson((JObject)columnDef["mapping"]);
                }

                if (Definition.FactTable != null)
                {
                    columnDef["fact_table"] = Utils.CreateJsonRef(Definition.FactTable);
                }

                if (Definition.GroupPaths != null)
                {
                    JArray group_paths = new JArray();
                    foreach (var path in Definition.GroupPaths)
                    {
                        JObject group_path = Utils.CreateJsonFromObject(path);
                        path.ToJson(group_path);
                        group_paths.Add(group_path);
                    }
                    columnDef["group_paths"] = group_paths;
                }

                if (Definition.MeasurePaths != null)
                {
                    JArray measure_paths = new JArray();
                    foreach (var path in Definition.MeasurePaths)
                    {
                        JObject measure_path = Utils.CreateJsonFromObject(path);
                        path.ToJson(measure_path);
                        measure_paths.Add(measure_path);
                    }
                    columnDef["measure_paths"] = measure_paths;
                }

                json["definition"] = columnDef;
            }

        }
        public virtual void FromJson(JObject json, Workspace ws) // Init this object fields by using json object
        {
            // No super-object

            Name = (string)json["name"];
            IsIdentity = json["key"] != null ? StringSimilarity.JsonTrue((string)json["key"]) : false;
            IsSuper = json["super"] != null ? StringSimilarity.JsonTrue((string)json["super"]) : false;

            LesserSet = (ComTable)Utils.ResolveJsonRef((JObject)json["lesser_table"], ws);
            GreaterSet = (ComTable)Utils.ResolveJsonRef((JObject)json["greater_table"], ws);

            // Column definition
            JObject columnDef = (JObject)json["definition"];
            if (columnDef != null && Definition != null)
            {
                Definition.IsGenerating = columnDef["generating"] != null ? StringSimilarity.JsonTrue(columnDef["generating"]) : false;
                Definition.DefinitionType = columnDef["definition_type"] != null ? (ColumnDefinitionType)(int)columnDef["definition_type"] : ColumnDefinitionType.NONE;

                if (columnDef["formula"] != null)
                {
                    ExprNode node = (ExprNode)Utils.CreateObjectFromJson((JObject)columnDef["formula"]);
                    if (node != null)
                    {
                        node.FromJson((JObject)columnDef["formula"], ws);
                        Definition.Formula = node;
                    }
                }

                if (columnDef["mapping"] != null)
                {
                    Mapping map = (Mapping)Utils.CreateObjectFromJson((JObject)columnDef["mapping"]);
                    if (map != null)
                    {
                        map.FromJson((JObject)columnDef["mapping"], ws);
                        Definition.Mapping = map;
                    }
                }

                if (columnDef["fact_table"] != null)
                {
                    ComTable facts = (ComTable)Utils.CreateObjectFromJson((JObject)columnDef["fact_table"]);
                    if (facts != null)
                    {
                        facts.FromJson((JObject)columnDef["fact_table"], ws);
                        Definition.FactTable = facts;
                    }
                }

                if (columnDef["group_paths"] != null)
                {
                    if (Definition.GroupPaths == null) Definition.GroupPaths = new List<DimPath>();
                    foreach (JObject group_path in columnDef["group_paths"])
                    {
                        DimPath path = (DimPath)Utils.CreateObjectFromJson(group_path);
                        if (path != null)
                        {
                            path.FromJson(group_path, ws);
                            Definition.GroupPaths.Add(path);
                        }
                    }
                }

                if (columnDef["measure_paths"] != null)
                {
                    if (Definition.MeasurePaths == null) Definition.MeasurePaths = new List<DimPath>();
                    foreach (JObject measure_path in columnDef["measure_paths"])
                    {
                        DimPath path = (DimPath)Utils.CreateObjectFromJson(measure_path);
                        if (path != null)
                        {
                            path.FromJson(measure_path, ws);
                            Definition.MeasurePaths.Add(path);
                        }
                    }
                }

            }
        
        }

        #endregion

        #region Overriding System.Object and interfaces

        public override string ToString()
        {
            return String.Format("{0}: {1} -> {2}", Name, LesserSet.Name, GreaterSet.Name);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (Object.ReferenceEquals(this, obj)) return true;

            if (obj is List<Dim>)
            {
                // ***
            }

            if (this.GetType() != obj.GetType()) return false;

            Dim dim = (Dim)obj;
            if (Id.Equals(dim.Id)) return true;

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
        public static ComColumnData CreateColumnData(ComTable type, ComColumn column)
        {
            ComColumnData colData = new DimEmpty();

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
                colData = new DimPrimitive<int>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Double"))
            {
                colData = new DimPrimitive<double>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Decimal"))
            {
                colData = new DimPrimitive<decimal>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "String"))
            {
                colData = new DimPrimitive<string>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Boolean"))
            {
                colData = new DimPrimitive<bool>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "DateTime"))
            {
                colData = new DimPrimitive<DateTime>(column);
            }
            else if (StringSimilarity.SameTableName(type.Name, "Set"))
            {
            }
            else // User (non-primitive) set
            {
                colData = new DimPrimitive<int>(column);
            }

            return colData;
        }

        public Dim(Dim dim)
            : this()
        {
            Name = dim.Name;

            IsIdentity = dim.IsIdentity;

            LesserSet = dim.LesserSet;
            GreaterSet = dim.GreaterSet;

            columnData = CreateColumnData(greaterSet, this);
            columnDefinition = new ColumnDefinition(this);
            // TODO: Copy definition
        }

        public Dim(Mapping mapping)
            : this(mapping.SourceSet.Name, mapping.SourceSet, mapping.TargetSet, false, false)
        {
            Definition.Mapping = mapping;
            columnDefinition.IsGenerating = true;
            if (GreaterSet != null) GreaterSet.Definition.DefinitionType = TableDefinitionType.PROJECTION;
        }

        public Dim(ComTable set) // Empty dimension
            : this("", set, set)
        {
        }

        public Dim()
            : this("")
        {
        }

        public Dim(string name)
            : this(name, null, null)
        {
        }

        public Dim(string name, ComTable lesserSet, ComTable greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public Dim(string name, ComTable lesserSet, ComTable greaterSet, bool isIdentity, bool isSuper)
        {
            Id = Guid.NewGuid();

            Name = name;

            IsIdentity = isIdentity;
            IsSuper = isSuper;

            LesserSet = lesserSet;
            GreaterSet = greaterSet;

            //
            // Creae storage for the function and its definition depending on the output set type
            //
            columnData = CreateColumnData(greaterSet, this);
            columnDefinition = new ColumnDefinition(this);
        }

        #endregion

    }

    /// <summary>
    /// Relational dimension representing a foreign key as a whole (without its attributes) or a primitive non-FK attribute. 
    /// </summary>
    public class DimRel : Dim
    {
        /// <summary>
        /// Additional names specific to the relational model and imported from a relational schema. 
        /// </summary>
        public string RelationalFkName { get; set; } // The original FK name this dimension was created from

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Dim

            json["RelationalFkName"] = RelationalFkName;
        }

        public override void FromJson(JObject json, Workspace ws)
        {
            base.FromJson(json, ws); // Dim

            RelationalFkName = (string)json["RelationalFkName"];
        }

        #endregion

        public DimRel()
            : base(null, null, null)
        {
        }

        public DimRel(string name)
            : this(name, null, null)
        {
        }

        public DimRel(string name, ComTable lesserSet, ComTable greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public DimRel(string name, ComTable lesserSet, ComTable greaterSet, bool isIdentity, bool isSuper)
            : base(name, lesserSet, greaterSet, isIdentity, isSuper)
        {
        }
    }

    /// <summary>
    /// Dimension representing a column in a text file. 
    /// </summary>
    public class DimCsv : Dim
    {
        /// <summary>
        /// Sample values. 
        /// </summary>
        public List<string> SampleValues { get; set; }

        public int ColumnIndex { get; set; }

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Dim

            json["ColumnIndex"] = ColumnIndex;
        }

        public override void FromJson(JObject json, Workspace ws)
        {
            base.FromJson(json, ws); // Dim

            ColumnIndex = (int)json["ColumnIndex"];
        }

        #endregion

        public DimCsv()
            : base(null, null, null)
        {
        }

        public DimCsv(string name)
            : this(name, null, null)
        {
        }

        public DimCsv(string name, ComTable lesserSet, ComTable greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public DimCsv(string name, ComTable lesserSet, ComTable greaterSet, bool isIdentity, bool isSuper)
            : base(name, lesserSet, greaterSet, isIdentity, isSuper)
        {
            SampleValues = new List<string>();
            ColumnIndex = -1;
        }
    }

}
