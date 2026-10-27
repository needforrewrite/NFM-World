/*
 * GL and context entry points for a C# browser build.
 *
 * Two problems this solves, both specific to emscripten without pthreads:
 *
 *   - Every gl* is JavaScript glue (emscripten_glClearColor and friends), so C# cannot P/Invoke
 *     it by name. Each wrapper below is a real wasm body that forwards, which the browser
 *     head's trampolines then point at.
 *   - There is no SDL and no EGL here, so the WebGL2 context is created through emscripten's
 *     HTML5 API instead (nfmw_context_create), and presentation is the browser's job.
 *
 * Generated from the same ordered list as the C# trampolines: keep the two in step.
 */
#include <emscripten/html5.h>
#include <emscripten/html5_webgl.h>
#include <GLES3/gl3.h>
#include <stdint.h>
#include <string.h>

static EMSCRIPTEN_WEBGL_CONTEXT_HANDLE g_context = 0;

/* Creates a WebGL2 context on the canvas and makes it current.
   Returns 0 on success, or the negative emscripten result code. */
int nfmw_context_create(const char *selector)
{
  EmscriptenWebGLContextAttributes attr;
  emscripten_webgl_init_context_attributes(&attr);
  attr.majorVersion = 2;
  attr.minorVersion = 0;
  attr.alpha = 1;
  attr.depth = 1;
  attr.stencil = 1;
  attr.antialias = 1;
  attr.premultipliedAlpha = 0;

  g_context = emscripten_webgl_create_context(selector, &attr);
  if (g_context <= 0)
    return g_context;

  EMSCRIPTEN_RESULT r = emscripten_webgl_make_context_current(g_context);
  return r == EMSCRIPTEN_RESULT_SUCCESS ? 0 : (int)r - 1000;
}

/* WebGL2 renders into the canvas and presents it when the frame ends, so the only thing a
   swap has to do is let the browser run. Nothing is copied back. */
void nfmw_context_swap(void)
{
}

int nfmw_context_resize(int width, int height)
{
  return emscripten_set_canvas_element_size("#canvas", width, height);
}

