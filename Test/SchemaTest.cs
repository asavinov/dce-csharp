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

            Set c1 = new Set("c1");
            c1.SuperDim = new DimRoot("super", c1, root);

            Set c2 = new Set("c2");
            c2.SuperDim = new DimRoot("super", c2, root);

            Set c11 = new Set("c11");
            c11.SuperDim = new DimSuper("super", c11, c1);

            Set c12 = new Set("c12");
            c12.SuperDim = new DimSuper("super", c12, c1);

            // Test if the schema correct
            Assert.AreEqual(2, root.SubDims.Count(x => x.LesserSet.Instantiable));
            Assert.AreEqual(2, c1.SubSetCount);
            Assert.AreEqual(0, c2.SubSetCount);

            // TODO: Delete leaf and intermediate element

            // TODO: Test if the schema correct

        }

        [TestMethod]
        public void TableSchemaTest()
        {
            SetRoot root = new SetRoot("Root");
            Set setDouble = root.SubDims.First(x => x.LesserSet.Name == "double").LesserSet;

            Set t1 = new Set("t1");
            t1.SuperDim = new DimRoot("super", t1, setDouble);

            Dimension sales = new DimDouble("sales", t1, setDouble);
            t1.AddGreaterDimension(sales); // Alternative: t1.GreaterDimensions.Add(sales);

            Assert.AreEqual(1, t1.GreaterDimensions.Count(x => x.Name == "sales"));
            Assert.AreEqual(1, t1.GreaterDimensions.Count);
        }

        [TestMethod]
        public void StarSchemaTest()
        {
        }
    }
}
