using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;

using Rowid = System.Int32;

namespace Com.Data
{
    public interface DcColumnData : DcJson // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
    {
        Rowid Length { get; set; }

        bool AutoIndex { get; set; }
        bool Indexed { get; }
        void Reindex();

        //
        // Untyped methods. Default conversion will be done according to the function type.
        //
        bool IsNull(Rowid input);

        object GetValue(Rowid input);
        void SetValue(Rowid input, object value);
        void SetValue(object value);

        void Nullify();

        void Append(object value);

        void Insert(Rowid input, object value);

        void Remove(Rowid input);

        //void WriteValue(object value); // Convenience, performance method: set all outputs to the specified value
        //void InsertValue(Offset input, object value); // Changle length. Do we need this?

        //
        // Project/de-project
        //
        object Project(Rowid[] offsets);
        Rowid[] Deproject(object value);

        //
        // Typed methods for each primitive type like GetInteger(). No NULL management since we use real values including NULL.
        //

        //
        // Index control.
        //
        // bool IsAutoIndexed { get; set; } // Index is maintained automatically
        // void Index(); // Index all and build new index
        // void Index(Offset start, Offset end); // The specified interval has been changed (or inserted?)

        //
        // The former DcColumnDefinition 
        //

        /// <summary>
        /// Formula in COEL with the function definition
        /// </summary>
        string Formula { get; set; }

        //
        // Schema and translation
        //
        // Formula translation option. Whether we need to append a new column if it has not been found in the schema.
        bool IsAppendSchema { get; set; }

        // Compile-time/schema-level status. The results of translation. 
        // Column (and all necessary columns) has been successfully translated (syntactically valid) and can be evaluated (either evaluated or not). 
        bool HasValidSchema { get; set; }

        // It is a compile-time or schema-level operation. Its goal is to parse/validate the formula, update dependencies and make sure that the formula can be evaluated. 
        // It translates only this column by assuming that all necessary conditions are fulfilled. Finally, it sets the flag (read or yellow).
        void Translate();

        //
        // Data and evaluation
        //
        // Formula evaluation option. Whether we need to append a new data element if it has not been found in the data.
        bool IsAppendData { get; set; }

        // Run-time/data-level status. The result of evaluation. 
        // Column (and all necessary columns) has been successfully evaluated and the data is up-to-date (non-dirty). 
        bool HasValidData { get; set; }

        // It is a run-time or data-level operation. Its goal is to execute the formula and make the column data up-to-date. 
        // It evaluates only this column and assumes all necessary columns have been evaluated. Finally, it sets the flag (yellow or green).
        void Evaluate();

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //
        List<DcColumn> UsesColumns(); // This element depends upon
        List<DcTable> UsesTables(); // This element depends upon

        List<DcColumn> IsUsedInColumns(); // Dependants
        List<DcTable> IsUsedInTables(); // Dependants
    }

}
