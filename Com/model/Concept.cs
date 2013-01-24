using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    public class Concept
    {
        private static int uniqueId;

        /// <summary>
        /// Unique (in the schema) id. In C++, this Id field will be used as a reference filed
        /// </summary>
        private int _id;
        public int Id { get { return _id; } }

        private string _name;
        public string Name { get; set; }

        #region Schema methods. Inclusion (subconcept) hierarchy.

        /// <summary>
        /// Returns a set where this element is a subconcept. 
        /// </summary>
        private Dimension _superDim;
        public Dimension SuperDim
        {
            get { return _superDim; }
            set
            {
                if (value == null) // Request to remove the current super-dimension
                {
                    if (_superDim != null)
                    {
                        _superDim.GreaterConcept._subDims.Remove(_superDim);
                        _superDim = null;
                    }
                    return;
                }

                if (_superDim != null) // Remove the current super-dimension if present
                {
                    this.SuperDim = null;
                }

                if (value.LesserConcept != this || value.GreaterConcept == null)
                {
                    // ERROR: Dimension greater and lesser concepts must be set correctly
                }

                // Add new dimension
                _superDim = value;
                _superDim.GreaterConcept._subDims.Add(_superDim);
            }
        }

        private List<Dimension> _subDims = new List<Dimension>();
        public List<Dimension> SubDims { get { return _subDims; } }

        public List<Concept> getSubConcepts()
        {
            return (List<Concept>)_subDims.Select(x => x.LesserConcept);
        }
        public int SubConceptCount
        {
            get { return _subDims != null ? _subDims.Count : 0; }
        }

        #endregion

        #region Schema methods. Dimension structure.

        /// <summary>
        /// These are domain-specific dimensions. 
        /// </summary>
        public List<Dimension> _greaterDimensions = new List<Dimension>();
        public List<Dimension> GreaterDimensions
        {
            get { return _greaterDimensions; }
            set { _greaterDimensions = value; }
        }
        public void AddGreaterDimension(Dimension dim)
        {
            RemoveGreaterDimension(dim);
            dim.GreaterConcept._lesserDimensions.Add(dim);
            dim.LesserConcept._greaterDimensions.Add(dim);
        }
        public void RemoveGreaterDimension(Dimension dim)
        {
            dim.GreaterConcept._lesserDimensions.Remove(dim);
            dim.LesserConcept._greaterDimensions.Remove(dim);
        }
        public void RemoveGreaterDimension(string name)
        {
            Dimension dim = GetGreaterDimension(name);
            if (dim != null)
            {
                RemoveGreaterDimension(dim);
            }
        }
        public Dimension GetGreaterDimension(string name)
        {
            return _greaterDimensions.FirstOrDefault(d => d.Name == name);
        }
        public List<Concept> GetGreaterConcepts()
        {
            return (List<Concept>)_greaterDimensions.Select(x => x.LesserConcept);
        }
        public int GreaterConceptCount
        {
            get { return _greaterDimensions != null ? _greaterDimensions.Count : 0; }
        }

        public List<Dimension> _lesserDimensions = new List<Dimension>();
        public List<Dimension> LesserDimensions
        {
            get { return _lesserDimensions; }
            set { _lesserDimensions = value; }
        }
        public List<Concept> getLesserConcepts()
        {
            return (List<Concept>)_lesserDimensions.Select(x => x.GreaterConcept);
        }
        public int LesserConceptCount
        {
            get { return _lesserDimensions != null ? _lesserDimensions.Count : 0; }
        }

        #endregion

        #region Instance (function) methods

        /// <summary>
        /// Size of values or cells (physical identities) in bytes.
        /// </summary>
        public virtual int InstanceSize
        {
            get { return 0; }
        }

        /// <summary>
        /// How many _instances this set has. Cardinality. Set power. Length (height) of instance set.
        /// If instances are identified by integer offsets, then size also represents offset range.
        /// </summary>
        public virtual int InstanceCount
        {
            get { return 0; }
        }

        #endregion

        #region Constructors and initializers.

        public Concept()
        {
            _id = uniqueId++;
        }

        public Concept(string name)
            : this()
        {
            Name = name;
        }

        #endregion

    }
}
