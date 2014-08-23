using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Offset = System.Int32;

using Com.Model;

// Unit test: http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class DcTest
    {

        [TestMethod]
        public void SchemaTest()
        {
            string schemaJson;
            string tableJson;
            string columnJson;
            DataCommander cs = new DataCommander();

            // Create schemas
            schemaJson = @"{ name=""MySchema""} ";
            DcScriptSchema schema = cs.CreateSchema(schemaJson);

            schemaJson = @"{ name=""MySchema"", kind=""csv""} ";
            DcScriptSchema schemaCsv = cs.CreateSchema(schemaJson);

            // Import remote schema
            tableJson = @"{ name=""C:\Users\savinov\git\comcsharp\Test\Products.csv""} ";
            DcScriptTable sourceTable = schemaCsv.ImportSchema(tableJson, null);

            tableJson = @"{ name=""Products"" }";
            DcScriptTable targetTable = schema.CreateTable(tableJson); // Create local target table

            // Create projection column
            columnJson = @"{ name=""Import"" }";
            DcScriptColumn importColumn = schema.CreateColumn(columnJson, sourceTable, targetTable);

            //importColumn.SetProperty("definition.formula", "(( (String)col1=this.col1, (Double)col2=this.col2,  ))");

            //targetTable.Populate(null);

            // Where inter-schema columns are store?
            // - ??? it is inter-schema column: which schema to use for its creation?
            //   - SOLUTION 1: any one: the creation method will detect that it is inter-schema column. maybe it should stored in the DC object.
            //   - SOLUTION 2: in all cases, use either schema of the greater set or the schema of the lesser set
            //   - SOLUTION 3: use DC object, and it will be stored in the DC object

            // - Populate target table by calling the corresponding API method.
            //   - We can populate the table - the column will be populated automatically, but for import dims it is not populated
            //   - We can populate the column - the only difference is that it will not empty the target table
            
            // SOLUTION: a column has a flag projection/generating/appending etc. which simply say that its output is appended if does not exist - nothing else
            // - "append_output_data=true" means that outputs will be appended if not found (missing)
            // - "append_output_columns=true" means that missing dimensions will be created (missing is detected during resolution/binding)
            //   - when? probably when the column is created or mapping is added so it is always up-to-date after each change. because we might want to use these columns for further definitions (yet, for lasy resolution they do not have to be created)
            // - deleting a column in target table is processed like any other deletion (w. propagation or not)
            // - adding columns or appending data tuples does not influence already existing columns/data. Yet, if there are free dims which are not used, the system either warns (and ignores) or errors (not implemented). 

            // How to distinguish a pair of <projection column, generated table>?
            // Theoretically, projection dimension is defined as usual independent of its use (as a generator).
            // In practice, all projection dimensions are defined via mapping (output is a tuple).
            // Important are the following things (checked during resolution): 
            // - output tuple of the column must be compatible with the target table. 
            //   - theoretically, output tuple have to be a subset of the free dims of the target table. other free dims take all possible values (like in product operation).
            //   - practically, it is much easier to do so that output tuple is equal to the free dims of the target table
            // - we cannot add free dims to the target table (exception with explanation, that is, the mapping has to be changed instead)
            //   - again, theoretically it should be possible, but then we need to generalize the population procedure
            // - we can delete a free dimension but then it is necessary to update the mapping 

            // There are the following solutions:
            // 1. Depenency. Free dims of the target table are defined by the projection column, so we cannot edit them. So the structure is managed automatically. Yet, it should be possible to change some parameters like Name or maybe even types with automatic updates of the mapping. 
            // 2. Immediate resolution. We must create structure of the target table, and only after that can define a compatible mapping that uses this structure.
            // 3. No resolution (lasy resolution of mapping). We can define a column with mapping as a generating column for arbitrary target table (possibly with warnings). And we can define target table indepenent of its generating column. Inconsistencies will be detected only at population time during concistency check (instantiating an evaluator) when error message will be raised. 
            // 4. Explicit API convenience methods: 
            //    - Explicit method (API call) for creating/deriving target table structure from the mapping which is called whenever we think it is necessary
            //    - A method for generating a meaningful and consistent mapping taking into account the existing target structure (it is part of the recommendation engine)
            // 5. Depends on configuration (convenience options):
            //    - free mode where we can change almost everything and errors appear only at population time. 
            //    - table structure depends on and is automatically derived from the generating dimension. In other words, we append not only data but also missing columns
            //      - we are not able to change this structure.
            //      - the structure will be recreated for each population (if the mapping has change).
            //      - it is a restricted mode, because we can only copy data. more complex expressions are not possible. 
            //      - also, we cannot change names




        }

    }
}
