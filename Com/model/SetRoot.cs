using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
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

        public List<Set> PrimitiveSets
        {
            get { return SubDims.Where(x => !x.LesserSet.Instantiable).Select(x => x.LesserSet).ToList(); }
        }

        public List<Set> NonPrimitiveSets
        {
            get { return SubDims.Where(x => x.LesserSet.Instantiable).Select(x => x.LesserSet).ToList(); }
        }

        public Set GetPrimitiveSet(string name)
        {
            return SubDims.First(x => !x.LesserSet.Instantiable && x.LesserSet.Name == name).LesserSet;
        }

        public SetRoot(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            _instantiable = false;

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

}
