// LLM maintained.
//
// The GL entry points the OpenGL backend calls, bound for the browser - and the
// INativeContext that hands Silk.NET their addresses.
//
// Two wasm facts drive the shape. First, emscripten's proc addresses are wasm function table
// indices, and calli only accepts the address of a managed [UnmanagedCallersOnly] method, so
// Silk.NET cannot resolve GL by name itself: every entry point is a trampoline below. Second,
// a P/Invoke resolves through a generated table keyed by the native file's name, and on a
// non-pthread build the gl* functions are JavaScript glue rather than wasm symbols - so each
// trampoline forwards to native/webgl_shim.c, which does have real wasm bodies.
//
// Generated from the shim's prototypes. When the backend starts calling something new, add the
// wrapper to webgl_shim.c and regenerate this file and the export list in
// NFMWorld.Browser.csproj - hand-editing the three separately is how they drift apart.
#nullable enable
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace NFMWorld.Platform.WebGL;

internal sealed unsafe class GlNativeContext : INativeContext
{
    private const string Lib = "webgl_shim";

    /// <summary>Extension name by index, or zero when there is none. See WebGlExtensions.</summary>
    internal static IntPtr GetExtensionName(uint index) => (IntPtr)glGetStringi(0x1F03 /* GL_EXTENSIONS */, index);

    public static readonly GlNativeContext Instance = new();

    public IntPtr GetProcAddress(string proc, int? slot = null) => Lookup(proc);

    public bool TryGetProcAddress(string proc, out nint addr, int? slot = null)
    {
        addr = Lookup(proc);
        return addr != IntPtr.Zero;
    }

    public bool IsExtensionSupported(string extensionName) => WebGlExtensions.IsSupported(extensionName);

    public void Dispose()
    {
    }

