using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json.Linq;

namespace Com.Model
{

    public class Utils
    {
        public static JObject CreateJsonRef(object obj) // Represent an existing object as a reference
        {
            dynamic jsonObj = new JObject();
            jsonObj.type = obj.GetType().Name;

            if (obj is ComSchema)
            {
                jsonObj.element_type = "schema";

                jsonObj.schema_name = ((ComSchema)obj).Name;
            }
            else if (obj is ComTable)
            {
                jsonObj.element_type = "table";

                jsonObj.schema_name = ((ComTable)obj).Top.Name;
                jsonObj.table_name = ((ComTable)obj).Name;
            }
            else if (obj is ComColumn)
            {
                jsonObj.element_type = "column";

                jsonObj.schema_name = ((ComColumn)obj).LesserSet.Top.Name;
                jsonObj.table_name = ((ComColumn)obj).LesserSet.Name;

                jsonObj.type_schema_name = ((ComColumn)obj).GreaterSet.Top.Name;
                jsonObj.type_table_name = ((ComColumn)obj).GreaterSet.Name;

                jsonObj.column_name = ((ComColumn)obj).Name;
            }

            return jsonObj;
        }
        public static object ResolveJsonRef(JObject json, Workspace ws) // Resolve a json reference to a real object
        {
            string element_type = ((dynamic)json).element_type;
            if (element_type == null) return null;

            if (element_type == "schema") // Find schema
            {
                return ws.GetSchema((string)((dynamic)json).schema_name);
            }
            else if (element_type == "table") // Find table
            {
                ComSchema schema = ws.GetSchema((string)((dynamic)json).schema_name);
                if (schema == null) return null;
                return schema.FindTable((string)((dynamic)json).table_name);
            }
            else if (element_type == "column") // Find column
            {
                ComSchema schema = ws.GetSchema((string)((dynamic)json).schema_name);
                if (schema == null) return null;
                ComTable table = schema.FindTable((string)((dynamic)json).table_name);
                if (table == null) return null;
                return table.GetGreaterDim((string)((dynamic)json).column_name);
            }
            else
            {
                throw new NotImplementedException("Unknown element type in the reference.");
            }
        }

        /// <summary>
        /// Create json object (JObject) for representing the specified object and write real type field
        /// </summary>
        public static JObject CreateJsonFromObject(object obj)
        {
            dynamic jsonObj = new JObject();
            jsonObj.type = obj.GetType().Name;
            jsonObj.full_type = obj.GetType().FullName;

            if (obj is ComSchema)
            {
                jsonObj.element_type = "schema";
            }
            else if (obj is ComTable)
            {
                jsonObj.element_type = "table";
            }
            else if (obj is ComColumn)
            {
                jsonObj.element_type = "column";
            }
            else if (obj is ExprNode)
            {
                jsonObj.element_type = "expression";
            }
            else if (obj is Mapping)
            {
                jsonObj.element_type = "path";
            }
            else if (obj is Mapping)
            {
                jsonObj.element_type = "mapping";
            }
            else if (obj is PathMatch)
            {
                jsonObj.element_type = "match";
            }

            return jsonObj;
        }

        /// <summary>
        /// Create an instance of the class described by the specified json object using its type field. 
        /// </summary>
        public static object CreateObjectFromJson(JObject jsonObj)
        {
            string type = ((dynamic)jsonObj).type;
            string full_type = ((dynamic)jsonObj).full_type;
            if (type == null) return null;

            var assembly = Assembly.GetExecutingAssembly();
            var klass = assembly.GetTypes().First(t => t.Name == type);

            object obj = Activator.CreateInstance(klass);
            // Alternatives:
            //object obj = Activator.CreateInstance(Type.GetType(full_type));
            //object obj = Activator.CreateInstance(null, full_type).Unwrap();

            // Get a list of derived types: assembly.GetTypes().Where(baseType.IsAssignableFrom).Where(t => baseType != t);

            if (obj == null)
            {
                throw new NotImplementedException("Cannot instantiate this object type");
            }

            return obj;
        }
    }
    
    // Check this: "fuzzy string comparisons using trigram cosine similarity"
    // Check this: "TF-IDF cosine similarity between columns"
    // minimum description length (MDL) similar to Jaccard similarity to compare two attributes. This measure computes the ratio of the size of the intersection of two columns' data to the size of their union.
    //   V. Raman and J. M. Hellerstein. Potter's wheel: An interactive data cleaning system. In VLDB, 381-390, 2001.
    // Welch's t-test for a pair of columns that contain numeric values. Given the columns' means and variances, the t-test gives the probability the columns were drawn from the same distribution.
    public class StringSimilarity
    {
        public static bool JsonTrue(dynamic val)
        {
            return "true".Equals((string)val, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool SameSchemaName(string n1, string n2)
        {
            return SameTableName(n1, n2);
        }

        public static bool SameTableName(string n1, string n2)
        {
            if (n1 == null || n2 == null) return false;
            if (string.IsNullOrEmpty(n1) || string.IsNullOrEmpty(n2)) return false;
            return n1.Equals(n2, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool SameColumnName(string n1, string n2)
        {
            return SameTableName(n1, n2);
        }

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
            if (source == null || target == null || source.Size == 0 || target.Size == 0) return 0;

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
