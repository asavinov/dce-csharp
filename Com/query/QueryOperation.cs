using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Query
{
    /// <summary>
    /// One operation is a unit of execution of the engine.
    /// An operation is broken into an Instruction and its Parameters. 
    /// </summary>
    public abstract class QueryOperation
    {
        /**
         * - operation: satisfy constraints
         * - operation: intersect/union/negate input sets (sets are specified as output of other Operations or explicitly)
         * - operation: project/de-project (with condition and sorting)
         * - operation: product (with condition and sorting)
         * - operation: discretize (parameter is a list of break values)
         */
        public InstructionType Instruction;

        /**
         * A map of Parameters. 
         * Each parameter is identified by some key.
         * A key can be viewed as the parameter address or register.
         * Keys can be used by Operations to access necessary data (so there should be a convention). 
         */
        public Dictionary<string, QueryParam> Parameters;
    }

    public enum InstructionType
    {
        PREDICATE,
        INTERSECTION,
        UNION,
        PROJECT,
        DEPROJECT,
        PRODUCT
    }
}
