import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

class FakeNode {
    connections = [];
    gain = undefined;
    connect(target) { this.connections.push(target); }
    disconnect() { this.connections = []; }
}

class FakeSource extends FakeNode {
    playbackRate = { value: 1 };
    stopped = false;
    start() { }
    stop() { this.stopped = true; this.onended?.(); }
}

class FakeContext {
    destination = {};
    sources = [];
    panners = [];
    listener = Object.fromEntries(['positionX', 'positionY', 'positionZ', 'forwardX', 'forwardY', 'forwardZ',
        'upX', 'upY', 'upZ'].map(key => [key, { value: 0 }]));
    createBuffer(channels, frames, rate) {
        const data = Array.from({ length: channels }, () => new Float32Array(frames));
        return { duration: frames / rate, getChannelData: channel => data[channel] };
    }
    createBufferSource() {
        const node = new FakeSource();
        node.context = this;
        this.sources.push(node);
        return node;
    }
    createGain() {
        const node = new FakeNode();
        node.gain = { value: 1 };
        return node;
    }
    createPanner() {
        const node = new FakeNode();
        node.positionX = { value: 0 };
        node.positionY = { value: 0 };
        node.positionZ = { value: 0 };
        this.panners.push(node);
        return node;
    }
}

const contexts = [];
globalThis.AudioContext = class extends FakeContext {
    constructor() { super(); contexts.push(this); }
};
globalThis.addEventListener = () => { };
const source = await readFile(new URL('../wwwroot/audio.js', import.meta.url), 'utf8');
const audio = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
const buffer = audio.createBuffer(new Uint8Array([0, 0, 0xff, 0x7f]), 8000, 1);
const context = contexts[0];

test('cars share samples but have independent voices, positions and volume', () => {
    const a = audio.startBuffer(buffer, 0.4, 1, true);
    const b = audio.startBuffer(buffer, 0.7, 1, true);
    audio.setSourcePosition(a, -1000, -100, 2000);
    audio.setSourcePosition(b, 1000, 0, 500);
    const [first, second] = context.sources.slice(-2);
    assert.equal(first.buffer, second.buffer);
    assert.notEqual(first.panner, second.panner);
    assert.equal(first.panner.positionX.value, -1000);
    assert.equal(second.panner.positionX.value, 1000);
    assert.equal(first.panner.panningModel, 'HRTF');
    assert.equal(first.panner.rolloffFactor, 0);
    assert.deepEqual(first.connections, [first.gain]);
    assert.deepEqual(first.gain.connections, [first.panner]);
    audio.setSourceVolume(a, 0.1);
    assert.equal(first.gain.gain.value, 0.1);
    assert.equal(second.gain.gain.value, 0.7);
    audio.stopSource(a);
    assert.equal(second.stopped, false);
    assert.equal(audio.isSourceActive(b), true);
    assert.deepEqual(first.panner.connections, []);
    audio.stopSource(b);
});

test('listener follows the game camera including its negative Y up direction', () => {
    audio.setListener(5, -20, 15, 0, 0, 1, 0, -1, 0);
    assert.equal(context.listener.positionY.value, -20);
    assert.equal(context.listener.forwardZ.value, 1);
    assert.equal(context.listener.upY.value, -1);
});

test('pause and volume changes preserve the positional routing', () => {
    const id = audio.startBuffer(buffer, 0.8, 1, true);
    audio.setSourcePosition(id, 200, 0, 100);
    const node = context.sources.at(-1);
    audio.setSourcePaused(id, true);
    audio.setSourceVolume(id, 0.3);
    assert.equal(node.gain.gain.value, 0);
    audio.setSourcePaused(id, false);
    assert.equal(node.gain.gain.value, 0.3);
    assert.deepEqual(node.gain.connections, [node.panner]);
    audio.stopSource(id);
});

test('finished one-shots release their panner and leave other playback alone', () => {
    const effect = audio.startBuffer(buffer, 0.5, 1, false);
    audio.setSourcePosition(effect, 1, 2, 3);
    const node = context.sources.at(-1);
    const music = audio.startBuffer(buffer, 0.6, 1, true);
    const musicNode = context.sources.at(-1);
    node.onended();
    assert.equal(audio.isSourceActive(effect), false);
    assert.deepEqual(node.gain.connections, []);
    assert.deepEqual(node.panner.connections, []);
    assert.equal(musicNode.panner, undefined);
    assert.equal(audio.isSourceActive(music), true);
    audio.stopSource(music);
});
