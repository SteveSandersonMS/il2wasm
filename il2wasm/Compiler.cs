using System;
using System.IO;
using Mono.Cecil;
using WebAssembly;
using CILCode = Mono.Cecil.Cil.Code;

namespace il2wasm
{
    static class Compiler
    {
        public static void Compile(string source, string dest)
        {
            var sourceModule = Mono.Cecil.AssemblyDefinition.ReadAssembly(source).MainModule;

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

            var paramIndex = 0;
            foreach (var ilParam in sourceMethod.Parameters)
            {
                var paramType = ToWebAssemblyType(ilParam.ParameterType).Value;
                fn.AddParameter(paramType);
                fn.GetLocalIndex($"arg{paramIndex}", paramType);
                paramIndex++;
            }

            if (!sourceMethod.HasBody)
            {
                fn.Instructions.AddRange(new WebAssembly.Instruction[]
                {
                    new WebAssembly.Instructions.End(),
                });
            }
            else
            {
                foreach (var ilInstruction in sourceMethod.Body.Instructions)
                {
                    CompileInstruction(ilInstruction, wasmBuilder, fn);
                }

                fn.Instructions.Add(new WebAssembly.Instructions.End());
            }
        }

        private static void CompileInstruction(Mono.Cecil.Cil.Instruction ilInstruction, WasmModuleBuilder moduleBuilder, WasmFunctionBuilder wasmFunction)
        {
            Console.WriteLine($"> Opcode {ilInstruction.OpCode}");
            var instructions = wasmFunction.Instructions;
            switch (ilInstruction.OpCode.Code)
            {
                case CILCode.Add:
                    {
                        // TODO: How do we know this is the right type?
                        instructions.Add(new WebAssembly.Instructions.Int32Add());
                        break;
                    }
                case CILCode.Call:
                    {
                        var target = (MethodDefinition)ilInstruction.Operand;
                        var targetWasmFuncIndex = moduleBuilder.GetOrReserveFunctionIndex(target.FullName);
                        instructions.Add(new WebAssembly.Instructions.Call(targetWasmFuncIndex));
                        break;
                    }
                case CILCode.Ldarg_0:
                    {
                        AddGetLocalInstruction(wasmFunction, "arg0");
                        break;
                    }
                case CILCode.Ldarg_1:
                    {
                        AddGetLocalInstruction(wasmFunction, "arg1");
                        break;
                    }
                case CILCode.Ldloc_0:
                    {
                        AddGetLocalInstruction(wasmFunction, "loc0");
                        break;
                    }
                case CILCode.Ldloc_1:
                    {
                        AddGetLocalInstruction(wasmFunction, "loc1");
                        break;
                    }
                case CILCode.Ldc_I4_0:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(0));
                        break;
                    }
                case CILCode.Ldc_I4_1:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(1));
                        break;
                    }
                case CILCode.Ldc_I4_2:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(2));
                        break;
                    }
                case CILCode.Ldc_I4_3:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(3));
                        break;
                    }
                case CILCode.Ldc_I4_4:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(4));
                        break;
                    }
                case CILCode.Ldc_I4_5:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(5));
                        break;
                    }
                case CILCode.Ldc_I4_6:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(6));
                        break;
                    }
                case CILCode.Ldc_I4_7:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(7));
                        break;
                    }
                case CILCode.Ldc_I4_8:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(8));
                        break;
                    }
                case CILCode.Ldc_I4_S:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(Convert.ToInt32(ilInstruction.Operand)));
                        break;
                    }
                case CILCode.Ldc_I4_M1:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(-1));
                        break;
                    }
                case CILCode.Neg:
                    {
                        instructions.Add(new WebAssembly.Instructions.Int32Constant(-1));
                        instructions.Add(new WebAssembly.Instructions.Int32Multiply());
                        break;
                    }
                case CILCode.Mul:
                    {
                        // TODO: Determine correct type
                        instructions.Add(new WebAssembly.Instructions.Int32Multiply());
                        break;
                    }
                case CILCode.Stloc_0:
                    {
                        AddStoreLocalInstruction(wasmFunction, "loc0");
                        break;
                    }
                case CILCode.Stloc_1:
                    {
                        AddStoreLocalInstruction(wasmFunction, "loc1");
                        break;
                    }
                case CILCode.Nop:
                    break;
                case CILCode.Ret:
                    {
                        instructions.Add(new WebAssembly.Instructions.Return());
                        break;
                    }
                default:
                    throw new ArgumentException($"Unsupported opcode: {ilInstruction.OpCode.Code}");
            }
        }

        private static void AddGetLocalInstruction(WasmFunctionBuilder wasmFunction, string localName)
        {
            // TODO: Determine correct type
            var localIndex = wasmFunction.GetLocalIndex(localName, WebAssembly.ValueType.Int32);
            wasmFunction.Instructions.Add(new WebAssembly.Instructions.GetLocal(localIndex));
        }

        private static void AddStoreLocalInstruction(WasmFunctionBuilder wasmFunction, string localName)
        {
            // TODO: Determine correct type
            var localIndex = wasmFunction.GetLocalIndex(localName, WebAssembly.ValueType.Int32);
            wasmFunction.Instructions.Add(new WebAssembly.Instructions.SetLocal(localIndex));
        }

        private static WebAssembly.ValueType? ToWebAssemblyType(TypeReference dotNetType)
        {
            switch (dotNetType.FullName)
            {
                case "System.Void": return null;
                case "System.Int32": return WebAssembly.ValueType.Int32;
                default: throw new ArgumentException($"Unsupported .NET type: {dotNetType.FullName}");
            }
        }
    }
}
