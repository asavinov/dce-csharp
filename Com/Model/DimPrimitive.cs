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
    public class DimPrimitive<T> : Dim
    {
        private T[] _cells; // Each cell contains a T value in arbitrary original order
        private int[] _offsets; // Each cell contains an offset to an element in cells in ascending or descending order

        private int _nullCount; // Nulls are stored in the beginning of array of indexes (that is, treated as absolute minimum)
        private T _nullValue; // It is what is written in cell instead of null if null is not supported by the type. If null is supported then null is stored (instead, we can use _nullValue=null).

        // Memory management parameters for instances (used by extensions and in future will be removed from this class).
        protected static int initialSize = 1024 * 8; // In elements
        protected static int incrementSize = 1024; // In elements

        protected int allocatedSize; // How many elements (maximum) fit into the allocated memory

        protected static IAggregator<T> Aggregator;

        public override Type SystemType
        {
            get { return typeof(T); }
        }

        public override int Width // sizeof(T) does not work for generic classes (even if constrained by value types)
        {
            get 
            { 
                Type tt = typeof(T);
                int size;
                if (tt.IsValueType)
                    if (tt.IsGenericType)
                    {
                        var t = default(T);
                        size = System.Runtime.InteropServices.Marshal.SizeOf(t);
                    }
                    else
                    {
                        size = System.Runtime.InteropServices.Marshal.SizeOf(tt);
                    }
                else
                {
                    size = IntPtr.Size;
                }
                return size;
            }
        }

        protected Offset _length; // It is only used if lesser set is not set, that is, for hanging dimension (theoretically, we should not use hanging dimensions and do not need then this field)
        public override Offset Length
        {
            get
            {
                return _length;
            }
            protected set
            {
                if (value == Length) return;

                // Ensure that there is enough memory
                if (value > allocatedSize) // Not enough storage for the new element
                {
                    allocatedSize += incrementSize * ((value - allocatedSize) / incrementSize + 1);
                    System.Array.Resize<T>(ref _cells, allocatedSize); // Resize the storage for values
                    System.Array.Resize(ref _offsets, allocatedSize); // Resize the indeex
                }

                // Update data and index in the case of increase (append to last) and decrease (delete last)
                if (value > Length)
                {
                    while (value > Length) Append(null); // OPTIMIZE: Instead of appending individual values, write a method for appending an interval of offset (with default value)
                }
                else if (value < Length)
                {
                    // TODO: remove last elements
                }
            }
        }

        #region Manipulate function (slow). Inherited object-based interface. Not generic. 

        public override bool IsNull(Offset offset)
        {
            // For non-nullable storage, use the index to find if this cell is in the null interval of the index (beginning)
            int pos = FindIndex(offset);
            return pos < _nullCount;
            // For nullable storage: simply check the value (actually this method is not needed for nullable storage because the user can compare the values returned from GetValue)
            // return EqualityComparer<T>.Default.Equals(_nullValue, _cells[offset]);
        }

        public override object GetValue(Offset offset)
        {
            return _cells[offset]; // We do not check the range of offset - the caller must guarantee its validity
        }

        public override void SetValue(Offset offset, object value) // Replace an existing value with the new value and update the index. 
        {
            T val = default(T);
            int oldPos = FindIndex(offset); // Old sorted position of the cell we are going to change
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
            pos = Array.BinarySearch(_offsets, interval.Item1, interval.Item2 - interval.Item1, offset);
            if (pos < 0) pos = ~pos;

            if (pos > oldPos)
            {
                Array.Copy(_offsets, oldPos + 1, _offsets, oldPos, (pos - 1) - oldPos); // Shift backward by overwriting old
                _offsets[pos - 1] = offset;
            }
            else if (pos < oldPos)
            {
                Array.Copy(_offsets, pos, _offsets, pos + 1, oldPos - pos); // Shift forward by overwriting old pos
                _offsets[pos] = offset;
            }

            _cells[offset] = val;
        }

        public override void UpdateValue(Offset offset, object value, ValueOp updater) // Change the existing value by applying the specified operation. 
        {
            if(updater == null) 
            { 
                SetValue(offset, value);
                return;
            }

            if (updater.OpType == ValueOpType.CALL_PROC)
            {
                double currentValue = Convert.ToDouble(GetValue(offset));
                double doubleValue = Convert.ToDouble(value);
                switch (updater.Name)
                {
                    case "+": { currentValue += doubleValue; break; }
                    // TODO: Other operations.
                    // OPTIMIZATION: It is very inefficient. We have to think about direct implementations for each primitive type resolved before the main loop
                    // It can a special procedure for each primitive type which makes takes as a parameter group-measure-count-this functions but is implemented for one primitive type.
                    // In this case, we always assume that group-measure functions are already pre-computed and then aggregation is reduced to such a procedure where the loop is implemented for each type directly on the array (cell) using arithmetic operatinos
                }
                SetValue(offset, currentValue);
            }
        }

        public override void NullifyValues() // Reset values and index to initial state (all nulls)
        {
            throw new NotImplementedException();
        }

        public override void Eval() // Compute the output values of this function according to the definition and store them for direct access
        {
            // TODO: Turn off indexing (sorting) and index after populating all values

            bool isAggregated = false;
            if (LoopSet != null && LoopSet != LesserSet)
            {
                Debug.Assert(LoopSet.IsLesser(LesserSet), "Wrong use: the fact set must be less than this set for aggregation.");
                isAggregated = true;
            }

            if (isAggregated)
            {
                for (Offset offset = 0; offset < LoopSet.Length; offset++) // Generate all input elements (surrogates)
                {
                    // TODO: Set 'this' parameter in the context (should be resolved in advance)
                    MeasureCode.Eval(); // Evaluate the expression (what if it has multiple statements and where is its context?)

                    // TODO: Set 'this' parameter in the context (should be resolved in advance)
                    GroupCode.Eval(); // Evalute the group

                    Offset group = (Offset)GroupCode.Value;
                    UpdateValue(group, MeasureCode.Value, AccuCode); // Update the final result

                    // TODO: Increment group counts function
                }
            }
            else
            {
                for (Offset offset = 0; offset < LesserSet.Length; offset++) // Generate all input elements (surrogates)
                {
                    // TODO: Set 'this' parameter in the context (should be resolved in advance)
                    MeasureCode.Eval(); // Evaluate the expression (what if it has multiple statements and where is its context?)
                    SetValue(offset, MeasureCode.Value); // Store the final result
                }
            }

            // TODO: Turn on indexing (sorting) and index the whole function
        }

        public override void ComputeValues()
        {
            if (Mapping != null)
            {
                Debug.Assert(Mapping.SourceSet == LesserSet && Mapping.TargetSet == GreaterSet, "Wrong use: the mapping source and target sets have to corresond to the dimension sets.");

                // Build a function from the mapping for populating a mapped dimension (type change or new mapped dimension)
                Expression tupleExpr = Mapping.GetTargetExpression(this);

                var funcExpr = ExpressionScope.CreateFunctionDeclaration(Name, LesserSet.Name, GreaterSet.Name);
                funcExpr.Statements[0].Input = tupleExpr; // Return statement
                funcExpr.ResolveFunction(LesserSet.Top);
                funcExpr.Resolve();

                SelectExpression = funcExpr;
            }

            if (SelectExpression != null) // Function definition the values of which have to be computed and stored
            {
                Debug.Assert(SelectExpression.Operation == Operation.FUNCTION, "Wrong use: derived function has to be FUNCTION expression.");
                Debug.Assert(SelectExpression.Input != null, "Wrong use: derived function must have Input representing this argument.");
                Debug.Assert(SelectExpression.Input.Operation == Operation.PARAMETER, "Wrong use: derived function Input has to be of PARAMETER type.");
                Debug.Assert(SelectExpression.Input.Dimension != null, "Wrong use: derived function Input has to reference a valid variable.");

                for (Offset offset = 0; offset < LesserSet.Length; offset++) // Compute the output function value for each input value (offset)
                {
//                    SelectExpression.SetOutput(Operation.PARAMETER, offset);
                    SelectExpression.Input.Dimension.Value = offset; // Initialize 'this'
                    SelectExpression.Evaluate(); // Compute
                    SetValue(offset, SelectExpression.Output); // Store the final result
/*
                    object val = null;
                    if (SelectExpression.Operation == Operation.TUPLE)
                    {
                        SelectExpression.OutputSet.Find(SelectExpression);
                    }
                    val = SelectExpression.Output;
                    SetValue(offset, val); // Store the final result
*/
                }
            }
        }

        public override void Append(object value)
        {
            // Ensure that there is enough memory
            if (allocatedSize == Length) // Not enough storage for the new element (we need Length+1)
            {
                allocatedSize += incrementSize;
                System.Array.Resize<T>(ref _cells, allocatedSize); // Resize the storage for values
                System.Array.Resize(ref _offsets, allocatedSize); // Resize the indeex
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

            Array.Copy(_offsets, pos, _offsets, pos + 1, Length - pos); // Free an index element by shifting other elements forward

            _cells[Length] = val;
            _offsets[pos] = Length;
            _length = _length + 1;
        }

        public override void Insert(Offset offset, object value)
        {
            throw new NotImplementedException();
        }

        public override object Aggregate(object values, string function) // It is actually static but we cannot use static virtual methods in C#
        {
            if (values == null) return default(T);

            T[] array = ObjectToGenericArray(values);
            return Aggregate(array, function, Aggregator);
        }

        public override object ProjectValues(Offset[] offsets)
        {
            return project(offsets);
            // Returns an array but we delcare it as object (rather than array of objects) because we cannot case between array types (that is, int[] is not object[]) and therefore we return object.
            // Alternatives for changing type and using array type:
            // Cast return array type T[] -> object[]
            // return (object[])Convert.ChangeType(project(offsets), typeof(object[])); // Will fail at run time in the case of wrong type
            // return project(offsets).Cast<object>().ToArray();
        }

        public override Offset[] DeprojectValue(object value)
        {
            if (value == null || !value.GetType().IsArray)
            {
                return deproject(ObjectToGeneric(value));
            }
            else
            {
                return deproject(ObjectToGenericArray(value));
            }
        }

        #endregion

        #region Index methods

        private int FindIndex(int offset) // Find an index for an offset of a cell (rather than a value in this cell)
        {
            // A value can be stored at many different offsets while one offset has always one index and therefore a single valueis returned rather than an interval.

            // First, we try to find it in the null interval
            int pos = Array.BinarySearch(_offsets, 0, _nullCount, offset);
            if (pos >= 0 && pos < _nullCount) return pos; // It is null
            
            // Second, try to find it as a value (find the value area and then find the offset in the value interval)
            Tuple<int,int> indexes = FindIndexes(_cells[offset]);
            pos = Array.BinarySearch(_offsets, indexes.Item1, indexes.Item2 - indexes.Item1, offset);
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

        #endregion

        #region Project and de-project

        public T project(int offset)
        {
            return _cells[offset];
        }

        public T[] project(int[] offsets)
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

        public int[] deproject(T value)
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

        public int[] deproject(T[] values)
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

        public static T Aggregate<T, TProcessor>(T[] values, string function, TProcessor proc) where TProcessor : IAggregator<T>
        {
            switch (function)
            {
                case "SUM": return proc.Sum(values);
                case "AVG": return proc.Avg(values);
                default: throw new Exception("There is no such aggregation operation.");
            }
        }

        #endregion

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

        public DimPrimitive(string name, Set lesserSet, Set greaterSet)
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if output (greater) set is of correct type

            Length = 0;
            allocatedSize = initialSize;
            _cells = new T[allocatedSize];
            _offsets = new int[allocatedSize];

            _nullCount = Length;

            if (IsInstantiable)
            {
                Length = lesserSet.Length;
            }

            // Initialize what representative value will be used instead of nulls
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
            else
            {
                // Check if type is nullable: http://stackoverflow.com/questions/374651/how-to-check-if-an-object-is-nullable
                _nullValue = default(T);
            }
        }

    }
}
