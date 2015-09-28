using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;
using Com.Schema.Csv;

using Rowid = System.Int32;

namespace Com.Data.Eval
{
    public class IteratorCsv : IteratorExpr
    {
        protected string[] currentRecord;
        protected ConnectionCsv connectionCsv;

        //
        // DcIterator interface
        //

        protected bool isUpdate;
        public bool IsUpdate { get { return isUpdate; } }

        public override bool Next()
        {
            currentRecord = connectionCsv.CurrentRecord;

            if (currentRecord == null) return false;

            thisVariable.SetValue(currentRecord);

            connectionCsv.ReadNext(); // We increment after iteration because csv is opened with first record initialized
            thisCurrent++;

            return true;
        }

        public override object Evaluate()
        {
            // Use input value to evaluate the expression
            thisVariable.SetValue(currentRecord);

            // evaluate the expression
            outputExpr.Evaluate();

            // Write the result value to the function
            // columnData.SetValue(currentElement, outputExpr.Result.GetValue()); // We do not store import functions (we do not need this data)

            return null;
        }

        public IteratorCsv(DcColumn column)
            : base(column)
        {
            // Produce a result set that can be iterated through
            connectionCsv = ((SchemaCsv)thisTable.Schema).connection;
            connectionCsv.OpenReader((SetCsv)thisTable);

            thisCurrent = 0;
            currentRecord = connectionCsv.CurrentRecord; // Start from the first record
        }
    }

}
