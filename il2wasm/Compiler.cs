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
            // The goal is to replace the source method's body with:
            //    return ((MonoWebAssemblyJSRuntime)JSRuntime.Current)
            //       .InvokeUnmarshalled<T0, T1, T2, TRet>("identifier string", arg0, arg1, arg2);
            // ... where "identifier string" is computed based on the original source method's name,
            // so at runtime we can match it up with the AoT equivalent.

            var body = sourceMethod.Body;
            var instructions = body.Instructions;
            instructions.Clear();
            body.ExceptionHandlers.Clear();

            var systemObjectTypeRef = sourceMethod.Module.TypeSystem.Object;
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
                ilProcessor.Create(OpCodes.Ldstr, GetAoTMethodJSInteropIdentifier(sourceMethod)));

            // Other args
            // TODO: Handle methods with 4+ args
            // Would need to wrap them in an object[] and invoke something that knows to unwrap them
            if (sourceMethod.Parameters.Count > 3)
            {
                throw new NotImplementedException("Support for methods with > 3 args is not yet implemented.");
            }
            for (var i = 0; i < 3; i++)
            {
                ilProcessor.Append(i < sourceMethod.Parameters.Count
                    ? ilProcessor.Create(OpCodes.Ldarg_S, sourceMethod.Parameters[i])
                    : ilProcessor.Create(OpCodes.Ldnull));
            }

            // Create closed-generic method reference
            var genericReturnType = sourceMethod.ReturnType ?? systemObjectTypeRef;
            var methodRef = new MethodReference(
                nameof(MonoWebAssemblyJSRuntime.InvokeUnmarshalled),
                genericReturnType,
                monoWebAssemblyJSRuntimeRef);
            methodRef.HasThis = true;
            methodRef.Parameters.Add(new ParameterDefinition(sourceMethod.Module.TypeSystem.String));
            for (var i = 0; i < 3; i++)
            {
                var genericParam = new GenericParameter($"T{i}", methodRef);
                methodRef.GenericParameters.Add(genericParam);
                methodRef.Parameters.Add(new ParameterDefinition(genericParam));
            }
            var returnTypeGenericParam = new GenericParameter("TRet", methodRef);
            methodRef.GenericParameters.Add(returnTypeGenericParam);
            var genericMethodRef = new GenericInstanceMethod(
                methodRef);
            for (var i = 0; i < 3; i++)
            {
                genericMethodRef.GenericArguments.Add(i < sourceMethod.Parameters.Count
                    ? sourceMethod.Parameters[i].ParameterType
                    : systemObjectTypeRef);
            }
            genericMethodRef.GenericArguments.Add(genericReturnType);
            genericMethodRef.ReturnType = returnTypeGenericParam;

            // Invoke and return
            ilProcessor.Append(
                ilProcessor.Create(OpCodes.Callvirt, genericMethodRef));
            ilProcessor.Append(
                ilProcessor.Create(OpCodes.Ret));
        }

        private static string GetAoTMethodJSInteropIdentifier(MethodDefinition sourceMethod)
        {
            return $"aot.{EncodeDots(sourceMethod.Module.Assembly.Name.Name)}.{EncodeDots(sourceMethod.FullName)}";
        }

        private static string EncodeDots(string str)
            // JSInterop treats dots as separators. We want just a two-level hierarchy (assembly, then method),
            // so avoid unintentional separators by replacing them with something that is unlikely to lead to clashes.
            => str.Replace(".", "-");

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
