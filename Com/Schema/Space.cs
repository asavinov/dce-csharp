using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;
using System.Collections.Specialized;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Utils;
using Com.Schema.Csv;
using Com.Schema.Rel;
using Com.Data;

namespace Com.Schema
{
    /// <summary>
    /// Workspace is a number of schemas as well as parameters for their management. 
    /// </summary>
    public class Space : DcSpace, INotifyPropertyChanged, INotifyCollectionChanged
    {
        #region DcSpace

        //
        // Schemas
        //

        protected List<DcSchema> _schemas;

        public virtual DcSchema CreateSchema(string name, DcSchemaKind schemaType)
        {
            DcSchema schema;

            if (schemaType == DcSchemaKind.Dc)
            {
                schema = new Schema(name, this);
            }
            else if (schemaType == DcSchemaKind.Csv)
            {
                schema = new SchemaCsv(name, this);
            }
            else if (schemaType == DcSchemaKind.Oledb)
            {
                schema = new SchemaOledb(name, this);
            }
            else if (schemaType == DcSchemaKind.Rel)
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _schemas.Add(schema);

            NotifyAdd(schema);

            return schema;
        }

        public virtual void DeleteSchema(DcSchema schema)
        {
            // We have to ensure that inter-schema (import/export) columns are also deleted
            List<DcTable> allTables = this.GetTables(schema); // schema.AllSubTables;
            foreach (DcTable t in allTables)
            {
                if (t.IsPrimitive) continue;
                this.DeleteTable(t);
            }

            _schemas.Remove(schema);

            NotifyRemove(schema);
        }
        public virtual List<DcSchema> GetSchemas()
        {
            return new List<DcSchema>(_schemas);
        }
        public virtual DcSchema GetSchema(string name)
        {
            return _schemas.FirstOrDefault(x => StringSimilarity.SameTableName(x.Name, name));
        }

        //
        // Tables
        //

        protected List<DcTable> _tables;

        public virtual DcTable CreateTable(DcSchemaKind schemaType, string name, DcTable parent)
        {
            DcSchema schema = parent.Schema;
            //DcSchemaKind schemaType = schema.GetSchemaKind();

            DcTable table;
            Column column;
            string colName;
            if (parent is DcSchema)
            {
                colName = "Top";
            }
            else
            {
                colName = "Super";
            }

            if (schemaType == DcSchemaKind.Dc)
            {
                table = new Table(name, this);
                column = new Column(colName, table, parent, true, true);
            }
            else if (schemaType == DcSchemaKind.Csv)
            {
                table = new TableCsv(name, this);
                column = new ColumnCsv(colName, table, parent, true, true);
            }
            else if (schemaType == DcSchemaKind.Oledb)
            {
                table = new TableRel(name, this);
                column = new ColumnRel(colName, table, parent, true, true);
            }
            else if (schemaType == DcSchemaKind.Rel)
            {
                table = new TableRel(name, this);
                column = new ColumnRel(colName, table, parent, true, true);
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _tables.Add(table);
            NotifyAdd(table);

            _columns.Add(column);
            NotifyAdd(column);

            return table;
        }

        public virtual void DeleteTable(DcTable table)
        {
            Debug.Assert(!table.IsPrimitive, "Wrong use: primitive tables can be deleted only along with the schema.");

            //DeleteTablePropagate(table);

            List<DcColumn> toRemove;

            // Remove input columns (for which this table is a type)
            toRemove = table.InputColumns.ToList();
            foreach (DcColumn col in toRemove)
            {
                this.DeleteColumn(col);
                NotifyRemove(col);
            }

            // Remove output columns
            toRemove = table.Columns.ToList();
            foreach (DcColumn col in toRemove)
            {
                this.DeleteColumn(col);
                NotifyRemove(col);
            }

            // Finally, remove the table itself
            _tables.Remove(table);
            NotifyRemove(table);
        }
        protected void DeleteTablePropagate(DcTable table)
        {
            // 
            // Delete tables *generated* from this table (alternatively, leave them but with empty definition)
            //
            var paths = new PathEnumerator(new List<DcTable>(new DcTable[] { table }), new List<DcTable>(), false, ColumnType.GENERATING);
            foreach (var path in paths)
            {
                for (int i = path.Segments.Count - 1; i >= 0; i--)
                {
                    this.DeleteTable(path.Segments[i].Output); // Delete (indirectly) generated table
                }
            }
        }
        public virtual List<DcTable> GetTables(DcSchema schema)
        {
            if(schema == null)
            {
                return new List<DcTable>(_tables);
            }
            else
            {
                return _tables.Where(x => x.Schema == schema).ToList();
            }
        }

        //
        // Columns
        //

        protected List<DcColumn> _columns;

        public virtual DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey)
        {
            Debug.Assert(!String.IsNullOrEmpty(name), "Wrong use: column name cannot be null or empty.");
            // TODO: Check constraints: 1. only one super-column can exist 2. no loops can appear

            DcSchemaKind inSchemaType = input.Schema.GetSchemaKind();
            DcSchemaKind outSchemaType = output.Schema.GetSchemaKind();

            DcColumn column;

            if (inSchemaType == DcSchemaKind.Dc || outSchemaType == DcSchemaKind.Dc) // Intra-mashup or import/export columns
            {
                column = new Column(name, input, output, isKey, false);
            }
            else if (inSchemaType == DcSchemaKind.Csv && outSchemaType == DcSchemaKind.Csv) // Intra-csv columns
            {
                column = new ColumnCsv(name, input, output, isKey, false);
            }
            else if (inSchemaType == DcSchemaKind.Oledb && outSchemaType == DcSchemaKind.Oledb) // Intra-oledb columns
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }
            else if (inSchemaType == DcSchemaKind.Rel && outSchemaType == DcSchemaKind.Rel) // Intra-rel columns
            {
                column = new ColumnRel(name, input, output, isKey, false);
            }
            else
            {
                throw new NotImplementedException("This schema type is not implemented.");
            }

