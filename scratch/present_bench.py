"""Measures what a windowed GL present actually costs, as a function of window size.

The question this exists to answer: NFM-World's GL renderers all cap near 600fps while its D3D11 and
Vulkan renderers reach 1200-1600, and [DRAWPROF] says the GL paths are NOT CPU-bound - they spend
~1088us of a 1667us frame, leaving ~580us unaccounted, whereas sokol D3D11 is genuinely CPU-bound
(~794us of an 833us frame). Something outside the profiled render region is stalling the GL paths.

The candidate: a windowed WGL swap copies the back buffer to the compositor surface, so its cost
scales with window area, while DXGI flip-model presents by flipping and does not. That would hit
every GL consumer equally (our backend, ANGLE, and FNA3D's own GL backend) and appear in none of
them as render time.

So: swap an empty frame at three window sizes with vsync off, and see whether swap cost tracks area.

This is a measurement harness, not a test of the game - it makes its own window and draws nothing.
"""
import ctypes
import statistics
import time
from ctypes import c_char_p, c_int, c_uint32, c_uint64, c_void_p

SDL = ctypes.CDLL(r"W:\projects\NFM-World\NFMWorld.NativeLibs\libs\x64\SDL3.dll")

SDL.SDL_Init.argtypes = [c_uint32]
SDL.SDL_Init.restype = c_int
SDL.SDL_GL_SetAttribute.argtypes = [c_int, c_int]
SDL.SDL_GL_SetAttribute.restype = c_int
SDL.SDL_GL_LoadLibrary.argtypes = [c_char_p]
SDL.SDL_GL_LoadLibrary.restype = c_int
SDL.SDL_CreateWindow.argtypes = [c_char_p, c_int, c_int, c_uint64]
SDL.SDL_CreateWindow.restype = c_void_p
SDL.SDL_GL_CreateContext.argtypes = [c_void_p]
SDL.SDL_GL_CreateContext.restype = c_void_p
SDL.SDL_GL_GetProcAddress.argtypes = [c_char_p]
SDL.SDL_GL_GetProcAddress.restype = c_void_p
SDL.SDL_GL_SwapWindow.argtypes = [c_void_p]
SDL.SDL_GL_SwapWindow.restype = c_int
SDL.SDL_GL_SetSwapInterval.argtypes = [c_int]
SDL.SDL_GL_SetSwapInterval.restype = c_int
SDL.SDL_DestroyWindow.argtypes = [c_void_p]
SDL.SDL_GL_DestroyContext.argtypes = [c_void_p]
SDL.SDL_PumpEvents.argtypes = []
SDL.SDL_GetError.restype = c_char_p

SDL_GL_CONTEXT_MAJOR_VERSION, SDL_GL_CONTEXT_MINOR_VERSION = 17, 18
SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE = 20, 1
SDL_WINDOW_OPENGL, SDL_WINDOW_HIDDEN = 0x2, 0x8

GL_COLOR_BUFFER_BIT, GL_DEPTH_BUFFER_BIT = 0x4000, 0x100
GL_VENDOR, GL_RENDERER = 0x1F00, 0x1F01


def gl_proc(name, restype, argtypes):
    addr = SDL.SDL_GL_GetProcAddress(name)
    if not addr:
        raise RuntimeError(f"unresolved: {name}")
    return ctypes.CFUNCTYPE(restype, *argtypes)(addr)


