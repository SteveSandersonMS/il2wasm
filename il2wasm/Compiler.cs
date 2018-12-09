using System;
using System.IO;
using System.Linq;
using Microsoft.JSInterop;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.WebAssembly.Interop;

namespace il2wasm
{
    static class Compiler
    {

        public static void Compile(string source, string destDir)
        {
            var sourceModule = AssemblyDefinition.ReadAssembly(source).MainModule;
            var destWithoutExtension = Path.Combine(destDir, Path.GetFileNameWithoutExtension(source));

            var wasmModule = CompileToWasm(sourceModule);
            using (var fs = File.Create($"{destWithoutExtension}.wasm"))
            {
                wasmModule.WriteToBinary(fs);
            }

            sourceModule.Write($"{destWithoutExtension}.dll");
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

            ReplaceWithJSInteropCall(sourceMethod);
        }

        private static void ReplaceWithJSInteropCall(MethodDefinition sourceMethod)
        {
            var body = sourceMethod.Body;
            var instructions = body.Instructions;
            instructions.Clear();
            body.ExceptionHandlers.Clear();
            
            var jsRuntimeRef = sourceMethod.Module.ImportReference(typeof(JSRuntime));
            var ijsRuntimeRef = sourceMethod.Module.ImportReference(typeof(IJSRuntime));
            var monoWebAssemblyJSRuntimeRef = sourceMethod.Module.ImportReference(typeof(MonoWebAssemblyJSRuntime));

            var ilProcessor = body.GetILProcessor();
            ilProcessor.Append(
                ilProcessor.Create(OpCodes.Call, new MethodReference("get_Current", ijsRuntimeRef, jsRuntimeRef)));
            ilProcessor.Append(
                ilProcessor.Create(OpCodes.Castclass, monoWebAssemblyJSRuntimeRef));

            // Identifier
            ilProcessor.Append(
                ilProcessor.Create(OpCodes.Ldstr, sourceMethod.FullName));

            // Other args
            for (var i = 0; i < sourceMethod.Parameters.Count; i++)
            {
                ilProcessor.Append(
                    ilProcessor.Create(OpCodes.Ldarg_S, sourceMethod.Parameters[i]));
            }
           
            // Create closed-generic method reference
            var methodRef = new MethodReference(
                nameof(MonoWebAssemblyJSRuntime.InvokeUnmarshalled),
                sourceMethod.ReturnType,
                monoWebAssemblyJSRuntimeRef);
            for (var i = 0; i < sourceMethod.Parameters.Count; i++)
            {
                methodRef.GenericParameters.Add(
                    new GenericParameter($"T{i}", methodRef));
            }
            methodRef.GenericParameters.Add(
                new GenericParameter("TRet", methodRef));
            var genericMethodRef = new GenericInstanceMethod(
                methodRef);
            foreach (var p in sourceMethod.Parameters)
            {
                genericMethodRef.GenericArguments.Add(p.ParameterType);
            }
            genericMethodRef.GenericArguments.Add(sourceMethod.ReturnType);

            // Invoke and return
            ilProcessor.Append(
                ilProcessor.Create(OpCodes.Callvirt, genericMethodRef));
            ilProcessor.Append(
                ilProcessor.Create(OpCodes.Ret));
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
