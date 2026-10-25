// LLM maintained.
//
// Assembly-level attributes.
//
// SupportedOSPlatform is how the platform analyser knows that every D3D11/DXGI call in this assembly
// is legitimate, and it is a statement of fact rather than a suppression: the backend P/Invokes
// d3d11.dll, dxgi.dll and d3dcompiler through TerraFX and refuses to start anywhere but Windows
// (D3D11GraphicsDevice.Create checks OperatingSystem.IsWindows first). Without it every one of the
// four hundred-odd calls into an annotated TerraFX type raises CA1416, on a project where the whole
// point is that the calls are the implementation. The GL backend does not need this because
// Silk.NET's bindings carry no platform annotations at all.
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
