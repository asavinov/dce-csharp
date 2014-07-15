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
        public CsTable LesserSet { get; protected set; }

        /// <summary>
        /// Greater (output) set.
        /// </summary>
        public CsTable GreaterSet { get; protected set; }

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
        public virtual CsColumnData ColumnData { get { return columnData; } }
        protected CsColumnDefinition columnDefinition;
        public virtual CsColumnDefinition ColumnDefinition { get { return columnDefinition; } }

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
            if (ColumnDefinition != null)
            {
                ColumnDefinition.Mapping = mapping;
                columnDefinition.IsGenerating = true;
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
            columnData = new DimEmpty();
            CsTable output = greaterSet;
            if (output == null || string.IsNullOrEmpty(output.Name))
            {
            }
            else if (StringSimilarity.SameTableName(output.Name, "Void"))
            {
            }
            else if (StringSimilarity.SameTableName(output.Name, "Top"))
            {
            }
            else if (StringSimilarity.SameTableName(output.Name, "Bottom")) // Not possible by definition
            {
            }
            else if (StringSimilarity.SameTableName(output.Name, "Root"))
            {
            }
            else if (StringSimilarity.SameTableName(output.Name, "Integer"))
            {
                columnData = new DimPrimitive<int>(this);
            }
            else if (StringSimilarity.SameTableName(output.Name, "Double"))
            {
                columnData = new DimPrimitive<double>(this);
            }
            else if (StringSimilarity.SameTableName(output.Name, "Decimal"))
            {
                columnData = new DimPrimitive<decimal>(this);
            }
            else if (StringSimilarity.SameTableName(output.Name, "String"))
            {
                columnData = new DimPrimitive<string>(this);
            }
            else if (StringSimilarity.SameTableName(output.Name, "Boolean"))
            {
                columnData = new DimPrimitive<bool>(this);
            }
            else if (StringSimilarity.SameTableName(output.Name, "DateTime"))
            {
                columnData = new DimPrimitive<DateTime>(this);
            }
            else if (StringSimilarity.SameTableName(output.Name, "Set"))
            {
            }
            else // User (non-primitive) set
            {
                columnData = new DimPrimitive<int>(this);
            }

            if (columnData is CsColumnDefinition)
            {
                columnDefinition = (CsColumnDefinition)columnData;
            }
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

}
