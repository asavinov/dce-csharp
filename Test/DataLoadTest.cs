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
            Assert.AreEqual(30, dim.GetValue(1));

            dim.SetValue(2, 50);
            dim.SetValue(1, 50);
            dim.SetValue(0, 50);
            Assert.AreEqual(50, dim.GetValue(1));

            dim.SetValue(1, 10);
            Assert.AreEqual(10, dim.GetValue(1));
            Assert.AreEqual(false, dim.IsNull(2));

            dim.SetValue(2, null);
            Assert.AreEqual(true, dim.IsNull(2));
            dim.SetValue(1, null);
            dim.SetValue(0, null);
            Assert.AreEqual(true, dim.IsNull(0));

            dim.SetValue(1, 100);
            Assert.AreEqual(100, dim.GetValue(1));
        }

        [TestMethod]
        public void TableLoadTest()
        {
            //
            // Prepare table schema
            //
            SetTop top = new SetTop("Top");
            Set setInteger = top.GetPrimitiveSubset("Integer");
            Set setDouble = top.GetPrimitiveSubset("Double");
            Set setString = top.GetPrimitiveSubset("String");

            // Insert table
            Set t1 = new Set("t1");
            top.Root.AddSubset(t1);

            Dim orders = new DimPrimitive<int>("orders", t1, setInteger);
            orders.Add();

            Dim revenue = new DimPrimitive<double>("revenue", t1, setDouble);
            revenue.Add();

            Dim name = new DimPrimitive<string>("name", t1, setString);
            name.Add();

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
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Import first set. Employees
            //
            Mapper mapper = new Mapper();
            mapper.SetCreationThreshold = 1.0;

            Set sourceSet = dbTop.FindSubset("Employees");
            mapper.MapSet(sourceSet, wsTop);

            SetMapping bestMapping = mapper.GetBestMapping(sourceSet, wsTop);
            Set targetSet = bestMapping.TargetSet;
            DimImport dimImport = new DimImport(bestMapping); // Configure first set for import
            dimImport.Add();

            targetSet.Populate();
            Assert.AreEqual(9, targetSet.Length);
            Assert.AreEqual(6, targetSet.GetValue("ID", 5));
            Assert.AreEqual("Mariya", targetSet.GetValue("First Name", 3));
            Assert.AreEqual("Seattle", targetSet.GetValue("City", 8));

            //
            // Import second set. Inventory Transactions
            //
            Set sourceSet2 = dbTop.FindSubset("Inventory Transactions");
            mapper.MapSet(sourceSet2, wsTop);

            SetMapping bestMapping2 = mapper.GetBestMapping(sourceSet2, wsTop);
            Set targetSet2 = bestMapping2.TargetSet;
            DimImport dimImport2 = new DimImport(bestMapping2); // Configure first set for import
            dimImport2.Add();

            targetSet2.Populate();
            Assert.AreEqual(102, targetSet2.Length);
            Assert.AreEqual(1, targetSet2.GetValue("Transaction Type", 99)); // 1 is offset which should correspond to second record "Sold"

            Set pro = wsTop.FindSubset("Products");
            Assert.AreEqual(28, pro.Length);
            Assert.AreEqual(34.8m, pro.GetValue("List Price", 1));
        }

        [TestMethod]
        public void SetDataOperationsTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set targetSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            targetSet.Populate();

            Set odet = wsTop.FindSubset("Order Details");
            Set orders = wsTop.FindSubset("Orders");
            Set cust = wsTop.FindSubset("Customers");
            Set doubleSet = wsTop.GetPrimitiveSubset("Double");
            Set intSet = wsTop.GetPrimitiveSubset("Integer");
            Set strSet = wsTop.GetPrimitiveSubset("String");

            Expression childExpr;

            //
            // Find operation
            //
            Expression orderExpr = new Expression("", Operation.TUPLE, orders);
            childExpr = new Expression("Order ID", Operation.PRIMITIVE, intSet);
            childExpr.Output = 35;
            orderExpr.AddOperand(childExpr);
            childExpr = new Expression("Tax Rate", Operation.PRIMITIVE, doubleSet);
            childExpr.Output = 55.5; // Will be ignored - only identities are used
            orderExpr.AddOperand(childExpr);

            orders.Find(orderExpr);
            object offset = orderExpr.Output;
            Assert.AreEqual(5, offset);

            //
            // Append operation
            //
            orderExpr.SetOutput(Operation.ALL, null);
            orderExpr.GetOperands(Operation.ALL, "Order ID")[0].Output = 1000;
            orderExpr.GetOperands(Operation.ALL, "Tax Rate")[0].Output = 99.99;

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

            orders.Find(orderExpr);
            offset = orderExpr.Output;
            Assert.AreEqual(orders.Length-1, offset);

            //
            // Product operation and population of a product
            //
            Set ods = wsTop.FindSubset("Order Details Status"); // 4 elements loaded
            Set os = wsTop.FindSubset("Orders Status"); // 3 elements loaded

            Set newSet = new Set("New Set");

            Dim d1 = ods.CreateDefaultLesserDimension("Order Details Status", newSet);
            d1.IsIdentity = true;
            d1.Add();

            Dim d2 = os.CreateDefaultLesserDimension("Orders Status", newSet);
            d2.IsIdentity = true;
            d2.Add();

            wsTop.Root.AddSubset(newSet);

            // Define filter
            Expression whereExpr = new Expression("EQUAL", Operation.EQ);

            Expression d1_Expr = Expression.CreateProjectExpression(new List<Dim> { d1, ods.GetGreaterDim("Status ID") }, Operation.DOT);
            Expression d2_Expr = Expression.CreateProjectExpression(new List<Dim> { d2, os.GetGreaterDim("Status ID") }, Operation.DOT);

            d1_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, newSet);
            d2_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, newSet);

            whereExpr.Input = d1_Expr;
            whereExpr.AddOperand(d2_Expr);

            newSet.WhereExpression = whereExpr;

            // Populate and test
            newSet.Populate();
            Assert.AreEqual(2, newSet.Length);
            Assert.AreEqual(0, newSet.GetValue("Order Details Status", 0));
            Assert.AreEqual(2, newSet.GetValue("Orders Status", 0));
            Assert.AreEqual(3, newSet.GetValue("Order Details Status", 1));
            Assert.AreEqual(1, newSet.GetValue("Orders Status", 1));

            //
            // Subsetting operation (product with super-dimension)
            //
            Set subset_ods = new Set("Subset of ODS");

            d2 = os.CreateDefaultLesserDimension("Orders Status", subset_ods);
            d2.IsIdentity = true;

            ods.AddSubset(subset_ods); // TODO: Check that super-dim is identity
            d2.Add();

            // Define filter

            // Populate and test
            subset_ods.Populate();
            Assert.AreEqual(12, subset_ods.Length);

            // Define filter
            whereExpr = new Expression("EQUAL", Operation.EQ);

            d1_Expr = Expression.CreateProjectExpression(new List<Dim> { subset_ods.SuperDim, ods.GetGreaterDim("Status ID") }, Operation.DOT);
            d2_Expr = Expression.CreateProjectExpression(new List<Dim> { d2, os.GetGreaterDim("Status ID") }, Operation.DOT);

            d1_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, subset_ods);
            d2_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, subset_ods);

            whereExpr.Input = d1_Expr;
            whereExpr.AddOperand(d2_Expr);

            subset_ods.WhereExpression = whereExpr;

            subset_ods.Unpopulate();
            subset_ods.Populate();
            Assert.AreEqual(2, subset_ods.Length);
        }

        [TestMethod]
        public void ProjectionTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set targetSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            targetSet.Populate();
            
            //
            // Create derived dimensions
            //
            Set od = wsTop.FindSubset("Order Details");

            // Create expression
            Dim d1 = od.GetGreaterDim("Order ID");
            Dim d2 = d1.GreaterSet.GetGreaterDim("Customer ID");
            Dim d3 = d2.GreaterSet.GetGreaterDim("Last Name");
            List<Dim> path = new List<Dim> { d1, d2, d3 };

            Expression expr = Expression.CreateProjectExpression(path, Operation.PROJECTION);

            // Add derived dimension
            Dim derived1 = d3.GreaterSet.CreateDefaultLesserDimension("Customer Last Name", od);
            derived1.Add();

            var funcExpr = ExpressionScope.CreateFunctionDeclaration("Customer Last Name", "Order Details", "String");
            funcExpr.Statements[0].Input = expr; // Return statement
            funcExpr.ResolveFunction(wsTop);
            funcExpr.Resolve();

            derived1.SelectExpression = funcExpr;

            // Update
            derived1.ComputeValues();

            Assert.AreEqual("Axen", od.GetValue("Customer Last Name", 10));
        }

        [TestMethod]
        public void AggregationTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set targetSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            targetSet.Populate();

            //
            // Create derived dimensions
            //
            Set odet = wsTop.FindSubset("Order Details");
            Set orders = wsTop.FindSubset("Orders");
            Set cust = wsTop.FindSubset("Customers");

            // Create deproject (grouping) expression: (Customers) <- (Orders) <- (Order Details)
            Dim d1 = odet.GetGreaterDim("Order ID");
            Dim d2 = d1.GreaterSet.GetGreaterDim("Customer ID");
            List<Dim> path = new List<Dim> { d1, d2 };

            Expression deprExpr = Expression.CreateDeprojectExpression(path);

            // Create project (measure) expression: (Order Details) -> (Product) -> List Price
            Dim d3 = odet.GetGreaterDim("Product ID");
            Dim d4 = d3.GreaterSet.GetGreaterDim("List Price");
            List<Dim> mesPath = new List<Dim> { d3, d4 };

            Expression projExpr = Expression.CreateProjectExpression(mesPath, Operation.DOT);

            Expression aggreExpr = Expression.CreateAggregateExpression("SUM", deprExpr, projExpr);

            // Add derived dimension
            Dim derived1 = d4.GreaterSet.CreateDefaultLesserDimension("Average List Price", cust);
            derived1.Add();

            var funcExpr1 = ExpressionScope.CreateFunctionDeclaration("Average List Price", cust.Name, d4.GreaterSet.Name);
            funcExpr1.Statements[0].Input = aggreExpr; // Return statement
            funcExpr1.ResolveFunction(wsTop);
            funcExpr1.Resolve();

            derived1.SelectExpression = funcExpr1;

            // Update
            derived1.ComputeValues();

            Assert.AreEqual(64.0m, cust.GetValue("Average List Price", 2));
        }

        [TestMethod]
        public void RecommendAggregationTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Root");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set targetSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            targetSet.Populate();

            Set cust = wsTop.FindSubset("Customers");
            Set prod = wsTop.FindSubset("Products");
            Set doubleSet = wsTop.GetPrimitiveSubset("Double");

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

            recoms.GroupingPaths.SelectedFragment = recoms.GroupingPaths.Alternatives[0];
            recoms.FactSets.SelectedFragment = recoms.FactSets.Alternatives[0];
            recoms.MeasurePaths.SelectedFragment = recoms.MeasurePaths.Alternatives[0];

            recoms.MeasureDimensions.SelectedFragment = recoms.MeasureDimensions.Alternatives.First(f => ((Dim)f.Fragment).Name == "List Price");
            recoms.AggregationFunctions.SelectedFragment = recoms.AggregationFunctions.Alternatives.First(f => f.Fragment == "SUM");

            Dim derived1 = doubleSet.CreateDefaultLesserDimension("Average List Price", cust);
            derived1.Add();

            Expression aggreExpr = recoms.GetExpression();

            var funcExpr = ExpressionScope.CreateFunctionDeclaration("Average List Price", cust.Name, doubleSet.Name);
            funcExpr.Statements[0].Input = aggreExpr; // Return statement
            funcExpr.ResolveFunction(wsTop);
            funcExpr.Resolve();

            derived1.SelectExpression = funcExpr;

            // Update
            derived1.ComputeValues();

            Assert.AreEqual(64.0, cust.GetValue("Average List Price", 2));
        }

        [TestMethod]
        public void ArithmeticTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set targetSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            targetSet.Populate();

            Set products = wsTop.FindSubset("Products");
            Set doubleSet = wsTop.GetPrimitiveSubset("Double");

            //
            // Create derived dimensions
            //

            // "List Price", "Standard Cost", "Target Level"
            // Column names are function names and they have to be assign to expression node names (DOT)
            // But where is 'this' variable and 'this' set? This set is Input.OutputSet. And the function will be applied to whatever is stored in Input.Output (interpreted as offset).
            // So Input.Output has to be assigned explicitly offset in a loop. Or we need to store a variable 'this' which, when evaluated, writes its current value to Input.Output.

            // Create simple (one-segment) function expressions
            Dim d1 = products.GetGreaterDim("List Price");
            Dim d2 = products.GetGreaterDim("Standard Cost");
            Dim d3 = products.GetGreaterDim("Target Level");

            Expression d1_Expr = Expression.CreateProjectExpression(new List<Dim> { d1 }, Operation.DOT);
            Expression d2_Expr = Expression.CreateProjectExpression(new List<Dim> { d2 }, Operation.DOT);
            Expression d3_Expr = Expression.CreateProjectExpression(new List<Dim> { d3 }, Operation.DOT);

            // Add derived dimension
            Dim derived1 = doubleSet.CreateDefaultLesserDimension("Derived Column", products);
            derived1.Add();

            Expression arithmExpr = new Expression("MINUS", Operation.SUB);
            arithmExpr.Input = d1_Expr;

            Expression plusExpr = new Expression("PLUS", Operation.ADD);
            plusExpr.Input = d2_Expr;
            plusExpr.AddOperand(d3_Expr);

            arithmExpr.AddOperand(plusExpr);

            var funcExpr1 = ExpressionScope.CreateFunctionDeclaration("Derived Column", "Products", "Double");
            funcExpr1.Statements[0].Input = arithmExpr; // Return statement
            funcExpr1.ResolveFunction(wsTop);
            funcExpr1.Resolve();

            derived1.SelectExpression = funcExpr1;

            // Update
            derived1.ComputeValues();
            Assert.AreEqual(-32.5, products.GetValue("Derived Column", 2));

            // 
            // Another (simpler) test
            //
            // Add derived dimension
            Dim derived2 = doubleSet.CreateDefaultLesserDimension("Derived Column 2", products);
            derived2.Add();

            plusExpr = new Expression("PLUS", Operation.ADD);
            plusExpr.Input = d1_Expr;
            plusExpr.AddOperand(d1_Expr);

            var funcExpr2 = ExpressionScope.CreateFunctionDeclaration("Derived Column 2", "Products", "Double");
            funcExpr2.Statements[0].Input = plusExpr; // Return statement
            funcExpr2.ResolveFunction(wsTop);
            funcExpr2.Resolve();

            derived2.SelectExpression = funcExpr2; // plusExpr;

            // Update
            derived2.ComputeValues();
            Assert.AreEqual(60.0, products.GetValue("Derived Column 2", 2));
        }

        [TestMethod]
        public void SubsettingTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set targetSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            targetSet.Populate();
            
            //
            // Create logical expression
            //
            Set products = wsTop.FindSubset("Products");

            // Add subset
            Set subProducts = new Set("SubProducts");
            products.AddSubset(subProducts);

            // Create simple (one-segment) function expressions
            Dim d1 = products.GetGreaterDim("List Price");
            Dim d2 = products.GetGreaterDim("Standard Cost");
            Dim d3 = products.GetGreaterDim("Target Level");

            Expression d1_Expr = Expression.CreateProjectExpression(new List<Dim> { subProducts.SuperDim, d1 }, Operation.DOT);
            Expression d2_Expr = Expression.CreateProjectExpression(new List<Dim> { subProducts.SuperDim, d2 }, Operation.DOT);
            Expression d3_Expr = Expression.CreateProjectExpression(new List<Dim> { subProducts.SuperDim, d3 }, Operation.DOT);

            // Here values will be stored
            d1_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, subProducts);
            d2_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, subProducts);
            d3_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, subProducts);

            Expression logicalExpr = new Expression("GREATER", Operation.GRE);

            logicalExpr.Input = d1_Expr;
            logicalExpr.AddOperand(d3_Expr);

            // Update
            subProducts.WhereExpression = logicalExpr;
            subProducts.Populate();
            Assert.AreEqual(2, subProducts.Length);
        }

        [TestMethod]
        public void ChangeTypeTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data. 
            //
            Set orderStatus = Mapper.ImportSet(dbTop.FindSubset("Orders Status"), wsTop); // We load it to get more (target) data
            orderStatus.Populate();

            Set mainSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            mainSet.Populate();

            //
            // Define mapping (Orders Details) -> Status ID: From (Order Details Status) To (Orders Status)
            //
            Set sourceSet = wsTop.FindSubset("Order Details Status");
            Dim sourceDim = mainSet.GetGreaterDim("Status ID");
            Set targetSet = wsTop.FindSubset("Orders Status");
            Dim targetDim = targetSet.CreateDefaultLesserDimension(sourceDim.Name, mainSet); // TODO: set also other properties so that new dim is identical to the old one

            SetMapping mapping = new SetMapping(sourceSet, targetSet);
            mapping.AddMatch(new PathMatch( // Add two primitive paths each having one primitive dimension
                new DimPath(sourceSet.GetGreaterDim("Status ID")),
                new DimPath(targetSet.GetGreaterDim("Status ID")))
                );

            //
            // Populate new dimension and delete old one
            //
            Expression tupleExpr = mapping.GetTargetExpression(sourceDim, targetDim);

            var funcExpr = ExpressionScope.CreateFunctionDeclaration(targetDim.Name, targetDim.LesserSet.Name, targetDim.GreaterSet.Name);
            funcExpr.Statements[0].Input = tupleExpr; // Return statement
            funcExpr.ResolveFunction(wsTop);
            funcExpr.Resolve();

            targetDim.SelectExpression = funcExpr;

            targetDim.ComputeValues(); // Evaluate tuple expression on the same set (not remove set), that is, move data from one dimension to the new dimension

            targetDim.Replace(sourceDim); // Remove old dimension (detach) and attach new dimension (if not attached)

            Assert.AreEqual(2, targetDim.GetValue(14));
            Assert.AreEqual(1, targetDim.GetValue(15));

            //
            // Define mapping (Orders) -> Employee ID: From (Employees) To (Suppliers)
            //
            targetSet = Mapper.ImportSet(dbTop.FindSubset("Suppliers"), wsTop);
            targetSet.Populate();

            mainSet = wsTop.FindSubset("Orders");

            sourceSet = wsTop.FindSubset("Employees");
            sourceDim = mainSet.GetGreaterDim("Employee ID");
            targetSet = wsTop.FindSubset("Suppliers");
            targetDim = targetSet.CreateDefaultLesserDimension(sourceDim.Name, mainSet); // TODO: set also other properties so that new dim is identical to the old one

            mapping = new SetMapping(sourceSet, targetSet);
            mapping.AddMatch(new PathMatch( // Add two primitive paths each having one primitive dimension
                new DimPath(sourceSet.GetGreaterDim("ID")),
                new DimPath(targetSet.GetGreaterDim("ID")))
                );

            //
            // Populate new dimension and delete old one
            //
            tupleExpr = mapping.GetTargetExpression(sourceDim, targetDim);

            funcExpr = ExpressionScope.CreateFunctionDeclaration(targetDim.Name, targetDim.LesserSet.Name, targetDim.GreaterSet.Name);
            funcExpr.Statements[0].Input = tupleExpr; // Return statement
            funcExpr.ResolveFunction(wsTop);
            funcExpr.Resolve();

            targetDim.SelectExpression = funcExpr;

            targetDim.ComputeValues(); // Evaluate tuple expression on the same set (not remove set), that is, move data from one dimension to the new dimension

            targetDim.Replace(sourceDim); // Remove old dimension (detach) and attach new dimension (if not attached)

            Assert.AreEqual(8, targetDim.GetValue(0));
            Assert.AreEqual(2, targetDim.GetValue(1));
            Assert.AreEqual(3, targetDim.GetValue(2));
            Assert.AreEqual(5, targetDim.GetValue(3));
        }            

    }
}
