using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.query
{
    /*
    !!! Finish design of the query language: 
    - translation to (in-memory) column store, that is, to functions (to in-memory functional model) similar to calcengine as a sequence of Operations on intermediate arrays (plan)
    - translation to distributed data sources. assume that columns/functions have different locations. 
    - correlated queries (fragments)
    */
    /// <summary>
    /// A query is a sequence of Operations. In the general case, it can be a dag of Operations. 
    /// For the engine, it is a program which is executed in an isolated context where intermediate results are stored. 
    /// Intermediate results can be passed between Operations so that output of the preivous operation is processed as input of the next operation. 
    /// 
    /// Question: are constraints imposed on sets or dimensions? In other words, should the constraint specification be associated with a set id or dimension id?
    /// Question: how the result of an operation is represented: as a single set, as a filter set, as a set with some dimensions? 
    /// </summary>
    public abstract class Query
    {
        public List<QueryOperation> Operations;
        public Dictionary<string, QueryParam> Context; // Registers visible between operations and containing their input and output
    }
}
