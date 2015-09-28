using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

using Com.Schema;
using Com.Schema.Rel;

using Rowid = System.Int32;

namespace Com.Data.Eval
{
    public class IteratorOledb : IteratorExpr
    {
        protected DataRow currentRow;
        protected IEnumerator rows;
        protected DataTable dataTable;

        //
        // DcIterator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public override bool Next()
        {
            thisCurrent++;

            bool res = rows.MoveNext();
            currentRow = (DataRow)rows.Current;

            thisVariable.SetValue(currentRow);
            return res;
        }

        public override object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(currentRow);

            // evaluate the expression
            outputExpr.Evaluate();

            // Write the result value to the function
            // columnData.SetValue(currentElement, outputExpr.Result.GetValue()); // We do not store import functions (we do not need this data)

            return null;
        }

        public IteratorOledb(DcColumn column)
            : base(column)
        {
            // Produce a result set from the remote database by executing a query on the source table
            dataTable = ((SchemaOledb)thisTable.Schema).LoadTable(thisTable);
            rows = dataTable.Rows.GetEnumerator();
        }
    }

}
