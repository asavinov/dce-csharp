using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// Describes one dimension in the schema without implementation of storage for instances. 
    /// </summary>
    public class Dimension
    {
        private static int uniqueId;

        /// <summary>
        /// Unique id within this database or (temporary) query session.  
        /// </summary>
        private int _id;
        public int Id { get { return _id; } }

        private string _name;
        public string Name { get; set; }

        /// <summary>
        /// Is identity dimension.
        /// </summary>
        private bool _identity;
        public bool Identity { get; set; }

        /// <summary>
        /// Reversed dimension has the opposite semantic interpretation (direction). It is used to resolve semantic cycles. 
        /// This flag is also used when returning input and output dimensions of sets and lesser/greater sets of dimensions.
        /// </summary>
        private bool _reversed;
        public bool Reversed { get; set; }

        /// <summary>
        /// Whether this dimension is supposed (able) to have instances. Some dimensions are used for conceptual purposes. 
        /// It is not about having zero instances - it is about the ability to have instances (essentially supporting the corresponding interface for working with instances).
        /// </summary>
        public bool Instantiable { get { return false; } }

        #region Schema methods.

        /// <summary>
        /// Greater (output) set.
        /// </summary>
        private Concept _greaterConcept;
        public Concept GreaterConcept
        {
            get { return _greaterConcept; }
            set
            {
                _greaterConcept = value;
            }
        }

        /// <summary>
        /// Lesser (input) set. 
        /// </summary>
        private Concept _lesserConcept;
        public Concept LesserConcept
        {
            get { return _lesserConcept; }
            set
            {
                _lesserConcept = value;
            }
        }

        #endregion

        #region Instance (function) methods.

        public virtual int InstanceCount // How many instances. It is the same for all dimensions of the lesser set. 
        {
            get { return _lesserConcept != null ? _lesserConcept.InstanceCount : 0; }
        }

        public virtual int InstanceSize // Width of instances. It is the same for all dimensions of the greater set. 
        {
            get { return _greaterConcept != null ? _greaterConcept.InstanceSize : 0; }
        }

        #endregion

        #region Constructors and initializers.

        public Dimension(string name)
            : this(name, null, null)
        {
        }

        public Dimension(string name, Concept lesserConcept, Concept greaterConcept)
            : this(name, lesserConcept, greaterConcept, false, false)
        {
        }

        public Dimension(string name, Concept lesserConcept, Concept greaterConcept, bool isIdentity, bool isReversed)
        {
            _id = uniqueId++;
            _name = name;

            _identity = isIdentity;
            _reversed = isReversed;

            LesserConcept = lesserConcept;
            GreaterConcept = greaterConcept;
        }

        #endregion

    }
}
