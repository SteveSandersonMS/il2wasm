using System;

namespace MyConsoleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            const int n = 10000;
            for (var i = 0; i < 10; i++)
            {
                var startTime = DateTime.Now;
                var result = MyLibrary.Test.RunComputation(n);
                var duration = DateTime.Now.Subtract(startTime).TotalMilliseconds;
                Console.WriteLine($"The {n}th prime is: {result} (time: {duration}ms)");
            }
        }
    }
}
