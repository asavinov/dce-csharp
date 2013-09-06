﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Com.Model
{
    /// <summary>
    /// It is a whole space of all recommendations for a specific type of result, query/analysis. 
    /// This result is normally generated by one method (or a few alterantive methods) and is used by one UI component for one pattern. 
    /// Each field represents a recommendation-specific dimension (list) with the relevant options represented by recommendation fragment objects.
    /// All more specific recommendations are derived from this class. 
    /// </summary>
    public class Recommender
    {
        protected bool IsUpdating = false;
        protected string LastUpdated = null;

        // All possible recommendations as a list of complete recommendation objects. 
        public List<RecommendedFragment> Recommendations = new List<RecommendedFragment>();

        // Incremental part of recommendastion
        // Assume that the specified fragment has changed its selection, update all other fragment selections as well as relevances and other parameters.
        protected virtual void UpdateFragmentSelections(string selected) { }

        protected List<RecommendedFragment> GetFragmentList(string name)
        {
            System.Reflection.PropertyInfo pi = this.GetType().GetProperty(name);
            Debug.Assert(pi != null, "Wrong use: Property does not exist");

            object obj = pi.GetValue(this, null);
            Debug.Assert(obj is List<RecommendedFragment>, "Wrong use: Property has wrong type");

            return (List<RecommendedFragment>)obj;
        }

        protected void SetSelectedFragment(string name, RecommendedFragment value)
        {
            List<RecommendedFragment> list = GetFragmentList(name);

            int sel = -1;
            if (value != null)
            {
                sel = list.IndexOf(value); // Find element to be selected
            }

            list.ForEach(f => f.IsSelected = false); // Reset the current selection (no selected element)

            if (sel >= 0) // If can select
                list[sel].IsSelected = true;

            UpdateFragmentSelections(name); // Propagate new selection
        }

        public virtual Expression GetExpression() { return null; }

        public virtual string IsValidExpression() { return null; }

        public virtual void Recommend() { }
    }

    /// <summary>
    /// Recommendation objects are of type Expression representing suggested aggregation expressions.
    /// </summary>
    public class RecommendedRelationships : Recommender
    {
        public Set SourceSet { get; set; }
        public Set TargetSet { get; set; }
        public Set FactSet { get; set; }

        public List<RecommendedFragment> GroupingPaths { get; set; } // of type Expression or List (path)
        public List<RecommendedFragment> FactSets { get; set; } // of type Set
        public List<RecommendedFragment> MeasurePaths { get; set; } // of type Expression or List (path)

        public RecommendedFragment SelectedGroupingPath 
        {
            get { return GroupingPaths.FirstOrDefault(f => f.IsSelected); }
            set { SetSelectedFragment("GroupingPaths", value);  }
        }

        public RecommendedFragment SelectedFactSet
        {
            get { return FactSets.FirstOrDefault(f => f.IsSelected); }
            set { SetSelectedFragment("FactSets", value); }
        }

        public RecommendedFragment SelectedMeasurePath
        {
            get { return MeasurePaths.FirstOrDefault(f => f.IsSelected); }
            set { SetSelectedFragment("MeasurePaths", value); }
        }

        protected override void UpdateFragmentSelections(string selected) 
        {
            if (IsUpdating == true) return; // Prevent calling this method recursively

            IsUpdating = true;
            LastUpdated = selected;

            List<RecommendedFragment> selectedFrag = GetFragmentList(selected);

            // Reset all
            GroupingPaths.ForEach(f => f.IsRelevant = true);
            FactSets.ForEach(f => f.IsRelevant = true);
            MeasurePaths.ForEach(f => f.IsRelevant = true);

            switch (selected)
            {
                case "GroupingPaths":
                    {
                        // Update FactSets
                        Set factSet = ((List<Dim>)SelectedGroupingPath.Fragment)[0].LesserSet; // Find the fact set for this path
                        foreach (RecommendedFragment f in FactSets)
                        {
                            f.IsRelevant = f.Fragment == factSet ? true : false; // Only one fact set is enabled
                            f.IsSelected = f.Fragment == factSet ? true : false; // And hence it is also selected
                        }

                        // Update MeasurePaths
                        int relevantCount = 0;
                        foreach (RecommendedFragment f in MeasurePaths)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (SelectedMeasurePath != null && !SelectedMeasurePath.IsRelevant) SelectedMeasurePath = null;
                        if (relevantCount == 1) MeasurePaths.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        break;
                    }

                case "FactSets":
                    {
                        Set factSet = (Set)SelectedFactSet.Fragment;

                        // Update GroupingPaths
                        int relevantCount = 0;
                        foreach (RecommendedFragment f in GroupingPaths)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (SelectedGroupingPath != null && !SelectedGroupingPath.IsRelevant) SelectedGroupingPath = null;
                        if (relevantCount == 1) GroupingPaths.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        // Update MeasurePaths
                        relevantCount = 0;
                        foreach (RecommendedFragment f in MeasurePaths)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (SelectedMeasurePath != null && !SelectedMeasurePath.IsRelevant) SelectedMeasurePath = null;
                        if (relevantCount == 1) MeasurePaths.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        break;
                    }

                case "MeasurePaths":
                    {
                        // Update FactSets
                        Set factSet = ((List<Dim>)SelectedMeasurePath.Fragment)[0].LesserSet; // Find the fact set for this path
                        foreach (RecommendedFragment f in FactSets)
                        {
                            f.IsRelevant = f.Fragment == factSet ? true : false; // Only one fact set is enabled
                            f.IsSelected = f.Fragment == factSet ? true : false; // And hence it is also selected
                        }

                        // Update GroupingPaths
                        int relevantCount = 0;
                        foreach (RecommendedFragment f in GroupingPaths)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (SelectedGroupingPath != null && !SelectedGroupingPath.IsRelevant) SelectedGroupingPath = null;
                        if (relevantCount == 1) GroupingPaths.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        break;
                    }

            }

            IsUpdating = false;
        }

        public override Expression GetExpression()
        {
            var deprExpr = Com.Model.Expression.CreateDeprojectExpression((Set)SelectedFactSet.Fragment, (List<Dim>)SelectedGroupingPath.Fragment); // Grouping (deproject) expression: (Customers) <- (Orders) <- (Order Details)
            var projExpr = Com.Model.Expression.CreateProjectExpression((Set)SelectedFactSet.Fragment, (List<Dim>)SelectedMeasurePath.Fragment, Operation.DOT); // Measure (project) expression: (Order Details) -> (Product) -> List Price

            // TODO: here we need a method of Expression class to create a path expression (or relationships expression)

            return null;
        }

        public override string IsValidExpression()
        {
            if (SelectedGroupingPath == null) return "GroupingPaths";

            if (FactSet == null && SelectedFactSet == null) return "FactSets";

            if (SelectedMeasurePath == null) return "MeasurePaths";

            return null;
        }

        /// <summary>
        /// Find all possible relationship paths from this set to the specified destination set via the specified lesser set.
        /// </summary>
        public override void Recommend()
        {
            //
            // 1. Find all possible lesser sets (relationship sets)
            //
            var lesserSets = new List<Set>();
            if (this.FactSet != null) // Lesser set is provided
            {
                lesserSets.Add(this.FactSet);
            }
            else // Generate all possible lesser sets
            {
                var allPaths = new PathEnumerator(null, new List<Set> { this.SourceSet }, true, DimensionType.IDENTITY_ENTITY).ToList();
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
            RecommendedFragment frag;
            foreach (Set set in lesserSets)
            {
                var gPaths = new PathEnumerator(new List<Set> { set }, new List<Set> { this.SourceSet }, false, DimensionType.IDENTITY_ENTITY).ToList();
                var mPaths = new PathEnumerator(new List<Set> { set }, new List<Set> { this.TargetSet }, false, DimensionType.IDENTITY_ENTITY).ToList();
                if (gPaths.Count == 0 || mPaths.Count == 0) continue;

                frag = new RecommendedFragment(set, 1.0);
                this.FactSets.Add(frag);

                gPaths.ForEach(gp => this.GroupingPaths.Add(new RecommendedFragment(gp, 1.0)));
                mPaths.ForEach(mp => this.MeasurePaths.Add(new RecommendedFragment(mp, 1.0)));

                // For each pair of down-up paths build a relationship path
                foreach (var gp in gPaths)
                {
                    foreach (var mp in mPaths)
                    {
                        this.Recommendations.Add(new RecommendedFragment(null, 1.0)); // TODO: build complete path or an object (tuple of indexes) representing a complete path
                    }
                }

            }
        }

        public RecommendedRelationships()
            : base()
        {
            GroupingPaths = new List<RecommendedFragment>(); // of type Expression or List (path)
            FactSets = new List<RecommendedFragment>(); // of type Set
            MeasurePaths = new List<RecommendedFragment>(); // of type Expression or List (path)
        }
    }

    public class RecommendedAggregations : RecommendedRelationships
    {
        public List<RecommendedFragment> MeasureDimensions { get; set; }
        public List<RecommendedFragment> AggregationFunctions { get; set; }

        public RecommendedFragment SelectedMeasureDimension
        {
            get { return MeasureDimensions.FirstOrDefault(f => f.IsSelected); }
            set { SetSelectedFragment("MeasureDimensions", value); }
        }

        public RecommendedFragment SelectedAggregationFunction
        {
            get { return AggregationFunctions.FirstOrDefault(f => f.IsSelected); }
            set { SetSelectedFragment("AggregationFunctions", value); }
        }

        public override Expression GetExpression()
        {
            var deprExpr = Com.Model.Expression.CreateDeprojectExpression((Set)SelectedFactSet.Fragment, (List<Dim>)SelectedGroupingPath.Fragment); 

            var measureDim = (Dim)SelectedMeasureDimension.Fragment;
            var measurePath = (List<Dim>)SelectedMeasurePath.Fragment;
            measurePath.Add(measureDim);

            var projExpr = Com.Model.Expression.CreateProjectExpression((Set)SelectedFactSet.Fragment, measurePath, Operation.DOT);

            var aggregExpr = Com.Model.Expression.CreateAggregateExpression((string)SelectedAggregationFunction.Fragment, deprExpr, projExpr);

            return aggregExpr;
        }

        public override string IsValidExpression()
        {
            if (base.IsValidExpression() != null) return base.IsValidExpression();

            if (SelectedMeasureDimension == null) return "MeasureDimensions";

            if (SelectedAggregationFunction == null) return "AggregationFunctions";

            return null;
        }

        public override void Recommend()
        {
            base.Recommend();

            // Add more for aggregations
            this.MeasureDimensions = new List<RecommendedFragment>();
            foreach (Dim dim in this.TargetSet.GreaterDims)
            {
                var frag = new RecommendedFragment(dim, 1.0);
                this.MeasureDimensions.Add(frag);
            }

            this.AggregationFunctions = new List<RecommendedFragment>();
            this.AggregationFunctions.Add(new RecommendedFragment("SUM", 1.0));
            this.AggregationFunctions.Add(new RecommendedFragment("AVG", 1.0));
        }

        public RecommendedAggregations()
            : base()
        {
            MeasureDimensions = new List<RecommendedFragment>();
            AggregationFunctions = new List<RecommendedFragment>();
        }
    }

    /// <summary>
    /// Recommended set and dimension mappings.
    /// One recommendation is a primitive tree. 
    /// </summary>
    public class RecommendedMappings : Recommender
    {
        public Set SourceSet { get; set; } // From this (imported) set
        public Set TargetSet { get; set; } // To this set (can point to root of the database which means that target set has to be suggested)

        // Root of the dimension tree
        public List<RecommendedFragment> Root { get; set; } // of type Dim?

        public override void Recommend()
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

        }

        public RecommendedMappings()
            : base()
        {
            Root = new List<RecommendedFragment>();
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
        public int FragmentType { get; set; } // Fragment is a object so we need its type: enumerator, typeof(Fragment), or subclassing for each type. 
        public object Fragment { get; set; } // It is the fragment itself. It can be an expression, set, dimension, function etc.
        public double Relevance { get; set; } // Unconditional (initial) weight between 0 and 1. Generated by the suggestion procedure. 
        public int Index { get; set; } // It is the order/rank according to the relevance. 0 index corresponds to highest (best) relevance. 

        private string _displayName;
        public string DisplayName // Shown in List view. Can be generated from expression or the object, or set explicitly
        { 
            get 
            {
                if (_displayName != null) return _displayName;

                if (Fragment is string) return (string)Fragment;
                else if (Fragment is Set) return ((Set)Fragment).Name;
                else if (Fragment is Dim) return ((Dim)Fragment).Name;
                else if (Fragment is List<Dim>)
                {
                    string name = "";
                    ((List<Dim>)Fragment).ForEach(seg => name += "." + seg.Name);
                    return name;
                }
                else return "<UNKNOWN>";
            }
            set { _displayName = value; }
        }

        public List<RecommendedFragment> children { get; set; }

        // UI (variable)
        public bool IsSelected { get; set; }
        public bool IsRelevant { get; set; } // Can be selected under the current constraints
        public double CurrentRelevance { get; set; } // Conditional relevance/weight depending on the curerent selection and other factors. 0 means that the component is disabled. 

        // We probably need support for soring items according their current weight, that is, getting first, second etc. Maybe some enumerator or support via ListView/WPF (filter/sorting)
        public int CurrentIndex { get; set; } // What entry number corresponds to this position. 0 for highest relevance. 

        // We might also provide support for coarse-grained categorization like HIGH, MID, LOW, DISABLED (so IsDisabled is a special case). The categories are defined by static parameters or dynamically calculated (equal intervals etc.)

        // We might also provide support for visualization like colors and icons. 

        public RecommendedFragment(object component, double relevance)
        {
            Fragment = component;
            Relevance = relevance;

            IsSelected = false;
            IsRelevant = true;
            CurrentRelevance = Relevance;
        }

    }

    public class DimList // Or DimPath
    {
        // It should be used where we use List<Dim>
        // It is more convenient because we can store additional methods, say, concatenate, iterate, append etc.
        // And DimList can be stored in Dim.Path 
        // It also can be used when using in DimTree, representing projections/de-projections and in other structures.
    }
    
    public class DimTree
    {
        public Dim Dim; // It is essentially the element of the tree. It must be between the parent and all children. 
        public Set Set { get { return Dim.GreaterSet; } } // Greater set of the dimension

        public DimTree Parent; // Where this node is a child (lesser)
        public DimTree Root { get { return null; } } // Computes root node

        public List<DimTree> Children; // Child nodes (greater)
        public DimTree AddChild(Dim dim)
        {
            return null;
        }
        public DimTree RemoveChild(Dim dim)
        {
            return null;
        }

        public DimTree MatchingNode; // A node in another tree
        public double MatchingFactor; // Relevance

        // Compute similarity if they are matched
        // Two trees have to be (correctly) matched, for example, by the user.
        // This method computes similarity for this match. It can be bad or good depending on the matches. 
        public static double ComputeMatch(DimTree tree)
        {
            // Compute own similarity of two pairs <dim, gSet> or, alternatively, two matching dims
            double own = 1.0;
            // tree.Dim vs tree.MatchingNode.Dim
            // tree.Set vs tree.MatchingNode.Set - can be primitive
            // A new (non-existing) dimension or set should be qualified as a perfect match because it is supposed to be created by the user or otherwise for this concrete purpose (independent of its name)

            // Aggregate similarities between children. If primitive then a predefined similarity
            double relevance = 0.0;
            foreach (DimTree child in tree.Children) // Aggregate all child matches
            {
                relevance += ComputeMatch(child); // Recursion
            }

            relevance /= tree.Children.Count(); // Average

            return (relevance + own) / 2;
        }

        // Generate best match(es) for this tree among ?
        // Where do we find possible target nodes (dim, greater set pairs)?
        // Must they (dimensions, sets) exist or can be created? 
        // What if the algorithm wants to create a new dimension or set?
        public static void RecommendMatch(DimTree tree, SetRoot target)
        {
            var candidates = new List<DimTree>();

            // Create a list of candidates in order to decrease the search space
            Set targetLesserSet = tree.Parent != null ? tree.Parent.MatchingNode.Dim.LesserSet : null;
            foreach (Dim dim in targetLesserSet.GreaterDims)
            {
                // Compare tree.Dim vs dim and include in candiates if relevance is higher than threshold 
            }

            // Start recursive search by find the best option. Sett a candidate and then evaluating the result
            foreach (DimTree candidate in candidates)
            {
                tree.MatchingNode = candidate;
            }


        }

    }


}
