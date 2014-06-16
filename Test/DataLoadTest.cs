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
        public static string TextDbConnection = "Provider=Microsoft.ACE.OLEDB.12.0; Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test; Extended Properties='Text;Excel 12.0;HDR=Yes;FMT=CSVDelimited;'";

        // Another provider: "Provider=Microsoft.Jet.OLEDB.4.0;"
        // Provider=Microsoft.Jet.OLEDB.4.0
        // Provider=Microsoft.ACE.OLEDB.12.0
        // CSVDelimited
        // Delimited(;)

        [TestMethod]
        public void PrimDimensinTest()
        {
            Dim dim = new DimPrimitive<int>("Test", null, null);

            //
            // Insert new data
            //
            dim.ColumnData.Append(10);
            dim.ColumnData.Append(30);
            dim.ColumnData.Append(20);
            Assert.AreEqual(3, dim.ColumnData.Length);
            Assert.AreEqual(30, dim.ColumnData.GetValue(1));

            dim.ColumnData.SetValue(2, 50);
            dim.ColumnData.SetValue(1, 50);
            dim.ColumnData.SetValue(0, 50);
            Assert.AreEqual(50, dim.ColumnData.GetValue(1));

            dim.ColumnData.SetValue(1, 10);
            Assert.AreEqual(10, dim.ColumnData.GetValue(1));
            Assert.AreEqual(false, dim.ColumnData.IsNull(2));

            dim.ColumnData.SetValue(2, null);
            Assert.AreEqual(true, dim.ColumnData.IsNull(2));
            dim.ColumnData.SetValue(1, null);
            dim.ColumnData.SetValue(0, null);
            Assert.AreEqual(true, dim.ColumnData.IsNull(0));

            dim.ColumnData.SetValue(1, 100);
            Assert.AreEqual(100, dim.ColumnData.GetValue(1));
        }

        [TestMethod]
        public void TableLoadTest()
        {
            //
            // Prepare table schema
            //
            SetTop top = new SetTop("Top");
            Set setInteger = top.GetPrimitive("Integer");
            Set setDouble = top.GetPrimitive("Double");
            Set setString = top.GetPrimitive("String");

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

            Mapping mapping = mapper.GetBestMapping(sourceSet, wsTop);
            mapping.AddTargetToSchema(wsTop);
            Dim dimImport = new Dim(mapping); // Configure first set for import
            dimImport.Add();
            dimImport.GreaterSet.ProjectDimensions.Add(dimImport);

            Set targetSet = mapping.TargetSet;
            targetSet.TableDefinition.Populate();

            Assert.AreEqual(9, targetSet.Length);
            Assert.AreEqual(6, targetSet.GetValue("ID", 5));
            Assert.AreEqual("Mariya", targetSet.GetValue("First Name", 3));
            Assert.AreEqual("Seattle", targetSet.GetValue("City", 8));

            //
            // Import second set. Inventory Transactions
            //
            Set sourceSet2 = dbTop.FindSubset("Inventory Transactions");
            mapper.MapSet(sourceSet2, wsTop);

            Mapping mapping2 = mapper.GetBestMapping(sourceSet2, wsTop);
            mapping2.AddTargetToSchema(wsTop);
            Dim dimImport2 = new Dim(mapping2); // Configure first set for import
            dimImport2.Add();
            dimImport2.GreaterSet.ProjectDimensions.Add(dimImport2);

            Set targetSet2 = mapping2.TargetSet;
            targetSet2.TableDefinition.Populate();

            Assert.AreEqual(102, targetSet2.Length);
            Assert.AreEqual(1, targetSet2.GetValue("Transaction Type", 99)); // 1 is offset which should correspond to second record "Sold"

            Set pro = wsTop.FindSubset("Products");
            Assert.AreEqual(28, pro.Length);
            Assert.AreEqual(34.8m, pro.GetValue("List Price", 1));
        }

        [TestMethod]
        public void TextDataImportTest()
        {
            // Create Oldedb top set
            SetTopText dbTop = new SetTopText("Products");
            dbTop.ConnectionString = TextDbConnection;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Import first set. Employees
            //
            Mapper mapper = new Mapper();
            mapper.SetCreationThreshold = 1.0;

            Set sourceSet = dbTop.FindSubset("Products#csv");
            mapper.MapSet(sourceSet, wsTop);

            Mapping mapping = mapper.GetBestMapping(sourceSet, wsTop);
            mapping.AddTargetToSchema(wsTop);
            Dim dimImport = new Dim(mapping); // Configure first set for import
            dimImport.Add();
            dimImport.GreaterSet.ProjectDimensions.Add(dimImport);

            Set targetSet = mapping.TargetSet;
            targetSet.TableDefinition.Populate();

            Assert.AreEqual(45, targetSet.Length);
            Assert.AreEqual(7, targetSet.GetValue("ID", 5));
            Assert.AreEqual("Northwind Traders Olive Oil", targetSet.GetValue("Product Name", 3));
            Assert.AreEqual(40, targetSet.GetValue("Target Level", 8));
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
            targetSet.TableDefinition.Populate();

            Set odet = wsTop.FindSubset("Order Details");
            Set orders = wsTop.FindSubset("Orders");
            Set cust = wsTop.FindSubset("Customers");
            Set doubleSet = wsTop.GetPrimitive("Double");
            Set intSet = wsTop.GetPrimitive("Integer");
            Set strSet = wsTop.GetPrimitive("String");

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

            Dim d1 = wsTop.CreateColumn("Order Details Status", newSet, ods, true);
            d1.Add();

            Dim d2 = wsTop.CreateColumn("Orders Status", newSet, os, true);
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

            newSet.TableDefinition.WhereExpression = whereExpr;

            // Populate and test
            newSet.TableDefinition.Populate();
            Assert.AreEqual(2, newSet.Length);
            Assert.AreEqual(0, newSet.GetValue("Order Details Status", 0));
            Assert.AreEqual(2, newSet.GetValue("Orders Status", 0));
            Assert.AreEqual(3, newSet.GetValue("Order Details Status", 1));
            Assert.AreEqual(1, newSet.GetValue("Orders Status", 1));

            //
            // Subsetting operation (product with super-dimension)
            //
            Set subset_ods = new Set("Subset of ODS");
            wsTop.AddTable(subset_ods); // TODO: Check that super-projDim is identity

            d2 = wsTop.CreateColumn("Orders Status", subset_ods, os, true);
            d2.Add();

            // Define filter

            // Populate and test
            subset_ods.TableDefinition.Populate();
            Assert.AreEqual(12, subset_ods.Length);

            // Define filter
            whereExpr = new Expression("EQUAL", Operation.EQ);

            d1_Expr = Expression.CreateProjectExpression(new List<Dim> { subset_ods.SuperDim, ods.GetGreaterDim("Status ID") }, Operation.DOT);
            d2_Expr = Expression.CreateProjectExpression(new List<Dim> { d2, os.GetGreaterDim("Status ID") }, Operation.DOT);

            d1_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, subset_ods);
            d2_Expr.GetInputLeaf().Input = new Expression("this", Operation.DOT, subset_ods);

            whereExpr.Input = d1_Expr;
            whereExpr.AddOperand(d2_Expr);

            subset_ods.TableDefinition.WhereExpression = whereExpr;

            subset_ods.TableDefinition.Unpopulate();
            subset_ods.TableDefinition.Populate();
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
            targetSet.TableDefinition.Populate();
            
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
            derived1.Evaluate();

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
            targetSet.TableDefinition.Populate();

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
            derived1.Evaluate();

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
            targetSet.TableDefinition.Populate();

            Set cust = wsTop.FindSubset("Customers");
            Set prod = wsTop.FindSubset("Products");
            Set doubleSet = wsTop.GetPrimitive("Double");

            //
            // Test aggregation recommendations. From Customers to Product
            // Grouping (deproject) expression: (Customers) <- (Orders) <- (Order Details)
            // Measure (project) tupleExpr+ession: (Order Details) -> (Product) -> List Price
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

            Dim derived1 = wsTop.CreateColumn("Average List Price", cust, doubleSet, true);
            derived1.Add();

            Expression aggreExpr = recoms.GetExpression();

            var funcExpr = ExpressionScope.CreateFunctionDeclaration("Average List Price", cust.Name, doubleSet.Name);
            funcExpr.Statements[0].Input = aggreExpr; // Return statement
            funcExpr.ResolveFunction(wsTop);
            funcExpr.Resolve();

            derived1.SelectExpression = funcExpr;

            // Update
            derived1.Evaluate();

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
            targetSet.TableDefinition.Populate();

            Set products = wsTop.FindSubset("Products");
            Set doubleSet = wsTop.GetPrimitive("Double");

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
            Dim derived1 = wsTop.CreateColumn("Derived Column", products, doubleSet, true);
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
            derived1.Evaluate();
            Assert.AreEqual(-32.5, products.GetValue("Derived Column", 2));

            // 
            // Another (simpler) test
            //
            // Add derived dimension
            Dim derived2 = wsTop.CreateColumn("Derived Column 2", products, doubleSet, true);
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
            derived2.Evaluate();
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
            targetSet.TableDefinition.Populate();
            
            //
            // Create logical expression
            //
            Set products = wsTop.FindSubset("Products");

            // Add subset
            Set subProducts = new Set("SubProducts");
            wsTop.AddTable(subProducts, products);

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
            subProducts.TableDefinition.WhereExpression = logicalExpr;
            subProducts.TableDefinition.Populate();
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
            Set orderStatus = Mapper.ImportSet(dbTop.FindSubset("Orders Status"), wsTop); // We load it separately to get more (target) data
            orderStatus.TableDefinition.Populate();

            Set mainSet = Mapper.ImportSet(dbTop.FindSubset("Order Details"), wsTop);
            mainSet.TableDefinition.Populate();

            Dim sourceDim = mainSet.GetGreaterDim("Status ID");
            Set sourceDimType = wsTop.FindSubset("Order Details Status");

            //
            // Define a new derived (mapped) dimension: (Order Details) -> newDim -> (Orders Status)
            // This new projDim is supposed to clone existing projDim: (Order Details) -> existingDim -> (Order Details Status)
            // Thus we essentially implement "Change Type" pattern
            //
            Set mappedDimType = wsTop.FindSubset("Orders Status");
            Dim mappedDim = wsTop.CreateColumn(sourceDim.Name + " (1)", mainSet, mappedDimType, true); // TODO: set also other properties so that new projDim is identical to the old one
            mappedDim.Add();

            // Manually define a mapping: Source: (Orders Details) -> Status ID -> Status ID. Target: (Orders Status) -> Status ID.
            Mapping mapping = new Mapping(mainSet, mappedDimType);
            mapping.AddMatch(new PathMatch( // Add one match between two paths
                new DimPath(new List<Dim> { sourceDim, sourceDimType.GetGreaterDim("Status ID") }),
                new DimPath(mappedDimType.GetGreaterDim("Status ID")))
                );

            mappedDim.Mapping = mapping;

            // Populate new dimension
            mappedDim.Evaluate(); // Evaluate tuple expression on the same set (not remove set), that is, move data from one dimension to the new dimension

            Assert.AreEqual(2, mappedDim.GetValue(14));
            Assert.AreEqual(1, mappedDim.GetValue(15));

            //
            // Define a new derived (mapped) dimension: (Orders) -> newDim -> (Suppliers)
            // This new projDim is supposed to clone existing projDim: (Orders) -> existingDim -> (Employees)
            // Thus we essentially implement "Change Type" pattern
            //
            mainSet = wsTop.FindSubset("Orders");

            sourceDimType = wsTop.FindSubset("Employees");
            sourceDim = mainSet.GetGreaterDim("Employee ID");

            //
            // Define a new derived (mapped) dimensions
            //
            mappedDimType = Mapper.ImportSet(dbTop.FindSubset("Suppliers"), wsTop);
            mappedDimType.TableDefinition.Populate();

            mappedDim = wsTop.CreateColumn(sourceDim.Name + " (1)", mainSet, mappedDimType, true); // TODO: set also other properties so that new projDim is identical to the old one
            mappedDim.Add();

            // Manually define a mapping
            mapping = new Mapping(mainSet, mappedDimType);
            mapping.AddMatch(new PathMatch( // Add one match between two paths
                new DimPath(new List<Dim> { sourceDim, sourceDimType.GetGreaterDim("ID") }),
                new DimPath(mappedDimType.GetGreaterDim("ID")))
                );

            mappedDim.Mapping = mapping;

            // Populate new dimension
            mappedDim.Evaluate(); // Evaluate tuple expression on the same set (not remove set), that is, move data from one dimension to the new dimension

            Assert.AreEqual(8, mappedDim.GetValue(0));
            Assert.AreEqual(2, mappedDim.GetValue(1));
            Assert.AreEqual(3, mappedDim.GetValue(2));
            Assert.AreEqual(5, mappedDim.GetValue(3));
        }

        [TestMethod]
        public void SetExtractionTest()
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
            targetSet.TableDefinition.Populate();

            Set products = wsTop.FindSubset("Products");
            Dim projDim = products.GetGreaterDim("Category"); // This is a parameter for the whole operation

            //
            // Create a set to be extracted
            //
            Set extractedSet = new Set("Categories");
            wsTop.Root.AddSubset(extractedSet);

            Set idSet = projDim.GreaterSet;
            Dim idDim = wsTop.CreateColumn(projDim.Name, extractedSet, idSet, true);
            idDim.Add();

            //
            // Configure the new extracted set population procedure (mapped dimension)
            //
            Mapping mapping = new Mapping(products, extractedSet);
            mapping.AddMatch(new PathMatch(new DimPath(projDim), new DimPath(idDim))); 

            Dim extractedDim = wsTop.CreateColumn(extractedSet.Name, products, extractedSet, true);
            extractedDim.Mapping = mapping;
            extractedDim.Add();
            extractedDim.GreaterSet.ProjectDimensions.Add(extractedDim);

            //
            // Populate the extracted set
            //
            extractedSet.TableDefinition.Populate();

            Assert.AreEqual("Dried Fruit & Nuts", extractedSet.GetValue("Category", 1));
            Assert.AreEqual("Pasta", extractedSet.GetValue("Category", 9));

            //
            // Populate the new dimension. It is equivalent to evaluating a normal (mapped) dimension because its greater set has been extracted.
            //
            extractedDim.Evaluate();

            Assert.AreEqual(1, extractedDim.GetValue(1));
            Assert.AreEqual(1, extractedDim.GetValue(19));
        }

    }
}