void nfmw_glActiveTexture(GLenum texture) { glActiveTexture(texture); }
void nfmw_glAttachShader(GLuint program, GLuint shader) { glAttachShader(program, shader); }
void nfmw_glBindBuffer(GLenum target, GLuint buffer) { glBindBuffer(target, buffer); }
void nfmw_glBindBufferBase(GLenum target, GLuint index, GLuint buffer) { glBindBufferBase(target, index, buffer); }
void nfmw_glBindFramebuffer(GLenum target, GLuint framebuffer) { glBindFramebuffer(target, framebuffer); }
void nfmw_glBindSampler(GLuint unit, GLuint sampler) { glBindSampler(unit, sampler); }
void nfmw_glBindVertexArray(GLuint array) { glBindVertexArray(array); }
void nfmw_glBlendFunc(GLenum sfactor, GLenum dfactor) { glBlendFunc(sfactor, dfactor); }
void nfmw_glBlendFuncSeparate(GLenum a, GLenum b, GLenum c, GLenum d) { glBlendFuncSeparate(a, b, c, d); }
void nfmw_glBufferData(GLenum target, GLsizeiptr size, const void *data, GLenum usage) { glBufferData(target, size, data, usage); }
void nfmw_glBufferSubData(GLenum target, GLintptr offset, GLsizeiptr size, const void *data) { glBufferSubData(target, offset, size, data); }
GLenum nfmw_glCheckFramebufferStatus(GLenum target) { return glCheckFramebufferStatus(target); }
void nfmw_glClear(GLbitfield mask) { glClear(mask); }
void nfmw_glClearColor(GLfloat r, GLfloat g, GLfloat b, GLfloat a) { glClearColor(r, g, b, a); }
void nfmw_glClearDepthf(GLfloat d) { glClearDepthf(d); }
void nfmw_glClearStencil(GLint s) { glClearStencil(s); }
void nfmw_glColorMask(GLboolean r, GLboolean g, GLboolean b, GLboolean a) { glColorMask(r, g, b, a); }
void nfmw_glCompileShader(GLuint shader) { glCompileShader(shader); }
GLuint nfmw_glCreateProgram(void) { return glCreateProgram(); }
GLuint nfmw_glCreateShader(GLenum type) { return glCreateShader(type); }
void nfmw_glCullFace(GLenum mode) { glCullFace(mode); }
void nfmw_glDeleteBuffers(GLsizei n, const GLuint *buffers) { glDeleteBuffers(n, buffers); }
void nfmw_glDeleteFramebuffers(GLsizei n, const GLuint *framebuffers) { glDeleteFramebuffers(n, framebuffers); }
void nfmw_glDeleteProgram(GLuint program) { glDeleteProgram(program); }
void nfmw_glDeleteSamplers(GLsizei n, const GLuint *samplers) { glDeleteSamplers(n, samplers); }
void nfmw_glDeleteShader(GLuint shader) { glDeleteShader(shader); }
void nfmw_glDeleteVertexArrays(GLsizei n, const GLuint *arrays) { glDeleteVertexArrays(n, arrays); }
void nfmw_glDepthFunc(GLenum func) { glDepthFunc(func); }
void nfmw_glDepthMask(GLboolean flag) { glDepthMask(flag); }
void nfmw_glDepthRangef(GLfloat n, GLfloat f) { glDepthRangef(n, f); }
void nfmw_glDisable(GLenum cap) { glDisable(cap); }
void nfmw_glDisableVertexAttribArray(GLuint index) { glDisableVertexAttribArray(index); }
void nfmw_glDrawArrays(GLenum mode, GLint first, GLsizei count) { glDrawArrays(mode, first, count); }
void nfmw_glDrawArraysInstanced(GLenum mode, GLint first, GLsizei count, GLsizei instances) { glDrawArraysInstanced(mode, first, count, instances); }
void nfmw_glDrawBuffers(GLsizei n, const GLenum *bufs) { glDrawBuffers(n, bufs); }
void nfmw_glDrawElements(GLenum mode, GLsizei count, GLenum type, const void *indices) { glDrawElements(mode, count, type, indices); }
void nfmw_glDrawElementsInstanced(GLenum mode, GLsizei count, GLenum type, const void *indices, GLsizei instances) { glDrawElementsInstanced(mode, count, type, indices, instances); }
void nfmw_glEnable(GLenum cap) { glEnable(cap); }
void nfmw_glEnableVertexAttribArray(GLuint index) { glEnableVertexAttribArray(index); }
void nfmw_glFinish(void) { glFinish(); }
void nfmw_glFlush(void) { glFlush(); }
void nfmw_glFramebufferTexture2D(GLenum target, GLenum attachment, GLenum textarget, GLuint texture, GLint level) { glFramebufferTexture2D(target, attachment, textarget, texture, level); }
void nfmw_glFrontFace(GLenum mode) { glFrontFace(mode); }
void nfmw_glGenBuffers(GLsizei n, GLuint *buffers) { glGenBuffers(n, buffers); }
void nfmw_glGenerateMipmap(GLenum target) { glGenerateMipmap(target); }
void nfmw_glGenFramebuffers(GLsizei n, GLuint *framebuffers) { glGenFramebuffers(n, framebuffers); }
void nfmw_glGenSamplers(GLsizei count, GLuint *samplers) { glGenSamplers(count, samplers); }
void nfmw_glGenVertexArrays(GLsizei n, GLuint *arrays) { glGenVertexArrays(n, arrays); }
void nfmw_glGenTextures(GLsizei n, GLuint *textures) { glGenTextures(n, textures); }
void nfmw_glBindTexture(GLenum target, GLuint texture) { glBindTexture(target, texture); }
void nfmw_glUniform1(GLint location, GLint v0) { glUniform1i(location, v0); }
void nfmw_glDeleteTextures(GLsizei n, const GLuint *textures) { glDeleteTextures(n, textures); }
GLenum nfmw_glGetError(void) { return glGetError(); }
void nfmw_glGetIntegerv(GLenum pname, GLint *data) { glGetIntegerv(pname, data); }
void nfmw_glGetProgramiv(GLuint program, GLenum pname, GLint *params) { glGetProgramiv(program, pname, params); }
void nfmw_glGetShaderiv(GLuint shader, GLenum pname, GLint *params) { glGetShaderiv(shader, pname, params); }
void nfmw_glGetActiveAttrib(GLuint program, GLuint index, GLsizei bufSize, GLsizei *length, GLint *size, GLenum *type, GLchar *name) { glGetActiveAttrib(program, index, bufSize, length, size, type, name); }
void nfmw_glGetActiveUniform(GLuint program, GLuint index, GLsizei bufSize, GLsizei *length, GLint *size, GLenum *type, GLchar *name) { glGetActiveUniform(program, index, bufSize, length, size, type, name); }
void nfmw_glGetProgramInfoLog(GLuint program, GLsizei bufSize, GLsizei *length, GLchar *infoLog) { glGetProgramInfoLog(program, bufSize, length, infoLog); }
void nfmw_glGetShaderInfoLog(GLuint shader, GLsizei bufSize, GLsizei *length, GLchar *infoLog) { glGetShaderInfoLog(shader, bufSize, length, infoLog); }
void nfmw_glGetActiveUniformBlockiv(GLuint program, GLuint index, GLenum pname, GLint *params) { glGetActiveUniformBlockiv(program, index, pname, params); }
void nfmw_glGetActiveUniformsiv(GLuint program, GLsizei count, const GLuint *indices, GLenum pname, GLint *params) { glGetActiveUniformsiv(program, count, indices, pname, params); }
void nfmw_glLinkProgram(GLuint program) { glLinkProgram(program); }
void nfmw_glPixelStorei(GLenum pname, GLint param) { glPixelStorei(pname, param); }
void nfmw_glPolygonOffset(GLfloat factor, GLfloat units) { glPolygonOffset(factor, units); }
void nfmw_glReadBuffer(GLenum src) { glReadBuffer(src); }
void nfmw_glReadPixels(GLint x, GLint y, GLsizei w, GLsizei h, GLenum format, GLenum type, void *pixels) { glReadPixels(x, y, w, h, format, type, pixels); }
void nfmw_glSamplerParameteri(GLuint sampler, GLenum pname, GLint param) { glSamplerParameteri(sampler, pname, param); }
void nfmw_glSamplerParameterf(GLuint sampler, GLenum pname, GLfloat param) { glSamplerParameterf(sampler, pname, param); }
void nfmw_glScissor(GLint x, GLint y, GLsizei w, GLsizei h) { glScissor(x, y, w, h); }
void nfmw_glStencilFunc(GLenum func, GLint ref, GLuint mask) { glStencilFunc(func, ref, mask); }
void nfmw_glStencilMask(GLuint mask) { glStencilMask(mask); }
void nfmw_glStencilOp(GLenum sfail, GLenum dpfail, GLenum dppass) { glStencilOp(sfail, dpfail, dppass); }
void nfmw_glTexImage2D(GLenum target, GLint level, GLint internalformat, GLsizei w, GLsizei h, GLint border, GLenum format, GLenum type, const void *pixels) { glTexImage2D(target, level, internalformat, w, h, border, format, type, pixels); }
void nfmw_glTexParameteri(GLenum target, GLenum pname, GLint param) { glTexParameteri(target, pname, param); }
void nfmw_glTexParameterf(GLenum target, GLenum pname, GLfloat param) { glTexParameterf(target, pname, param); }
void nfmw_glUniform1i(GLint location, GLint v0) { glUniform1i(location, v0); }
void nfmw_glUniform1f(GLint location, GLfloat v0) { glUniform1f(location, v0); }
void nfmw_glUniform1ui(GLint location, GLuint v0) { glUniform1ui(location, v0); }
void nfmw_glUniform1fv(GLint location, GLsizei count, const GLfloat *value) { glUniform1fv(location, count, value); }
void nfmw_glUniform1iv(GLint location, GLsizei count, const GLint *value) { glUniform1iv(location, count, value); }
void nfmw_glUniform1uiv(GLint location, GLsizei count, const GLuint *value) { glUniform1uiv(location, count, value); }
void nfmw_glUniformBlockBinding(GLuint program, GLuint index, GLuint binding) { glUniformBlockBinding(program, index, binding); }
void nfmw_glUseProgram(GLuint program) { glUseProgram(program); }
void nfmw_glVertexAttribDivisor(GLuint index, GLuint divisor) { glVertexAttribDivisor(index, divisor); }
void nfmw_glVertexAttribIPointer(GLuint index, GLint size, GLenum type, GLsizei stride, const void *pointer) { glVertexAttribIPointer(index, size, type, stride, pointer); }
void nfmw_glVertexAttribPointer(GLuint index, GLint size, GLenum type, GLboolean normalized, GLsizei stride, const void *pointer) { glVertexAttribPointer(index, size, type, normalized, stride, pointer); }
void nfmw_glViewport(GLint x, GLint y, GLsizei w, GLsizei h) { glViewport(x, y, w, h); }

