extern alias SDL3New;

using System.Runtime.InteropServices;
using SDL3New::SDL3;

namespace NFMWorld.Platform.SDL3;

/// <summary>
/// SDL3's native open-file dialog. Its filter array and result list are UTF-8 pointer
/// structures, and the callback can arrive on a worker thread, so the marshalling and the
/// hand-off back to the main thread live here instead of every caller.
/// </summary>
public static class SdlFileDialog
{
    private sealed class PendingDialog
    {
        public Action<IReadOnlyList<string>>? OnFiles;
        public IntPtr[] FilterMemory = [];
        public string[] Files = [];
        public GCHandle Self;
    }

    private static PendingDialog? _pending;
    private static SDL.SDL_DialogFileCallback? _dialogCallback;
    private static SDL.SDL_MainThreadCallback? _mainCallback;

    /// <summary>
    /// Shows the OS file picker. <paramref name="onFiles"/> runs on the main thread once the
    /// dialog closes, with the chosen paths (empty when cancelled).
    /// </summary>
    public static void OpenFile(
        IntPtr parentWindow,
        IReadOnlyList<(string Name, string Pattern)> filters,
        string? defaultLocation,
        Action<IReadOnlyList<string>> onFiles
    )
    {
        if (_pending is not null)
            return;

        var state = new PendingDialog { OnFiles = onFiles, FilterMemory = new IntPtr[filters.Count * 2] };
        var nativeFilters = new SDL.SDL_DialogFileFilter[filters.Count];

        unsafe
        {
            for (var i = 0; i < filters.Count; i++)
            {
                state.FilterMemory[i * 2] = Marshal.StringToCoTaskMemUTF8(filters[i].Name);
                state.FilterMemory[i * 2 + 1] = Marshal.StringToCoTaskMemUTF8(filters[i].Pattern);
                nativeFilters[i] = new SDL.SDL_DialogFileFilter
                {
                    name = (byte*)state.FilterMemory[i * 2],
                    pattern = (byte*)state.FilterMemory[i * 2 + 1]
                };
            }
        }

        state.Self = GCHandle.Alloc(state);
        _pending = state;
        var dialogCallback = _dialogCallback ??= OnDialogClosed;
        _mainCallback ??= OnMainThread;

        SDL.SDL_ClearError();
        unsafe
        {
            SDL.SDL_ShowOpenFileDialog(
                dialogCallback,
                GCHandle.ToIntPtr(state.Self),
                parentWindow,
                nativeFilters.AsSpan(),
                nativeFilters.Length,
                defaultLocation ?? "",
                false
            );
        }

        if (SDL.SDL_GetError() is { Length: > 0 } error)
        {
            Cleanup(state);
            throw new InvalidOperationException(error);
        }
    }

    private static void OnDialogClosed(IntPtr userdata, IntPtr fileList, int filter)
    {
        var files = new List<string>();
        if (fileList != IntPtr.Zero)
        {
            for (var entry = Marshal.ReadIntPtr(fileList); entry != IntPtr.Zero; entry = Marshal.ReadIntPtr(
                fileList += IntPtr.Size
            ))
            {
                if (Marshal.PtrToStringUTF8(entry) is { Length: > 0 } path)
                    files.Add(path);
            }
        }

        if (GCHandle.FromIntPtr(userdata).Target is not PendingDialog state)
            return;

        state.Files = [.. files];

        // The platform may report from its own thread; the UI must only be touched on the main one.
        SDL.SDL_RunOnMainThread(_mainCallback!, userdata, false);
    }

    private static void OnMainThread(IntPtr userdata)
    {
        if (GCHandle.FromIntPtr(userdata).Target is not PendingDialog state)
            return;

        if (ReferenceEquals(_pending, state))
            _pending = null;

        var onFiles = state.OnFiles;
        var files = state.Files;
        Cleanup(state);
        onFiles?.Invoke(files);
    }

    private static void Cleanup(PendingDialog state)
    {
        if (_pending is not null && ReferenceEquals(_pending, state))
            _pending = null;

        foreach (var pointer in state.FilterMemory)
            Marshal.FreeCoTaskMem(pointer);

        state.FilterMemory = [];
        state.OnFiles = null;

        if (state.Self.IsAllocated)
            state.Self.Free();
    }
}
