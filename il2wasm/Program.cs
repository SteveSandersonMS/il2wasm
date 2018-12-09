using System;
using System.IO;

namespace il2wasm
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine($"Usage: dotnet exec {args[0]} -- <sourceassembly> <outputfile>");
                return 1;
            }

            var source = args[1];
            var dest = args[2];

            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            Compiler.Compile(source, dest);

            return 0;
        }
    }
}
