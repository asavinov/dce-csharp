using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Offset = System.Int32;

using Com.Query;

namespace Com.Model
{
    /// <summary>
    /// Primitive dimension.
    /// 
    /// One array of type T stores elements in their original order without sorting. 
    /// Second array stores indexes (offsets) of elements in the first array in sorted order.
    /// </summary>
    public class DimData<T> : DcColumnData
    {
        protected DcColumn Dim { get; set; }

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

        #region ComColumnData interface

        protected Offset _length; // It is only used if lesser set is not set, that is, for hanging dimension (theoretically, we should not use hanging dimensions and do not need then this field)
        public Offset Length
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
            // Here the idea is to sort the index (that is, precisely what we need) by defining a custom comparare via cells referenced by the index elements
            // Source: http://stackoverflow.com/questions/659866/is-there-c-sharp-support-for-an-index-based-sort
            // http://www.csharp-examples.net/sort-array/
            Comparer<T> comparer = Comparer<T>.Default;
            Array.Sort(_offsets, /* 0, _count, */ (a, b) => comparer.Compare(_cells[a], _cells[b]));

            _indexed = true;
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

        public bool IsNull(Offset input)
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

        public object GetValue(Offset input)
        {
            return _cells[input]; // We do not check the range of offset - the caller must guarantee its validity
        }

        public void SetValue(Offset input, object value) // Replace an existing value with the new value and update the index. 
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
            for (Offset i = 0; i < _length; i++)
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

        public void Insert(Offset input, object value)
        {
            throw new NotImplementedException();
        }

        public void Remove(Offset input) 
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

        public object Project(Offset[] offsets)
        {
            return projectOffsets(offsets);
            // Returns an array but we delcare it as object (rather than array of objects) because we cannot case between array types (that is, int[] is not object[]) and therefore we return object.
            // Alternatives for changing type and using array type:
            // Cast return array type T[] -> object[]
            // return (object[])Convert.ChangeType(project(offsets), typeof(object[])); // Will fail at run time in the case of wrong type
            // return project(offsets).Cast<object>().ToArray();
        }

        public Offset[] Deproject(object value)
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
            
            // Second, try to find it as a value (find the value area and then find the offset in the value interval)
            Tuple<int,int> indexes = FindIndexes(_cells[input]);
            pos = Array.BinarySearch(_offsets, indexes.Item1, indexes.Item2 - indexes.Item1, input);
            if (pos >= indexes.Item1 && pos < indexes.Item2) return pos;

            return -1; // Not found (error - all valid offset must be present in the index)
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

        public DimData(DcColumn dim)
        {
            // TODO: Check if output (greater) set is of correct type

            Dim = dim;

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
            if (dim.Input != null && dim.Input.Data != null)
            {
                Length = dim.Input.Data.Length;
            }
        }

        #endregion
    }

    /// <summary>
    /// Empty data.
    /// 
    /// </summary>
    public class DimDataEmpty : DcColumnData
    {

        #region ComColumnData interface

