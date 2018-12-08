using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace il2wasm
{
    class FlowAnalysis
    {
        public IReadOnlyList<int> JumpTargetOffsets { get; }

        public FlowAnalysis(MethodBody methodBody)
        {
            var sortedOffsets = new SortedSet<int>();

            foreach (var instruction in methodBody.Instructions)
            {
                if (TryGetJumpTarget(instruction, out var jumpTargets))
                {
                    foreach (var jumpTarget in jumpTargets)
                    {
                        sortedOffsets.Add(jumpTarget.Offset);
                    }
                }
            }

            JumpTargetOffsets = sortedOffsets.ToList();
        }

        private bool TryGetJumpTarget(Instruction instruction, out Instruction[] jumpTargets)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Br:
                case Code.Br_S:
                case Code.Brfalse:
                case Code.Brfalse_S:
                case Code.Brtrue:
                case Code.Brtrue_S:
                case Code.Beq:
                case Code.Beq_S:
                case Code.Bgt:
                case Code.Bgt_S:
                case Code.Blt:
                case Code.Blt_S:
                    jumpTargets = new[] { (Instruction)instruction.Operand };
                    return true;
                case Code.Switch:
                    jumpTargets = (Instruction[])instruction.Operand;
                    return true;
                default:
                    jumpTargets = Array.Empty<Instruction>();
                    return false;
            }
        }
    }
}
