using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Data;
using Com.Data.Query;

using Rowid = System.Int32;
using Com.Utils;

namespace Com.Schema
{
    public interface DcColumn : DcJson // One column object
    {
        string Name { get; set; }

        bool IsKey { get; set; }
        bool IsSuper { get; } // Changing this property may influence storage type
        bool IsPrimitive { get; }
        // Other properties: isNullable, isTemporary, IsInstantiable (is supposed/able to have instances = lesser set instantiable)

        // Note: Set property works only for handing dims. For connected dims, a dim has to be disconnected first, then change its lesser/greater set and finally added again.
        DcTable Input { get; set; }
        DcTable Output { get; set; }

        void Add(); // Add to schema
        void Remove(); // Remove from schema

        DcColumnData Data { get; }
        DcColumnDefinition Definition { get; }
    }

}
