using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Data
{
    public interface DcTableData // It is interface for manipulating data in a table.
    {
        Rowid Length { get; set; }

        bool AutoIndex { set; }
        bool Indexed { get; }
        void Reindex();

        DcTableReader GetTableReader();
        DcTableWriter GetTableWriter();
        DcTableDefinition GetDefinition();
    }

}
