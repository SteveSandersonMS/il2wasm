using Mono.Cecil;
using System;
using System.Collections.Generic;
using WebAssembly;
using WebAssembly.Instructions;
using CILCode = Mono.Cecil.Cil.Code;

namespace il2wasm
{
    static class MethodBodyCompiler
    {
        public static IEnumerable<Instruction> Compile(MethodDefinition sourceMethod, WasmModuleBuilder wasmBuilder, WasmFunctionBuilder functionBuilder)
        {
            var result = new List<Instruction>();

            if (sourceMethod.HasBody)
            {
                foreach (var ilInstruction in sourceMethod.Body.Instructions)
                {
                    result.AddRange(CompileInstruction(ilInstruction, wasmBuilder, functionBuilder));
                }
            }

            result.Add(new End());
            return result;
        }

        private static IEnumerable<Instruction> CompileInstruction(Mono.Cecil.Cil.Instruction ilInstruction, WasmModuleBuilder wasmBuilder, WasmFunctionBuilder functionBuilder)
        {
            Console.WriteLine($"> Opcode {ilInstruction.OpCode}");
            
            switch (ilInstruction.OpCode.Code)
            {
                case CILCode.Add:
                    {
                        // TODO: How do we know this is the right type?
                        yield return new Int32Add();
                        break;
                    }
                case CILCode.Call:
                    {
                        var target = (MethodDefinition)ilInstruction.Operand;
                        var targetWasmFuncIndex = wasmBuilder.GetOrReserveFunctionIndex(target.FullName);
                        yield return new Call(targetWasmFuncIndex);
                        break;
                    }
                case CILCode.Ldarg_0:
                    {
                        yield return GetLocalInstruction(functionBuilder, "arg0");
                        break;
                    }
                case CILCode.Ldarg_1:
                    {
                        yield return GetLocalInstruction(functionBuilder, "arg1");
                        break;
                    }
                case CILCode.Ldloc_0:
                    {
                        yield return GetLocalInstruction(functionBuilder, "loc0");
                        break;
                    }
                case CILCode.Ldloc_1:
                    {
                        yield return GetLocalInstruction(functionBuilder, "loc1");
                        break;
                    }
                case CILCode.Ldc_I4_0:
                    {
                        yield return new Int32Constant(0);
                        break;
                    }
                case CILCode.Ldc_I4_1:
                    {
                        yield return new Int32Constant(1);
                        break;
                    }
                case CILCode.Ldc_I4_2:
                    {
                        yield return new Int32Constant(2);
                        break;
                    }
                case CILCode.Ldc_I4_3:
                    {
                        yield return new Int32Constant(3);
                        break;
                    }
                case CILCode.Ldc_I4_4:
                    {
                        yield return new Int32Constant(4);
                        break;
                    }
                case CILCode.Ldc_I4_5:
                    {
                        yield return new Int32Constant(5);
                        break;
                    }
                case CILCode.Ldc_I4_6:
                    {
                        yield return new Int32Constant(6);
                        break;
                    }
                case CILCode.Ldc_I4_7:
                    {
                        yield return new Int32Constant(7);
                        break;
                    }
                case CILCode.Ldc_I4_8:
                    {
                        yield return new Int32Constant(8);
                        break;
                    }
                case CILCode.Ldc_I4_S:
                    {
                        yield return new Int32Constant(Convert.ToInt32(ilInstruction.Operand));
                        break;
                    }
                case CILCode.Ldc_I4_M1:
                    {
                        yield return new Int32Constant(-1);
                        break;
                    }
                case CILCode.Neg:
                    {
                        yield return new Int32Constant(-1);
                        yield return new Int32Multiply();
                        break;
                    }
                case CILCode.Mul:
                    {
                        // TODO: Determine correct type
                        yield return new Int32Multiply();
                        break;
                    }
                case CILCode.Stloc_0:
                    {
                        yield return StoreLocalInstruction(functionBuilder, "loc0");
                        break;
                    }
                case CILCode.Stloc_1:
                    {
                        yield return StoreLocalInstruction(functionBuilder, "loc1");
                        break;
                    }
                case CILCode.Nop:
                    break;
                case CILCode.Ret:
                    {
                        yield return new Return();
                        break;
                    }
                default:
                    throw new ArgumentException($"Unsupported opcode: {ilInstruction.OpCode.Code}");
            }
        }

        private static Instruction StoreLocalInstruction(WasmFunctionBuilder functionBuilder, string localName)
        {
            var localIndex = functionBuilder.GetLocalIndex(localName, WebAssembly.ValueType.Int32);
            return new SetLocal(localIndex);
        }

        private static Instruction GetLocalInstruction(WasmFunctionBuilder functionBuilder, string localName)
        {
            // TODO: Determine correct type
            var localIndex = functionBuilder.GetLocalIndex(localName, WebAssembly.ValueType.Int32);
            return new GetLocal(localIndex);
        }
    }
}
