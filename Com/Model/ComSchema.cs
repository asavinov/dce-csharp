using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Model
{
    public interface ComSchema : ComTable
    {
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

}
