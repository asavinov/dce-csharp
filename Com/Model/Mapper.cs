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

        public CsTable GetBestTargetSet(CsTable sourceSet, CsSchema targetSchema) // Find target in the cache
        {
            Mapping bestMapping = GetBestMapping(sourceSet, targetSchema);
            return bestMapping == null ? null : bestMapping.TargetSet;
        }
        public CsTable GetBestSourceSet(CsSchema sourceSchema, CsTable targetSet)
        {
            Mapping bestMapping = GetBestMapping(sourceSchema, targetSet);
            return bestMapping == null ? null : bestMapping.TargetSet;
        }

        public Mapping GetBestMapping(CsTable sourceSet, CsSchema targetSchema) // Find best mapping in the cache
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
        public Mapping GetBestMapping(CsSchema sourceSchema, CsTable targetSet)
        {
            Mapping bestMapping = GetBestMapping(targetSet, sourceSchema);
            bestMapping.Invert();
            return bestMapping;
        }

        public List<Mapping> MapPrimitiveSet(CsTable sourceSet, CsSchema targetSchema)
        {
            CsSchema sourceSchema = sourceSet.Top;
            List<Mapping> maps = new List<Mapping>();
            CsTable targetSet;

            if (sourceSchema.GetType() == typeof(SetTop)) // SetTop -> *
            {
                if (targetSchema.GetType() == typeof(SetTop)) // SetTop -> SetTop
                {
                    targetSet = targetSchema.GetPrimitive(sourceSet.Name);
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
                    OleDbType sourceType = (OleDbType)Enum.Parse(typeof(OleDbType), sourceSet.Name, false); // Convert type representation: from name to enum (equivalent)
                    string targetType;

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
                            targetType = "Integer";
                            break;

                        // Double
                        case OleDbType.Double: // DBTYPE_R8
                        case OleDbType.Single: // DBTYPE_R4 -> Single
                            targetType = "Double";
                            break;

                        // Decimal
                        case OleDbType.Currency: // DBTYPE_CY
                        case OleDbType.Decimal: // DBTYPE_DECIMAL
                        case OleDbType.Numeric: // DBTYPE_NUMERIC
                        case OleDbType.VarNumeric:
                            targetType = "Decimal";
                            break;

                        // Boolean
                        case OleDbType.Boolean: // DBTYPE_BOOL
                            targetType = "Boolean";
                            break;

                        // DateTime
                        case OleDbType.Date: // DBTYPE_DATE
                        case OleDbType.DBDate: // DBTYPE_DBDATE
                        case OleDbType.DBTime: // DBTYPE_DBTIME ->  TimeSpan
                        case OleDbType.DBTimeStamp: // DBTYPE_DBTIMESTAMP
                        case OleDbType.Filetime: // DBTYPE_FILETIME
                            targetType = "DateTime";
                            break;

                        // Strings
                        case OleDbType.BSTR: // DBTYPE_BSTR
                        case OleDbType.Char: // DBTYPE_STR
                        case OleDbType.LongVarChar: // 
                        case OleDbType.LongVarWChar: //
                        case OleDbType.VarChar: //
                        case OleDbType.VarWChar: //
                        case OleDbType.WChar: // DBTYPE_WSTR
                            targetType = "String";
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

                    targetSet = targetSchema.GetPrimitive(targetType);

                    Mapping map = new Mapping(sourceSet, targetSet);
                    map.Similarity = 1.0;
                    maps.Add(map);
                }
                else if (targetSchema.GetType() == typeof(SetTopOledb)) // SetTopOledb -> SetTopOledb
                {
                    targetSet = targetSchema.GetPrimitive(sourceSet.Name);
                    Mapping map = new Mapping(sourceSet, targetSet);
                    map.Similarity = 1.0;
                    maps.Add(map);
                }
            }

            Mappings.AddRange(maps);
            return maps;
        }
        public List<Mapping> MapPrimitiveSet(CsSchema sourceSchema, CsTable targetSet)
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
        public List<Mapping> MapSet(CsTable sourceSet, CsSchema targetSchema)
        {
            if (sourceSet.IsPrimitive) return MapPrimitiveSet((Set)sourceSet, targetSchema);
            CsSchema sourceSchema = sourceSet.Top;
            List<Mapping> maps = new List<Mapping>();

            Dictionary<CsColumn, Mapping> greaterMappings = new Dictionary<CsColumn, Mapping>();

            //
            // 1. Find target greater sets. They are found among mappings and hence can contain both existing (in the schema) and new sets. 
            //
            List<CsTable> targetGreaterSets = new List<CsTable>();

            foreach (CsColumn sd in sourceSet.GreaterDims)
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
            List<CsTable> allTargetSets = targetSchema.GetAllSubsets();
            double[] coverage = new double[allTargetSets.Count];
            double maxCoverage = 0;
            int maxCoverageIndex = -1;

            for (int i = 0; i < allTargetSets.Count; i++)
            {
                // Find coverage of this target set (how many best greater target sets it covers)
                coverage[i] = 0;
                foreach (CsTable tgs in allTargetSets[i].GetGreaterSets())
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
                CsTable ts = new Set(sourceSet.Name); // New set has the same name as the soure set

                newMapping = new Mapping(sourceSet, ts);

                foreach (CsColumn sd in sourceSet.GreaterDims) // For each source dimension, create one new target dimension 
                {
                    Mapping gMapping = greaterMappings[sd];
                    CsTable gts = gMapping.TargetSet;

                    CsColumn td = targetSchema.CreateColumn(sd.Name, ts, gts, sd.IsIdentity); // Create a clone for the source dimension

                    newMapping.AddPaths(sd, td, gMapping); // Add a pair of dimensions as a match (with expansion using the specified greater mapping)
                }

                newMapping.Similarity = 1.0;
                maps.Add(newMapping);
            }
            else // Use existing target set(s) for mapping(s)
            {
                CsTable ts = allTargetSets[maxCoverageIndex];

                newMapping = new Mapping(sourceSet, ts);

                foreach (CsColumn sd in sourceSet.GreaterDims) // For each source dimension, find best target dimension 
                {
                    Mapping gMapping = greaterMappings[sd];
                    CsTable gts = gMapping.TargetSet;

                    // Find an existing dimension from ts to gts with the best similarity to source dim sd
                    CsColumn td = null;
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

                    //td.IsIdentity = sd.IsIdentity;

                    newMapping.AddPaths(sd, td, gMapping); // Add a pair of dimensions as a match (with expansion using the specified greater mapping)
                }

                newMapping.Similarity = maxCoverage;
                maps.Add(newMapping);
            }

            Mappings.AddRange(maps);
            return maps;
        }
        public List<Mapping> MapSet(CsSchema sourceSchema, CsTable targetSet)
        {
            if (targetSet.IsPrimitive) return MapPrimitiveSet(sourceSchema, targetSet);

            List<Mapping> maps = MapSet(targetSet, sourceSchema);
            maps.ForEach(m => Mappings.Remove(m));

            maps.ForEach(m => m.Invert());
            Mappings.AddRange(maps);
            return maps;
        }

        /// <summary>
        /// Generate best mappings from the source set to the target set. 
        /// </summary>
        public List<Mapping> MapSet_NEW(CsTable sourceSet, CsTable targetSet)
        {
            // For the first simplest version, we generate only column mappings for relational source sets

            CsSchema sourceSchema = sourceSet.Top;
            CsSchema targetSchema = targetSet.Top;
            List<Mapping> maps = new List<Mapping>();

            if (sourceSet.IsPrimitive)
            {
                throw new NotImplementedException();
            }

            // Mapping usage scenarios:
            // - type change (MapDim). Here we have old dim with its type and want to change this type. 
            //   we list all formally possible new types and for each of them generate a mapping (old type -> new type) by taking into account their usage by the old/new dimensions
            // - from concrete source set to target schema. new (greater) could be created if they are needed (if all existing are bad)
            //   it is used for importing sets as a whole by finding the best position in the schema
            // - from concrete set to concrete set (new dimensions could be created if it is allowed and all existing are bad, greater sets could be created if they are allowed and all existing are bad)
            //    here the goal is to find best map to the concrete target but it assumes that all existing target sets could be used as targets for greater sets (types)

            // Building a mapping always means finding a good target type (set as a whole, among all available) for each source type
            // It can be viewed as finding type usage which means a pair <lesser dim, type set>. 
            // A good type means a set with all its greater dimensions which means recursion

            // One algorithm is that in order to find a good set mapping we have to find good mappings for its greater sets (recursively)
            // Another algorithm is that build all source and target primitive paths and then evaluate all matches among them. First, we choose only good path matches to decrease the space. Then build one possible mapping and evaluate its quality.

            // One formal and semantic way to think about a mapping is that target dim tree has to be fit into the source dim tree (or vice versa) by maximizing relevance factor
            // Leaves of the paths must be primitive sets and these sets must be matched
            // The root also must be matched (the question is whether it is a dim or set)
            // It is a kind of semantically best tree coverage. 
            // Note that intermediate nodes represent trees and hence are also matches that can be evaluated so we get recursion
            // The main question here is how to generate all possible tree coverages (by satisfying some formal conditions like leaf matching)
            // Second question is what are nodes of the tree: dims or sets? 
            // What is being matched: nodes (sets or dims), edges, or a pair of <edge, node>
            // How a whole node quality is evaluated as opposed to one edge evaluation?

            // One algorithm to fit a graph into another graph is as follows: 
            // Enumerate all possible path matches which satisfy formal constraints: 
            // - Leaves are matched (that is, connects starts and ends of the paths - not in the middle)
            // - Next path match must satisfy constraints of all the existing path matches (see CanMatch method)
            //   - If source path has non-null prefix intersection with some existing source path, the target must continue the previous target. Set match inference: these intermediate sets are matched.
            // - Set inference rules:
            //   - common prefix of two source paths in different matches (target paths must also have the same set in the middle)
            //   - (a single variant) The only possible matching set for another set. As a consequence, other sets of this paths might also get the only matching set.
            //   - (no variants) Having no matching set rule. If this set is between an immediately connected sets which have been already matched (no free places, no options).

            // This algorithm could be implemented on paths or on tuples (trees)
            // In the case of trees, we match the tree leaf nodes and then choose other matches and derive set matches (so intermediate set nodes also could be matched).
            // A tree can be then converted to a mapping (path matches) and vice versa
            // The algorithm finds all possible leaf matches
            // For each chosen next free (non-matched) source, it is necessary to choose (best) nest non-matched target leaf (taking into account formal constraints)
            // After choosing next match, derive intermediate set matches. A set match can be represented as a list of formally possible set matches (including empty list, a single set and more)
            // Choosing best options are based on evaluating primitive set (predefined or user-defined) matches. Then we can evaluate complex sets (tree) quality. Here we need to aggregate its dimension and set matches by taking into account coverage.


            // How it will be used?
            // We will give a relational set as a source (with dims and atts) 
            // Some existing non-relational set will be used as a target
            // Normally this call is made before opening an editor for mappings in a dialog box to initialize/recommend mappings
            // The target can be empty or non-empty
            // If it is empty (first call of the dialog for import) then we simply generate identical target dimensions (copy)
            // If it is non-empty the we do not make any recommendations and can simply edit the existing mapping

            // Dimensions or attributes?
            // Mappings use dimension paths by definition
            // One use of mappings is generating a tuple expression which is then used by the interpreter to access functions of the current record
            // 1. For relational set (flat), a record is always a DataRow with attribute names as functions - dimensions are not used.
            // 2. Relational expanded sets can however generate new attributes which correspond to fk-attribute-paths. Their names are generated and specified in the SQL-query (with joins to attach fk-tables). 
            // Second usage of mappings is in editor where the user can choose manually which source attributes have to be chosen for import.
            // Third use is in the mapper for recommendation and schema matching. Here the mapping stores important semantic data.

            // Our first use is to simply store which source attributes have to imported and which target primitive types have to be used.
            // Automatic mapping is not needed here. We list all source attributes - are they dimensions or attributes? Indeed, we use DimPath object. May be use DimAttribute?
            // If it has to be imported then we add a target dimension as a match. If not, then either do not add the source or leave the matching target path empty.
            // Parameterize the target path: its greater primtive set, its name. 
            // We need an initializer (constructor) for this structure. 
            // And we need a dialog to be able to edit this structure, say, by listing all source paths, and for each of them having a checkbox for inclusion as well as name, target primitive set in combo box.

            return null;
        }

        /// <summary>
        /// Build mappings from the source set to the target set. The sets are greater sets of the specified dimensions. 
        /// The mapping should take into account (semantically) that these sets are used from these dimensions. 
        /// </summary>
        public List<Mapping> MapDim(DimPath sourcePath, DimPath targetPath)
        {
            // We analyze all continuations of the specified prefix paths
            List<DimPath> sourcePaths = (new PathEnumerator(sourcePath.GreaterSet, DimensionType.IDENTITY_ENTITY)).ToList();
            sourcePaths.ForEach(p => p.InsertFirst(sourcePath));
            if (sourcePaths.Count == 0) sourcePaths.Add(sourcePath);

            List<DimPath> targetPaths = (new PathEnumerator(targetPath.GreaterSet, DimensionType.IDENTITY_ENTITY)).ToList();
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
        public static CsTable ImportSet(CsTable sourceSet, CsSchema targetSchema)
        {
            Mapper mapper = new Mapper();
            mapper.SetCreationThreshold = 1.0;
            mapper.MapSet(sourceSet, targetSchema);
            Mapping mapping = mapper.GetBestMapping(sourceSet, targetSchema);
            mapping.AddTargetToSchema(targetSchema);

            // Define the column
            CsColumn dimImport = new Dim(mapping);
            dimImport.Add();

            // Define the table
            dimImport.GreaterSet.Definition.DefinitionType = TableDefinitionType.PROJECTION;

            return mapping.TargetSet;
        }

        /// <summary>
        /// Create and initialize a new mapping which produces a flat target set with all primitive dimensions for copying primitive data from the source set.
        /// Only identity (PK) source dimensions are expanded recursively. 
        /// For relational source, this means that all primitive columns of the source table will be mapped with their relational names, no FK-referenced tables will be joined and no artifical column names will be used. 
        /// If it is necessary to expand entity dimensions (non-PK columns of joined tables) then a different implementation is needed (which will require joins, artifical column/path names etc.)
        /// </summary>
        public Mapping CreatePrimitive(CsTable sourceSet, CsTable targetSet)
        {
            Debug.Assert(!sourceSet.IsPrimitive && !targetSet.IsPrimitive, "Wrong use: copy mapping can be created for only non-primitive sets.");

            Mapping map = new Mapping(sourceSet, targetSet);

            CsSchema sourceSchema = map.SourceSet.Top;
            CsSchema targetSchema = map.TargetSet.Top;

            DimPath sp;
            DimPath tp;
            Dim td;

            PathMatch match;

            if (sourceSchema is SetTopOledb)
            {
                SetRel set = (SetRel)map.SourceSet;
                foreach (DimAttribute att in set.GreaterPaths)
                {
                    sp = new DimAttribute(att);

                    // Recommend matching target type (mapping primitive types)
                    this.MapPrimitiveSet(att.GreaterSet, targetSchema);
                    CsTable targetType = this.GetBestTargetSet(att.GreaterSet, targetSchema);

                    td = new Dim(att.RelationalColumnName, map.TargetSet, targetType, att.IsIdentity, false);
                    tp = new DimPath(td);
                    tp.Name = sp.Name;

                    match = new PathMatch(sp, tp, 1.0);

                    map.Matches.Add(match);
                }
            }

            return map;
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
    
}
