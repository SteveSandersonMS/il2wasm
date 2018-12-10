using System;

namespace MyLibrary
{
    public static class Test
    {
        public static int Run(int myArg)
        {
            return GetNthPrime(myArg);
        }
        
        static int GetNthPrime(int n)
        {
            var primeCalculator = new PrimeCalculator();

            for (var i = 0; i < n - 1; i++)
            {
                primeCalculator.ComputeNextPrime();
            }

            return primeCalculator.ComputeNextPrime();
        }
    }

    class PrimeCalculator
    {
        private int current = 1;

        public int ComputeNextPrime()
        {
            do
            {
                current++;
            }
            while (!IsPrime(current));

            return current;
        }

        static bool IsPrime(int value)
        {
            for (var possibleDivisor = 2; possibleDivisor < value; possibleDivisor++)
            {
                if ((value % possibleDivisor) == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
