using System;
using System.IO;

namespace il2wasm
{
    class Program
    {
        static void Main(string[] args)
        {
            var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\samples"));
            var source = Path.Combine(root, @"MyLibrary\bin\Debug\netstandard2.0\MyLibrary.dll");
            var dest = Path.Combine(root, @"MyWebHost\wwwroot\MyLibrary.wasm");

            Compiler.Compile(source, dest);
        }
    }
}
