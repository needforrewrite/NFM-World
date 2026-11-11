---
tags:
- graphics
- macos
- angle
- sdl
- startup
- gotcha
---
macOS ANGLE startup failure "SDL_CreateWindow failed: Could not initialize OpenGL / GLES library"
- SDL's EGL path (SDL_EGL_LoadLibraryInternal, src/video/SDL_egl.c) loads libGLESv2.dylib and libEGL.dylib by LEAF NAME via dlopen. On macOS a leaf name is searched in LD_LIBRARY_PATH, DYLD_LIBRARY_PATH, the process working directory, then DYLD_FALLBACK_LIBRARY_PATH — NOT the executable's folder. So launching the game from any cwd other than the output folder fails, even though both dylibs sit beside the binary. Working users launched with cwd = install dir.
- On the ANGLE arm the error surfaces from SDL_CreateWindow (Cocoa_CreateWindow -> Cocoa_GLES_SetupWindow -> SDL_EGL_LoadLibrary), not from SDL_GL_CreateContext. CreateGlContext's earlier SDL_GL_LoadLibrary uses the CGL loader (system libGL), so it succeeds first.
- Shipped SDL3 has no SDL_OPENGL_ES_DRIVER hint handling on Cocoa: the ES profile alone selects EGL. Desktop GL ("OpenGL" on mac) never goes through this path.
- Fix in WorldGame.CreateGlContext: on macOS set SDL_HINT_EGL_LIBRARY -> AppContext.BaseDirectory/libEGL.dylib and SDL_HINT_OPENGL_LIBRARY -> .../libGLESv2.dylib for the ES profile, and reset both for the Core profile (the same hint names the system OpenGL lib there). Warn if the files are missing.
