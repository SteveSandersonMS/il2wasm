// TODO: Update the Blazor build process so that, for assemblies being AOT compiled,
// we automatically add them to the boot JSON file. Then, have the client-side
// startup code automatically load the references .wasm files and wire them up
// at the right time.
(function () {
  let invoke_method;

  function callInterpreterMethod(methodHandle, targetPtr, args) {
      if (args.length > 4) {
        // Hopefully this restriction can be eased soon, but for now make it clear what's going on
        throw new Error(`Currently, MonoPlatform supports passing a maximum of 4 arguments from JS to .NET. You tried to pass ${args.length}.`);
      }

      const stack = Module.stackSave();

      try {
        const argsBuffer = Module.stackAlloc(args.length * 4);
        const exceptionFlagManagedInt = Module.stackAlloc(4);
        for (var i = 0; i < args.length; ++i) {
          // Assume the arg is of type Int32. In the case of value types, mono_runtime_invoke expects to be passed the address
          // of the value type data (whereas for objects, it expects the MonoObject*, i.e., heap address).
          // TODO: Deal with other types somehow.
          const i32argBuffer = Module.stackAlloc(4);
          Module.setValue(i32argBuffer, args[i], 'i32');
          Module.setValue(argsBuffer + i * 4, i32argBuffer, 'i32');
        }
        Module.setValue(exceptionFlagManagedInt, 0, 'i32');

        const res = invoke_method(methodHandle, targetPtr, argsBuffer, exceptionFlagManagedInt);

        if (Module.getValue(exceptionFlagManagedInt, 'i32') !== 0) {
          // If the exception flag is set, the returned value is exception.ToString()
          // TODO: Somehow figure out how to dispatch the exception back into AOT code
          throw new Error(Blazor.platform.toJavaScriptString(res));
        }
  
        return res;
    } finally {
      Module.stackRestore(stack);
    }
  }
        
  async function instantiateWasmModule(url) {
    const response = await fetch(url);
    const module = await WebAssembly.compileStreaming(response);
    await monoRuntimeIsReady;

    // Get the list of .NET methods it imports from other assemblies
    invoke_method = Module.cwrap('mono_wasm_invoke_method', 'number', ['number', 'number', 'number']);
    const declaredStaticImports = WebAssembly.Module.imports(module)
      .filter(x => x.kind === 'function' && x.module === 'static')
      .map(x => {
        const tokens = x.name.split('|');
        return { identifier: x.name, assemblyName: tokens[0], typeName: tokens[1], methodNameWithSignature: tokens[2] };
      });

    // Build an imports object by providing a thunk that can call the interpreter for each imported method
    const staticImportThunks = {};
    declaredStaticImports.forEach(function (entry) {
      const methodHandle = DotNet.invokeMethod('BlazorStandalone', 'FindMethod', entry.assemblyName, entry.typeName, entry.methodNameWithSignature);
      if(methodHandle) {
        staticImportThunks[entry.identifier] = function() {
          const argsArray = Array.prototype.slice.call(arguments);
          callInterpreterMethod(methodHandle, /* targetPtr */ 0, argsArray);
        };
      }
    });

    const memory = Module.wasmMemory; // Share same memory as Mono interpreter
    let nextHeapObjectAddr = 0;
    return await WebAssembly.instantiate(module, {
      sys: {
        malloc: numBytes => {
          // TODO: Don't just corrupt the Mono heap like this. Replace this 'malloc' with a 'createUninitalizedObject(Type)' that calls Mono (so we alloc real objects that can later be GCed)
          const result = nextHeapObjectAddr;
          nextHeapObjectAddr += numBytes;
          return result;
        },
        memory: memory
      },
      static: staticImportThunks
    });
  }

  function encodeDots(str) {
    return str.replace(/\./g, '-');
  }

  window.loadAotAssembly = async function (aotAssemblyName) {
    const exports = {};
    window.aot = window.aot || {};
    window.aot[encodeDots(aotAssemblyName)] = exports;

    const wasmModule = await instantiateWasmModule(`_framework/wasm/${aotAssemblyName}.wasm`);
    Object.getOwnPropertyNames(wasmModule.exports).forEach(exportName => {
      exports[encodeDots(exportName)] = wasmModule.exports[exportName];
    });
  };

  // This is kind of ridiculous, but since there isn't any more straightforward way to hook
  // into the loading process, poll until it is ready enough
  const monoRuntimeIsReady = new Promise(resolve => {
    const intervalHandle = setInterval(() => {
      if (typeof MONO !== 'undefined' && MONO.mono_wasm_runtime_is_ready) {
        clearInterval(intervalHandle);
        resolve();
      }
    }, 10);
  });
})();
