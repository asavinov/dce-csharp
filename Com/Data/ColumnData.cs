using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Com.Utils;
using Com.Data;
using Com.Schema;
using Com.Schema.Csv;
using Com.Data.Query;

using Rowid = System.Int32;

namespace Com.Data
{
    /// <summary>
    /// Primitive dimension.
    /// 
    /// One array of type T stores elements in their original order without sorting. 
    /// Second array stores indexes (offsets) of elements in the first array in sorted order.
    /// </summary>
    public class ColumnData<T> : DcColumnData
    {
        protected DcColumn Column { get; set; }

        // Memory management parameters for instances (used by extensions and in future will be removed from this class).
        protected static int initialSize = 1024 * 10; // In elements
        protected static int incrementSize = 1024 * 2; // In elements

        //
        // Storage for the values of the column
        //
        protected int allocatedSize; // How many elements (maximum) fit into the allocated memory
        private T[] _cells; // Each cell contains a T value in arbitrary original order

        private bool[] _nullCells; // True if the cell is null and false if it is not null. The field is used only if nulls are not values.
        private bool _nullAsValue; // Null is a normal value with a special meaning 
        private T _nullValue; // If null is a normal value this it is what is this null value. If null is not a value then this field is ignored. 
        private int _nullCount; // Nulls are stored in the beginning of array of indexes (that is, treated as absolute minimum)

        //
        // Index
        //
        private int[] _offsets; // Each cell contains an offset to an element in cells in ascending or descending order

        protected static IAggregator<T> Aggregator;
        public static T Aggregate<T, TProcessor>(T[] values, string function, TProcessor proc) where TProcessor : IAggregator<T>
        {
            switch (function)
            {
                case "SUM": return proc.Sum(values);
                case "AVG": return proc.Avg(values);
                default: throw new Exception("There is no such aggregation operation.");
            }
        }

        #region DcColumnData interface

        protected Rowid _length; // It is only used if lesser set is not set, that is, for hanging dimension (theoretically, we should not use hanging dimensions and do not need then this field)
        public Rowid Length
        {
            get
            {
                return _length;
            }
            set
            {
                if (value == _length) return;

                // Ensure that there is enough memory
                if (value > allocatedSize) // Not enough storage for the new element
                {
                    allocatedSize += incrementSize * ((value - allocatedSize) / incrementSize + 1);
                    System.Array.Resize<T>(ref _cells, allocatedSize); // Resize the storage for values
                    System.Array.Resize(ref _offsets, allocatedSize); // Resize the index
                }

                // Update data and index in the case of increase (append to last) and decrease (delete last)
                if (value > _length)
                {
                    while (value > _length) Append(null); 
                    // OPTIMIZE: Instead of appending individual values, write a method for appending an interval of offset (with default value)
                }
                else if (value < _length)
                {
                    while (value < _length) Remove(Length - 1);
                    // OPTIMIZE: remove last elements directly
                }
            }
        }

        public bool AutoIndex { get; set; }

        protected bool _indexed;
        public bool Indexed { get { return _indexed; } }

        public void Reindex()
        {
            Reindex2();
        }

        public void Reindex1()
        {
            // Here the idea is to sort the index (that is, precisely what we need) by defining a custom comparer via cells referenced by the index elements
            // Source: http://stackoverflow.com/questions/659866/is-there-c-sharp-support-for-an-index-based-sort
            // http://www.csharp-examples.net/sort-array/

            // Approach 1 (.Net 4.5) method Create can be used to create a delegate:
            //Comparer<T> cellComparer = Comparer<T>.Default;
            //IComparer<int> offsetComparer = Comparer<int>.Create( (a, b) => cellComparer.Compare(_cells[a], _cells[b]) );

            // Approach 2 use a custom comparer
            // Problem: this method does not retain the original order for equal values; intervals with the same value will not be sorted; BinarySearch will not work.
            OffsetComparer<T> offsetComparer = new OffsetComparer<T>(_cells);

            Array.Sort<int>(_offsets, 0, _length, offsetComparer);

            _indexed = true;
        }

        class OffsetComparer<T> : IComparer<int>
        {
            T[] cells;
            Comparer<T> cellComparer;

            public int Compare(int x, int y)
            {
                return cellComparer.Compare(cells[x], cells[y]);
            }
            public OffsetComparer(T[] cells)
            {
                this.cells = cells;
                cellComparer = Comparer<T>.Default;
            }
        }

