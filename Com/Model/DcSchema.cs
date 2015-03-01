using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Model
{
    public interface DcSchema : DcTable
    {
        Workspace Workspace { get; set; }

        DcTable GetPrimitive(string dataType);
        DcTable Root { get; } // Convenience

        //
        // Table factory
        //

        DcTable CreateTable(string name);
        DcTable AddTable(DcTable table, DcTable parent, string superName);
        void DeleteTable(DcTable table);
        void RenameTable(DcTable table, string newName);

        //
        // Column factory
        //

        DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey);
        void DeleteColumn(DcColumn column);
        void RenameColumn(DcColumn column, string newName);
    }

    /// <summary>
    /// Primitive data types used in our local database system. 
    /// We need to enumerate data types for each kind of database along with the primitive mappings to other databases.
    /// </summary>
    public enum DcDataType
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
        Set, // User-defined. It is any set that is not root (non-primititve type). Arbitrary user-defined name.
    }

}
