(function () {

  async function instantiateWasmModule(url) {
    const response = await fetch(url);
    const module = await WebAssembly.compileStreaming(response);
    return await WebAssembly.instantiate(module);
  }

  async function start() {
    const moduleInstance = await instantiateWasmModule('MyLibrary.wasm');
    console.log(`Exports: ${ Object.getOwnPropertyNames(moduleInstance.exports) }`);

    const fnToInvoke = moduleInstance.exports['System.Int32 MyLibrary.Test::Run(System.Int32)'];
    console.log(fnToInvoke(123));
  }

  start();
})();