        public void Reindex2()
        {
            // Index sort in Java: http://stackoverflow.com/questions/951848/java-array-sort-quick-way-to-get-a-sorted-list-of-indices-of-an-array
            // We need it because the sorting method will change the cells. 
            // Optimization: use one global large array for that purpose instead of a local variable
            T[] tempCells = (T[])_cells.Clone();

            // Reset offsets befroe sorting (so it will be completely new sort)
            for (int i = 0; i < _length; i++)
            {
                _offsets[i] = i; // Now each offset represents (references) an element of the function (from domain) but they are unsorted
            }

            Array.Sort<T, int>(tempCells, _offsets, 0, _length);
            // Now offsets are sorted and temp array can be deleted
        }

        public bool IsNull(Rowid input)
        {
            if (_nullAsValue)
            {
                // For nullable storage: simply check the value (actually this method is not needed for nullable storage because the user can compare the values returned from GetValue)
                return EqualityComparer<T>.Default.Equals(_cells[input], _nullValue);
            }
            else
            {
                // For non-nullable storage, use the index to find if this cell is in the null interval of the index (beginning)
                int pos = FindIndex(input);
                return pos < _nullCount;
            }
        }

        public object GetValue(Rowid input)
        {
            return _cells[input]; // We do not check the range of offset - the caller must guarantee its validity
        }

        public void SetValue(Rowid input, object value) // Replace an existing value with the new value and update the index. 
        {
            T val = default(T);
            if (value == null)
            {
                val = _nullValue;
            }
            else
            {
                val = ToThisType(value);
            }

            // No indexing required
            if (!AutoIndex)
            {
                _offsets[_length] = _length; // It will be last in sorted order
                _cells[input] = val;
                _indexed = false; // Mark index as dirty
                return;
            }

            //
            // Index
            //

            // 1. Old sorted position of the cell we are going to overwrite
            int oldPos = FindIndex(input);

            // 2.1 Find an interval for the new value (FindIndexes)
            Tuple<int, int> interval;
            if (value == null)
            {
                interval = new Tuple<int, int>(0, _nullCount);
                if (oldPos >= _nullCount) _nullCount++; // If old value is not null, then increase the number of nulls
            }
            else
            {
                interval = FindIndexes(val);
                if (oldPos < _nullCount) _nullCount--; // If old value is null, then decrease the number of nulls
            }

            // 2.2 Find sorted position within this value interval (by increasing offsets)
            int pos = Array.BinarySearch(_offsets, interval.Item1, interval.Item2 - interval.Item1, input);
            if (pos < 0) pos = ~pos;

            // 3. Finally simply change the position by shifting the index elements accordingly
            if (pos > oldPos)
            {
                Array.Copy(_offsets, oldPos + 1, _offsets, oldPos, (pos - 1) - oldPos); // Shift backward by overwriting old
                pos = pos - 1;
            }
            else if (pos < oldPos)
            {
                Array.Copy(_offsets, pos, _offsets, pos + 1, oldPos - pos); // Shift forward by overwriting old pos
            }

            _offsets[pos] = input;
            _cells[input] = val;
        }

        public void SetValue(object value)
        {
            if (value == null)
            {
                Nullify();
                return;
            }

            T val = ToThisType(value);
            for (Rowid i = 0; i < _length; i++)
            {
                _cells[i] = val;
                _offsets[i] = i;
            }

            _nullCount = 0;
            _indexed = true;
        }

        public void Nullify() // Reset values and index to initial state (all nulls)
        {
            throw new NotImplementedException();
        }

        public void Append(object value)
        {
            // Ensure that there is enough memory
            if (_length == allocatedSize) // Not enough storage for the new element (we need _length+1)
            {
                allocatedSize += incrementSize;
                System.Array.Resize<T>(ref _cells, allocatedSize); // Resize the storage for values
                System.Array.Resize(ref _offsets, allocatedSize); // Resize the index
            }

            T val = default(T);
            if (value == null)
            {
                val = _nullValue;
            }
            else
            {
                val = ToThisType(value);
            }

            // No indexing required
            if (!AutoIndex)
            {
                _offsets[_length] = _length; // It will be last in sort
                _cells[_length] = val;
                _length = _length + 1;

                _indexed = false;
                return;
            }

            //
            // Index
            //
            Tuple<int, int> interval;
            if (value == null)
            {
                interval = new Tuple<int, int>(0, _nullCount);
                _nullCount++;
            }
            else
            {
                interval = FindIndexes(val);
            }

            int pos = interval.Item2; // New value has the largest offset and hence is inserted after the end of the interval of values
            Array.Copy(_offsets, pos, _offsets, pos + 1, _length - pos); // Free an index element by shifting other elements forward

            _offsets[pos] = _length; // Update index
            _cells[_length] = val; // Update storage
            _length = _length + 1;
        }

