using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Rowid = System.Int32;

namespace Com.Data
{
    public interface DcColumnData // It is interface for managing function data as a mapping to output values (implemented by some kind of storage manager). Input always offset. Output type is a parameter.
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
    }

}
