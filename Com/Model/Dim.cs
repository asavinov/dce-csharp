using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// An abstract dimension with storage methods but without implementation. 
    /// Concrete implementations of the storage depending on the value type are implemented in the extensions which have to used. 
    /// Extensions also can define functions defined via a formula or a query to an external database.
    /// It is only important that a function somehow impplements a mapping from its lesser set to its greater set. 
    /// </summary>
    public class Dim : CsColumn
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
        protected CsTable lesserSet;
        public CsTable LesserSet 
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
        protected CsTable greaterSet;
        public CsTable GreaterSet
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
            if (GreaterSet != null) GreaterSet.LesserDims.Add(this);
            if (LesserSet != null) LesserSet.GreaterDims.Add(this);

            // Notify that a new child has been added
            if (LesserSet != null) ((Set)LesserSet).NotifyAdd(this);
            if (GreaterSet != null) ((Set)GreaterSet).NotifyAdd(this);
        }

        /// <summary>
        /// Remove (detach) from its lesser and greater sets if it is there. Depends on the dimension type.
        /// </summary>
        public virtual void Remove()
        {
            if (GreaterSet != null) GreaterSet.LesserDims.Remove(this);
            if (LesserSet != null) LesserSet.GreaterDims.Remove(this);

            // Notify that a new child has been removed
            if (LesserSet != null) ((Set)LesserSet).NotifyRemove(this);
            if (GreaterSet != null) ((Set)GreaterSet).NotifyRemove(this);
        }


        protected CsColumnData columnData;
        public virtual CsColumnData Data { get { return columnData; } }

        protected CsColumnDefinition columnDefinition;
        public virtual CsColumnDefinition Definition { get { return columnDefinition; } }

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
        public static CsColumnData CreateColumnData(CsTable type, CsColumn column)
        {
            CsColumnData colData = new DimEmpty();

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

        public Dim()
        {
            Id = Guid.NewGuid();
        }

        public Dim(CsTable set) // Empty dimension
            : this("", set, set)
        {
        }

        public Dim(Dim dim)
            : this()
        {
            Name = dim.Name;

            IsIdentity = dim.IsIdentity;

            LesserSet = dim.LesserSet;
            GreaterSet = dim.GreaterSet;
        }

        public Dim(string name)
            : this(name, null, null)
        {
        }

        public Dim(Mapping mapping)
            : this(mapping.SourceSet.Name, mapping.SourceSet, mapping.TargetSet, false, false)
        {
            if (Definition != null)
            {
                Definition.Mapping = mapping;
                columnDefinition.IsGenerating = true;
                if (GreaterSet != null) GreaterSet.Definition.DefinitionType = TableDefinitionType.PROJECTION;
            }
        }

        public Dim(string name, CsTable lesserSet, CsTable greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public Dim(string name, CsTable lesserSet, CsTable greaterSet, bool isIdentity, bool isSuper)
            : this()
        {
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

        public DimRel(string name)
            : this(name, null, null)
        {
        }

        public DimRel(string name, CsTable lesserSet, CsTable greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public DimRel(string name, CsTable lesserSet, CsTable greaterSet, bool isIdentity, bool isSuper)
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

        public DimCsv(string name)
            : this(name, null, null)
        {
        }

        public DimCsv(string name, CsTable lesserSet, CsTable greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public DimCsv(string name, CsTable lesserSet, CsTable greaterSet, bool isIdentity, bool isSuper)
            : base(name, lesserSet, greaterSet, isIdentity, isSuper)
        {
            SampleValues = new List<string>();
            ColumnIndex = -1;
        }
    }

}
