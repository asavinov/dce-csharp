using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    // This class is used only by the column evaluation procedure. 
    public interface DcIterator // Compute output for one input based on some column definition and other already computed columns
    {
        // Never changes any set - neither lesser nor greater - just compute output given input

        Workspace Workspace { get; set; }

        bool Next(); // True if there exists a next element
        bool First(); // True if there exists a first element (if the set is not empty)
        bool Last(); // True if there exists a last element (if the set is not empty)

        object Evaluate(); // Compute output for the specified intput and write it

        object GetResult();
    }

}
