using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Utils;

using Rowid = System.Int32;

namespace Com.Schema.Rel
{
    /// <summary>
    /// Relational attribute to be used in relational schemas (relational table and column classes). It is a primitive path - a sequence of normal dimensions leading to a primitive type. 
    /// </summary>
    public class DimAttribute : DimPath
    {
        #region ComColumn interface

        public override void Add()
        {
            //if (Output != null) ((SetRel)Output).AddLesserPath(this);
            if (Input != null) ((SetRel)Input).AddGreaterPath(this);

            // Notify that a new child has been added
            //if (Input != null) ((Set)Input).NotifyAdd(this);
            //if (Output != null) ((Set)Output).NotifyAdd(this);
        }

        public override void Remove()
        {
            //if (Output != null) ((SetRel)Output).RemoveLesserPath(this);
            if (Input != null) ((SetRel)Input).RemoveGreaterPath(this);

            // Notify that a new child has been removed
            //if (Input != null) ((Set)Input).NotifyRemove(this);
            //if (Output != null) ((Set)Output).NotifyRemove(this);
        }

        #endregion

        /// <summary>
        /// Additional names specific to the relational model and imported from a relational schema. 
        /// </summary>
        public string RelationalColumnName { get; set; } // Original column name used in the database
        public string RelationalPkName { get; set; } // PK this column belongs to according to the relational schema
        public string RelationalFkName { get; set; } // Original FK name this column belongs to
        public string RelationalTargetTableName { get; set; }
        public string RelationalTargetColumnName { get; set; }

        /// <summary>
        /// Expand one attribute by building its path segments as dimension objects. 
        /// Use the provided list of attributes for expansion recursively. This list essentially represents a schema.
        /// Also, adjust path names in special cases like empty name or simple structure. 
        /// </summary>
        public void ExpandAttribute(List<DimAttribute> attributes, List<DcColumn> columns) // Add and resolve attributes by creating dimension structure from FKs
        {
            DimAttribute att = this;

            if (att.Segments.Count > 0) return; // Already expanded (because of recursion)

            bool isKey = !string.IsNullOrEmpty(att.RelationalPkName) || att.IsKey;

            if (string.IsNullOrEmpty(att.RelationalFkName)) // No FK - primitive column - end of recursion
            {
                // Find or create a primitive dim segment
                DcColumn seg = columns.FirstOrDefault(c => c.Input == att.Input && StringSimilarity.SameColumnName(((DimRel)c).RelationalFkName, att.RelationalFkName));
                if (seg == null)
                {
                    seg = new DimRel(att.RelationalColumnName, att.Input, att.Output, isKey, false); // Maybe copy constructor?
                    ((DimRel)seg).RelationalFkName = att.RelationalFkName;
                    columns.Add(seg);
                }

                att.InsertLast(seg); // add it to this attribute as a single segment
            }
            else
            { // There is FK - non-primitive column
                // Find target set and target attribute (name resolution)
                DimAttribute tailAtt = attributes.FirstOrDefault(a => StringSimilarity.SameTableName(a.Input.Name, att.RelationalTargetTableName) && StringSimilarity.SameColumnName(a.Name, att.RelationalTargetColumnName));
                DcTable gSet = tailAtt.Input;

                // Find or create a dim segment
                DcColumn seg = columns.FirstOrDefault(c => c.Input == att.Input && StringSimilarity.SameColumnName(((DimRel)c).RelationalFkName, att.RelationalFkName));
                if (seg == null)
                {
                    seg = new DimRel(att.RelationalFkName, att.Input, gSet, isKey, false);
                    ((DimRel)seg).RelationalFkName = att.RelationalFkName;
                    columns.Add(seg);
                }

                att.InsertLast(seg); // add it to this attribute as first segment

                //
                // Recursion. Expand tail attribute and add all segments from the tail attribute (continuation)
                //
                tailAtt.ExpandAttribute(attributes, columns);
                att.InsertLast(tailAtt);

                // Adjust name. How many attributes belong to the same FK as this attribute (FK composition)
                List<DimAttribute> fkAtts = attributes.Where(a => a.Input == att.Input && StringSimilarity.SameColumnName(att.RelationalFkName, a.RelationalFkName)).ToList();
                if (fkAtts.Count == 1)
                {
                    seg.Name = att.RelationalColumnName; // Adjust name. For 1-column FK, name of the FK-dim is the column name (not the FK name)
                }
            }
        }


        /// <summary>
        /// Convert nested path to a flat canonical representation as a sequence of simple dimensions which do not contain other dimensions.
        /// Initially, paths are pairs <this set dim, greater set path>. We recursively replace all nested paths by dimensions.
        /// Also, adjust path names in special cases like empty name or simple structure. 
        /// </summary>
        [System.Obsolete("Use ExpandAttribute(s)", true)]
        public void ExpandPath()
        {
            //
            // Flatten all paths by converting <dim, greater-path> pairs by sequences of dimensions <dim, dim2, dim3,...>
            //
            List<DcColumn> allSegments = GetAllSegments();
            Segments.Clear();
            if (allSegments != null && allSegments.Count != 0)
            {
                Segments.AddRange(allSegments);
            }
            else
            {
                // ERROR: Wrong use: The path does not have the corresponding dimension
            }

            //
            // Adding missing paths. Particularly, non-stored paths (paths returning values which are stored only in the greater sets but not in this set).
            //
            if (!String.IsNullOrEmpty(RelationalFkName) /*&& Output.IdentityPrimitiveArity == 1*/)
            {
                Segments[0].Name = Name; // FK-name is overwritten and lost - attribute name is used instead
            }

            //
            // Dim name adjustment: for 1-column FK dimensions, we prefer to use its only column name instead of the FK-name (fkName is not used)
            //
            if (!String.IsNullOrEmpty(RelationalFkName) /*&& Output.IdentityPrimitiveArity == 1*/)
            {
                Segments[0].Name = Name; // FK-name is overwritten and lost - attribute name is used instead
            }
        }

        #region ComJson Serialization

        public override void ToJson(JObject json) // Write fields to the json object
        {
            base.ToJson(json); // DimPath

            json["RelationalColumnName"] = RelationalColumnName;
            json["RelationalPkName"] = RelationalPkName;
            json["RelationalFkName"] = RelationalFkName;
            json["RelationalTargetTableName"] = RelationalTargetTableName;
            json["RelationalTargetColumnName"] = RelationalTargetColumnName;
        }
        public override void FromJson(JObject json, DcWorkspace ws) // Init this object fields by using json object
        {
            base.FromJson(json, ws); // DimPath

            RelationalColumnName = (string)json["RelationalColumnName"];
            RelationalPkName = (string)json["RelationalPkName"];
            RelationalFkName = (string)json["RelationalFkName"];
            RelationalTargetTableName = (string)json["RelationalTargetTableName"];
            RelationalTargetColumnName = (string)json["RelationalTargetColumnName"];
        }

        #endregion

        public DimAttribute()
            : base()
        {
        }

        public DimAttribute(string name)
            : base(name)
        {
        }

        public DimAttribute(DimPath path)
            : base(path)
        {
        }

        public DimAttribute(string name, DcTable input, DcTable output)
            : base(name, input, output)
        {
        }
    }

}
