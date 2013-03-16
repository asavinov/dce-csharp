using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace Com.Model
{
    /// <summary>
    /// One data element represented as a complex value composed of other values. 
    /// It is a tree of values where leaves are primitive values without child values.
    /// Each instance is a node in a tree. Each node corresponds to a pair of lesser dimension and its greater set.
    /// </summary>
    public class Instance
    {
        /// <summary>
        /// For leaf instances with no children this field stores a real object.
        /// For intermediate nodes with children it is null.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// It is a set this value belongs to, that is, where this value is a member.
        /// </summary>
        public string SetName { get; set; }
        public Set Set { get; set; }

        /// <summary>
        /// It is a dimension (place, label, attribute) leading to this set from its parent. NULL for a root.
        /// This dimension has a lesser set equal to the parent and greater set equal to this set (the set specified in this instance).
        /// </summary>
        public string DimensionName { get; set; }
        public Dimension Dimension { get; set; }

        /// <summary>
        /// Constituents of this element.
        /// </summary>
        public List<Instance> ChildInstances { get; set; }
        public Instance GetChild(Dimension dimension)
        {
            return ChildInstances.FirstOrDefault(i => i.Dimension == dimension);
        }
        public Instance GetChild(string name)
        {
            return ChildInstances.FirstOrDefault(i => i.DimensionName.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Where ths value is a child.
        /// </summary>
        public Instance ParentInstance { get; set; }
        public Instance Root
        {
            get
            {
                Instance root = this;
                while (root.ParentInstance != null)
                {
                    root = root.ParentInstance;
                }

                return root;
            }
        }

        // GetPrimitiveDictionary
        // Get primitive values (flatten the tree). Either as a dictionary or as a flattened Instance (remove intermediate nodes).
        // Set primitive value given a path name or list of intermediate names


        // Build instance from a dimension (empty, just a structure)
        // Map instance-dimensions (various options)
        // Build instance from a flat key-value pairs (raw from a flat result set)

        public static Instance CreatePrimitiveInstance(Set set)
        {
            // Create instance structure corresponding to the primitive structure of identity dimensions of the specified set
            Instance instance = new Instance();
            return instance;
        }

        public void SetValues(DataRow row, object mapping)
        {
        }

    }
}
