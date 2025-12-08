using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NFMWorld.Util;
using ImGuiNET;

namespace NFMWorld.Mad
{
    public class DevConsole
    {
        private bool _isOpen = false;
        private string _currentInput = string.Empty;
        private readonly List<(string message, string level)> _outputLog = new();
        private readonly Dictionary<string, Action<DevConsole, string[]>> _commands = new();

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private string? _pendingInput = null;
        
        // Autocomplete state
        private List<string> _autocompleteMatches = new();
        private int _autocompleteIndex = -1;

        public DevConsole()
        {
            DevConsoleCommands.RegisterAll(this);
            Log("NFM-World master-2025.12.08", "info");
        }

        public void Toggle()
        {
            _isOpen = !_isOpen;
        }

        public bool IsOpen()
        {
            return _isOpen;
        }

        private void ExecuteCommand(string input)
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var command = parts[0];
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            if (_commands.TryGetValue(command, out var action))
            {
                action(this, args);
            }
            else
            {
                Log($"Unknown command: {command}");
            }
        }

        public void RegisterCommand(string name, Action<DevConsole, string[]> action)
        {
            _commands[name] = action;
        }

        public IEnumerable<string> GetCommandNames()
        {
            return _commands.Keys;
        }

        public void ClearLog()
        {
            _outputLog.Clear();
        }

        public void Log(string message, string logLevel = "default")
        {
            // Don't log empty strings
            if (string.IsNullOrWhiteSpace(message)) return;
            
            string formattedMessage = message;
            string normalizedLevel = logLevel.ToLowerInvariant();

            // Format the message based on the log level
            switch (normalizedLevel)
            {
                case "warning":
                    formattedMessage = $"[WARN] {message}";
                    break;
                case "error":
                    formattedMessage = message; // No prefix for error
                    break;
                case "info":
                    formattedMessage = message; // No prefix for info
                    break;
                case "debug":
                    formattedMessage = message; // No prefix for debug
                    break;
                default:
                    formattedMessage = message; // Default to plain message
                    normalizedLevel = "default";
                    break;
            }

            _outputLog.Add((formattedMessage, normalizedLevel));
            if (_outputLog.Count > 100) _outputLog.RemoveAt(0); // Limit log size
        }

        public void Render()
        {
            if (!_isOpen) return;

            // windows size
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(50, 50), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(900, 600), ImGuiCond.FirstUseEver);

            // console pos on screen so autocomplete window moves with it
            System.Numerics.Vector2 consolePos = default;
            System.Numerics.Vector2 consoleSize = default;

