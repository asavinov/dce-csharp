using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;

namespace Com.Data.Eval
{
    // This class is used only by the column evaluation procedure. 
    public interface DcEvaluator // Compute output for one input based on some column definition and other already computed columns
    {
        // Never changes any set - neither lesser nor greater - just compute output given input

        DcWorkspace Workspace { get; set; }

        bool NextInput(); // True if there exists a next element
        bool FirstInput(); // True if there exists a first element (if the set is not empty)
        bool LastInput(); // True if there exists a last element (if the set is not empty)

        object Evaluate(); // Compute output for the specified intput and write it

        object GetOutput();
    }

}