            _columns.Add(column);

            NotifyAdd(column);

            return column;
        }

        /* TODO: Notify after adding
        public virtual void Add()
        {
            if (IsSuper) // Only one super-dim per table can exist
            {
                if (Input != null && Input.SuperColumn != null)
                {
                    Input.SuperColumn.Remove(); // Replace the existing column by the new one
                }
            }

            if (Output != null) Output.InputColumns.Add(this);
            if (Input != null) Input.Columns.Add(this);

            // Notify that a new child has been added
            if (Input != null) ((Table)Input).NotifyAdd(this);
            if (Output != null) ((Table)Output).NotifyAdd(this);
        }
        */
        public virtual void DeleteColumn(DcColumn column)
        {
            Debug.Assert(!column.Input.IsPrimitive, "Wrong use: top columns cannot be created/deleted.");
            // TODO: Check constraints: deleting a super-column means deleting the corresponding table (with all its columns)

            //DeleteColumnPropagate(column);

            _columns.Remove(column);

            NotifyRemove(column);
        }
        /* TODO: Notify after deleting
        public virtual void Remove()
        {
            if (Output != null) Output.InputColumns.Remove(this);
            if (Input != null) Input.Columns.Remove(this);

            // Notify that a new child has been removed
            if (Input != null) ((Table)Input).NotifyRemove(this);
            if (Output != null) ((Table)Output).NotifyRemove(this);
        }
        */
        protected void DeleteColumnPropagate(DcColumn column)
        {
            DcSchema schema = column.Input.Schema;

            // 
            // Delete related columns/tables
            //
            if (column.GetData().IsAppendData) // Delete all tables that are directly or indirectly generated by this column
            {
                DcTable gTab = column.Output;
                var paths = new PathEnumerator(new List<DcTable>(new DcTable[] { gTab }), new List<DcTable>(), false, ColumnType.GENERATING);
                foreach (var path in paths)
                {
                    for (int i = path.Segments.Count - 1; i >= 0; i--)
                    {
                        this.DeleteTable(path.Segments[i].Output); // Delete (indirectly) generated table
                    }
                }
                this.DeleteTable(gTab); // Delete (directly) generated table
                // This column will be now deleted as a result of the deletion of the generated table
            }
            else if (column.Input.GetData().DefinitionType == TableDefinitionType.PROJECTION) // It is a extracted table and this column is produced by the mapping (depends on function output tuple)
            {
                //DcColumn projDim = column.Input.InputColumns.Where(d => d.Definition.IsAppendData).ToList()[0];
                //Mapping mapping = projDim.Definition.Mapping;
                //PathMatch match = mapping.GetMatchForTarget(new DimPath(column));
                //mapping.RemoveMatch(match.SourcePath, match.TargetPath);
            }

            //
            // Delete all expression nodes that use the deleted column and all references to this column from other objects
            //
            List<DcTable> tables = schema.AllSubTables;
            var nodes = new List<ExprNode>();
            foreach (var tab in tables)
            {
                if (tab.IsPrimitive) continue;

                foreach (var col in tab.Columns)
                {
                    if (col.GetData() == null) continue;
                    DcColumnData data = col.GetData();

                    /* REFACTOR: Here essentially we want to manually find all uses and hence have to use dependencies API
                    if (data.FormulaExpr != null)
                    {
                        nodes = data.FormulaExpr.Find(column);
                        foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                    }
                    */

                }

                // Update table definitions by finding the uses of the specified column
                if (tab.GetData().WhereExpr != null)
                {
                    nodes = tab.GetData().WhereExpr.Find(column);
                    foreach (var node in nodes) if (node.Parent != null) node.Parent.RemoveChild(node);
                }
            }
        }
        public virtual List<DcColumn> GetColumns(DcTable table)
        {
            if (table == null)
            {
                return new List<DcColumn>(_columns);
            }
            else
            {
                return _columns.Where(x => x.Input == table).ToList();
            }
        }
        public virtual List<DcColumn> GetInputColumns(DcTable table)
        {
            if (table == null)
            {
                return new List<DcColumn>(_columns);
            }
            else
            {
                return _columns.Where(x => x.Output == table).ToList();
            }
        }

