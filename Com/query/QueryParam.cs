using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Query
{
    /// <summary>
    /// - Parameters: a list of enabled/disabled values, e.g., {4, 1, 35}
    /// - Parameters: logical condition on values, '<30', can be complex logical expression
    /// - parameter: sorting criterion, ascending/descending (can be complex)
    /// - Parameters: set id, dimension id, 
    /// - parameter (fragment): direct path, reverse path, arbitrary path
    /// </summary>
    public abstract class QueryParam
    {
        /**
         * Type of this parameter.
         */
        public ParamType type;

        /**
         * For each parameter there is a class of object (or primitive type).
         */
        public Object value;
    }

    public enum ParamType
    {
        VALUES,
        PREDICATE,
        SORTING,
        DIMENSION,
        SET,
        DIRECT_PATH,
        REVERSE_PATH
    }
}