    private static nint Lookup(string name) => name switch
    {
        "glActiveTexture" => (nint)(delegate* unmanaged<uint, void>)&W_glActiveTexture,
        "glAttachShader" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glAttachShader,
        "glBindBuffer" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glBindBuffer,
        "glBindBufferBase" => (nint)(delegate* unmanaged<uint, uint, uint, void>)&W_glBindBufferBase,
        "glBindFramebuffer" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glBindFramebuffer,
        "glBindSampler" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glBindSampler,
        "glBindVertexArray" => (nint)(delegate* unmanaged<uint, void>)&W_glBindVertexArray,
        "glBlendFunc" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glBlendFunc,
        "glBlendFuncSeparate" => (nint)(delegate* unmanaged<uint, uint, uint, uint, void>)&W_glBlendFuncSeparate,
        "glBufferData" => (nint)(delegate* unmanaged<uint, nint, void*, uint, void>)&W_glBufferData,
        "glBufferSubData" => (nint)(delegate* unmanaged<uint, nint, nint, void*, void>)&W_glBufferSubData,
        "glCheckFramebufferStatus" => (nint)(delegate* unmanaged<uint, uint>)&W_glCheckFramebufferStatus,
        "glClear" => (nint)(delegate* unmanaged<uint, void>)&W_glClear,
        "glClearColor" => (nint)(delegate* unmanaged<float, float, float, float, void>)&W_glClearColor,
        "glClearDepthf" => (nint)(delegate* unmanaged<float, void>)&W_glClearDepthf,
        "glClearStencil" => (nint)(delegate* unmanaged<int, void>)&W_glClearStencil,
        "glColorMask" => (nint)(delegate* unmanaged<byte, byte, byte, byte, void>)&W_glColorMask,
        "glCompileShader" => (nint)(delegate* unmanaged<uint, void>)&W_glCompileShader,
        "glCreateProgram" => (nint)(delegate* unmanaged<uint>)&W_glCreateProgram,
        "glCreateShader" => (nint)(delegate* unmanaged<uint, uint>)&W_glCreateShader,
        "glCullFace" => (nint)(delegate* unmanaged<uint, void>)&W_glCullFace,
        "glDeleteBuffers" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glDeleteBuffers,
        "glDeleteFramebuffers" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glDeleteFramebuffers,
        "glDeleteProgram" => (nint)(delegate* unmanaged<uint, void>)&W_glDeleteProgram,
        "glDeleteSamplers" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glDeleteSamplers,
        "glDeleteShader" => (nint)(delegate* unmanaged<uint, void>)&W_glDeleteShader,
        "glDeleteVertexArrays" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glDeleteVertexArrays,
        "glDepthFunc" => (nint)(delegate* unmanaged<uint, void>)&W_glDepthFunc,
        "glDepthMask" => (nint)(delegate* unmanaged<byte, void>)&W_glDepthMask,
        "glDepthRangef" => (nint)(delegate* unmanaged<float, float, void>)&W_glDepthRangef,
        "glDisable" => (nint)(delegate* unmanaged<uint, void>)&W_glDisable,
        "glDisableVertexAttribArray" => (nint)(delegate* unmanaged<uint, void>)&W_glDisableVertexAttribArray,
        "glDrawArrays" => (nint)(delegate* unmanaged<uint, int, int, void>)&W_glDrawArrays,
        "glDrawArraysInstanced" => (nint)(delegate* unmanaged<uint, int, int, int, void>)&W_glDrawArraysInstanced,
        "glDrawBuffers" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glDrawBuffers,
        "glDrawElements" => (nint)(delegate* unmanaged<uint, int, uint, void*, void>)&W_glDrawElements,
        "glDrawElementsInstanced" => (nint)(delegate* unmanaged<uint, int, uint, void*, int, void>)&W_glDrawElementsInstanced,
        "glEnable" => (nint)(delegate* unmanaged<uint, void>)&W_glEnable,
        "glEnableVertexAttribArray" => (nint)(delegate* unmanaged<uint, void>)&W_glEnableVertexAttribArray,
        "glFinish" => (nint)(delegate* unmanaged<void>)&W_glFinish,
        "glFlush" => (nint)(delegate* unmanaged<void>)&W_glFlush,
        "glFramebufferTexture2D" => (nint)(delegate* unmanaged<uint, uint, uint, uint, int, void>)&W_glFramebufferTexture2D,
        "glFrontFace" => (nint)(delegate* unmanaged<uint, void>)&W_glFrontFace,
        "glGenBuffers" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glGenBuffers,
        "glGenerateMipmap" => (nint)(delegate* unmanaged<uint, void>)&W_glGenerateMipmap,
        "glGenFramebuffers" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glGenFramebuffers,
        "glGenSamplers" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glGenSamplers,
        "glGenVertexArrays" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glGenVertexArrays,
        "glGenTextures" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glGenTextures,
        "glBindTexture" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glBindTexture,
        "glUniform1" => (nint)(delegate* unmanaged<int, int, void>)&W_glUniform1,
        "glDeleteTextures" => (nint)(delegate* unmanaged<int, uint*, void>)&W_glDeleteTextures,
        "glGetError" => (nint)(delegate* unmanaged<uint>)&W_glGetError,
        "glGetIntegerv" => (nint)(delegate* unmanaged<uint, int*, void>)&W_glGetIntegerv,
        "glGetProgramiv" => (nint)(delegate* unmanaged<uint, uint, int*, void>)&W_glGetProgramiv,
        "glGetShaderiv" => (nint)(delegate* unmanaged<uint, uint, int*, void>)&W_glGetShaderiv,
        "glGetActiveAttrib" => (nint)(delegate* unmanaged<uint, uint, int, int*, int*, uint*, byte*, void>)&W_glGetActiveAttrib,
        "glGetActiveUniform" => (nint)(delegate* unmanaged<uint, uint, int, int*, int*, uint*, byte*, void>)&W_glGetActiveUniform,
        "glGetProgramInfoLog" => (nint)(delegate* unmanaged<uint, int, int*, byte*, void>)&W_glGetProgramInfoLog,
        "glGetShaderInfoLog" => (nint)(delegate* unmanaged<uint, int, int*, byte*, void>)&W_glGetShaderInfoLog,
        "glGetActiveUniformBlockiv" => (nint)(delegate* unmanaged<uint, uint, uint, int*, void>)&W_glGetActiveUniformBlockiv,
        "glGetActiveUniformsiv" => (nint)(delegate* unmanaged<uint, int, uint*, uint, int*, void>)&W_glGetActiveUniformsiv,
        "glLinkProgram" => (nint)(delegate* unmanaged<uint, void>)&W_glLinkProgram,
        "glPixelStorei" => (nint)(delegate* unmanaged<uint, int, void>)&W_glPixelStorei,
        "glPolygonOffset" => (nint)(delegate* unmanaged<float, float, void>)&W_glPolygonOffset,
        "glReadBuffer" => (nint)(delegate* unmanaged<uint, void>)&W_glReadBuffer,
        "glReadPixels" => (nint)(delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)&W_glReadPixels,
        "glSamplerParameteri" => (nint)(delegate* unmanaged<uint, uint, int, void>)&W_glSamplerParameteri,
        "glSamplerParameterf" => (nint)(delegate* unmanaged<uint, uint, float, void>)&W_glSamplerParameterf,
        "glScissor" => (nint)(delegate* unmanaged<int, int, int, int, void>)&W_glScissor,
        "glStencilFunc" => (nint)(delegate* unmanaged<uint, int, uint, void>)&W_glStencilFunc,
        "glStencilMask" => (nint)(delegate* unmanaged<uint, void>)&W_glStencilMask,
        "glStencilOp" => (nint)(delegate* unmanaged<uint, uint, uint, void>)&W_glStencilOp,
        "glTexImage2D" => (nint)(delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)&W_glTexImage2D,
        "glTexParameteri" => (nint)(delegate* unmanaged<uint, uint, int, void>)&W_glTexParameteri,
        "glTexParameterf" => (nint)(delegate* unmanaged<uint, uint, float, void>)&W_glTexParameterf,
        "glUniform1i" => (nint)(delegate* unmanaged<int, int, void>)&W_glUniform1i,
        "glUniform1f" => (nint)(delegate* unmanaged<int, float, void>)&W_glUniform1f,
        "glUniform1ui" => (nint)(delegate* unmanaged<int, uint, void>)&W_glUniform1ui,
        "glUniform1fv" => (nint)(delegate* unmanaged<int, int, float*, void>)&W_glUniform1fv,
        "glUniform1iv" => (nint)(delegate* unmanaged<int, int, int*, void>)&W_glUniform1iv,
        "glUniform1uiv" => (nint)(delegate* unmanaged<int, int, uint*, void>)&W_glUniform1uiv,
        "glUniformBlockBinding" => (nint)(delegate* unmanaged<uint, uint, uint, void>)&W_glUniformBlockBinding,
        "glUseProgram" => (nint)(delegate* unmanaged<uint, void>)&W_glUseProgram,
        "glVertexAttribDivisor" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glVertexAttribDivisor,
        "glVertexAttribIPointer" => (nint)(delegate* unmanaged<uint, int, uint, int, void*, void>)&W_glVertexAttribIPointer,
        "glVertexAttribPointer" => (nint)(delegate* unmanaged<uint, int, uint, byte, int, void*, void>)&W_glVertexAttribPointer,
        "glViewport" => (nint)(delegate* unmanaged<int, int, int, int, void>)&W_glViewport,
        "glShaderSource" => (nint)(delegate* unmanaged<uint, int, byte**, int*, void>)&W_glShaderSource,
        "glGetAttribLocation" => (nint)(delegate* unmanaged<uint, byte*, int>)&W_glGetAttribLocation,
        "glGetUniformLocation" => (nint)(delegate* unmanaged<uint, byte*, int>)&W_glGetUniformLocation,
        "glGetUniformBlockIndex" => (nint)(delegate* unmanaged<uint, byte*, uint>)&W_glGetUniformBlockIndex,
        "glBlendEquationSeparate" => (nint)(delegate* unmanaged<uint, uint, void>)&W_glBlendEquationSeparate,
        "glStencilFuncSeparate" => (nint)(delegate* unmanaged<uint, uint, int, uint, void>)&W_glStencilFuncSeparate,
        "glStencilOpSeparate" => (nint)(delegate* unmanaged<uint, uint, uint, uint, void>)&W_glStencilOpSeparate,
        "glTexSubImage2D" => (nint)(delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)&W_glTexSubImage2D,

        "glGetString" => (nint)(delegate* unmanaged<uint, byte*>)&W_glGetString,
        "glGetStringi" => (nint)(delegate* unmanaged<uint, uint, byte*>)&W_glGetStringi,
        _ => IntPtr.Zero,
    };

