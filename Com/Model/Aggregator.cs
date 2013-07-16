using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Offset = System.Int32;

namespace Com.Model
{
    public interface IAggregator<T>
    {
        // Array aggregation operations
        T Sum(T[] values);
        T Avg(T[] values);

        // Value arithmetic operations
        T Add(T a, T b);
        T Div(T a, int count); // надо уметь делить на целое, т.к. длина массива - целая величина
    }

    public static class ComArray
    {
/*
        public static object Sum(this System.Collections.IEnumerable values)
        {
            return values.Sum(); // System method of Array
        }
*/
        public static object Avg(this System.Collections.IEnumerable values)
        {
             var intArray = values as int[];
             if (intArray != null)
             {
                 return intArray.Average();
             }
             var doubleArray = values as double[]; 
             if (doubleArray != null)
             {
                 return doubleArray.Average();
             }
            // And so on for all primitive types
             return null;
        }
    }

    /// <summary>
    /// Aggregation functions for int[]
    /// </summary>
    public struct IntAggregator : IAggregator<int>
    {
        public int Sum(int[] values) 
        {
            return values.Sum(); 
        }
        public int Avg(int[] values)
        {
            return (int)values.Average();
        }

        public int Add(int a, int b) { return a + b; }
        public int Div(int a, int b) { return a / b; }
    }
    public static class IntArray // Expand int[]
    {
/*
        public static int Sum(this IEnumerable<int> values)
        {
            return values.Sum();
        }
*/
        public static int Avg(this IEnumerable<int> values)
       {
            return (int)values.Average();
        }
    }

    /// <summary>
    /// Aggregation functions for double[]
    /// </summary>
    public struct DoubleAggregator : IAggregator<double>
    {
        public double Sum(double[] values)
        {
            return (double)values.Sum();
        }
        public double Avg(double[] values)
        {
            return values.Average();
        }

        public double Add(double a, double b) { return a + b; }
        public double Div(double a, int b) { return a / b; }
    }
    public static class DoubleArray // Expand double[]
    {
/*
        public static double Sum(this IEnumerable<double> values)
        {
            return values.Sum();
        }
*/
        public static double Avg(this IEnumerable<double> values)
        {
            return values.Average();
        }
    }
}