/* GL's own shape. Silk.NET's string overload funnels into this one internally, so the
   trampoline has to take all four parameters - a shorter signature traps at the wasm boundary
   with "function signature mismatch". */
void nfmw_glShaderSource(GLuint shader, GLsizei count, const GLchar *const *strings, const GLint *lengths)
{
  glShaderSource(shader, count, strings, lengths);
}

/* glGetString/glGetStringi return GLubyte*, which C# can read directly. */
const char *nfmw_glGetString(GLenum name) { return (const char *)glGetString(name); }

/* WebGL2 core has no glGetStringi, and the list it would return is what
   WebGlExtensions.IsSupported already reads from JS. So this answers from the same source: a
   static list filled on first use, indexed like the real call. It has to be non-null - an
   unresolved or null return is a hard failure in the middle of Silk.NET's context setup. */
#include <emscripten.h>

EM_JS(int, nfmw_extension_count, (void), {
  const c = Module['canvas'];
  const gl = c && c.getContext && c.getContext('webgl2');
  return gl ? gl.getSupportedExtensions().length : 0;
});

EM_JS(int, nfmw_extension_at, (int index, char *out, int size), {
  const c = Module['canvas'];
  const gl = c && c.getContext && c.getContext('webgl2');
  if (!gl) return 0;
  const list = gl.getSupportedExtensions();
  if (index < 0 || index >= list.length) return 0;
  stringToUTF8(list[index], out, size);
  return 1;
});

