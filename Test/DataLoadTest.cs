using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.model;

namespace Test
{
    [TestClass]
    public class DataLoadTest
    {
        [TestMethod]
        public void PrimDimensinTest()
        {
            Dimension dim = new DimPrimitive<int>("Test", null, null);

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
            Set setInteger = root.GetPrimitiveSet("Integer");
            Set setDouble = root.GetPrimitiveSet("Double");
            Set setString = root.GetPrimitiveSet("String");

            // Insert table
            Set t1 = new Set("t1");
            t1.SuperDim = new DimRoot("super", t1, root);

            Dimension orders = new DimPrimitive<int>("orders", t1, setInteger);
            t1.AddGreaterDimension(orders);

            Dimension revenue = new DimPrimitive<double>("revenue", t1, setDouble);
            t1.AddGreaterDimension(revenue);

            Dimension name = new DimPrimitive<string>("name", t1, setString);
            t1.AddGreaterDimension(name);

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
        public void TwoTableLoadTest()
        {
            // Table Employees has one column (say, dept_id - column number 5) referencing rows from table Departments (say, by using column dept_id - column number 0)
            // Define two sets where one references the other. Define load scenario so that we load rows along with references.
            // The idea is that the original function E -> dept_id (int) is translated into the target two functions E -> dept_id (D) -> dept_id (int). So we get one intermediate collection. 

            //- Use case scenario:
            //  - Load a flat table
            //  - Define (as a load option of later change) that some attributes are actually a separate or virtual complex type
            //  - Define (as a load option of later change) that this complex type is actually hierarchical (inclusion rather than tuple)
            //  - For some attribute considered a domain get all _instances (this attribute is not represented explicitly as a domain)
            //  - Create a new set of _instances for some attributes either manually or by imposing constraints on its original domain
            //  - Deproject this set and return a new set of _instances from the table
            //  - Use this set of table rows and project it by returning a new subset of the target attribute _instances
        }
    }
}
