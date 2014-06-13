using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Com.Model
{

    /// <summary>
    /// Generate set mappings. It is part of assistence engine. 
    /// This class knows about mappings between primitive sets from different type systems. 
    /// And it can derive mappings for more complex sets. 
    /// </summary>
    public class Mapper
    {
        public List<Mapping> Mappings { get; private set; } // Use for caching intermediate results (for greater sets)

        // Mapping parameters: thresholds, weights, algorithm options

        public double SetCreationThreshold { get; set; }

        public double MinPathSimilarity { get; set; } // Do not consider path matches with lower similarity
        public int MaxPossibleTargetPaths { get; set; } // Consider only this number of (best) possible target paths for each source paths
        public double MinSourcePathsMatched { get; set; } // How many source dims are mapped in percent
        public double MinSetMappingQuality { get; set; } // Do not return mappings with lower quality
        public int MaxMappingsToBuild { get; set; } // Size of the search space. Do not build more potential mappings.

        public Set GetBestTargetSet(Set sourceSet, SetTop targetSchema) // Find target in the cache
        {
            Mapping bestMapping = GetBestMapping(sourceSet, targetSchema);
            return bestMapping == null ? null : bestMapping.TargetSet;
        }
        public Set GetBestSourceSet(SetTop sourceSchema, Set targetSet)
        {
            Mapping bestMapping = GetBestMapping(sourceSchema, targetSet);
            return bestMapping == null ? null : bestMapping.TargetSet;
        }

        public Mapping GetBestMapping(Set sourceSet, SetTop targetSchema) // Find best mapping in the cache
        {
            Mapping bestMapping = null;
            var setMappings = Mappings.Where(m => m.SourceSet == sourceSet && (m.TargetSet.Top == null || m.TargetSet.Top == targetSchema)); // Find available mappings

            if (setMappings.Count() > 0)
            {
                double bestSimilarity = setMappings.Max(m => m.Similarity);
                bestMapping = setMappings.First(m => m.Similarity == bestSimilarity);
            }

            return bestMapping;
        }
        public Mapping GetBestMapping(SetTop sourceSchema, Set targetSet)
        {
            Mapping bestMapping = GetBestMapping(targetSet, sourceSchema);
            bestMapping.Invert();
            return bestMapping;
        }

        public List<Mapping> MapPrimitiveSet(SetPrimitive sourceSet, SetTop targetSchema)
        {
            SetTop sourceSchema = sourceSet.Top;
            List<Mapping> maps = new List<Mapping>();
            SetPrimitive targetSet;

            if (sourceSchema.GetType() == typeof(SetTop)) // SetTop -> *
            {
                if (targetSchema.GetType() == typeof(SetTop)) // SetTop -> SetTop
                {
                    targetSet = targetSchema.GetPrimitiveSubset(sourceSet.Name);
                    Mapping map = new Mapping(sourceSet, targetSet);
                    map.Similarity = 1.0;
                    maps.Add(map);
                }
                else if (targetSchema.GetType() == typeof(SetTopOledb)) // SetTop -> SetTopOledb
                {
                    throw new NotImplementedException();
                }
            }
            else if (sourceSchema is SetTopOledb) // SetTopOledb -> *
            {
                if (targetSchema.GetType() == typeof(SetTop)) // SetTopOledb -> SetTop
                {
                    OleDbType sourceType = (OleDbType)sourceSet.DataType;
                    CsDataType? targetType;

                    // Mappings: 
                    // http://msdn.microsoft.com/en-us/library/system.data.oledb.oledbtype(v=vs.110).aspx
                    // http://msdn.microsoft.com/en-us/library/cc668759(v=vs.110).aspx
                    switch (sourceType)
                    {                        // Integers
                        case OleDbType.BigInt: // DBTYPE_I8 -> Int64
                        case OleDbType.Integer: // DBTYPE_I4 -> Int32
                        case OleDbType.SmallInt: // DBTYPE_I2 -> Int16
                        case OleDbType.TinyInt: // DBTYPE_I1 -> SByte
                        case OleDbType.UnsignedBigInt: // DBTYPE_UI8 -> UInt64
                        case OleDbType.UnsignedInt: // DBTYPE_UI4 -> UInt32
                        case OleDbType.UnsignedSmallInt: // DBTYPE_UI2 -> UInt16
                        case OleDbType.UnsignedTinyInt: // DBTYPE_UI1 -> Byte
                            targetType = CsDataType.Integer;
                            break;

                        // Double
                        case OleDbType.Double: // DBTYPE_R8
                        case OleDbType.Single: // DBTYPE_R4 -> Single
                            targetType = CsDataType.Double;
                            break;

                        // Decimal
                        case OleDbType.Currency: // DBTYPE_CY
                        case OleDbType.Decimal: // DBTYPE_DECIMAL
                        case OleDbType.Numeric: // DBTYPE_NUMERIC
                        case OleDbType.VarNumeric:
                            targetType = CsDataType.Decimal;
                            break;

                        // Boolean
                        case OleDbType.Boolean: // DBTYPE_BOOL
                            targetType = CsDataType.Boolean;
                            break;

                        // DateTime
                        case OleDbType.Date: // DBTYPE_DATE
                        case OleDbType.DBDate: // DBTYPE_DBDATE
                        case OleDbType.DBTime: // DBTYPE_DBTIME ->  TimeSpan
                        case OleDbType.DBTimeStamp: // DBTYPE_DBTIMESTAMP
                        case OleDbType.Filetime: // DBTYPE_FILETIME
                            targetType = CsDataType.DateTime;
                            break;

                        // Strings
                        case OleDbType.BSTR: // DBTYPE_BSTR
                        case OleDbType.Char: // DBTYPE_STR
                        case OleDbType.LongVarChar: // 
                        case OleDbType.LongVarWChar: //
                        case OleDbType.VarChar: //
                        case OleDbType.VarWChar: //
                        case OleDbType.WChar: // DBTYPE_WSTR
                            targetType = CsDataType.String;
                            break;

                        // Binary
                        case OleDbType.Binary: // DBTYPE_BYTES -> Array of type Byte
                        case OleDbType.LongVarBinary: // Array of type Byte
                        case OleDbType.VarBinary: // Array of type Byte

                        // NULL
                        case OleDbType.Empty: // DBTYPE_EMPTY

                        case OleDbType.Guid: // DBTYPE_GUID -> Guid
                        case OleDbType.Error: // DBTYPE_ERROR -> Exception
                        case OleDbType.IDispatch: // DBTYPE_IDISPATCH -> Object
                        case OleDbType.IUnknown: // DBTYPE_UNKNOWN -> Object

                        case OleDbType.PropVariant: // DBTYPE_PROP_VARIANT -> Object
                        case OleDbType.Variant: // DBTYPE_VARIANT -> Object
                            targetType = null;
                            break;

                        default:
                            targetType = null;
                            break;
                    }

                    targetSet = targetSchema.GetPrimitiveSubset(targetType.ToString());

                    Mapping map = new Mapping(sourceSet, targetSet);
                    map.Similarity = 1.0;
                    maps.Add(map);
                }
                else if (targetSchema.GetType() == typeof(SetTopOledb)) // SetTopOledb -> SetTopOledb
                {
                    targetSet = targetSchema.GetPrimitiveSubset(sourceSet.Name);
                    Mapping map = new Mapping(sourceSet, targetSet);
                    map.Similarity = 1.0;
                    maps.Add(map);
                }
            }

            Mappings.AddRange(maps);
            return maps;
        }
        public List<Mapping> MapPrimitiveSet(SetTop sourceSchema, SetPrimitive targetSet)
        {
            List<Mapping> maps = MapPrimitiveSet(targetSet, sourceSchema);
            maps.ForEach(m => Mappings.Remove(m));
            
            maps.ForEach(m => m.Invert());
            Mappings.AddRange(maps);
            return maps;
        }

        /// <summary>
        /// Generate best mappings from the specified source set to all possible target sets in the specified schema. 
        /// Best mappings from the source greater sets will be (re)used and created if they do not already exist in the mapper. 
        /// </summary>
        public List<Mapping> MapSet(Set sourceSet, SetTop targetSchema)
        {
            if (sourceSet.IsPrimitive) return MapPrimitiveSet((SetPrimitive)sourceSet, targetSchema);
            SetTop sourceSchema = sourceSet.Top;
            List<Mapping> maps = new List<Mapping>();

            Dictionary<Dim, Mapping> greaterMappings = new Dictionary<Dim, Mapping>();

            //
            // 1. Find target greater sets. They are found among mappings and hence can contain both existing (in the schema) and new sets. 
            //
            List<Set> targetGreaterSets = new List<Set>();

            foreach (Dim sd in sourceSet.GreaterDims)
            {
                Mapping gMapping = GetBestMapping(sd.GreaterSet, targetSchema);

                if (gMapping == null) // Either does not exist or cannot be built (for example, formally not possible or meaningless)
                {
                    MapSet(sd.GreaterSet, targetSchema); // Recursion up to primitive sets if not computed and stored earlier
                    gMapping = GetBestMapping(sd.GreaterSet, targetSchema); // Try again after generation
                }

                greaterMappings.Add(sd, gMapping);

                targetGreaterSets.Add(gMapping != null ? gMapping.TargetSet : null);
            }

            //
            // 2. Now find the best (existing) lesser set for the target greater sets. The best set should cover most of them by its greater dimensions
            //
            List<Set> allTargetSets = targetSchema.GetAllSubsets();
            double[] coverage = new double[allTargetSets.Count];
            double maxCoverage = 0;
            int maxCoverageIndex = -1;

            for (int i = 0; i < allTargetSets.Count; i++)
            {
                // Find coverage of this target set (how many best greater target sets it covers)
                coverage[i] = 0;
                foreach (Set tgs in allTargetSets[i].GetGreaterSets())
                {
                    if (!targetGreaterSets.Contains(tgs)) continue;

                    // TODO: Compare dimension names and then use it as a weight [0,1] instead of simply incrementing
                    coverage[i] += 1;
                }
                coverage[i] /= targetGreaterSets.Count; // Normalize to [0,1]
                if (coverage[i] > 1) coverage[i] = 1; // A lesser set can use (reference, cover) a greater set more than once

                // Take into account individual similarity of the target set with the source set
                double nameSimilarity = StringSimilarity.ComputeStringSimilarity(sourceSet.Name, allTargetSets[i].Name, 3);
                coverage[i] *= nameSimilarity;

                // TODO: Take into account difference in max ranks

                if (coverage[i] > maxCoverage)
                {
                    maxCoverage = coverage[i];
                    maxCoverageIndex = i;
                }
            }

            //
            // 3. Create and store a mapping (or several mappings) 
            //
            Mapping newMapping = null;
            if (maxCoverage < SetCreationThreshold) // Create new target set for mapping (and its greater dimensions) which will be accessible only via the mapping object (not via the schema)
            {
                Set ts = new Set(sourceSet.Name); // New set has the same name as the soure set

                newMapping = new Mapping(sourceSet, ts);

                foreach (Dim sd in sourceSet.GreaterDims) // For each source dimension, create one new target dimension 
                {
                    Mapping gMapping = greaterMappings[sd];
                    Set gts = gMapping.TargetSet;

                    Dim td = gts.CreateDefaultLesserDimension(sd.Name, ts); // Create a clone for the source dimension
                    td.IsIdentity = sd.IsIdentity;

                    newMapping.AddPaths(sd, td, gMapping); // Add a pair of dimensions as a match (with expansion using the specified greater mapping)
                }

                newMapping.Similarity = 1.0;
                maps.Add(newMapping);
            }
            else // Use existing target set(s) for mapping(s)
            {
                Set ts = allTargetSets[maxCoverageIndex];

                newMapping = new Mapping(sourceSet, ts);

                foreach (Dim sd in sourceSet.GreaterDims) // For each source dimension, find best target dimension 
                {
                    Mapping gMapping = greaterMappings[sd];
                    Set gts = gMapping.TargetSet;

                    // Find an existing dimension from ts to gts with the best similarity to source dim sd
                    Dim td = null;
                    var tDims = ts.GreaterDims.Where(d => d.GreaterSet == gts); // All target dimensions from ts to gts
                    if (tDims != null && tDims.Count() > 0)
                    {
                        // TODO: In fact, we need to choose the best dimension, for example, by comparing their names, usages, ranks and other semantic factors
                        td = tDims.ToList()[0];
                    }

                    if (td == null) // No good target dimension found (the source dimension is not covered)
                    {
                        continue; // TODO: Maybe create a new target dimension rather than simply ingnoring it
                    }

                    td.IsIdentity = sd.IsIdentity;

                    newMapping.AddPaths(sd, td, gMapping); // Add a pair of dimensions as a match (with expansion using the specified greater mapping)
                }

                newMapping.Similarity = maxCoverage;
                maps.Add(newMapping);
            }

            Mappings.AddRange(maps);
            return maps;
        }
        public List<Mapping> MapSet(SetTop sourceSchema, Set targetSet)
        {
            if (targetSet.IsPrimitive) return MapPrimitiveSet(sourceSchema, (SetPrimitive)targetSet);

            List<Mapping> maps = MapSet(targetSet, sourceSchema);
            maps.ForEach(m => Mappings.Remove(m));

            maps.ForEach(m => m.Invert());
            Mappings.AddRange(maps);
            return maps;
        }

        /// <summary>
        /// Build mappings from the source set to the target set. The sets are greater sets of the specified dimensions. 
        /// The mapping should take into account (semantically) that these sets are used from these dimensions. 
        /// </summary>
        public List<Mapping> MapDim(DimPath sourcePath, DimPath targetPath)
        {
            // We analize all continuations of the specified prefix paths
            List<DimPath> sourcePaths = sourcePath.GreaterSet.GetGreaterPrimitiveDims(DimensionType.IDENTITY_ENTITY).ToList();
            sourcePaths.ForEach(p => p.InsertFirst(sourcePath));
            if (sourcePaths.Count == 0) sourcePaths.Add(sourcePath);

            List<DimPath> targetPaths = targetPath.GreaterSet.GetGreaterPrimitiveDims(DimensionType.IDENTITY_ENTITY).ToList();
            targetPaths.ForEach(p => p.InsertFirst(targetPath));
            if (targetPaths.Count == 0) targetPaths.Add(targetPath);

            List<Mapping> mappings = new List<Mapping>();

            int dimCount = sourcePaths.Count();

            var matches = new List<Tuple<DimPath, List<DimPath>>>(); // List of: <srcPath, targetPaths>
            int[] lengths = new int[dimCount]; // Each dimension has some length (some valid target paths)
            for (int i = 0; i < dimCount; i++)
            {
                DimPath sp = sourcePaths[i];
                List<DimPath> tps = new List<DimPath>();

                // Sort target paths according to their similarity
                tps.AddRange(targetPaths);
                tps = tps.OrderByDescending(p => StringSimilarity.ComputePathSimilarity(sp, p)).ToList();
                if (tps.Count > MaxPossibleTargetPaths) // Leave only top n target paths with the best similarity
                {
                    tps.RemoveRange(MaxPossibleTargetPaths, tps.Count - MaxPossibleTargetPaths);
                }

                // TODO: Cut the tail with similarity less than MinPathSimilarity

                matches.Add(Tuple.Create(sp, tps));
                lengths[i] = tps.Count;
            }

            int[] offsets = new int[dimCount]; // Here we store the current state of choices for each dimensions (target path number)
            for (int i = 0; i < dimCount; i++) offsets[i] = -1;

            int top = -1; // The current level/top where we change the offset. Depth of recursion.
            do ++top; while (top < dimCount && lengths[top] == 0);

            int mappingsBuilt = 0; // The number of all hypothesis (mappings) built and checked

            Func<int, Mapping> BuildSetMapping = delegate(int sourcePathCount)
            {
                bool withPrefix = true;
                Mapping mapping;
                if (withPrefix)
                {
                    mapping = new Mapping(sourcePath.LesserSet, targetPath.LesserSet);
                }
                else
                {
                    mapping = new Mapping(sourcePath.GreaterSet, targetPath.GreaterSet);
                }

                for (int i = 0; i < sourcePathCount; i++)
                {
                    if (offsets[i] < 0 || offsets[i] >= lengths[i]) continue;

                    DimPath sp = matches[i].Item1;
                    if (!withPrefix) sp.RemoveFirst();
                    DimPath tp = matches[i].Item2[offsets[i]];
                    if (!withPrefix) tp.RemoveFirst();

                    mapping.AddMatch(new PathMatch(sp, tp));
                }

                return mapping;
            };

            while (top >= 0)
            {
                if (top == dimCount) // Element is ready. Process new element.
                {
                    if (++mappingsBuilt > MaxMappingsToBuild) break;

                    // Check coverage. However many source paths have been assigned a non-null target path
                    double coverage = 0;
                    for (int i = 0; i < top; i++) 
                        if (offsets[i] >= 0 && offsets[i] < lengths[i]) coverage += 1;

                    coverage /= dimCount;

                    if (coverage >= MinSourcePathsMatched)
                    {
                        // Evaluate the whole mapping (aggregated quality with coverage and other parameters)
                        Mapping currentMapping = BuildSetMapping(top);

                        currentMapping.ComputeSimilarity();
                        currentMapping.Similarity *= coverage;
                        if (currentMapping.Similarity >= MinSetMappingQuality)
                        {
                            mappings.Add(currentMapping);
                        }
                    }

                    top--;
                    while (top >= 0 && (offsets[top] >= lengths[top] || lengths[top] == 0)) // Go up by skipping finished and empty dimensions
                    { offsets[top--] = -1; } 
                }
                else // Find the next valid offset
                {
                    Mapping currentMapping = BuildSetMapping(top);

                    for (offsets[top]++; offsets[top] < lengths[top]; offsets[top]++)
                    {
                        DimPath sp = matches[top].Item1;
                        DimPath tp = matches[top].Item2[offsets[top]]; // New target path

                        bool canUse = true;

                        // Check if it has not been already used as a target for previous paths
                        for (int i = 0; i < top; i++)
                        {
                            if (offsets[i] < 0 || offsets[i] >= lengths[i]) continue;
                            DimPath usedtp = matches[i].Item2[offsets[i]]; // Used target path (by i-th source path)
                            if (usedtp == tp) { canUse = false; break; }
                        }
                        if (!canUse) continue;

                        canUse = currentMapping.Compatible(new PathMatch(sp, tp));
                        if (!canUse) continue;

                        break; // Found
                    }

                    // Offset chosen. Go foreward by skipping empty dimensions.
                    top++;
                    while (top < dimCount && (offsets[top] >= lengths[top] || lengths[top] == 0)) // Go up (foreward) by skipping finished and empty dimensions
                    { top++; }
                }
            }

            mappings = mappings.OrderByDescending(m => m.Similarity).ToList();

            // Remove prefixes
            foreach (Mapping m in mappings)
            {
                m.RemoveFirst(sourcePath, targetPath);
            }

            Mappings.AddRange(mappings);
            return mappings;
        }

        /// <summary>
        /// Import the specified set along with all its greater sets. 
        /// The set is not populated but is ready to be populated. 
        /// It is a convenience method simplifying a typical operation. 
        /// </summary>
        public static Set ImportSet(Set sourceSet, SetTop targetSchema)
        {
            Mapper mapper = new Mapper();
            mapper.SetCreationThreshold = 1.0;
            mapper.MapSet(sourceSet, targetSchema);
            Mapping mapping = mapper.GetBestMapping(sourceSet, targetSchema);
            mapping.AddTargetToSchema(targetSchema);

            Dim dimImport = new Dim(mapping);
            dimImport.Add();
            dimImport.GreaterSet.ProjectDimensions.Add(dimImport);

            // TO DELETE
            //DimImport dimImport = new DimImport(mapping);
            //dimImport.Add();

            return mapping.TargetSet;
        }

        public Mapper()
        {
            Mappings = new List<Mapping>();
            SetCreationThreshold = 1.0;
        
            MinPathSimilarity = 0.1;
            MaxPossibleTargetPaths = 3;
            MinSourcePathsMatched = 0.2;
            MinSetMappingQuality = 0.0;
            MaxMappingsToBuild = 1000;
        }
    }
    
    /// <summary>
    /// The class describes one mapping between two concrete sets as a collection of path pairs. 
    /// </summary>
    public class Mapping
    {
        public List<PathMatch> Matches { get; private set; }

        public double Similarity { get; set; }

        private Set _sourceSet;
        public Set SourceSet 
        {
            get { return _sourceSet; }
            set
            {
                if (_sourceSet == null) { _sourceSet = value; return; }
                if (_sourceSet == value) return;

                List<DimPath> lesserSegs = value.GetGreaterPaths(_sourceSet);
                List<DimPath> greaterSegs = _sourceSet.GetGreaterPaths(value);

                if (lesserSegs != null && lesserSegs.Count > 0) // New set is a lesser set for the current
                {
                    foreach (PathMatch m in Matches) // Insert new segments
                    {
                        m.SourcePath.InsertFirst(lesserSegs[0]);
                    }
                }
                else if (greaterSegs != null && greaterSegs.Count > 0) // New set is a greater set for the current
                {
                    foreach (PathMatch m in Matches) // Remove segments
                    {
                        m.SourcePath.RemoveFirst(greaterSegs[0].GreaterSet);
                    }
                }
                else // Otherwise
                {
                    Matches.Clear();
                }

                _sourceSet = value;
            }
        }

        private Set _targetSet;
        public Set TargetSet
        {
            get { return _targetSet; }
            set
            {
                if (_targetSet == null) { _targetSet = value; return; }
                if (_targetSet == value) return;

                List<DimPath> lesserSegs = value.GetGreaterPaths(_targetSet);
                List<DimPath> greaterSegs = _targetSet.GetGreaterPaths(value);

                if (lesserSegs != null && lesserSegs.Count > 0) // New set is a lesser set for the current
                {
                    foreach (PathMatch m in Matches) // Insert new segments
                    {
                        m.TargetPath.InsertFirst(lesserSegs[0]);
                    }
                }
                else if (greaterSegs != null && greaterSegs.Count > 0) // New set is a greater set for the current
                {
                    foreach (PathMatch m in Matches) // Remove segments
                    {
                        m.TargetPath.RemoveFirst(greaterSegs[0].GreaterSet);
                    }
                }
                else // Otherwise
                {
                    Matches.Clear();
                }

                _targetSet = value;
            }
        }

        public bool IsSourcePathValid(DimPath path)
        {
            if (path.LesserSet != SourceSet && !path.LesserSet.IsGreater(SourceSet)) return false;
            return true;
        }
        public bool IsTargetPathValid(DimPath path)
        {
            if (path.LesserSet != TargetSet && !path.LesserSet.IsGreater(TargetSet)) return false;
            return true;
        }

        public bool Compatible(PathMatch match) // Determine if the specified match does not contradict to this mapping and can be added to it without removing existing matches
        {
            foreach (PathMatch m in Matches)
            {
                if (!m.Compatible(match)) return false;
            }
            return true; // Compatible and can be added
        }

        public double ComputeSimilarity()
        {
            double sum = 0.0;
            foreach (PathMatch m in Matches)
            {
                m.Similarity = StringSimilarity.ComputePathSimilarity(m.SourcePath, m.TargetPath);
                sum += m.Similarity;
            }

            if (Matches.Count > 0) Similarity = sum / Matches.Count;
            else Similarity = 0.0;

            return Similarity;
        }

        public bool AreMatched(PathMatch match) // Determine if both paths are matched (true if there exist the same or more specific match)
        {
            foreach (PathMatch m in Matches)
            {
                if (m.Matches(match)) return true;
            }
            return false; // Not found
        }

        public PathMatch GetMatchForSource(DimPath path) // Find a match with this path
        {
            foreach(PathMatch m in Matches) 
            {
                if (m.MatchesSource(path)) return m;
            }
            return null;
        }

        public PathMatch GetMatchForTarget(DimPath path) // Find a match with this path
        {
            foreach (PathMatch m in Matches)
            {
                if (m.MatchesTarget(path)) return m;
            }
            return null;
        }

        public void AddPaths(Dim sd, Dim td, Mapping gMapping) // Add this pair by expanding it using the mapping
        {
            Debug.Assert(sd != null && sd.LesserSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(td != null && td.LesserSet == TargetSet, "Wrong use: target path must start from the target set.");

            Debug.Assert(sd != null && sd.GreaterSet == gMapping.SourceSet, "Wrong use: source path must end where the mapping starts.");
            Debug.Assert(td != null && td.GreaterSet == gMapping.TargetSet, "Wrong use: target path must end where the mapping starts.");

            if (gMapping.Matches.Count == 0) // If there are no continuations then add only the starting segments (for example, for mappings between primitive sets)
            {
                DimPath sp = new DimPath(); // A path consists of one segment
                sp.InsertLast(sd);

                DimPath tp = new DimPath(); // A path consists of one segment
                tp.InsertLast(td);

                PathMatch match = new PathMatch(sp, tp);
                Matches.Add(match);
            }

            foreach (PathMatch gMatch in gMapping.Matches)
            {
                DimPath sp = new DimPath(); // Create source path by concatenating one segment and continuation path from the mapping
                sp.InsertLast(sd);
                sp.InsertLast(gMatch.SourcePath);

                DimPath tp = new DimPath(); // Create target path by concatenating one segment and continuation path from the mapping
                tp.InsertLast(td);
                tp.InsertLast(gMatch.TargetPath);

                PathMatch match = new PathMatch(sp, tp);
                Matches.Add(match);
            }
        }

        public void AddMatch(PathMatch match)
        {
            Debug.Assert(match != null && match.SourceSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(match != null && match.TargetSet == TargetSet, "Wrong use: target path must start from the target set.");


            // In the case of coverage we do nothing. We want to keep more general matches (between sets) while more specific can be added/removed independently (if they satisfy them). 
            // ??? in the case of no coverage, they have a point of intersection. Then - ? Should this point of intersection be within the seconad path or otherwise constrained?
            // Problem: two sets can be matched explcitly or implicitly, that is, their match logically follows from other path matches.
            // Implicit match: intersections of two paths produce a more general match
            // Alternative approach: Incrementally add path segments and checking consistency conditions

            List<PathMatch> toRemove = new List<PathMatch>();
            foreach (PathMatch m in Matches)
            {
                if (!m.Compatible(match))
                {
                    toRemove.Add(m);
                }
            }

            Matches.Add(match); // Now it is guaranteed to be compatible with all existing matches
        }

        public void AddMatches(List<PathMatch> matches) // Import all path matches (as new objects) that fit into this mapping
        {
            foreach (PathMatch m in matches)
            {
                PathMatch match = new PathMatch(m);
                AddMatch(match);
            }
        }

        public void RemoveMatch(DimPath sourcePath, DimPath targetPath) // Remove the specified and all more specific matches (continuations)
        {
            Debug.Assert(sourcePath.LesserSet == SourceSet, "Wrong use: source path must start from the source set.");
            Debug.Assert(targetPath.LesserSet == TargetSet, "Wrong use: target path must start from the target set.");

            List<PathMatch> toRemove = new List<PathMatch>();
            foreach (PathMatch m in Matches)
            {
                // If existing match is the same or more specific than the specified match to be removed
                if (m.MatchesSource(sourcePath) && m.MatchesTarget(targetPath)) 
                {
                    toRemove.Add(m);
                }
            }

            toRemove.ForEach(m => Matches.Remove(m));
        }

        public void InsertFirst(DimPath sourcePath, DimPath targetPath)
        {
            if (sourcePath != null)
            {
                _sourceSet = sourcePath.LesserSet;
                Matches.ForEach(m => m.SourcePath.InsertFirst(sourcePath));
            }
            if (targetPath != null)
            {
                _targetSet = targetPath.LesserSet;
                Matches.ForEach(m => m.TargetPath.InsertFirst(targetPath));
            }
        }
        public void RemoveFirst(DimPath sourcePath, DimPath targetPath)
        {
            if (sourcePath != null)
            {
                _sourceSet = sourcePath.GreaterSet;
                Matches.ForEach(m => m.SourcePath.RemoveFirst(sourcePath));
            }
            if (targetPath != null)
            {
                _targetSet = targetPath.GreaterSet;
                Matches.ForEach(m => m.TargetPath.RemoveFirst(targetPath));
            }
        }

        public void Invert() // Invert source and target
        {
            var tempSet = _sourceSet;
            _sourceSet = _targetSet;
            _targetSet = tempSet;

            Matches.ForEach(m => m.Invert());
        }

        public DimTree GetSourceTree() // Convert all source paths into a dimension tree where the source set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(SourceSet);
            Matches.ForEach(p => tree.AddPath(p.SourcePath));
            return tree;
        }
        public DimTree GetSourceTree(Set set) // Convert all paths of the specified set into a dimension tree where the source set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(set);
            foreach (PathMatch m in Matches)
            {
                // if(m.SourcePath.LesserSet != set) continue; // Use this if we want to take into account only paths *starting* from this set (rather than crossing this set)
                int index = m.SourcePath.IndexOfLesser(set);
                if (index < 0) continue; // The path does not cross this set

                tree.AddPath(m.SourcePath.SubPath(index));
            }

            return tree;
        }

        public DimTree GetTargetTree() // Convert all target paths into a dimension tree where the target set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(TargetSet);
            Matches.ForEach(p => tree.AddPath(p.TargetPath));
            return tree;
        }
        public DimTree GetTargetTree(Set set) // Convert all target paths into a dimension tree where the target set will be a root. The tree can contain non-existing elements if they are used in the mapping. 
        {
            DimTree tree = new DimTree(set);
            foreach (PathMatch m in Matches)
            {
                // if(m.TargetPath.LesserSet != set) continue; // Use this if we want to take into account only paths *starting* from this set (rather than crossing this set)
                int index = m.TargetPath.IndexOfLesser(set);
                if (index < 0) continue; // The path does not cross this set

                tree.AddPath(m.TargetPath.SubPath(index));
            }

            return tree;
        }

        public Expression GetSourceExpression()
        {
            throw new NotImplementedException();
        }

        public Expression GetTargetExpression() // Build tuple expression where target paths define a tuple and source paths are used leaf expressions in this tuple applied to the source set
        {
            return GetTargetExpression(null);
        }
        public Expression GetTargetExpression(Dim dim) // Build tuple expression for the specified mapped dimension
        {
            // It is mapping from LesserSet to GreaterSet of the dimension
            Debug.Assert(dim == null || dim.LesserSet == SourceSet, "Wrong use: lesser set of the mapped dimension corresponds to the source set of the mapping.");
            Debug.Assert(dim == null || dim.GreaterSet == TargetSet, "Wrong use: greater set of the mapped dimension corresponds to the target set of the mapping.");

            Expression tupleExpr = new Expression(null, Operation.TUPLE, TargetSet);
            if (dim != null)
            {
                tupleExpr.Name = dim.Name;
                tupleExpr.Dimension = dim;
            }

            foreach (PathMatch match in Matches)
            {
                // Leaf expression for accessing a variable (parameter of the function)
                Expression thisExpr = new Expression("this", Operation.DOT, match.SourceSet);

                Expression srcExpr = null;
                DimPath srcPath = match.SourceSet.GetGreaterPath(match.SourcePath.Path); // First, we try to find a direct path/function 
                if (srcPath == null) // No direct path. Use a sequence of simple dimensions
                {
                    srcExpr = Expression.CreateProjectExpression(match.SourcePath.Path, Operation.DOT);
                }
                else // There is a direct path (relation attribute in a relational data source). Use attribute name as function name
                {
                    srcExpr = new Expression(srcPath.Name, Operation.DOT, match.TargetPath.GreaterSet);
                }
                srcExpr.GetInputLeaf().Input = thisExpr;

                Expression leafTuple = tupleExpr.AddPath(match.TargetPath);
                leafTuple.Input = srcExpr;
            }

            return tupleExpr;
        }

        public void AddSourceToSchema(SetTop schema = null)
        {
            throw new NotImplementedException();
        }
        public void AddTargetToSchema(SetTop schema = null) // Ensure that all target elements exist in the specified schema
        {
            // The mapping can reference new elements which are not in the schema yet so we try to find them and add if necessary

            if (schema == null) // Find the schema from the mapping elements
            {
                PathMatch match = Matches.FirstOrDefault(m => m.TargetPath.GreaterSet.IsPrimitive);
                schema = match != null ? match.TargetPath.GreaterSet.Top : null; // We assume that primitive sets always have root defined (other sets might not have been added yet).
            }

            DimTree tree = GetTargetTree();
            tree.AddToSchema(schema);
        }

        public override string ToString()
        {
            return String.Format("{0} -> {1}. Similarity={2}. Matches={3}", SourceSet.Name, TargetSet.Name, Similarity, Matches.Count);
        }

        public Mapping(Set sourceSet, Set targetSet)
        {
            // Debug.Assert((sourceSet.IsPrimitive && targetSet.IsPrimitive) || (!sourceSet.IsPrimitive && !targetSet.IsPrimitive), "Wrong use: cannot create a mapping between a primitive set and a non-primitive set.");
            Debug.Assert(sourceSet != null && targetSet != null, "Wrong use: parametes cannot be null.");

            Matches = new List<PathMatch>();

            SourceSet = sourceSet;
            TargetSet = targetSet;
        }

    }

    /// <summary>
    /// A pair of matching paths.
    /// </summary>
    public class PathMatch
    {
        public DimPath SourcePath { get; private set; }
        public DimPath TargetPath { get; private set; }
        public double Similarity { get; set; }

        public Set SourceSet { get { return SourcePath == null ? null : SourcePath.LesserSet; } }
        public Set TargetSet { get { return TargetPath == null ? null : TargetPath.LesserSet; } }

        public bool MatchesSource(DimPath path) // This is more specific (longer) than argument
        {
            return SourcePath.StartsWith(path);
        }

        public bool MatchesTarget(DimPath path) // This is more specific (longer) than argument
        {
            return TargetPath.StartsWith(path);
        }

        public bool Matches(PathMatch match) // This is more specific (longer) than argument
        {
            return MatchesSource(match.SourcePath) && MatchesTarget(match.TargetPath);
        }

        public void Invert() // Invert source and target
        {
            var tempPath = SourcePath;
            SourcePath = TargetPath;
            TargetPath = tempPath;
        }

        public bool Compatible(PathMatch match) // The specified match is compatible with (does not contradicts to) this match
        {
            // Main rule: if a coverage condition holds for one path (in one or another direction) then it must hold for the other path (in the same direction)

            // SourcePath -> TargetPath
            if (match.SourcePath.StartsWith(SourcePath)) // The specified match continues this path
            {
                return match.TargetPath.StartsWith(TargetPath); // The same must be true for the other paths
            }
            else if (SourcePath.StartsWith(match.SourcePath)) // Opposite
            {
                return TargetPath.StartsWith(match.TargetPath); // The same must be true for the other paths
            }

            // TargetPath -> SourcePath
            if (match.TargetPath.StartsWith(TargetPath))
            {
                return match.SourcePath.StartsWith(SourcePath);
            }
            else if (TargetPath.StartsWith(match.TargetPath))
            {
                return SourcePath.StartsWith(match.SourcePath);
            }
            
            // Neither source path nor target paths cover. 
            // This means they have some intersection (meet set they both continue). 
            // This meet point defines an implicit match which we do not check but it might contradict to other matches.

            return true;
        }

        public PathMatch(PathMatch m)
        {
            SourcePath = new DimPath(m.SourcePath);
            TargetPath = new DimPath(m.TargetPath);
            Similarity = m.Similarity;
        }

        public PathMatch(DimPath sourcePath, DimPath targetPath)
        {
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Similarity = 1.0;
        }

        public PathMatch(DimPath sourcePath, DimPath targetPath, double similarity)
            : this(sourcePath, targetPath)
        {
            Similarity = 1.0;
        }
    }

    /// <summary>
    /// It stores all necessary information for editing a mapping and the current state of mapping. 
    /// </summary>
    public class MappingModel
    {
        public MatchTree SourceTree { get; private set; }
        public MatchTree TargetTree { get; private set; }

        public Mapping Mapping { get; set; } // It is the current state of the mapping. And it is what is initialized and returned. 

        private Set _sourceSet;
        public Set SourceSet 
        {
            get { return _sourceSet; }
            set 
            {
                Debug.Assert(value != null, "Wrong use: a set in mapping cannot be null (use root instead).");
                if (_sourceSet == value) return;
                _sourceSet = value;

                Mapping.SourceSet = SourceSet; // Update mapper

                // Update tree
                SourceTree.Children.Clear();
                MatchTreeNode node = new MatchTreeNode(SourceSet);
                SourceTree.AddChild(node);
                node.ExpandTree();
                node.AddSourcePaths(Mapping);
            }
        }

        private Set _targetSet;
        public Set TargetSet
        {
            get { return _targetSet; }
            set
            {
                Debug.Assert(value != null, "Wrong use: a set in mapping cannot be null (use root instead).");
                if (_targetSet == value) return;
                _targetSet = value;

                Mapping.TargetSet = TargetSet; // Update mapper

                // Update tree
                TargetTree.Children.Clear();
                MatchTreeNode node = new MatchTreeNode(TargetSet);
                node.ExpandTree();
                node.AddTargetPaths(Mapping);
                TargetTree.AddChild(node);
            }
        }

        /// <summary>
        /// Primary: returns true if this node has any match and false otherwise (not assigned). 
        /// Secondary: returns true if this node is matched against the currently selected primary node and false otherwise (so only one node in the whole secondary tree is true).
        /// </summary>
        /// <returns></returns>
        public bool IsMatchedSource()
        {
            if (SourceTree.SelectedPath == null) return false;
            return IsMatchedSource(SourceTree.SelectedPath);
        }
        public bool IsMatchedSource(DimPath path)
        {
            PathMatch match = Mapping.GetMatchForSource(path);

            if (match == null) return false;

            if (SourceTree.IsPrimary)
            {
                return true;
            }
            else
            {
                if (TargetTree.SelectedPath == null) return false;
                return match.MatchesTarget(TargetTree.SelectedPath);
            }
        }

        /// <summary>
        /// Primary: returns true if this node has any match and false otherwise (not assigned). 
        /// Secondary: returns true if this node is matched against the currently selected primary node and false otherwise (so only one node in the whole secondary tree is true).
        /// </summary>
        /// <returns></returns>
        public bool IsMatchedTarget()
        {
            if (TargetTree.SelectedPath == null) return false;
            return IsMatchedTarget(TargetTree.SelectedPath);
        }
        public bool IsMatchedTarget(DimPath path)
        {
            PathMatch match = Mapping.GetMatchForTarget(path);

            if (match == null) return false;

            if (TargetTree.IsPrimary)
            {
                return true;
            }
            else
            {
                if (SourceTree.SelectedPath == null) return false;
                return match.MatchesSource(SourceTree.SelectedPath);
            }
        }

        /// <summary>
        /// Secondary: Enabled/disabled status of a secondary node. Whether the current paths can be added as a new match without contradiction to the existing matches.
        /// Secondary: given a primary currently selected node, compute if a match with this secondary node does not contradict to existing matches (so it can be added). Alternatively, if relevances are precomputed then we find if relevance is higher than 0.
        /// Primary: always true (if there is at least one possible secondary match). 
        /// </summary>
        public bool CanMatchTarget(DimPath path)
        {
            if (TargetTree.IsPrimary)
            {
                return true;
            }
            else
            {
                DimPath priPath = SourceTree.SelectedPath;
                if (priPath == null) return false; // Primary node is not selected

                if (!priPath.IsPrimitive || !path.IsPrimitive) return false; // Only primitive paths can be matchd

                return true;
            }
        }

        public bool CanMatchSource(DimPath path)
        {
            // TODO: Copy-paste when ready
            return true;
        }

        /// <summary>
        /// Primary: does not do anything (or calls the same method of the secondary tree). 
        /// Secondary: takes the currently selected primary node and this secondary node and adds this match to the list. Previous match is deleted. Contradictory matches are removed. Match status of nodes needs to be redrawn.
        /// </summary>
        public PathMatch AddMatch()
        {
            if (SourceTree.SelectedPath == null || TargetTree.SelectedPath == null) return null;

            PathMatch match = new PathMatch(SourceTree.SelectedPath, TargetTree.SelectedPath, 1.0);
            Mapping.AddMatch(match); // Some existing matches (which contradict to the added one) will be removed

            return match;
        }

        /// <summary>
        /// Remove the mathc corresponding to the current selections. 
        /// </summary>
        public void RemoveMatch()
        {
            if (SourceTree.SelectedPath == null || TargetTree.SelectedPath == null) return;

            Mapping.RemoveMatch(SourceTree.SelectedPath, TargetTree.SelectedPath); // Also other matches can be removed
        }
        public MappingModel(Dim sourceDim, Dim targetDim)
            : this(sourceDim.GreaterSet, targetDim.GreaterSet)
        {
            SourceTree.Children[0].Dim = sourceDim;
            TargetTree.Children[0].Dim = targetDim;
        }

        public MappingModel(Set sourceSet, Set targetSet)
        {
            Mapping = new Mapping(sourceSet, targetSet);

            SourceTree = new MatchTree(this);
            SourceTree.IsPrimary = true;
            TargetTree = new MatchTree(this);
            TargetTree.IsPrimary = false;

            SourceSet = sourceSet; // Here also the tree will be constructed
            TargetSet = targetSet;
        }

        public MappingModel(Mapping mapping)
        {
            Mapping = mapping;

            SourceTree = new MatchTree(this);
            SourceTree.IsPrimary = true;
            TargetTree = new MatchTree(this);
            TargetTree.IsPrimary = false;

            SourceSet = mapping.SourceSet; // Here also the tree will be constructed
            TargetSet = mapping.TargetSet;
        }
    }

    /// <summary>
    /// It displays the current state of mapping between two sets as properties of the tree nodes depending on the role of the tree. 
    /// </summary>
    public class MatchTree : MatchTreeNode
    {
        public MappingModel MappingModel { get; set; }

        public bool IsSource { get { return MappingModel.SourceTree == this;  } } // Whether this tree corresponds to source paths in the mappings.
        public bool IsTarget { get { return MappingModel.TargetTree == this; } } // Whether this tree corresponds to target paths in the mappings. 

        public bool IsPrimary { get; set; } // Defines how the node properties are computed and displayed as well as the logic of the tree. 
        public bool OnlyPrimitive { get; set; } // Only primitive dimensions/paths can be matched (not intermediate). So intermediate elemens are not matched (but might display information about matches derived from primitive elements).

        // This is important for generation of the current status: disabled/enabled, relevance etc.
        public MatchTreeNode SelectedNode { get; set; } // Selected in this tree. Bind tree view selected item to this field.
        public DimPath SelectedPath // Transform selected node into valid selected path
        {
            get
            {
                if (SelectedNode == null) return null;
                DimPath selectedPath = SelectedNode.DimPath;

                // Trimm to source or target set of the mapping in the root
                if(IsSource)
                    selectedPath.RemoveFirst(MappingModel.SourceSet);
                else
                    selectedPath.RemoveFirst(MappingModel.TargetSet);

                return selectedPath;
            }
        }

        public MatchTree CounterTree { get { return IsSource ? MappingModel.TargetTree : MappingModel.SourceTree; } } // Another tree

        public MatchTree(MappingModel model)
            : base()
        {
            MappingModel = model;
        }
    }

    /// <summary>
    /// It provides methods and propoerties for a node which depend on the current mappings and role of the tree. 
    /// The class assumes that the root of the tree is a special node storing mappings, tree roles and other necessary data. 
    /// </summary>
    public class MatchTreeNode : DimTree
    {
        public DimPath MappingPath // Trimmed to source or target set of the mapping in the root
        {
            get
            {
                MatchTree root = (MatchTree)Root;
                DimPath path = DimPath;
                if (root.IsSource)
                    path.RemoveFirst(root.MappingModel.SourceSet);
                else
                    path.RemoveFirst(root.MappingModel.TargetSet);
                return path;
            }
        }

        /// <summary>
        /// Primary: either not shown or 1.0 or relevance of the current (existing) match if it exists for this element retrieved from the mapper. 
        /// Secondary: if it is already matched then relevance of existing match (the same as for primary) and if it is not chosen (not current) then the relevance computed by the recommender. 
        /// </summary>
        public double MatchRelevance
        {
            get
            {
                return 1.0;
            }
        }

        public bool IsMatched
        {
            get
            {
                MatchTree root = (MatchTree)Root;
                MappingModel model = root.MappingModel;

                if (root.IsSource) return model.IsMatchedSource(MappingPath);
                else return model.IsMatchedTarget(MappingPath);
            }
        }

        public bool CanMatch
        {
            get
            {
                MatchTree root = (MatchTree)Root;
                MappingModel model = root.MappingModel;

                if (root.IsSource) return model.CanMatchSource(MappingPath);
                else return model.CanMatchTarget(MappingPath);
            }
        }

        public PathMatch AddMatch()
        {
            MatchTree root = (MatchTree)Root;
            return root.MappingModel.AddMatch();
        }

        /// <summary>
        /// Primary: does not do anything (or calls the same method of the secondary tree). 
        /// Secondary: remove the current match so this secondary is node (and the corresponding primary node) are not matched anymore. Works only if the two nodes are currently matched. 
        /// </summary>
        public PathMatch RemoveMatch()
        {
            throw new NotImplementedException();
        }

        public MatchTreeNode(Dim dim, DimTree parent = null)
            : base(dim, parent)
        {
        }

        public MatchTreeNode(Set set, DimTree parent = null)
            : base(set, parent)
        {
        }

        public MatchTreeNode()
            : base()
        {
        }
    }

    public enum MappingDirection
    {
        SOURCE, // Data flow in the direction FROM this set to a target set
        TARGET, // Data flow in the direction TO this element from a source set
    }


    // Check this: "fuzzy string comparisons using trigram cosine similarity"
	// Check this: "TF-IDF cosine similarity between columns"
	// minimum description length (MDL) similar to Jaccard similarity to compare two attributes. This measure computes the ratio of the size of the intersection of two columns' data to the size of their union.
	//   V. Raman and J. M. Hellerstein. Potter's wheel: An interactive data cleaning system. In VLDB, 381-390, 2001.
	// Welch's t-test for a pair of columns that contain numeric values. Given the columns' means and variances, the t-test gives the probability the columns were drawn from the same distribution.
	class StringSimilarity
    {
        public static double ComputeStringSimilarity(string source, string target, int gramlength)
        {
            if (source == null || target == null || source.Length == 0 || target.Length == 0) return 0;

            List<string> sourceGrams = generateNGrams(source, gramlength);
            List<string> targetGrams = generateNGrams(target, gramlength);

            int similarGrams = 0;
            for (int i = 0; i < sourceGrams.Count; i++)
            {
                string s1 = sourceGrams[i];
                for (int j = 0; j < targetGrams.Count; j++)
                {
                    if (s1.Equals(targetGrams[j], StringComparison.InvariantCultureIgnoreCase))
                    {
                        similarGrams++;
                        break;
                    }
                }
            }
            return (2.0 * similarGrams) / (sourceGrams.Count + targetGrams.Count);
        }

        private static List<string> generateNGrams(string str, int gramlength)
        {
            if (str == null || str.Length == 0) return null;

            int length = str.Length;
            List<string> grams;
            string gram;
            if (length < gramlength)
            {
                grams = new List<string>(length + 1);
                for (int i = 1; i <= length; i++)
                {
                    gram = str.Substring(0, i - 0);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
                gram = str.Substring(length - 1, length - (length - 1));
                if (grams.IndexOf(gram) == -1) grams.Add(gram);
            }
            else
            {
                grams = new List<string>(length - gramlength + 1);
                for (int i = 1; i <= gramlength - 1; i++)
                {
                    gram = str.Substring(0, i - 0);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
                for (int i = 0; i < length - gramlength + 1; i++)
                {
                    gram = str.Substring(i, i + gramlength - i);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
                for (int i = length - gramlength + 1; i < length; i++)
                {
                    gram = str.Substring(i, length - i);
                    if (grams.IndexOf(gram) == -1) grams.Add(gram);
                }
            }
            return grams;
        }

        public static double ComputePathSimilarity(DimPath source, DimPath target)
        {
            if (source == null || target == null || source.Length == 0 || target.Length == 0) return 0;

            double rankFactor1 = 0.5;
            double rankFactor2 = 0.5;

            double sumDim = 0.0;
            double sumSet = 0.0;
            double w1 = 1.0;
            for (int i = source.Path.Count - 1; i >= 0; i--)
            {
                string d1 = source.Path[i].Name;
                string s1 = source.Path[i].GreaterSet.Name;

                double w2 = 1.0;
                for (int j = target.Path.Count - 1; j >= 0; j--)
                {
                    string d2 = target.Path[j].Name;
                    string s2 = target.Path[j].GreaterSet.Name;

                    double simDim = ComputeStringSimilarity(d1, d2, 3);
                    simDim *= (w1 * w2);
                    sumDim += simDim;

                    double simSet = ComputeStringSimilarity(s1, s2, 3);
                    simSet *= (w1 * w2);
                    sumSet += simSet;

                    w2 *= rankFactor1; // Decrease the weight
                }

                w1 *= rankFactor2; // Decrease the weight
            }

            sumDim /= (source.Path.Count * target.Path.Count);
            sumSet /= (source.Path.Count * target.Path.Count);

            return (sumDim + sumSet) / 2;
        }

    }
}
