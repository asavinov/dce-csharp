﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.Model;

namespace Test
{
    [TestClass]
    public class DataLoadTest
    {
        public static string Northwind = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test\\Northwind.accdb";
        // Another provider: "Provider=Microsoft.Jet.OLEDB.4.0;"

        [TestMethod]
        public void PrimDimensinTest()
        {
            Dim dim = new DimPrimitive<int>("Test", null, null);

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

            Dim orders = new DimPrimitive<int>("orders", t1, setInteger);
            t1.AddGreaterDim(orders);

            Dim revenue = new DimPrimitive<double>("revenue", t1, setDouble);
            t1.AddGreaterDim(revenue);

            Dim name = new DimPrimitive<string>("name", t1, setString);
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

            dbRoot.ConnectionString = Northwind;

            dbRoot.Open();

            dbRoot.ImportSchema();

            // Check validity of the schema
            Set doubleSet = dbRoot.FindSubset("Double");
            Assert.AreEqual(16, doubleSet.LesserDims.Count);
            Assert.AreEqual(45, doubleSet.LesserPaths.Count);

            Set empSet = dbRoot.FindSubset("Employees");
            System.Data.DataTable dataTable = dbRoot.Export(empSet);
            Assert.AreEqual(9, dataTable.Rows.Count);
            Assert.AreEqual(18, dataTable.Columns.Count);

            Set epSet = dbRoot.FindSubset("Employee Privileges");
            Assert.AreEqual(2, epSet.GreaterDims.Count);
            Assert.AreEqual(20, epSet.GreaterPaths.Count); // 2 stored paths and 18 non-stored paths (inherited from Employees)
            Assert.AreEqual(2, epSet.GetGreaterPath("Employee ID").Rank);
            Assert.AreEqual(2, epSet.GetGreaterPath("Privilege ID").Rank);

            // Test enumerators
            int pathCount = 0;
            foreach (List<Dim> p in empSet.GetGreaterPrimitiveDims(DimensionType.IDENTITY_ENTITY))
            {
                Assert.AreEqual(1, p.Count);
                pathCount++;
            }
            Assert.AreEqual(18, pathCount);

            pathCount = 0;
            foreach (List<Dim> p in epSet.GetGreaterPrimitiveDims(DimensionType.IDENTITY_ENTITY))
            {
                pathCount++;
            }
            Assert.AreEqual(20, pathCount);

            // Test path correctness
            pathCount = epSet.GreaterPaths.Count;
            Assert.AreEqual(20, pathCount);
        }

        [TestMethod]
        public void OledbSchemaImportTest()
        {
            //
            // Create Oldedb root set
            //
            SetRootOledb dbRoot = new SetRootOledb("Northwind");
            dbRoot.ConnectionString = Northwind;
            dbRoot.Open();
            dbRoot.ImportSchema();

            //
            // Create workspace root set
            //
            SetRoot wsRoot = new SetRoot("My Mashup");

            // Add a new set manualy
            Set emp = new Set("Employees");
            emp.SuperDim = new DimRoot("super", emp, wsRoot); // Insert the set (no dimensions)

            //
            // Import an existing set
            //
            DimExport dimExp = new DimExport("export emp", dbRoot.FindSubset("Employees"), emp);
            dimExp.BuildExpression();
            dimExp.ExportDimensions();

            // Assert. Check imported dimensions
            Assert.AreEqual(18, emp.GreaterDims.Count); // The existing set had to get all dimensions

            //
            // Import second non-existing set
            //
            DimExport dimExp2 = new DimExport("export emp priv", dbRoot.FindSubset("Employee Privileges"), wsRoot);
            dimExp2.BuildExpression();
            dimExp2.ExportDimensions();

            // Assert. Check imported dimensions
            Set ep = wsRoot.FindSubset("Employee Privileges"); // This set had to be created
            Assert.AreEqual(2, ep.GreaterDims.Count);

            Set privs = wsRoot.FindSubset("Privileges");
            Assert.AreEqual(2, privs.GreaterDims.Count); // This set had to be created
        }

        [TestMethod]
        public void OledbDataImportTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Northwind");
            dbRoot.ConnectionString = Northwind;
            dbRoot.Open();
            dbRoot.ImportSchema();

            Set emp = dbRoot.FindSubset("Inventory Transactions"); // "Purchase Order Details" "Employee Privileges"
            string sql = dbRoot.BuildSql(emp);

            // Create workspace root set
            SetRoot wsRoot = new SetRoot("My Mashup");

            //
            // Import first set
            //
            DimExport dimExp = new DimExport("export emp", dbRoot.FindSubset("Employees"), wsRoot);
            dimExp.BuildExpression();
            dimExp.ExportDimensions();
/*
            // Import data
            dimExp.Populate();

            // Assert. Check imported data
            Set emp = wsRoot.FindSubset("Employees");

            Assert.AreEqual(9, emp.Length);
            Assert.AreEqual(6, emp.GetValue("ID", 5));
            //Assert.AreEqual("Mariya", emp.GetValue("First Name", 3));
            //Assert.AreEqual("Seattle", emp.GetValue("City", 8));
*/
            //
            // Import second set
            //
            DimExport dimExp2 = new DimExport("export emp priv", dbRoot.FindSubset("Employee Privileges"), wsRoot);
            dimExp2.BuildExpression();
            dimExp2.ExportDimensions();

            // Import data
            dimExp2.Populate();

            // Assert. Check imported data
            Set ep = wsRoot.FindSubset("Employee Privileges");

            Assert.AreEqual(1, ep.Length);
            //Assert.AreEqual(2, ep.GetValue("Employee ID", 0));

            Set priv = wsRoot.FindSubset("Privileges");

            Assert.AreEqual(1, priv.Length);
            //Assert.AreEqual(2, ep.GetValue("Purchase ID", 0));
            //Assert.AreEqual("Purchase Approvals", ep.GetValue("Purchase Name", 0));
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
