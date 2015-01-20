using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    public interface ComVariable // It is a storage element like function or table
    {
        //
        // Variable name (strictly speaking, it should belong to a different interface)
        //

        string Name { get; set; }

        //
        // Type info
        //

        string SchemaName { get; set; }
        string TypeName { get; set; }

        void Resolve(Workspace workspace); // Resolve schema name and table name (type) into object references

        ComSchema TypeSchema { get; set; } // Resolved schema name
        ComTable TypeTable { get; set; } // Resolved table name

        //
        // Variable data. Analogous to the column data interface but without input argument
        //

        bool IsNull();

        object GetValue();
        void SetValue(object value);

        void Nullify();

        //
        // Typed methods
        //

    }

    public class Variable : ComVariable
    {
        protected bool isNull;
        object Value;


        #region ComVariable interface

        public string Name { get; set; }

        public string SchemaName { get; set; }
        public string TypeName { get; set; }

        public void Resolve(Workspace workspace)
        {
            if (!string.IsNullOrEmpty(SchemaName))
            {
                // 1. Resolve schema name
                TypeSchema = workspace.GetSchema(SchemaName);
                if (TypeSchema == null) return; // Cannot resolve

                // 2. Resolve table name
                TypeTable = TypeSchema.GetSubTable(TypeName);
                if (TypeTable == null) return; // Cannot resolve
            }
            else if (!string.IsNullOrEmpty(TypeName)) // No schema name (imcomplete info)
            {
                // 1. try to find the table in the mashup 
                if (workspace.Mashup != null)
                {
                    TypeTable = workspace.Mashup.GetSubTable(TypeName);
                    if (TypeTable != null)
                    {
                        TypeSchema = workspace.Mashup;
                        SchemaName = TypeSchema.Name; // We also reconstruct the name
                        return;
                    }
                }

                // 2. try to find the table in any other schema
                foreach (ComSchema schema in workspace.Schemas)
                {
                    TypeTable = schema.GetSubTable(TypeName);
                    if (TypeTable != null)
                    {
                        TypeSchema = schema;
                        SchemaName = TypeSchema.Name; // We also reconstruct the name
                        return;
                    }
                }
            }
        }

        public ComSchema TypeSchema { get; set; }
        public ComTable TypeTable { get; set; }

        public bool IsNull()
        {
            return isNull;
        }

        public object GetValue()
        {
            return isNull ? null : Value;
        }

        public void SetValue(object value)
        {
            if (value == null)
            {
                Value = null;
                isNull = true;
            }
            else
            {
                Value = value;
                isNull = false;
            }
        }

        public void Nullify()
        {
            isNull = true;
        }

        #endregion 

        public Variable(string schema, string type, string name)
        {
            SchemaName = schema;
            TypeName = type;

            Name = name;

            isNull = true;
            Value = null;
        }

        public Variable(ComTable type, string name)
        {
            SchemaName = type.Schema.Name;
            TypeName = type.Name;

            TypeSchema = type.Schema;
            TypeTable = type;

            Name = name;

            isNull = true;
            Value = null;
        }
    }

}
