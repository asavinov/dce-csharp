﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Utils;
using Com.Data;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Schema
{
    public interface DcColumn : DcJson // One column object
    {
        string Name { get; set; }

        bool IsKey { get; set; }
        bool IsSuper { get; } // Changing this property may influence storage type
        bool IsPrimitive { get; }
        // Other properties: isNullable, isTemporary, IsInstantiable (is supposed/able to have instances = lesser set instantiable)

        // Note: Set property works only for handing cols. For connected cols, a col has to be disconnected first, then change its lesser/greater set and finally added again.
        DcTable Input { get; set; }
        DcTable Output { get; set; }

        DcColumnData GetData();

        DcColumnStatus Status { get; }
    }

    // User-oriented status indicating the state of translation, evaluation as well as ability to do different operations.
    // It is used mostly in UI to vidualize elements and determine possiblity to perform operations.
    public enum DcColumnStatus
    {
        Red, // Red = !CanUpdate (= Translate error, = schema-level problem, =compile-time error). Translate error.
        Yellow, // Yellow = Dirty & CanUpdate (Translate success). Translate success.
        Green, // Green = !Dirty (all dependencies must be also green). Evaluate success.
    }
}
