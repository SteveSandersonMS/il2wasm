using Microsoft.AspNetCore.Components.Hosting;
using Microsoft.JSInterop;
using System;
using System.Linq;
using System.Reflection;

namespace BlazorStandalone
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IWebAssemblyHostBuilder CreateHostBuilder(string[] args) =>
            BlazorWebAssemblyHost.CreateDefaultBuilder()
                .UseBlazorStartup<Startup>();

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
