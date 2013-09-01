using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Model
{
    public class Recommender
    {
        public static RecommendedMappings RecommendMappings(Set srcSet, Set dstSet, Set lesserSet)
        {
            // Find relevant set mappings of the specified set to the destination database. 
            // Each mappings means specifying a target set as well as dimension mappings which also possibly provide other set mappings.
            // At the end, each mapping is based upon and formally has to specify primitive set mappings. 
            // Target sets could be existing or new. A new target set essentially means creating a more or less exact copy of the source set.

            // It is an example of hierarchical space of recommendations (as opposed to multidimensional space). 
            // Depending on the choise of the parent set mapping, its alternative child set mappings are chosen. 
            // Moreover, in hierarchical space, the choice of parent determines relevance of children and the whole mapping

            // How alternatives are represented? In multidimensional space, alterantives are represtented as combinations of field values
            // In hierarchical space, each node has some alternatives. And then depending on the choice, its children have their alternatives and so on. 
            // Thus it is a tree but it can change its structure depending on the choices of alternatives.

            // Anohter representation is a flat list of mappings between paths. In other words, for each source path we specify alternative target paths. 
            // Selection propagation is also hierarchical but is represnted in flat space: when some intermediate mapping is selected then all possible paths are restricted.
            // Or, we can consider path mapping individual independent elements. If one path mapping is selected then other possible paths are also restricted and their relevance is updated. 
            // The source set is fixed and it is characterized by a fixed set of primitive paths.
            // One alternative mapping consists of a set of target (existing) paths assigned to the source paths. 
            // We can find a set of all possible target paths and for each source path a set of possible mappings.
            // By selecting a possible mapping for one source path, other mapping are updated. 
            // Thus we have a multidimensional space where one source path is a dimension with many alternaive target paths.
            // When selecting one path mapping, we actually fix intermediate set mapping. But the question is whether we need set mappings because it is already a hierachical approach.

            return null;
        }

        /// <summary>
        /// Find all possible relationship paths from this set to the specified destination set via the specified lesser set.
        /// </summary>
        public static RecommendedRelationships RecommendRelationships(Set srcSet, Set dstSet, Set lesserSet)
        {
            //
            // 1. Find all possible lesser sets (relationship sets)
            //
            var lesserSets = new List<Set>();
            if (lesserSet != null) // Lesser set is provided
            {
                lesserSets.Add(lesserSet);
            }
            else // Generate all possible lesser sets
            {
                var allPaths = new PathEnumerator(null, new List<Set> { srcSet }, true, DimensionType.IDENTITY_ENTITY).ToList();
                foreach (var path in allPaths)
                {
                    foreach (Dim seg in path)
                    {
                        if (!lesserSets.Contains(seg.LesserSet)) lesserSets.Add(seg.LesserSet);
                    }
                }
            }

            //
            // 2. Given a lesser set, find all relationship paths as pairs of <grouping path, measure path
            //
            var recom = new RecommendedRelationships();
            foreach (Set set in lesserSets)
            {
                var gPaths = new PathEnumerator(new List<Set> { set }, new List<Set> { srcSet }, false, DimensionType.IDENTITY_ENTITY).ToList();
                var mPaths = new PathEnumerator(new List<Set> { set }, new List<Set> { dstSet }, false, DimensionType.IDENTITY_ENTITY).ToList();
                if (gPaths.Count == 0 || mPaths.Count == 0) continue;

                recom.FactSets.Add(new RecommendedFragment(set, 1.0));
                gPaths.ForEach(gp => recom.GroupingPaths.Add(new RecommendedFragment(gp, 1.0)));
                mPaths.ForEach(mp => recom.MeasurePaths.Add(new RecommendedFragment(mp, 1.0)));

                // For each pair of down-up paths build a relationship path
                foreach (var gp in gPaths)
                {
                    foreach (var mp in mPaths)
                    {
                        recom.Recommendations.Add(new RecommendedFragment(null, 1.0)); // TODO: build complete path or an object (tuple of indexes) representing a complete path
                    }
                }

            }

            return recom;
        }

    }

    /// <summary>
    /// It is one of many possible fragments within a more complex recommendation for a query, expression or pattern. 
    /// It represents one option among many alteranatives to be chosen by the user, that is, it is a coordinate along one dimension. 
    /// It is independent of the type of recommendation and this type can be stored in a field as enumeration. 
    /// If it is necessary to develop a more specific fragment the this class has to be extended by a subclass. 
    /// </summary>
    public class RecommendedFragment
    {
        // Constant parameters
        public int FragmentType; // Fragment is a object so we need its type: enumerator, typeof(Fragment), or subclassing for each type. 
        public object Fragment; // It is the fragment itself. It can be an expression, set, dimension, function etc.
        public double Relevance; // Unconditional (initial) weight between 0 and 1. Generated by the suggestion procedure. 
        public int Index; // It is the order/rank according to the relevance. 0 index corresponds to highest (best) relevance. 
        public string DisplayName; // Shown in List view. Can be generated from expression or the object, or set explicitly

        public List<RecommendedFragment> children;

        // UI (variable) parameters
        public double CurrentRelevance; // Conditional relevance/weight depending on the curerent selection and other factors. 0 means that the component is disabled. 
        public bool IsDisabled { get { return CurrentRelevance == 0; } }

        // We probably need support for soring items according their current weight, that is, getting first, second etc. Maybe some enumerator or support via ListView/WPF (filter/sorting)
        public int CurrentIndex; // What entry number corresponds to this position. 0 for highest relevance. 

        // We might also provide support for coarse-grained categorization like HIGH, MID, LOW, DISABLED (so IsDisabled is a special case). The categories are defined by static parameters or dynamically calculated (equal intervals etc.)

        // We might also provide support for visualization like colors and icons. 

        public RecommendedFragment(object component, double relevance)
        {
            Fragment = component;
            Relevance = relevance;

            CurrentRelevance = Relevance;
        }

    }

    /// <summary>
    /// It is a whole space of all recommendations for a specific type of result, query/analysis. 
    /// This result is normally generated by one method (or a few alterantive methods) and is used by one UI component for one pattern. 
    /// Each field represents a recommendation-specific dimension (list) with the relevant options represented by recommendation fragment objects.
    /// All more specific recommendations are derived from this class. 
    /// </summary>
    public class RecommendedOptions
    {
        // All possible recommendations as a list of complete recommendation objects. 
        public List<RecommendedFragment> Recommendations = new List<RecommendedFragment>();
    }

    /// <summary>
    /// Recommendation objects are of type Expression representing suggested aggregation expressions.
    /// </summary>
    public class RecommendedRelationships : RecommendedOptions
    {
        public Set SourceSet;
        public Set TargetSet;
        public Set RelationshipSet;

        public List<RecommendedFragment> GroupingPaths = new List<RecommendedFragment>(); // of type Expression or List (path)
        public List<RecommendedFragment> FactSets = new List<RecommendedFragment>(); // of type Set
        public List<RecommendedFragment> MeasurePaths = new List<RecommendedFragment>(); // of type Expression or List (path)
    }

    public class RecommendedAggregations : RecommendedRelationships
    {
        public List<RecommendedFragment> MeasureSet = new List<RecommendedFragment>(); // of type Set. It is normally a primitive set type like Double
        public List<RecommendedFragment> MeasureDimensions = new List<RecommendedFragment>(); // of type Dim (could be fixed param of suggestion like source and target sets)
        public List<RecommendedFragment> AggregationFunctions = new List<RecommendedFragment>(); // of type string (or Function object if we introduce them)
    }

    /// <summary>
    /// Recommended set and dimension mappings.
    /// One recommendation is a primitive tree. 
    /// </summary>
    public class RecommendedMappings : RecommendedOptions
    {
        public Set SourceSet; // From this (imported) set
        public Set TargetSet; // To this set (can point to root of the database which means that target set has to be suggested)

        // Root of the dimension tree
        public List<RecommendedFragment> Root = new List<RecommendedFragment>(); // of type Dim?
    }
}
