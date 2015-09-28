using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;

namespace Com.Data.Eval
{
    /// <summary>
    /// This class implements functions for accessing Oledb data source as input. 
    /// </summary>
    public class ExprNodeOledb : ExprNode
    {
        public override void Resolve(DcWorkspace workspace, List<DcVariable> variables)
        {
            if (Operation == OperationType.VALUE)
            {
                base.Resolve(workspace, variables);
            }
            else if (Operation == OperationType.TUPLE)
            {
                base.Resolve(workspace, variables);
            }
            else if (Operation == OperationType.CALL)
            {
                base.Resolve(workspace, variables);
                // Resolve attribute names by preparing them for access - use directly the name for accessting the row object found in the this child
            }
        }

        public override void Evaluate()
        {
            if (Operation == OperationType.VALUE)
            {
                base.Evaluate();
            }
            else if (Operation == OperationType.TUPLE)
            {
                throw new NotImplementedException("ERROR: Wrong use: tuple is never evaluated for relational table.");
            }
            else if (Operation == OperationType.CALL)
            {
                base.Evaluate();
            }
        }

    }

}
