using System.Linq;
using System.Collections;
using System.Collections.Generic;
using WebAssembly;
using WebAssembly.Instructions;
using Mono.Cecil;

namespace il2wasm
{
    class WasmModuleBuilder
    {
        private readonly Module _module = new Module();
        private Dictionary<string, uint> _functionIndicesByName = new Dictionary<string, uint>();
        private SortedDictionary<uint, WasmFunctionBuilder> _functionBuildersByIndex = new SortedDictionary<uint, WasmFunctionBuilder>();

        public Module ToModule()
        {
            foreach (var (fnIndex, fnBuilder) in _functionBuildersByIndex)
            {
                var typeIndex = AddType(fnBuilder.ParameterTypes, fnBuilder.ResultType);

                _module.Functions.Add(new Function(typeIndex));
                _module.Codes.Add(new FunctionBody
                {
                    Code = fnBuilder.Instructions,
                    Locals = fnBuilder.Locals,
                });

                if (fnBuilder.Export)
                {
                    _module.Exports.Add(new Export
                    {
                        Name = fnBuilder.Name,
                        Kind = ExternalKind.Function,
                        Index = fnIndex
                    });
                }
            }

            return _module;
        }

        private uint AddType(IList<WebAssembly.ValueType> parameterTypes, WebAssembly.ValueType? resultType)
        {
            for (var existingTypeIndex = 0; existingTypeIndex < _module.Types.Count; existingTypeIndex++)
            {
                var existingType = _module.Types[existingTypeIndex];

                var existingResultType = existingType.Returns.Any() ? existingType.Returns.Single() : (WebAssembly.ValueType?)null;
                if (existingResultType != resultType)
                {
                    continue;
                }

                var existingParameterTypes = existingType.Parameters;
                if (existingParameterTypes.Count != parameterTypes.Count)
                {
                    continue;
                }

                for (var paramIndex = 0; paramIndex < parameterTypes.Count; paramIndex++)
                {
                    if (existingParameterTypes[paramIndex] != parameterTypes[paramIndex])
                    {
                        continue;
                    }
                }

                // It's a match
                return (uint)existingTypeIndex;
            }

            // No match - add a new type
            var newTypeIndex = (uint)_module.Types.Count;
            var newType = new WebAssembly.Type { Parameters = parameterTypes };
            if (resultType.HasValue)
            {
                newType.Returns = new[] { resultType.Value };
            }
            _module.Types.Add(newType);
            return newTypeIndex;
        }

        public void AddFunction(uint reservedFunctionIndex, WasmFunctionBuilder builder)
        {
            _functionBuildersByIndex[reservedFunctionIndex] = builder;
        }

        private uint AddExport(WebAssembly.Export wasmExport)
        {
            var exportIndex = (uint)_module.Exports.Count;
            _module.Exports.Add(wasmExport);
            return exportIndex;
        }

        private uint AddFunctionBody(WebAssembly.FunctionBody wasmFunctionBody)
        {
            var functionBodyIndex = (uint)_module.Codes.Count;
            _module.Codes.Add(wasmFunctionBody);
            return functionBodyIndex;
        }

        public uint GetOrReserveFunctionIndex(string name)
        {
            if (!_functionIndicesByName.TryGetValue(name, out var index))
            {
                index = (uint)_functionIndicesByName.Count;
                _functionIndicesByName.Add(name, index);
            }

            return index;
        }
    }
}
