using System;

namespace MyLibrary
{
    public static class Test
    {
        public static int Run(int myArg)
        {
            if (GetNumber() < 10)
            {
                myArg--;
            }

            if (GetNumber() > 100)
            {
                myArg++;
            }

            return myArg * 2;
        }

        static int GetNumber()
        {
            return 0;
        }
    }
}
