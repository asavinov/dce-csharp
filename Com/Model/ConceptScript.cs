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

    public interface CsTable
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

    public interface CsColumn
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

}
