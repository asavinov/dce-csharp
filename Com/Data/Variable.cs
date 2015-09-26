﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Schema;

namespace Com.Data
{
    public class Variable : DcVariable
    {
        protected bool isNull;
        object Value;


        #region ComVariable interface

        public string Name { get; set; }

        public string SchemaName { get; set; }
        public string TypeName { get; set; }

        public void Resolve(DcWorkspace workspace)
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
                foreach (DcSchema schema in workspace.Schemas)
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

        public DcSchema TypeSchema { get; set; }
        public DcTable TypeTable { get; set; }

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

        public Variable(DcTable type, string name)
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