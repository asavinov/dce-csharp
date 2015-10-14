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
    public interface DcTableReader
    {
        void Open();
        void Close();

        object Next(); // Null if no more elements
    }
}
