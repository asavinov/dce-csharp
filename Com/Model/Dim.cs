﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Com.Query;

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
        public void Add()
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
        public void Remove()
        {
            if (GreaterSet != null) GreaterSet.LesserDims.Remove(this);
            if (LesserSet != null) LesserSet.GreaterDims.Remove(this);

            // Notify that a new child has been removed
            if (LesserSet != null) ((Set)LesserSet).NotifyRemove(this);
            if (GreaterSet != null) ((Set)GreaterSet).NotifyRemove(this);
        }

        public CsColumnData ColumnData { get { return null; } }
        public CsColumnDefinition ColumnDefinition { get { return null; } }

        #endregion

        #region Data methods (abstract)

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


        public object Value { get; set; } // Static value (of the variable) which does not depend on the instance

        public virtual bool IsNull(Offset offset) { return false; } // Check if it is null

        public virtual object GetValue(Offset offset) { return null; } // Returned what is really stored without checking if it is null (it should return _nullValue if it is really null). Use IsNull to check if the value is null.

        public virtual void SetValue(Offset offset, object value) { }

        public virtual void NullifyValues() { } // Note that import dimension implement it by removing instances.

        public virtual void Append(object value) { } // Increment length and set the value (or insert last)

        public virtual void Insert(Offset offset, object value) { }

        public virtual void UpdateValue(Offset offset, object value, ValueOp updater) { }

        public virtual object Aggregate(object values, string function) { return null; } // It is actually static but we cannot use static virtual methods in C#

        public virtual object ProjectValues(Offset[] offsets) { return null; }

        public virtual Offset[] DeprojectValue(object value) { return null; } // Accepts both a single object or an array. Do we need it as public?

        #endregion

        #region Formula and evaluation

        /// <summary>
        /// It is a formula (expression) defining a function for this dimension. 
        /// When evaluated, it computes a value of the greater set for the identity value of the lesser set.
        /// </summary>
        public Expression SelectExpression { get; set; }

        /// <summary>
        /// One particular type of function specification used for defining mapped dimensions, import specification, copy specification etc.
        /// It defines greater set (nested) tuple in terms of the lesser set (nested) tuple. 
        /// The function computation procedure can transoform this mapping to a normal expression for evaluation in a loop or it can translate it to a join or other target engine formats.
        /// </summary>
        public Mapping Mapping { get; set; }

        public Expression WhereExpression { get; set; } // It describes the domain of the function or where the function returns null independent of other definitions

        // Source (user, non-executable) formula for computing this function consisting of value-operations
        public AstNode FormulaAst { get; set; } // Analogous to SelectExpression
        // Fact set is a set for looping through and providing input for measure and group functions. By default, it is this (lesser) set.

        public Set LoopSet { get; set; } // Dependency on a lesser set and lesser functions
        // It is a translated, optimized and directly executable code (value operatinos) for computing output values given an input value (input is fact set which by default is this set)
        public ValueOp MeasureCode { get; set; } // Input=FactSet. Output as declared by this function output (generaly, as consumed by the accumulator operator). By default, it is an expression for computing this function output given this set input (so normal evaluation). In the simplest case, it is a single call of an existing function.
        public ValueOp GroupCode { get; set; } // Input=FactSet. Output as declared by this function input (this set)
        public ValueOp AccuCode { get; set; } // Accumulator expression which computes a new value by taking into account the current value and a new output. For built-in functions it has a single system procedure call like SUM, AVG etc.
        // Principle: LoopSet.GroupCode + ThisSet.ThisFunc = LoopSet.MeasureCode
        // Principle: if LoopSet == ThisSet then GroupCode = null, ThisFunc = MeasureCode
        public Dim CountDim { get; set; } // Input=ThisSet. This dimension will store group counts

        public List<Dim> Dependencies { get; set; } // Other functions this function directly depends upon. Computed from the definition of this function.
        // Find and store all outputs of this function by evaluating (executing) its definition in a loop for all input elements of the fact set (not necessarily this set)
        public virtual void Eval() { return; }

        public virtual void ComputeValues() { return; } // Set output values of the function by evaluating an expression (or using other means)

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

        #region Relational attribute (TODO: move to a subclass along with related methods. The same for Set class.)

        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// These fields can be extracted into some child class if it will be created like relational dimension, path dimension etc.
        /// </summary>
        public string RelationalColumnName { get; set; } // For paths, it is the original column name used in the database (can be moved to a child class if such will be introduced for relational dimensions or for path dimensions). 
        public string RelationalFkName { get; set; } // For dimensions, which were created from FK, it stores the original FK name
        public string RelationalPkName { get; set; } // PK this column belongs to according to the schema

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

            LesserSet = dim.LesserSet;
            GreaterSet = dim.GreaterSet;
        }

        public Dim(string name)
            : this(name, null, null)
        {
        }

        public Dim(Mapping mapping)
            : this(mapping.SourceSet.Name, mapping.SourceSet, mapping.TargetSet)
        {
            Mapping = mapping;
        }

        public Dim(string name, Set lesserSet, Set greaterSet)
            : this(name, lesserSet, greaterSet, false, false)
        {
        }

        public Dim(string name, Set lesserSet, Set greaterSet, bool isIdentity, bool isSuper)
            : this()
        {
            Name = name;

            IsIdentity = isIdentity;
            IsSuper = isSuper;

            LesserSet = lesserSet;
            GreaterSet = greaterSet;
        }

        #endregion

    }

}
