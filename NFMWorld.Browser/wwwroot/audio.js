// Web Audio output for the game's decoded PCM.
//
// FAudio has no wasm build, so the game's clips and music play through AudioBufferSourceNodes.
// The decoded samples arrive as 16-bit PCM and are converted here; everything about how a sound is
// produced (decoding, tempo stretching, looping decisions) stays in managed code, which is why
// this file is only buffers and nodes.

let ctx = null;

// Browsers refuse to start an AudioContext outside a user gesture. The game boots before any
// interaction, so the context is created lazily and resumed on the first input event.
function context() {
    if (ctx) return ctx;
    const Ctor = globalThis.AudioContext || globalThis.webkitAudioContext;
    if (!Ctor) return null;
    ctx = new Ctor();
    const resume = () => { if (ctx.state !== 'running') ctx.resume(); };
    for (const e of ['pointerdown', 'keydown', 'touchstart']) {
        globalThis.addEventListener(e, resume, { passive: true });
    }
    return ctx;
}

const buffers = new Map();
const sources = new Map();
let nextId = 1;

export function audioAvailable() {
    return context() !== null;
}

export function createBuffer(pcm, sampleRate, channels) {
    const c = context();
    if (!c || !pcm || !pcm.length) return 0;

    const frames = Math.floor(pcm.length / (2 * channels));
    const buffer = c.createBuffer(channels, frames, sampleRate);
    for (let ch = 0; ch < channels; ch++) {
        const out = buffer.getChannelData(ch);
        // Interleaved little-endian int16 to per-channel floats.
        for (let i = 0; i < frames; i++) {
            const o = (i * channels + ch) * 2;
            const v = (pcm[o] | (pcm[o + 1] << 8)) << 16 >> 16;
            out[i] = v / 32768;
        }
    }

    const id = nextId++;
    buffers.set(id, buffer);
    return id;
}

export function startBuffer(buffer, volume, rate, loop) {
    return startBufferAt(buffer, volume, rate, loop, 0);
}

export function startBufferAt(buffer, volume, rate, loop, offset) {
    const c = context();
    const b = buffers.get(buffer);
    if (!c || !b) return 0;

    const node = c.createBufferSource();
    node.buffer = b;
    node.loop = !!loop;
    if (loop) {
        // A looping track that should already be partway through starts at that point instead of
        // from the beginning, which is what makes a late-started track feel continuous.
        offset = offset % b.duration;
    }
    node.playbackRate.value = rate > 0 ? rate : 1;
    node.connect(c.destination);

    const id = nextId++;
    node.onended = () => sources.delete(id);
    sources.set(id, node);
    node.start(0, loop ? offset : 0);
    return id;
}

export function stopSource(id) {
    const node = sources.get(id);
    if (!node) return;
    sources.delete(id);
    try { node.stop(); } catch { /* already ended */ }
}

export function setSourceVolume(id, volume) {
    const node = sources.get(id);
    if (!node) return;
    // AudioBufferSourceNode has no gain of its own, so a per-source node is inserted on demand.
    if (!node.gain) {
        const g = node.context.createGain();
        node.disconnect();
        node.connect(g);
        g.connect(node.context.destination);
        node.gain = g;
    }
    node.gain.gain.value = Math.max(0, volume);
}

export function setSourceRate(id, rate) {
    const node = sources.get(id);
    if (node) node.playbackRate.value = rate > 0 ? rate : 1;
}

// A source node cannot be paused, only stopped and re-created, so pausing suspends the whole
// context while this is the only thing playing - which is what the desktop backend's
// Pause/Resume does for a music track anyway.
export function setSourcePaused(id, paused) {
    const c = context();
    if (!c) return;
    if (paused) c.suspend(); else c.resume();
}

export function releaseBuffer(id) {
    buffers.delete(id);
}

export function stopAll() {
    for (const id of [...sources.keys()]) stopSource(id);
}
