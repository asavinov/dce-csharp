using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// A special implementation of int-int mapping with the following features:
    /// - only one dimension with special meaning
    /// - this dimension supports one-to-one mapping (project and then de-project produces the same elements) 
    /// - projection has the same size as input 
    /// - de-projection are smaller because not all elements are referenced 
    /// </summary>
    public class DimIntFilter : Dimension
    {
        private int[] _cells; // Index is offset in the input set. Cell is the offset in the output set. If two filtered elements: _cells[0]=2000, _cells[1]=1000
        private int[] _offsets; // Index is offset in the output set. Cell is the offset in the input set. If two filtered elements: _offsets[0]=1, _offsets[0]=0

	    public DimIntFilter(string name, Set lesserSet, Set greaterSet)
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if output is of correct type
		
		    // In fact, we know the input set size and hence can allocate the exact number of elements in the array
		    allocatedSize = initialSize;
		    _cells = new int[initialSize];
		    _offsets = new int[initialSize];
	    }

        //
        // Manipulate function: add, edit, delete, update etc.
        //

        public int AddOutput(int output)
        {
            // Ensure that there is enough memory
            if (GreaterSet.InstanceCount == allocatedSize)
            {
                allocatedSize += incrementSize;

                int[] newCells = new int[allocatedSize];
//                System.arraycopy(_cells, 0, newCells, 0, _cells.Length); // Alternatively, Arrays.copyOf(result, resultSize);
                _cells = newCells;

                int[] newOffsets = new int[allocatedSize];
//                System.arraycopy(_offsets, 0, newOffsets, 0, _offsets.Length); // Alternatively, Arrays.copyOf(result, resultSize);
                _offsets = newOffsets;
            }

            // Assign the value to the new offset
            _cells[GreaterSet.InstanceCount] = output;

            // Return the new offset
            return GreaterSet.InstanceCount + 1;
        }

        public int AddOutput(int[] output)
        {
            return _cells.Length + output.Length;
        }

        public void SetOutput(int input, int output)
        {
            // Update direct function
            _cells[input] = output;

            // Update reverse function
            _offsets[input] = input;
        }

        //
	    // Project
	    //

	    public int project(int offset) {
		    return _cells[offset];
	    }

	    // Projection has always the same size as input 
	    public int[] project(int[] offsets) {
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
	
	    //
	    // De-project
	    //

	    // Either offset or null.
	    public int[] deproject(int cell) {
		    // Binary search on _offsets
		    // Inefficient approach by simply scanning
		    int[] result = new int[_cells.Length];
		    int resultSize = 0;
		    for(int i=0; i<_cells.Length; i++) {
			    if(_cells[i] != cell) continue;
			    result[resultSize] = i;
			    resultSize++;
		    }

            return null; // Arrays.copyOf(result, resultSize);
	    }

	    // De-projection is smaller because not all elements are referenced
	    public int[] deproject(int[] values) {
		    return null;
	    }

    }
}
