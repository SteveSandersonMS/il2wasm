using System;

namespace MyConsoleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            for (var i = 0; i < 10; i++)
            {
                var startTime = DateTime.Now;
                var result = MyLibrary.Test.Run(2000);
                var duration = DateTime.Now.Subtract(startTime).TotalMilliseconds;
                Console.WriteLine($"Result: {result}; Duration: {duration}ms");
            }
        }
    }
}
