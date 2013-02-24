using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    public class Attribute
    {
        public string TableName { get; set; } // TABLE_NAME

        public string Name { get; set; } // COLUMN_NAME. If the name is not set then the last dimension name is used

        public string TypeSystem { get; set; } // Type system name (the same for all attributes)

        public string DataType { get; set; } // DATA_TYPE. type system is implementation specific (OleDb, Oracle, Standard SQL, CSV etc.) so we can use strings and in addition store somewhere type system name

        public string PkName { get; set; } // PK it belongs to (what about many PKs?

        public string FkName { get; set; } // FK_NAME. FK it belongs to (what about many FKs?
        public string FkTargetTableName { get; set; } // PK_TABLE_NAME. Referenced table
        public string FkTargetColumnName { get; set; } // PK_COLUMN_NAME. Referenced column (corresponding to this attribute)
        public string FkTargetPkName { get; set; } // PK_NAME. Referenced key where referenced column is (only important in the case of many PKs)

        public string OnDelete { get; set; } // What to do if the value is deleted

        public string OnUpdate { get; set; } // What to do if the value id updated

        Dictionary<string, string> Properties = new Dictionary<string, string>();

        /// <summary>
        /// It is a sequence of dimensions leading from this attribute set to a primitive set. 
        /// The last set (range of the last dimension) is a target of this attribute.
        /// Each next dimension domain is the previous dimension range. 
        /// </summary>
        private List<Dimension> _path = new List<Dimension>();
        public List<Dimension> Path { get { return _path; } }

        public Attribute(string name)
        {
            Name = name;
        }
    }

}
