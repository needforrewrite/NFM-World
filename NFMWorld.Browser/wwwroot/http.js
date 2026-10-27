// Synchronous byte-range reads against a URL.
//
// System.Net.Http has no synchronous send on wasm (the runtime cannot block a thread), but
// ZipArchive needs a stream whose reads return immediately to parse the archive's directory.
// A synchronous XMLHttpRequest is the one API the browser offers that does exactly that, and it is
// allowed for a same-origin request - which this is, because the music archive is served beside
// the page.
//
// This is not a general-purpose fetch: it runs on the game thread while a track loads, and the
// game is paused for that moment anyway.

// A synchronous XHR cannot use responseType, so the body is read as a byte-per-char string and
// unpacked - the long-standing way to get binary out of a sync request.
function syncBytes(url, headers) {
    const xhr = new XMLHttpRequest();
    xhr.open('GET', url, false);
    for (const [k, v] of Object.entries(headers || {})) xhr.setRequestHeader(k, v);
    xhr.overrideMimeType('text/plain; charset=x-user-defined');
    xhr.send();

    const text = xhr.responseText;
    const bytes = new Uint8Array(text.length);
    for (let i = 0; i < text.length; i++) bytes[i] = text.charCodeAt(i) & 0xff;
    return { status: xhr.status, bytes };
}

export function contentLength(url) {
    const xhr = new XMLHttpRequest();
    xhr.open('HEAD', url, false);
    xhr.send();
    const length = xhr.getResponseHeader('content-length');
    return length ? Number(length) : -1;
}

// The bytes at [start, start+count). Returned rather than written into a caller's buffer: a
// byte[] argument is marshalled by copy into the call only, so writes to it never reach .NET.
export function readRange(url, start, count) {
    const end = start + count - 1;
    const { status, bytes } = syncBytes(url, { Range: `bytes=${start}-${end}` });

    if (status !== 200 && status !== 206) {
        throw new Error(`range ${start}+${count} of ${url}: ${status}`);
    }

    // A server without range support answers 200 with the whole file, in which case the slice has
    // to be taken here.
    return status === 200 ? bytes.slice(start, start + count) : bytes;
}
