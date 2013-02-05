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
        private T[] _cells; // Each cell contains a T value in arbitrary order
        private int[] _offsets; // Each cell contains an offset to an element in cells in ascending or descending order

        public DimPrimitive(string name, Set lesserSet, Set greaterSet)
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if output (greater) set is of correct type

            // In fact, we know the input set size and hence can allocate the exact number of elements in the array
            allocatedSize = lesserSet.InstanceCount;
            _cells = new T[allocatedSize];
            _offsets = new int[allocatedSize];
        }

        // Memory management parameters for instances (used by extensions and in future will be removed from this class).
        protected static int initialSize = 1024 * 8; // In elements
        protected static int incrementSize = 1024; // In elements

        protected int allocatedSize; // How many elements (maximum) fit into the allocated memory

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
            _offsets[offset] = offset;
        }

        public int AddValue(T value)
        {
            // In fact, an element can be added only to all dimensions of the lesser set and dimensions do not deal with set elements. 
            // A dimension is a function, so we need to implement it as a function, that is, set an output to some specific input. Allocating new elements in the domain (and in the function) is already another method. 
            
            // Ensure that there is enough memory
            if (allocatedSize == Count)
            {
                allocatedSize += incrementSize;

                T[] newCells = new T[allocatedSize];
//                System.arraycopy(_cells, 0, newCells, 0, _cells.Length); // Alternatively, Arrays.copyOf(result, resultSize);
                _cells = newCells;

                int[] newOffsets = new int[allocatedSize];
//                System.arraycopy(_offsets, 0, newOffsets, 0, _offsets.Length); // Alternatively, Arrays.copyOf(result, resultSize);
//                _offsets = newOffsets;
            }

            // Assign the value to the new offset
            _cells[Count] = value;

            // TODO: Update reverse function (reindex)
            _offsets[Count] = Count;

            // Return the new offset
            return Count + 1;
        }

        public int AddOutput(T[] values)
        {
            return _cells.Length + values.Length;
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
    }
}
