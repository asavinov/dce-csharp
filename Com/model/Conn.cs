using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// Connection to a remote storage where schema elements and _instances are stored. 
    /// It is supposed to be extended by connections specific to each storage like JDBC or Excel.  
    /// </summary>
    public abstract class Conn
    {
        string name;
        public abstract List<Concept> getSets();
        public abstract List<DimAbstract> getDims();
    }
}
