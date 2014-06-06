using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Offset = System.Int32;

namespace Com.Model
{
    public class ConceptScript
    {
        public string N { get; set; }

        public CsSchema schema { get; set; }

        public CsTable CreateTable(string name, CsTable super=null)
        {
            // Create instance of Set (must implement CsTable)
            Set table = new Set(name);

            // Create a super-dimension
            if (super == null)
            {
                super = schema.T("Root");
            }

            // Add super-dimension
            CsColumn superCol = this.CreateColumn("Super", table, super, true, true);
            superCol.Include();

            return table;
        }

        public CsColumn CreateColumn(string name, CsTable input, CsTable output, bool isKey, bool isSuper)
        {
            // Create instance of the subclass of Dim (must implement CsColumn) corresponding to the output table
            Dim column = ((Set)output).CreateDefaultLesserDimension(name, (Set)input);
            column.IsIdentity = isKey;
            column.IsSuper = isSuper;

            return column;
        }

        public ConceptScript(string name)
        {
            N = name;
        }
    }

    public interface CsTable // One table object
    {
        string N { get; set; }
        CsColumn C(string name); // Name resolution

        //
        // Column/schema information
        //
        List<CsColumn> OutputColumns { get; }
        List<CsColumn> InputColumns { get; }

        //CsColumn Super { get; set; } // Convenience method
        //CsColumn Where { get; set; } // Boolean function definition must be true for all elements
        // OrderBy - what should be here - comparator?

        //void Eval();
        // TODO: Type of population: manual (external API), projection (from external), projection (from internal), all

        //
        // Tuple manipulation: append, insert, remove, read, write.
        // Here we use TUPLE and its constituents as primitive types: Reference etc.
        // Column names or references are important. Types (table references or names) are necessary and important. Maybe also flags like Super, Key would be useful. 
        // TUPLE could be used as a set structure specification (e.g., param for set creation).
        //

    }

    public interface CsSchema : CsTable
    {
        CsTable T(string name); // Name resolution
    }

    public interface CsConnection : CsSchema
    {
    }

    public interface CsColumn // One column object
    {
        string N { get; set; }

        void Include(); // Add to schema
        void Exclude(); // Remove from schema

        //
        // Properties 
        //
        CsTable Input { get; }
        CsTable Output { get; } 

        bool IsKey { get; }
        bool IsParent { get; } // Changing this property may influence storage type

        //
        // Dependencies and evaluation
        //
        //string Formula { get; set; } 
        // TODO: It can string, ast, Java delegate etc. Maybe create a class Formula.
        // TODO: Three different kinds of definitions: arithmetic or pimitive (return direct primitive value including composition and return of offsets), mapping or complex (return tuple, search for finding offset), filter (null specification, good as a component in where), aggregation (accumulation, update)
        //void AddDependency(CsColumn column);
        //void RemoveDependency(CsColumn column);
        //void Eval();

        //
        // Function value manipulation: read, write
        // Here we manipulate primitive data types: Reference, Void (empty, null), String, Double etc.
        //

    }

    public interface CsFormula // How a function is represented and evaluated. It uses API of the column storage like read, write (typed or untyped).
    {
        void Initialize(); // Initialize the value. Is called one time before the function evaluation. Important for aggregation.
        void Evaluate(object input); // Called for each iteration over this set. Compute the output and write it to the functino storage. The implementatino can be typed or untyped. 
        bool Evaluate(object input, object output); // Called for all pairs of input and output *if* the definition is a join prodicate.
        void Finish(); // Is called one time after the function evaluation. Can be important for aggregation, say, devide the accumulated measure on the accumulated count. 
    }

    public interface CsColumnData // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
    {
        DataType CsType { get; } // Check the output value type. Can be needed to choose appropriate typed methods.
        Offset Length { get; set; }

        //
        // Untyped methods. Default conversion will be done according to the function type.
        //
        bool IsNullValue(Offset input);
        object ReadValue(Offset input);
        void WriteValue(Offset input, object value);
        void WriteValue(object value); // Convenience, performance method: set all outputs to the specified value
        void InsertValue(Offset input, object value); // Changle length. Do we need this?

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

    public interface CsTableData // It is interface for manipulating data in a table.
    {
        Offset FindTuple(CsRecord record); // If many records can satisfy then another method has to be used. What is many found? Maybe return negative number with the number of records (-1 or Length means not found, positive means found 1)? 

        void InsertTuple(Offset input, CsRecord record); // All keys are required? Are non-keys allowed?

        void RemoveTuple(Offset input); // We use it to remove a tuple that does not satisfy filter constraints. Note that filter constraints can use only data that is loaded (some columns will be computed later). So the filter predicate has to be validated for sets which are projected/loaded or all other functions have to be evaluated during set population. 
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

}
