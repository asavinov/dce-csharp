﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Newtonsoft.Json.Linq;

using Com.Schema;

using Rowid = System.Int32;

namespace Com.Utils
{
    /// <summary>
    /// Abstract base class for all kinds of path enumerators without complex constraints.
    /// 
    /// Implementing iterators: 
    /// http://msdn.microsoft.com/en-us/magazine/cc163682.aspx
    /// http://www.codeproject.com/Articles/34352/Tree-Iterators
    /// TODO:
    /// - study how to use yield in iterators
    /// - study how to use nested classes for iterators 
    /// - implement many different kinds of iterators: depth-first, bredth-first, leafs-only etc.
    /// </summary>
    public abstract class ColumnEnumerator : ColumnPath, IEnumerator<ColumnPath>, IEnumerable<ColumnPath>
    {

        public ColumnEnumerator(DcTable tab)
            : base(tab)
        {
            Segments = new List<DcColumn>();
        }

        // Get the explicit current node.
        public ColumnPath Current { get { return new ColumnPath(Segments); } }

        // Get the implicit current node.
        object System.Collections.IEnumerator.Current
        {
            get { return Current; }
        }

        // Increment the iterator and moves the current node to the next one
        public abstract bool MoveNext();

        // Dispose the object.
        public void Dispose()
        {
            Input = null;
            Output = null;

            Segments.Clear();
            Segments = null;
        }

        // Reset the iterator.
        public void Reset()
        {
            if (Segments != null) Segments.Clear();
        }

        // Get the underlying enumerator.
        public virtual IEnumerator<ColumnPath> GetEnumerator()
        {
            return (IEnumerator<ColumnPath>)this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }

    /// <summary>
    /// Abstract base class for path enumerators with complex constraints.
    /// </summary>
    public abstract class ColumnComplexEnumerator : ColumnEnumerator
    {
        protected List<DcTable> _inputs;
        protected List<DcTable> _outputs;
        protected bool isInverse;
        protected ColumnType columnType; // The path will be composed of only these types of segments
        protected bool allowIntermediateTabs = false; // Not implemented. lesser and greater tables only as source and destination - not in the middle of the path (default). Otherwise, they can appear in the middle of the path (say, one greater table and then the final greater table).

        // Here we store the current child number for the next element
        protected int[] childNumber = new int[1024];
        // Whether the current table is valid or not. If it is valid then all parents/previous are invalid in the case of coverage condition.
        protected bool[] isValidTab = new bool[1024];

        public ColumnComplexEnumerator(List<DcTable> inputs, List<DcTable> outputs, bool _isInverse, ColumnType _columnType)
            : base(null)
        {
            _inputs = inputs;
            _outputs = outputs;

            isInverse = _isInverse;
            columnType = _columnType;

            if (!isInverse)
            {
                Input = _inputs[0];
                Output = _inputs[0];
            }
            else
            {
                Input = _outputs[0];
                Output = _outputs[0];
            }

            childNumber = Enumerable.Repeat(-1, 1024).ToArray();
            isValidTab = Enumerable.Repeat(false, 1024).ToArray();
        }

        public ColumnComplexEnumerator(DcTable tab)
            : base(tab)
        {
            _inputs = new List<DcTable>(new DcTable[] { tab }); // One source table
            _outputs = new List<DcTable>(new DcTable[] { tab.Schema.Root }); // All destination tables from this schema

            isInverse = false;

            childNumber = Enumerable.Repeat(-1, 1024).ToArray();
            isValidTab = Enumerable.Repeat(false, 1024).ToArray();
        }
    }

    /// <summary>
    /// Enumerate all different paths between specified tables. 
    /// Notes:
    /// - Consider only segments that satisfy constraints imposed on segment types (entity, identity etc.) Only these segments are used to traverse the poset.
    /// - Consider only tables that satisfy the constraints imposed on possible lesser and greater tables (only primitive, all, selected tables etc.) Only paths starting/ending with these tables can be returned (and the paths involve only segments of the specified type).
    /// - Currently we return only maximum length paths. 
    /// </summary>
    public class PathEnumerator : ColumnComplexEnumerator
    {
        public PathEnumerator(DcTable tab, ColumnType columnType) // All primitive paths
            : this(new List<DcTable>(new DcTable[] { tab }), null, false, columnType)
        {
        }

