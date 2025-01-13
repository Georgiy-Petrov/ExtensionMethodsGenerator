using System;
using System.Threading.Tasks;

namespace ExtensionMethodsGenerator.Sample
{
    // This class demonstrates basic static methods
    [ToExtensionMethod]
    public static class MathUtilities
    {
        public static int Add(int x, int y) => x + y;
        public static double Multiply(double a, double b) => a * b;
        public static int Scale(int x, int y, int factor) => (x + y) * factor;
    }

    // This class demonstrates internal methods for logging
    [ToExtensionMethod]
    internal static class InternalLogger
    {
        public static void LogInfo(string message)
        {
            Console.WriteLine($"[INFO]: {message}");
        }

        internal static void LogWarning(string message)
        {
            Console.WriteLine($"[WARNING]: {message}");
        }

        public static void NoParameterMethod()
        {
        }
    }

    // This class demonstrates generic static methods
    [ToExtensionMethod]
    public static class GenericMethods
    {
        public static T Echo<T>(T item) => item;

        public static T Max<T>(T first, T second) where T : IComparable<T>
        {
            return first.CompareTo(second) > 0 ? first : second;
        }
    }

    // This class demonstrates a generic container with constraints
    [ToExtensionMethod]
    public static class GenericContainer<T> where T : class
    {
        public static T PrintAndReturn(T item)
        {
            Console.WriteLine(item?.ToString());
            return item;
        }

        public static U Transform<U>(T item, Func<T, U> transformer) where U : struct
        {
            return transformer(item);
        }
        
        public static U PrintAndReturn<U>(U item)
        {
            Console.WriteLine(item?.ToString());
            return item;
        }
    }

    // This class demonstrates overloaded methods with multiple parameters
    [ToExtensionMethod]
    public static class StringExtensionsTest
    {
        public static string Concat(string s1, string s2) => s1 + s2;
        public static string Concat(string s1, string s2, string s3) => s1 + s2 + s3;
        public static string Concat(string s1, int number) => s1 + number;
        public static bool IsEmpty(string s) => string.IsNullOrEmpty(s);
    }

    // This class demonstrates mixed accessibility methods
    [ToExtensionMethod]
    public static class ServiceHelper
    {
        public static void PublicCall(string message)
        {
            Console.WriteLine("[Public] " + message);
        }

        internal static void InternalCall(string message)
        {
            Console.WriteLine("[Internal] " + message);
        }

        private static void PrivateCall(string message)
        {
        }
    }

    // This class demonstrates async methods and methods that return Task without await
    [ToExtensionMethod]
    public static class AsyncUtilities
    {
        public static async Task<int> AddAfterDelay(int x, int y, int delayMs)
        {
            await Task.Delay(delayMs);
            return x + y;
        }

        public static Task<int> AddNoAwait(int x, int y)
        {
            return Task.FromResult(x + y);
        }
    }
}
