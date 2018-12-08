using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace il2wasm
{
    static class Compiler
    {
        public static void Compile(string source, string dest)
        {
            var sourceModule = AssemblyDefinition.ReadAssembly(source).MainModule;

            var wasmModule = CompileToWasm(sourceModule);
            using (var fs = File.Create(dest))
            {
                wasmModule.WriteToBinary(fs);
            }
        }

        private static WebAssembly.Module CompileToWasm(Mono.Cecil.ModuleDefinition sourceModule)
        {
            var wasmBuilder = new WasmModuleBuilder();

            foreach (var sourceType in sourceModule.Types)
            {
                CompileType(sourceType, wasmBuilder);
            }

            return wasmBuilder.ToModule();
        }

        private static void CompileType(Mono.Cecil.TypeDefinition sourceType, WasmModuleBuilder wasmBuilder)
        {
            foreach (var sourceMethod in sourceType.Methods)
            {
                CompileMethod(sourceMethod, wasmBuilder);
            }
        }

        private static void CompileMethod(Mono.Cecil.MethodDefinition sourceMethod, WasmModuleBuilder wasmBuilder)
        {
            Console.WriteLine($"Compiling { sourceMethod.FullName }...");
            var fn = new WasmFunctionBuilder(sourceMethod.FullName, wasmBuilder)
            {
                Export = sourceMethod.IsPublic,
                ResultType = ToWebAssemblyType(sourceMethod.ReturnType)
            };

            var paramTypes = sourceMethod.Parameters.Select(p => ToWebAssemblyType(p.ParameterType).Value);
            if (sourceMethod.HasThis)
            {
                paramTypes = paramTypes.Prepend(WebAssembly.ValueType.Int32); // "this"
            }

            var paramIndex = 0;
            foreach (var paramType in paramTypes)
            {
                fn.AddParameter(paramType);
                fn.GetLocalIndex($"arg{paramIndex}", paramType);
                paramIndex++;
            }

            fn.Instructions.AddRange(
                MethodBodyCompiler.Compile(sourceMethod, wasmBuilder, fn));
        }

        private static WebAssembly.ValueType? ToWebAssemblyType(TypeReference dotNetType)
        {
            switch (dotNetType.FullName)
            {
                case "System.Void": return null;
                case "System.Int32": return WebAssembly.ValueType.Int32;
                case "System.Boolean": return WebAssembly.ValueType.Int32;
                default: throw new ArgumentException($"Unsupported .NET type: {dotNetType.FullName}");
            }
        }
    }
}
