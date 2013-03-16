using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.Model;

// http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class SchemaTest
    {
        [TestMethod]
        public void InclusionTest()
        {
            SetRoot root = new SetRoot("Root");

            // Test root structure
            Assert.IsTrue("Double" == root.GetPrimitiveSet("Double").Name);
            Assert.IsTrue("String" == root.GetPrimitiveSet("String").Name);

            Set c1 = new Set("c1");
            c1.SuperDim = new DimRoot("super", c1, root);

            Set c2 = new Set("c2");
            c2.SuperDim = new DimRoot("super", c2, root);

            Set c11 = new Set("c11");
            c11.SuperDim = new DimSuper("super", c11, c1);

            Set c12 = new Set("c12");
            c12.SuperDim = new DimSuper("super", c12, c1);

            // Test quantities
            Assert.AreEqual(2, root.NonPrimitiveSets.Count);
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
            Set setInteger = root.GetPrimitiveSet("Integer");
            Set setDouble = root.GetPrimitiveSet("Double");
            Set setString = root.GetPrimitiveSet("String");

            // Insert table
            Set t1 = new Set("t1");
            t1.SuperDim = new DimRoot("super", t1, root);

            // Insert attributes
            Dimension orders = new DimPrimitive<int>("orders", t1, setInteger);
            t1.AddGreaterDimension(orders);

            Dimension revenue = new DimPrimitive<double>("revenue", t1, setDouble);
            t1.AddGreaterDimension(revenue);

            Dimension name = new DimPrimitive<string>("name", t1, setString);
            t1.AddGreaterDimension(name);

            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "orders"));
            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "revenue"));
            Assert.AreEqual(1, t1.GreaterDims.Count(x => x.Name == "name"));

            Assert.AreEqual(3, t1.GreaterDims.Count);
            Assert.AreEqual(3, t1.GetGreaterSets().Count);
        }

        [TestMethod]
        public void StarSchemaTest()
        {
        }
    }
}
