using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace Com.Model
{
    /// <summary>
    /// The root set is a predefined primitive set with no instances and no superset. 
    /// It has a number of predefined (primitive) subsets which do not have greater sets. 
    /// 
    /// The root is normally used to represent a database, connection, data source or mash up. 
    /// It may also describe how its instances are loaded (populated) in terms of source databases. It is not clear where it is described (for each set or dimension).
    /// </summary>
    public class SetRoot : Set
    {
        private DataSourceType _type=DataSourceType.LOCAL; // Where data is stored and processed (engine)
        public DataSourceType DataSourceType
        {
            get { return _type; }
        }


        public override int Width
        {
            get { return 0; }
        }

        public override int Length
        {
            get { return 0; }
        }

        public virtual DataTable Export(Set set)
        {
            // Check if this set is our child
            DataTable dataTable = new DataTable(set.Name);
            // Add rows by reading them from this set local dimensions
            return null;
        }

        public SetRoot(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            Instantiable = false;

            //
            // Generate all predefined primitive sets as subsets
            //
            SetInteger setInteger = new SetInteger("Integer");
            setInteger.SuperDim = new DimRoot("super", setInteger, this);

            SetDouble setDouble = new SetDouble("Double");
            setDouble.SuperDim = new DimRoot("super", setDouble, this);

            SetString setString = new SetString("String");
            setString.SuperDim = new DimRoot("super", setString, this);
        }
    }

    /// <summary>
    /// Primitive data types used in our database system. 
    /// See also OleDb types: System.Data.OleDb.OleDbType.*
    /// </summary>
    public enum DataType 
    {
        Double,
        Integer,
        String, 
        Boolean
    }
}