        public void Insert(Rowid input, object value)
        {
            throw new NotImplementedException();
        }

        public void Remove(Rowid input) 
        {
            int pos = FindIndex(input);

            Array.Copy(_offsets, pos + 1, _offsets, pos, _length - pos - 1); // Remove this index element by shifting all next elements backward

            // If it was null value then decrease also their count
            if (pos < _nullCount)
            {
                _nullCount--;
            }

            _length = _length - 1;
        }

        public object Project(Rowid[] offsets)
        {
            return projectOffsets(offsets);
            // Returns an array but we delcare it as object (rather than array of objects) because we cannot case between array types (that is, int[] is not object[]) and therefore we return object.
            // Alternatives for changing type and using array type:
            // Cast return array type T[] -> object[]
            // return (object[])Convert.ChangeType(project(offsets), typeof(object[])); // Will fail at run time in the case of wrong type
            // return project(offsets).Cast<object>().ToArray();
        }

        public Rowid[] Deproject(object value)
        {
            if (value == null || !value.GetType().IsArray)
            {
                return deprojectValue(ToThisType(value));
            }
            else
            {
                return deproject(ArrayToThisType(value));
            }
        }

        #endregion

        #region The former DcColumnDefinition. Now part of DcColumnData

        protected string formula;
        public String Formula
        {
            get { return formula; }
            set
            {
                formula = value;
                _translateError = false;
                _evaluateError = true;
                Translate();
            }
        }

        //
        // Used for evaluation
        //
        protected ExprNode formulaExpr { get; set; }

        protected DcVariable thisVariable;

        // For aggregation
        protected ExprNode factsExpr;
        protected ExprNode groupExpr; // Returns a group this fact belongs to, is stored in the group variable
        protected DcVariable groupVariable; // Stores current group (input for the aggregated function)
        protected ExprNode measureExpr; // Returns a new value to be aggregated with the old value, is stored in the measure variable
        protected DcVariable measureVariable; // Stores new value (output for the aggregated function)
        protected ExprNode outputExpr;

        //
        // Schema and translation
        //
        public bool IsAppendSchema { get; set; }

        protected bool _translateError;
        public virtual bool TranslateError
        {
            get { return _translateError; }
            set
            {
                // True cannot be set directly  it results from Translate only
                if (value == true) return;

                // False (invalid) has to be propagated 
                if (value == true)
                {
                    // TODO: Check how it influences dependent columns (recursively). Some of them can change their status
                    // Alternatively, the status is checked by all columns individually on demand. 
                }
                else
                {
                    _translateError = value;
                    // TODO: Mark invalid all directly dependant columns/tables which will propagate it further
                    // Get all dependant elements
                    // Set all these elements invalid
                }
            }
        }
        public virtual void Translate()
        {
            _translateError = false;

            //
            // Translate only this individual column formula. It will generate a new dependency graph.
            //

            // Some column types do not need formulas (like key columns) and hence are always valid at both schema- and data-level
            // What if a formula has not been given yet but is supposed to be defined, say, for non-key column?
            // We could assume that it is a null-function which is therefore valid
            if (string.IsNullOrWhiteSpace(formula))
            {
                _translateError = false;
            }
            else
            {
                // Parse
                try
                {
                    Parse();
                }
                catch (Exception e)
                {
                    _translateError = true;
                }

                // Bind
                if (!_translateError)
                {
                    try
                    {
                        Bind();
                    }
                    catch (Exception e)
                    {
                        _translateError = true;
                    }
                }
            }

            // Update the graph
            this.Column.Input.Space.Dependencies.RemoveUsed(Column);
            if (!_translateError)
            {
                var usesColumns = UsesColumns(); // Get columns from ast
                this.Column.Input.Space.Dependencies.AddUsed(Column, usesColumns);
            }

            ((Column)Column).NotifyPropertyChanged("");

            // This can influence status of any other column
            var allColumns = Column.Input.Space.GetColumns(null);
            allColumns.ForEach(c => ((Column)c).NotifyPropertyChanged("Status"));
        }

        // Parse and generate a syntax tree
        protected void Parse()
        {
            ExprBuilder exprBuilder = new ExprBuilder();
            ExprNode expr = exprBuilder.Build(formula);
            formulaExpr = expr;
        }

