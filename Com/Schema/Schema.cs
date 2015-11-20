using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Data;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Schema
{
    /// <summary>
    /// Top set in a poset of all sets. It is a parent for all primitive sets.
    /// 
    /// Top set is used to represent a whole database like a local mash up or a remote database. 
    /// It also can describe how its instances are loaded from a remote source and stored.
    /// </summary>
    public class Schema : Table, DcSchema
    {

        #region DcSchema interface

        protected DcSchemaKind _schemaKind;
        public virtual DcSchemaKind GetSchemaKind() { return _schemaKind; }

        public DcTable GetPrimitive(string name)
        {
            DcColumn col = SubColumns.FirstOrDefault(x => StringSimilarity.SameTableName(x.Input.Name, name));
            return col != null ? col.Input : null;
        }

        public DcTable Root { get { return GetPrimitive("Root"); } }

        #endregion

        #region DcJson serialization

        public override void ToJson(JObject json)
        {
            base.ToJson(json); // Set

            json["SchemaKind"] = (int)_schemaKind;
        }

        public override void FromJson(JObject json, DcSpace ws)
        {
            base.FromJson(json, ws); // Set

            _schemaKind = json["SchemaKind"] != null ? (DcSchemaKind)(int)json["SchemaKind"] : DcSchemaKind.Dc;

            // List of tables
            // Tables are stored in space

            // List of columns
            // Columns cannot be loaded because not all schemas might have been loaded (so it is a problem with import columns)
        }

        #endregion

        protected virtual void CreateDataTypes() // Create all primitive data types from some specification like Enum, List or XML
        {
            Space.CreateTable("Root", this);
            Space.CreateTable("Integer", this);
            Space.CreateTable("Double", this);
            Space.CreateTable("Decimal", this);
            Space.CreateTable("String", this);
            Space.CreateTable("Boolean", this);
            Space.CreateTable("DateTime", this);
        }

        public Schema(DcSpace space)
            : this("", space)
        {
        }

        public Schema(string name, DcSpace space)
            : base(name, space)
        {
            _schemaKind = DcSchemaKind.Dc;

            CreateDataTypes(); // Generate all predefined primitive sets as subsets
        }

    }

}