        //
        // Dependencies
        //

        private Dependencies _dependencies;
        public virtual Dependencies Dependencies { get { return _dependencies; } }

        #endregion

        #region Json serialization

        public virtual void ToJson(JObject json)
        {
            // List of schemas
            JArray schemas = new JArray();
            foreach (DcSchema sch in GetSchemas())
            {
                JObject schema = Utils.CreateJsonFromObject(sch);
                sch.ToJson(schema);
                schemas.Add(schema);
            }
            json["schemas"] = schemas;

            // List of tables
            JArray tables = new JArray();
            foreach (DcTable comTable in _tables)
            {
                if (comTable.IsPrimitive) continue;

                JObject table = Utils.CreateJsonFromObject(comTable);
                comTable.ToJson(table);
                tables.Add(table);
            }
            json["tables"] = tables;


            // List of tables
            JArray columns = new JArray();
            foreach (DcColumn comColumn in _columns)
            {
                JObject column = Utils.CreateJsonFromObject(comColumn);
                comColumn.ToJson(column);
                columns.Add(column);
            }
            json["columns"] = columns;
        }

        public virtual void FromJson(JObject json, DcSpace ws)
        {
            // List of schemas
            foreach (JObject schema in json["schemas"])
            {
                DcSchema sch = (DcSchema)Utils.CreateObjectFromJson(schema);
                if (sch != null)
                {
                    sch.FromJson(schema, this);
                    _schemas.Add(sch);
                }
            }

            // List of tables
            foreach (JObject table in json["tables"])
            {
                DcTable tab = (DcTable)Utils.CreateObjectFromJson(table);
                if (tab != null)
                {
                    tab.FromJson(table, this);
                    _tables.Add(tab);
                }
            }

            // Load all columns from all schema (now all tables are present)
            foreach (JObject schema in json["schemas"])
            {
                foreach (JObject column in schema["columns"]) // List of columns
                {
                    DcColumn col = (DcColumn)Utils.CreateObjectFromJson(column);
                    if (col != null)
                    {
                        col.FromJson(column, this);
                        _columns.Add(col);
                    }
                }
            }

            // Second pass on all columns with the purpose to load their definitions (now all columns are present)
            foreach (JObject schema in json["schemas"])
            {
                foreach (JObject column in schema["columns"]) // List of columns
                {
                    DcColumn col = (DcColumn)Utils.CreateObjectFromJson(column);
                    if (col != null)
                    {
                        col.FromJson(column, this);

                        // Find the same existing column (possibly without a definition)
                        DcColumn existing = col.Input.GetColumn(col.Name);

                        // Copy the definition
                        existing.FromJson(column, this);
                    }
                }
            }

        }

        #endregion

        #region System interfaces

