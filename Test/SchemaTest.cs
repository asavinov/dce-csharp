using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.Model;

// Unit test: http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class SchemaTest
    {
        public static string Northwind = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\Users\\savinov\\git\\comcsharp\\Test\\Northwind.accdb";
        // Another provider: "Provider=Microsoft.Jet.OLEDB.4.0;"

        [TestMethod]
        public void InclusionTest()
        {
            SetRoot root = new SetRoot("Root");

            // Test root structure
            Assert.IsTrue("Double" == root.GetPrimitiveSubset("Double").Name);
            Assert.IsTrue("String" == root.GetPrimitiveSubset("String").Name);

            Set c1 = new Set("c1");
            root.AddSubset(c1);

            Set c2 = new Set("c2");
            root.AddSubset(c2);

            Set c11 = new Set("c11");
            c1.AddSubset(c11);

            Set c12 = new Set("c12");
            c1.AddSubset(c12);

            // Test quantities
            Assert.AreEqual(2, root.NonPrimitiveSubsets.Count);
            Assert.AreEqual(2, c1.SubSets.Count);
            Assert.AreEqual(0, c2.SubSets.Count);

            // Test existence
            Assert.IsTrue(c1 == root.FindSubset("c1"));
            Assert.IsTrue(c2 == root.FindSubset("c2"));
            Assert.IsTrue(c11 == root.FindSubset("c11"));
            Assert.IsTrue(c12 == root.FindSubset("c12"));

            // TODO: Delete leaf and intermediate element
        }

        [TestMethod]
        public void TableSchemaTest()
        {
            SetRoot root = new SetRoot("Root");
            Set setInteger = root.GetPrimitiveSubset("Integer");
            Set setDouble = root.GetPrimitiveSubset("Double");
            Set setString = root.GetPrimitiveSubset("String");

            // Insert table
            Set t1 = new Set("t1");
            root.AddSubset(t1);

            // Insert attributes
            Dim orders = new DimPrimitive<int>("orders", t1, setInteger);
            t1.AddGreaterDim(orders);

            Dim revenue = new DimPrimitive<double>("revenue", t1, setDouble);
            t1.AddGreaterDim(revenue);

            Dim name = new DimPrimitive<string>("name", t1, setString);
            t1.AddGreaterDim(name);

            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "orders"));
            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "revenue"));
            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "name"));

            Assert.AreEqual(3, t1.GreaterDims.Count);
            Assert.AreEqual(3, t1.GetGreaterSets().Count);
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
            wsRoot.AddSubset(emp);

            //
            // Import an existing set
            //
            DimImport dimExp = new DimImport("import", emp, dbRoot.FindSubset("Employees"));
            dimExp.BuildImportExpression();
            dimExp.ImportDimensions();

            // Assert. Check imported dimensions
            Assert.AreEqual(18, emp.GreaterDims.Count); // The existing set had to get all dimensions

            //
            // Import second non-existing set
            //
            DimImport dimExp2 = new DimImport("import", wsRoot, dbRoot.FindSubset("Employee Privileges"));
            dimExp2.BuildImportExpression();
            dimExp2.ImportDimensions();

            // Assert. Check imported dimensions
            Set ep = wsRoot.FindSubset("Employee Privileges"); // This set had to be created
            Assert.AreEqual(2, ep.GreaterDims.Count);

            Set privs = wsRoot.FindSubset("Privileges");
            Assert.AreEqual(2, privs.GreaterDims.Count); // This set had to be created
        }

        [TestMethod]
        public void RecommendRelationshipsTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Root");

            dbRoot.ConnectionString = Northwind;

            dbRoot.Open();

            dbRoot.ImportSchema();

            // Test path enumerators
            var pathsEnum = new PathEnumerator(new List<Set>(), new List<Set> { dbRoot.FindSubset("Purchase Orders") }, true, DimensionType.IDENTITY_ENTITY);
            var paths = pathsEnum.ToList();
            Assert.AreEqual(2, paths.Count);

            pathsEnum = new PathEnumerator(new List<Set>(), new List<Set> { dbRoot.FindSubset("Products") }, true, DimensionType.IDENTITY_ENTITY);
            paths = pathsEnum.ToList();
            Assert.AreEqual(3, paths.Count);

            pathsEnum = new PathEnumerator(new List<Set>(), new List<Set> { dbRoot.FindSubset("Employees") }, true, DimensionType.IDENTITY_ENTITY);
            paths = pathsEnum.ToList();
            Assert.AreEqual(6, paths.Count);

            // 3 paths exist. POD -> PO -> E; POD -> IT -> PO -> E; POD -> O -> E
            pathsEnum = new PathEnumerator(new List<Set>() { dbRoot.FindSubset("Purchase Order Details") }, new List<Set> { dbRoot.FindSubset("Employees") }, false, DimensionType.IDENTITY_ENTITY);
            paths = new List<List<Dim>>(pathsEnum);
            Assert.AreEqual(3, paths.Count);

            pathsEnum = new PathEnumerator(new List<Set>() { dbRoot.FindSubset("Purchase Order Details") }, new List<Set> { dbRoot.FindSubset("Employees") }, true, DimensionType.IDENTITY_ENTITY);
            paths = new List<List<Dim>>(pathsEnum);
            Assert.AreEqual(3, paths.Count);

            // Test relationships recommendations. From Employees to Customers. 8
            RecommendedRelationships recoms = new RecommendedRelationships();
            recoms.SourceSet = dbRoot.FindSubset("Employees");
            recoms.TargetSet = dbRoot.FindSubset("Customers");
            recoms.FactSet = null; // Any

            recoms.Recommend();
            Assert.AreEqual(8, recoms.Recommendations.Alternatives.Count());

            // Selection methods
            Assert.AreEqual(null, recoms.GroupingPaths.SelectedFragment);
            recoms.GroupingPaths.SelectedFragment = recoms.GroupingPaths.Alternatives[6];
            Assert.AreEqual(recoms.GroupingPaths.Alternatives[6], recoms.GroupingPaths.SelectedFragment);
            recoms.GroupingPaths.SelectedFragment = null;
            Assert.AreEqual(null, recoms.GroupingPaths.SelectedFragment);

            // Grouping (deproject) expression: (Customers) <- (Orders) <- (Order Details)
            // Measure (project) expression: (Order Details) -> (Product) -> List Price
        }

        [TestMethod]
        public void RecommendMappingsTest()
        {
            // Create Oldedb root set
            SetRootOledb dbRoot = new SetRootOledb("Root");

            dbRoot.ConnectionString = Northwind;

            dbRoot.Open();

            dbRoot.ImportSchema();

            // Create a DimTree which will be used as a target
            Set orders = dbRoot.FindSubset("Orders");
            Set emps = dbRoot.FindSubset("Employees");
            
            DimTree targetTree = new DimTree(); // Root
            DimTree targetChild = new DimTree(orders);
            targetChild.ExpandTree(); // Build complete primitive tree (up to the primitive sets)
            targetTree.AddChild(targetChild); // Add some set we want to map to
            targetChild = new DimTree(emps);
            targetTree.AddChild(targetChild); // Add second set as a possible target
            targetChild.ExpandTree(); 

            // Create a MatchTree which will be used for matching
            Set odets = dbRoot.FindSubset("Order Details");
            Set cust = dbRoot.FindSubset("Customers");

            MatchTree sourceTree = new MatchTree(targetTree); // Root
            MatchTree sourceChild = new MatchTree(odets);
            sourceChild.ExpandTree(); // Build complete primitive tree (up to the primitive sets)
            sourceTree.AddChild(sourceChild);
            sourceChild = new MatchTree(cust);
            sourceTree.AddChild(sourceChild);
            sourceChild.ExpandTree(); // Build complete primitive tree (up to the primitive sets)

            // Check the generation of recommendations (alternatives)
            sourceTree.Recommend(); // For this and all child nodes

            // Check that selections (matchings) and the updates of recommendations (selection propagation) work correctly
            sourceChild = (MatchTree)sourceTree.Children[1]; // Customers
            Assert.AreEqual(7, sourceChild.Matches.Alternatives.Count);
            Assert.AreEqual(91, ((MatchTree)sourceChild.Children[0]).Matches.Alternatives.Count); // All attributes of all possible target sets

            sourceChild.Matches.SelectedObject = targetTree.Children[1]; // Select Employees
            Assert.AreEqual(targetTree.Children[1], sourceChild.Matches.SelectedObject);

            sourceChild.UpdateSelection();
            Assert.AreEqual(18, ((MatchTree)sourceChild.Children[0]).Matches.Alternatives.Count); // Attributes of the target Employees
        }

    }
}
