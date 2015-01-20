using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Model
{
    public interface ComSchema : ComTable
    {
        Workspace Workspace { get; set; }

        ComTable GetPrimitive(string dataType);
        ComTable Root { get; } // Convenience

        //
        // Table factory
        //

        ComTable CreateTable(string name);
        ComTable AddTable(ComTable table, ComTable parent, string superName);
        void DeleteTable(ComTable table);
        void RenameTable(ComTable table, string newName);

        //
        // Column factory
        //

        ComColumn CreateColumn(string name, ComTable input, ComTable output, bool isKey);
        void DeleteColumn(ComColumn column);
        void RenameColumn(ComColumn column, string newName);
    }

    /// <summary>
    /// Primitive data types used in our local database system. 
    /// We need to enumerate data types for each kind of database along with the primitive mappings to other databases.
    /// </summary>
    public enum ComDataType
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