    // One import into the shim and one trampoline per entry point. The trampoline must match the
    // native signature exactly - that signature is what calli passes.

    [DllImport(Lib, EntryPoint = "nfmw_glActiveTexture")]
    internal static extern void glActiveTexture(uint texture);

    [DllImport(Lib, EntryPoint = "nfmw_glAttachShader")]
    internal static extern void glAttachShader(uint program, uint shader);

    [DllImport(Lib, EntryPoint = "nfmw_glBindBuffer")]
    internal static extern void glBindBuffer(uint target, uint buffer);

    [DllImport(Lib, EntryPoint = "nfmw_glBindBufferBase")]
    internal static extern void glBindBufferBase(uint target, uint index, uint buffer);

    [DllImport(Lib, EntryPoint = "nfmw_glBindFramebuffer")]
    internal static extern void glBindFramebuffer(uint target, uint framebuffer);

    [DllImport(Lib, EntryPoint = "nfmw_glBindSampler")]
    internal static extern void glBindSampler(uint unit, uint sampler);

    [DllImport(Lib, EntryPoint = "nfmw_glBindVertexArray")]
    internal static extern void glBindVertexArray(uint array);

    [DllImport(Lib, EntryPoint = "nfmw_glBlendFunc")]
    internal static extern void glBlendFunc(uint sfactor, uint dfactor);

    [DllImport(Lib, EntryPoint = "nfmw_glBlendFuncSeparate")]
    internal static extern void glBlendFuncSeparate(uint a, uint b, uint c, uint d);

    [DllImport(Lib, EntryPoint = "nfmw_glBufferData")]
    internal static extern void glBufferData(uint target, nint size, void* data, uint usage);

    [DllImport(Lib, EntryPoint = "nfmw_glBufferSubData")]
    internal static extern void glBufferSubData(uint target, nint offset, nint size, void* data);

    [DllImport(Lib, EntryPoint = "nfmw_glCheckFramebufferStatus")]
    internal static extern uint glCheckFramebufferStatus(uint target);

    [DllImport(Lib, EntryPoint = "nfmw_glClear")]
    internal static extern void glClear(uint mask);

    [DllImport(Lib, EntryPoint = "nfmw_glClearColor")]
    internal static extern void glClearColor(float r, float g, float b, float a);

    [DllImport(Lib, EntryPoint = "nfmw_glClearDepthf")]
    internal static extern void glClearDepthf(float d);

    [DllImport(Lib, EntryPoint = "nfmw_glClearStencil")]
    internal static extern void glClearStencil(int s);

    [DllImport(Lib, EntryPoint = "nfmw_glColorMask")]
    internal static extern void glColorMask(byte r, byte g, byte b, byte a);

    [DllImport(Lib, EntryPoint = "nfmw_glCompileShader")]
    internal static extern void glCompileShader(uint shader);

    [DllImport(Lib, EntryPoint = "nfmw_glCreateProgram")]
    internal static extern uint glCreateProgram();

    [DllImport(Lib, EntryPoint = "nfmw_glCreateShader")]
    internal static extern uint glCreateShader(uint type);

    [DllImport(Lib, EntryPoint = "nfmw_glCullFace")]
    internal static extern void glCullFace(uint mode);

