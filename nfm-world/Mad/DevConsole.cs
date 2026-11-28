using System.Text;
using Hexa.NET.ImGui;
using NFMWorld.UI;
using NFMWorldLibrary;

namespace NFMWorld;

public class DevConsole
{
    private bool _isOpen = false;
    private bool _focusInput;
    private string _currentInput = string.Empty;
    private readonly byte[] _commandBuffer = new byte[256];
    private readonly Dictionary<string, (Action<DevConsole, string[]> Action, bool Hidden)> _commands = new();

    // UI Windows
    private readonly DebugCameraWindow _cameraSettings = new();
    private readonly DevConsoleOutput _output = new();

    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private int _inputRevision;
    private bool _refocusCommand;

    // Autocomplete state
    private List<string> _autocompleteMatches = new();
    private int _autocompleteIndex = -1;
    private readonly Dictionary<string, Func<string[], int, IReadOnlyList<string>?>> _argumentAutocompleters = new();

    public DevConsole()
    {
        DevConsoleCommands.RegisterAll(this);
        Logging.Info(GameSparker.version);
    }

    public void Toggle()
    {
        _isOpen = !_isOpen;
        _focusInput = _isOpen;
        if (!_isOpen)
            _output.Blur();
    }

    public bool IsOpen()
    {
        return _isOpen;
    }

    public void ToggleCameraSettings()
    {
        _cameraSettings.Toggle();
    }

    public void ExecuteCommand(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)

            return;

