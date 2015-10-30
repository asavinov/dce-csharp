using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Com.Data;
using Com.Schema;

namespace Com.Data
{
    public interface DcTableDefinition
    {
        /// <summary>
        /// Specifies kind of formula used to define this table. 
        /// </summary>
        TableDefinitionType DefinitionType { get; }

        /// <summary>
        /// Constraints on all possible instances. 
        /// Currently, it is written in terms of and is applied to source (already existing) instances - not instances of this set. Only instances satisfying these constraints are used for populating this set. 
        /// In future, we should probabyl apply these constraints to this set elements while the source set has its own constraints.
        /// </summary>
        string WhereFormula { get; set; }
        ExprNode WhereExpr { get; set; } // May store ComColumn which stores a boolean function definition

        /// <summary>
        /// Ordering of the instances. 
        /// Here again we have a choice: it is how source elements are sorted or it is how elements of this set have to be sorted. 
        /// Expression should be something like Comparator
        /// </summary>
        string OrderbyFormula { get; set; }

        /// <summary>
        /// Create all instances of this set. 
        /// Notes:
        /// - (principle): never change any set - neither lesser nor greater (even in the case of generating/projection dimensions)
        /// - Population means produce a subset of all possible elements where all possible elements are defined by greater dimensions.
        /// - There are two ways to restrict all possible elements: where predicate, and generating/projection lesser dimensions (equivalently, referencing from some lesser set by the generating dimensions).
        /// - Accordingly, there are two algorithms: produce all combinations of greater elements (for identity dimensions), and produce all combinations of the lesser elements (for generating dimensions).
        /// </summary>
        void Populate();
        /// <summary>
        /// Remove all instances.
        /// </summary>
        void Unpopulate(); // Is not it Length=0? Or nullify all columns?

        //
        // Dependencies. The order is important and corresponds to dependency chain
        //
        List<DcTable> UsesTables(bool recursive); // This element depends upon
        List<DcTable> IsUsedInTables(bool recursive); // Dependants

        List<DcColumn> UsesColumns(bool recursive); // This element depends upon
        List<DcColumn> IsUsedInColumns(bool recursive); // Dependants
    }

    public enum TableDefinitionType // Specific types of table formula
    {
        FREE, // No definition for the table (and cannot be defined). 
        // Example: manually created table with primitive dimensions.
        // Maybe we should interpret it as MANUALly entered data (which is stored along with this table definition as a consequence)
        // Another possible interpretation is explicit population/definition by analogy with colums defined as an explicit array

        PRODUCT, // Table contains all combinations of its greater (key) sets satsifying the constraints
        // An important parameter is which dimensions to vary: FREE or IsKey? So we need to define dependency between to be FREE (definition) and to be key (uniqueness constraint)
        // - greater with filter (use key dims; de-projection of several greater dims by storing all combinations of their inputs)
        //   - use only Key greater dims for looping. 
        //   - user greater sets input values as constituents for new tuples
        //   - greater dims are populated simultaniously without evaluation (they do not have defs.)

        PROJECTION, // Table gets its elements from (unique) outputs of some function
        // - lesser with filter
        //   - use only IsGenerating lesser dims for looping. 
        //   - use thier lesser sets to loop through all their combinations
        //   - !!! each lesser dim is evaluated using its formula that returns some constituent of the new set (or a new element in the case of 1 lesser dim)
        //   - set elements store a combination of lesser dims outputs
        //   - each lesser dim stores the new tuple in its output (alternatively, these dims could be evaluatd after set population - will it really work for multiple lesser dims?)

        // Use only generating lesser dimensions (IsGenerating)
        // Organize a loop on combinations of their inputs
        // For each combination, evaluate all the lesser generating dimensions by finding their outputs
        // Use these outputs to create a new tuple and try to append it to this set
        // If appended or found, then set values of the greater generating dimensions
        // If cannot be added (does not satisfy constraints) then set values of the greater generating dimensions to null

        FILTER, // Tables contains a subset of elements from its super-set

        ANY, // Arbitrary formula without constraints can be provided with a mix of various expression types
        // It is introduced by analogy with columns but for tables we actually do not have definitions - they are always populated from columns
        // Therefore, this option is not currently used
    }

}
