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


        //
        // Factories for tables and columns
        //

        /*
        public virtual DcTable CreateTable(String name) 
        {
            DcTable table = new Table(name);
            return table;
        }
        */

        /*
        public virtual DcTable AddTable(DcTable table, DcTable parent, string superName)
        {
            if (parent == null)
            {
                parent = Root;
            }
            if (string.IsNullOrEmpty(superName))
            {
                superName = "Super";
            }

            Column col = new Column(superName, table, parent, true, true);

            col.Add();

            return table;
        }
        */

        /*
        public virtual void DeleteTable(DcTable table) 
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: users do not create/delete primitive sets - they are part of the schema.");

            List<DcColumn> toRemove;
            toRemove = table.InputColumns.ToList();
            foreach (DcColumn col in toRemove) 
            {
                col.Remove();
            }
            toRemove = table.Columns.ToList();
            foreach (DcColumn col in toRemove)
            {
                col.Remove();
            }
        }
        */

        /*
        public void RenameTable(DcTable table, string newName)
        {
            TableRenamed(table, newName); // Rename with propagation
            table.Name = newName;
        }
        */

        /*
        public virtual DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: dimension name cannot be null or empty.");

            DcColumn col = new Column(name, input, output, isKey, false);

            return col;
        }
        */
        /*
        public virtual void DeleteColumn(DcColumn column)
        {
            Debug.Assert(!column.Input.IsPrimitive, "Wrong use: top columns cannot be created/deleted.");

            ColumnDeleted(column);
            column.Remove();
        }
        */
        /*
        public void RenameColumn(DcColumn column, string newName)
        {
            ColumnRenamed(column, newName); // Rename with propagation
            column.Name = newName;
        }
        */

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
