﻿using System;

namespace MyLibrary
{
    public static class Test
    {
        public static int Run(int myArg)
        {
            for (var i = 0; i < 10; i++)
            {
                Console.WriteLine(GetNthPrime(myArg + i));
            }

            return 0;
        }

        static int GetNthPrime(int n)
        {
            var current = 1;
            for (var i = 0; i < n; i++)
            {
                current++;
                while (!IsPrime(current))
                {
                    current++;
                }
            }
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
