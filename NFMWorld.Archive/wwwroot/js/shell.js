// Partial navigation for the archive.
//
// The 3D preview is an entire .NET runtime inside a frame: about a second of CPU to start, and a
// fraction of that to load another radpack once it is running. A normal page load throws the frame
// away and pays that second again, so internal navigations fetch the next document and swap its
// content in place, leaving one frame alive for the session.
//
// The frame is a sibling of the swap target and is never moved, removed or re-sourced: any of the
// three destroys its browsing context, which is the cost this whole file exists to avoid. Another
// asset is handed to it as a message instead.

const host = document.getElementById('preview-host');
const columns = document.getElementById('page-columns');

let frame = host.querySelector('iframe');

// The game origin: the only origin a message to that frame may name. Null until the first preview.
let gameOrigin = frame ? new URL(frame.src).origin : null;

// The asset the frame is showing, so a repeat visit only has to un-hide it. Only what has actually
// been handed over counts: a request made before the runtime answered `ready` is still pending, and
// treating it as shown would strand the frame on the previous asset.
let shownHash = readHash(frame);

// The frame answers `ready` once its runtime is up, which is when a message starts being delivered.
// Anything sent earlier would be dropped, so the last request waits here.
let ready = false;
let pending = null;

function readHash(node) {
    if (!node) return '';
    const src = node.getAttribute('src');
    return src ? new URL(src, document.baseURI).searchParams.get('hash') ?? '' : '';
}

function deliver(request) {
    shownHash = request.hash;
    pending = null;
    frame?.contentWindow?.postMessage(request, gameOrigin);
}

// Points the live frame at another asset. Nothing here reloads it: the runtime swaps its own phase.
function show(request) {
    if (!frame) return;

    frame.title = request.title || frame.title;
    if (request.hash === shownHash && !pending) return;

    if (ready) deliver(request);
    else pending = request;
}

// The first previewable page of the session has no frame yet, so this one boot is unavoidable.
// The src is the page's own cold-load URL, which keeps AssetPreview.Request the only place that
// knows how a preview is addressed.
function create(request) {
    frame = document.createElement('iframe');
    frame.id = 'preview-frame';
    frame.src = request.url;
    frame.title = request.title;
    frame.setAttribute('allowfullscreen', '');
    gameOrigin = new URL(request.url, document.baseURI).origin;
    shownHash = request.hash;
    ready = false;
    pending = null;
    host.append(frame);
}

// Applies the preview state of the page being entered, read from the document that will replace
// the current one. Its own frame is never attached - that would boot a second runtime.
function adopt(nextHost) {
    if (!nextHost || nextHost.hasAttribute('hidden')) {
        host.hidden = true;
        columns.classList.remove('preview-on');
        pending = null;
        return;
    }

    const source = nextHost.querySelector('iframe');
    const src = source?.getAttribute('src');
    if (!src) return;

    host.hidden = false;
    columns.classList.add('preview-on');

    const query = new URL(src, document.baseURI).searchParams;
    const request = {
        type: 'preview',
        hash: query.get('hash') ?? '',
        mode: query.get('preview') ?? '',
        archive: query.get('archive') ?? '',
        title: source.getAttribute('title') ?? ''
    };

    if (frame) show(request);
    else create({ ...request, url: src });
}

let latest = 0;

async function navigate(url, push) {
    const token = ++latest;

    let next;
    try {
        const response = await fetch(url, { headers: { Accept: 'text/html' } });
        const type = response.headers.get('Content-Type') ?? '';
        if (!response.ok || !type.includes('text/html')) {
            location.assign(url);
            return false;
        }

        next = new DOMParser().parseFromString(await response.text(), 'text/html');
    } catch {
        // No fetch, no partial navigation: the browser's own is the fallback for everything.
        location.assign(url);
        return false;
    }

    if (token !== latest) return false;

    const nextMain = next.getElementById('main-content');
    const nextHeader = next.querySelector('header.site-header');
    const main = document.getElementById('main-content');
    const header = document.querySelector('header.site-header');
    if (!nextMain || !nextHeader || !main || !header) {
        location.assign(url);
        return false;
    }

    // innerHTML does not run scripts, so a page that brings its own (the sign-in page's Turnstile
    // widget) needs the real thing. Asked of the document rather than marked on each link: a page
    // that grows a script keeps working without anyone remembering this file.
    if (nextMain.querySelector('script')) {
        location.assign(url);
        return false;
    }

    adopt(next.getElementById('preview-host'));

    main.innerHTML = nextMain.innerHTML;
    document.title = next.title;

    // The tab highlight is server-rendered, so copy it across rather than rebuilding the header -
    // rebuilding would re-create the sign-out form, and any focus in it, on every click.
    for (const link of header.querySelectorAll('[aria-current]')) link.removeAttribute('aria-current');
    for (const link of nextHeader.querySelectorAll('a[aria-current]')) {
        const mine = header.querySelector(`a[href="${CSS.escape(link.getAttribute('href') ?? '')}"]`);
        mine?.setAttribute('aria-current', link.getAttribute('aria-current') ?? 'page');
    }

    if (push) {
        history.replaceState({ ...history.state, scrollY: scrollY }, '');
        history.pushState({ url, scrollY: 0 }, '', url);
        main.focus({ preventScroll: true });
        scrollTo(0, 0);
    }

    return true;
}

addEventListener('message', event => {
    if (!frame || event.source !== frame.contentWindow) return;
    if (event.origin !== gameOrigin) return;

    const message = event.data;
    if (!message || typeof message !== 'object' || message.type !== 'ready') return;

    ready = true;
    if (pending) deliver(pending);
});

addEventListener('click', event => {
    if (event.defaultPrevented || event.button !== 0) return;
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

    const anchor = event.target.closest?.('a[href]');
    if (!anchor) return;

    // A download, a new tab and an explicit opt-out are all plain navigations.
    if (anchor.hasAttribute('download') || anchor.dataset.spa === 'off') return;
    if (anchor.target && anchor.target !== '_self') return;

    const url = new URL(anchor.href, document.baseURI);
    if (url.origin !== location.origin) return;
    if (url.href === location.href) return;

    // A fragment on the current document is a scroll. The browser does that already.
    if (url.hash && url.pathname === location.pathname && url.search === location.search) return;

    event.preventDefault();
    navigate(url.href, true);
});

addEventListener('popstate', async event => {
    if (!await navigate(location.href, false)) return;

    // The scroll the entry was left at. Manual restoration, because the document that recorded it
    // is gone by the time the browser would restore it.
    scrollTo(0, event.state?.scrollY ?? 0);
});

// The restored scroll position lands on a document that no longer exists; every navigation here
// places it itself.
history.scrollRestoration = 'manual';
