using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            // Add members into sets according to the schema
            //
/*
            // Add a new complex member 
            int t_1 = t.appendEmptyInstance(); // All functions are nulls (only _id functions must exist but in this model _id is offset)
            // int t_1 = t.addMember(new int[] {c1_1, c2_1, c3_1});

            // Define all functions of this member by appending or updating the mappings between set _instances
            t.idDims.get(0).setOutput(t_1, c1_1);
            t.enDims.get(0).setOutput(t_1, c2_1);
            t.enDims.get(1).setOutput(t_1, c3_1);
            // t.set(t_1, new int[] {c1_1, c2_1, c3_1});

            // Add (or use existing) primitive values and get their _offsets
            int c1_1 = c1.appendEmptyInstances(2.2);
            int c2_1 = c2.appendEmptyInstances(4.4);
            int c3_1 = c3.appendEmptyInstances(6.6);
*/
            // Read and test
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
