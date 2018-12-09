using System;
using System.IO;

namespace il2wasm
{
    class Program
    {
        static void Main(string[] args)
        {
            // CWD is inconsistent between VS and `dotnet run` :(
            // See https://github.com/dotnet/project-system/issues/3619
            // Try VS way first: cwd is .exe file location
            var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "samples"));
            if (!Directory.Exists(root))
            {
                // If dir doesn't exist, try `dotnet run` way: cwd is .csproj location
                root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "samples"));
            }
            var source = Path.Combine(root, "MyLibrary", "bin", "Debug", "netstandard2.0", "MyLibrary.dll");
            var dest = Path.Combine(root, "MyWebHost", "wwwroot", "MyLibrary.wasm");

            Compiler.Compile(source, dest);
        }
    }
}
