using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Com.model;

namespace Test
{
    [TestClass]
    public class DataLoadTest
    {
        [TestMethod]
        public void TableLoadTest()
        {
            //- Use case scenario:
            //  - Load a flat table
            //  - Define (as a load option of later change) that some attributes are actually a separate or virtual complex type
            //  - Define (as a load option of later change) that this complex type is actually hierarchical (inclusion rather than tuple)
            //  - For some attribute considered a domain get all _instances (this attribute is not represented explicitly as a domain)
            //  - Create a new set of _instances for some attributes either manually or by imposing constraints on its original domain
            //  - Deproject this set and return a new set of _instances from the table
            //  - Use this set of table rows and project it by returning a new subset of the target attribute _instances

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
            t1.Append(); // Append a new record. An overloaded method could take an array/list/map of values - check how TableSet works
            t1.SetValue("orders", t1.Length-1, 10);
            t1.SetValue("revenue", t1.Length - 1, 2000);
            t1.SetValue("name", t1.Length - 1, "Smith");

            // Test

        }

        [TestMethod]
        public void TwoTableLoadTest()
        {
            // Table Employees has one column (say, dept_id - column number 5) referencing rows from table Departments (say, by using column dept_id - column number 0)
            // Define two sets where one references the other. Define load scenario so that we load rows along with references.
            // The idea is that the original function E -> dept_id (int) is translated into the target two functions E -> dept_id (D) -> dept_id (int). So we get one intermediate collection. 
        }
    }
}
