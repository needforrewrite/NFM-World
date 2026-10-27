// Boots the game, then drives one frame per animation frame. The game's own loop cannot run in
// the browser: it sleeps and spins, which on the main thread would block the page.

import { dotnet } from './_framework/dotnet.js';

const canvas = document.getElementById('canvas');
const boot = document.getElementById('boot');

boot.textContent = 'loading runtime…';

// The jiterpreter compiles its jit-call wrappers with wasm-EH try_table blocks whenever the
// browser supports exception handling, but this module is linked with Emscripten's legacy
// catching (-fexceptions), so it has no wasm tag to point them at and every wrapper fails to
// compile. Two failures disable the jiterpreter for the rest of the session, so it has to be
// off. The browser feature check in dotnet.runtime.js only asks the engine, not the module.
//
// dotnet.instance only exists once the runtime is created, so the canvas is handed over after.
const { getAssemblyExports, getConfig } =
    await dotnet.withRuntimeOptions(['--no-jiterpreter-wasm-eh-enabled']).create();

// SDL's emscripten backend and Silk.NET's extension query both look for the canvas by this name.
dotnet.instance.Module['canvas'] = canvas;

// WebGL2 has no glGetStringi, so the extension list Silk.NET probes during GetApi comes from here.
window.__nfmwGlExtensions = () => {
    const gl = canvas.getContext('webgl2');
    return gl ? gl.getSupportedExtensions() : [];
};

// The game's audio plays through Web Audio. The runtime resolves [JSImport] against modules it has
// imported itself, so this registers the file with it rather than a plain ES import.
const audio = await import('./audio.js');
dotnet.instance.setModuleImports('audio.js', audio);

// The music archive is parsed by ZipArchive, which is synchronous - see http.js.
const http = await import('./http.js');
dotnet.instance.setModuleImports('http.js', http);

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

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
