using NFMWorld.Util;
using ImGuiNET;

namespace NFMWorld.Mad.UI;

public class MainMenu
{
    private struct Button
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public string Text;
        public Action OnClick;
        public bool IsHovered;
    }

    private readonly List<Button> _buttons = new();
    private int _mouseX;
    private int _mouseY;
    private readonly Font _titleFont;
    private readonly Font _buttonFont;

    public MainMenu()
    {
        // Create title and button fonts
        _titleFont = new Font("Arial", 1, 48); // Large title
        _buttonFont = new Font("Arial", 1, 24); // Button text

        // Initialize buttons (matching the image positions)
        int buttonWidth = 230;
        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        AddButton(startX, startY + spacing * 0, buttonWidth, buttonHeight, "PLAY", OnPlayClicked);
        AddButton(startX, startY + spacing * 1, buttonWidth, buttonHeight, "GARAGE", OnClickUnavailable);
        AddButton(startX, startY + spacing * 2, buttonWidth, buttonHeight, "WORKSHOP", OnClickUnavailable);
        AddButton(startX, startY + spacing * 3, buttonWidth, buttonHeight, "SETTINGS", OnSettingsClicked);
        AddButton(startX, startY + spacing * 4, buttonWidth, buttonHeight, "CREDITS", OnClickUnavailable);
        AddButton(startX, startY + spacing * 5, buttonWidth, buttonHeight, "QUIT", OnQuitClicked);
    }

    private void AddButton(int x, int y, int width, int height, string text, Action? onClick)
    {
        _buttons.Add(new Button
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Text = text,
            OnClick = onClick ?? (() => { }) // Empty action for non-implemented buttons
        });
    }

    public void UpdateMouse(int x, int y)
    {
        _mouseX = x;
        _mouseY = y;

        // Don't process mouse if ImGui is capturing it
        if (ImGui.GetIO().WantCaptureMouse)
        {
            // Clear all hover states when ImGui is active
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                button.IsHovered = false;
                _buttons[i] = button;
            }
            return;
        }

        // Update hover states
        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            bool wasHovered = button.IsHovered;
            button.IsHovered = IsPointInButton(x, y, button);
            if (button.IsHovered && !wasHovered)
            {
                Console.WriteLine($"Hovering over button: {button.Text} at mouse ({x}, {y})");
            }
            _buttons[i] = button;
        }
    }

    public void HandleClick(int x, int y)
    {
        // Don't process clicks if ImGui is capturing the mouse
        if (ImGui.GetIO().WantCaptureMouse)
            return;

        foreach (var button in _buttons)
        {
            if (IsPointInButton(x, y, button))
            {
                button.OnClick?.Invoke();
                break;
            }
        }
    }

    private bool IsPointInButton(int x, int y, Button button)
    {
        return x >= button.X && x <= button.X + button.Width &&
               y >= button.Y && y <= button.Y + button.Height;
    }

    public void Render()
    {
        // Clear to dark purple background
        G.SetColor(new Color(15, 0, 35));
        G.FillRect(0, 0, 1920, 1080);

        // Draw title
        G.SetFont(_titleFont);
        G.SetColor(new Color(255, 140, 0)); // Orange
        
        // Draw "NEED FOR MADNESS?" with styling similar to the image
        G.DrawString("NEED FOR MADNESS?", 90, 290);

        // Draw buttons
        G.SetFont(_buttonFont);
        foreach (var button in _buttons)
        {
            DrawButton(button);
        }
        
        // Debug: Show mouse position
        G.SetFont(new Font("Arial", 0, 12));
        G.SetColor(new Color(255, 255, 0)); // Yellow
        G.DrawString($"Mouse: ({_mouseX}, {_mouseY})", 10, 30);

        // Draw tooltip at bottom (similar to image)
        G.SetFont(new Font("Arial", 0, 14));
        G.SetColor(new Color(255, 140, 0)); // Orange
        
        // Find hovered button and show description
        foreach (var button in _buttons)
        {
            if (button.IsHovered)
            {
                string description = button.Text switch
                {
                    "PLAY" => "Play public, private matches online or play singleplayer.",
                    "GARAGE" => "Customize and inspect your vehicles in the garage.",
                    "WORKSHOP" => "Build your own models and stages.",
                    "SETTINGS" => "Adjust game settings.",
                    "CREDITS" => "View game credits.",
                    "QUIT" => "Exit the game.",
                    _ => ""
                };
                G.DrawString(description, 120, 637);
                break;
            }
        }
    }

    private void DrawButton(Button button)
    {
        // Button background and border
        if (button.IsHovered)
        {
            // Hovered state - filled orange background
            G.SetColor(new Color(255, 140, 0)); // Bright orange
            G.FillRect(button.X, button.Y, button.Width, button.Height);
            
            // Inner dark background
            G.SetColor(new Color(20, 15, 35));
            G.FillRect(button.X + 3, button.Y + 3, button.Width - 6, button.Height - 6);
            
            // Border
            G.SetColor(new Color(255, 140, 0)); // Orange
            G.DrawRect(button.X, button.Y, button.Width, button.Height);
        }
        else
        {
            // Normal state - just orange border, no fill
            G.SetColor(new Color(255, 140, 0)); // Orange
            G.DrawRect(button.X, button.Y, button.Width, button.Height);
        }

        // Button text
        G.SetColor(new Color(255, 140, 0)); // Orange text
        G.SetFont(_buttonFont);
        
        // Center the text in the button
        int textX = button.X + 12; // Left-aligned with padding
        int textY = button.Y + 5; // Adjusted for proper vertical centering
        
        G.DrawString(button.Text, textX, textY);
    }

    private void OnPlayClicked()
    {
        GameSparker.StartGame();
    }

    private void OnSettingsClicked()
    {
        GameSparker.SettingsMenu.Open();
    }

    private void OnClickUnavailable()
    {
        GameSparker.MessageWindow.ShowMessage("Info", "This feature is currently unavailable.");
    }

    private void OnQuitClicked()
    {
        GameSparker.MessageWindow.ShowYesNo("Quit", "Are you sure you want to quit?",
        result => {
            if (result == MessageWindow.MessageResult.Yes) {
                System.Environment.Exit(0);
            }
        });
    }
}
