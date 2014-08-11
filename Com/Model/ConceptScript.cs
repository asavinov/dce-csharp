using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Com.Query;

using Offset = System.Int32;

namespace Com.Model
{
    public class ConceptScript
    {
        public string N { get; set; }

        public CsSchema schema { get; set; }

        public ConceptScript(string name)
        {
            N = name;
        }
    }

    public interface CsTable // One table object
    {
        string Name { get; set; }

        bool IsPrimitive { get; }

        //
        // Outputs
        //
        List<CsColumn> GreaterDims { get; }
        CsColumn SuperDim { get; }
        List<CsColumn> KeyColumns { get; }
        List<CsColumn> NonkeyColumns { get; }
        CsSchema Top { get; }
        List<CsTable> GetGreaterSets();
        CsTable SuperSet { get; }

        //
        // Inputs
        //

        List<CsColumn> LesserDims { get; }
        List<CsColumn> SubDims { get; }
        List<CsTable> SubSets { get; }
        List<CsTable> GetAllSubsets();

        //
        // Poset relation
        //

        bool IsIn(CsTable parent);
        bool IsLesser(CsTable set);
        bool IsLeast { get; }
        bool IsGreatest { get; }

        //
        // Names
        //
        CsColumn GetGreaterDim(string name);
        CsTable GetTable(string name);
        CsTable FindTable(string name);

        CsTableData Data { get; }
        CsTableDefinition Definition { get; }
    }

    public interface CsTableData // It is interface for manipulating data in a table.
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

        Offset Find(CsColumn[] dims, object[] values);
        Offset Append(CsColumn[] dims, object[] values);
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

    public interface CsTableDefinition
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

        List<CsColumn> GeneratingDimensions { get; }

        /// <summary>
        /// Ordering of the instances. 
        /// Here again we have a choice: it is how source elements are sorted or it is how elements of this set have to be sorted. 
        /// </summary>
        ExprNode OrderbyExpression { get; set; } // Here we should store something like Comparator

        CsColumnEvaluator GetWhereEvaluator(); // Get an object which is used to compute the where expression according to the formula

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
        List<CsTable> GetPreviousTables(bool recursive); // This element depends upon
        List<CsTable> GetNextTables(bool recursive); // Dependants

