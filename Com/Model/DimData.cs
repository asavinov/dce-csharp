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
    public class DimData<T> : ComColumnData
    {
        protected ComColumn Dim { get; set; }

        private T[] _cells; // Each cell contains a T value in arbitrary original order
        private int[] _offsets; // Each cell contains an offset to an element in cells in ascending or descending order

        private int _nullCount; // Nulls are stored in the beginning of array of indexes (that is, treated as absolute minimum)
        private T _nullValue; // It is what is written in cell instead of null if null is not supported by the type. If null is supported then null is stored (instead, we can use _nullValue=null).

        // Memory management parameters for instances (used by extensions and in future will be removed from this class).
        protected static int initialSize = 1024 * 8; // In elements
        protected static int incrementSize = 1024; // In elements

        protected int allocatedSize; // How many elements (maximum) fit into the allocated memory

        protected bool _autoindex = true; // If true then index will be automatically maintained. If false then indexing has to be done manually.

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

        public bool IsNull(Offset input)
        {
            // For non-nullable storage, use the index to find if this cell is in the null interval of the index (beginning)
            int pos = FindIndex(input);
            return pos < _nullCount;
            // For nullable storage: simply check the value (actually this method is not needed for nullable storage because the user can compare the values returned from GetValue)
            // return EqualityComparer<T>.Default.Equals(_nullValue, _cells[offset]);
        }

        public object GetValue(Offset input)
        {
            return _cells[input]; // We do not check the range of offset - the caller must guarantee its validity
        }

        public void SetValue(Offset input, object value) // Replace an existing value with the new value and update the index. 
        {
            T val = default(T);
            int oldPos = FindIndex(input); // Old sorted position of the cell we are going to change
            Tuple<int,int> interval;
            int pos = -1; // New sorted position for this cell

            if (value == null)
            {
                val = _nullValue;
                interval = new Tuple<int,int>(0, _nullCount);

                if (oldPos >= _nullCount) _nullCount++; // If old value is not null, then increase the number of nulls
            }
            else
            {
                val = ObjectToGeneric(value);
                interval = FindIndexes(val);

                if (oldPos < _nullCount) _nullCount--; // If old value is null, then decrease the number of nulls
            }

            // Find sorted position within this value interval (by increasing offsets)
            pos = Array.BinarySearch(_offsets, interval.Item1, interval.Item2 - interval.Item1, input);
            if (pos < 0) pos = ~pos;

            if (pos > oldPos)
            {
                Array.Copy(_offsets, oldPos + 1, _offsets, oldPos, (pos - 1) - oldPos); // Shift backward by overwriting old
                _offsets[pos - 1] = input;
            }
            else if (pos < oldPos)
            {
                Array.Copy(_offsets, pos, _offsets, pos + 1, oldPos - pos); // Shift forward by overwriting old pos
                _offsets[pos] = input;
            }

            _cells[input] = val;
        }
        public void SetValue(object value)
        {
            if (value == null)
            {
                Nullify();
                return;
            }

            T val = ObjectToGeneric(value);
            for (Offset i = 0; i < _length; i++)
            {
                _cells[i] = val;
                _offsets[i] = i;
            }

            _nullCount = 0;
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
            Tuple<int, int> interval;
            int pos = -1;

            if (value == null)
            {
                val = _nullValue;
                interval = new Tuple<int, int>(0, _nullCount);
                _nullCount++;
            }
            else
            {
                val = ObjectToGeneric(value);
                interval = FindIndexes(val);
            }

            pos = interval.Item2; // New value has the largest offset and hence is inserted after the end of the interval of values

            Array.Copy(_offsets, pos, _offsets, pos + 1, _length - pos); // Free an index element by shifting other elements forward

            _cells[_length] = val;
            _offsets[pos] = _length;
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
                return deprojectValue(ObjectToGeneric(value));
            }
            else
            {
                return deproject(ObjectToGenericArray(value));
            }
        }

        #endregion

        #region Protected data methods (index, sorting, projecting etc.)

        public object Aggregate(object values, string function) // It is actually static but we cannot use static virtual methods in C#
        {
            if (values == null) return default(T);

            T[] array = ObjectToGenericArray(values);
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

        private void FullSort()
        {
            // Index sort in Java: http://stackoverflow.com/questions/951848/java-array-sort-quick-way-to-get-a-sorted-list-of-indices-of-an-array
            // We need it because the sorting method will change the cells. 
            // Optimization: use one global large array for that purpose
            T[] tempCells = (T[])_cells.Clone();

            // Reset offsets befroe sorting (so it will be completely new sort)
            for (int i = 0; i < _length; i++)
            {
                _offsets[i] = i; // Now each offset represents (references) an element of the function (from domain) but they are unsorted
            }

            Array.Sort<T, int>(tempCells, _offsets, 0, _length);
            // Now offsets are sorted and temp array can be deleted
        }

        private void FullSort_2()
        {
            // Here the idea is to sort the index (that is, precisely what we need) by defining a custom comparare via cells referenced by the index elements
            // Source: http://stackoverflow.com/questions/659866/is-there-c-sharp-support-for-an-index-based-sort
            // http://www.csharp-examples.net/sort-array/
            Comparer<T> comparer = Comparer<T>.Default;
            Array.Sort(_offsets, /* 0, _count, */ (a, b) => comparer.Compare(_cells[a], _cells[b]));
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

        protected T ObjectToGeneric(object value)
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

        protected T[] ObjectToGenericArray(object values)
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
                return new T[] { ObjectToGeneric(values) };
            }

            // Alternatives:
            // return values.Cast<T>().ToArray(); 
            // Array.ConvertAll<object, T>(values, Convert.ToChar);
        }

        #endregion

        #region Constructors

        public DimData(ComColumn dim)
        {
            // TODO: Check if output (greater) set is of correct type

            Dim = dim;

            _length = 0;
            allocatedSize = initialSize;
            _cells = new T[allocatedSize];
            _offsets = new int[allocatedSize];

            _nullCount = Length;

            Length = 0;
            if (dim.Input != null && dim.Input.Data != null)
            {
                Length = dim.Input.Data.Length;
            }

            // Initialize what representative value will be used instead of nulls
            _nullValue = default(T); // Check if type is nullable: http://stackoverflow.com/questions/374651/how-to-check-if-an-object-is-nullable
            Type type = typeof(T);
            if (type == typeof(int))
            {
                _nullValue = ObjectToGeneric(int.MinValue);
                Aggregator = new IntAggregator() as IAggregator<T>;
            }
            else if (type == typeof(double))
            {
                _nullValue = ObjectToGeneric(double.NaN);
                Aggregator = new DoubleAggregator() as IAggregator<T>;
            }
            else if (type == typeof(decimal))
            {
                _nullValue = ObjectToGeneric(decimal.MinValue);
                Aggregator = new DecimalAggregator() as IAggregator<T>;
            }
            else if (!type.IsValueType) // Reference type
            {
                _nullValue = default(T);
            }
            else if (Nullable.GetUnderlyingType(type) != null) // Nullable<T> (like int?)
            {
                _nullValue = default(T);
            }

        }

        #endregion
    }

    /// <summary>
    /// Empty data.
    /// 
    /// </summary>
    public class DimDataEmpty : ComColumnData
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

    public class ColumnDefinition : ComColumnDefinition 
    {
        protected ComColumn Dim { get; set; }

        #region ComColumnDefinition interface

        public bool IsAppendData { get; set; }

        public bool IsAppendSchema { get; set; }

        public ColumnDefinitionType DefinitionType { get; set; }

        public AstNode FormulaAst { get; set; }

        public ExprNode FormulaExpr { get; set; }

        public Mapping Mapping { get; set; }

        public ExprNode WhereExpr { get; set; }

        //
        // Aggregation
        //

        public ComTable FactTable { get; set; }

        public List<DimPath> GroupPaths { get; set; }

        public List<DimPath> MeasurePaths { get; set; }

        public string Updater { get; set; }

        // Aassert: FactTable.GroupFormula + ThisSet.ThisFunc = FactTable.MeasureFormula
        // Aassert: if LoopSet == ThisSet then GroupCode = null, ThisFunc = MeasureCode

        //
        // Compute
        //

        public ComEvaluator GetEvaluator()
        {
            ComEvaluator evaluator = null;

            if (DefinitionType == ColumnDefinitionType.FREE) 
            {
                ; // Nothing to do
            }
            else if (Dim.Input.Schema != Dim.Output.Schema && Dim.Input.Schema is SetTopCsv) // Import data from a remote source
            {
                evaluator = new CsvEvaluator(Dim);
            }
            else if (Dim.Input.Schema != Dim.Output.Schema && Dim.Input.Schema is SetTopOledb) // Import data from a remote source
            {
                evaluator = new OledbEvaluator(Dim);
            }
            else if (DefinitionType == ColumnDefinitionType.AGGREGATION)
            {
                evaluator = new AggrEvaluator(Dim);
            }
            else if (DefinitionType == ColumnDefinitionType.ARITHMETIC)
            {
                evaluator = new ExprEvaluator(Dim);
            }
            else if (DefinitionType == ColumnDefinitionType.LINK)
            {
                evaluator = new ExprEvaluator(Dim);
            }
            else
            {
                throw new NotImplementedException("This type of column definition is not implemented.");
            }

            return evaluator;
        }

        public void Initialize() { }

        public void Evaluate()
        {
            ComEvaluator evaluator = GetEvaluator();
            if (evaluator == null) return;

            while (evaluator.Next())
            {
                evaluator.Evaluate();
            }
        }

        public void Finish() { }

        //
        // Dependencies
        //

        public List<Dim> Dependencies { get; set; } // Other functions this function directly depends upon. Computed from the definition of this function.
        // Find and store all outputs of this function by evaluating (executing) its definition in a loop for all input elements of the fact set (not necessarily this set)

        public List<ComTable> UsesTables(bool recursive) // This element depends upon
        {
            List<ComTable> res = new List<ComTable>();

            if (DefinitionType == ColumnDefinitionType.FREE)
            {
                ;
            }
            else if (DefinitionType == ColumnDefinitionType.ANY || DefinitionType == ColumnDefinitionType.ARITHMETIC || DefinitionType == ColumnDefinitionType.LINK)
            {
                if (FormulaExpr != null) // Dependency information is stored in expression (formula)
                {
                    res = FormulaExpr.Find((ComTable)null).Select(x => x.Result.TypeTable).ToList();
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
                        foreach (ComColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }
                if (MeasurePaths != null)
                {
                    foreach (DimPath path in MeasurePaths)
                    {
                        foreach (ComColumn seg in path.Segments)
                        {
                            if (!res.Contains(seg.Output)) res.Add(seg.Output);
                        }
                    }
                }
            }

            return res;
        }
        public List<ComTable> IsUsedInTables(bool recursive) // Dependants
        {
            List<ComTable> res = new List<ComTable>();

            // TODO: Which other sets use this function for their content? Say, if it is a generating function. Or it is a group/measure function.
            // Analyze other function definitions and check if this function is used there directly. 
            // If such a function has been found, then make the same call for it, that is find other functins where it is used.

            // A function can be used in Filter expression and Sort expression

            return res;
        }

        public List<ComColumn> UsesColumns(bool recursive) // This element depends upon
        {
            List<ComColumn> res = new List<ComColumn>();

            if (DefinitionType == ColumnDefinitionType.FREE)
            {
                ;
            }
            else if (DefinitionType == ColumnDefinitionType.ANY || DefinitionType == ColumnDefinitionType.ARITHMETIC || DefinitionType == ColumnDefinitionType.LINK)
            {
                if (FormulaExpr != null) // Dependency information is stored in expression (formula)
                {
                    res = FormulaExpr.Find((ComColumn)null).Select(x => x.Column).ToList();
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
        public List<ComColumn> IsUsedInColumns(bool recursive) // Dependants
        {
            List<ComColumn> res = new List<ComColumn>();

            // TODO: Find which other columns use this column in the definition

            return res;
        }

        #endregion

        public ColumnDefinition(ComColumn dim)
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
