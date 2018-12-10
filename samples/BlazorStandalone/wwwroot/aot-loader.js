// TODO: Update the Blazor build process so that, for assemblies being AOT compiled,
// we automatically add them to the boot JSON file. Then, have the client-side
// startup code automatically load the references .wasm files and wire them up
// at the right time.
(function () {
  async function instantiateWasmModule(url) {
    const response = await fetch(url);
    const module = await WebAssembly.compileStreaming(response);
    const memory = new WebAssembly.Memory({ initial: 1 });
    let nextHeapObjectAddr = 0;
    return await WebAssembly.instantiate(module, {
      sys: {
        malloc: numBytes => {
          const result = nextHeapObjectAddr;
          nextHeapObjectAddr += numBytes;
          return result;
        },
        memory: memory,
      },
      static: {
        'System.Void System.Console::WriteLine(System.Int32)': console.log
      }
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
})();
