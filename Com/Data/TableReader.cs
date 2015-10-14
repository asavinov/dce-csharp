using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Schema.Csv;
using Com.Schema.Rel;
using Com.Data.Query;
using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Data
{
    public class TableReader : DcTableReader
    {
        DcTable table;
        Rowid rowid = -1;

        public void Open()
        {
            rowid = -1;
        }

        public void Close()
        {
            rowid = table.Data.Length;
        }

        public object Next()
        {
            if (rowid < table.Data.Length-1)
            {
                rowid++;
                return rowid;
            }
            else
            {
                return null;
            }
        }

        public TableReader(DcTable table)
        {
            this.table = table;
        }
    }
}
