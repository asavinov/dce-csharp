﻿using System;
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
        public override int InstanceSize
        {
            get { return 0; }
        }

        public override int InstanceCount
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

        /// <summary>
        /// Connection to the location where this schema is persistently stored, that is, original database.
        /// It is a way to lower level of physical representation and serialization.  
        /// </summary>
        string conn;

        /// <summary>
        /// Input schemas used to define this schema elements and build its _instances.
        /// </summary>
        List<Conn> inputDbs;

        public SetRoot(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            _instantiable = false;

            //
            // Generate all predefined primitive sets as subsets
            //
            SetDouble setDouble = new SetDouble("double");
            setDouble.SuperDim = new DimRoot("super", setDouble, this);

        }
    }
}
