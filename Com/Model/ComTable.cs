using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Offset = System.Int32;

namespace Com.Model
{
    public interface ComTable // One table object
    {
        string Name { get; set; }

        bool IsPrimitive { get; }

        //
        // Outputs
        //
        List<ComColumn> GreaterDims { get; }
        ComColumn SuperDim { get; }
        List<ComColumn> KeyColumns { get; }
        List<ComColumn> NonkeyColumns { get; }
        ComSchema Top { get; }
        List<ComTable> GetGreaterSets();
        ComTable SuperSet { get; }

        //
        // Inputs
        //

        List<ComColumn> LesserDims { get; }
        List<ComColumn> SubDims { get; }
        List<ComTable> SubSets { get; }
        List<ComTable> GetAllSubsets();

        //
        // Poset relation
        //

        bool IsIn(ComTable parent);
        bool IsLesser(ComTable set);
        bool IsLeast { get; }
        bool IsGreatest { get; }

        //
        // Names
        //
        ComColumn GetGreaterDim(string name);
        ComTable GetTable(string name);
        ComTable FindTable(string name);

        ComTableData Data { get; }
        ComTableDefinition Definition { get; }
    }

    public interface ComTableData // It is interface for manipulating data in a table.
    {
        Offset Length { get; set; }

        //
        // Value methods (convenience, probably should be removed and replaced by manual access to dimensions)
        //

        object GetValue(string name, int offset);
        void SetValue(string name, int offset, object value);

        //
        // Tuple methods: append, insert, remove, read, write.
        //
        // Here we use TUPLE and its constituents as primitive types: Reference etc.
        // Column names or references are important. Types (table references or names) are necessary and important. Maybe also flags like Super, Key would be useful. 
        // TUPLE could be used as a set structure specification (e.g., param for set creation).
        //

        Offset Find(ComColumn[] dims, object[] values);
        Offset Append(ComColumn[] dims, object[] values);
        void Remove(int input);

        Offset Find(ExprNode expr);
        bool CanAppend(ExprNode expr);
        Offset Append(ExprNode expr);

        //Offset FindTuple(CsRecord record); // If many records can satisfy then another method has to be used. What is many found? Maybe return negative number with the number of records (-1 or Length means not found, positive means found 1)? 
        //void InsertTuple(Offset input, CsRecord record); // All keys are required? Are non-keys allowed?
        //void RemoveTuple(Offset input); // We use it to remove a tuple that does not satisfy filter constraints. Note that filter constraints can use only data that is loaded (some columns will be computed later). So the filter predicate has to be validated for sets which are projected/loaded or all other functions have to be evaluated during set population. 

        //
        // Typed data manipulation methods (do we need this especially taking into account that we will mapping of data types with conversion)
        // Here we need an interface like ResultSet in JDBC with all possible types
        // Alternative: maybe define these methos in the SetRemote class where we will have one class for manually entering elements
        //
    }

    public interface ComTableDefinition
    {
        /// <summary>
        /// Specifies kind of formula used to define this table. 
        /// </summary>
        TableDefinitionType DefinitionType { get; set; }

        /// <summary>
        /// Constraints on all possible instances. 
        /// Currently, it is written in terms of and is applied to source (already existing) instances - not instances of this set. Only instances satisfying these constraints are used for populating this set. 
        /// In future, we should probabyl apply these constraints to this set elements while the source set has its own constraints.
        /// </summary>
        ExprNode WhereExpression { get; set; } // May store CsColumn which stores a boolean function definition

        List<ComColumn> GeneratingDimensions { get; }

        /// <summary>
        /// Ordering of the instances. 
        /// Here again we have a choice: it is how source elements are sorted or it is how elements of this set have to be sorted. 
        /// </summary>
        ExprNode OrderbyExpression { get; set; } // Here we should store something like Comparator

        ComColumnEvaluator GetWhereEvaluator(); // Get an object which is used to compute the where expression according to the formula

        /// <summary>
        /// Create all instances of this set. 
        /// Notes:
        /// - (principle): never change any set - neither lesser nor greater (even in the case of generating/projection dimensions)
        /// - Population means produce a subset of all possible elements where all possible elements are defined by greater dimensions.
        /// - There are two ways to restrict all possible elements: where predicate, and generating/projection lesser dimensions (equivalently, referencing from some lesser set by the generating dimensions).
        /// - Accordingly, there are two algorithms: produce all combinations of greater elements (for identity dimensions), and produce all combinations of the lesser elements (for generating dimensions).
        /// </summary>
        void Populate();
        /// <summary>
        /// Remove all instances.
        /// </summary>
        void Unpopulate(); // Is not it Length=0?

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //
        List<ComTable> UsesTables(bool recursive); // This element depends upon
        List<ComTable> IsUsedInTables(bool recursive); // Dependants

        List<ComColumn> UsesColumns(bool recursive); // This element depends upon
        List<ComColumn> IsUsedInColumns(bool recursive); // Dependants
    }

    public enum TableDefinitionType // Specific types of table formula
    {
        NONE, // No definition for the table (and cannot be defined). Example: manually created table with primitive dimensions.
        ANY, // Arbitrary formula without constraints can be provided with a mix of various expression types
        PROJECTION, // Table gets its elements from (unique) outputs of some function
        PRODUCT, // Table contains all combinations of its greater (key) sets satsifying the constraints
        FILTER, // Tables contains a subset of elements from its super-set
    }

}
