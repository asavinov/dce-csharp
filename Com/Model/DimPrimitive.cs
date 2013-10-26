using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

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

        // Memory management parameters for instances (used by extensions and in future will be removed from this class).
        protected static int initialSize = 1024 * 8; // In elements
        protected static int incrementSize = 1024; // In elements

        protected int allocatedSize; // How many elements (maximum) fit into the allocated memory

        protected T NullValue; // One possible implementation of Nulls (not the best)

        protected static IAggregator<T> Aggregator;

        public DimPrimitive(string name, Set lesserSet, Set greaterSet)
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if output (greater) set is of correct type

            Length = 0;
            allocatedSize = initialSize;
            _cells = new T[allocatedSize];
            _offsets = new int[allocatedSize];

            if (IsInstantiable)
            {
                Length = lesserSet.Length;
            }

            if (typeof(T) == typeof(int))
            {
                NullValue = ObjectToGeneric(int.MaxValue);
                Aggregator = new IntAggregator() as IAggregator<T>;
            }
            else if (typeof(T) == typeof(double))
            {
                NullValue = ObjectToGeneric(double.NaN);
                Aggregator = new DoubleAggregator() as IAggregator<T>;
            }
            else
            {
                NullValue = default(T);
            }
        }

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
                if (allocatedSize < value) // Not enough storage for the new element
                {
                    allocatedSize += incrementSize * ((value - allocatedSize) / incrementSize + 1);
                    System.Array.Resize<T>(ref _cells, allocatedSize); // Resize the storage for values
                    System.Array.Resize(ref _offsets, allocatedSize); // Resize the indeex
                }

                // Update data and index in the case of increase (append to last) and decrease (delete last)
                if (value > Length)
                {
                    // !!! TODO: We actually have to append NULLs rather than 0
                    while (value > Length) AppendIndex(ObjectToGeneric(0)); // OPTIMIZE: Instead of appending individual values, write a method for appending an interval of offset (with default value)
                }
                else if (value < Length)
                {
                    // TODO: remove last elements
                }
            }
        }

        #region Manipulate function (slow). Inherited object-based interface. Not generic. 

        public override void Append(object value)
        {
            // Ensure that there is enough memory
            if (allocatedSize == Length) // Not enough storage for the new element (we need Length+1)
            {
                allocatedSize += incrementSize;
                System.Array.Resize<T>(ref _cells, allocatedSize); // Resize the storage for values
                System.Array.Resize(ref _offsets, allocatedSize); // Resize the indeex
            }

            AppendIndex(ObjectToGeneric(value));
        }

        public override void Insert(Offset offset, object value)
        {
            // TODO
        }

        public override object GetValue(Offset offset)
        {
            T cell = _cells[offset]; // We do not check the range of offset - the caller must guarantee its validity
            if (EqualityComparer<T>.Default.Equals(NullValue, cell))
                return null;
            else
                return cell;
        }

        public override void SetValue(Offset offset, object value)
        {
            if (value == null)
                UpdateIndex(offset, NullValue);
            else
                UpdateIndex(offset, ObjectToGeneric(value));
        }

        public override Offset[] GetOffsets(object value)
        {
            if (value.GetType().IsArray) return deproject(ObjectToGenericArray(value));
            else return deproject(ObjectToGeneric(value));
        }

        public override object GetValues(Offset[] offsets)
        {
            return project(offsets);
            // Returns an array. We cannot case between array types (that is, int[] is not object[]) and therefore we return object.
            // Alternatives for changing type and using array type:
            // Cast return array type T[] -> object[]
            // return (object[])Convert.ChangeType(project(offsets), typeof(object[])); // Will fail at run time in the case of wrong type
            // return project(offsets).Cast<object>().ToArray();
        }

        public override object Aggregate(object values, string function) // It is actually static but we cannot use static virtual methods in C#
        {
            if (values == null) return default(T);

            T[] array = ObjectToGenericArray(values);
            return Aggregate(array, function, Aggregator);
        } 

        #endregion

        #region Data methods

        public override void Populate()
        {
            if (SelectExpression != null) // Derived dimension the values of which have to be computed and stored
            {
                for (Offset offset = 0; offset < LesserSet.Length; offset++) // Compute the output function value for each input value (offset)
                {
                    SelectExpression.SetOutput(Operation.VARIABLE, offset); // Initialize 'this'
                    SelectExpression.Evaluate(); // Compute
                    object val = null;
                    if (SelectExpression.Operation == Operation.TUPLE)
                    {
                        val = SelectExpression.OutputSet.Find(SelectExpression);
                    }
                    else
                    {
                        val = SelectExpression.Output;
                    }
                    SetValue(offset, val); // Store the final result
                }
            }
        }

        public override void Unpopulate()
        {
            base.Unpopulate();
        }

        #endregion

        #region Index methods

        private void AppendIndex(T value)
        {
            // Append the specified element to the index. The index and the storage must have enough memory. 

            // The last element contains garbadge and is not referenced from index. 
            // The last element of index is also free and contains garbadge.

            int pos = FindIndexes(value).Item2;
            Array.Copy(_offsets, pos, _offsets, pos + 1, Length - pos); // Free an index element by shifting other elements forward

            _offsets[pos] = Length;
            _cells[Length] = value;

            _length = _length + 1;
        }

        private void UpdateIndex(int offset, T value)
        {
            // Update index when changing a single value at one offset

            // Replace an existing value with the new value and update the index. 
            int oldPos = FindIndex(offset); // Old sorted position of the cell we are going to change
            int pos = FindIndexes(value).Item2; // The new sorted position for this cell

            // Optimization: Instead of inserting after the last element with this same value, it is a good idea to position it within this interval by preserving the order of offsets (as a kind of secondary criterion). 
            // In this case all elements with the same value will have growing index in the sorted array like [3, 25, 153]. 
            // It will make some operations (with several dimensions) much more efficient by allowing for binary search for a given index (among the same value).
            // To find such a new position, we need to make binary search among indexes within the returned interval.

            if (pos > oldPos)
            {
                Array.Copy(_offsets, oldPos+1, _offsets, oldPos, (pos-1) - oldPos); // Shift backward by overwriting old
                _offsets[pos-1] = offset;
            }
            else if (pos < oldPos)
            {
                Array.Copy(_offsets, pos, _offsets, pos + 1, oldPos - pos); // Shift forward by overwriting old pos
                _offsets[pos] = offset;
            }

            _cells[offset] = value;
        }

        private Tuple<int,int> FindIndexes(T target)
        {
            // Returns an interval of indexes which all reference the specified value
            // min is inclusive and max is exclusive
            // min<max - the value is found between [min,max)
            // min=max - the value is not found, min=max is the position where it has to be inserted
            // min=length - the value has to be appended (and is not found, so min=max) 

            // C# bionary search works directly with value array. 
            // int index = Array.BinarySearch<T>(mynumbers, target);
            // BinarySearch<T>(T[], Int32, Int32, T) - search in range 

            // Implemented as binary search
            // Source: http://stackoverflow.com/questions/8067643/binary-search-of-a-sorted-array

            int mid = 0, first = 0, last = Length;

            //for a sorted array with ascending values
            while (first < last)
            {
                mid = (first + last) / 2;

                if (Comparer<T>.Default.Compare(target, _cells[_offsets[mid]]) > 0) // Less: target > mid
                {
                    first = mid+1;
                }
                else if (Comparer<T>.Default.Compare(target, _cells[_offsets[mid]]) < 0) // Greater: target < mynumbers[mid]
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

            // Now find min and max positions for the interval of equal values
            // Optimization: such search is not efficient - it is simple scan. One option would be use binary serach within interval [first, mid] and [mid, last]
            for (first = mid; first >= 0 && EqualityComparer<T>.Default.Equals(target, _cells[_offsets[first]]); first--)
                ;
            for (last = mid; last < Length && EqualityComparer<T>.Default.Equals(target, _cells[_offsets[last]]); last++) 
                ;

            return new Tuple<int, int>(first+1, last);
        }

        private int FindIndex(int offset)
        {
            // Find an index for an offset (rather than a value in this offset).
            // A value can be stored at many different offsets while one offset has always one index and therefore a single valueis returned rather than an interval.
            Tuple<int,int> indexes = FindIndexes(_cells[offset]);
            for (int i = indexes.Item1; i < indexes.Item2; i++)
            {
                if (_offsets[i] == offset) return i;
            }

            return -1;
        }

        private int FindIndex_2(T target)
        {
            int mid = -1;
            // Comparer<T> comparer = Comparer<T>.Default;
            // mid = Array.BinarySearch(_offsets, 0, _count, target, (a, b) => comparer.Compare(_cells[a], _cells[b]));

            //IComparer<T> comparer = new IndexComparer<T>(this);
            //mid = Array.BinarySearch(_offsets, 0, _count, target, comparer);

            return mid;
        }

        private void FullSort()
        {
            // Index sort in Java: http://stackoverflow.com/questions/951848/java-array-sort-quick-way-to-get-a-sorted-list-of-indices-of-an-array
            // We need it because the sorting method will change the cells. 
            // Optimization: use one global large array for that purpose
            T[] tempCells = (T[])_cells.Clone();

            // Reset offsets befroe sorting (so it will be completely new sort)
            for (int i = 0; i < Length; i++)
            {
                _offsets[i] = i; // Now each offset represents (references) an element of the function (from domain) but they are unsorted
            }

            Array.Sort<T, int>(tempCells, _offsets, 0, Length);
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

        private int[] FindOffsets(Dictionary<string, object> values)
        {
            // Return an array of offsets of elements which have the specified values

            return null;
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
            Tuple<int,int> indexes = FindIndexes(value);

            if (indexes.Item1 == indexes.Item2)
            {
                return new int[0]; // Not found
            }

            int[] result = new int[indexes.Item2 - indexes.Item1];

            for (int i = 0; i < result.Length; i++)
            {
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


    }
}
