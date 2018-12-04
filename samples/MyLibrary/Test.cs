using System;

namespace MyLibrary
{
    public static class Test
    {
        public static int Run(int myArg)    
        {
            if (myArg == 123)
            {
                myArg--;
            }
            else
            {
                myArg++;
            }

            return myArg;
        }

        static int GetNumber()
        {
            return 0;
        }
    }
}