    [DllImport(Lib, EntryPoint = "nfmw_glDeleteBuffers")]
    internal static extern void glDeleteBuffers(int n, uint* buffers);

    [DllImport(Lib, EntryPoint = "nfmw_glDeleteFramebuffers")]
    internal static extern void glDeleteFramebuffers(int n, uint* framebuffers);

    [DllImport(Lib, EntryPoint = "nfmw_glDeleteProgram")]
    internal static extern void glDeleteProgram(uint program);

    [DllImport(Lib, EntryPoint = "nfmw_glDeleteSamplers")]
    internal static extern void glDeleteSamplers(int n, uint* samplers);

    [DllImport(Lib, EntryPoint = "nfmw_glDeleteShader")]
    internal static extern void glDeleteShader(uint shader);

    [DllImport(Lib, EntryPoint = "nfmw_glDeleteVertexArrays")]
    internal static extern void glDeleteVertexArrays(int n, uint* arrays);

    [DllImport(Lib, EntryPoint = "nfmw_glDepthFunc")]
    internal static extern void glDepthFunc(uint func);

    [DllImport(Lib, EntryPoint = "nfmw_glDepthMask")]
    internal static extern void glDepthMask(byte flag);

    [DllImport(Lib, EntryPoint = "nfmw_glDepthRangef")]
    internal static extern void glDepthRangef(float n, float f);

    [DllImport(Lib, EntryPoint = "nfmw_glDisable")]
    internal static extern void glDisable(uint cap);

    [DllImport(Lib, EntryPoint = "nfmw_glDisableVertexAttribArray")]
    internal static extern void glDisableVertexAttribArray(uint index);

    [DllImport(Lib, EntryPoint = "nfmw_glDrawArrays")]
    internal static extern void glDrawArrays(uint mode, int first, int count);

    [DllImport(Lib, EntryPoint = "nfmw_glDrawArraysInstanced")]
    internal static extern void glDrawArraysInstanced(uint mode, int first, int count, int instances);

    [DllImport(Lib, EntryPoint = "nfmw_glDrawBuffers")]
    internal static extern void glDrawBuffers(int n, uint* bufs);

    [DllImport(Lib, EntryPoint = "nfmw_glDrawElements")]
    internal static extern void glDrawElements(uint mode, int count, uint type, void* indices);

    [DllImport(Lib, EntryPoint = "nfmw_glDrawElementsInstanced")]
    internal static extern void glDrawElementsInstanced(uint mode, int count, uint type, void* indices, int instances);

    [DllImport(Lib, EntryPoint = "nfmw_glEnable")]
    internal static extern void glEnable(uint cap);

    [DllImport(Lib, EntryPoint = "nfmw_glEnableVertexAttribArray")]
    internal static extern void glEnableVertexAttribArray(uint index);

    [DllImport(Lib, EntryPoint = "nfmw_glFinish")]
    internal static extern void glFinish();

    [DllImport(Lib, EntryPoint = "nfmw_glFlush")]
    internal static extern void glFlush();