        List<CsColumn> GetPreviousColumns(bool recursive); // This element depends upon
        List<CsColumn> GetNextColumns(bool recursive); // Dependants
    }

    public enum TableDefinitionType // Specific types of table formula
    {
        NONE, // No definition for the table (and cannot be defined). Example: manually created table with primitive dimensions.
        ANY, // Arbitrary formula without constraints can be provided with a mix of various expression types
        PROJECTION, // Table gets its elements from (unique) outputs of some function
        PRODUCT, // Table contains all combinations of its greater (key) sets satsifying the constraints
        FILTER, // Tables contains a subset of elements from its super-set
    }

    public interface CsSchema : CsTable
    {
        CsTable GetPrimitive(string dataType);
        CsTable Root { get; } // Convenience

        //
        // Factories for tables and columns
        //
        
        CsTable CreateTable(string name);
        CsTable AddTable(CsTable table, CsTable parent, string superName);
        CsTable RemoveTable(CsTable table);

        CsColumn CreateColumn(string name, CsTable input, CsTable output, bool isKey);
    }

    public interface CsConnection : CsSchema
    {
    }

    public interface CsColumn // One column object
    {
        string Name { get; set; }

        bool IsIdentity { get; }
        bool IsSuper { get; } // Changing this property may influence storage type
        // Other properties: isNullable, isPrimitive, IsInstantiable (is supposed/able to have instances = lesser set instantiable), isTemporary
        bool IsPrimitive { get; }

        // Note: Set property works only for handing dims. For connected dims, a dim has to be disconnected first, then change its lesser/greater set and finally added again.
        CsTable LesserSet { get; set; }
        CsTable GreaterSet { get; set; }

        void Add(); // Add to schema
        void Remove(); // Remove from schema

        CsColumnData Data { get; }
        CsColumnDefinition Definition { get; }
    }

    public interface CsColumnData // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
    {
        Offset Length { get; set; }

        //
        // Untyped methods. Default conversion will be done according to the function type.
        //
        bool IsNull(Offset input);
        object GetValue(Offset input);
        void SetValue(Offset input, object value);
        void NullifyValues();
        void Append(object value);
        void Insert(Offset input, object value);
        void Remove(Offset input);

        //void WriteValue(object value); // Convenience, performance method: set all outputs to the specified value
        //void InsertValue(Offset input, object value); // Changle length. Do we need this?

        //
        // Project/de-project
        //

        object ProjectValues(Offset[] offsets);
        Offset[] DeprojectValue(object value);
        
        //
        // Typed methods for each primitive type like GetInteger(). No NULL management since we use real values including NULL.
        //

        //
        // Index control.
        //
        // bool IsAutoIndexed { get; set; } // Index is maintained automatically
        // void Index(); // Index all and build new index
        // void Index(Offset start, Offset end); // The specified interval has been changed (or inserted?)
    }

    public interface CsColumnDefinition // How a function is represented and evaluated. It uses API of the column storage like read, write (typed or untyped).
    {
        /// <summary>
        /// Whether output values are appended to the output set. 
        /// </summary>
        bool IsGenerating { get; set; }

        /// <summary>
        /// Restricts kind of formula used to define this column. 
        /// </summary>
        ColumnDefinitionType DefinitionType { get; set; }

        /// <summary>
        /// Source (user, non-executable) formula for computing this function consisting of value-operations
        /// </summary>
        AstNode FormulaAst { get; set; }

        /// <summary>
        /// Represents a function definition in terms of other functions (select expression).
        /// When evaluated, it computes a value of the greater set for the identity value of the lesser set.
        /// For aggregated columns, it is an updater expression which computes a new value from the current value and a new fact (measure).
        /// </summary>
        ExprNode Formula { get; set; }

        /// <summary>
        /// One particular type of function specification used for defining mapped dimensions, import specification, copy specification etc.
        /// It defines greater set (nested) tuple in terms of the lesser set (nested) tuple. 
        /// The function computation procedure can transoform this mapping to a normal expression for evaluation in a loop or it can translate it to a join or other target engine formats.
        /// </summary>
        Mapping Mapping { get; set; }

        /// <summary>
        /// It describes the domain of the function or where the function returns null independent of other definitions
        /// </summary>
        ExprNode WhereExpression { get; set; }

        //
        // Aggregation
        //

        // TODO: Conceptual:
        // - Separating fact feeder from updater:
        //   - Separate fact feeder description (fact table + group + measure) from updater expression.
        //   - Aggr column has only updater + ref to a feeder object (by name or by ref.) Or a feeder referendes several aggr columns. 
        //   - Several aggr columns can reference one feeder.
        //   - Evaluation is done by running a loop over a feeder by filling several aggr columns
        // - Add initializer and finalizer expressions:
        //   - Initializer for aggr column before evaluation. Can be arbitrary complex expr. 
        //   - Finalizer for aggre column after evaluation. Is a normal expr, also, quite complex, say, devide by another column (COUNT).

        /// <summary>
        /// Fact set is a set for looping through and providing input for measure and group functions. By default, it is this (lesser) set.
        /// </summary>
        CsTable FactTable { get; set; } // Dependency on a lesser set and lesser functions

        /// <summary>
        /// Computes a group (this column input) from the current fact (fact table input). Result of this expression is input for this column.
        /// </summary>
        List<DimPath> GroupPaths { get; set; }

        /// <summary>
        /// Computes a new value (this column output) from the current fact (fact table input). Result of this expression has to be aggregated with the current output stored in this column.
        /// </summary>
        List<DimPath> MeasurePaths { get; set; }

        /// <summary>
        /// Name of the function for accumulating facts.
        /// </summary>
        string Updater { get; set; }

        //
        // Compute
        //

        CsColumnEvaluator GetColumnEvaluator(); // Get an object which is used to compute the function values according to the formula

        void Initialize();
        void Evaluate(); 
        void Finish();

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //
        List<CsTable> GetPreviousTables(bool recursive); // This element depends upon
        List<CsTable> GetNextTables(bool recursive); // Dependants

        List<CsColumn> GetPreviousColumns(bool recursive); // This element depends upon
        List<CsColumn> GetNextColumns(bool recursive); // Dependants
    }

    public enum ColumnDefinitionType // Specific types of column formula
    {
        NONE, // No definition for the column (and cannot be defined). Example: key columns of a product table
        ANY, // Arbitrary formula without constraints which can mix many other types of expressions
        ARITHMETIC, // Column uses only other columns or paths of this same table as well as operations
        LINK, // Column is defined via a mapping represented as a tuple with paths as leaves
        AGGREGATION, // Column is defined via an updater (accumulator) function which is fed by facts using grouping and measure paths
        CASE,
    }

    // This class is used only by the column evaluation procedure. 
    public interface CsColumnEvaluator // Compute output for one input based on some column definition and other already computed columns
    {
        // Never changes any set - neither lesser nor greater - just compute output given input

        bool Next(); // True if there exists a next element
        bool First(); // True if there exists a first element (if the set is not empty)
        bool Last(); // True if there exists a last element (if the set is not empty)

        bool IsUpdate { get; }

        object Evaluate(); // Compute output for the specified intput and write it
        object EvaluateUpdate(); // Read group and measure for the specified input and compute update according to the aggregation formula. It may also increment another function if necessary.
        bool EvaluateJoin(object output); // Called for all pairs of input and output *if* the definition is a join predicate.

        object GetResult();
    }

    public interface CsVariable // It is a storage element like function or table
    {
        //
        // Type infor
        //

        string TypeName { get; set; }
        CsTable TypeTable { get; set; } // Resolved table name

        //
        // Variable name (strictly speaking, it should belong to a different interface)
        //
        
        string Name { get; set; }

        //
        // Variable data. Analogous to the column data interface but without input argument
        //
        
        bool IsNull();
        object GetValue();
        void SetValue(object value);
        void NullifyValue();

        //
        // Typed methods
        //

    }

    public interface CsRecord // It is a complex data type generalizing primitive values and with leaves as primitive values (possibly surrogates). The main manipulation object for table data methods.
    {
        string Name { get; set; } // It is column/attribute/function name
        CsColumn Column { get; set; } // It is resolved column object corresponding to the name

        object Value { get; set; } // Untyped. Surrogate for non-leaf
        // TODO: do we need typed methods?

        List<CsRecord> Children { get; } // Child records
        bool IsLeaf { get; } // Convenience
        CsRecord Parent { get; } // Parent
    }

    public enum DataSourceType // Essentially, it a marker for a subclass of SetTop (Schema)
    {
        LOCAL, // This database
        ACCESS,
        OLEDB,
        SQL, // Generic (standard) SQL
        CSV,
        ODATA,
        EXCEL
    }
}
