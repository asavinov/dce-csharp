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

        // Record is a dictionary where attributes are keys and values are values
        protected Dictionary<string,string> currentRecord;
        protected ConnectionCsv connectionCsv;

        // It stores integer positions of each columns. 
        // Initialized when reader/connection is opened, that is, determined by the existing file.
        public Dictionary<string, int> ColumnIndexes;
        public List<string> Columns;

        public void Open()
        {
            rowid = -1;

            connectionCsv.OpenReader((TableCsv)table);

            Columns = connectionCsv.ReadColumns();
            ColumnIndexes = new Dictionary<string, int>();

            // Initialize column index
            for (int i=0; i < Columns.Count; i++)
            {
                ColumnIndexes.Add(Columns[i], i);
            }
        }

        public void Close()
        {
            rowid = table.GetData().Length;

            connectionCsv.CloseReader();
        }

        public object Next()
        {
            string[] values = connectionCsv.CurrentRecord;
            if (values == null)
            {
                currentRecord = null;
            }
            else
            {
                currentRecord = new Dictionary<string, string>();
                for (int i = 0; i < values.Length; i++)
                {
                    currentRecord[Columns[i]] = values[i];
                }
            }

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
