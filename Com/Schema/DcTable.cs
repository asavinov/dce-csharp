using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Data;
using Com.Data.Query;
using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Schema
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

}
