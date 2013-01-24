using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// The root set does not have instances and does not have a superset but it has a number of predefined (primitive) subsets which do not have greater sets. 
    /// 
    /// The root is normally used to represent a database, connection, data source or mash up. 
    /// It may also describe how its instances are loaded (populated) in terms of source databases. It is not clear where it is described (for each set or dimension).
    /// </summary>
    public class SetRoot : Concept
    {
        public override int InstanceSize
        {
            get { return 0; }
        }

        /// <summary>
        /// Connection to the location where this schema is persistently stored, that is, original database.
        /// It is a way to lower level of physical representation and serialization.  
        /// </summary>
        string conn;

        /// <summary>
        /// Input schemas used to define this schema elements and build its _instances.
        /// </summary>
        List<Conn> inputDbs;

        public SetRoot(string id)
            : base(id)
        {
            // TODO: Generate all predefined primitive sets as subsets
        }
    }
}
