using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace il2wasm
{
    class FlowAnalysis
    {
        public Block RootBlock { get; }

        public FlowAnalysis(MethodBody methodBody)
        {
            var numInstructions = methodBody.Instructions.Count;
            var outerBlock = new Block(0, methodBody.Instructions[numInstructions - 1].Offset + 1);

            var blocks = new List<Block>();
            blocks.Add(outerBlock);

            for (var instructionIndex = 0; instructionIndex < numInstructions; instructionIndex++)
            {
                var instruction = methodBody.Instructions[instructionIndex];
                if (TryGetJumpTarget(instruction, out var jumpTargets))
                {
                    foreach (var jumpTarget in jumpTargets)
                    {
                        blocks.Add(new Block(0, jumpTarget.Offset - 1));
                    }
                }
            }

            // Sort in reverse order of EndIndexExcl. The EndIndexExcl values are fixed and
            // the highest EndIndexExcl values must contain more instructions than lower ones. 
            blocks.Sort((l, r) => r.EndIndexExcl - l.EndIndexExcl);

            var blockStack = new Stack<Block>();
            blockStack.Push(blocks[0]);
            for (var blockIndex = 1; blockIndex < blocks.Count; blockIndex++)
            {
                var block = blocks[blockIndex];
                var stackTopBlock = blockStack.Peek();

                while (block.EndIndexExcl <= stackTopBlock.StartIndexIncl)
                {
                    // This block is disjoint from stackTopBlock - it's completely before it
                    blockStack.Pop();
                    stackTopBlock = blockStack.Peek();
                }

                if (block.StartIndexIncl >= stackTopBlock.StartIndexIncl
                    && block.EndIndexExcl <= stackTopBlock.EndIndexExcl)
                {
                    stackTopBlock.Children.Add(block);
                    stackTopBlock.Children.Sort((lhs, rhs) => lhs.StartIndexIncl - rhs.StartIndexIncl);
                    blockStack.Push(block);
                }
                else
                {
                    throw new NotImplementedException("Overlapping blocks");
                }
            }

            RootBlock = outerBlock;
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

        public class Block
        {
            public int StartIndexIncl { get; private set; }
            public int EndIndexExcl { get; private set; }
            public List<Block> Children { get; }

            public Block(int startIndexIncl, int endIndexExcl)
            {
                StartIndexIncl = startIndexIncl;
                EndIndexExcl = endIndexExcl;
                Children = new List<Block>();
            }

            public bool Contains(int instructionOffset)
            {
                return instructionOffset >= StartIndexIncl && instructionOffset < EndIndexExcl;
            }
        }
    }
}