        var command = parts[0];
        var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        if (_commands.TryGetValue(command, out var entry))
        {
            entry.Action(this, args);
        }
        else
        {
            Logging.Info($"Unknown command: {command}");
        }
    }

    public void RegisterCommand(string name, Action<DevConsole, string[]> action, bool hidden = false)
    {
        _commands[name] = (action, hidden);
    }

    public void RegisterArgumentAutocompleter(string commandName, Func<string[], int, IReadOnlyList<string>?> autocompleter)
    {
        _argumentAutocompleters[commandName] = autocompleter;
    }

    public IEnumerable<string> GetCommandNames()
    {
        return _commands.Where(entry => !entry.Value.Hidden).Select(entry => entry.Key);
    }

    public void ClearLog()
    {
        NfmwLoggerProvider.ClearMessages();
    }

    public void Render()
    {
        // Render console only if open

        if (_isOpen)
        {
            RenderConsole();
        }

        // temp, render UI windows independently (they manage their own visibility)
        _cameraSettings.Render();
    }

    private void RenderConsole()
    {
        // windows size
        ImGui.SetNextWindowPos(new Vector2(50, 50), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(540, 400), ImGuiCond.FirstUseEver);
        if (_focusInput)
            ImGui.SetNextWindowFocus();

        // console pos on screen so autocomplete window moves with it
        Vector2 consolePos = default;
        Vector2 consoleSize = default;
        bool commandActive = false;

        bool isOpen = _isOpen;

        if (ImGui.Begin(
            "Console",
            ref isOpen,
            ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoNavInputs
            | ImGuiWindowFlags.NoNavFocus
        ))
        {
            if (ImGui.BeginChild(
                "ScrollingRegion",
                new Vector2(0, -ImGui.GetFrameHeight() - 1 - 3 * ImGui.GetStyle().ItemSpacing.Y),
                ImGuiChildFlags.None,
                ImGuiWindowFlags.AlwaysVerticalScrollbar
            ))
            {
                _output.Render();
            }

            ImGui.EndChild();

            ImGui.Separator();
            ImGui.Spacing();

            // focus input when console opens

            if (_focusInput || ImGui.IsWindowAppearing())
            {
                ImGui.SetWindowFocus();
                _refocusCommand = true;
                _focusInput = false;
            }

            commandActive = RenderCommandInput();

            // Capture console window position BEFORE ending it
            consolePos = ImGui.GetWindowPos();
            consoleSize = ImGui.GetWindowSize();
        }

        ImGui.End();

        _isOpen = isOpen; // Update open state if user closed window
        if (!_isOpen)
            _output.Blur();

        // Render autocomplete popup as separate window

        if (_isOpen && commandActive && _autocompleteMatches.Count > 0)
        {
            // Position popup below the console input field using captured position
            var popupPos = new Vector2(consolePos.X, consolePos.Y + consoleSize.Y + 5);

            ImGui.SetNextWindowPos(popupPos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(400, Math.Min(_autocompleteMatches.Count * 25 + 10, 200)));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.GetStyle().Colors[(int)ImGuiCol.PopupBg]);

            if (ImGui.Begin(
                "##Autocomplete",
                ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoInputs
            ))
            {
                if (ImGui.BeginChild("##AutocompleteScroll", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None))
                {
                    for (int i = 0; i < _autocompleteMatches.Count; i++)
                    {
                        bool isSelected = i == _autocompleteIndex;

                        if (isSelected)
                        {
                            // Draw selection background
                            var drawList = ImGui.GetWindowDrawList();
                            var textPos = ImGui.GetCursorScreenPos();
                            var textSize = ImGui.CalcTextSize(_autocompleteMatches[i]);
                            drawList.AddRectFilled(
                                new Vector2(textPos.X - 4, textPos.Y),
                                new Vector2(textPos.X + textSize.X + 4, textPos.Y + textSize.Y),
                                ImGui.GetColorU32(ImGuiCol.HeaderHovered)
                            );
                        }

                        ImGui.Text(_autocompleteMatches[i]);

                        // Auto-scroll to selected item

                        if (isSelected)
                        {
                            ImGui.SetScrollHereY(0.5f);
                        }
                    }
                }

                ImGui.EndChild();
            }

            ImGui.End();

            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
        }
    }

    private unsafe bool RenderCommandInput()
    {
        if (_refocusCommand)
        {
            ImGui.SetKeyboardFocusHere();
            _refocusCommand = false;
            _output.Blur();
        }

        var previousInput = _currentInput;
        ImGui.PushID(_inputRevision);
        ImGui.SetNextItemWidth(-1);
        bool submitted;
        fixed (byte* buffer = _commandBuffer)
            submitted = ImGui.InputText("##Command", buffer, (nuint)_commandBuffer.Length, ImGuiInputTextFlags.EnterReturnsTrue);
        var active = ImGui.IsItemActive();
        ImGui.PopID();
        if (active || submitted)
            _output.Blur();
        _currentInput = Encoding.UTF8.GetString(_commandBuffer.AsSpan(0, Array.IndexOf(_commandBuffer, (byte)0)));
        if (_currentInput != previousInput)
            UpdateAutocomplete();

        if (!active && !submitted)

            return false;

        var up = ImGui.IsKeyPressed(ImGuiKey.UpArrow);
        var down = ImGui.IsKeyPressed(ImGuiKey.DownArrow);

        if (_autocompleteIndex >= 0)
        {
            if (submitted || ImGui.IsKeyPressed(ImGuiKey.Tab, false))
                ReplaceCommandInput(_autocompleteMatches[_autocompleteIndex]);
            else if (up || down)
                _autocompleteIndex = (_autocompleteIndex + (down ? 1 : -1) + _autocompleteMatches.Count)
                                     % _autocompleteMatches.Count;
        }
        else if (submitted)
        {
            if (!string.IsNullOrWhiteSpace(_currentInput))
            {
                Logging.Info($"> {_currentInput}");

                if (_commandHistory.Count == 0 || _commandHistory[^1] != _currentInput)
                {
                    _commandHistory.Add(_currentInput);
                    if (_commandHistory.Count > 50)
                        _commandHistory.RemoveAt(0);
                }

                _historyIndex = _commandHistory.Count;
                ExecuteCommand(_currentInput);
            }

            ReplaceCommandInput(string.Empty);
        }
        else if ((up || down) && _commandHistory.Count > 0)
        {
            var current = _historyIndex < 0 ? _commandHistory.Count : _historyIndex;
            _historyIndex = Math.Clamp(current + (up ? -1 : 1), 0, _commandHistory.Count);
            ReplaceCommandInput(_historyIndex == _commandHistory.Count ? string.Empty : _commandHistory[_historyIndex]);
        }

        return active;
    }

    private void ReplaceCommandInput(string text)
    {
        Encoding.UTF8.GetEncoder().Convert(
            text,
            _commandBuffer.AsSpan(0, _commandBuffer.Length - 1),
            true,
            out _,
            out var length,
            out _
        );
        _commandBuffer[length] = 0;
        _currentInput = Encoding.UTF8.GetString(_commandBuffer.AsSpan(0, length));
        _autocompleteMatches.Clear();
        _autocompleteIndex = -1;
        // Give the changed text a fresh widget ID to discard ImGui's active edit buffer.
        _inputRevision++;
        _refocusCommand = true;
    }

    private void UpdateAutocomplete()
    {
        _autocompleteMatches.Clear();
        _autocompleteIndex = -1;

        if (string.IsNullOrWhiteSpace(_currentInput))

            return;

        // Split input into parts
        var parts = _currentInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var firstSpace = _currentInput.IndexOf(' ');

        // If no space yet, autocomplete command names

        if (firstSpace < 0)
        {
            var prefix = parts.Length > 0 ? parts[0] : string.Empty;

            // Find all matching commands

            foreach (var cmd in GetCommandNames())
            {
                if (cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    _autocompleteMatches.Add(cmd);
                }
            }

            // Sort command matches alphabetically
            _autocompleteMatches.Sort();
        }
        // If we have a space, we're in argument completion mode
        else if (parts.Length > 0)
        {
            var command = parts[0];
            var args = parts.Length > 1 ? parts[1..] : [];
            var currentArg = _currentInput.EndsWith(' ') ? string.Empty : (args.Length > 0 ? args[^1] : string.Empty);

            // Determine which argument position we're on (0-based)
            int argPosition = _currentInput.EndsWith(' ') ? args.Length : Math.Max(0, args.Length - 1);

            // Check if this command has an argument autocompleter

            if (_commands.TryGetValue(command, out var entry)
                && !entry.Hidden
                && _argumentAutocompleters.TryGetValue(command, out var autocompleter))
            {
                var suggestions = autocompleter(args, argPosition);

                // If no suggestions returned, don't show autocomplete for this position

                if (suggestions == null || suggestions.Count == 0)
                {
                    return;
                }

                foreach (var suggestion in suggestions)
                {
                    // partial matching
                    bool matches = string.IsNullOrEmpty(currentArg)
                                   || suggestion.StartsWith(currentArg, StringComparison.OrdinalIgnoreCase)
                                   || suggestion.Contains(currentArg, StringComparison.OrdinalIgnoreCase);

                    if (matches)
                    {
                        // Build the full command with this suggestion
                        var previousArgs = args.Length > 0 && !_currentInput.EndsWith(' ') ? args[..^1] : args;
                        var fullCommand = command + " " + string.Join(" ", previousArgs);
                        if (previousArgs.Length > 0)
                            fullCommand += " ";
                        _autocompleteMatches.Add(fullCommand + suggestion);
                    }
                }

                // Don't sort - argument completers may have custom sorting (e.g., natural sort for numbers)
            }
        }

        // Set initial selection to first match
        var input = _currentInput.TrimEnd();
        var exactMatch = _autocompleteMatches.RemoveAll(match => match == input) != 0;

        if (exactMatch)
            _autocompleteMatches.Clear();
        else if (_autocompleteMatches.Count > 0)
        {
            _autocompleteIndex = 0;
        }
    }
}
