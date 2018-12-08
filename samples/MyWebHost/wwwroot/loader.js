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

  async function start() {
    const moduleInstance = await instantiateWasmModule('MyLibrary.wasm');
    console.log(`Exports: ${ Object.getOwnPropertyNames(moduleInstance.exports) }`);

    const fnToInvoke = moduleInstance.exports['System.Int32 MyLibrary.Test::Run(System.Int32)'];
    fnToInvoke(2000);
  }

  start();
})();
