using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Offset = System.Int32;

namespace Com.Model
{
    public interface ComColumn : ComJson // One column object
    {
        string Name { get; set; }

        bool IsKey { get; }
        bool IsSuper { get; } // Changing this property may influence storage type
        bool IsPrimitive { get; }
        // Other properties: isNullable, isTemporary, IsInstantiable (is supposed/able to have instances = lesser set instantiable)

        // Note: Set property works only for handing dims. For connected dims, a dim has to be disconnected first, then change its lesser/greater set and finally added again.
        ComTable Input { get; set; }
        ComTable Output { get; set; }

        void Add(); // Add to schema
        void Remove(); // Remove from schema

        ComColumnData Data { get; }
        ComColumnDefinition Definition { get; }
    }

    public interface ComColumnData // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
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

    public interface ComColumnDefinition // How a function is represented and evaluated. It uses API of the column storage like read, write (typed or untyped).
    {
        // The form of representation:
        // Our own v-expr or its parsed AST: 
        // Native source code: Java, C# etc.
        // Native library class: Java, C#, Python etc.
        // OS script (e.g., using pipes): Bash, Python etc.

        // Type of formula:
        // Primitive (direct computations returning a primitive value): = [col 1]+this.[col 2] / SYS_FUNC([col 3].[col 4] - 1.0)
        // Complex (mapping, tuple): ([Set 1] [s 1] = this.[a1], [Set 2] [s 2] = (...), [Double] [amount] = [col1]+[col2] )
        // A sequence of statements with return (primitive or tuple).
        // Aggregation/accumulation (loop over another set): standard (sum, mul etc. - separate class), user-defined like this+value/2 - 1.
        // Join predicate (two loops over input and output sets)


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
        //AstNode FormulaAst { get; set; }

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
        ExprNode WhereExpr { get; set; }

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
        ComTable FactTable { get; set; } // Dependency on a lesser set and lesser functions

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

        ComColumnEvaluator GetColumnEvaluator(); // Get an object which is used to compute the function values according to the formula

        void Initialize();
        void Evaluate();
        void Finish();

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //

        List<ComTable> UsesTables(bool recursive); // This element depends upon
        List<ComTable> IsUsedInTables(bool recursive); // Dependants

        List<ComColumn> UsesColumns(bool recursive); // This element depends upon
        List<ComColumn> IsUsedInColumns(bool recursive); // Dependants
    }

    public enum ColumnDefinitionType // Specific types of column formula
    {
        FREE, // No definition for the column (and cannot be defined). Example: key columns of a product table
        ANY, // Arbitrary formula without constraints which can mix many other types of expressions
        ARITHMETIC, // Column uses only other columns or paths of this same table as well as operations
        LINK, // Column is defined via a mapping represented as a tuple with paths as leaves
        AGGREGATION, // Column is defined via an updater (accumulator) function which is fed by facts using grouping and measure paths
        CASE,
    }

    // This class is used only by the column evaluation procedure. 
    public interface ComColumnEvaluator // Compute output for one input based on some column definition and other already computed columns
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

    public interface ComVariable // It is a storage element like function or table
    {
        //
        // Variable name (strictly speaking, it should belong to a different interface)
        //

        string Name { get; set; }

        //
        // Type info
        //

        string TypeName { get; set; }
        ComTable TypeTable { get; set; } // Resolved table name

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

}