        public event NotifyCollectionChangedEventHandler CollectionChanged; // Operations with collections (schemas, tables, columns)
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, e);
            }
        }
        public virtual void NotifyAdd(object elem) // Convenience method: notifying about adding
        {
            if (elem == null) return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, elem));
        }
        public virtual void NotifyRemove(object elem) // Convenience method: notifying about removing
        {
            if (elem == null) return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, elem));
        }

        //
        // INotifyPropertyChanged Members
        //
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public virtual void NotifyPropertyChanged(String propertyName = "") // Convenience method: notifying all about property change
        {
            OnPropertyChanged(propertyName);
        }

        #endregion

        public Space()
        {
            _schemas = new List<DcSchema>();
            _tables = new List<DcTable>();
            _columns = new List<DcColumn>();

            _dependencies = new Dependencies();
        }

    }

    /// <summary>
    /// Space dependencies.
    /// - Manage (store, update) the graph. Re-building the graph.
    /// - Retrieve/query the graph
    /// - Find inconsistencies like cycles and manage them
    /// The graph is fed from outside, i.e., the class cannot access column dependencies. 
    /// In other words, columns have to provide their dependencies (and update them) themselves. 
    /// The class could build the graph itself if columns provide API for getting their dependencies.
    /// </summary>
    public class Dependencies
    {
        private List<Tuple<DcColumn, DcColumn>> dependencies = new List<Tuple<DcColumn, DcColumn>>();

        //
        // Retrieving graph data
        //
        public List<DcColumn> GetUsed(DcColumn column)
        {
            return dependencies.Where(d => d.Item1 == column).Select(d => d.Item2).ToList();
        }
        public List<DcColumn> GetUsedIn(DcColumn column)
        {
            return dependencies.Where(d => d.Item2 == column).Select(d => d.Item1).ToList();
        }

        //
        // Change the graph
        //

        // Add columns the specified column depends on 
        // The specified columns uses these columns either in its formula or is influenced by them
        // Existing dependencies are not removed but duplicates are not added
        public void AddUsed(DcColumn column, List<DcColumn> usedColumns)
        {
            List<Tuple<DcColumn, DcColumn>> colDeps = dependencies.Where(d => d.Item1 == column).ToList();

            // Add each individual dependency
            foreach (DcColumn col in usedColumns)
            {
                // Check if it is already there
                if (colDeps.Exists(d => d.Item2 == col)) continue;

                // If not then add it
                dependencies.Add(new Tuple<DcColumn, DcColumn>(column, col));

                // Add also to the temporary list so that we can check for duplicates
                colDeps.Add(new Tuple<DcColumn, DcColumn>(column, col));
            }
        }
        public void RemoveUsed(DcColumn column)
        {
            dependencies.RemoveAll(d => d.Item1 == column);
        }
        public void Remove(DcColumn column)
        {
            dependencies.RemoveAll(d => d.Item1 == column || d.Item2 == column);
        }

        //
        // Analyze and build the graph
        //

        public void Rebuild()
        {
            // Recompute the complete graph of columns

            // One question by generating a graph is whether we have to perform translation (parse, bind)
            // or we assume that it has been done already 
            // Parsing is normally not needed 
            // Binding might be needed in the case of name changes, type changes etc.

            // Another question is whether we need to deal with status propagation and status change
            // If we do translation then it will triger status change and status propagation

            // Parsing and Binding are individual to each column 
            // Status is also reflects the used columns status (it is propagated)
            // So we might separate Parsing/Binding status and inherited status

            // Generally, we can translate (parse and bind) columns individually. 
            // However, we need to update the dependency graph which finally is used to get final status. 
            // Public API has to provide operations which produce consistent state, that is, 
            // either dependencies have to be dynamically computed for each request or translation has to update dependencies.
            // The problem with updating dependencies is that this may trigger translation of other columns in the graph. 
            // So column change leads to translation which needs to re-translate other columns which will lead to graph update again. 

            // One assumption is that if we change a formula then only this column has to be re-translated (parse, bind) because it is an independent operation. 
            // This column also changes its individual dependency graph. 
            // All other columns ar known to have correct translation state. 
            // Yet, this and other columns may get a different final state which is computed from (updated) dependency graph.
            // The strategy here is to translate (parse, bind) and immediately update individual dependencies. 
            // So we split two operations: translate (parse, bind) w. always individual dependencies update and computing final state.
            // Computing final status involves analysis of the graph including status propagation and cyclie analysis.

            // If we change name, then this formula needs not be translated at all. 
            // Rather, other formulas might need to be re-translated (in fact, re-bound) or we could simply optimize it by propagate name change throught the space. 
            // So either we rebuild the whole graph (translate all) or propagate name change. 

            // If delete a column then we update the graph for this column only. 

            // In the case of structural changes (type change, copy/paste etc.) we can rebuilt the whole graph. 
            // Here we re-translate all individual columns and also re-compute their individual dependencies.

            // So maybe translate should not be public operation because it is always called from other methods.
            // For example, setting Formula, Name, Delete, Constructor etc. 
            // We never call Translate from outside. 

            // Thus we need to separate operations on individual columns like translation (parsing, binding). 
            // Translation is performed on individual columns but it generates the graph. 
            // In other words, if we want to create/update the graph, we do it via translation of individual columns.
            // Each column then stores the results of translation (error code).
        }
        public void Rebuild(DcSchema schema)
        {
        }
        public void Rebuild(DcColumn column)
        {
            // Assume that the specified column has changed (name, formula etc.)
            // The column has been translated (not necessarily successfully)
            // We need to update dependencies by removing old and adding new

            // If formula has been changed then it can appear, disapper, or updated
            // We need to retrieve used columns by removing the old columns

            // If name has been changed then its own deps do not change. 
            // However, all columns which use it, have to be re-translated (and will be red which will be propagated)

            // If type has been changed then we need to re-translate 
        }

    }

}
