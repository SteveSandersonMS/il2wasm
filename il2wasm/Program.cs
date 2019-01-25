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
                Console.Error.WriteLine($"Usage: dotnet exec {args[0]} -- <sourceassembly> <outputdirectory>");
                return 1;
            }

            var source = args[1];
            var destDir = args[2];

            Directory.CreateDirectory(destDir);
            Compiler.Compile(source, destDir);

            return 0;
        }
    }
}
