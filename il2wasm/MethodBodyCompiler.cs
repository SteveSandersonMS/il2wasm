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
        const int MonoObjectHeaderLength = 8;

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

            if (!sourceMethod.HasBody)
            {
                result.Add(new End());
            }
            else
            {
                var jumpTargets = new FlowAnalysis(sourceMethod.Body).JumpTargetOffsets;

                result.Add(new Loop());

                var nextJumpTargetIndex = 0;
                var nextJumpTargetOffset = jumpTargets.Count > 0 ? (int?)jumpTargets[0] : null;
                Func<Mono.Cecil.Cil.Instruction, uint> getBreakDepth = (Mono.Cecil.Cil.Instruction instruction) =>
                {
                    for (var targetIndex = 0; targetIndex < jumpTargets.Count; targetIndex++)
                    {
                        if (jumpTargets[targetIndex] == instruction.Offset)
                        {
                            var depth = targetIndex - nextJumpTargetIndex;
                            if (depth >= 0)
                            {
                                // Forward jump
                                return (uint)depth;
                            }
                            else
                            {
                                // Backward jump
                                result.Add(new Int32Constant(targetIndex + 1));
                                result.Add(StoreLocalInstruction(functionBuilder, "jumpTarget"));
                                return (uint)(jumpTargets.Count - nextJumpTargetIndex);
                            }
                        }
                    }

                    throw new ArgumentException($"No available jump target with offset {instruction.Offset}");
                };

                // Open all the nested blocks that start at the top of the function
                for (var openBlockIndex = 0; openBlockIndex < jumpTargets.Count; openBlockIndex++)
                {
                    result.Add(new Block());
                }

                // Jump table for backward jumps
                if (jumpTargets.Count > 0)
                {
                    result.Add(new Block());
                    result.Add(GetLocalInstruction(functionBuilder, "jumpTarget"));
                    result.Add(new BranchTable((uint)jumpTargets.Count, Enumerable.Range(0, jumpTargets.Count).Select(x => (uint)x).ToList()));
                    result.Add(new End());
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

                while (nextJumpTargetIndex < jumpTargets.Count)
                {
                    result.Add(new End());
                    nextJumpTargetIndex++;
                }

                result.Add(new End()); // Loop

                // Although the control flow will never reach this (because .NET always includes an
                // explicit "ret" instruction), we have to put a fake return value on the stack at
                // the end, otherwise the WASM function will be invalid, because the static analyzer
                // won't realise we can't get here.
                if (functionBuilder.ResultType.HasValue)
                {
                    // TODO: Use correct type to match functionBuilder.ResultType
                    result.Add(new Int32Constant(-1));
                }

                result.Add(new End());
            }

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
                case CILCode.Blt_S:
                    {
                        var breakFromBlockDepth = getBreakDepth((Mono.Cecil.Cil.Instruction)ilInstruction.Operand);
                        yield return new Int32LessThanSigned();
                        yield return new BranchIf(breakFromBlockDepth);
                        break;
                    }
                case CILCode.Ble_S:
                    {
                        var breakFromBlockDepth = getBreakDepth((Mono.Cecil.Cil.Instruction)ilInstruction.Operand);
                        yield return new Int32LessThanOrEqualSigned();
                        yield return new BranchIf(breakFromBlockDepth);
                        break;
                    }
                case CILCode.Call:
                case CILCode.Callvirt: // TODO: Implement Callvirt properly (do actual virtual dispatch)
                    {
                        switch (ilInstruction.Operand)
                        {
                            case MethodDefinition targetDefinition:
                                {
                                    // Call to a method in the same assembly
                                    var targetWasmFuncIndex = wasmBuilder.GetOrReserveFunctionIndex(targetDefinition.FullName);
                                    yield return new Call(targetWasmFuncIndex);
                                    break;
                                }
                            case MethodReference targetReference:
                                {
                                    if (targetReference.FullName == "System.Void System.Object::.ctor()")
                                    {
                                        // No-op - just drop the pointer to "this"
                                        yield return new Drop();
                                    }
                                    else
                                    {
                                        // Call to a method in some other assembly
                                        // Currently this assumes it always corresponds to a static function import
                                        // TODO: Implement a way to call into a .NET function in the Mono WebAssembly interpreter
                                        //       To do this, during this compilation process, generate a separate global variable
                                        //       for each .NET method we want to reference. Also generate some kind of JSON manifest
                                        //       describing the mapping from .NET method to global variable index. Then when loading
                                        //       the .wasm module, first get Mono's function ID for each referenced .NET method and
                                        //       use it to prepopulate the corresponding globals.
                                        //       Then here we can call out to the static _mono_invoke_method (or whatever), passing
                                        //       the Mono function ID from the corresponding global as well as the args. That way,
                                        //       the calls don't have to go through JS at all.
                                        var targetImportIndex = wasmBuilder.GetStaticImportIndex(targetReference);
                                        yield return new Call(targetImportIndex);
                                    }
                                    break;
                                }
                            default:
                                throw new ArgumentException($"Unknown call instruction operand type: {ilInstruction.Operand.GetType().FullName}");
                        }
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
                case CILCode.Dup:
                    {
                        // TODO: Get correct type
                        yield return StoreLocalInstruction(functionBuilder, "dupTemp");
                        yield return GetLocalInstruction(functionBuilder, "dupTemp");
                        yield return GetLocalInstruction(functionBuilder, "dupTemp");
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
                case CILCode.Ldarg_2:
                    {
                        yield return GetLocalInstruction(functionBuilder, "arg2");
                        break;
                    }
                case CILCode.Ldfld:
                    {
                        var fieldOffset = GetFieldOffset((Mono.Cecil.FieldDefinition)ilInstruction.Operand);
                        yield return new Int32Load { Offset = fieldOffset };
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
                case CILCode.Ldloc_3:
                    {
                        yield return GetLocalInstruction(functionBuilder, "loc3");
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
                case CILCode.Ldloc_S:
                    {
                        var locIndex = ((Mono.Cecil.Cil.VariableDefinition)ilInstruction.Operand).Index;
                        yield return GetLocalInstruction(functionBuilder, $"loc{locIndex}");
                        break;
                    }
                case CILCode.Newobj:
                    {
                        var ctor = (Mono.Cecil.MethodDefinition)ilInstruction.Operand;

                        // Read all the params into locals so we can replay them after the new object address
                        for (var paramIndex = 0; paramIndex < ctor.Parameters.Count; paramIndex++)
                        {
                            yield return StoreLocalInstruction(functionBuilder, $"ctorparam{ctor.Parameters.Count - 1 - paramIndex}");
                        }

                        // Create heap object of desired type
                        yield return GetTypeHandleInstruction(wasmBuilder, ctor.DeclaringType);
                        yield return new Call(wasmBuilder.GetStaticImportIndex("mono_wasm_object_new"));
                        yield return StoreLocalInstruction(functionBuilder, "newObjectAddr");

                        // Invoke constructor
                        var targetWasmFuncIndex = wasmBuilder.GetOrReserveFunctionIndex(ctor.FullName);
                        yield return GetLocalInstruction(functionBuilder, "newObjectAddr");
                        for (var paramIndex = 0; paramIndex < ctor.Parameters.Count; paramIndex++)
                        {
                            yield return GetLocalInstruction(functionBuilder, $"ctorparam{paramIndex}");
                        }
                        yield return new Call(targetWasmFuncIndex);

                        yield return GetLocalInstruction(functionBuilder, "newObjectAddr");
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
                case CILCode.Pop:
                    {
                        yield return new Drop();
                        break;
                    }
                case CILCode.Rem:
                    {
                        // TODO: Determine correct type
                        yield return new Int32RemainderSigned();
                        break;
                    }
                case CILCode.Starg_S:
                    {
                        var argIndex = ((ParameterDefinition)ilInstruction.Operand).Index;
                        yield return StoreLocalInstruction(functionBuilder, $"arg{argIndex}");
                        break;
                    }
                case CILCode.Stfld:
                    {
                        var fieldOffset = GetFieldOffset((Mono.Cecil.FieldDefinition)ilInstruction.Operand);
                        yield return new Int32Store { Offset = fieldOffset };
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
                case CILCode.Stloc_3:
                    {
                        yield return StoreLocalInstruction(functionBuilder, "loc3");
                        break;
                    }
                case CILCode.Stloc_S:
                    {
                        var locIndex = ((Mono.Cecil.Cil.VariableDefinition)ilInstruction.Operand).Index;
                        yield return StoreLocalInstruction(functionBuilder, $"loc{locIndex}");
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

        private static Instruction GetTypeHandleInstruction(WasmModuleBuilder wasmBuilder, TypeDefinition typeDefinition)
        {
            return GetGlobalInstruction(wasmBuilder, $"type:{typeDefinition.Scope.Name}|{typeDefinition.FullName}");
        }

        private static uint GetFieldOffset(Mono.Cecil.FieldDefinition field)
        {
            uint offset = MonoObjectHeaderLength; // Skip this many bytes

            if (field.Offset >= 0)
            {
                // Explicit struct layout
                return offset + (uint)field.Offset;
            }

            foreach (var typeField in field.DeclaringType.Fields)
            {
                if (typeField == field)
                {
                    return offset;
                }

                offset += GetTypeStackSize(typeField.FieldType);
            }

            throw new InvalidOperationException($"Somehow, field {field.FullName} was not declared on its declaring type.");
        }

        private static uint GetTypeStackSize(Mono.Cecil.TypeReference field)
        {
            return (uint)4; // TODO
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

        private static Instruction GetGlobalInstruction(WasmModuleBuilder moduleBuilder, string globalName)
        {
            // TODO: Determine correct type
            var localIndex = moduleBuilder.GetGlobalIndex(globalName, WebAssembly.ValueType.Int32);
            return new GetGlobal(localIndex);
        }
    }
}