    [DllImport(Lib, EntryPoint = "nfmw_glFramebufferTexture2D")]
    internal static extern void glFramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level);

    [DllImport(Lib, EntryPoint = "nfmw_glFrontFace")]
    internal static extern void glFrontFace(uint mode);

    [DllImport(Lib, EntryPoint = "nfmw_glGenBuffers")]
    internal static extern void glGenBuffers(int n, uint* buffers);

    [DllImport(Lib, EntryPoint = "nfmw_glGenerateMipmap")]
    internal static extern void glGenerateMipmap(uint target);

    [DllImport(Lib, EntryPoint = "nfmw_glGenFramebuffers")]
    internal static extern void glGenFramebuffers(int n, uint* framebuffers);

    [DllImport(Lib, EntryPoint = "nfmw_glGenSamplers")]
    internal static extern void glGenSamplers(int count, uint* samplers);

    [DllImport(Lib, EntryPoint = "nfmw_glGenVertexArrays")]
    internal static extern void glGenVertexArrays(int n, uint* arrays);

    [DllImport(Lib, EntryPoint = "nfmw_glGenTextures")]
    internal static extern void glGenTextures(int n, uint* textures);

    [DllImport(Lib, EntryPoint = "nfmw_glBindTexture")]
    internal static extern void glBindTexture(uint target, uint texture);

    [DllImport(Lib, EntryPoint = "nfmw_glUniform1")]
    internal static extern void glUniform1(int location, int v0);

    [DllImport(Lib, EntryPoint = "nfmw_glDeleteTextures")]
    internal static extern void glDeleteTextures(int n, uint* textures);

    [DllImport(Lib, EntryPoint = "nfmw_glGetError")]
    internal static extern uint glGetError();

    [DllImport(Lib, EntryPoint = "nfmw_glGetIntegerv")]
    internal static extern void glGetIntegerv(uint pname, int* data);

    [DllImport(Lib, EntryPoint = "nfmw_glGetProgramiv")]
    internal static extern void glGetProgramiv(uint program, uint pname, int* values);

    [DllImport(Lib, EntryPoint = "nfmw_glGetShaderiv")]
    internal static extern void glGetShaderiv(uint shader, uint pname, int* values);

    [DllImport(Lib, EntryPoint = "nfmw_glGetActiveAttrib")]
    internal static extern void glGetActiveAttrib(uint program, uint index, int bufSize, int* length, int* size, uint* type, byte* name);

    [DllImport(Lib, EntryPoint = "nfmw_glGetActiveUniform")]
    internal static extern void glGetActiveUniform(uint program, uint index, int bufSize, int* length, int* size, uint* type, byte* name);

    [DllImport(Lib, EntryPoint = "nfmw_glGetProgramInfoLog")]
    internal static extern void glGetProgramInfoLog(uint program, int bufSize, int* length, byte* infoLog);

    [DllImport(Lib, EntryPoint = "nfmw_glGetShaderInfoLog")]
    internal static extern void glGetShaderInfoLog(uint shader, int bufSize, int* length, byte* infoLog);

    [DllImport(Lib, EntryPoint = "nfmw_glGetActiveUniformBlockiv")]
    internal static extern void glGetActiveUniformBlockiv(uint program, uint index, uint pname, int* values);

    [DllImport(Lib, EntryPoint = "nfmw_glGetActiveUniformsiv")]
    internal static extern void glGetActiveUniformsiv(uint program, int count, uint* indices, uint pname, int* values);

    [DllImport(Lib, EntryPoint = "nfmw_glLinkProgram")]
    internal static extern void glLinkProgram(uint program);

    [DllImport(Lib, EntryPoint = "nfmw_glPixelStorei")]
    internal static extern void glPixelStorei(uint pname, int param);

    [DllImport(Lib, EntryPoint = "nfmw_glPolygonOffset")]
    internal static extern void glPolygonOffset(float factor, float units);

    [DllImport(Lib, EntryPoint = "nfmw_glReadBuffer")]
    internal static extern void glReadBuffer(uint src);

    [DllImport(Lib, EntryPoint = "nfmw_glReadPixels")]
    internal static extern void glReadPixels(int x, int y, int w, int h, uint format, uint type, void* pixels);

    [DllImport(Lib, EntryPoint = "nfmw_glSamplerParameteri")]
    internal static extern void glSamplerParameteri(uint sampler, uint pname, int param);

    [DllImport(Lib, EntryPoint = "nfmw_glSamplerParameterf")]
    internal static extern void glSamplerParameterf(uint sampler, uint pname, float param);

    [DllImport(Lib, EntryPoint = "nfmw_glScissor")]
    internal static extern void glScissor(int x, int y, int w, int h);

    [DllImport(Lib, EntryPoint = "nfmw_glStencilFunc")]
    internal static extern void glStencilFunc(uint func, int ref_, uint mask);

    [DllImport(Lib, EntryPoint = "nfmw_glStencilMask")]
    internal static extern void glStencilMask(uint mask);

    [DllImport(Lib, EntryPoint = "nfmw_glStencilOp")]
    internal static extern void glStencilOp(uint sfail, uint dpfail, uint dppass);

    [DllImport(Lib, EntryPoint = "nfmw_glTexImage2D")]
    internal static extern void glTexImage2D(uint target, int level, int internalformat, int w, int h, int border, uint format, uint type, void* pixels);

    [DllImport(Lib, EntryPoint = "nfmw_glTexParameteri")]
    internal static extern void glTexParameteri(uint target, uint pname, int param);

    [DllImport(Lib, EntryPoint = "nfmw_glTexParameterf")]
    internal static extern void glTexParameterf(uint target, uint pname, float param);

    [DllImport(Lib, EntryPoint = "nfmw_glUniform1i")]
    internal static extern void glUniform1i(int location, int v0);

    [DllImport(Lib, EntryPoint = "nfmw_glUniform1f")]
    internal static extern void glUniform1f(int location, float v0);

    [DllImport(Lib, EntryPoint = "nfmw_glUniform1ui")]
    internal static extern void glUniform1ui(int location, uint v0);

    [DllImport(Lib, EntryPoint = "nfmw_glUniform1fv")]
    internal static extern void glUniform1fv(int location, int count, float* value);

    [DllImport(Lib, EntryPoint = "nfmw_glUniform1iv")]
    internal static extern void glUniform1iv(int location, int count, int* value);

    [DllImport(Lib, EntryPoint = "nfmw_glUniform1uiv")]
    internal static extern void glUniform1uiv(int location, int count, uint* value);

    [DllImport(Lib, EntryPoint = "nfmw_glUniformBlockBinding")]
    internal static extern void glUniformBlockBinding(uint program, uint index, uint binding);

    [DllImport(Lib, EntryPoint = "nfmw_glUseProgram")]
    internal static extern void glUseProgram(uint program);

    [DllImport(Lib, EntryPoint = "nfmw_glVertexAttribDivisor")]
    internal static extern void glVertexAttribDivisor(uint index, uint divisor);

    [DllImport(Lib, EntryPoint = "nfmw_glVertexAttribIPointer")]
    internal static extern void glVertexAttribIPointer(uint index, int size, uint type, int stride, void* pointer);

    [DllImport(Lib, EntryPoint = "nfmw_glVertexAttribPointer")]
    internal static extern void glVertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, void* pointer);

    [DllImport(Lib, EntryPoint = "nfmw_glViewport")]
    internal static extern void glViewport(int x, int y, int w, int h);

    [DllImport(Lib, EntryPoint = "nfmw_glShaderSource")]
    internal static extern void glShaderSource(uint shader, int count, byte** strings, int* lengths);

    [DllImport(Lib, EntryPoint = "nfmw_glGetAttribLocation")]
    internal static extern int glGetAttribLocation(uint program, byte* name);

    [DllImport(Lib, EntryPoint = "nfmw_glGetUniformLocation")]
    internal static extern int glGetUniformLocation(uint program, byte* name);

    [DllImport(Lib, EntryPoint = "nfmw_glGetUniformBlockIndex")]
    internal static extern uint glGetUniformBlockIndex(uint program, byte* name);

    [DllImport(Lib, EntryPoint = "nfmw_glBlendEquationSeparate")]
    internal static extern void glBlendEquationSeparate(uint a, uint b);

    [DllImport(Lib, EntryPoint = "nfmw_glStencilFuncSeparate")]
    internal static extern void glStencilFuncSeparate(uint a, uint b, int c, uint d);

    [DllImport(Lib, EntryPoint = "nfmw_glStencilOpSeparate")]
    internal static extern void glStencilOpSeparate(uint a, uint b, uint c, uint d);

    [DllImport(Lib, EntryPoint = "nfmw_glTexSubImage2D")]
    internal static extern void glTexSubImage2D(uint t, int l, int x, int y, int w, int h, uint f, uint ty, void* p);

    [UnmanagedCallersOnly]
    private static void W_glActiveTexture(uint texture) => glActiveTexture(texture);

    [UnmanagedCallersOnly]
    private static void W_glAttachShader(uint program, uint shader) => glAttachShader(program, shader);

    [UnmanagedCallersOnly]
    private static void W_glBindBuffer(uint target, uint buffer) => glBindBuffer(target, buffer);

    [UnmanagedCallersOnly]
    private static void W_glBindBufferBase(uint target, uint index, uint buffer) => glBindBufferBase(target, index, buffer);

    [UnmanagedCallersOnly]
    private static void W_glBindFramebuffer(uint target, uint framebuffer) => glBindFramebuffer(target, framebuffer);

    [UnmanagedCallersOnly]
    private static void W_glBindSampler(uint unit, uint sampler) => glBindSampler(unit, sampler);

    [UnmanagedCallersOnly]
    private static void W_glBindVertexArray(uint array) => glBindVertexArray(array);

    [UnmanagedCallersOnly]
    private static void W_glBlendFunc(uint sfactor, uint dfactor) => glBlendFunc(sfactor, dfactor);

    [UnmanagedCallersOnly]
    private static void W_glBlendFuncSeparate(uint a, uint b, uint c, uint d) => glBlendFuncSeparate(a, b, c, d);

    [UnmanagedCallersOnly]
    private static void W_glBufferData(uint target, nint size, void* data, uint usage) => glBufferData(target, size, data, usage);

    [UnmanagedCallersOnly]
    private static void W_glBufferSubData(uint target, nint offset, nint size, void* data) => glBufferSubData(target, offset, size, data);

    [UnmanagedCallersOnly]
    private static uint W_glCheckFramebufferStatus(uint target) => glCheckFramebufferStatus(target);

    [UnmanagedCallersOnly]
    private static void W_glClear(uint mask) => glClear(mask);

    [UnmanagedCallersOnly]
    private static void W_glClearColor(float r, float g, float b, float a) => glClearColor(r, g, b, a);

    [UnmanagedCallersOnly]
    private static void W_glClearDepthf(float d) => glClearDepthf(d);

    [UnmanagedCallersOnly]
    private static void W_glClearStencil(int s) => glClearStencil(s);

    [UnmanagedCallersOnly]
    private static void W_glColorMask(byte r, byte g, byte b, byte a) => glColorMask(r, g, b, a);

    [UnmanagedCallersOnly]
    private static void W_glCompileShader(uint shader) => glCompileShader(shader);

    [UnmanagedCallersOnly]
    private static uint W_glCreateProgram() => glCreateProgram();

    [UnmanagedCallersOnly]
    private static uint W_glCreateShader(uint type) => glCreateShader(type);

    [UnmanagedCallersOnly]
    private static void W_glCullFace(uint mode) => glCullFace(mode);

    [UnmanagedCallersOnly]
    private static void W_glDeleteBuffers(int n, uint* buffers) => glDeleteBuffers(n, buffers);

    [UnmanagedCallersOnly]
    private static void W_glDeleteFramebuffers(int n, uint* framebuffers) => glDeleteFramebuffers(n, framebuffers);

    [UnmanagedCallersOnly]
    private static void W_glDeleteProgram(uint program) => glDeleteProgram(program);

    [UnmanagedCallersOnly]
    private static void W_glDeleteSamplers(int n, uint* samplers) => glDeleteSamplers(n, samplers);

    [UnmanagedCallersOnly]
    private static void W_glDeleteShader(uint shader) => glDeleteShader(shader);

    [UnmanagedCallersOnly]
    private static void W_glDeleteVertexArrays(int n, uint* arrays) => glDeleteVertexArrays(n, arrays);

    [UnmanagedCallersOnly]
    private static void W_glDepthFunc(uint func) => glDepthFunc(func);

    [UnmanagedCallersOnly]
    private static void W_glDepthMask(byte flag) => glDepthMask(flag);

    [UnmanagedCallersOnly]
    private static void W_glDepthRangef(float n, float f) => glDepthRangef(n, f);

    [UnmanagedCallersOnly]
    private static void W_glDisable(uint cap) => glDisable(cap);

    [UnmanagedCallersOnly]
    private static void W_glDisableVertexAttribArray(uint index) => glDisableVertexAttribArray(index);

    [UnmanagedCallersOnly]
    private static void W_glDrawArrays(uint mode, int first, int count) => glDrawArrays(mode, first, count);

    [UnmanagedCallersOnly]
    private static void W_glDrawArraysInstanced(uint mode, int first, int count, int instances) => glDrawArraysInstanced(mode, first, count, instances);

    [UnmanagedCallersOnly]
    private static void W_glDrawBuffers(int n, uint* bufs) => glDrawBuffers(n, bufs);

    [UnmanagedCallersOnly]
    private static void W_glDrawElements(uint mode, int count, uint type, void* indices) => glDrawElements(mode, count, type, indices);

    [UnmanagedCallersOnly]
    private static void W_glDrawElementsInstanced(uint mode, int count, uint type, void* indices, int instances) => glDrawElementsInstanced(mode, count, type, indices, instances);

    [UnmanagedCallersOnly]
    private static void W_glEnable(uint cap) => glEnable(cap);

    [UnmanagedCallersOnly]
    private static void W_glEnableVertexAttribArray(uint index) => glEnableVertexAttribArray(index);

    [UnmanagedCallersOnly]
    private static void W_glFinish() => glFinish();

    [UnmanagedCallersOnly]
    private static void W_glFlush() => glFlush();

    [UnmanagedCallersOnly]
    private static void W_glFramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level) => glFramebufferTexture2D(target, attachment, textarget, texture, level);

    [UnmanagedCallersOnly]
    private static void W_glFrontFace(uint mode) => glFrontFace(mode);

    [UnmanagedCallersOnly]
    private static void W_glGenBuffers(int n, uint* buffers) => glGenBuffers(n, buffers);

    [UnmanagedCallersOnly]
    private static void W_glGenerateMipmap(uint target) => glGenerateMipmap(target);

    [UnmanagedCallersOnly]
    private static void W_glGenFramebuffers(int n, uint* framebuffers) => glGenFramebuffers(n, framebuffers);

    [UnmanagedCallersOnly]
    private static void W_glGenSamplers(int count, uint* samplers) => glGenSamplers(count, samplers);

    [UnmanagedCallersOnly]
    private static void W_glGenVertexArrays(int n, uint* arrays) => glGenVertexArrays(n, arrays);

    [UnmanagedCallersOnly]
    private static void W_glGenTextures(int n, uint* textures) => glGenTextures(n, textures);

    [UnmanagedCallersOnly]
    private static void W_glBindTexture(uint target, uint texture) => glBindTexture(target, texture);

    [UnmanagedCallersOnly]
    private static void W_glUniform1(int location, int v0) => glUniform1(location, v0);

    [UnmanagedCallersOnly]
    private static void W_glDeleteTextures(int n, uint* textures) => glDeleteTextures(n, textures);

    [UnmanagedCallersOnly]
    private static uint W_glGetError() => glGetError();

    [UnmanagedCallersOnly]
    private static void W_glGetIntegerv(uint pname, int* data) => glGetIntegerv(pname, data);

    [UnmanagedCallersOnly]
    private static void W_glGetProgramiv(uint program, uint pname, int* values) => glGetProgramiv(program, pname, values);

    [UnmanagedCallersOnly]
    private static void W_glGetShaderiv(uint shader, uint pname, int* values) => glGetShaderiv(shader, pname, values);

    [UnmanagedCallersOnly]
    private static void W_glGetActiveAttrib(uint program, uint index, int bufSize, int* length, int* size, uint* type, byte* name) => glGetActiveAttrib(program, index, bufSize, length, size, type, name);

    [UnmanagedCallersOnly]
    private static void W_glGetActiveUniform(uint program, uint index, int bufSize, int* length, int* size, uint* type, byte* name) => glGetActiveUniform(program, index, bufSize, length, size, type, name);

    [UnmanagedCallersOnly]
    private static void W_glGetProgramInfoLog(uint program, int bufSize, int* length, byte* infoLog) => glGetProgramInfoLog(program, bufSize, length, infoLog);

    [UnmanagedCallersOnly]
    private static void W_glGetShaderInfoLog(uint shader, int bufSize, int* length, byte* infoLog) => glGetShaderInfoLog(shader, bufSize, length, infoLog);

    [UnmanagedCallersOnly]
    private static void W_glGetActiveUniformBlockiv(uint program, uint index, uint pname, int* values) => glGetActiveUniformBlockiv(program, index, pname, values);

    [UnmanagedCallersOnly]
    private static void W_glGetActiveUniformsiv(uint program, int count, uint* indices, uint pname, int* values) => glGetActiveUniformsiv(program, count, indices, pname, values);

    [UnmanagedCallersOnly]
    private static void W_glLinkProgram(uint program) => glLinkProgram(program);

    [UnmanagedCallersOnly]
    private static void W_glPixelStorei(uint pname, int param) => glPixelStorei(pname, param);

    [UnmanagedCallersOnly]
    private static void W_glPolygonOffset(float factor, float units) => glPolygonOffset(factor, units);

    [UnmanagedCallersOnly]
    private static void W_glReadBuffer(uint src) => glReadBuffer(src);

    [UnmanagedCallersOnly]
    private static void W_glReadPixels(int x, int y, int w, int h, uint format, uint type, void* pixels) => glReadPixels(x, y, w, h, format, type, pixels);

    [UnmanagedCallersOnly]
    private static void W_glSamplerParameteri(uint sampler, uint pname, int param) => glSamplerParameteri(sampler, pname, param);

    [UnmanagedCallersOnly]
    private static void W_glSamplerParameterf(uint sampler, uint pname, float param) => glSamplerParameterf(sampler, pname, param);

    [UnmanagedCallersOnly]
    private static void W_glScissor(int x, int y, int w, int h) => glScissor(x, y, w, h);

    [UnmanagedCallersOnly]
    private static void W_glStencilFunc(uint func, int ref_, uint mask) => glStencilFunc(func, ref_, mask);

    [UnmanagedCallersOnly]
    private static void W_glStencilMask(uint mask) => glStencilMask(mask);

    [UnmanagedCallersOnly]
    private static void W_glStencilOp(uint sfail, uint dpfail, uint dppass) => glStencilOp(sfail, dpfail, dppass);

    [UnmanagedCallersOnly]
    private static void W_glTexImage2D(uint target, int level, int internalformat, int w, int h, int border, uint format, uint type, void* pixels) => glTexImage2D(target, level, internalformat, w, h, border, format, type, pixels);

    [UnmanagedCallersOnly]
    private static void W_glTexParameteri(uint target, uint pname, int param) => glTexParameteri(target, pname, param);

    [UnmanagedCallersOnly]
    private static void W_glTexParameterf(uint target, uint pname, float param) => glTexParameterf(target, pname, param);

    [UnmanagedCallersOnly]
    private static void W_glUniform1i(int location, int v0) => glUniform1i(location, v0);

    [UnmanagedCallersOnly]
    private static void W_glUniform1f(int location, float v0) => glUniform1f(location, v0);

    [UnmanagedCallersOnly]
    private static void W_glUniform1ui(int location, uint v0) => glUniform1ui(location, v0);

    [UnmanagedCallersOnly]
    private static void W_glUniform1fv(int location, int count, float* value) => glUniform1fv(location, count, value);

    [UnmanagedCallersOnly]
    private static void W_glUniform1iv(int location, int count, int* value) => glUniform1iv(location, count, value);

    [UnmanagedCallersOnly]
    private static void W_glUniform1uiv(int location, int count, uint* value) => glUniform1uiv(location, count, value);

    [UnmanagedCallersOnly]
    private static void W_glUniformBlockBinding(uint program, uint index, uint binding) => glUniformBlockBinding(program, index, binding);

    [UnmanagedCallersOnly]
    private static void W_glUseProgram(uint program) => glUseProgram(program);

    [UnmanagedCallersOnly]
    private static void W_glVertexAttribDivisor(uint index, uint divisor) => glVertexAttribDivisor(index, divisor);

    [UnmanagedCallersOnly]
    private static void W_glVertexAttribIPointer(uint index, int size, uint type, int stride, void* pointer) => glVertexAttribIPointer(index, size, type, stride, pointer);

    [UnmanagedCallersOnly]
    private static void W_glVertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, void* pointer) => glVertexAttribPointer(index, size, type, normalized, stride, pointer);

    [UnmanagedCallersOnly]
    private static void W_glViewport(int x, int y, int w, int h) => glViewport(x, y, w, h);

    [UnmanagedCallersOnly]
    private static void W_glShaderSource(uint shader, int count, byte** strings, int* lengths) => glShaderSource(shader, count, strings, lengths);

    [UnmanagedCallersOnly]
    private static int W_glGetAttribLocation(uint program, byte* name) => glGetAttribLocation(program, name);

    [UnmanagedCallersOnly]
    private static int W_glGetUniformLocation(uint program, byte* name) => glGetUniformLocation(program, name);

    [UnmanagedCallersOnly]
    private static uint W_glGetUniformBlockIndex(uint program, byte* name) => glGetUniformBlockIndex(program, name);

    [UnmanagedCallersOnly]
    private static void W_glBlendEquationSeparate(uint a, uint b) => glBlendEquationSeparate(a, b);

    [UnmanagedCallersOnly]
    private static void W_glStencilFuncSeparate(uint a, uint b, int c, uint d) => glStencilFuncSeparate(a, b, c, d);

    [UnmanagedCallersOnly]
    private static void W_glStencilOpSeparate(uint a, uint b, uint c, uint d) => glStencilOpSeparate(a, b, c, d);

    [UnmanagedCallersOnly]
    private static void W_glTexSubImage2D(uint t, int l, int x, int y, int w, int h, uint f, uint ty, void* p) => glTexSubImage2D(t, l, x, y, w, h, f, ty, p);

    [DllImport(Lib, EntryPoint = "nfmw_glGetString")]
    private static extern byte* glGetString(uint name);

    [UnmanagedCallersOnly]
    private static byte* W_glGetString(uint name) => glGetString(name);

    [DllImport(Lib, EntryPoint = "nfmw_glGetStringi")]
    internal static extern byte* glGetStringi(uint name, uint index);

    [UnmanagedCallersOnly]
    private static byte* W_glGetStringi(uint name, uint index) => glGetStringi(name, index);
}