            bool isOpen = _isOpen;
            if (ImGui.Begin("Console", ref isOpen, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoNavFocus))
            {
                // output log
                if (ImGui.BeginChild("ScrollingRegion", new System.Numerics.Vector2(0, -ImGui.GetFrameHeightWithSpacing()), ImGuiChildFlags.None, ImGuiWindowFlags.None))
                {
                    foreach (var (message, level) in _outputLog)
                    {
                        // Set color based on log level
                        switch (level)
                        {
                            case "warning":
                                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)); // Yellow
                                break;
                            case "error":
                                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1.0f, 0.4f, 0.4f, 1.0f)); // Red
                                break;
                            case "info":
                                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.34f, 0.8f, 1.0f, 1.0f)); // Aqua Blue
                                break;
                            case "debug":
                                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.612f, 1f, 0f, 1.0f)); // Green
                                break;
                            default:
                                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 1f, 1f, 1f)); // Default (White)
                                break;
                        }

                        ImGui.TextUnformatted(message);
                        ImGui.PopStyleColor();
                    }
                    
                    // auto-scroll to bottom
                    if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                        ImGui.SetScrollHereY(1.0f);
                }
                ImGui.EndChild();

                ImGui.Separator();
                ImGui.Spacing();
                
                // focus input when console opens
                if (ImGui.IsWindowAppearing())
                {
                    ImGui.SetWindowFocus();
                    ImGui.SetKeyboardFocusHere();
                }

                if (ImGui.IsWindowFocused())
                {
                    for (int key = (int)ImGuiKey.Tab; key <= (int)ImGuiKey.Menu; key++)
                    {
                        if (ImGui.IsKeyPressed((ImGuiKey)key))
                        {
                            //Console.WriteLine($"Key pressed: {(ImGuiKey)key}");
                        }
                    }
                }

                ImGui.PushItemWidth(-1);
                
                // Apply any pending input from previous frame's history navigation
                if (_pendingInput != null)
                {
                    _currentInput = _pendingInput;
                    _pendingInput = null;
                    Console.WriteLine($"Applied pending input: {_currentInput}");
                }
                
                // Store previous input to detect changes
                string previousInput = _currentInput;
                
                // input field
                ImGui.InputText("##Command", ref _currentInput, 256);
                
                // Check for keys while input has focus
                bool upPressed = ImGui.IsKeyPressed(ImGuiKey.UpArrow);
                bool downPressed = ImGui.IsKeyPressed(ImGuiKey.DownArrow);
                bool tabPressed = ImGui.IsKeyPressed(ImGuiKey.Tab);
                bool enterPressed = ImGui.IsKeyPressed(ImGuiKey.Enter);

                
                // Update autocomplete matches when input changes
                if (_currentInput != previousInput)
                {
                    UpdateAutocomplete();
                }
                
                // Handle autocomplete first (higher priority)
                if (_autocompleteMatches.Count > 0)
                {
                    if (tabPressed && _autocompleteIndex >= 0 && _autocompleteIndex < _autocompleteMatches.Count)
                    {
                        ImGui.SetNextWindowFocus();
                        _currentInput = _autocompleteMatches[_autocompleteIndex];
                        _autocompleteMatches.Clear();
                        _autocompleteIndex = -1;
                        ImGui.SetKeyboardFocusHere(-1); // Refocus input after tab completion
                    }
                    else if (downPressed || upPressed)
                    {
                        ImGui.SetNextWindowFocus();
                        
                        if (downPressed)
                        {
                            _autocompleteIndex = (_autocompleteIndex + 1) % _autocompleteMatches.Count;
                        }
                        else // upPressed
                        {
                            _autocompleteIndex--;
                            if (_autocompleteIndex < 0)
                                _autocompleteIndex = _autocompleteMatches.Count - 1;
                        }
                    }
                }
                // command history (only when no autocomplete active)
                else if (_commandHistory.Count > 0)
                {
                    if (upPressed)
                    {
                        ImGui.SetNextWindowFocus();
                        if (_historyIndex < 0 || _historyIndex >= _commandHistory.Count)
                            _historyIndex = _commandHistory.Count;
                        if (_historyIndex > 0)
                        {
                            _historyIndex--;
                            _currentInput = _commandHistory[_historyIndex];
                        }
                        ImGui.SetKeyboardFocusHere(-1);
                    }
                    else if (downPressed)
                    {
                        ImGui.SetNextWindowFocus();
                        if (_historyIndex < _commandHistory.Count - 1)
                        {
                            _historyIndex++;
                            _currentInput = _commandHistory[_historyIndex];
                        }
                        else if (_historyIndex >= 0)
                        {
                            _historyIndex = _commandHistory.Count;
                            _currentInput = string.Empty;
                        }
                        ImGui.SetKeyboardFocusHere(-1);
                    }
                }
                
                // command execution
                if (enterPressed)
                {
                    if (!string.IsNullOrWhiteSpace(_currentInput))
                    {
                        Log($"> {_currentInput}"); // Show command in log
                        
                        // Add to history BEFORE executing
                        if (_commandHistory.Count == 0 || _commandHistory[^1] != _currentInput)
                        {
                            _commandHistory.Add(_currentInput);
                            if (_commandHistory.Count > 50) _commandHistory.RemoveAt(0);
                        }
                        _historyIndex = _commandHistory.Count;
                        
                        ExecuteCommand(_currentInput);
                        _currentInput = string.Empty;
                        _autocompleteMatches.Clear();
                        _autocompleteIndex = -1;

                        ImGui.SetKeyboardFocusHere(-1);
                        //_shouldRefocusInput = true;
                    }
                }
                
                // Capture console window position BEFORE ending it
                consolePos = ImGui.GetWindowPos();
                consoleSize = ImGui.GetWindowSize();
            }
            ImGui.End();

            _isOpen = isOpen; // Update open state if user closed window
            
            // Render autocomplete popup as separate window
            if (_autocompleteMatches.Count > 0 && !string.IsNullOrWhiteSpace(_currentInput) && _isOpen)
            {
                // Position popup below the console input field using captured position
                var popupPos = new System.Numerics.Vector2(consolePos.X, consolePos.Y + consoleSize.Y + 5);
                
                ImGui.SetNextWindowPos(popupPos, ImGuiCond.Always);
                ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, Math.Min(_autocompleteMatches.Count * 25 + 10, 200)));
                
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4.0f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(8, 8));
                ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0.1f, 0.1f, 0.1f, 0.95f));
                
                if (ImGui.Begin("##Autocomplete", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoInputs))
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
                                new System.Numerics.Vector2(textPos.X - 4, textPos.Y),
                                new System.Numerics.Vector2(textPos.X + textSize.X + 4, textPos.Y + textSize.Y),
                                ImGui.GetColorU32(new System.Numerics.Vector4(0.3f, 0.3f, 0.8f, 0.6f))
                            );
                        }
                        
                        ImGui.Text(_autocompleteMatches[i]);
                    }
                }
                ImGui.End();
                
                ImGui.PopStyleColor();
                ImGui.PopStyleVar(2);
            }
        }
        
        private void UpdateAutocomplete()
        {
            _autocompleteMatches.Clear();
            _autocompleteIndex = -1;
            
            if (string.IsNullOrWhiteSpace(_currentInput))
                return;
            
            // Get the first word (command part)
            var firstSpace = _currentInput.IndexOf(' ');
            var prefix = firstSpace >= 0 ? _currentInput.Substring(0, firstSpace) : _currentInput;
            
            // Find all matching commands
            foreach (var cmd in _commands.Keys)
            {
                if (cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    _autocompleteMatches.Add(cmd);
                }
            }
            
            // Sort matches alphabetically
            _autocompleteMatches.Sort();
            
            // Set initial selection to first match
            if (_autocompleteMatches.Count > 0)
            {
                _autocompleteIndex = 0;
            }
        }
    }
}