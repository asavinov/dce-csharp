using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Com.Utils;
using Com.Schema;
using Com.Data;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Schema.Csv
{
    public class TableWriterCsv : DcTableWriter
    {
        DcTable table;
        Rowid rowid = -1;

        protected string[] currentRecord;
        protected ConnectionCsv connectionCsv;

        // It stores integer positions of each columns. 
        // Should be initialized by from outside before the data is really written.
        // The system will determine position for each attribute of written records using this structure. 
        // If indexes are absent then some defaults will be used and the index will be accordingly updated. 
        public Dictionary<string, int> ColumnIndexes = new Dictionary<string, int>();

        public void Open()
        {
            rowid = -1;

            connectionCsv.OpenWriter((TableCsv)table);

            // Write header
            if (((TableCsv)table).HasHeaderRecord)
            {
                var header = GetColumnNamesByIndex();
                connectionCsv.WriteNext(header);
            }
        }

        public void Close()
        {
            rowid = table.GetData().Length;

            connectionCsv.CloseWriter();
        }

        public virtual Rowid Find(ExprNode expr) // Use only identity dims (for general case use Search which returns a subset of elements)
        {
            return -1; // Not found 
        }

        public virtual bool CanAppend(ExprNode expr) // Determine if this expression (it has to be evaluated) can be added into this set as a new instance
        {
            return true;
        }

        public virtual Rowid Append(ExprNode expr) // Identity dims must be set (for uniqueness). Entity dims are also used when appending.
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: cannot append to a primitive set. ");
            Debug.Assert(expr.OutputVariable.TypeTable == table, "Wrong use: expression OutputSet must be equal to the set its value is appended/found.");
            Debug.Assert(expr.Operation == OperationType.TUPLE, "Wrong use: operation type for appending has to be TUPLE. ");


            var columns = GetColumnsByIndex();
            string[] record = new string[columns.Length];

            //
            // Prepare a record with all fields. Here we choose the columns to be written
            //

            for (int i = 0; i < columns.Length; i++) // We must append one value to ALL greater dimensions even if a child expression is absent
            {
                DcColumn col = columns[i];
                ExprNode childExpr = expr.GetChild(col.Name);
                object val = null;
                if (childExpr != null) // Found. Value is present.
                {
                    val = childExpr.OutputVariable.GetValue();
                    if (val != null)
                    {
                        record[i] = val.ToString();
                    }
                    else
                    {
                        record[i] = "";
                    }
                }
            }

            // Really append record to the file
            connectionCsv.WriteNext(record);

            table.GetData().Length = table.GetData().Length + 1;
            return table.GetData().Length - 1;
        }

        public DcColumn[] GetColumnsByIndex() // Return an array of columns with indexes starting from 0 and ending with last index
        {
            var columns = new List<DcColumn>();
            int columnCount = 0;

            foreach (DcColumn col in table.Columns)
            {
                if (!(col is ColumnCsv)) continue;

                int colIdx = ((ColumnCsv)col).ColumnIndex;
                if (colIdx < 0) continue;

                if (colIdx >= columns.Count) // Ensure that this index exists 
                {
                    columns.AddRange(new DcColumn[colIdx - columns.Count + 1]);
                }

                columns[colIdx] = col;
                columnCount = Math.Max(columnCount, colIdx);
            }

            return columns.ToArray();
        }
        public string[] GetColumnNamesByIndex()
        {
            var columns = GetColumnsByIndex();
            var columnNames = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++) columnNames[i] = columns[i].Name;
            return columnNames;
        }

        public Rowid Find(DcColumn[] cols, object[] values)
        {
            throw new NotImplementedException("Csv does not support record finding");
        }

        public Rowid Append(DcColumn[] cols, object[] values)
        {
            throw new NotImplementedException("TODO:");
        }

        public void Remove(Rowid input)
        {
            throw new NotImplementedException("Csv does not support record removal");
        }

        public TableWriterCsv(DcTable table)
        {
            this.table = table;

            connectionCsv = connectionCsv = new ConnectionCsv();
        }
    }
}
