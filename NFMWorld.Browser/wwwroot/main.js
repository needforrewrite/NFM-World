// Boots the game, then drives one frame per animation frame. The game's own loop cannot run in
// the browser: it sleeps and spins, which on the main thread would block the page.

import { dotnet } from './_framework/dotnet.js';

const canvas = document.getElementById('canvas');
const boot = document.getElementById('boot');

boot.textContent = 'loading runtime…';

// The WebGPU device is requested here, before the game boots, because a request answers through a
// promise and the game's boot is synchronous: it can await nothing. The device is parked on the
// module below and `emscripten_webgpu_get_device` imports it with no future to wait on.
//
// Float32 filtering is requested rather than optional - the shadow cascade is a Single-format
// texture, and without the feature its sampler is unfilterable. Browsers that do not offer it
// still get a device; the backend reports the shortfall in its boot line.
let webgpu = null;
if (navigator.gpu) {
    try {
        const adapter = await navigator.gpu.requestAdapter({ powerPreference: 'high-performance' });
        if (adapter) {
            const requiredFeatures = ['float32-filterable'].filter(f => adapter.features.has(f));
            webgpu = await adapter.requestDevice({ requiredFeatures });
        }
    } catch (e) {
        console.warn('[nfmw] no WebGPU device:', e);
    }
}

// CoreCLR: the runtime object is what carries the interop surface (getAssemblyExports,
// setModuleImports and the emscripten Module), not the `dotnet` namespace. Mono's `dotnet.instance`
// does not exist here, so everything below goes through this object.
const runtime = await dotnet.create();

// SDL's emscripten backend and Silk.NET's extension query both look for the canvas by this name.
runtime.Module['canvas'] = canvas;

if (webgpu) {
    // Read by emscripten_webgpu_get_device on the first call, which is the game's boot.
    runtime.Module['preinitializedWebGPUDevice'] = webgpu;
}

// WebGL2 has no glGetStringi, so the extension list Silk.NET probes during GetApi comes from here.
// Lazy on purpose: this is only called when the game actually takes the WebGL2 path. Calling it at
// load would bind the canvas to WebGL2, and a canvas holds one context type - the WebGPU surface
// would then find `getContext('webgpu')` returning null.
window.__nfmwGlExtensions = () => {
    const gl = canvas.getContext('webgl2');
    return gl ? gl.getSupportedExtensions() : [];
};

// The game's audio plays through Web Audio. The runtime resolves [JSImport] against modules it
// has imported itself, so this registers the file with it rather than a plain ES import.
const audio = await import('./audio.js');
runtime.setModuleImports('audio.js', audio);

// The music archive is parsed by ZipArchive, which is synchronous - see http.js.
const http = await import('./http.js');
runtime.setModuleImports('http.js', http);

const config = runtime.getConfig();
const exports = await runtime.getAssemblyExports(config.mainAssemblyName);

boot.textContent = 'loading assets…';

// The browser has no install directory, so the game's data comes from this archive. Music is
// deliberately not in it - it is most of the tree and is fetched per track instead.
const assets = await fetch('./data.boot.zip');
if (!assets.ok) {
    boot.textContent = `could not load data.boot.zip (${assets.status})`;
    throw new Error(`data.boot.zip: ${assets.status}`);
}
// The music archive is read on demand over range requests, so the head only needs its URL.
const musicUrl = new URL('./data.music.zip', document.baseURI).toString();
exports.NFMWorld.Browser.Program.MountAssets(new Uint8Array(await assets.arrayBuffer()), musicUrl);

boot.textContent = 'starting…';

try {
    // The size is handed over before boot rather than applied afterwards: the first phase sizes its
    // camera from the window when it is entered, so a window created at the default 1280x720 and
    // resized a moment later leaves the camera with a 1280x720 aspect until something resizes the
    // page again. Main itself stays parameterless - it is the assembly entry point, and the runtime
    // calls it with no arguments.
    exports.NFMWorld.Browser.Program.SetBootSize(innerWidth, innerHeight);
    // The renderer list and the renderer itself both come from this, so it lands before Main.
    exports.NFMWorld.Browser.Program.SetWebGpuAvailable(webgpu !== null);
    exports.NFMWorld.Browser.Program.Main();
} catch (e) {
    boot.textContent = `boot failed: ${e}`;
    throw e;
}

boot.remove();

// The canvas is laid out by CSS but its backing store is not, so the game has to be told the page
// size. SDL's emscripten backend does the actual resizing from here - it sets the canvas to the
// window size scaled by the device pixel ratio - so this only forwards the size. Nothing else
// reports it: a canvas that CSS has laid out does not resize its backing store by itself.
//
// The call at boot is not optional. The game creates its context at the canvas's declared size,
// so without it the first frame draws 1280x720 into a full-window canvas and stays that way until
// something else resizes the page.
function syncSize() {
    exports.NFMWorld.Browser.Program.Resize(innerWidth, innerHeight);
}
addEventListener('resize', syncSize);
syncSize();

let last = 0;
function frame(now) {
    requestAnimationFrame(frame);
    // requestAnimationFrame can fire twice in one millisecond, and a zero-length frame would make
    // the game's own timing divide by zero.
    if (now - last < 1) return;
    last = now;
    exports.NFMWorld.Browser.Program.Tick();
}
requestAnimationFrame(frame);
