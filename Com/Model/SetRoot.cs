using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// The root set is a predefined primitive set with no instances and no superset. 
    /// </summary>
    public class SetRoot : Set
    {
        public override int Width
        {
            get { return sizeof(Offset); }
        }

        public override int Length
        {
            get { return 0; }
        }

        public override Dim CreateDefaultLesserDimension(string name, Set lesserSet)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");
            Debug.Assert(name.Equals("Super", StringComparison.InvariantCultureIgnoreCase), "Wrong use: only super-dimensions can reference a root.");

            return new DimTop(name, lesserSet, this);
        }

        public SetRoot(string name)
            : base(name) // C#: If nothing specified, then base() will always be called by default
        {
            // TODO: Parameterize this instance as double virtual set. Important: isPrimitive
            Name = "Root"; // So the parameter is ignored
            IsInstantiable = false;
            IsPrimitive = true;
        }
    }

}
