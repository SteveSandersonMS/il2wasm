using System;

namespace MyLibrary
{
    public static class Test
    {
        public static int Run()
        {
            switch (GetNumber())
            {
                case 1:
                    return -1;
                case 2:
                    return -2;
                case 3:
                    return -3;
                default:
                    return 123;
            }
        }

        static int GetNumber()
        {
            return 0;
        }
    }
}