def run_case(width, height, interval, frames, hidden):
    for attr, val in ((SDL_GL_CONTEXT_MAJOR_VERSION, 3), (SDL_GL_CONTEXT_MINOR_VERSION, 3),
                      (SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE)):
        SDL.SDL_GL_SetAttribute(attr, val)
    if not SDL.SDL_GL_LoadLibrary(None):
        raise RuntimeError(f"SDL_GL_LoadLibrary: {SDL.SDL_GetError()}")

    flags = SDL_WINDOW_OPENGL | (SDL_WINDOW_HIDDEN if hidden else 0)
    win = SDL.SDL_CreateWindow(b"present bench", width, height, flags)
    if not win:
        raise RuntimeError(f"SDL_CreateWindow: {SDL.SDL_GetError()}")
    ctx = SDL.SDL_GL_CreateContext(win)
    if not ctx:
        raise RuntimeError(f"SDL_GL_CreateContext: {SDL.SDL_GetError()}")

    SDL.SDL_GL_SetSwapInterval(interval)
    applied = SDL.SDL_GL_SetSwapInterval  # (the getter needs a GL context slot; assumed honoured)

    gl_viewport = gl_proc(b"glViewport", None, [c_int, c_int, c_int, c_int])
    gl_clear = gl_proc(b"glClear", None, [c_uint32])
    gl_clear_color = gl_proc(b"glClearColor", None, [ctypes.c_float] * 4)

    gl_viewport(0, 0, width, height)
    gl_clear_color(0.2, 0.3, 0.4, 1.0)

    for _ in range(60):                      # warmup: let the driver pick its paths
        gl_clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT)
        SDL.SDL_GL_SwapWindow(win)

    clear_ns, swap_ns, total_ns = [], [], []
    for _ in range(frames):
        SDL.SDL_PumpEvents()

        t0 = time.perf_counter_ns()
        gl_clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT)
        t1 = time.perf_counter_ns()
        SDL.SDL_GL_SwapWindow(win)
        t2 = time.perf_counter_ns()

        clear_ns.append(t1 - t0)
        swap_ns.append(t2 - t1)
        total_ns.append(t2 - t0)

    SDL.SDL_GL_DestroyContext(ctx)
    SDL.SDL_DestroyWindow(win)

    def us(xs):
        return statistics.median(xs) / 1000.0

    return us(clear_ns), us(swap_ns), us(total_ns), statistics.quantiles(total_ns, n=20)[18] / 1000.0


def main():
    if not SDL.SDL_Init(0x20):
        raise RuntimeError(f"SDL_Init: {SDL.SDL_GetError()}")

    # Report the driver once, so a surprising number can be attributed.
    SDL.SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 3)
    SDL.SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 3)
    SDL.SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE)
    SDL.SDL_GL_LoadLibrary(None)
    w = SDL.SDL_CreateWindow(b"probe", 64, 64, SDL_WINDOW_OPENGL | SDL_WINDOW_HIDDEN)
    SDL.SDL_GL_CreateContext(w)
    get_string = gl_proc(b"glGetString", c_void_p, [c_uint32])

    def s(enum):
        p = get_string(enum)
        return ctypes.string_at(p).decode() if p else "<null>"

    print(f"GL_VENDOR   : {s(GL_VENDOR)}")
    print(f"GL_RENDERER : {s(GL_RENDERER)}")
    SDL.SDL_DestroyWindow(w)

    print()
    print("Windowed GL present cost, vsync OFF. 'total' is clear+swap, i.e. one frame.")
    print("If swap cost tracks window AREA, the present path is copying to the compositor.")
    print()
    print(f"{'window':>12} {'vis':>4} {'interval':>9} | {'clear us':>9} {'swap us':>9} {'total us':>9} {'p95 total':>10} | {'implied fps':>11}")
    print("-" * 88)

    cases = [
        ((640, 360), 0, False),
        ((1280, 720), 0, False),
        ((1920, 1080), 0, False),
        ((2560, 1440), 0, False),
        ((1280, 720), 1, False),
        ((1280, 720), 0, True),      # hidden: no compositor, so this isolates the driver's own cost
    ]
    for (width, height), interval, hidden in cases:
        clear, swap, total, p95 = run_case(width, height, interval, 400, hidden)
        print(f"{f'{width}x{height}':>12} {str(not hidden):>4} {interval:>9} | "
              f"{clear:>9.1f} {swap:>9.1f} {total:>9.1f} {p95:>10.1f} | {1e6 / total:>11.0f}")


if __name__ == "__main__":
    main()
