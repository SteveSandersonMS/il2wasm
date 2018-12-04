using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAssembly;
using WebAssembly.Instructions;
using CILCode = Mono.Cecil.Cil.Code;

namespace il2wasm
{
    static class MethodBodyCompiler
    {
        private static T RemoveFirstOrDefault<T>(List<T> list)
        {
            if (list.Count > 0)
            {
                var result = list[0];
                list.RemoveAt(0);
                return result;
            }

            return default(T);
        }

        public static IEnumerable<Instruction> Compile(MethodDefinition sourceMethod, WasmModuleBuilder wasmBuilder, WasmFunctionBuilder functionBuilder)
        {
            var result = new List<Instruction>();

            if (sourceMethod.HasBody)
            {
                var jumpTargets = new FlowAnalysis(sourceMethod.Body).JumpTargetOffsets;
                var nextJumpTargetIndex = 0;
                var nextJumpTargetOffset = jumpTargets.Count > 0 ? (int?)jumpTargets[0] : null;
                Func<Mono.Cecil.Cil.Instruction, uint> getBreakDepth = (Mono.Cecil.Cil.Instruction instruction) =>
                {
                    for (var targetIndex = 0; targetIndex < jumpTargets.Count; targetIndex++)
                    {
                        if (jumpTargets[targetIndex] == instruction.Offset)
                        {
                            return (uint)(targetIndex - nextJumpTargetIndex);
                        }
                    }

                    throw new ArgumentException($"No available jump target with offset {instruction.Offset}");
                };

                // Open all the nested blocks that start at the top of the function
                for (var openBlockIndex = 0; openBlockIndex < jumpTargets.Count; openBlockIndex++)
                {
                    result.Add(new Block());
                }

                foreach (var ilInstruction in sourceMethod.Body.Instructions)
                {
                    if (ilInstruction.Offset >= nextJumpTargetOffset)
                    {
                        result.Add(new End());
                        nextJumpTargetOffset = (jumpTargets.Count > ++nextJumpTargetIndex)
                            ? (int?)jumpTargets[nextJumpTargetIndex]
                            : null;
                    }

                    result.AddRange(CompileInstruction(ilInstruction, wasmBuilder, functionBuilder, getBreakDepth));
                }
            }

            result.Add(new End());
            return result;
        }

        private static IEnumerable<Instruction> CompileInstruction(Mono.Cecil.Cil.Instruction ilInstruction, WasmModuleBuilder wasmBuilder, WasmFunctionBuilder functionBuilder, Func<Mono.Cecil.Cil.Instruction, uint> getBreakDepth)
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
                case CILCode.Br_S:
                    {
                        var breakFromBlockDepth = getBreakDepth((Mono.Cecil.Cil.Instruction)ilInstruction.Operand);
                        yield return new Branch(breakFromBlockDepth);
                        break;
                    }
                case CILCode.Brfalse_S:
                    {
                        var breakFromBlockDepth = getBreakDepth((Mono.Cecil.Cil.Instruction)ilInstruction.Operand);
                        yield return new Int32Constant(1);
                        yield return new Int32Subtract();
                        yield return new BranchIf(breakFromBlockDepth);
                        break;
                    }
                case CILCode.Brtrue_S:
                    {
                        var breakFromBlockDepth = getBreakDepth((Mono.Cecil.Cil.Instruction)ilInstruction.Operand);
                        yield return new BranchIf(breakFromBlockDepth);
                        break;
                    }
                case CILCode.Beq_S:
                    {
                        var breakFromBlockDepth = getBreakDepth((Mono.Cecil.Cil.Instruction)ilInstruction.Operand);
                        yield return new Int32Equal();
                        yield return new BranchIf(breakFromBlockDepth);
                        break;
                    }
                case CILCode.Bgt_S:
                    {
                        var breakFromBlockDepth = getBreakDepth((Mono.Cecil.Cil.Instruction)ilInstruction.Operand);
                        yield return new Int32GreaterThanSigned();
                        yield return new BranchIf(breakFromBlockDepth);
                        break;
                    }
                case CILCode.Call:
                    {
                        var target = (MethodDefinition)ilInstruction.Operand;
                        var targetWasmFuncIndex = wasmBuilder.GetOrReserveFunctionIndex(target.FullName);
                        yield return new Call(targetWasmFuncIndex);
                        break;
                    }
                case CILCode.Ceq:
                    {
                        // TODO: Get correct type
                        yield return new Int32Equal();
                        break;
                    }
                case CILCode.Clt:
                    {
                        // TODO: Get correct type
                        yield return new Int32LessThanSigned();
                        break;
                    }
                case CILCode.Cgt:
                    {
                        // TODO: Get correct type
                        yield return new Int32GreaterThanSigned();
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
                case CILCode.Ldloc_2:
                    {
                        yield return GetLocalInstruction(functionBuilder, "loc2");
                        break;
                    }
                case CILCode.Ldc_I4:
                    {
                        yield return new Int32Constant(Convert.ToInt32(ilInstruction.Operand));
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
                case CILCode.Starg_S:
                    {
                        var argIndex = ((ParameterDefinition)ilInstruction.Operand).Index;
                        yield return StoreLocalInstruction(functionBuilder, $"arg{argIndex}");
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
                case CILCode.Stloc_2:
                    {
                        yield return StoreLocalInstruction(functionBuilder, "loc2");
                        break;
                    }
                case CILCode.Sub:
                    {
                        // TODO: Type
                        yield return new Int32Subtract();
                        break;
                    }
                case CILCode.Switch:
                    {
                        yield return StoreLocalInstruction(functionBuilder, "switchValue");

                        var itemIndex = 0;
                        foreach (var target in ((Mono.Cecil.Cil.Instruction[])ilInstruction.Operand))
                        {
                            yield return GetLocalInstruction(functionBuilder, "switchValue");
                            yield return new Int32Constant(itemIndex++);
                            yield return new Int32Equal();

                            var breakFromBlockDepth = getBreakDepth(target);
                            yield return new BranchIf(breakFromBlockDepth);
                        }

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
