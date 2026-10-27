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
    // Music loops and sound effects do not, which is how stopAll tells them apart.
    node.looping = !!loop;
    if (loop) {
        // A looping track that should already be partway through starts at that point instead of
        // from the beginning, which is what makes a late-started track feel continuous.
        offset = offset % b.duration;
    }
    node.playbackRate.value = rate > 0 ? rate : 1;
    // The gain goes up with the source rather than on the first setSourceVolume: a track would
    // otherwise play its first moment at full volume whatever it was started with.
    node.userVolume = Math.max(0, volume);
    node.connect(c.destination);
    gainOf(node).gain.value = node.userVolume;

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

// AudioBufferSourceNode has no gain of its own, so a per-source node is inserted on demand. Every
// volume change goes through it, and pausing rides the same node as a mute - see setSourcePaused.
function gainOf(node) {
    if (!node.gain) {
        const g = node.context.createGain();
        node.disconnect();
        node.connect(g);
        g.connect(node.context.destination);
        node.gain = g;
    }
    return node.gain;
}

export function setSourceVolume(id, volume) {
    const node = sources.get(id);
    if (!node) return;
    node.userVolume = Math.max(0, volume);
    // A paused source stays silent whatever its volume is set to; the value is remembered for
    // when it resumes.
    gainOf(node).gain.value = node.paused ? 0 : node.userVolume;
}

export function setSourceRate(id, rate) {
    const node = sources.get(id);
    if (node) node.playbackRate.value = rate > 0 ? rate : 1;
}

// Pausing mutes this one source, not the context. Suspending the context was the obvious reading of
// "pause the music" and is wrong: it silences every sound effect with it, and every source started
// during the gap - the next stage's music included - queues up and then all plays at once on resume.
// Muting keeps the track's position, which is all a paused music track needs.
export function setSourcePaused(id, paused) {
    const node = sources.get(id);
    if (!node) return;
    node.paused = !!paused;
    gainOf(node).gain.value = node.paused ? 0 : (node.userVolume ?? 1);
}

export function releaseBuffer(id) {
    buffers.delete(id);
}

// Sound effects only. The desktop backend's equivalent walks its clip list and leaves the music
// playing, so stopping every source here would make the browser build the odd one out.
export function stopAll() {
    for (const id of [...sources.keys()]) {
        const node = sources.get(id);
        if (node && !node.looping) stopSource(id);
    }
}
