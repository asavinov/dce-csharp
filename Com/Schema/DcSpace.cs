using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Utils;

namespace Com.Schema
{
    public interface DcSpace : DcJson
    {
        //
        // Schemas
        //
        DcSchema CreateSchema(string name, DcSchemaKind schemaType);
        void DeleteSchema(DcSchema schema);
        List<DcSchema> GetSchemas();
        DcSchema GetSchema(string name);

        //
        // Tables
        //
        DcTable CreateTable(string name, DcTable parent);
        void DeleteTable(DcTable table);
        List<DcTable> GetTables(DcSchema schema);

        //
        // Columns
        //
        DcColumn CreateColumn(string name, DcTable input, DcTable output, bool isKey);
        void DeleteColumn(DcColumn column);
        List<DcColumn> GetColumns(DcTable table);
        List<DcColumn> GetInputColumns(DcTable table);

        //
        // Dependencies
        //
        Dependencies Dependencies { get; }
    }

    /// <summary>
    /// Schema types. 
    /// </summary>
    public enum DcSchemaKind
    {
        Dc, // Default. It is internal implementation
        Csv, // Csv
        Oledb, // Oledb
        Jdbc, // Jdbc
        Rel, // Rel can be an abstract parent for Jdbc and Oledb 
    }

}