        public PathEnumerator(DcTable input, DcTable output, ColumnType columnType) // Between two tables
            : this(new List<DcTable>(new DcTable[] { input }), new List<DcTable>(new DcTable[] { output }), false, columnType)
        {
        }

        public PathEnumerator(List<DcTable> inputs, List<DcTable> outputs, bool isInverse, ColumnType columnType)
            : base(inputs, outputs, isInverse, columnType)
        {
        }

        public override bool MoveNext()
        {
            // TODO: We need also loop over all source tables

            // It is a traverse algorithm. We move either one step forward or one step back on each iteration of the loop.
            // Various conditions are checked after we arrive at a new child (forward) or before we leave a node and go to the parent. 
            // The algorithm returns if either a good node is found (true) or no nodes exist anymore (false).
            while (true)
            {
                var nextSegs = GetNextValidSegments();
                int childNo = childNumber[Size];

                if (childNo + 1 < nextSegs.Count) // Can move to the next child
                {
                    childNumber[Size] = childNo + 1;
                    AddLastSegment(nextSegs[childNo + 1]);

                    // Process a new node
                    isValidTab[Size] = TargetTableValid();
                    // Coverage. We are interested only in longest paths. If we need to return all (even shorter) paths then remove this block.
                    if (isValidTab[Size])
                    {
                        for (int i = 0; i < Size; i++) isValidTab[i] = false;
                    }

                    // Process the child node just added on the next iteration.
                }
                else // No children anymore - return to the previous parent
                {
                    // Before leaving this node, we are able to decide whether it is what we have been looking for because all its children have been processed
                    // If it is what we need then return true
                    if (isValidTab[Size])
                    {
                        isValidTab[Size] = false; // During next call, we have to skip this block
                        return true;
                    }

                    if (Size == 0) // Detect finish condition
                    {
                        return false; // All nodes have been visited - no nodes anymore
                    }

                    // Really leave this node
                    childNumber[Size] = -1;
                    DcColumn column = RemoveLastSegment();

                    // Process the parent node we have just returned to on the next iteration.
                }

            }

        }

        [System.Obsolete("Not needed.", true)]
        private bool MoveForward() // return true - valid destination table found, false - no valid destination found and cannot continue (terminal)
        {
            // Move using only valid segments. 
            List<DcColumn> nextSegs;
            bool moved = false;
            while (true)
            {
                nextSegs = GetNextValidSegments();
                if (nextSegs.Count == 0) return moved;

                moved = true;
                AddLastSegment(nextSegs[0]); // always attach first possible segment (next segments will be attached while moving back)
            }
        }
        [System.Obsolete("Not needed.", true)]
        private bool MoveBackward() // return true - found a continuation from some parent, false - not found a table with possibility to continue(end, go to next source table)
        {
            DcColumn segment = null;
            while (true) // A loop for removing last segment and moving backward
            {
                segment = RemoveLastSegment(); // Remove last segment
                if (segment == null)
                {
                    return false; // All segments removed but no continuation found in any of the previous tables including the source one
                }

                List<DcColumn> nextSegs = GetNextValidSegments();

                int segIndex = nextSegs.IndexOf(segment);
                if (segIndex + 1 < nextSegs.Count) // Continuation found. Use it
                {
                    segment = nextSegs[segIndex + 1];
                    AddLastSegment(segment); // Add next last segment

                    return true;
                }
                else // Continuation from the parent not found. Continue removing to the next parent. 
                {
                }
            }
        }

