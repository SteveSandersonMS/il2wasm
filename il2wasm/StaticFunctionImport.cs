using System.Collections.Generic;

namespace il2wasm
{
    public struct StaticFunctionImport
    {
        public readonly string ModuleName;
        public readonly string FieldName;
        public readonly WebAssembly.ValueType? ReturnType;
        public readonly IEnumerable<WebAssembly.ValueType> Parameters;

        public StaticFunctionImport(string moduleName, string fieldName, WebAssembly.ValueType? returnType, params WebAssembly.ValueType[] parameters)
        {
            ModuleName = moduleName;
            FieldName = fieldName;
            ReturnType = returnType;
            Parameters = parameters;
        }
    }
}
