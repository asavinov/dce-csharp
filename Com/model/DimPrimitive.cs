using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// Primitive dimension with double values (dictionary with all double values used which are identified by _offsets). 
    /// </summary>
    public class DimPrimitive<T> : Dimension
    {
        // Alternative: keep values always sorted while the iondex will store the original positions of these sorted values. Advantage: use of the build-in sort, search and other algorithms
        private T[] _cells; // Each cell contains a T value in arbitrary order
        private int[] _offsets; // Each cell contains an offset to an element in cells in ascending or descending order

        // Memory management parameters for instances (used by extensions and in future will be removed from this class).
        protected static int initialSize = 1024 * 8; // In elements
        protected static int incrementSize = 1024; // In elements

        protected int allocatedSize; // How many elements (maximum) fit into the allocated memory

        public DimPrimitive(string name, Set lesserSet, Set greaterSet)
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if output (greater) set is of correct type

            // In fact, we know the input set size and hence can allocate the exact number of elements in the array
            _count = 0;
            allocatedSize = initialSize;
            _cells = new T[allocatedSize];
            _offsets = new int[allocatedSize];
        }

        public override int Size // sizeof(T) does not work for generic classes (even if constrained by value types)
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

        #region Manipulate function: add, edit, delete, update values etc.

        public T GetValue(int offset)
        {
            return _cells[offset]; // We do not check the range of offset - the caller must guarantee its validity
        }

        public void SetValue(int offset, T value)
        {
            // Update direct function by setting output value for the input offset
            _cells[offset] = value; // We do not check the range of offset - the caller must guarantee its validity

            // TODO: Update reverse function (reindex)
            Sort(); // Alternative: update only the changed element
        }

        public void AppendValue(T value)
        {
            // Ensure that there is enough memory
            if (allocatedSize == Count) // Not enough storage for the new element
            {
                allocatedSize += incrementSize;
                System.Array.Resize<T>(ref _cells, allocatedSize); // Resize the storage for values
                System.Array.Resize(ref _offsets, allocatedSize); // Resize the indeex
            }

            _cells[_count - 1] = value; // Assign the value to the new offset
            _offsets[_count - 1] = _count - 1; // This element has to be moved to correct position in this array during sorting

            Sort(); // Alternative: UpdateSortLast()
        }

        #endregion

        #region Project and de-project

        public T project(int offset)
        {
		    return _cells[offset];
	    }

	    public T[] project(int[] offsets) {
		    int[] result = new int[offsets.Length];
		    int resultSize = 0;
		    for(int i=0; i<offsets.Length; i++) {
			    // Check if it exists already 
			    // Optimization is needed: 
			    // 1. Sorting during projection (and hence easy to find duplicates), 
			    // 2. Maintain own (local) index (e.g., sorted elements but separate from projection buing built) 
			    // 3. Use de-projection
			    // 4. In many cases the _offsets can be already sorted (if selected from a sorted list)
			    int cell = offsets[i];
			    bool found=false;
			    for(int j=0; j<resultSize; j++) {
				    if(result[j] == cell) {
					    found = true;
					    break; // Found 
				    }
			    }
			    if(found) break;
			    // Append new cell
			    offsets[resultSize] = cell;
			    resultSize++;
		    }
            return null; // Arrays.copyOf(result, resultSize);
	    }
	
	    public int[] deproject(T cell) {
		    // Binary search on _offsets
		    // Inefficient approach by simply scanning
		    int[] result = new int[_cells.Length];
		    int resultSize = 0;
		    for(int i=0; i<_cells.Length; i++) {
			    if(!_cells[i].Equals(cell)) continue;
			    result[resultSize] = i;
			    resultSize++;
		    }

            return null; // Arrays.copyOf(result, resultSize);
	    }

	    public int[] deproject(T[] values) {
		    return null;
	    }

	    public int[] deproject(String expression) {
		    return null;
	    }

        #endregion

        #region Sorting methods

        private void Sort()
        {
            // We need it because the sorting method will change the cells. 
            // Optimization: use one global large array for that purpose
            T[] tempCells = (T[])_cells.Clone();

            // Reset offsets befroe sorting (so it will be completely new sort)
            for (int i = 0; i < _count; i++)
            {
                _offsets[i] = i; // Now each offset represents (references) an element of the function (from domain) but they are unsorted
            }

            Array.Sort<T, int>(tempCells, _offsets, 0, _count); 
            // Now offsets are sorted
        }

        private void UpdateSortLast()
        {
            T target = _cells[_count-1];

            int pos = BinarySearch(target);
            Array.Copy(_offsets, pos, _offsets, pos + 1, _count - pos - 1); // Free an index element by shifting other elements

            _offsets[pos] = _count - 1;
        }

        private int BinarySearch(T target)
        {
            // Essentially, it is deproject one value

            // C# bionary search works directly with value array. 
            // int index = Array.BinarySearch<T>(mynumbers, target);
            // BinarySearch<T>(T[], Int32, Int32, T) - search in range 

            // Algorithm here: http://stackoverflow.com/questions/8067643/binary-search-of-a-sorted-array

            int mid = -1, first = 0, last = _count - 1;
            bool found = false;

            //for a sorted array with descending values
            while (!found && first <= last)
            {
                mid = (first + last) / 2;

                if (Comparer<T>.Default.Compare(target, _cells[_offsets[mid]]) < 0) // Less: target < mid
                {
                    first = mid + 1;
                }

                if (Comparer<T>.Default.Compare(target, _cells[_offsets[mid]]) > 0) // Greater: target > mynumbers[mid]
                {
                    last = mid - 1;
                }

                else
                {
                    // You need to stop here once found or it's an infinite loop once it finds it.
                    found = true;
                }
            }

            return mid;
        }

        #endregion
    }
}
