using Microsoft.JSInterop;
using System.Reflection;

namespace BlazorStandalone
{
    public static class MethodFinder
    {
        // TODO: Don't have this at all. It's a hack to work around the fact that our build of Mono doesn't export 'mono_class_get_methods'
        // or any other way to locate a specific method overload by signature. The workaround is to do the lookups inside interpreted .NET
        // code. A real implementation would involve exporting 'mono_class_get_methods' and then calling it directly from JS when wiring up
        // the imports to an AOT .wasm module.
        [JSInvokable]
        public static int FindMethod(string assemblyName, string typeName, string signature)
        {
            var assembly = Assembly.Load(assemblyName);
            var type = assembly.GetType(typeName, throwOnError: true);
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i].ToString() == signature)
                {
                    return methods[i].MethodHandle.Value.ToInt32();
                }
            }
            return 0;
        }
    }
}
