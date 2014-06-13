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

        bool IsKey { get; }
        bool IsParent { get; } // Changing this property may influence storage type

        CsTable Input { get; }
        CsTable Output { get; }

        void Include(); // Add to schema
        void Exclude(); // Remove from schema

        CsColumnData ColumnData { get; }
        CsColumnDefinition ColumnDefinition { get; }
    }

    public interface CsColumnData // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
    {
        DataType CsType { get; } // Check the output value type. Can be needed to choose appropriate typed methods.
        Offset Size { get; set; }

        //
        // Untyped methods. Default conversion will be done according to the function type.
        //
        bool IsNullValue(Offset input);
        object ReadValue(Offset input);
        void WriteValue(Offset input, object value);
        //void WriteValue(object value); // Convenience, performance method: set all outputs to the specified value
        //void InsertValue(Offset input, object value); // Changle length. Do we need this?

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
        void Initialize();
        void Evaluate(); 
        void Finish();
    }

    // This class is used only by the column evaluation procedure. 
    public interface CsRecordEvaluator // Compute output for one input based on some column definition and other already computed columns
    {
        object EvaluateSet(Offset input); // Compute output for the specified intput and write it
        object EvaluateUpdate(Offset input); // Read group and measure for the specified input and compute update according to the aggregation formula. It may also increment another function if necessary.
        bool EvaluateJoin(Offset input, object output); // Called for all pairs of input and output *if* the definition is a join predicate.
    }

    
    public interface CsTableData // It is interface for manipulating data in a table.
    {
        Offset Size { get; }

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