static char g_extension_buffer[256];

const char *nfmw_glGetStringi(GLenum name, GLuint index)
{
  (void)name;
  if (!nfmw_extension_at((int)index, g_extension_buffer, sizeof(g_extension_buffer)))
    return 0;
  return g_extension_buffer;
}

GLint nfmw_glGetAttribLocation(GLuint program, const GLchar *name) { return glGetAttribLocation(program, name); }
GLint nfmw_glGetUniformLocation(GLuint program, const GLchar *name) { return glGetUniformLocation(program, name); }
GLuint nfmw_glGetUniformBlockIndex(GLuint program, const GLchar *name) { return glGetUniformBlockIndex(program, name); }

/* The state and texture-subimage calls, which the pipeline and shape renderers reach through
   the case-2 surface rather than the resource paths above. */
void nfmw_glBlendEquationSeparate(GLenum a, GLenum b) { glBlendEquationSeparate(a, b); }
void nfmw_glStencilFuncSeparate(GLenum a, GLenum b, GLint c, GLuint d) { glStencilFuncSeparate(a, b, c, d); }
void nfmw_glStencilOpSeparate(GLenum a, GLenum b, GLenum c, GLenum d) { glStencilOpSeparate(a, b, c, d); }
void nfmw_glTexSubImage2D(GLenum t, GLint l, GLint x, GLint y, GLsizei w, GLsizei h, GLenum f, GLenum ty, const void *p)
{
  glTexSubImage2D(t, l, x, y, w, h, f, ty, p);
}
