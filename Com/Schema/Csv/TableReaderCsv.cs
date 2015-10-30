using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Schema.Csv;
using Com.Schema.Rel;
using Com.Data;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Schema.Csv
{
    public class TableReaderCsv : DcTableReader
    {
        DcTable table;
        Rowid rowid = -1;

        protected string[] currentRecord;
        protected ConnectionCsv connectionCsv;

        public void Open()
        {
            rowid = -1;

            connectionCsv.OpenReader((SetCsv)table);
        }

        public void Close()
        {
            rowid = table.GetData().Length;

            connectionCsv.CloseReader();
        }

        public object Next()
        {
            currentRecord = connectionCsv.CurrentRecord;
            rowid++;
            if(currentRecord != null)
            {
                // Csv is opened with first record initialized (set as current) therefore we move forward at the end
                connectionCsv.ReadNext(); 
            }
            return currentRecord;
        }

        public TableReaderCsv(DcTable table)
        {
            this.table = table;

            connectionCsv = new ConnectionCsv();
        }
    }

}
