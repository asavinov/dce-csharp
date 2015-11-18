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
    public class TableRel : Table
    {
        /// <summary>
        /// Additional names specific to the relational model and maybe other PK-FK-based models.
        /// We assume that there is only one PK (identity). Otherwise, we need a collection. 
        /// </summary>
        public string RelationalTableName { get; set; }
        public string RelationalPkName { get; set; } // Note that the same field exists also in Dim

        public DcColumn GetGreaterColByFkName(string name)
        {
            return Columns.FirstOrDefault(d => StringSimilarity.SameColumnName(((ColumnRel)d).RelationalFkName, name));
        }

        #region Paths = relational attributes

        public List<ColumnAtt> SuperPaths { get; private set; }
        public List<ColumnAtt> SubPaths { get; private set; }
        public List<ColumnAtt> GreaterPaths { get; private set; }
        public List<ColumnAtt> LesserPaths { get; private set; }

        public void AddGreaterPath(ColumnAtt path)
        {
            Debug.Assert(path.Output != null && path.Input != null, "Wrong use: path must specify a lesser and greater sets before it can be added to a set.");
            RemoveGreaterPath(path);
            if (path.Output is TableRel) ((TableRel)path.Output).LesserPaths.Add(path);
            if (path.Input is TableRel) ((TableRel)path.Input).GreaterPaths.Add(path);
        }
        public void RemoveGreaterPath(ColumnAtt path)
        {
            Debug.Assert(path.Output != null && path.Input != null, "Wrong use: path must specify a lesser and greater sets before it can be removed from a set.");
            if (path.Output is TableRel) ((TableRel)path.Output).LesserPaths.Remove(path);
            if (path.Input is TableRel) ((TableRel)path.Input).GreaterPaths.Remove(path);
        }
        public void RemoveGreaterPath(string name)
        {
            ColumnAtt path = GetGreaterPath(name);
            if (path != null)
            {
                RemoveGreaterPath(path);
            }
        }
        public ColumnAtt GetGreaterPath(string name)
        {
            return GreaterPaths.FirstOrDefault(d => StringSimilarity.SameColumnName(d.Name, name));
        }
        public ColumnAtt GetGreaterPathByColumnName(string name)
        {
            return GreaterPaths.FirstOrDefault(d => StringSimilarity.SameColumnName(d.RelationalColumnName, name));
        }
        public ColumnAtt GetGreaterPath(ColumnAtt path)
        {
            if (path == null || path.Segments == null) return null;
            return GetGreaterPath(path.Segments);
        }
        public ColumnAtt GetGreaterPath(List<DcColumn> path)
        {
            if (path == null) return null;
            foreach (ColumnAtt p in GreaterPaths)
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
        public List<ColumnAtt> GetGreaterPathsStartingWith(ColumnAtt path)
        {
            if (path == null || path.Segments == null) return new List<ColumnAtt>();
            return GetGreaterPathsStartingWith(path.Segments);
        }
        public List<ColumnAtt> GetGreaterPathsStartingWith(List<DcColumn> path)
        {
            var result = new List<ColumnAtt>();
            foreach (ColumnAtt p in GreaterPaths)
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

            ColumnAtt path = new ColumnAtt("");
            PathEnumerator primPaths = new PathEnumerator(this, ColumnType.IDENTITY_ENTITY);
            foreach (ColumnAtt p in primPaths)
            {
                if (p.Size < 2) continue; // All primitive paths are stored in this set. We need at least 2 segments.

                // Check if this path already exists
                path.Segments = p.Segments;
                if (GetGreaterPath(path) != null) continue; // Already exists

                string pathName = "__inherited__" + ++pathCounter;

                ColumnAtt newPath = new ColumnAtt(pathName);
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

        #region DcJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Table

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

        public override void FromJson(JObject json, DcSpace ws)
        {
            base.FromJson(json, ws); // Table

            RelationalTableName = (string)json["RelationalTableName"];
            RelationalPkName = (string)json["RelationalPkName"];

            // List of greater paths (relational attributes)
            if (json["greater_paths"] != null)
            {
                if (GreaterPaths == null) GreaterPaths = new List<ColumnAtt>();
                foreach (JObject greater_path in json["greater_paths"])
                {
                    ColumnAtt path = (ColumnAtt)Utils.CreateObjectFromJson(greater_path);
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

        public TableRel(DcSpace space)
            : this("", space)
        {
        }

        public TableRel(string name, DcSpace space)
            : base(name, space)
        {
            SuperPaths = new List<ColumnAtt>();
            SubPaths = new List<ColumnAtt>();
            GreaterPaths = new List<ColumnAtt>();
            LesserPaths = new List<ColumnAtt>();
        }

        #endregion
    }

}
