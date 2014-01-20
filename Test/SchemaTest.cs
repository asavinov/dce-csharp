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
            SetTop top = new SetTop("Top");

            // Test top structure
            Assert.IsTrue("Double" == top.GetPrimitiveSubset("Double").Name);
            Assert.IsTrue("String" == top.GetPrimitiveSubset("String").Name);

            Set c1 = new Set("c1");
            top.Root.AddSubset(c1);

            Set c2 = new Set("c2");
            top.Root.AddSubset(c2);

            Set c11 = new Set("c11");
            c1.AddSubset(c11);

            Set c12 = new Set("c12");
            c1.AddSubset(c12);

            // Test quantities
            Assert.AreEqual(2, top.Root.SubSets.Count);
            Assert.AreEqual(2, c1.SubSets.Count);
            Assert.AreEqual(0, c2.SubSets.Count);

            // Test existence
            Assert.IsTrue(c1 == top.FindSubset("c1"));
            Assert.IsTrue(c2 == top.FindSubset("c2"));
            Assert.IsTrue(c11 == top.FindSubset("c11"));
            Assert.IsTrue(c12 == top.FindSubset("c12"));

            // TODO: Delete leaf and intermediate sets
        }

        [TestMethod]
        public void TableSchemaTest()
        {
            SetTop top = new SetTop("Top");
            Set setInteger = top.GetPrimitiveSubset("Integer");
            Set setDouble = top.GetPrimitiveSubset("Double");
            Set setString = top.GetPrimitiveSubset("String");

            // Insert table
            Set t1 = new Set("t1");
            top.Root.AddSubset(t1);

            // Insert attributes
            Dim orders = new DimPrimitive<int>("orders", t1, setInteger);
            orders.Add();

            Dim revenue = new DimPrimitive<double>("revenue", t1, setDouble);
            revenue.Add();

            Dim name = new DimPrimitive<string>("name", t1, setString);
            name.Add();

            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "orders"));
            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "revenue"));
            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "name"));

            Assert.AreEqual(3, t1.GreaterDims.Count);
            Assert.AreEqual(3, t1.GetGreaterSets().Count);
        }


        [TestMethod]
        public void OledbSchemaLoadTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Top");

            dbTop.ConnectionString = Northwind;

            dbTop.Open();

            dbTop.ImportSchema();

            // Check validity of the schema
            Set doubleSet = dbTop.FindSubset("Double");
            Assert.AreEqual(2, doubleSet.LesserDims.Count); // 16
            Assert.AreEqual(6, doubleSet.LesserPaths.Count); // 45

            Set empSet = dbTop.FindSubset("Employees");
            System.Data.DataTable dataTable = dbTop.Export(empSet);
            Assert.AreEqual(9, dataTable.Rows.Count);
            Assert.AreEqual(18, dataTable.Columns.Count);

            Set epSet = dbTop.FindSubset("Employee Privileges");
            Assert.AreEqual(2, epSet.GreaterDims.Count);
            Assert.AreEqual(20, epSet.GreaterPaths.Count); // 2 stored paths and 18 non-stored paths (inherited from Employees)
            Assert.AreEqual(2, epSet.GetGreaterPath("Employee ID").Rank);
            Assert.AreEqual(2, epSet.GetGreaterPath("Privilege ID").Rank);

            // Test enumerators
            int pathCount = 0;
            foreach (DimPath p in empSet.GetGreaterPrimitiveDims(DimensionType.IDENTITY_ENTITY))
            {
                Assert.AreEqual(1, p.Length);
                pathCount++;
            }
            Assert.AreEqual(18, pathCount);

            pathCount = 0;
            foreach (DimPath p in epSet.GetGreaterPrimitiveDims(DimensionType.IDENTITY_ENTITY))
            {
                pathCount++;
            }
            Assert.AreEqual(20, pathCount);

            // Test path correctness
            pathCount = epSet.GreaterPaths.Count;
            Assert.AreEqual(20, pathCount);

            // Test tree
            SubsetTree treeTop = new SubsetTree(null);
            SubsetTree treeRoot = new SubsetTree(dbTop.Root.SuperDim, treeTop);
            treeTop.ExpandTree();

            Assert.AreEqual(20, treeRoot.Count); // 20 tables in the root 
            Assert.AreEqual(20, treeRoot[8].Count); // Orders has 20 greater dimensions

            // Check schema notifications and tree updates
            Set t1 = new Set("New Set");
            dbTop.Root.AddSubset(t1);
            Assert.AreEqual(21, treeRoot.Count); // Now we have 1 more table in the root

            Set t2 = new Set("New Subset");
            dbTop.Root.FindSubset("Orders").AddSubset(t2);
            Assert.AreEqual(21, treeRoot[8].Count); // In addition to 20 attributes, it has one new subset

            Dim d1 = new DimPrimitive<double>("d1", t1, dbTop.GetPrimitiveSubset("Double"));
            d1.Add();
            Assert.AreEqual(1, treeRoot[20].Count); // 1 dimension in the new table
        }

        [TestMethod]
        public void OledbSchemaImportTest()
        {
            //
            // Create Oldedb top set
            //
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Import a set
            //
            Set targetSet = Mapper.ImportSet(dbTop.FindSubset("Employees"), wsTop);

            // Assert. Check imported dimensions
            Set emp = wsTop.FindSubset("Employees"); // This set had to be created
            Assert.AreEqual(18, emp.GreaterDims.Count); // The existing set had to get all dimensions

            //
            // Import second set which uses the first set
            //
            targetSet = Mapper.ImportSet(dbTop.FindSubset("Employee Privileges"), wsTop);

            // Assert. Check imported dimensions
            Set ep = wsTop.FindSubset("Employee Privileges"); // This set had to be created
            Assert.AreEqual(2, ep.GreaterDims.Count);

            Set privs = wsTop.FindSubset("Privileges");
            Assert.AreEqual(2, privs.GreaterDims.Count); // This set had to be created
        }

        [TestMethod]
        public void RecommendRelationshipsTest()
        {
            // Create Oldedb top set
            SetTopOledb dbTop = new SetTopOledb("Top");

            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            // Test path enumerators
            var pathsEnum = new PathEnumerator(new List<Set>(), new List<Set> { dbTop.FindSubset("Purchase Orders") }, true, DimensionType.IDENTITY_ENTITY);
            var paths = pathsEnum.ToList();
            Assert.AreEqual(2, paths.Count);

            pathsEnum = new PathEnumerator(new List<Set>(), new List<Set> { dbTop.FindSubset("Products") }, true, DimensionType.IDENTITY_ENTITY);
            paths = pathsEnum.ToList();
            Assert.AreEqual(3, paths.Count);

            pathsEnum = new PathEnumerator(new List<Set>(), new List<Set> { dbTop.FindSubset("Employees") }, true, DimensionType.IDENTITY_ENTITY);
            paths = pathsEnum.ToList();
            Assert.AreEqual(6, paths.Count);

            // 3 paths exist. POD -> PO -> E; POD -> IT -> PO -> E; POD -> O -> E
            pathsEnum = new PathEnumerator(new List<Set>() { dbTop.FindSubset("Purchase Order Details") }, new List<Set> { dbTop.FindSubset("Employees") }, false, DimensionType.IDENTITY_ENTITY);
            paths = new List<DimPath>(pathsEnum);
            Assert.AreEqual(3, paths.Count);

            pathsEnum = new PathEnumerator(new List<Set>() { dbTop.FindSubset("Purchase Order Details") }, new List<Set> { dbTop.FindSubset("Employees") }, true, DimensionType.IDENTITY_ENTITY);
            paths = new List<DimPath>(pathsEnum);
            Assert.AreEqual(3, paths.Count);

            // Test relationships recommendations. From Employees to Customers. 8
            RecommendedRelationships recoms = new RecommendedRelationships();
            recoms.SourceSet = dbTop.FindSubset("Employees");
            recoms.TargetSet = dbTop.FindSubset("Customers");
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
        public void MatchingOperationsTest()
        {
            // Create Oldedb top new set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set ordersSource = dbTop.FindSubset("Orders");

            //
            // Initialize a mapping model 
            //
            Mapper mapper = new Mapper();
            mapper.SetCreationThreshold = 1.0;
            mapper.MapSet(ordersSource, wsTop);
            SetMapping mapping = mapper.GetBestMapping(ordersSource, wsTop);

            MappingModel model = new MappingModel(mapping);

            MatchTreeNode priNode = (MatchTreeNode)model.SourceTree.Children[0].Children[5];
            Assert.AreEqual(true, priNode.IsMatched); // Is matched with some other path
            model.SourceTree.SelectedNode = priNode;

            MatchTreeNode secNode = (MatchTreeNode)model.TargetTree.Children[0].Children[5];
            Assert.AreEqual(true, secNode.IsMatched); // Is matched with the selected primary
            secNode = (MatchTreeNode)model.TargetTree.Children[0].Children[6];
            Assert.AreEqual(false, secNode.IsMatched); // Is NOT matched with the new selected primary

            bool aaa = secNode.CanMatch; // Both are primitive so can match
            secNode = (MatchTreeNode)model.TargetTree.Children[0].Children[0];
            bool bbb = secNode.CanMatch; // Non-primitive cannot be matched


            Set targetSet = mapping.TargetSet;
            DimImport dimImport = new DimImport(mapping); // Configure first set for import
            dimImport.Add();

            //
            // Use the output of the mapping model: create new suggested elements, create expression for importing a set, create expression for importing data
            //

            //
            // Find recommended mappings
            //
        }

        [TestMethod]
        public void MappingForTypeChangeTest()
        {
            // Create Oldedb top new set
            SetTopOledb dbTop = new SetTopOledb("Northwind");
            dbTop.ConnectionString = Northwind;
            dbTop.Open();
            dbTop.ImportSchema();

            SetTop wsTop = new SetTop("My Mashup");

            //
            // Load test data
            //
            Set mainSet = Mapper.ImportSet(dbTop.FindSubset("Orders"), wsTop);
            mainSet.Populate(); // Actually, we do not need it for mapping (yet, some mappings algorithms might use instances for better mapping)

            // Dimension "Employee ID" will change its type from "Employees" to "Customers"
            Dim sourceDim = mainSet.GetGreaterDim("Employee ID");
            Set sourceSet = sourceDim.GreaterSet;
            Set targetSet = wsTop.FindSubset("Customers");
            Dim targetDim = targetSet.CreateDefaultLesserDimension(sourceDim.Name, mainSet); // TODO: set also other properties so that new dim is identical to the old one

            //
            // Initialize a mapping model 
            //
            Mapper mapper = new Mapper();
            mapper.MaxMappingsToBuild = 100;

            // So each Employee instance referenced by this dimension has to be converted to some Customer instance (or null if not found)
            // Each source instance is identified by all primitive values and these values are used to compose a target tuple and find it.
            // Interestingly, it is not necessary to use identities - we use any properties provided that source values uniquely determine some target instance.
            mapper.MapDim(new DimPath(sourceDim), new DimPath(targetDim));

            SetMapping mapping = mapper.Mappings[0];
            DimPath sourcePath = new DimPath(sourceSet.GetGreaterDim("Last Name"));
            DimPath targetPath = mapping.GetMatchForSource(sourcePath).TargetPath;
            Assert.AreEqual(sourcePath.FirstSegment.Name, targetPath.FirstSegment.Name);

            sourcePath = new DimPath(sourceSet.GetGreaterDim("ID"));
            targetPath = mapping.GetMatchForSource(sourcePath).TargetPath;
            Assert.AreEqual(sourcePath.FirstSegment.Name, targetPath.FirstSegment.Name);


            // Shippers.ID.Integer -> Shippers.ID.Customers (instead of Integer we want to point to Customer and map to Customer ID)
            mapper.Mappings.Clear();

            mainSet = wsTop.FindSubset("Shippers");

            sourceDim = mainSet.GetGreaterDim("ID");
            sourceSet = sourceDim.GreaterSet;
            targetSet = wsTop.FindSubset("Customers");
            targetDim = targetSet.CreateDefaultLesserDimension(sourceDim.Name, mainSet); // TODO: set also other properties so that new dim is identical to the old one

            mapper.MapDim(new DimPath(sourceDim), new DimPath(targetDim));
            string sourceName = "ID";
            string targetName = mapper.Mappings[0].Matches[0].TargetPath.LastSegment.Name;
            Assert.AreEqual(sourceName, targetName);
        }
    }
}
