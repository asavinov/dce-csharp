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
using Com.Data.Eval;

using Rowid = System.Int32;

namespace Com.Schema.Csv
{
    public class TableReaderOledb : DcTableReader
    {
        DcTable table;

        protected DataRow currentRow;
        protected IEnumerator rows;
        protected DataTable dataTable;
        Rowid rowid = -1;

        protected string[] currentRecord;
        protected ConnectionCsv connectionCsv;

        public void Open()
        {
            // Produce a result set from the remote database by executing a query on the source table
            dataTable = ((SchemaOledb)table.Schema).LoadTable(table);
            rows = dataTable.Rows.GetEnumerator();
        }

        public void Close()
        {
        }

        public object Next()
        {
            rowid++;

            bool res = rows.MoveNext();
            currentRow = (DataRow)rows.Current;

            return currentRow;
        }

        public TableReaderOledb(DcTable table)
        {
            this.table = table;
        }
    }

}
