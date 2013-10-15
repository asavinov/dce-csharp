﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
        public Fragments<object> Recommendations { get; set; }

        // Incremental part of recommendastion
        // Assume that the specified fragment has changed its selection, update all other fragment selections as well as relevances and other parameters.
        protected virtual void UpdateFragmentSelections(string selected) { }

        public virtual Expression GetExpression() { return null; }

        public virtual string IsValidExpression() { return null; }

        public virtual void Recommend() { }

        public virtual void UpdateSelection(string selected) { }

        public Recommender()
        {
            Recommendations = new Fragments<object>();
        }
    }

    /// <summary>
    /// Recommendation objects are of type Expression representing suggested aggregation expressions.
    /// </summary>
    public class RecommendedRelationships : Recommender
    {
        public Set SourceSet { get; set; }
        public Set TargetSet { get; set; }
        public Set FactSet { get; set; }

        public Fragments<List<Dim>> GroupingPaths { get; set; }
        public Fragments<Set> FactSets { get; set; }
        public Fragments<List<Dim>> MeasurePaths { get; set; }

        public override void UpdateSelection(string selected)
        {
            if (IsUpdating == true) return; // Prevent calling this method recursively

            IsUpdating = true;
            LastUpdated = selected;

            // Reset all
            GroupingPaths.Alternatives.ForEach(f => f.IsRelevant = true);
            FactSets.Alternatives.ForEach(f => f.IsRelevant = true);
            MeasurePaths.Alternatives.ForEach(f => f.IsRelevant = true);

            switch (selected)
            {
                case "GroupingPaths":
                    {
                        if (!GroupingPaths.IsSelected) break;

                        // Update FactSets
                        Set factSet = GroupingPaths.SelectedObject[0].LesserSet; // Find the fact set for this path
                        foreach (var f in FactSets.Alternatives)
                        {
                            f.IsRelevant = f.Fragment == factSet ? true : false; // Only one fact set is enabled
                            f.IsSelected = f.Fragment == factSet ? true : false; // And hence it is also selected
                        }

                        // Update MeasurePaths
                        int relevantCount = 0;
                        foreach (var f in MeasurePaths.Alternatives)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (MeasurePaths.SelectedFragment != null && !MeasurePaths.SelectedFragment.IsRelevant) MeasurePaths.SelectedFragment = null;
                        if (relevantCount == 1) MeasurePaths.Alternatives.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        break;
                    }

                case "FactSets":
                    {
                        if (!FactSets.IsSelected) break;

                        Set factSet = FactSets.SelectedObject;

                        // Update GroupingPaths
                        int relevantCount = 0;
                        foreach (var f in GroupingPaths.Alternatives)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (GroupingPaths.SelectedFragment != null && !GroupingPaths.SelectedFragment.IsRelevant) GroupingPaths.SelectedFragment = null;
                        if (relevantCount == 1) GroupingPaths.Alternatives.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        // Update MeasurePaths
                        relevantCount = 0;
                        foreach (var f in MeasurePaths.Alternatives)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (MeasurePaths.SelectedFragment != null && !MeasurePaths.SelectedFragment.IsRelevant) MeasurePaths.SelectedFragment = null;
                        if (relevantCount == 1) MeasurePaths.Alternatives.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        break;
                    }

                case "MeasurePaths":
                    {
                        if (!MeasurePaths.IsSelected) break;

                        // Update FactSets
                        Set factSet = MeasurePaths.SelectedObject[0].LesserSet; // Find the fact set for this path
                        foreach (var f in FactSets.Alternatives)
                        {
                            f.IsRelevant = f.Fragment == factSet ? true : false; // Only one fact set is enabled
                            f.IsSelected = f.Fragment == factSet ? true : false; // And hence it is also selected
                        }

                        // Update GroupingPaths
                        int relevantCount = 0;
                        foreach (var f in GroupingPaths.Alternatives)
                        {
                            f.IsRelevant = ((List<Dim>)f.Fragment)[0].LesserSet == factSet ? true : false;
                            if (f.IsRelevant) relevantCount++;
                        }
                        if (GroupingPaths.SelectedFragment != null && !GroupingPaths.SelectedFragment.IsRelevant) GroupingPaths.SelectedFragment = null;
                        if (relevantCount == 1) GroupingPaths.Alternatives.ForEach(f => f.IsSelected = (f.IsRelevant ? true : false));

                        break;
                    }

            }

            IsUpdating = false;
        }

        public override Expression GetExpression()
        {
            var deprExpr = Com.Model.Expression.CreateDeprojectExpression(GroupingPaths.SelectedObject); // Grouping (deproject) expression: (Customers) <- (Orders) <- (Order Details)
            var projExpr = Com.Model.Expression.CreateProjectExpression(MeasurePaths.SelectedObject, Operation.DOT); // Measure (project) expression: (Order Details) -> (Product) -> List Price

            // TODO: here we need a method of Expression class to create a path expression (or relationships expression)

            return null;
        }

        public override string IsValidExpression()
        {
            if (GroupingPaths.SelectedObject == null) return "GroupingPaths";

            if (FactSet == null && FactSets.SelectedObject == null) return "FactSets";

            if (MeasurePaths.SelectedObject == null) return "MeasurePaths";

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
            foreach (Set set in lesserSets)
            {
                var gPaths = new PathEnumerator(new List<Set> { set }, new List<Set> { this.SourceSet }, false, DimensionType.IDENTITY_ENTITY).ToList();
                var mPaths = new PathEnumerator(new List<Set> { set }, new List<Set> { this.TargetSet }, false, DimensionType.IDENTITY_ENTITY).ToList();
                if (gPaths.Count == 0 || mPaths.Count == 0) continue;

                RecommendedFragment<Set> frag = new RecommendedFragment<Set>(set, 1.0);
                this.FactSets.Alternatives.Add(frag);

                gPaths.ForEach(gp => this.GroupingPaths.Alternatives.Add(new RecommendedFragment<List<Dim>>(gp, 1.0)));
                mPaths.ForEach(mp => this.MeasurePaths.Alternatives.Add(new RecommendedFragment<List<Dim>>(mp, 1.0)));

                // For each pair of down-up paths build a relationship path
                foreach (var gp in gPaths)
                {
                    foreach (var mp in mPaths)
                    {
                        this.Recommendations.Alternatives.Add(new RecommendedFragment<object>(null, 1.0)); // TODO: build complete path or an object (tuple of indexes) representing a complete path
                    }
                }

            }
        }

        public RecommendedRelationships()
            : base()
        {
            GroupingPaths = new Fragments<List<Dim>>();
            FactSets = new Fragments<Set>();
            MeasurePaths = new Fragments<List<Dim>>();
        }
    }

    public class RecommendedAggregations : RecommendedRelationships
    {
        public Fragments<Dim> MeasureDimensions { get; set; }
        public Fragments<string> AggregationFunctions { get; set; }

        public override Expression GetExpression()
        {
            var deprExpr = Com.Model.Expression.CreateDeprojectExpression(GroupingPaths.SelectedObject);

            var measureDim = MeasureDimensions.SelectedObject;
            var measurePath = MeasurePaths.SelectedObject;
            measurePath.Add(measureDim);

            var projExpr = Com.Model.Expression.CreateProjectExpression(measurePath, Operation.DOT);

            var aggregExpr = Com.Model.Expression.CreateAggregateExpression(AggregationFunctions.SelectedObject, deprExpr, projExpr);

            return aggregExpr;
        }

        public override string IsValidExpression()
        {
            if (base.IsValidExpression() != null) return base.IsValidExpression();

            if (MeasureDimensions.SelectedObject == null) return "MeasureDimensions";

            if (AggregationFunctions.SelectedObject == null) return "AggregationFunctions";

            return null;
        }

        public override void Recommend()
        {
            base.Recommend();

            // Add more for aggregations
            MeasureDimensions.Alternatives.Clear();
            foreach (Dim dim in this.TargetSet.GreaterDims)
            {
                var frag = new RecommendedFragment<Dim>(dim, 1.0);
                MeasureDimensions.Alternatives.Add(frag);
            }

            AggregationFunctions.Alternatives.Clear();
            AggregationFunctions.Alternatives.Add(new RecommendedFragment<string>("SUM", 1.0));
            AggregationFunctions.Alternatives.Add(new RecommendedFragment<string>("AVG", 1.0));
        }

        public RecommendedAggregations()
            : base()
        {
            MeasureDimensions = new Fragments<Dim>();
            AggregationFunctions = new Fragments<string>();
        }
    }

    /// <summary>
    /// It is one of many possible fragments within a more complex recommendation for a query, expression or pattern. 
    /// It represents one option among many alteranatives to be chosen by the user, that is, it is a coordinate along one dimension. 
    /// It is independent of the type of recommendation and this type can be stored in a field as enumeration. 
    /// If it is necessary to develop a more specific fragment the this class has to be extended by a subclass. 
    /// </summary>
    public class RecommendedFragment<T>
    {
        // Constant parameters
        public T Fragment { get; set; } // It is the fragment itself. It can be an expression, set, dimension, function etc.
        public double Relevance { get; set; } // Unconditional (initial) weight between 0 and 1. Generated by the suggestion procedure. 
        public int Index { get; set; } // It is the order/rank according to the relevance. 0 index corresponds to highest (best) relevance. 

        private string _displayName;
        public string DisplayName // Shown in List view. Can be generated from expression or the object, or set explicitly
        {
            get
            {
                if (_displayName != null) return _displayName;

                if (typeof(T) == typeof(string)) return (string)(object)Fragment;
                else if (typeof(T) == typeof(Set)) return ((Set)(object)Fragment).Name;
                else if (typeof(T) == typeof(Dim)) return ((Dim)(object)Fragment).Name;
                else if (typeof(T) == typeof(List<Dim>))
                {
                    string name = "";
                    ((List<Dim>)(object)Fragment).ForEach(seg => name += "." + seg.Name);
                    return name;
                }
                else if (typeof(T) == typeof(DimPath))
                {
                    return ((DimPath)(object)Fragment).ComplexName;
                }
                else if (typeof(T) == typeof(DimTree))
                {
                    return ((DimTree)(object)Fragment).Dim.LesserSet.Name + ":" + ((DimTree)(object)Fragment).Dim.Name + ":" + ((DimTree)(object)Fragment).Set.Name;
                }
                else return "<UNKNOWN>";
            }
            set { _displayName = value; }
        }

        // UI (variable)
        public bool IsSelected { get; set; }
        public bool IsRelevant { get; set; } // Can be selected under the current constraints
        public double CurrentRelevance { get; set; } // Conditional relevance/weight depending on the curerent selection and other factors. 0 means that the component is disabled. 

        // We probably need support for soring items according their current weight, that is, getting first, second etc. Maybe some enumerator or support via ListView/WPF (filter/sorting)
        public int CurrentIndex { get; set; } // What entry number corresponds to this position. 0 for highest relevance. 

        // We might also provide support for coarse-grained categorization like HIGH, MID, LOW, DISABLED (so IsDisabled is a special case). The categories are defined by static parameters or dynamically calculated (equal intervals etc.)

        // We might also provide support for visualization like colors and icons. 

        public RecommendedFragment(T component, double relevance)
        {
            Fragment = component;
            Relevance = relevance;

            IsSelected = false;
            IsRelevant = true;
            CurrentRelevance = Relevance;
        }
    }

    public class Fragments<T>
    {
        public string Name { get; set; }
        public List<RecommendedFragment<T>> Alternatives { get; set; }
        public bool Readonly { get; set; } // Whether the selection can be changed from UI

        public bool IsSelected
        {
            get { return Alternatives.Exists(f => f.IsSelected); }
        }

        public RecommendedFragment<T> SelectedFragment // The currently chosen alternative (can be null)
        {
            get { return Alternatives.FirstOrDefault(f => f.IsSelected); }
            set
            {
                Alternatives.ForEach(f => f.IsSelected = false); // Reset the current selection
                if (value == null) return; // No selected element

                int sel = -1;
                sel = Alternatives.IndexOf(value); // Find element to be selected
                if (sel < 0) // Item does not exist. Add it.
                {
                    Alternatives.Add(value);
                }

                value.IsSelected = true;

                // TODO: Make selection in the selected node (symmetric selection).  Simply add the same fragment?
            }
        }

        public T SelectedObject
        {
            get { return SelectedFragment == null ? default(T) : SelectedFragment.Fragment; }
            set
            {
                Alternatives.ForEach(f => f.IsSelected = false); // Reset the current selection
                if (value == null) return; // No selected element

                RecommendedFragment<T> selFrag = Alternatives.FirstOrDefault(f => EqualityComparer<T>.Default.Equals(f.Fragment, value)); // Find element to be selected
                if (selFrag == null) // Item does not exist. Add it.
                {
                    selFrag = new RecommendedFragment<T>(value, 1.0);
                    Alternatives.Add(selFrag);
                }

                selFrag.IsSelected = true;
            }
        }

        public void SelectBest() // Select the best fragment with the highest relevance which is not disabled (that is, compatible with other selections)
        {
            var relevantAlternatives = Alternatives.Where(f => f.IsRelevant == true);
            double bestRelevance = relevantAlternatives.Max(f => f.Relevance);
            RecommendedFragment<T> bestFragment = relevantAlternatives.First(f => f.Relevance == bestRelevance);
            SelectedFragment = bestFragment; // Can be null which means reset selection
        }

        public Fragments()
        {
            Alternatives = new List<RecommendedFragment<T>>();
        }
    }

}

