using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Schema
{
    public interface DcSchema : DcTable
    {
        DcSchemaKind GetSchemaKind();

        DcTable GetPrimitive(string dataType);
        DcTable Root { get; } // Convenience

        //
        // Table factory
        //

//DEL        DcTable CreateTable(string name);
//DEL        DcTable AddTable(DcTable table, DcTable parent, string superName);
//DEL        void DeleteTable(DcTable table);
//DEL        void RenameTable(DcTable table, string newName);

        //
        // Column factory
        //

//DEL        DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey);
//DEL        void DeleteColumn(DcColumn column);
//DEL        void RenameColumn(DcColumn column, string newName);
    }

    /// <summary>
    /// Primitive data types used in our local database system. 
    /// We need to enumerate data types for each kind of database along with the primitive mappings to other databases.
    /// </summary>
    public enum DcPrimitiveType
    {
        // Built-in types in C#: http://msdn.microsoft.com/en-us/library/vstudio/ya5y69ds.aspx
        Void, // Null, Nothing, Empty no value. Can be equivalent to Top.
        Top, // Maybe equivalent to Void
        Bottom, // The most specific type but introduced formally. This guarantees that any set has a lesser set.
        Root, // It is surrogate or reference
        Integer,
        Double,
        Decimal,
        String,
        Boolean,
        DateTime,
        Table, // User-defined. It is any set that is not root (non-primititve type). Arbitrary user-defined name.
    }

}
