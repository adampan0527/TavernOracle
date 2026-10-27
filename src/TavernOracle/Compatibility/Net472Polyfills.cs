namespace TavernOracle
{
    using System;

    internal static class MathEx
    {
        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
        public static double Clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
    }
}

#if !NETCOREAPP && !NETSTANDARD2_1_OR_GREATER
namespace System.Collections.Generic
{
    internal static class KeyValuePairExtensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}

namespace System
{
    internal static class StringExtensions
    {
        public static bool Contains(this string source, string value, StringComparison comparisonType)
        {
            if (source == null) return false;
            return source.IndexOf(value, comparisonType) >= 0;
        }
    }
}


#endif
