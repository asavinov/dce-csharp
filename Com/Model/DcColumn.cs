using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Offset = System.Int32;

namespace Com.Model
{
    public interface DcColumn : DcJson // One column object
    {
        string Name { get; set; }

        bool IsKey { get; }
        bool IsSuper { get; } // Changing this property may influence storage type
        bool IsPrimitive { get; }
        // Other properties: isNullable, isTemporary, IsInstantiable (is supposed/able to have instances = lesser set instantiable)

        // Note: Set property works only for handing dims. For connected dims, a dim has to be disconnected first, then change its lesser/greater set and finally added again.
        DcTable Input { get; set; }
        DcTable Output { get; set; }

        void Add(); // Add to schema
        void Remove(); // Remove from schema

        DcColumnData Data { get; }
        DcColumnDefinition Definition { get; }
    }

    public interface DcColumnData // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
    {
        Offset Length { get; set; }

        //
        // Untyped methods. Default conversion will be done according to the function type.
        //
        bool IsNull(Offset input);

        object GetValue(Offset input);
        void SetValue(Offset input, object value);
        void SetValue(object value);

        void Nullify();

        void Append(object value);

        void Insert(Offset input, object value);

        void Remove(Offset input);

        //void WriteValue(object value); // Convenience, performance method: set all outputs to the specified value
        //void InsertValue(Offset input, object value); // Changle length. Do we need this?

        //
        // Project/de-project
        //
        object Project(Offset[] offsets);
        Offset[] Deproject(object value);

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

    public interface DcColumnDefinition // How a function is represented and evaluated. It uses API of the column storage like read, write (typed or untyped).
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
        bool IsAppendData { get; set; }

        bool IsAppendSchema { get; set; }

        /// <summary>
        /// Restricts kind of formula used to define this column. 
        /// </summary>
        ColumnDefinitionType DefinitionType { get; set; }

        //
        // COEL (language) representation
        //

        /// <summary>
        /// Formula in COEL with the function definition
        /// </summary>
        string Formula { get; set; }

        /// <summary>
        /// Source (user, non-executable) formula for computing this function consisting of value-operations
        /// </summary>
        //AstNode FormulaAst { get; set; }

        //
        // Structured (object) representation
        //

        /// <summary>
        /// Represents a function definition in terms of other functions (select expression).
        /// When evaluated, it computes a value of the greater set for the identity value of the lesser set.
        /// For aggregated columns, it is an updater expression which computes a new value from the current value and a new fact (measure).
        /// </summary>
        ExprNode FormulaExpr { get; set; }

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
        DcTable FactTable { get; set; } // Dependency on a lesser set and lesser functions

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
        // Schema/structure operations
        //
        
        /// <summary>
        /// Analyze this definition by extracting the structure of its function output. 
        /// Append these output columns from the definition of the output table. 
        ///
        /// The method guarantees that the function outputs (data) produced by this definition can be appended to the output table, that is, the output table is consistent with this definition. 
        /// This method can be called before (or within) resolution procedure which can be viewed as a schema-level analogue of data population and which ensures that we have correct schema which is consistent with all formulas/definitions. 
        /// </summary>
        void Append();

        //
        // Compute. Data operations.
        //

        void Evaluate();

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //

        List<DcTable> UsesTables(bool recursive); // This element depends upon
        List<DcTable> IsUsedInTables(bool recursive); // Dependants

        List<DcColumn> UsesColumns(bool recursive); // This element depends upon
        List<DcColumn> IsUsedInColumns(bool recursive); // Dependants
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

}
