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
            set
            {
                if (GreaterSet == null) return;
                var dimList = GreaterSet.ExportDims; // Only this line will be changed in this class extensions for other dimension types
                if (value == true) // Include
                {
                    if (IsInGreaterSet) return;
                    dimList.Add(this);
                }
                else // Exclude
                {
                    dimList.Remove(this);
                }
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
            set
            {
                if (LesserSet == null) return;
                var dimList = LesserSet.ImportDims; // Only this line will be changed in this class extensions for other dimension types
                if (value == true) // Include
                {
                    if (IsInLesserSet) return;
                    dimList.Add(this);
                }
                else // Exclude
                {
                    dimList.Remove(this);
                }
            }
        }

        #endregion

        #region Data methods

        /// <summary>
        /// Definition of this set tuples in terms of import dimension tuples. It is used to populate this set by using data from other sets via import dimensions. 
        /// </summary>
        public SetMapping _importMapping;
        public SetMapping ImportMapping // We store mapping instead of expression because it is easier to maintain (edit)
        {
            get { return _importMapping; }
            set
            {
                if (_importMapping == value) return;

                if (value == null)
                {
                    _importMapping = value; // now the dimension is useless so maybe detach it from the sets
                    return;
                }

                if (GreaterSet == null) GreaterSet = value.SourceSet;
                if (LesserSet == null) GreaterSet = value.TargetSet;

                Debug.Assert(value.SourceSet == GreaterSet && value.TargetSet == LesserSet, "Wrong use: the mapping source and target sets have to corresond to the dimension sets.");

                // The mapping can reference new elements which have to be integrated into the schema
                DimTree tree = value.GetTargetTree();
                PathMatch match = value.Matches.FirstOrDefault(m => m.TargetPath.GreaterSet.IsPrimitive);
                SetTop schema = match != null ? match.TargetPath.GreaterSet.Top : null; // We assume that primitive sets always have root defined (other sets might not have been added yet).
                tree.IncludeInSchema(schema); // Include new elements in schema

                _importMapping = value; // Configure set for import
            }
        }

        public override void ComputeValues()
        {
            Debug.Assert(ImportMapping != null && ImportMapping.SourceSet == GreaterSet && ImportMapping.TargetSet == LesserSet, "Target/Output of import mapping/expression must be equal to the set where it is stored.");

            Expression importExpression = ImportMapping.GetTargetExpression(); // Build a tuple tree with paths in leaves

            Set remoteSet = GreaterSet;
            if (remoteSet.Top is SetTopOledb)
            {
                // Request a (flat) result set from the remote set (data table)
                DataTable dataTable = ((SetTopOledb)remoteSet.Top).ExportAll(remoteSet);

                // For each row, evaluate the expression and append the new element
                foreach (DataRow row in dataTable.Rows) // A row is <colName, primValue> collection
                {
                    importExpression.SetOutput(Operation.ALL, null);
                    importExpression.SetOutput(Operation.VARIABLE, row); // Set the input variable 'source'
                    importExpression.Evaluate(); // Evaluate the expression tree by computing primtive tuple values
//                    if (LesserSet.Find(importExpression)) continue; // Check if this and nested tuples exist already
//                    if (!LesserSet.CanAppend(importExpression)) continue; // Check if it can be formally added
                    LesserSet.Append(importExpression); // Append an element using the tuple composed of primitive values
                }
            }
            else if (remoteSet.Top is SetTopOdata)
            {
            }
            else if (remoteSet.Top == LesserSet.Top) // Intraschema: direct access using offsets
            {
                for (Offset offset = 0; offset < remoteSet.Length; offset++)
                {
                    importExpression.SetOutput(Operation.VARIABLE, offset); // Assign value of 'this' variable
                    importExpression.Evaluate();
//                    LesserSet.Find(importExpression);
//                    if (!LesserSet.CanAppend(importExpression)) continue;
                    LesserSet.Append(importExpression);
                }
            }
        }

        #endregion

        public DimImport(SetMapping mapping)
            : this(mapping.SourceSet.Name, mapping.TargetSet, mapping.SourceSet)
        {
            ImportMapping = mapping;
            // this.Add();
        }

        public DimImport(string name, Set lesserSet, Set greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if sets are of correct type.
            // TODO: Parameterize the dimension according to its purpose.
	    }
    }
}
