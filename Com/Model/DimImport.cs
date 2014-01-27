using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using Offset = System.Int32;

namespace Com.Model
{
    /// <summary>
    /// This dimension describes a function for importing data from the greater set and to the lesser set. 
    /// It is not supposed to store this function.
    /// The import procedure will iterate through the identities of the greater set and the result of evaluation will be stored in the lesser set. 
    /// Theoretically, it is possible to import data from such artificial greater sets as user input, message channels and other unusual sources.
    /// </summary>
    public class DimImport : Dim
    {
        #region Schema methods

        public override bool IsInGreaterSet
        {
            get
            {
                if (GreaterSet == null) return true;
                var dimList = GreaterSet.ExportDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
        }

        public override bool IsInLesserSet
        {
            get
            {
                if (LesserSet == null) return true;
                var dimList = LesserSet.ImportDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
        }

        public override void Add(int lesserSetIndex, int greaterSetIndex = -1)
        {
            if (GreaterSet != null) AddToDimensions(GreaterSet.ExportDims, greaterSetIndex);
            if (LesserSet != null) AddToDimensions(LesserSet.ImportDims, lesserSetIndex);

            // Notify that a new child has been added
            if (LesserSet != null) LesserSet.NotifyAdd(this);
            if (GreaterSet != null) GreaterSet.NotifyAdd(this);
        }

        public override void Remove()
        {
            if (GreaterSet != null) GreaterSet.ExportDims.Remove(this);
            if (LesserSet != null) LesserSet.ImportDims.Remove(this);

            // Notify that a new child has been removed
            if (LesserSet != null) LesserSet.NotifyRemove(this);
            if (GreaterSet != null) GreaterSet.NotifyRemove(this);
        }

        public override void Replace(Dim dim)
        {
            int greaterSetIndex = GreaterSet.ExportDims.IndexOf(dim);
            int lesserSetIndex = LesserSet.ImportDims.IndexOf(dim);
            dim.Remove();

            this.Add(lesserSetIndex, greaterSetIndex);
        }

        #endregion

        #region Data methods

        public override void ComputeValues()
        {
            Debug.Assert(Mapping != null && Mapping.SourceSet == GreaterSet && Mapping.TargetSet == LesserSet, "Target/Output of import mapping/expression must be equal to the set where it is stored.");

            Expression tupleExpression = Mapping.GetTargetExpression(); // Build a tuple tree with paths in leaves

            var funcExpr = ExpressionScope.CreateFunctionDeclaration(Name, GreaterSet.Name, LesserSet.Name);
            funcExpr.Statements[0].Input = tupleExpression; // Return statement
            funcExpr.ResolveFunction(GreaterSet.Top);
            funcExpr.Resolve();

            Set remoteSet = GreaterSet;
            if (remoteSet.Top is SetTopOledb)
            {
                // Request a (flat) result set from the remote set (data table)
                DataTable dataTable = ((SetTopOledb)remoteSet.Top).ExportAll(remoteSet);

                // For each row, evaluate the expression and append the new element
                foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
                {
//                    tupleExpression.SetOutput(Operation.ALL, null);
//                    tupleExpression.SetOutput(Operation.PARAMETER, row); // Set the input variable 'source'
                    funcExpr.Input.Dimension.Value = row; // Initialize 'this'
                    funcExpr.Evaluate(); // Evaluate the expression tree by computing primtive tuple values
//                    if (LesserSet.Find(importExpression)) continue; // Check if this and nested tuples exist already
//                    if (!LesserSet.CanAppend(importExpression)) continue; // Check if it can be formally added
                    LesserSet.Append(tupleExpression); // Append an element using the tuple composed of primitive values
                }
            }
            else if (remoteSet.Top is SetTopOdata)
            {
            }
            else if (remoteSet.Top == LesserSet.Top) // Intraschema: direct access using offsets
            {
                for (Offset offset = 0; offset < remoteSet.Length; offset++)
                {
//                    tupleExpression.SetOutput(Operation.PARAMETER, offset); // Assign value of 'this' variable
                    tupleExpression.Input.Dimension.Value = offset; // Initialize 'this'
                    tupleExpression.Evaluate();
                    //                    LesserSet.Find(importExpression);
                    //                    if (!LesserSet.CanAppend(importExpression)) continue;
                    LesserSet.Append(tupleExpression);
                }
            }
        }

        #endregion

        public DimImport(Mapping mapping)
            : this(mapping.SourceSet.Name, mapping.TargetSet, mapping.SourceSet)
        {
            Mapping = mapping;

            // The mapping can reference new elements which have to be integrated into the schema
            DimTree tree = Mapping.GetTargetTree();
            PathMatch match = Mapping.Matches.FirstOrDefault(m => m.TargetPath.GreaterSet.IsPrimitive);
            SetTop schema = match != null ? match.TargetPath.GreaterSet.Top : null; // We assume that primitive sets always have root defined (other sets might not have been added yet).
            tree.IncludeInSchema(schema); // Include new elements in schema
        }

        public DimImport(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension according to its purpose.
	    }
    }
}
