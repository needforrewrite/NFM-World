extern alias SDL3New;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using NFMWorld.Graphics;
using NFMWorld.UI;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using ClearOptions = NFMWorld.Graphics.ClearOptions;
using SDL = SDL3New::SDL3.SDL;

namespace NFMWorld;

public partial class WorldGame
{
    private const string GraphicsRecoveryArgument = "--graphics-recovery";
    private const string StartupStatusPath = "data/cfg/startup-status.txt";
    private const string RecoveryUiPrefix = "[Recovery UI]\n";
    private static bool _graphicsRecoveryMode;

    private static string ReadStartupStatus()
    {
        try
        {
            return TheVFS.VFS.FileExists(StartupStatusPath) ? TheVFS.VFS.ReadAllText(StartupStatusPath) : "";
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not read startup status: {ex.Message}");
            return "";
        }
    }

    private static void WriteStartupStatus(string status)
    {
        try
        {
            using var writer = new StreamWriter(TheVFS.VFS.OpenWrite(StartupStatusPath));
            writer.Write(status);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not save startup status: {ex.Message}");
        }
    }

    /// <summary>Set on the child, so the relaunch happens once.</summary>
    private const string CrashDumpChildVariable = "NFMW_CRASH_DUMP_CHILD";

    /// <summary>
    /// Where <c>createdump</c> writes. The same folder <c>CrashDump</c> in the crash reporter
    /// uses; the exe cannot reference that project, so the path is repeated rather than shared.
    /// </summary>
    private static string CrashDumpPath => Path.GetFullPath(
        Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "NFMWorld",
            "crashes",
            "crash-%p.dmp"
        )
    );

    /// <summary>
    /// Relaunches the game with the runtime's dump settings in the environment. They are only
    /// read as the CLR starts, so setting them from managed code is too late - a relaunch is the
    /// only way to arm them.
    /// </summary>
    /// <remarks>
    /// The top-level exception filter in <c>CrashDump</c> catches access violations, but an
    /// internal CLR error is a fail-fast: it terminates from inside the runtime and never reaches
    /// that filter. <c>createdump</c> does catch it, and a stack overflow too, once the runtime is
    /// started with these variables set.
    ///
    /// Returned rather than always taken: a debugger attach does not survive a relaunch, and
    /// under <c>dotnet</c> the process path is the host, so there is no game executable to
    /// relaunch. Both fall through to the filter alone.
    /// </remarks>
    private static int RelaunchForCrashDumps(string[] args)
    {
        // Release only. A Debug build is what a developer runs from an IDE, and a relaunch
        // there would detach the child from the debugger and leave it orphaned when the IDE
        // stops the parent. The crash hook is gated the same way.
#if DEBUG
        return -1;
#else
        if (!OperatingSystem.IsWindows()
            || Debugger.IsAttached
            || System.Environment.GetEnvironmentVariable(CrashDumpChildVariable) == "1")
            return -1;

        var executable = System.Environment.ProcessPath;
        if (executable is null
            || string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            return -1;

        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = System.Environment.CurrentDirectory
        };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        start.Environment[CrashDumpChildVariable] = "1";
        start.Environment["DOTNET_DbgEnableMiniDump"] = "1";
        // 4 = full dump. The runtime's default is a mini dump, which has no heap and so no
        // managed stack or object graph to read.
        start.Environment["DOTNET_DbgMiniDumpType"] = "4";
        start.Environment["DOTNET_DbgMiniDumpName"] = CrashDumpPath;

        // createdump writes into an existing folder or not at all - it does not create one,
        // and it fails silently when it cannot open the path.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashDumpPath)!);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not create the crash dump folder: {ex.Message}");
        }

        try
        {
            using var child = Process.Start(start);
            if (child is null) return -1;
            child.WaitForExit();
            return child.ExitCode;
        }
        catch (Exception ex)
        {
            // No dump, but the game itself still has to start.
            Logging.Warning($"Could not relaunch for crash dumps: {ex.Message}");
            return -1;
        }
