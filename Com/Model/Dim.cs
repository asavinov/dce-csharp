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
    public class Dim
    {
        #region Properties

        private static int uniqueId;

        /// <summary>
        /// Unique id within this database or (temporary) query session.  
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// This name is unique within the lesser set.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// These fields can be extracted into some child class if it will be created like relational dimension, path dimension etc.
        /// </summary>
        public string RelationalColumnName { get; set; } // For paths, it is the original column name used in the database (can be moved to a child class if such will be introduced for relational dimensions or for path dimensions). 
        public string RelationalFkName { get; set; } // For dimensions, which were created from FK, it stores the original FK name
        public string RelationalPkName { get; set; } // PK this column belongs to according to the schema

        public virtual Type SystemType
        {
            get { return GreaterSet != null ? GreaterSet.SystemType : null; }
        }

        public virtual int Width // Width of instances. It depends on the implementation (and might not be the same for all dimensions of the greater set). 
        {
            get { return GreaterSet != null ? GreaterSet.Width : 0; }
        }

        public virtual Offset Length // How many instances. 
        {
            get // Dimensions to not have their own instandes and this number if the number of elements in the lesser set (the same for all dimensions of the set).
            {
                return LesserSet != null ? LesserSet.Length : 0;
            }
            protected set // Setter is not for public API - only a whole set length can be changed. This setter is used to reallocate.
            { 
                // Do nothing because this implementation does not manage elements but it can be overriden in sub-classes
            } 
        } 

        /// <summary>
        /// Whether this function is has a primitive range (greater set). 
        /// </summary>
        public bool IsPrimitive { get { return GreaterSet == null ? false : GreaterSet.IsPrimitive; } }

        /// <summary>
        /// Whether this dimension is supposed (able) to have instances. Some dimensions are used for conceptual purposes. 
        /// It is not about having zero instances - it is about the ability to have instances (essentially supporting the corresponding interface for working with instances).
        /// It characterizes and depends on the domain (lesser set). 
        /// </summary>
        public bool IsInstantiable { get { return LesserSet == null ? false : LesserSet.IsInstantiable; } }

        /// <summary>
        /// Whether it is an identity dimension.
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// Reversed dimension has the opposite semantic interpretation (direction). It is used to resolve semantic cycles. 
        /// For example, when a department references its manager then this dimension is makred by this flag. 
        /// One use is when deciding +how to interpret input and output dimensions of sets and lesser/greater sets of dimensions.
        /// </summary>
        public bool IsReversed { get; set; }

        /// <summary>
        /// Whether this function is allowed to store nulls as output values, that is, to have no output assigned to inputs.
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Temporary dimension is discarded after it has been used for computing other dimensions.
        /// It is normally invisible (private) dimension. 
        /// It can be created in the scope of some other dimension, expression or query, and then it is automatically deleted when the process exits this scope.
        /// </summary>
        public bool IsTemporary { get; set; }

        /// <summary>
        /// This dimension belongs to the inclusion hierarchy (super-dimension).
        /// </summary>
        public bool IsSuper { get; set; }

        #endregion

        #region Schema methods.

        /// <summary>
        /// Greater (output) set.
        /// </summary>
        public Set GreaterSet { get; set; }

        /// <summary>
        /// Lesser (input) set. 
        /// </summary>
        public Set LesserSet { get; set; }

        /// <summary>
        /// false if this dimension references the greaer set but is not included into it (not part of the schema).
        /// </summary>
        public virtual bool IsInGreaterSet 
        {
            get
            {
                if (GreaterSet == null) return true;
                var dimList = GreaterSet.LesserDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
        }

        /// <summary>
        /// false if this dimension references the lesser set but is not included into it (not part of the schema).
        /// </summary>
        public virtual bool IsInLesserSet 
        {
            get
            {
                if (LesserSet == null) return true;
                var dimList = LesserSet.GreaterDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
        }

        /// <summary>
        /// true if it is included in both lesser and greater sets. Depends on the dimension type.
        /// </summary>
        public bool IsHanging
        {
            get
            {
                return IsInLesserSet && IsInGreaterSet;
            }
        }

        /// <summary>
        /// Add (attach) to its lesser and greater sets if not added yet. 
        /// Dimension type is important because different dimensions are stored in different collections.
        /// </summary>
        public void Add()
        {
            Add(-1, -1);
        }

        /// <summary>
        /// Add to its lesser and greater sets if not added yet. 
        /// Dimension type is important because different dimensions are stored in different collections.
        /// Ensure that the dimension has the specified indexes. Change indexes if the current position is different from the requested.
        /// </summary>
        public virtual void Add(int lesserSetIndex, int greaterSetIndex = -1)
        {
            if (GreaterSet != null) AddToDimensions(GreaterSet.LesserDims, greaterSetIndex);
            if (LesserSet != null) AddToDimensions(LesserSet.GreaterDims, lesserSetIndex);

            // Notify that a new child has been added
            if (LesserSet != null) LesserSet.NotifyAdd(this);
            if (GreaterSet != null) GreaterSet.NotifyAdd(this);
        }
        protected void AddToDimensions(IList<Dim> dimList, int index = -1) 
        {
            if (index < 0 || index > dimList.Count) index = dimList.Count;
            int current_index = dimList.IndexOf(this);
            if (current_index < 0) // Does not exist
            {
                dimList.Insert(index, this);
            }
            else if (index != current_index) // Exists but has different index
            {
                if (index == dimList.Count) index = dimList.Count - 1;
                dimList.RemoveAt(current_index);
                dimList.Insert(index, this);
            }
        }

        /// <summary>
        /// Remove (detach) from its lesser and greater sets if it is there. Depends on the dimension type.
        /// </summary>
        public virtual void Remove()
        {
            if (GreaterSet != null) GreaterSet.LesserDims.Remove(this);
            if (LesserSet != null) LesserSet.GreaterDims.Remove(this);

            // Notify that a new child has been removed
            if (LesserSet != null) LesserSet.NotifyRemove(this);
            if (GreaterSet != null) GreaterSet.NotifyRemove(this);
        }

        /// <summary>
        /// Remove the specified dimension and add this dimension at the same position.
        /// </summary>
        public virtual void Replace(Dim dim)
        {
            int greaterSetIndex = GreaterSet.LesserDims.IndexOf(dim);
            int lesserSetIndex = LesserSet.GreaterDims.IndexOf(dim);
            dim.Remove();

            this.Add(lesserSetIndex, greaterSetIndex);
        }

        #endregion

        #region Function methods (abstract)

        public object Value { get; set; } // Static value (of the variable) which does not depend on the instance

        public virtual bool IsNull(Offset offset) { return false; } // Check if it is null

        public virtual object GetValue(Offset offset) { return null; } // Returned what is really stored without checking if it is null (it should return NullValue if it is really null). Use IsNull to check if the value is null.

        public virtual void SetValue(Offset offset, object value) { }

        public virtual void NullifyValues() { } // Note that import dimension implement it by removing instances.

        /// <summary>
        /// It is a formula defining a function for this dimension. When evaluated, it returs a value of the greater set for the identity value of the lesser set.
        /// </summary>
        public Expression SelectExpression { get; set; }

        public virtual void ComputeValues() { return; } // Set output values of the function by evaluating an expression (or using other means)

        public virtual void Append(object value) { } // Increment length and set the value (or insert last)

        public virtual void Insert(Offset offset, object value) { }


        public virtual object Aggregate(object values, string function) { return null; } // It is actually static but we cannot use static virtual methods in C#

        public virtual object ProjectValues(Offset[] offsets) { return null; }

        public virtual Offset[] DeprojectValue(object value) { return null; } // Accepts both a single object or an array. Do we need it as public?

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

        public Dim(Set set) // Empty dimension
            : this("", set, set)
        {
        }

        public Dim(Dim dim)
            : this()
        {
            Name = dim.Name;

            IsIdentity = dim.IsIdentity;
            IsReversed = dim.IsReversed;

            LesserSet = dim.LesserSet;
            GreaterSet = dim.GreaterSet;
        }

        public Dim(string name)
            : this(name, null, null)
        {
        }

        public Dim(string name, Set lesserSet, Set greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public Dim(string name, Set lesserSet, Set greaterSet, bool isIdentity, bool isReversed)
            : this()
        {
            Name = name;

            IsIdentity = isIdentity;
            IsReversed = isReversed;

            LesserSet = lesserSet;
            GreaterSet = greaterSet;

            // Parameterize depending on the reserved names: super
            // Parameterize depending on the greater and lesser set type. For example, dimension type must correspond to its greater set type (SetInteger <- DimInteger etc.)
        }

        #endregion

    }

    public enum DimensionType
    {
        INCLUSION, // Both super and sub
        SUPER, // 
        SUB, // 

        POSET, // Both greater and lesser
        GREATER, // 
        LESSER, // 

        IDENTITY_ENTITY, // Both identity and entity
        IDENTITY, //
        ENTITY, // 

        EXPORT,
    }

    public enum DimensionDirection
    {
        GREATER, // Up
        LESSER, // Down, reverse
    }

}
