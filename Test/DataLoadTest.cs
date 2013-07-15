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

            // Create workspace root set
            SetRoot wsRoot = new SetRoot("My Mashup");

            //
            // Import first set
            //
            DimExport dimExp = new DimExport("export emp", dbRoot.FindSubset("Employees"), wsRoot);
            dimExp.BuildExpression();
            dimExp.ExportDimensions();
            dimExp.LesserSet.ExportDims.Add(dimExp);
            dimExp.GreaterSet.ImportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            // Assert. Check imported data
            Set emp = wsRoot.FindSubset("Employees");

            Assert.AreEqual(9, emp.Length);
            Assert.AreEqual(6, emp.GetValue("ID", 5));
            Assert.AreEqual("Mariya", emp.GetValue("First Name", 3));
            Assert.AreEqual("Seattle", emp.GetValue("City", 8));

            //
            // Import second set
            //
            DimExport dimExp2 = new DimExport("export emp priv", dbRoot.FindSubset("Inventory Transactions"), wsRoot); // "Employee Privileges"
            dimExp2.BuildExpression();
            dimExp2.ExportDimensions();
            dimExp2.LesserSet.ExportDims.Add(dimExp2);
            dimExp2.GreaterSet.ImportDims.Add(dimExp2);

            // Import data
            dimExp2.Populate();

            // Assert. Check imported data
            Set it = wsRoot.FindSubset("Inventory Transactions");

            Assert.AreEqual(102, it.Length);
            Assert.AreEqual(1, it.GetValue("Transaction Type", 99)); // 1 is offset which should correspond to second record "Sold"

            Set pro = wsRoot.FindSubset("Products");

            Assert.AreEqual(28, pro.Length);
            Assert.AreEqual(34.8, pro.GetValue("List Price", 1));
        }

        [TestMethod]
        public void ProjectionTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Northwind");
            dbRoot.ConnectionString = Northwind;
            dbRoot.Open();
            dbRoot.ImportSchema();

            //
            // Load test data
            //
            SetRoot wsRoot = new SetRoot("My Mashup");

            DimExport dimExp = new DimExport("export emp", dbRoot.FindSubset("Order Details"), wsRoot);
            dimExp.BuildExpression();
            dimExp.ExportDimensions();
            dimExp.LesserSet.ExportDims.Add(dimExp);
            dimExp.GreaterSet.ImportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            //
            // Create derived dimensions
            //
            Set od = wsRoot.FindSubset("Order Details");

            // Create expression
            Dim d1 = od.GetGreaterDim("Order ID");
            Dim d2 = d1.GreaterSet.GetGreaterDim("Customer ID");
            Dim d3 = d2.GreaterSet.GetGreaterDim("Last Name");
            List<Dim> path = new List<Dim> { d1, d2, d3 };

            Expression expr = Expression.CreateProjectExpression(od, path);

            // Add derived dimension
            Dim derived1 = d3.GreaterSet.CreateDefaultLesserDimension("Customer Last Name", od);
            derived1.SelectExpression = expr;
            od.AddGreaterDim(derived1);

            // Update
            derived1.Populate(); // Call SelectExpression.Evaluate(EvaluationMode.UPDATE);

            Assert.AreEqual("Axen", od.GetValue("Customer Last Name", 10));

            // Create expression: Customers <- Orders <- Order Details -> Product (List Price)
        }

        [TestMethod]
        public void AggregationTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Northwind");
            dbRoot.ConnectionString = Northwind;
            dbRoot.Open();
            dbRoot.ImportSchema();

            //
            // Load test data
            //
            SetRoot wsRoot = new SetRoot("My Mashup");

            DimExport dimExp = new DimExport("export emp", dbRoot.FindSubset("Order Details"), wsRoot);
            dimExp.BuildExpression();
            dimExp.ExportDimensions();
            dimExp.LesserSet.ExportDims.Add(dimExp);
            dimExp.GreaterSet.ImportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            //
            // Create derived dimensions
            //
            Set od = wsRoot.FindSubset("Order Details");
            Set cust = wsRoot.FindSubset("Customers");
            Set strSet = wsRoot.GetPrimitiveSubset("String");

            // Create deproject (grouping) expression: Customers <- Orders <- Order Details
            Dim d1 = od.GetGreaterDim("Order ID");
            Dim d2 = d1.GreaterSet.GetGreaterDim("Customer ID");
            List<Dim> path = new List<Dim> { d1, d2 };

            Expression deprExpr = Expression.CreateDeprojectExpression(od, path);

            // Create project (measure) expression: Order Details -> Product (List Price)
            Dim d3 = od.GetGreaterDim("Product ID");
            Dim d4 = d3.GreaterSet.GetGreaterDim("List Price");
            List<Dim> mesPath = new List<Dim> { d3, d4 };

            Expression projExpr = Expression.CreateDeprojectExpression(od, mesPath);

            // Add derived dimension
            Expression aggreExpr = Expression.CreateAggregateExpression("SUM", deprExpr, projExpr);
            Dim derived1 = strSet.CreateDefaultLesserDimension("Average List Price", cust);
            derived1.SelectExpression = aggreExpr;
            cust.AddGreaterDim(derived1);

            // Update
            derived1.Populate(); // Call SelectExpression.Evaluate(EvaluationMode.UPDATE);

            Assert.AreEqual(25.456, od.GetValue("Average List Price", 10));

        }

    }
}
