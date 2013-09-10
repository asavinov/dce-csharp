using System;
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
            root.AddSubset(t1);

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
            DimImport dimExp = new DimImport("import", wsRoot, dbRoot.FindSubset("Employees"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();
            dimExp.LesserSet.ImportDims.Add(dimExp);
            dimExp.GreaterSet.ExportDims.Add(dimExp);

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
            DimImport dimExp2 = new DimImport("import", wsRoot, dbRoot.FindSubset("Inventory Transactions")); // "Employee Privileges"
            dimExp2.BuildImportExpression();
            dimExp2.ImportDimensions();
            dimExp2.LesserSet.ImportDims.Add(dimExp2);
            dimExp2.GreaterSet.ExportDims.Add(dimExp2);

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
        public void SetDataOperationsTest()
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

            DimImport dimExp = new DimImport("import", wsRoot, dbRoot.FindSubset("Order Details"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();
            dimExp.LesserSet.ImportDims.Add(dimExp);
            dimExp.GreaterSet.ExportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            Set odet = wsRoot.FindSubset("Order Details");
            Set orders = wsRoot.FindSubset("Orders");
            Set cust = wsRoot.FindSubset("Customers");
            Set doubleSet = wsRoot.GetPrimitiveSubset("Double");
            Set intSet = wsRoot.GetPrimitiveSubset("Integer");
            Set strSet = wsRoot.GetPrimitiveSubset("String");

            Expression childExpr;

            //
            // Find elements
            //
            Expression orderExpr = new Expression("", Operation.TUPLE, orders);
            childExpr = new Expression("Order ID", Operation.PRIMITIVE, intSet);
            childExpr.Output = 35;
            orderExpr.AddOperand(childExpr);
            childExpr = new Expression("Tax Rate", Operation.PRIMITIVE, doubleSet);
            childExpr.Output = 55.5; // Will be ignored - only identities are used
            orderExpr.AddOperand(childExpr);

            object offset = orders.Find(orderExpr);
            Assert.AreEqual(5, offset);

            //
            // Append elements
            //
            orderExpr.GetOperand("Order ID").Output = 1000;
            orderExpr.GetOperand("Tax Rate").Output = 99.99;

            Expression custExpr = new Expression("Customer ID", Operation.TUPLE, cust);
            childExpr = new Expression("ID", Operation.PRIMITIVE, intSet);
            childExpr.Output = 2000;
            custExpr.AddOperand(childExpr);
            childExpr = new Expression("Last Name", Operation.PRIMITIVE, strSet);
            childExpr.Output = "Lennon";
            custExpr.AddOperand(childExpr);

            orderExpr.AddOperand(custExpr);

            offset = orders.Append(orderExpr);
            Assert.AreEqual(40, offset);
            Assert.AreEqual(1000, orders.GetValue("Order ID", (int)offset));
            Assert.AreEqual(99.99, orders.GetValue("Tax Rate", (int)offset));
            Assert.AreEqual(2000, cust.GetValue("ID", 15));
            Assert.AreEqual("Lennon", cust.GetValue("Last Name", 15));

            offset = orders.Find(orderExpr);
            Assert.AreEqual(orders.Length-1, offset);

            //
            // Create a new set as a product and populate it
            //
            Set ods = wsRoot.FindSubset("Order Details Status"); // 4 elements loaded
            Set os = wsRoot.FindSubset("Orders Status"); // 3 elements loaded

            Set newSet = new Set("New Set");
            wsRoot.AddSubset(newSet);

            Dim d1 = ods.CreateDefaultLesserDimension("Order Details Status", newSet);
            d1.IsIdentity = true;
            Dim d2 = os.CreateDefaultLesserDimension("Orders Status", newSet);
            d2.IsIdentity = true;

            newSet.AddGreaterDim(d1);
            newSet.AddGreaterDim(d2);

            Expression whereExpr = new Expression("EQUAL", Operation.EQUAL);

            Expression d1_Expr = Expression.CreateProjectExpression(newSet, new List<Dim> { d1, ods.GetGreaterDim("Status ID") }, Operation.DOT);
            Expression d2_Expr = Expression.CreateProjectExpression(newSet, new List<Dim> { d2, os.GetGreaterDim("Status ID") }, Operation.DOT);

            whereExpr.Input = d1_Expr;
            whereExpr.AddOperand(d2_Expr);

            newSet.WhereExpression = whereExpr;

            newSet.Populate();
            Assert.AreEqual(2, newSet.Length);
            Assert.AreEqual(0, newSet.GetValue("Order Details Status", 0));
            Assert.AreEqual(2, newSet.GetValue("Orders Status", 0));
            Assert.AreEqual(3, newSet.GetValue("Order Details Status", 1));
            Assert.AreEqual(1, newSet.GetValue("Orders Status", 1));
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

            DimImport dimExp = new DimImport("import", wsRoot, dbRoot.FindSubset("Order Details"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();
            dimExp.LesserSet.ImportDims.Add(dimExp);
            dimExp.GreaterSet.ExportDims.Add(dimExp);

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

            Expression expr = Expression.CreateProjectExpression(od, path, Operation.PROJECTION);

            // Add derived dimension
            Dim derived1 = d3.GreaterSet.CreateDefaultLesserDimension("Customer Last Name", od);
            derived1.SelectExpression = expr;
            od.AddGreaterDim(derived1);

            // Update
            derived1.Populate(); // Call SelectExpression.Evaluate(EvaluationMode.UPDATE);

            Assert.AreEqual("Axen", od.GetValue("Customer Last Name", 10));
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

            DimImport dimExp = new DimImport("import", wsRoot, dbRoot.FindSubset("Order Details"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();
            dimExp.LesserSet.ImportDims.Add(dimExp);
            dimExp.GreaterSet.ExportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            //
            // Create derived dimensions
            //
            Set odet = wsRoot.FindSubset("Order Details");
            Set orders = wsRoot.FindSubset("Orders");
            Set cust = wsRoot.FindSubset("Customers");
            Set doubleSet = wsRoot.GetPrimitiveSubset("Double");

            // Create deproject (grouping) expression: (Customers) <- (Orders) <- (Order Details)
            Dim d1 = odet.GetGreaterDim("Order ID");
            Dim d2 = d1.GreaterSet.GetGreaterDim("Customer ID");
            List<Dim> path = new List<Dim> { d1, d2 };

            Expression deprExpr = Expression.CreateDeprojectExpression(odet, path);

            // Create project (measure) expression: (Order Details) -> (Product) -> List Price
            Dim d3 = odet.GetGreaterDim("Product ID");
            Dim d4 = d3.GreaterSet.GetGreaterDim("List Price");
            List<Dim> mesPath = new List<Dim> { d3, d4 };

            Expression projExpr = Expression.CreateProjectExpression(odet, mesPath, Operation.DOT);

            // Add derived dimension
            Expression aggreExpr = Expression.CreateAggregateExpression("SUM", deprExpr, projExpr);
            Dim derived1 = doubleSet.CreateDefaultLesserDimension("Average List Price", cust);
            derived1.SelectExpression = aggreExpr;
            cust.AddGreaterDim(derived1);

            // Update
            derived1.Populate(); // Call SelectExpression.Evaluate(EvaluationMode.UPDATE);

            Assert.AreEqual(64.0, cust.GetValue("Average List Price", 2));
        }

        [TestMethod]
        public void RecommendAggregationTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Root");

            dbRoot.ConnectionString = Northwind;

            dbRoot.Open();

            dbRoot.ImportSchema();

            //
            // Load test data
            //
            SetRoot wsRoot = new SetRoot("My Mashup");

            DimImport dimExp = new DimImport("import", wsRoot, dbRoot.FindSubset("Order Details"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();
            dimExp.LesserSet.ImportDims.Add(dimExp);
            dimExp.GreaterSet.ExportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            Set cust = wsRoot.FindSubset("Customers");
            Set prod = wsRoot.FindSubset("Products");
            Set doubleSet = wsRoot.GetPrimitiveSubset("Double");

            //
            // Test aggregation recommendations. From Customers to Product
            // Grouping (deproject) expression: (Customers) <- (Orders) <- (Order Details)
            // Measure (project) expr+ession: (Order Details) -> (Product) -> List Price
            //
            RecommendedAggregations recoms = new RecommendedAggregations();
            recoms.SourceSet = cust;
            recoms.TargetSet = prod;
            recoms.FactSet = null; // Any

            recoms.Recommend();

            recoms.SelectedGroupingPath = recoms.GroupingPaths[0];
            recoms.SelectedFactSet = recoms.FactSets[0];
            recoms.SelectedMeasurePath = recoms.MeasurePaths[0];

            recoms.SelectedMeasureDimension = recoms.MeasureDimensions.First(f => ((Dim)f.Fragment).Name == "List Price");
            recoms.SelectedAggregationFunction = recoms.AggregationFunctions.First(f => f.Fragment == "SUM");

            Expression aggreExpr = recoms.GetExpression();
            Dim derived1 = doubleSet.CreateDefaultLesserDimension("Average List Price", cust);
            derived1.SelectExpression = aggreExpr;
            cust.AddGreaterDim(derived1);

            // Update
            derived1.Populate(); // Call SelectExpression.Evaluate(EvaluationMode.UPDATE);

            Assert.AreEqual(64.0, cust.GetValue("Average List Price", 2));

        }

        [TestMethod]
        public void ArithmeticTest()
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

            DimImport dimExp = new DimImport("import", wsRoot, dbRoot.FindSubset("Order Details"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();
            dimExp.LesserSet.ImportDims.Add(dimExp);
            dimExp.GreaterSet.ExportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            //
            // Create derived dimensions
            //

            // "List Price", "Standard Cost", "Target Level"
            // Column names are function names and they have to be assign to expression node names (DOT)
            // But where is 'this' variable and 'this' set? This set is Input.OutputSet. And the function will be applied to whatever is stored in Input.Output (interpreted as offset).
            // So Input.Output has to be assigned explicitly offset in a loop. Or we need to store a variable 'this' which, when evaluated, writes its current value to Input.Output.


            Set products = wsRoot.FindSubset("Products");
            Set doubleSet = wsRoot.GetPrimitiveSubset("Double");

            // Create simple (one-segment) function expressions
            Dim d1 = products.GetGreaterDim("List Price");
            Dim d2 = products.GetGreaterDim("Standard Cost");
            Dim d3 = products.GetGreaterDim("Target Level");

            Expression d1_Expr = Expression.CreateProjectExpression(products, new List<Dim> { d1 }, Operation.DOT);
            Expression d2_Expr = Expression.CreateProjectExpression(products, new List<Dim> { d2 }, Operation.DOT);
            Expression d3_Expr = Expression.CreateProjectExpression(products, new List<Dim> { d3 }, Operation.DOT);

            Expression arithmExpr = new Expression("MINUS", Operation.MINUS);
            arithmExpr.Input = d1_Expr;

            Expression plusExpr = new Expression("PLUS", Operation.PLUS);
            plusExpr.Input = d2_Expr;
            plusExpr.AddOperand(d3_Expr);

            arithmExpr.AddOperand(plusExpr);

            // Add derived dimension
            Dim derived1 = doubleSet.CreateDefaultLesserDimension("Derived Column", products);
            derived1.SelectExpression = arithmExpr;
            products.AddGreaterDim(derived1);

            // Update
            derived1.Populate(); // Call SelectExpression.Evaluate(EvaluationMode.UPDATE);
            Assert.AreEqual(-32.5, products.GetValue("Derived Column", 2));

            // 
            // Another (simpler) test
            //
            plusExpr = new Expression("PLUS", Operation.PLUS);
            plusExpr.Input = d1_Expr;
            plusExpr.AddOperand(d1_Expr);

            // Add derived dimension
            Dim derived2 = doubleSet.CreateDefaultLesserDimension("Derived Column 2", products);
            derived2.SelectExpression = plusExpr;
            products.AddGreaterDim(derived2);

            // Update
            derived2.Populate(); // Call SelectExpression.Evaluate(EvaluationMode.UPDATE);
            Assert.AreEqual(60.0, products.GetValue("Derived Column 2", 2));
        }

        [TestMethod]
        public void SubsettingTest()
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

            DimImport dimExp = new DimImport("import", wsRoot, dbRoot.FindSubset("Order Details"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();
            dimExp.LesserSet.ImportDims.Add(dimExp);
            dimExp.GreaterSet.ExportDims.Add(dimExp);

            // Import data
            dimExp.Populate();

            //
            // Create logical expression
            //
            Set products = wsRoot.FindSubset("Products");

            // Create simple (one-segment) function expressions
            Dim d1 = products.GetGreaterDim("List Price");
            Dim d2 = products.GetGreaterDim("Standard Cost");
            Dim d3 = products.GetGreaterDim("Target Level");

            Expression d1_Expr = Expression.CreateProjectExpression(products, new List<Dim> { d1 }, Operation.DOT);
            Expression d2_Expr = Expression.CreateProjectExpression(products, new List<Dim> { d2 }, Operation.DOT);
            Expression d3_Expr = Expression.CreateProjectExpression(products, new List<Dim> { d3 }, Operation.DOT);

            Expression logicalExpr = new Expression("LESS", Operation.LESS);

            logicalExpr.Input = d1_Expr;
            logicalExpr.AddOperand(d2_Expr);

            // Add subset
            Set subProducts = new Set("SubProducts");
            subProducts.WhereExpression = logicalExpr;

            products.AddSubset(subProducts);

            // Update
//            subProducts.Populate();
        }


    }
}
