using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Rowid = System.Int32;
using Com.Utils;

namespace Com.Schema.Rel
{
    /// <summary>
    /// A relational table.
    /// </summary>
    public class SetRel : Set
    {
        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// We assume that there is only one PK (identity). Otherwise, we need a collection. 
        /// </summary>
        public string RelationalTableName { get; set; }
        public string RelationalPkName { get; set; } // Note that the same field exists also in Dim

        public DcColumn GetGreaterDimByFkName(string name)
        {
            return Columns.FirstOrDefault(d => StringSimilarity.SameColumnName(((DimRel)d).RelationalFkName, name));
        }

        #region Paths = relational attributes

        public List<DimAttribute> SuperPaths { get; private set; }
        public List<DimAttribute> SubPaths { get; private set; }
        public List<DimAttribute> GreaterPaths { get; private set; }
        public List<DimAttribute> LesserPaths { get; private set; }

        public void AddGreaterPath(DimAttribute path)
        {
            Debug.Assert(path.Output != null && path.Input != null, "Wrong use: path must specify a lesser and greater sets before it can be added to a set.");
            RemoveGreaterPath(path);
            if (path.Output is SetRel) ((SetRel)path.Output).LesserPaths.Add(path);
            if (path.Input is SetRel) ((SetRel)path.Input).GreaterPaths.Add(path);
        }
        public void RemoveGreaterPath(DimAttribute path)
        {
            Debug.Assert(path.Output != null && path.Input != null, "Wrong use: path must specify a lesser and greater sets before it can be removed from a set.");
            if (path.Output is SetRel) ((SetRel)path.Output).LesserPaths.Remove(path);
            if (path.Input is SetRel) ((SetRel)path.Input).GreaterPaths.Remove(path);
        }
        public void RemoveGreaterPath(string name)
        {
            DimAttribute path = GetGreaterPath(name);
            if (path != null)
            {
                RemoveGreaterPath(path);
            }
        }
        public DimAttribute GetGreaterPath(string name)
        {
            return GreaterPaths.FirstOrDefault(d => StringSimilarity.SameColumnName(d.Name, name));
        }
        public DimAttribute GetGreaterPathByColumnName(string name)
        {
            return GreaterPaths.FirstOrDefault(d => StringSimilarity.SameColumnName(d.RelationalColumnName, name));
        }
        public DimAttribute GetGreaterPath(DimAttribute path)
        {
            if (path == null || path.Segments == null) return null;
            return GetGreaterPath(path.Segments);
        }
        public DimAttribute GetGreaterPath(List<DcColumn> path)
        {
            if (path == null) return null;
            foreach (DimAttribute p in GreaterPaths)
            {
                if (p.Segments == null) continue;
                if (p.Segments.Count != path.Count) continue; // Different lengths => not equal

                bool equal = true;
                for (int seg = 0; seg < p.Segments.Count && equal; seg++)
                {
                    if (!StringSimilarity.SameColumnName(p.Segments[seg].Name, path[seg].Name)) equal = false;
                    // if (p.Path[seg] != path[seg]) equal = false; // Compare strings as objects
                }
                if (equal) return p;
            }
            return null;
        }
        public List<DimAttribute> GetGreaterPathsStartingWith(DimAttribute path)
        {
            if (path == null || path.Segments == null) return new List<DimAttribute>();
            return GetGreaterPathsStartingWith(path.Segments);
        }
        public List<DimAttribute> GetGreaterPathsStartingWith(List<DcColumn> path)
        {
            var result = new List<DimAttribute>();
            foreach (DimAttribute p in GreaterPaths)
            {
                if (p.Segments == null) continue;
                if (p.Segments.Count < path.Count) continue; // Too short path (cannot include the input path)
                if (p.StartsWith(path))
                {
                    result.Add(p);
                }
            }
            return result;
        }

        [System.Obsolete("What was the purpose of this method?", true)]
        public void AddAllNonStoredPaths()
        {
            // The method adds entity (non-PK) columns from referenced (by FK) tables (recursively).
            int pathCounter = 0;

            DimAttribute path = new DimAttribute("");
            PathEnumerator primPaths = new PathEnumerator(this, DimensionType.IDENTITY_ENTITY);
            foreach (DimAttribute p in primPaths)
            {
                if (p.Size < 2) continue; // All primitive paths are stored in this set. We need at least 2 segments.

                // Check if this path already exists
                path.Segments = p.Segments;
                if (GetGreaterPath(path) != null) continue; // Already exists

                string pathName = "__inherited__" + ++pathCounter;

                DimAttribute newPath = new DimAttribute(pathName);
                newPath.Segments = new List<DcColumn>(p.Segments);
                newPath.RelationalColumnName = newPath.Name; // It actually will be used for relational queries
                newPath.RelationalFkName = path.RelationalFkName; // Belongs to the same FK
                newPath.RelationalPkName = null;
                //newPath.Input = this;
                //newPath.Output = p.Path[p.Length - 1].Output;

                AddGreaterPath(newPath);
            }
        }

        #endregion

        #region ComJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["RelationalTableName"] = RelationalTableName;
            json["RelationalPkName"] = RelationalPkName;

            // List of greater paths (relational attributes)
            if (GreaterPaths != null)
            {
                JArray greater_paths = new JArray();
                foreach (var path in GreaterPaths)
                {
                    JObject greater_path = Utils.CreateJsonFromObject(path);
                    path.ToJson(greater_path);
                    greater_paths.Add(greater_path);
                }
                json["greater_paths"] = greater_paths;
            }

        }

        public override void FromJson(JObject json, DcWorkspace ws)
        {
            base.FromJson(json, ws); // Set

            RelationalTableName = (string)json["RelationalTableName"];
            RelationalPkName = (string)json["RelationalPkName"];

            // List of greater paths (relational attributes)
            if (json["greater_paths"] != null)
            {
                if (GreaterPaths == null) GreaterPaths = new List<DimAttribute>();
                foreach (JObject greater_path in json["greater_paths"])
                {
                    DimAttribute path = (DimAttribute)Utils.CreateObjectFromJson(greater_path);
                    if (path != null)
                    {
                        path.FromJson(greater_path, ws);
                        GreaterPaths.Add(path);
                    }
                }
            }
        }

        #endregion

        #region Constructors and initializers.

        public SetRel()
            : this("")
        {
        }

        public SetRel(string name)
            : base(name)
        {
            SuperPaths = new List<DimAttribute>();
            SubPaths = new List<DimAttribute>();
            GreaterPaths = new List<DimAttribute>();
            LesserPaths = new List<DimAttribute>();
        }

        #endregion
    }

}
