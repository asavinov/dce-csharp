using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Com.Model;

// Unit test: http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class CsTest
    {
        [TestMethod]
        public void SchemaTest() // Manually add/remove tables/columns
        {
            // Schema
            CsSchema schema = new SetTop("My Schema");

            //
            // Tables
            //
            CsTable t1 = schema.CreateTable("Table 1");
            schema.AddTable(t1, schema.Root, null);

            CsTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, t1, null);

            //
            // Columns
            //
            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetTable("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetTable("Double"), false);
            c12.Add();

            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetTable("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetTable("Double"), false);
            c22.Add();


            // Finding by name and check various properties provided by the schema
            Assert.AreEqual(schema.FindTable("Table 1"), t1);
            Assert.AreEqual(schema.FindTable("Table 2"), t2);
            Assert.AreEqual(t1.GetTable("Table 2"), t2);

            Assert.AreEqual(t1.GetGreaterDim("Column 11"), c11);
            Assert.AreEqual(t2.GetGreaterDim("Column 21"), c21);

            Assert.AreEqual(t2.GetGreaterDim("Super").IsSuper, true);
            Assert.AreEqual(t2.SuperDim.LesserSet, t2);
            Assert.AreEqual(t2.SuperDim.GreaterSet, t1);
        }

        [TestMethod]
        public void ColumnDataTest() // Manually read/write data
        {
            // Schema
            CsSchema schema = new SetTop("My Schema");

            // Tables
            CsTable t1 = schema.CreateTable("Table 1");
            schema.AddTable(t1, schema.Root, null);

            CsTable t2 = schema.CreateTable("Table 2");
            schema.AddTable(t2, t1, null);

            // Columns
            CsColumn c11 = schema.CreateColumn("Column 11", t1, schema.GetTable("Integer"), true);
            c11.Add();
            CsColumn c12 = schema.CreateColumn("Column 12", t1, schema.GetTable("Double"), false);
            c12.Add();

            CsColumn c21 = schema.CreateColumn("Column 21", t2, schema.GetTable("String"), true);
            c21.Add();
            CsColumn c22 = schema.CreateColumn("Column 22", t2, schema.GetTable("Double"), false);
            c22.Add();

            //
            // Data
            //

            t1.TableData.Length = 3; 
            // All functions' lengths are set in a loop. What about uniqueness of key columns? If we simply increase the length then it can be broken.

            // 2. Write/read individual column data by using column data methods (not table methods)
            c11.ColumnData.SetValue(1, 10);
            c11.ColumnData.SetValue(0, 20);
            c11.ColumnData.SetValue(2, 30);

            c12.ColumnData.SetValue(1, 10.0);
            c12.ColumnData.SetValue(0, 20.0);
            c12.ColumnData.SetValue(2, 30.0);

            t2.TableData.Length = 2;

            c21.ColumnData.SetValue(0, "Value 1");
            c21.ColumnData.SetValue(1, "Value 2");

            c22.ColumnData.SetValue(0, 1.0);
            c22.ColumnData.SetValue(1, 2.0);

            t2.SuperDim.ColumnData.SetValue(0, 1); // It is offset to the parent record
            t2.SuperDim.ColumnData.SetValue(1, 2);



            // Problem: for Super to Root (as well as for Top dims) we need a special implementation of ColumnData that returns constant null for all offsets (independent of the offset argument). 
            // For example, ConstantColumnData/NullColumnData/EmptyColumnData. It maintains correct length, but ingnores SetValue and GetValue returns NULL.
            // Currently, it is not set (ColumnData is null)
            // Alternatively, check ColumnData for NULL.

            // Primitive sets have a special implementation for Length. It cannot be changed (exception or ignored). And it returns some special value like -1 or Max.

        }

        [TestMethod]
        public void ColumnDefinitionTest() // Defining new columns and evaluate them
        {
        }

        [TestMethod]
        public void AggregationTest() // Defining new aggregated columns and evaluate them
        {
        }

        [TestMethod]
        public void TableDataTest() // Defining new tables and populate them
        {
        }

        [TestMethod]
        public void ProjectionTest() // Defining new tables via function projection and populate them
        {
        }

    }
}
