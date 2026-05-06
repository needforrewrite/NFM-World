
#ifndef SDLGPU_HLSLI
#define SDLGPU_HLSLI

// Register binding macros for SDL3 GPU HLSL shaders
//
// SDL_CreateGPUShader requires resources to be bound in a specific order
// per shader stage. These macros encode that convention so you don't have
// to remember the space assignments.
//
// See: https://wiki.libsdl.org/SDL3/SDL_CreateGPUShader
//
// Binding order within each space:
//   t[n]: sampled textures, then storage textures, then storage buffers
//   s[n]: samplers (indices match the sampled textures)
//   b[n]: uniform buffers

// ─── Vertex stage ───────────────────────────────────────────────────────────

#define SDL_VS_TEXTURE(slot)        register(t##slot, space0)
#define SDL_VS_SAMPLER(slot)        register(s##slot, space0)
#define SDL_VS_UNIFORM(slot)        register(b##slot, space1)

// ─── Fragment / pixel stage ─────────────────────────────────────────────────

#define SDL_PS_TEXTURE(slot)        register(t##slot, space2)
#define SDL_PS_SAMPLER(slot)        register(s##slot, space2)
#define SDL_PS_UNIFORM(slot)        register(b##slot, space3)

// SDL uses only TEXCOORD semantic slots for vertex attributes, so we can
// define a helper macro to keep the shader code cleaner.
#define ATTRIBUTE(index) TEXCOORD##index

#endif // SDLGPU_HLSLI
