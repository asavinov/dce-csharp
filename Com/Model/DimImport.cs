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
                if (GreaterSet == null) return false;
                var dimList = GreaterSet.ImportDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
        }

        public override bool IsInLesserSet
        {
            get
            {
                if (LesserSet == null) return false;
                var dimList = LesserSet.ExportDims; // Only this line will be changed in this class extensions for other dimension types
                return dimList.Contains(this);
            }
        }

        public override void Add(int lesserSetIndex, int greaterSetIndex = -1)
        {
            if (GreaterSet != null) AddToDimensions(GreaterSet.ImportDims, greaterSetIndex);
            if (LesserSet != null) AddToDimensions(LesserSet.ExportDims, lesserSetIndex);

            // Notify that a new child has been added
            if (LesserSet != null) LesserSet.NotifyAdd(this);
            if (GreaterSet != null) GreaterSet.NotifyAdd(this);
        }

        public override void Remove()
        {
            if (GreaterSet != null) GreaterSet.ImportDims.Remove(this);
            if (LesserSet != null) LesserSet.ExportDims.Remove(this);

            // Notify that a new child has been removed
            if (LesserSet != null) LesserSet.NotifyRemove(this);
            if (GreaterSet != null) GreaterSet.NotifyRemove(this);
        }

        public override void Replace(Dim dim)
        {
            int greaterSetIndex = GreaterSet.ImportDims.IndexOf(dim);
            int lesserSetIndex = LesserSet.ExportDims.IndexOf(dim);
            dim.Remove();

            this.Add(lesserSetIndex, greaterSetIndex);
        }

        #endregion

        #region Data methods

        [System.Obsolete("Use set population method instead.", true)]
        public override void ComputeValues()
        {
            Debug.Assert(Mapping != null && Mapping.TargetSet == GreaterSet && Mapping.SourceSet == LesserSet, "Wrong use: source set of mapping must be lesser set of the dimension, and target set must be greater set.");

            Set sourceSet = LesserSet;
            Set targetSet = GreaterSet;

            //
            // Prepare the expression from the mapping
            //
            Expression tupleExpression = Mapping.GetTargetExpression(); // Build a tuple tree with paths in leaves

            var funcExpr = ExpressionScope.CreateFunctionDeclaration(Name, sourceSet.Name, targetSet.Name);
            funcExpr.Statements[0].Input = tupleExpression; // Return statement
            funcExpr.ResolveFunction(sourceSet.Top);
            funcExpr.Resolve();

            //
            // Evaluate the expression depending on the source set type
            //
            if (sourceSet.Top is SetTopOledb)
            {
                // Request a (flat) result set from the remote set (data table)
                DataTable dataTable = ((SetTopOledb)sourceSet.Top).ExportAll(sourceSet);

                // For each row, evaluate the expression and append the new element
                foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
                {
                    funcExpr.Input.Dimension.Value = row; // Initialize 'this'
                    funcExpr.Evaluate(); // Evaluate the expression tree by computing primtive tuple values
                    //if (targetSet.Find(importExpression)) continue; // Check if this and nested tuples exist already
                    //if (!targetSet.CanAppend(importExpression)) continue; // Check if it can be formally added
                    targetSet.Append(tupleExpression); // Append an element using the tuple composed of primitive values
                }
            }
            else if (sourceSet.Top is SetTopOdata)
            {
            }
            else if (sourceSet.Top == LesserSet.Top) // Intraschema: direct access using offsets
            {
                for (Offset offset = 0; offset < sourceSet.Length; offset++)
                {
                    tupleExpression.Input.Dimension.Value = offset; // Initialize 'this'
                    tupleExpression.Evaluate();
                    //targetSet.Find(importExpression);
                    //if (!targetSet.CanAppend(importExpression)) continue;
                    targetSet.Append(tupleExpression);
                }
            }
        }

        #endregion

        public DimImport(Mapping mapping)
            : this(mapping.SourceSet.Name, mapping.TargetSet, mapping.SourceSet)
        {
            Mapping = mapping;
        }

        public DimImport(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension according to its purpose.
	    }
    }

}
