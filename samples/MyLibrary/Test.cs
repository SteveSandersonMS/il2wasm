using System;

namespace MyLibrary
{
    public static class Test
    {
        public static int Run()
        {
            return (GetNumber(5, 2) + GetNumber(8, -1)) * -1;
        }

        static int GetNumber(int num, int multiplyBy)
        {
            return num * multiplyBy;
        }
    }
}
