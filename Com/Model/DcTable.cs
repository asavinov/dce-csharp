using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Offset = System.Int32;

namespace Com.Model
{
    public interface DcTable : DcJson // One table object
    {
        /// <summary>
        /// A set name. Note that in the general case a set has an associated structure (concept, type) which may have its own name. 
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Whether it is a primitive set. Primitive sets do not have greater dimensions.
        /// It can depend on other propoerties (it should be clarified) like instantiable, autopopulated, virtual etc.
        /// </summary>
        bool IsPrimitive { get; }

        //
        // Outputs
        //
        List<DcColumn> Columns { get; }

        DcColumn SuperColumn { get; }
        DcTable SuperTable { get; }
        DcSchema Schema { get; }

        //
        // Inputs
        //
        List<DcColumn> InputColumns { get; }

        List<DcColumn> SubColumns { get; }
        List<DcTable> SubTables { get; }
        List<DcTable> AllSubTables { get; }

        //
        // Poset relation
        //

        bool IsSubTable(DcTable parent); // Is subset of the specified table
        bool IsInput(DcTable set); // Is lesser than the specified table
        bool IsLeast { get; } // Has no inputs
        bool IsGreatest { get; } // Has no outputs

        //
        // Names
        //
        DcColumn GetColumn(string name); // Greater column
        DcTable GetTable(string name); // TODO: Greater table/type - not subtable
        DcTable GetSubTable(string name); // Subtable

        DcTableData Data { get; }
        DcTableDefinition Definition { get; }
    }

    public interface DcTableData // It is interface for manipulating data in a table.
    {
        Offset Length { get; set; }

        bool AutoIndex { set; }
        bool Indexed { get; }
        void Reindex();
        
        //
        // Value methods (convenience, probably should be removed and replaced by manual access to dimensions)
        //

        object GetValue(string name, Offset offset);
        void SetValue(string name, Offset offset, object value);

        //
        // Tuple (flat record) methods: append, insert, remove, read, write.
        //
        // Here we use TUPLE and its constituents as primitive types: Reference etc.
        // Column names or references are important. Types (table references or names) are necessary and important. Maybe also flags like Super, Key would be useful. 
        // TUPLE could be used as a set structure specification (e.g., param for set creation).
        //

        Offset Find(DcColumn[] dims, object[] values);
        Offset Append(DcColumn[] dims, object[] values);
        void Remove(int input);

        //
        // Expression (nested record) methods: append, insert, remove, read, write.
        //

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

    public interface DcTableDefinition
    {
        /// <summary>
        /// Specifies kind of formula used to define this table. 
        /// </summary>
        DcTableDefinitionType DefinitionType { get; set; }

        /// <summary>
        /// Constraints on all possible instances. 
        /// Currently, it is written in terms of and is applied to source (already existing) instances - not instances of this set. Only instances satisfying these constraints are used for populating this set. 
        /// In future, we should probabyl apply these constraints to this set elements while the source set has its own constraints.
        /// </summary>
        ExprNode WhereExpr { get; set; } // May store ComColumn which stores a boolean function definition

        /// <summary>
        /// Ordering of the instances. 
        /// Here again we have a choice: it is how source elements are sorted or it is how elements of this set have to be sorted. 
        /// </summary>
        ExprNode OrderbyExpr { get; set; } // Here we should store something like Comparator

        DcIterator GetWhereEvaluator(); // Get an object which is used to compute the where expression according to the formula

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
        void Unpopulate(); // Is not it Length=0? Or nullify all columns?

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //
        List<DcTable> UsesTables(bool recursive); // This element depends upon
        List<DcTable> IsUsedInTables(bool recursive); // Dependants

        List<DcColumn> UsesColumns(bool recursive); // This element depends upon
        List<DcColumn> IsUsedInColumns(bool recursive); // Dependants
    }

    public enum DcTableDefinitionType // Specific types of table formula
    {
        FREE, // No definition for the table (and cannot be defined). 
        // Example: manually created table with primitive dimensions.
        // Maybe we should interpret it as MANUALly entered data (which is stored along with this table definition as a consequence)
        // Another possible interpretation is explicit population/definition by analogy with colums defined as an explicit array

        ANY, // Arbitrary formula without constraints can be provided with a mix of various expression types
        // It is introduced by analogy with columns but for tables we actually do not have definitions - they are always populated from columns
        // Therefore, this option is not currently used

        PRODUCT, // Table contains all combinations of its greater (key) sets satsifying the constraints
        // An important parameter is which dimensions to vary: FREE or IsKey? So we need to define dependency between to be FREE (definition) and to be key (uniqueness constraint)
        // - greater with filter (use key dims; de-projection of several greater dims by storing all combinations of their inputs)
        //   - use only Key greater dims for looping. 
        //   - user greater sets input values as constituents for new tuples
        //   - greater dims are populated simultaniously without evaluation (they do not have defs.)

        PROJECTION, // Table gets its elements from (unique) outputs of some function
        // - lesser with filter
        //   - use only IsGenerating lesser dims for looping. 
        //   - use thier lesser sets to loop through all their combinations
        //   - !!! each lesser dim is evaluated using its formula that returns some constituent of the new set (or a new element in the case of 1 lesser dim)
        //   - set elements store a combination of lesser dims outputs
        //   - each lesser dim stores the new tuple in its output (alternatively, these dims could be evaluatd after set population - will it really work for multiple lesser dims?)

        // Use only generating lesser dimensions (IsGenerating)
        // Organize a loop on combinations of their inputs
        // For each combination, evaluate all the lesser generating dimensions by finding their outputs
        // Use these outputs to create a new tuple and try to append it to this set
        // If appended or found, then set values of the greater generating dimensions
        // If cannot be added (does not satisfy constraints) then set values of the greater generating dimensions to null

        FILTER, // Tables contains a subset of elements from its super-set
    }

}
