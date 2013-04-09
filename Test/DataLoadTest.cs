﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.Model;

namespace Test
{
    [TestClass]
    public class DataLoadTest
    {
        [TestMethod]
        public void PrimDimensinTest()
        {
            Dimension dim = new DimPrimitive<int>("Test", null, null);

            //
            // Insert new data
            //
            dim.Append(10);
            dim.Append(30);
            dim.Append(20);
            Assert.AreEqual(3, dim.Length);

            dim.SetValue(0, 50);
            dim.SetValue(1, 50);
            dim.SetValue(2, 50);

            dim.SetValue(1, 10);
        }

        [TestMethod]
        public void TableLoadTest()
        {
            //
            // Prepare table schema
            //
            SetRoot root = new SetRoot("Root");
            Set setInteger = root.GetPrimitiveSubset("Integer");
            Set setDouble = root.GetPrimitiveSubset("Double");
            Set setString = root.GetPrimitiveSubset("String");

            // Insert table
            Set t1 = new Set("t1");
            t1.SuperDim = new DimRoot("super", t1, root);

            Dimension orders = new DimPrimitive<int>("orders", t1, setInteger);
            t1.AddGreaterDim(orders);

            Dimension revenue = new DimPrimitive<double>("revenue", t1, setDouble);
            t1.AddGreaterDim(revenue);

            Dimension name = new DimPrimitive<string>("name", t1, setString);
            t1.AddGreaterDim(name);

            //
            // Insert new data
            //
            t1.Append();
            Assert.AreEqual(1, t1.Length);
            
            t1.SetValue("orders", 0, 10);
            t1.SetValue("revenue", 0, 12345.67);
            t1.SetValue("name", 0, "Smith");

            Assert.AreEqual(10, t1.GetValue("orders", 0));
            Assert.AreEqual(12345.67, t1.GetValue("revenue", 0));
            Assert.AreEqual("Smith", t1.GetValue("name", 0));
        }

        [TestMethod]
        public void OledbSchemaLoadTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Root");

            dbRoot.ConnectionString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test\\Northwind.accdb";
            // Another provider: "Provider=Microsoft.Jet.OLEDB.4.0;"

            dbRoot.Open();

            dbRoot.ImportSchema();

            // Check validity of the schema
            Set epSet = dbRoot.FindSubset("Employee Privileges");
            Assert.AreEqual(2, epSet.GreaterDims.Count);
            Assert.AreEqual(2, epSet.GreaterPaths.Count);
            Assert.AreEqual(2, epSet.GreaterPaths[0].Rank);
            Assert.AreEqual(2, epSet.GreaterPaths[1].Rank);

            Set doubleSet = dbRoot.FindSubset("Double");
            Assert.AreEqual(2, doubleSet.LesserDims.Count);
            Assert.AreEqual(2, doubleSet.LesserPaths.Count);

            Set empSet = dbRoot.FindSubset("Employees");
            System.Data.DataTable dataTable = dbRoot.Export(empSet);
            Assert.AreEqual(9, dataTable.Rows.Count);
            Assert.AreEqual(18, dataTable.Columns.Count);
        }

        [TestMethod]
        public void OledbSchemaImportTest()
        {
            //
            // Create Oldedb root set
            //
            SetRootOledb dbRoot = new SetRootOledb("Root");
            dbRoot.ConnectionString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test\\Northwind.accdb";
            dbRoot.Open();
            dbRoot.ImportSchema();

            //
            // Create workspace root set
            //
            SetRoot wsRoot = new SetRoot("My Mashup");

            // Add a new set
            Set empPriv = new Set("Emp", dbRoot.FindSubset("Employee Privileges"));
            empPriv.SuperDim = new DimRoot("super", empPriv, wsRoot); // Insert the set (no dimensions)

            // Import dimension(s)
            empPriv.ImportDimensions();

            // Assert. Check imported dimensions
            Assert.AreEqual(2, empPriv.GreaterPaths.Count);
            Assert.AreEqual(2, empPriv.GreaterDims.Count);

            // Check intermediate sets and their imported structure
            Assert.AreEqual(18, empPriv.GreaterDims[0].GreaterSet.GreaterPaths.Count);
            Assert.AreEqual(18, empPriv.GreaterDims[0].GreaterSet.GreaterDims.Count);

            Assert.AreEqual(2, empPriv.GreaterDims[1].GreaterSet.GreaterPaths.Count);
            Assert.AreEqual(2, empPriv.GreaterDims[1].GreaterSet.GreaterDims.Count);
        }

        [TestMethod]
        public void OledbDataImportTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Root");

            dbRoot.ConnectionString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test\\Northwind.accdb";
            // Another provider: "Provider=Microsoft.Jet.OLEDB.4.0;"

            dbRoot.Open();

            dbRoot.ImportSchema();

            Set empSet = dbRoot.FindSubset("Employees");
            System.Data.DataTable dataTable = dbRoot.Export(empSet);

            // Create workspace root set
            SetRoot wsRoot = new SetRoot("My Mashup");

            // Add a new set and import its structure
            Set emp = new Set("Emp", dbRoot.FindSubset("Employees"));
            emp.SuperDim = new DimRoot("super", emp, wsRoot); // Insert the set (no dimensions)
            emp.ImportDimensions(); // Import all dimensions

            // Import data
            emp.Populate();

            Assert.AreEqual(9, emp.Length);
            Assert.AreEqual(6, emp.GetValue("ID", 5));
            Assert.AreEqual("Mariya", emp.GetValue("First Name", 3));
            Assert.AreEqual("Seattle", emp.GetValue("City", 8));

            // Add EmployeePrivileges table which references one existing and one non-existing tables
            Set empPriv = new Set("EmpPriv", dbRoot.FindSubset("Employee Privileges"));
            empPriv.SuperDim = new DimRoot("super", empPriv, wsRoot); // Insert the set (no dimensions)
            empPriv.ImportDimensions(); // Import all dimensions

//            empPriv.Populate();

        }

        [TestMethod]
        public void TwoTableLoadTest()
        {
            // Table Employees has one column (say, dept_id - column number 5) referencing rows from table Departments (say, by using column dept_id - column number 0)
            // Define two sets where one references the other. Define load scenario so that we load rows along with references.
            // The idea is that the original function E -> dept_id (int) is translated into the target two functions E -> dept_id (D) -> dept_id (int). So we get one intermediate collection. 

            //- Use case scenario:
            //  - Load a flat table
            //  - Define (as a load option of later change) that some attributes are actually a separate or virtual complex type
            //  - Define (as a load option of later change) that this complex type is actually hierarchical (inclusion rather than tuple)
            //  - For some attribute considered a domain get all _instances (this attribute is not represented explicitly as a domain)
            //  - Create a new set of _instances for some attributes either manually or by imposing constraints on its original domain
            //  - Deproject this set and return a new set of _instances from the table
            //  - Use this set of table rows and project it by returning a new subset of the target attribute _instances
        }
    }
}