        // Resolve symbols in the syntax tree by finding referenced schema elements. 
        // This also performes schema population by appending columns if they cannot be found. 
        // Schema population is performed if the corresponding flag is true. 
        protected void Bind()
        {
            // NOTE: This should be removed or moved to the expression. Here we store non-syntactic part of the definition in columndef and then set the expression. Maybe we should have syntactic annotation for APPEND flag (output result annotation, what to do with the output). 
            if (formulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                // Adjust the expression according to other parameters of the definition
                if (IsAppendData)
                {
                    formulaExpr.Action = ActionType.APPEND;
                }
                else
                {
                    formulaExpr.Action = ActionType.READ;
                }
            }

            // General parameters
            DcSpace Workspace = Column.Input.Schema.Space;

            if (Column.Input.Schema is SchemaCsv) // Import from CSV
            {
                // Prepare parameter variables for the expression 
                DcTable thisTable = Column.Input;

                thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
                thisVariable.TypeSchema = thisTable.Schema;
                thisVariable.TypeTable = thisTable;

                // Parameterize expression and resolve it (bind names to real objects) 
                formulaExpr.OutputVariable.SchemaName = Column.Output.Schema.Name;
                formulaExpr.OutputVariable.TypeName = Column.Output.Name;
                formulaExpr.OutputVariable.TypeSchema = Column.Output.Schema;
                formulaExpr.OutputVariable.TypeTable = Column.Output;

                formulaExpr.EvaluateAndResolveSchema(Workspace, new List<DcVariable>() { thisVariable });
            }
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC || formulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                // Prepare parameter variables for the expression 
                DcTable thisTable = Column.Input;

                thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
                thisVariable.TypeSchema = thisTable.Schema;
                thisVariable.TypeTable = thisTable;

                // Parameterize expression and resolve it (bind names to real objects) 
                formulaExpr.OutputVariable.SchemaName = Column.Output.Schema.Name;
                formulaExpr.OutputVariable.TypeName = Column.Output.Name;
                formulaExpr.OutputVariable.TypeSchema = Column.Output.Schema;
                formulaExpr.OutputVariable.TypeTable = Column.Output;

                formulaExpr.EvaluateAndResolveSchema(Workspace, new List<DcVariable>() { thisVariable });
            }
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                // Aassert: FactTable.GroupFormula + ThisSet.ThisFunc = FactTable.MeasureFormula
                // Aassert: if LoopSet == ThisSet then GroupCode = null, ThisFunc = MeasureCode

                // Facts
                factsExpr = formulaExpr.GetChild("facts").GetChild(0);

                // This table and variable
                string thisTableName = factsExpr.Name;
                DcTable thisTable = Column.Input.Schema.GetSubTable(thisTableName);

                thisVariable = new Variable(thisTable.Schema.Name, thisTable.Name, "this");
                thisVariable.TypeSchema = thisTable.Schema;
                thisVariable.TypeTable = thisTable;

                // Groups
                groupExpr = formulaExpr.GetChild("groups").GetChild(0);

                groupExpr.EvaluateAndResolveSchema(Workspace, new List<DcVariable>() { thisVariable });

                groupVariable = new Variable(Column.Input.Schema.Name, Column.Input.Name, "this");
                groupVariable.TypeSchema = Column.Input.Schema;
                groupVariable.TypeTable = Column.Input;

                // Measure
                measureExpr = formulaExpr.GetChild("measure").GetChild(0);

                measureExpr.EvaluateAndResolveSchema(Workspace, new List<DcVariable>() { thisVariable });

                measureVariable = new Variable(Column.Output.Schema.Name, Column.Output.Name, "value");
                measureVariable.TypeSchema = Column.Output.Schema;
                measureVariable.TypeTable = Column.Output;

                // Updater/aggregation function
                ExprNode updaterExpr = formulaExpr.GetChild("aggregator").GetChild(0);
                outputExpr = ExprNode.CreateUpdater(Column, updaterExpr.Name);

