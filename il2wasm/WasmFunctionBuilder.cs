using System.Collections.Generic;
using WebAssembly;
using System;

namespace il2wasm
{
    class WasmFunctionBuilder
    {
        public string Name { get; }
        public bool Export { get; set; }
        public List<Instruction> Instructions { get; }
        public List<Local> Locals { get; }
        public List<WebAssembly.ValueType> ParameterTypes { get; }
        public WebAssembly.ValueType? ResultType { get; set; }

        private readonly Dictionary<string, (uint, WebAssembly.ValueType)> _localsByName;

        public WasmFunctionBuilder(string name, WasmModuleBuilder wasmBuilder)
        {
            Name = name;
            ParameterTypes = new List<WebAssembly.ValueType>();
            Instructions = new List<Instruction>();
            Locals = new List<Local>();
            _localsByName = new Dictionary<string, (uint, WebAssembly.ValueType)>();

            var functionIndex = wasmBuilder.GetOrReserveFunctionIndex(Name);
            wasmBuilder.AddFunction(functionIndex, this);
        }

        public int AddParameter(WebAssembly.ValueType parameterType)
        {
            ParameterTypes.Add(parameterType);
            return ParameterTypes.Count - 1;
        }

        public uint GetLocalIndex(string name, WebAssembly.ValueType valueType)
        {
            if (_localsByName.TryGetValue(name, out var existingLocal))
            {
                if (existingLocal.Item2 != valueType)
                {
                    throw new ArgumentException($"Trying to redefine existing local '{existingLocal.Item2} {name}' as type {valueType}.");
                }

                return existingLocal.Item1;
            }

            var newIndex = (uint)_localsByName.Count;
            _localsByName.Add(name, (newIndex, valueType));
            Locals.Add(new Local { Type = valueType, Count = 1 });
            return newIndex;
        }
    }
}
