using ImGuiNET;
using System;

namespace NFMWorld.Mad.UI;

/// <summary>
/// Universal message window using ImGui that can display messages with configurable buttons.
/// 
/// </summary>
/// <remarks>
/// Usage Examples:
/// 
/// 1. Simple OK message:
///    GameSparker.MessageWindow.ShowMessage("Title", "Your message here", 
///        result => Console.WriteLine($"Clicked: {result}"));
/// 
/// 2. Yes/No confirmation:
///    GameSparker.MessageWindow.ShowYesNo("Confirm", "Are you sure?",
///        result => {
///            if (result == MessageWindow.MessageResult.Yes) {
///                // User clicked Yes
///            }
///        });
/// 
/// 3. OK/Cancel dialog:
///    GameSparker.MessageWindow.ShowOKCancel("Warning", "Continue?",
///        result => { /* handle result */ });
/// 
/// 4. Custom buttons:
///    GameSparker.MessageWindow.ShowCustom("Choose", "Select option:",
///        new[] { "Option 1", "Option 2", "Option 3" },
///        result => {
///            // result will be Custom1, Custom2, or Custom3
///        });
/// 
/// Test in console with: msg ok, msg yesno, msg okcancel, msg custom
/// </remarks>
public class MessageWindow
{
    public enum ButtonType
    {
        OK,
        YesNo,
        OKCancel,
        Custom
    }

    public enum MessageResult
    {
        None,
        OK,
        Yes,
        No,
        Cancel,
        Custom1,
        Custom2,
        Custom3
    }

    private bool _isOpen;
    private string _title = "";
    private string _message = "";
    private ButtonType _buttonType = ButtonType.OK;
    private string[] _customButtonLabels = Array.Empty<string>();
    private Action<MessageResult>? _callback;
    private MessageResult _result = MessageResult.None;

    public bool IsOpen => _isOpen;

    /// <summary>
    /// Show a message window with OK button
    /// </summary>
    public void ShowMessage(string title, string message, Action<MessageResult>? callback = null)
    {
        Show(title, message, ButtonType.OK, callback);
    }

    /// <summary>
    /// Show a message window with Yes/No buttons
    /// </summary>
    public void ShowYesNo(string title, string message, Action<MessageResult>? callback = null)
    {
        Show(title, message, ButtonType.YesNo, callback);
    }

    /// <summary>
    /// Show a message window with OK/Cancel buttons
    /// </summary>
    public void ShowOKCancel(string title, string message, Action<MessageResult>? callback = null)
    {
        Show(title, message, ButtonType.OKCancel, callback);
    }

    /// <summary>
    /// Show a message window with custom buttons
    /// </summary>
    public void ShowCustom(string title, string message, string[] buttonLabels, Action<MessageResult>? callback = null)
    {
        _customButtonLabels = buttonLabels;
        Show(title, message, ButtonType.Custom, callback);
    }

    private void Show(string title, string message, ButtonType buttonType, Action<MessageResult>? callback = null)
    {
        _isOpen = true;
        _title = title;
        _message = message;
        _buttonType = buttonType;
        _callback = callback;
        _result = MessageResult.None;
    }

    public void Close()
    {
        _isOpen = false;
        _result = MessageResult.None;
    }

    public void Render()
    {
        if (!_isOpen)
            return;

        // Center the window on screen
        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 0), ImGuiCond.Appearing);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse | 
                                 ImGuiWindowFlags.NoResize | 
                                 ImGuiWindowFlags.AlwaysAutoResize;

        if (ImGui.Begin(_title, ref _isOpen, flags))
        {
            // Display the message
            ImGui.TextWrapped(_message);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Draw buttons based on button type
            DrawButtons();

            ImGui.End();
        }

        // If window was closed without clicking a button, treat as cancel/close
        if (!_isOpen && _result == MessageResult.None)
        {
            _result = MessageResult.Cancel;
            InvokeCallback();
        }
    }

    private void DrawButtons()
    {
        // Calculate button layout
        float buttonWidth = 100f;
        float spacing = 8f;
        float totalWidth;
        
        switch (_buttonType)
        {
            case ButtonType.OK:
                totalWidth = buttonWidth;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);
                
                if (ImGui.Button("OK", new System.Numerics.Vector2(buttonWidth, 0)))
                {
                    _result = MessageResult.OK;
                    _isOpen = false;
                    InvokeCallback();
                }
                break;

            case ButtonType.YesNo:
                totalWidth = buttonWidth * 2 + spacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);
                
                if (ImGui.Button("Yes", new System.Numerics.Vector2(buttonWidth, 0)))
                {
                    _result = MessageResult.Yes;
                    _isOpen = false;
                    InvokeCallback();
                }
                
                ImGui.SameLine(0, spacing);
                
                if (ImGui.Button("No", new System.Numerics.Vector2(buttonWidth, 0)))
                {
                    _result = MessageResult.No;
                    _isOpen = false;
                    InvokeCallback();
                }
                break;

            case ButtonType.OKCancel:
                totalWidth = buttonWidth * 2 + spacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);
                
                if (ImGui.Button("OK", new System.Numerics.Vector2(buttonWidth, 0)))
                {
                    _result = MessageResult.OK;
                    _isOpen = false;
                    InvokeCallback();
                }
                
                ImGui.SameLine(0, spacing);
                
                if (ImGui.Button("Cancel", new System.Numerics.Vector2(buttonWidth, 0)))
                {
                    _result = MessageResult.Cancel;
                    _isOpen = false;
                    InvokeCallback();
                }
                break;

            case ButtonType.Custom:
                if (_customButtonLabels.Length > 0)
                {
                    totalWidth = buttonWidth * _customButtonLabels.Length + spacing * (_customButtonLabels.Length - 1);
                    ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);

                    for (int i = 0; i < _customButtonLabels.Length; i++)
                    {
                        if (i > 0)
                        {
                            ImGui.SameLine(0, spacing);
                        }

                        if (ImGui.Button(_customButtonLabels[i], new System.Numerics.Vector2(buttonWidth, 0)))
                        {
                            _result = i switch
                            {
                                0 => MessageResult.Custom1,
                                1 => MessageResult.Custom2,
                                2 => MessageResult.Custom3,
                                _ => MessageResult.None
                            };
                            _isOpen = false;
                            InvokeCallback();
                        }
                    }
                }
                break;
        }
    }

    private void InvokeCallback()
    {
        _callback?.Invoke(_result);
    }
}