                outputExpr.EvaluateAndResolveSchema(Workspace, new List<DcVariable>() { groupVariable, measureVariable });
            }
            else
            {
                throw new NotImplementedException("This type of column definition is not implemented.");
            }
        }

        //
        // Data and evaluation
        //
        public bool IsAppendData { get; set; }

        protected bool _evaluateError;
        public virtual bool EvaluateError
        {
            get { return _evaluateError; }
            set
            {
                // True cannot be set directly - it results from Evaluate only
                if (value == true) return;

                // False (invalid) has to be propagated 
                if (value == true)
                {
                    // TODO: Check how it influences dependent columns. Some of them can change their status
                    // Alternatively, the status is checked by all columns individually on demand. 
                }
                else
                {
                    _evaluateError = value;
                    // TODO: Mark invalid all directly dependant columns/tables which will propagate it further
                    // Get all dependant elements
                    // Set all these elements invalid
                }
            }
        }
        public virtual void Evaluate()
        {
            // Either formula is not needed or not yet provided 
            if (formulaExpr == null || formulaExpr.DefinitionType == ColumnDefinitionType.FREE)
            {
                _evaluateError = false;
                ((Column)Column).NotifyPropertyChanged("");
                return; // Nothing to evaluate
            }

            //
            // Evaluate depends on the type of definition
            //
            AutoIndex = false;
            //Nullify();

            object thisCurrent = null;

            if (Column.Input.Schema is SchemaCsv) // Import from CSV
            {
                formulaExpr.EvaluateBegin();
                DcTableReader tableReader = Column.Input.GetData().GetTableReader();
                tableReader.Open();
                while ((thisCurrent = tableReader.Next()) != null)
                {
                    thisVariable.SetValue(thisCurrent); // Set parameters of the expression

                    formulaExpr.Evaluate(); // Evaluate the expression

                    // We do not store import functions (we do not need this data)
                    object newValue = formulaExpr.OutputVariable.GetValue();
                    //columnData.SetValue((Rowid)thisCurrent, newValue);
                }
                tableReader.Close();
                formulaExpr.EvaluateEnd();
            }
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC || formulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                formulaExpr.EvaluateBegin();
                DcTableReader tableReader = Column.Input.GetData().GetTableReader();
                tableReader.Open();
                while ((thisCurrent = tableReader.Next()) != null)
                {
                    thisVariable.SetValue(thisCurrent); // Set parameters of the expression

                    formulaExpr.Evaluate(); // Evaluate the expression

                    // Write the result value to the function
                    // NOTE: We want to implement write operations with functions in the expression itself, particularly, because this might be done by intermediate nodes each of them having also APPEND flag
                    // NOTE: when writing or find/append output to a table, an expression needs a TableWriter (or reader) object which is specific to the expression node (also intermediate)
                    // NOTE: it could be meaningful to implement separately TUPLE (DOWN, NON-PRIMITIVE) nodes and CALL (UP, PRIMITIVE) expression classes since their general logic/purpose is quite different, particularly, for table writing. 
                    // NOTE: where expression (in tables) is evaluated without writing to column
                    object newValue = formulaExpr.OutputVariable.GetValue();
                    this.SetValue((Rowid)thisCurrent, newValue);
                }
                tableReader.Close();
                formulaExpr.EvaluateEnd();
            }
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                // Aassert: FactTable.GroupFormula + ThisSet.ThisFunc = FactTable.MeasureFormula
                // Aassert: if LoopSet == ThisSet then GroupCode = null, ThisFunc = MeasureCode

                DcTable thisTable = Column.Input.Schema.GetSubTable(factsExpr.Name);

                formulaExpr.EvaluateBegin();
                DcTableReader tableReader = thisTable.GetData().GetTableReader();
                tableReader.Open();
                while ((thisCurrent = tableReader.Next()) != null)
                {
                    thisVariable.SetValue(thisCurrent); // Set parameters of the expression

                    groupExpr.Evaluate();
                    Rowid groupElement = (Rowid)groupExpr.OutputVariable.GetValue();
                    groupVariable.SetValue(groupElement);

                    measureExpr.Evaluate();
                    object measureValue = measureExpr.OutputVariable.GetValue();
                    measureVariable.SetValue(measureValue);

                    outputExpr.Evaluate(); // Evaluate the expression

                    // Write the result value to the function
                    object newValue = outputExpr.OutputVariable.GetValue();
                    this.SetValue(groupElement, newValue);
                }
                tableReader.Close();
                formulaExpr.EvaluateEnd();
            }
            else
            {
                throw new NotImplementedException("This type of column definition is not implemented.");
            }

            Reindex();
            AutoIndex = true;

            // Finally, we need to set the flag that indicates the result of the operation and status for the column
            _evaluateError = false;
            ((Column)Column).NotifyPropertyChanged("");
        }

        //
        // Dependencies
        //
        public List<List<DcColumn>> UsesColumnsAll()
        {
            // The first list has independent tables which do not have incoming columns (remote or product)
            // Each next list has columns which can be evaluted after the previous list
            // After the last list, this column can be evaluated. 

            var res = new List<List<DcColumn>>();
            res.Add(new List<DcColumn>(new DcColumn[] { this.Column })); // Start from this column

            while (true)
            {
                // Compute new dependencies for each column in the previous dependencies 
                var nextDeps = new List<DcColumn>();
                var prevDeps = res[res.Count - 1];
                foreach (DcColumn col in prevDeps)
                {
                    List<DcColumn> newDeps = UsesColumns();
                    nextDeps.AddRange(newDeps.Except(nextDeps)); // Add only new columns
                }

                if (nextDeps.Count > 0)
                {
                    res.Add(nextDeps);
                    // TODO: Check for possible cycles if a new column exists among previous columns (or do it later)
                }
                else
                {
                    break; // No dependencies anymore
                }
            }

            return res;
        }
        public List<DcColumn> UsesColumns()
        {
            var res = new List<DcColumn>();

            if (string.IsNullOrEmpty(Formula) || formulaExpr == null) // Free columns with no formula
            {
                //
                // Product column. No input column with formula (even indirectly). 
                // There is no any lesser column which writes to this column. 
                // 1. [product is flat - currently it is so implemented] We can assume that we depend on greater tables and hence it our task to fill them. 
                //    The product operation works only at one level (flat) and it can work/be started only if all greater tables are green.
                // 2. [product is hierarchical like tuple append] We can think of it as equivalent to appending *all* tuples which will simultaniously fill greater tables before. 
                //    So the product procedure itself can recursievely fill the greater table which are not filled. 
                //    Note that appending tuples does the same (recursive filling) but for each individual tuple

                //
                // Depends on input column with formula (also indirectly). 
                // There is a lesser column which writes/influences this column
                // Evaluation of lesser (append) columns can result in green status for many (covered) columns including this one

            }
            // Depends on this.greater columns. Formula uses/reads other (this.greater) columns and writes/fills (output.greater) columns 
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.ANY || formulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC || formulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                // 1. Find all explicit read-references to other columns excluding write-references (tuple members)
                List<ExprNode> nodes = formulaExpr.Find((DcColumn)null);
                // 2. Exclude write columns (tuple members)
                List<ExprNode> readNodes = nodes.Where(x => x.Parent == null || x.Parent.Item.Operation != OperationType.TUPLE).ToList();
                // 3. Get column references
                res = readNodes.Select(x => x.Column).ToList();
            }
            // Depends on this.lesser columns. Formula depends on lesser table/columns
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                /*
                // 1. We need the fact table to be up-to-date
                res.Add(FactTable); // This column depends on the fact table

                // 2. All columns in the group path (actually, any group expression)
                if (GroupPaths != null)
                {
                    foreach (ColumnPath path in GroupPaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }

                // 2. All columns in the measure path (actually, any group expression)
                if (MeasurePaths != null)
                {
                    foreach (ColumnPath path in MeasurePaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }

                // 4. All columns in the update expression (aggregation function)
                */

            }

            return res;
        }

        public List<DcColumn> IsUsedInColumns() // Dependants
        {
            List<DcColumn> res = new List<DcColumn>();

            // TODO: Find which other columns use (depend on) this column in the definition
            // They either reference its name or are influenced indirectly

            return res;
        }

        public List<DcTable> UsesTables() // This element depends upon
        {
            // Compoile formula and produce an expression (structured formula) which will be then analized

            List<DcTable> res = new List<DcTable>();

            // 0. This input table must be up-to-date IF this table is calculated (not key)

            if (formulaExpr == null)
            {
                ;
            }
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.ANY || formulaExpr.DefinitionType == ColumnDefinitionType.ARITHMETIC || formulaExpr.DefinitionType == ColumnDefinitionType.LINK)
            {
                // 1. Find all references to other columns
                res = formulaExpr.Find((DcTable)null).Select(x => x.OutputVariable.TypeTable).ToList();

                // 2. All their input tables must be up-to-date
                // 3. NOTE: transitive dependencies: these columns can actually depend on each other
            }
            else if (formulaExpr.DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                /*
                // 1. We need the fact table to be up-to-date
                res.Add(FactTable); // This column depends on the fact table

                // 2. All columns in the group path (actually, any group expression)
                if (GroupPaths != null)
                {
                    foreach (ColumnPath path in GroupPaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }

                // 2. All columns in the measure path (actually, any group expression)
                if (MeasurePaths != null)
                {
                    foreach (ColumnPath path in MeasurePaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }

                // 4. All columns in the update expression (aggregation function)
                */

            }

            return res;
        }

        #endregion

        #region DcJson serialization

        public virtual void ToJson(JObject json) // Write fields to the json object
        {
            // No super-object

            // Column definition
            json["formula"] = Formula;
        }
        public virtual void FromJson(JObject json, DcSpace ws) // Init this object fields by using json object
        {
            // No super-object

            // Column definition
            json["formula"] = Formula;
        }

        #endregion

        #region Protected data methods (index, sorting, projecting etc.)

        public object Aggregate(object values, string function) // It is actually static but we cannot use static virtual methods in C#
        {
            if (values == null) return default(T);

            T[] array = ArrayToThisType(values);
            return Aggregate(array, function, Aggregator);
        }

        private int FindIndex(int input) // Find an index for an offset of a cell (rather than a value in this cell)
        {
            // A value can be stored at many different offsets while one offset has always one index and therefore a single valueis returned rather than an interval.

            // First, we try to find it in the null interval
            int pos = Array.BinarySearch(_offsets, 0, _nullCount, input);
            if (pos >= 0 && pos < _nullCount) return pos; // It is null

            // Second, find an interval of offsets which all reference this value
            Tuple<int,int> indexes = FindIndexes(_cells[input]);
            // Third, in this interval, find exact offset for the specified input. 
            // In BinarySearch the array (interval) has to be sorted - it is a requirement to the index method
            pos = Array.BinarySearch(_offsets, indexes.Item1, indexes.Item2 - indexes.Item1, input);
            if (pos >= indexes.Item1 && pos < indexes.Item2) return pos;

            return -1; // Not found (error - all valid/existing offsets must be present in the index)
        }

        private Tuple<int, int> FindIndexes(T value)
        {
            // Returns an interval of indexes which all reference the specified value
            // min is inclusive and max is exclusive
            // min<max - the value is found between [min,max)
            // min=max - the value is not found, min=max is the position where it has to be inserted
            // min=length - the value has to be appended (and is not found, so min=max) 

            // Alternative: Array.BinarySearch<T>(mynumbers, value) or  BinarySearch<T>(T[], Int32, Int32, T) - search in range
            // Comparer<T> comparer = Comparer<T>.Default;
            // mid = Array.BinarySearch(_offsets, 0, _count, value, (a, b) => comparer.Compare(_cells[a], _cells[b]));
            //IComparer<T> comparer = new IndexComparer<T>(this);
            //mid = Array.BinarySearch(_offsets, 0, _count, value, comparer);

            // Binary search in a sorted array with ascending values: http://stackoverflow.com/questions/8067643/binary-search-of-a-sorted-array
            int mid = _nullCount, first = _nullCount, last = _length;
            while (first < last)
            {
                mid = (first + last) / 2;

                int comp = Comparer<T>.Default.Compare(value, _cells[_offsets[mid]]);
                if (comp > 0) // Less: target > mid
                {
                    first = mid + 1;
                }
                else if (comp < 0) // Greater: target < mynumbers[mid]
                {
                    last = mid;
                }
                else
                {
                    break;
                }
            }

            if (first == last) // Not found
            {
                return new Tuple<int, int>(first, last);
            }

            // One element is found. Now find min and max positions for the interval of equal values.
            // Optimization: such search is not efficient - it is simple scan. One option would be use binary serach within interval [first, mid] and [mid, last]
            for (first = mid; first >= _nullCount && EqualityComparer<T>.Default.Equals(value, _cells[_offsets[first]]); first--)
                ;
            for (last = mid; last < _length && EqualityComparer<T>.Default.Equals(value, _cells[_offsets[last]]); last++)
                ;

            return new Tuple<int, int>(first + 1, last);
        }

        protected T[] projectOffsets(int[] offsets)
        {
            // Question: possible sorting of output: ascending, according to input offsets specified, preserve the original order of offsets or do not guarantee anything
            // Question: will it be easier to compute if input offsets are somehow sorted?

            if(offsets == null || offsets.Length == 0) return new T[0];

            T[] result = new T[offsets.Length];
            int resultSize = 0;
            for (int i = 0; i < offsets.Length; i++) // For each input offset to be projected
            {
                // Check if it exists already 
                // Optimization is needed: 
                // 1. Sorting during projection (and hence easy to find duplicates), 
                // 2. Maintain own (local) index (e.g., sorted elements but separate from projection buing built) 
                // 3. Use de-projection
                // 4. In many cases the _offsets can be already sorted (if selected from a sorted list)
                T cell = _cells[offsets[i]]; // It is what we either write to the output (if not written already) or ignore (if exists in the output)
                bool found = false;
                for (int j = 0; j < resultSize; j++)
                {
                    if (EqualityComparer<T>.Default.Equals(cell, result[j])) // result[j] == cell does not work for generics
                    {
                        found = true;
                        break; // Found 
                    }
                }
                if (found) break;
                // Append new cell
                result[resultSize] = cell;
                resultSize++;
            }

            Array.Resize<T>(ref result, resultSize);
            return result;
        }

        protected int[] deprojectValue(T value)
        {
            Tuple<int, int> indexes;

            if (value == null)
            {
                indexes = new Tuple<int, int>(0, _nullCount);
            }
            else
            {
                indexes = FindIndexes(value);
            }

            if (indexes.Item1 == indexes.Item2)
            {
                return new int[0]; // Not found
            }

            int[] result = new int[indexes.Item2 - indexes.Item1];

            for (int i = 0; i < result.Length; i++)
            {
                // OPTIMIZE: Use system copy function
                result[i] = _offsets[indexes.Item1 + i];
            }

            return result;
        }

        protected int[] deproject(T[] values)
        {
            // Assumption: values are unique

            Tuple<int, int>[] intervals = new Tuple<int, int>[values.Length];
            int totalLength = 0;
            for (int i = 0; i < values.Length; i++)
            {
                intervals[i] = FindIndexes(values[i]);
                totalLength += intervals[i].Item2 - intervals[i].Item1;
            }

            int[] result = new int[totalLength];
            int pos = 0;
            for (int i = 0; i < intervals.Length; i++)
            {
                for (int j = intervals[i].Item1; j < intervals[i].Item2; j++, pos++)
                {
                    result[pos] = _offsets[j];
                }
            }

            return result;
        }

        protected T ToThisType(object value)
        {
            // You can use Convert.ChangeType method, if the types you use implement IConvertible (all primitive types do):
            // Convert.ChangeType(value, targetType);
            // Returns an object of the specified type and whose value is equivalent to the specified object.
            // Double d = -2.345;
            // int i = (int)Convert.ChangeType(d, typeof(int));
            // string s = "12/12/98";
            // DateTime dt = (DateTime)Convert.ChangeType(s, typeof(DateTime));

            // object readData = reader.ReadContentAsObject();
            // int outInt;
            // if (int.TryParse(readData, out outInt))
            //    return outInt;

            if (value is T)
            {
                return (T)value;
            }
            else
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (InvalidCastException e)
                {
                    return default(T);
                }
            }
        }

        protected T[] ArrayToThisType(object values)
        {
            // Cast array parameter type: object[] -> T[]
            if (values is T[])
            {
                return values as T[]; // Or return (T[])values;
            }
            else if (values.GetType().IsArray)
            {
                var array = values as T[];
                if (array != null) return array;

                // If array is null ('as' failed)
                try
                {
                    return (T[])Convert.ChangeType(values, typeof(T[])); // Will fail at run time in the case of wrong type
                }
                catch (InvalidCastException e)
                {
                    return default(T[]);
                }
            }
            else
            {
                return new T[] { ToThisType(values) };
            }

            // Alternatives:
            // return values.Cast<T>().ToArray(); 
            // Array.ConvertAll<object, T>(values, Convert.ToChar);
        }

        #endregion

        #region Constructors

        public ColumnData(DcColumn col)
        {
            // TODO: Check if output (greater) set is of correct type

            Column = col;

            allocatedSize = initialSize;
            _cells = new T[allocatedSize];

            _nullCells = null;
            _nullCount = Length;
            _nullAsValue = false;
            // Initialize what representative value will be used instead of nulls
            _nullValue = default(T); // Check if type is nullable: http://stackoverflow.com/questions/374651/how-to-check-if-an-object-is-nullable
            Type type = typeof(T);
            if (type == typeof(int))
            {
                _nullValue = ToThisType(int.MinValue);
                Aggregator = new IntAggregator() as IAggregator<T>;
            }
            else if (type == typeof(double))
            {
                _nullValue = ToThisType(double.NaN);
                Aggregator = new DoubleAggregator() as IAggregator<T>;
            }
            else if (type == typeof(decimal))
            {
                _nullValue = ToThisType(decimal.MinValue);
                Aggregator = new DecimalAggregator() as IAggregator<T>;
            }
            else if (type == typeof(bool))
            {
                _nullValue = ToThisType(false);
                Aggregator = new DecimalAggregator() as IAggregator<T>;
            }
            else if (type == typeof(DateTime))
            {
                _nullValue = ToThisType(DateTime.MinValue);
                Aggregator = new DecimalAggregator() as IAggregator<T>;
            }
            else if (!type.IsValueType) // Reference type
            {
                _nullAsValue = true;
                _nullValue = default(T);
            }
            else if (Nullable.GetUnderlyingType(type) != null) // Nullable<T> (like int?)
            {
                _nullAsValue = true;
                _nullValue = default(T);
            }
            else
            {
                throw new NotImplementedException();
            }

            _offsets = new int[allocatedSize];
            AutoIndex = true;
            _indexed = true;

            _length = 0;
            Length = 0;
            if (col.Input != null && col.Input.GetData() != null)
            {
                Length = col.Input.GetData().Length;
            }

            _translateError = false;
            _evaluateError = true;
        }

        #endregion
    }

}
