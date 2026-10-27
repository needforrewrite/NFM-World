// Boots the game, then drives one frame per animation frame. The game's own loop cannot run in
// the browser: it sleeps and spins, which on the main thread would block the page.

import { dotnet } from './_framework/dotnet.js';

const canvas = document.getElementById('canvas');
const boot = document.getElementById('boot');

boot.textContent = 'loading runtime…';

// dotnet.instance only exists once the runtime is created, so the canvas is handed over after.
const { getAssemblyExports, getConfig } = await dotnet.create();

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
    exports.NFMWorld.Browser.Program.Main();
} catch (e) {
    boot.textContent = `boot failed: ${e}`;
    throw e;
}

boot.remove();

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
