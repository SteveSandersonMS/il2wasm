﻿using System.Linq;
using System.Collections.Generic;
using WebAssembly;
using System;

namespace il2wasm
{
    class WasmModuleBuilder
    {
        private readonly Module _module = new Module();
        private Dictionary<string, uint> _functionIndicesByName = new Dictionary<string, uint>();
        private SortedDictionary<uint, WasmFunctionBuilder> _functionBuildersByIndex = new SortedDictionary<uint, WasmFunctionBuilder>();
        private Dictionary<string, uint> _globalsIndicesByName = new Dictionary<string, uint>();
        private SortedDictionary<uint, (string, WebAssembly.ValueType)> _globalsByIndex = new SortedDictionary<uint, (string, WebAssembly.ValueType)>();

        // This has to be read-only, because we need to know how many of them exist before we
        // can assign any function indices
        private readonly IReadOnlyList<StaticFunctionImport> _functionImports = new List<StaticFunctionImport>
        {
            new StaticFunctionImport("sys", "mono_wasm_object_new", WebAssembly.ValueType.Int32, WebAssembly.ValueType.Int32),
            new StaticFunctionImport("static", "netstandard|System.Console|Void WriteLine(Int32)", null, WebAssembly.ValueType.Int32)
        };

        public Module ToModule()
        {
            _module.Imports.Add(new Import.Memory
            {
                Module = "sys",
                Field = "memory",
                Type = new Memory()
            });

            foreach (var functionImport in _functionImports)
            {
                var typeIndex = AddType(functionImport.Parameters.ToList(), functionImport.ReturnType);
                _module.Imports.Add(new Import.Function
                {
                    Module = functionImport.ModuleName,
                    Field = functionImport.FieldName,
                    TypeIndex = typeIndex
                });
            }

            foreach (var (globalIndex, (globalName, valueType)) in _globalsByIndex)
            {
                _module.Imports.Add(new Import.Global
                {
                    Module = "dotnet",
                    Field = globalName,
                    Type = new Global
                    {
                        ContentType = valueType,
                        IsMutable = false
                    }
                });
            }

            foreach (var (fnIndex, fnBuilder) in _functionBuildersByIndex)
            {
                var typeIndex = AddType(fnBuilder.ParameterTypes, fnBuilder.ResultType);

                _module.Functions.Add(new Function(typeIndex));
                _module.Codes.Add(new FunctionBody
                {
                    Code = fnBuilder.Instructions,
                    Locals = fnBuilder.Locals,
                });

                if (fnBuilder.ExportName != null)
                {
                    _module.Exports.Add(new Export
                    {
                        Name = fnBuilder.ExportName,
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

        public uint GetOrReserveFunctionIndex(string name)
        {
            if (!_functionIndicesByName.TryGetValue(name, out var index))
            {
                // We have to reserve the first N slots for the static imports
                // Unfortunately there's no way to interleave the imported functions with the self-defined ones
                // Maybe there is in WebAssembly itself, but not in the .NET library we're using to generate the .wasm file
                index = (uint)(_functionImports.Count + _functionIndicesByName.Count);
                _functionIndicesByName.Add(name, index);
            }

            return index;
        }

        public uint GetStaticImportIndex(Mono.Cecil.MethodReference methodReference)
        {
            var assemblyName = methodReference.DeclaringType.Scope.Name; // Not 100% certain this is correct, but does return 'netstandard' when I expect it to
            var declaringType = methodReference.DeclaringType;
            var formattedName = $"{assemblyName}|{declaringType.Namespace}.{declaringType.Name}|{Compiler.FormatMethodSignature(methodReference)}";
            return GetStaticImportIndex(formattedName);
        }

        public uint GetStaticImportIndex(string formattedName)
        {
            for (var i = 0; i < _functionImports.Count; i++)
            {
                if (_functionImports[i].FieldName == formattedName)
                {
                    return (uint)i;
                }
            }

            throw new ArgumentException($"No static function import for '{formattedName}'");
        }

        public uint GetGlobalIndex(string name, WebAssembly.ValueType valueType)
        {
            if (!_globalsIndicesByName.TryGetValue(name, out var index))
            {
                index = (uint)_globalsByIndex.Count;
                _globalsByIndex.Add(index, (name, valueType));
                _globalsIndicesByName.Add(name, index);
            }

            return index;
        }
    }
}
