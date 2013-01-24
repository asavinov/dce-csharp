using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    /// <summary>
    /// Integer cells.
    /// </summary>
    public class Dim : DimAbstract
    {
	    private int[] _cells;
        private int[] _offsets;

	    public Dim(string name, Concept lesserSet, Concept greaterSet) 
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

	    /**
	     * Project one value.
	     * A sequence of bytes is a physical type has to be converted into the target data type.
	     * Here there is no check of the Length which means that every implementation makes some assumptions about the output data type. 
	     * The result can be null. 
	     */
	    public int project(int offset) {
		    return _cells[offset]; // Java array index is int so long is not supported
	    }

	    /**
	     * Project many values. 
	     * The result can be shorter because it is a projection.
	     * Problem/questions: maybe it is better to return also a ComFunc as a result of projection/de-projection rather than a native object? 
	     */
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

	    /**
	     * De-project one value, that is, find and return _offsets where this one value is stored.
	     * The result can be null if this value is not referenced. 
	     * Bytes represent a physical value which has to be converted into the target data type. 
	     */
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

	    /**
	     * De-project many values. 
	     * The result can be null if this value is not referenced. 
	     * It returns all _offsets referencing these values.
	     */
	    public int[] deproject(int[] values) {
		    return null;
	    }

	    /** 
	     * De-projection using condition (predicate). 
	     * The condition is primitive and is applied directly to the cell values (so it is not a query).
	     * Some more complex conditions are possible but only if they are easy to implement (and more efficient than combining the results of individual Operations). 
	     * Important for primitive but can be also defined on other dimensions. 
	     */
	    public int[] deproject(String expression) {
		    return null;
	    }

    }
}
