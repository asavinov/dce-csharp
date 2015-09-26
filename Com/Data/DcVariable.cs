using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;

namespace Com.Data
{
    public interface DcVariable // It is a storage element like function or table
    {
        //
        // Variable name (strictly speaking, it should belong to a different interface)
        //

        string Name { get; set; }

        //
        // Type info
        //

        string SchemaName { get; set; }
        string TypeName { get; set; }

        void Resolve(DcWorkspace workspace); // Resolve schema name and table name (type) into object references

        DcSchema TypeSchema { get; set; } // Resolved schema name
        DcTable TypeTable { get; set; } // Resolved table name

        //
        // Variable data. Analogous to the column data interface but without input argument
        //

        bool IsNull();

        object GetValue();
        void SetValue(object value);

        void Nullify();

        //
        // Typed methods
        //

    }
}
