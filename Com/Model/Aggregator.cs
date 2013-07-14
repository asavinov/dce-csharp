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

    // We implement aggregation functions separately for each primitive type
    public struct IntAggregator : IAggregator<int>
    {
        public int Sum(int[] values) 
        {
            int agg = 0;
            for (int i = 0; i < values.Length; i++) agg += values[i];
            return agg; 
        }
        public int Avg(int[] values)
        {
            return Sum(values) / values.Length;
        }

        public int Add(int a, int b) { return a + b; }
        public int Div(int a, int b) { return a / b; }
    }

    public struct DoubleAggregator : IAggregator<double>
    {
        public double Sum(double[] values)
        {
            double agg = 0;
            for (int i = 0; i < values.Length; i++) agg += values[i];
            return agg;
        }
        public double Avg(double[] values)
        {
            return Sum(values) / values.Length;
        }

        public double Add(double a, double b) { return a + b; }
        public double Div(double a, int b) { return a / b; }
    }

}
