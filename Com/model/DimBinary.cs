using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.model
{
    public class DimBinary : DimAbstract
    {
	    private bool[] _cells;
	    private int[] _offsets;

	    public DimBinary(string name, Concept lesserSet, Concept greaterSet) 
            : base(name, lesserSet, greaterSet)
        {
            // TODO: Check if output is of correct type
		
		    // In fact, we know the input set size and hence can allocate the exact number of elements in the array
		    allocatedSize = initialSize;
		    _cells = new bool[initialSize];
		    _offsets = new int[initialSize];
	    }

        //
        // Manipulate function: add, edit, delete, update etc.
        //

        public int AddOutput(bool output)
        {
            // Ensure that there is enough memory
            if (GreaterSet.InstanceCount == allocatedSize)
            {
                allocatedSize += incrementSize;

                bool[] newCells = new bool[allocatedSize];
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

        public int AddOutput(bool[] output)
        {
            return _cells.Length + output.Length;
        }

        public void SetOutput(int input, bool output)
        {
            // Update direct function
            _cells[input] = output;

            // Update reverse function
            _offsets[input] = input;
        }

        //
	    // Project
	    //

	    public bool project(int offset) {
		    return _cells[offset];
	    }

	    public bool[] project(int[] offsets) {
		    // In fact, we need to find out whether the input set contains at least one false and at least one true value. Full scan is performed only if no false and no true values were found. 
		
		    bool[] result = new bool[offsets.Length]; // =2
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
				    if(true /* result[j] == cell */) {
					    found = true;
					    break; // Found 
				    }
			    }
			    if(found) break;
			    // Append new cell
			    offsets[resultSize] = cell;
			    resultSize++;
		    }
            return null; //  Arrays.copyOf(result, resultSize);
	    }
	
	    //
	    // De-project
	    //

	    public int[] deproject(bool cell) {
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

	    public int[] deproject(bool[] values) {
		    return null;
	    }

	    public int[] deproject(String expression) {
		    return null;
	    }

    }
}