        private List<DcColumn> GetNextValidSegments() // Here we find continuation segments that satisfy our criteria on columns
        {
            if (!isInverse) // Move up from lesser to greater
            {
                if (Output.IsPrimitive) return new List<DcColumn>(); // We exclude the top element
                switch (columnType)
                {
                    case ColumnType.IDENTITY_ENTITY: return Output.Columns.Where(x => x.Input.Schema == x.Output.Schema).ToList();
                    case ColumnType.IDENTITY: return Output.Columns.Where(x => x.IsKey && x.Input.Schema == x.Output.Schema).ToList();
                    case ColumnType.ENTITY: return Output.Columns.Where(x => !x.IsKey && x.Input.Schema == x.Output.Schema).ToList();

                    case ColumnType.GENERATING: return Output.Columns.Where(x => (x.GetData() != null && x.GetData().IsAppendData) && x.Input.Schema == x.Output.Schema).ToList();
                }
            }
            else
            {
                switch (columnType)
                {
                    case ColumnType.IDENTITY_ENTITY: return Input.InputColumns.Where(x => x.Input.Schema == x.Output.Schema).ToList();
                    case ColumnType.IDENTITY: return Input.InputColumns.Where(x => x.IsKey && x.Input.Schema == x.Output.Schema).ToList();
                    case ColumnType.ENTITY: return Input.InputColumns.Where(x => !x.IsKey && x.Input.Schema == x.Output.Schema).ToList();

                    case ColumnType.GENERATING: return Input.InputColumns.Where(x => (x.GetData() != null && x.GetData().IsAppendData) && x.Input.Schema == x.Output.Schema).ToList();
                }
            }

            return null;
        }
        private DcColumn RemoveLastSegment()
        {
            if (Size == 0) return null; // Nothing to remove

            DcColumn segment = null;
            if (!isInverse)
            {
                segment = RemoveLast();
            }
            else
            {
                segment = RemoveFirst();
            }
            return segment;
        }
        private void AddLastSegment(DcColumn segment)
        {
            if (!isInverse)
            {
                InsertLast(segment);
            }
            else
            {
                InsertFirst(segment);
            }
        }
        private bool TargetTableValid() // Here we determined of the table satisfy our criteria on tables
        {
            List<DcTable> destinations = !isInverse ? _outputs : _inputs;
            DcTable dest = !isInverse ? Output : Input;

            if (destinations == null) // Destinations are primitive and only primitive tables
            {
                if (!isInverse) return Output.IsPrimitive;
                else return Input.IsLeast; // Just least table because primitive tables do not exist for least tables
            }
            else if (destinations.Count == 0) // Destinations are terminal tables (greatest or least). Check possibility to continue.
            {
                return true;
                /*
                                if (!isInverse) return Output.IsGreatest;
                                else return Input.IsLeast;
                */
            }
            else // Concrete destinations are specified
            {
                foreach (DcTable tab in destinations) if (dest.IsSubTable(tab)) return true;
                return false;
            }
        }
    }

    /*
        public class DepthDimEnumerator : DimEnumerator
        {
            private DimensionType dimType;

            public DepthDimEnumerator(Set set, DimensionType _dimType)
                : base(set) 
            {
                dimType = _dimType;
            }

            public override bool MoveNext()
            {
                if (!Output.IsPrimitive)
                {
                    bool primitiveFound = MoveForward();
                    if (primitiveFound) return true;
                    // Else move down back
                }

                // Go down (return back) until we find the next (unvisited) child
                bool childFound = MoveBackward();
                if (!childFound) return false;

                return MoveForward();
            }
            private bool MoveForward() // true - found primitive, false - found non-primitive leaf
            {
                while (!Output.IsPrimitive) // Not a leaf - go up deeper and search for the first primitive set
                {
                    List<Dim> dims = GetContinuations();

                    if (dims.Count != 0)
                    {
                        Path.Add(dims[0]); // Continue depth-first by adding the very first dimension
                        Output = LastSegment.Output;
                    }
                    else
                    {
                        return false; // Non-primitive set but no possibility to continue
                    }
                }
                return true;
            }
            private bool MoveBackward() // true - found next child that can be continued up, false - not found (end)
            {
                Dim child = null;
                do // It is only down loop (removing last segment)
                {
                    if (Path.Count == 0) // Nothing to remove. End.
                    {
                        return false;
                    }

                    child = LastSegment;
                    Path.RemoveAt(Path.Count - 1);
                    Output = Path.Count == 0 ? Input : LastSegment.Output;

                    List<Dim> children = GetContinuations();

                    int childIndex = children.IndexOf(child);
                    if (childIndex + 1 < children.Count) // Good child. Use it
                    {
                        child = children[childIndex + 1];
                        Path.Add(child);
                        Output = LastSegment.Output;
                        return true;
                    }
                    else // Bad child. Continue removing.
                    {
                        child = null;
                    }
                } while (child == null);

                return false; // Unreachable
            }

            private List<Dim> GetContinuations()
            {
                switch (dimType)
                {
                    case DimensionType.IDENTITY: return Output.GetIdentityDims();
                    case DimensionType.ENTITY: return Output.GetEntityDims();
                    case DimensionType.IDENTITY_ENTITY: return Output.GreaterDims;
                }
                return null;
            }
        }
    */

}
