using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.model;

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
            Assert.IsTrue("double" == root.GetPrimitiveSet("double").Name);

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
            Set setDouble = root.GetPrimitiveSet("double");

            // Insert table
            Set t1 = new Set("t1");
            t1.SuperDim = new DimRoot("super", t1, setDouble);

            Dimension sales = new DimDouble("sales", t1, setDouble);
            t1.AddGreaterDimension(sales);

            Dimension revenue = new DimDouble("revenue", t1, setDouble);
            t1.AddGreaterDimension(revenue);

            Assert.AreEqual(1, t1.GreaterDimensions.Count(x => x.Name == "sales"));
            Assert.AreEqual(1, t1.GreaterDimensions.Count(x => x.Name == "revenue"));
            Assert.AreEqual(2, t1.GreaterDimensions.Count);

            Assert.AreEqual(2, t1.GetGreaterSets().Count);
            Assert.AreEqual(2, setDouble.GetLesserSets().Count);
        }

        [TestMethod]
        public void StarSchemaTest()
        {
        }
    }
}
