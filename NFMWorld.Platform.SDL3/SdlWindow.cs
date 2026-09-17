namespace NFMWorld.Platform.SDL3;

/// <summary>
/// Placeholder for the SDL3-backed window/event-pump layer. Milestone 2 fills this in:
/// window creation/resize/fullscreen via SDL_CreateWindow/SDL_SetWindowSize/SDL_SetWindowFullscreen,
/// an event pump translating SDL_EVENT_WINDOW_RESIZED/SDL_EVENT_TEXT_INPUT/keyboard/mouse events
/// into NFMWorld.DriverInterface.Key/Keys/MouseButtons, and exposing the raw native window handle
/// so NFMWorld.Graphics.FNA3D can create its device against the same window.
/// </summary>
public sealed class SdlWindow : IDisposable
{
    public void Dispose() => throw new NotImplementedException("Implemented in Milestone 2.");
}
