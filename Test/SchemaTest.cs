using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.model;

// http://msdn.microsoft.com/en-us/library/ms182517.aspx

namespace Test
{
    [TestClass]
    public class SchemaTest
    {
        [TestMethod]
        public void ConceptSchemaTest()
        {
            Set root = new Set("Root");

            Set c1 = new Set("c1");
            c1.SuperDim = new Dimension("super", c1, root);

            Set c2 = new Set("c2");
            c2.SuperDim = new Dimension("super", c2, root);

            Set c11 = new Set("c11");
            c11.SuperDim = new Dimension("super", c11, c1);

            Set c12 = new Set("c12");
            c12.SuperDim = new Dimension("super", c12, c1);

            // Test if the schema correct
            Assert.AreEqual(2, root.SubSetCount);
            Assert.AreEqual(2, c1.SubSetCount);
            Assert.AreEqual(0, c2.SubSetCount);

            // TODO: Delete leaf and intermediate element
            // TODO: Test if the schema correct
        }

        [TestMethod]
        public void TableSchemaTest()
        {
/*
            // Define table
            Set t = new Set(10, root);
            t.idDims.Add(new Dim(1, t, c1));
            t.enDims.Add(new Dim(2, t, c2));
            t.enDims.Add(new Dim(3, t, c3));


            // Define primitive domains: what are their dimensions?
            // Primitive domains store real values (double, string etc.) the only _id dimension.
            // Also complex domains can be parameterized to store real values rather than _offsets from other domains (so their greater domains are defined but do not store separate members - members are derived as projection from all lesser domains) 
            Set c1 = new Set(1, root);
            Set c2 = new Set(2, root);
            Set c3 = new Set(3, root);

            // Define table
            Set t = new Set(10, root);
            t.idDims.Add(new Dim(1, t, c1));
            t.enDims.Add(new Dim(2, t, c2));
            t.enDims.Add(new Dim(3, t, c3));
*/
        }

        [TestMethod]
        public void StarSchemaTest()
        {
        }
    }
}