#endif
    }

    private static string[] RecoveryRetryArguments(string[] args) => args.Where(arg =>
        arg != GraphicsRecoveryArgument
        && !arg.StartsWith(RendererArgumentPrefix, StringComparison.OrdinalIgnoreCase)
        && !arg.StartsWith("--angle-backend=", StringComparison.OrdinalIgnoreCase)
        && !arg.StartsWith("--angle-device=", StringComparison.OrdinalIgnoreCase)
    ).ToArray();

    private static void Relaunch(IEnumerable<string> args)
    {
        var executable = System.Environment.ProcessPath
                         ?? throw new InvalidOperationException("Cannot determine the game executable for restart.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = System.Environment.CurrentDirectory
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var restarted = Process.Start(start)
                              ?? throw new InvalidOperationException("Failed to restart the game.");
    }

    private static void RecoverFailedStartup(WorldGame? program, string[] args, Exception error)
    {
        Logging.Error($"Game startup failed: {error}");
        WriteStartupStatus(error.ToString());
        try
        {
            program?.Dispose();
        }
        catch (Exception ex)
        {
            Logging.Warning($"Startup cleanup failed: {ex.Message}");
        }

        Relaunch([.. RecoveryRetryArguments(args), GraphicsRecoveryArgument]);
    }

    private static void SaveRecoverySettings(string renderer, bool reset)
    {
        const string configPath = "data/cfg/config.cfg";
        const string backupPath = "data/cfg/config.before-recovery.cfg";
        if (!SettingsMenu.Renderers.Contains(renderer))
            throw new ArgumentException("Unsupported renderer.", nameof(renderer));
        var original = TheVFS.VFS.FileExists(configPath) ? TheVFS.VFS.ReadAllText(configPath) : "";
        if (!TheVFS.VFS.FileExists(backupPath))
        {
            using var backup = new StreamWriter(TheVFS.VFS.OpenWrite(backupPath));
            backup.Write(original);
        }

        var updated = GraphicsRecoveryConfig.SelectRenderer(original, renderer, reset);
        using (var writer = new StreamWriter(TheVFS.VFS.OpenWrite(configPath)))
            writer.Write(updated);
        if (TheVFS.VFS.ReadAllText(configPath) != updated)
            throw new IOException("The saved configuration could not be read back.");
        WriteStartupStatus("");
    }

    private static void RunGraphicsRecovery(string[] args)
    {
        var error = ReadStartupStatus();
        if (error.StartsWith(RecoveryUiPrefix, StringComparison.Ordinal))
        {
            RunNativeGraphicsRecovery(args, error[RecoveryUiPrefix.Length..]);
            return;
        }

        var selected = Math.Max(0, Array.IndexOf(SettingsMenu.Renderers, SettingsMenu.SelectedRendererName));
        var retry = false;
        var resetConfirmation = false;
        WorldGame? recovery = null;
        WriteStartupStatus(RecoveryUiPrefix + error);
        try
        {
            _graphicsRecoveryMode = true;
            SettingsMenu.PrepareStartupRecovery();
            _renderer = OperatingSystem.IsWindows() ? Renderer.D3d11 : Renderer.DesktopGl;
            SDL.SDL_ResetHint(SDL.SDL_HINT_OPENGL_ES_DRIVER);
            recovery = new WorldGame();
            recovery.Window.SetTitle("NFM World - Graphics recovery");
            recovery.ImguiRenderer = new SdlImGuiRenderer(recovery._device, recovery.Window);
            ImGuiTheme.Apply();
            var clock = Stopwatch.StartNew();
            var previous = TimeSpan.Zero;
            var firstFrame = true;
            while (!recovery.Window.ShouldQuit)
            {
                recovery.Window.PumpEvents();
                recovery.EnsureSwapchainMatchesWindow();
                var now = clock.Elapsed;
                recovery.ImguiRenderer.BeginLayout(new GameTime(now, now - previous));
                previous = now;
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(recovery.Window.Width, recovery.Window.Height));
                if (ImGui.Begin(
                        "Graphics recovery",
                        ImGuiWindowFlags.NoResize
                        | ImGuiWindowFlags.NoMove
                        | ImGuiWindowFlags.NoCollapse
                        | ImGuiWindowFlags.NoSavedSettings
                    ))
                {
                    ImGui.TextUnformatted("NFM World could not finish starting.");
                    ImGui.TextWrapped(
                        "Choose a renderer to retry, or reset settings to defaults. A copy of your original configuration is kept as config.before-recovery.cfg."
                    );
                    ImGui.Separator();
                    if (ImGui.BeginCombo("Renderer", SettingsMenu.Renderers[selected]))
                    {
                        for (var i = 0; i < SettingsMenu.Renderers.Length; i++)
                            if (ImGui.Selectable(SettingsMenu.Renderers[i], selected == i))
                                selected = i;
                        ImGui.EndCombo();
                    }

                    if (resetConfirmation)
                    {
                        ImGui.TextWrapped("Reset all settings to defaults and retry with Auto?");
                        if (ImGui.Button("Reset and retry")) Save("Auto", true);
                        ImGui.SameLine();
                        if (ImGui.Button("Cancel reset")) resetConfirmation = false;
                    }
                    else
                    {
                        if (ImGui.Button("Save and retry")) Save(SettingsMenu.Renderers[selected], false);
                        ImGui.SameLine();
                        if (ImGui.Button("Reset settings to defaults")) resetConfirmation = true;
                        ImGui.SameLine();
                        if (ImGui.Button("Exit")) recovery.Window.RequestClose();
                    }

                    ImGui.Separator();
                    ImGui.TextUnformatted("Startup error:");
                    if (ImGui.BeginChild("##startup-error", new Vector2(0, 0), ImGuiChildFlags.None))
                        ImGui.TextWrapped(string.IsNullOrWhiteSpace(error) ? "Recovery opened manually." : error);
                    ImGui.EndChild();
                }

                ImGui.End();
                var cb = recovery._device.AcquireCommandBuffer();
                cb.Clear(ClearOptions.Color, new ColorRgba(0.025f, 0.04f, 0.08f));
                recovery.ImguiRenderer.EndLayout(cb);
                recovery._device.Submit(cb);
                recovery._device.Swapchain.Present();
                if (firstFrame)
                {
                    if (!retry) WriteStartupStatus(error);
                    firstFrame = false;
                }

                Thread.Sleep(16);
            }

            void Save(string renderer, bool reset)
            {
                try
                {
                    SaveRecoverySettings(renderer, reset);
                    retry = true;
                    recovery.Window.RequestClose();
                }
                catch (Exception ex)
                {
                    error = $"Could not save recovery settings: {ex.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            RunNativeGraphicsRecovery(args, $"{error}\n\nRecovery renderer failed: {ex.Message}");
            return;
        }
        finally
        {
            if (recovery != null)
            {
                try
                {
                    recovery.ImguiRenderer?.Dispose();
                    recovery._device.Dispose();
                    SDL.SDL_GL_DestroyContext(recovery._glContext);
                    recovery.Window.Dispose();
                }
                catch (Exception ex)
                {
                    Logging.Warning($"Recovery cleanup failed: {ex.Message}");
                }
            }
        }

        if (retry) Relaunch(RecoveryRetryArguments(args));
    }

    private static void RunNativeGraphicsRecovery(string[] args, string error)
    {
        var labels = SettingsMenu.Renderers.Concat(["Reset defaults", "Exit"]).ToArray();
        while (true)
        {
            var choice = NativeRecoveryChoice(
                "NFM World - Graphics recovery",
                "Choose a renderer to retry.\n\n" + error[..Math.Min(error.Length, 1200)],
                labels
            );
            if (choice < 0 || choice >= labels.Length - 1) return;
            var reset = choice == SettingsMenu.Renderers.Length;
            if (reset
                && NativeRecoveryChoice(
                    "Reset settings?",
                    "Reset all settings and retry with Auto? The original config will be backed up.",
                    ["Reset", "Cancel"]
                )
                != 0)
                continue;
            try
            {
                SaveRecoverySettings(reset ? "Auto" : SettingsMenu.Renderers[choice], reset);
                Relaunch(RecoveryRetryArguments(args));
                return;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }
    }

    private static unsafe int NativeRecoveryChoice(string title, string message, string[] labels)
    {
        var strings = new List<nint>();

        byte* Utf8(string text)
        {
            var pointer = Marshal.StringToCoTaskMemUTF8(text);
            strings.Add(pointer);
            return (byte*)pointer;
        }

        try
        {
            SDL.SDL_MessageBoxButtonData* buttons = stackalloc SDL.SDL_MessageBoxButtonData[labels.Length];
            for (var i = 0; i < labels.Length; i++)
                buttons[i] = new SDL.SDL_MessageBoxButtonData
                {
                    buttonID = i,
                    text = Utf8(labels[i]),
                    flags = i == labels.Length - 1 ? SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT : 0
                };
            var data = new SDL.SDL_MessageBoxData
            {
                flags = SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR,
                title = Utf8(title), message = Utf8(message), numbuttons = labels.Length, buttons = buttons
            };
            if (SDL.SDL_ShowMessageBox(ref data, out var choice)) return choice;
            Console.Error.WriteLine($"{title}: {message}\n{SDL.SDL_GetError()}");
            return -1;
        }
        finally
        {
            foreach (var pointer in strings) Marshal.FreeCoTaskMem(pointer);
        }
    }
}
