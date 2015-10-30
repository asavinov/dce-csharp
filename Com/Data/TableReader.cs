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

using Rowid = System.Int32;

namespace Com.Data
{
    public class TableReader : DcTableReader
    {
        DcTable table;
        Rowid rowid = -1;

        public virtual void Open()
        {
            rowid = -1;
        }

        public virtual void Close()
        {
            rowid = table.GetData().Length;
        }

        public virtual object Next()
        {
            if (rowid < table.GetData().Length-1)
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