        protected Offset _length;
        public Offset Length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
            }
        }

        public bool AutoIndex { get; set; }
        protected bool _indexed;
        public bool Indexed { get { return _indexed; } }
        public void Reindex() { }

        public bool IsNull(Offset input) { return true; }

        public object GetValue(Offset input) { return null; }

        public void SetValue(Offset input, object value) { }
        public void SetValue(object value) { }

        public void Nullify() { }

        public void Append(object value) { }

        public void Insert(Offset input, object value) { }

        public void Remove(Offset input) { }

        public object Project(Offset[] offsets) { return null; }

        public Offset[] Deproject(object value) { return null; } // Or empty array 

        #endregion
    }

    public class ColumnDefinition : DcColumnDefinition 
    {
        protected DcColumn Dim { get; set; }

        #region ComColumnDefinition interface

        public bool IsAppendData { get; set; }

        public bool IsAppendSchema { get; set; }

        public ColumnDefinitionType DefinitionType { get; set; }

        //
        // COEL (language) representation
        //

        protected string formula;
        public String Formula 
        {
            get { return formula; }
            set
            {
                formula = value;

                ExprBuilder exprBuilder = new ExprBuilder();
                ExprNode expr = exprBuilder.Build(formula);
                FormulaExpr = expr;

                if (expr.Operation == OperationType.TUPLE)
                {
                    DefinitionType = ColumnDefinitionType.LINK;
                }
                else if (expr.Operation == OperationType.CALL && expr.Name.Equals("AGGREGATE", StringComparison.InvariantCultureIgnoreCase))
                {
                    DefinitionType = ColumnDefinitionType.AGGREGATION;
                }
                else
                {
                    DefinitionType = ColumnDefinitionType.ARITHMETIC;
                }
            }
        }

        //public AstNode FormulaAst { get; set; }

        //
        // Structured (object) representation
        //

        public ExprNode FormulaExpr { get; set; }

        public Mapping Mapping { get; set; }

        public ExprNode WhereExpr { get; set; }

        //
        // Aggregation
        //

        public DcTable FactTable { get; set; }

        public List<DimPath> GroupPaths { get; set; }

        public List<DimPath> MeasurePaths { get; set; }

        public string Updater { get; set; }

        // Aassert: FactTable.GroupFormula + ThisSet.ThisFunc = FactTable.MeasureFormula
        // Aassert: if LoopSet == ThisSet then GroupCode = null, ThisFunc = MeasureCode

        //
        // Schema/structure operations
        //

        public void Append()
        {
            if (Dim == null) return;
            if (Dim.Output == null) return;
            if (Dim.Output.IsPrimitive) return; // Primitive tables do not have structure

            if (DefinitionType != ColumnDefinitionType.AGGREGATION) return;
            if (DefinitionType != ColumnDefinitionType.ARITHMETIC) return;

            if (FormulaExpr == null) return;

            //
            // Analyze output structure of the definition and extract all tables that are used in its output
            //
            if (FormulaExpr.Result.TypeTable == null)
            {
                string outputTableName = FormulaExpr.Item.Result.TypeName;

                // Try to find this table and if found then assign to the column output
                // If not found then create output table in the schema and assign to the column output
            }

            //
            // Analyze output structure of the definition and extract all columns that are used in its output
            //
            if (FormulaExpr.Operation == OperationType.TUPLE)
            {
                foreach (var child in FormulaExpr.Children)
                {
                    string childName = child.Item.Name;
                }
            }

            // Append the columns extracted from the definition to the output set

        }

        //
        // Compute. Data operations.
        //

        // Get an object which is used to compute the function values according to the formula
        protected DcIterator GetIterator()
        {
            DcIterator evaluator = null;

            if (DefinitionType == ColumnDefinitionType.FREE) 
            {
                ; // Nothing to do
            }
            else if (Dim.Input.Schema is SchemaCsv) // Import from CSV
            {
                evaluator = new CsvIterator(Dim);
            }
            else if (Dim.Input.Schema is SchemaOledb) // Import from OLEDB
            {
                evaluator = new OledbIterator(Dim);
            }
            else if (DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                evaluator = new AggrIterator(Dim);
            }
            else if (DefinitionType == ColumnDefinitionType.ARITHMETIC)
            {
                evaluator = new ExprIterator(Dim);
            }
            else if (DefinitionType == ColumnDefinitionType.LINK)
            {
                evaluator = new ExprIterator(Dim);
            }
            else
            {
                throw new NotImplementedException("This type of column definition is not implemented.");
            }

            return evaluator;
        }

        private void EvaluateBegin() 
        {
            //
            // Open files/databases
            //
            if (Dim.Output.Schema is SchemaCsv) // Prepare to writing to a csv file during evaluation
            {
                SchemaCsv csvSchema = (SchemaCsv) Dim.Output.Schema;
                SetCsv csvOutput = (SetCsv) Dim.Output;

                // Ensure that all parameters are correct
                // Set index for all columns that have to written to the file
                int index = 0;
                for(int i = 0; i<csvOutput.Columns.Count; i++) 
                {
                    if (!(csvOutput.Columns[i] is DimCsv)) continue;

                    DimCsv col = (DimCsv)csvOutput.Columns[i];
                    if(col.IsSuper) 
                    {
                        col.ColumnIndex = -1; // Will not be written 
                    }
                    else if(col.Output.Schema != col.Input.Schema) // Import/export columns do not store data
                    {
                        col.ColumnIndex = -1;
                    }
                    else 
                    {
                        col.ColumnIndex = index;
                        index++;
                    }
                }

                // Open file for writing
                if (csvSchema.connection != null)
                {
                    csvSchema.connection.OpenWriter(csvOutput);

                    // Write header
                    if (csvOutput.HasHeaderRecord)
                    {
                        var header = csvOutput.GetColumnNamesByIndex();
                        csvSchema.connection.WriteNext(header);
                    }
                }
            }
            else if (Dim.Output.Schema is SchemaOledb) // Prepare to writing to a database during evaluation
            {
            }

            Dim.Data.AutoIndex = false;
            //Dim.Data.Nullify();
        }

        public void Evaluate()
        {
            DcIterator evaluator = GetIterator();
            if (evaluator == null) return;

            try
            {
                EvaluateBegin();

                while (evaluator.Next())
                {
                    evaluator.Evaluate();
                }
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
            finally
            {
                EvaluateEnd();
            }
        }

        private void EvaluateEnd() 
        {
            Dim.Data.Reindex();
            Dim.Data.AutoIndex = true;

            //
            // Close files/databases
            //
            if (Dim.Output.Schema is SchemaCsv)
            {
                SchemaCsv csvSchema = (SchemaCsv)Dim.Output.Schema;
                SetCsv csvOutput = (SetCsv)Dim.Output;

                // Close file
                if (csvSchema.connection != null)
                {
                    csvSchema.connection.CloseWriter();
                }
            }
            else if (Dim.Output.Schema is SchemaOledb)
            {
            }
        }

        //
        // Dependencies
        //

        public List<Dim> Dependencies { get; set; } // Other functions this function directly depends upon. Computed from the definition of this function.
        // Find and store all outputs of this function by evaluating (executing) its definition in a loop for all input elements of the fact set (not necessarily this set)

        public List<DcTable> UsesTables(bool recursive) // This element depends upon
        {
            List<DcTable> res = new List<DcTable>();

            if (DefinitionType == ColumnDefinitionType.FREE)
            {
                ;
            }
            else if (DefinitionType == ColumnDefinitionType.ANY || DefinitionType == ColumnDefinitionType.ARITHMETIC || DefinitionType == ColumnDefinitionType.LINK)
            {
                if (FormulaExpr != null) // Dependency information is stored in expression (formula)
                {
                    res = FormulaExpr.Find((DcTable)null).Select(x => x.Result.TypeTable).ToList();
                }
            }
            else if (DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                res.Add(FactTable); // This column depends on the fact table

                // Grouping and measure paths are used in this column
                if (GroupPaths != null)
                {
                    foreach (DimPath path in GroupPaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }
                if (MeasurePaths != null)
                {
                    foreach (DimPath path in MeasurePaths)
                    {
                        foreach (DcColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }
            }

            return res;
        }
        public List<DcTable> IsUsedInTables(bool recursive) // Dependants
        {
            List<DcTable> res = new List<DcTable>();

            // TODO: Which other sets use this function for their content? Say, if it is a generating function. Or it is a group/measure function.
            // Analyze other function definitions and check if this function is used there directly. 
            // If such a function has been found, then make the same call for it, that is find other functins where it is used.

            // A function can be used in Filter expression and Sort expression

            return res;
        }

        public List<DcColumn> UsesColumns(bool recursive) // This element depends upon
        {
            List<DcColumn> res = new List<DcColumn>();

            if (DefinitionType == ColumnDefinitionType.FREE)
            {
                ;
            }
            else if (DefinitionType == ColumnDefinitionType.ANY || DefinitionType == ColumnDefinitionType.ARITHMETIC || DefinitionType == ColumnDefinitionType.LINK)
            {
                if (FormulaExpr != null) // Dependency information is stored in expression (formula)
                {
                    res = FormulaExpr.Find((DcColumn)null).Select(x => x.Column).ToList();
                }
            }
            else if (DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                // Grouping and measure paths are used in this column
                if (GroupPaths != null)
                {
                    foreach (var path in GroupPaths)
                    {
                        foreach (var seg in path.Segments)
                        {
                            if (!res.Contains(seg)) res.Add(seg);
                        }
                    }
                }
                if (MeasurePaths != null)
                {
                    foreach (var path in MeasurePaths)
                    {
                        foreach (var seg in path.Segments)
                        {
                            if (!res.Contains(seg)) res.Add(seg);
                        }
                    }
                }
            }

            return res;
        }
        public List<DcColumn> IsUsedInColumns(bool recursive) // Dependants
        {
            List<DcColumn> res = new List<DcColumn>();

            // TODO: Find which other columns use this column in the definition

            return res;
        }

        #endregion

        public ColumnDefinition(DcColumn dim)
        {
            Dim = dim;

            IsAppendData = false;
            DefinitionType = ColumnDefinitionType.FREE;
            
            GroupPaths = new List<DimPath>();
            MeasurePaths = new List<DimPath>();

            Dependencies = new List<Dim>();
        }

    }

}
