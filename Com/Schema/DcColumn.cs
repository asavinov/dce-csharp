using System;
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

        // Red - !CanUpdate (= Translate error, = schema-level problem, =compile-time error)
        // Yellow - Dirty & CanUpdate (Translate success)
        // Green - !Dirty (all dependencies must be also green)
        DcColumnStatus Status { get; }

    }

    public enum DcColumnStatus
    {
        // After column creation and after each update, it gets Unknown state for Translate -> what is Evaluate state?
        // Then we immediately call Translate and get either success or error.

        // Translate_Success -> Evaluate_Unknown, which means Dirty but CanEvaluate (yellow)
        // Translate_Error -> Evaluate_Unknown, which means Dirty but !CanEvaluate (red)

        // Evaluate_Success -> !Dirty
        // Evaluate_Error -> Dirty but !CanEvaluate? - is this situation possible? 

        // Approach 1: 
        // - Translate is always called automatically immediately after each schema change. 
        //   Hence, Translate is either Sucessful or Error
        // - There is one Update button which calls Evaluate. Update buton can be either enabled or disabled: CanUpdate, !CanUpdate. 
        //   Enabled/Disabled dependes on Tranlsate Success of Error. 
        //   In other words, user can always Update if Translate successful (even repeatedly for non-dirty data).
        // - There is Status flag. 
        //   - If Translate is error then Dirty Red (because there was change but no evaluation).
        //   - If Translate is success but no evaluate (user has not Update) then Dirty Yellow
        //   - If Translate success & Evaluate success then Non-dirty-Green. 
        //   - If Translate success & Evaluate error then Dirty-Yellow (or special flag).

        // IsDirty() - set true by the update/edit procedure and propagates along dependencies. 
        // set true manually by the user (explict requiest, optional)
        // set false only by Evaluate success. does not propate. constraint: true is only if all dependencies are true.

        Red,
        Yellow,
        Green,
    }
}
