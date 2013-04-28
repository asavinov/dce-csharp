using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Offset = System.Int32;

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
        /// For leaf instances with no children this field stores a real value like double.
        /// For intermediate nodes it is either null or contains a surrogate (offset).
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// It identifies a set this value belongs to, that is, where this value is a member.
        /// Many nodes can reference the same set if the same greater set is used in many paths. 
        /// </summary>
        public string SetName { get; set; }
        public Set Set { get; set; }

        /// <summary>
        /// It is a dimension (place, label, attribute) leading to this set from its parent. NULL for a root.
        /// This dimension has a lesser set equal to the parent, and greater set equal to this set (name of this node).
        /// </summary>
        public string DimensionName { get; set; }
        public Dim Dimension { get; set; }

        /// <summary>
        /// Constituents of this element.
        /// </summary>
        public List<Instance> ChildInstances { get; set; }
        public Instance GetChild(Dim dimension)
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

        public void EmptyValues()
        {
            Value = null;
            foreach (Instance child in ChildInstances)
            {
                child.EmptyValues();
            }
        }

        // GetPrimitiveDictionary
        // Get primitive values (flatten the tree). Either as a dictionary or as a flattened Instance (remove intermediate nodes).
        // Set primitive value given a path name or list of intermediate names


        // Build instance from a dimension (empty, just a structure)
        // Map instance-dimensions (various options)
        // Build instance from a flat key-value pairs (raw from a flat result set)

        public void SetValues(DataRow row, object mapping)
        {
        }

        public Instance(Set set)
        {
            Set = set;
            SetName = set.Name;

            // Create instances for each greater dimension set (recursively) and add them to the new instance. 
            foreach (Dim dim in set.GreaterDims)
            {
                Instance child = new Instance(dim.GreaterSet);
                child.Dimension = dim;
                child.DimensionName = dim.Name;
                child.ParentInstance = this;
                ChildInstances.Add(child);
            }
        }

    }
}
