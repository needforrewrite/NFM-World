using Microsoft.Xna.Framework.Graphics;
using nfm_world_library;
using nfm_world_library.mad.rad;
using nfm_world_library.util;
using nfm_world.account;
using nfm_world.multiplayer;
using nfm_world.ui;
using nfm_world.util;

namespace nfm_world.gameplay;

// TODO: implement the same menu as in nfm-lit

public class MainMenuPhase : BasePhase
{
    public enum MenuState
    {
        Main,
        Play,
        Workshop,
        Singleplayer,
        Multiplayer,
        Training,
        Instructions,
    }

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
    public MenuState _currentMenuState = MenuState.Main;
    private readonly List<MenuState> _menuHistory = new();

    private GraphicsDevice _graphicsDevice;

    private Rad3d? garageSelectedCar;
    private AccountManagerFloatingMenu? accountManagerMenu;

    public MainMenuPhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        // Create title and button fonts
        _titleFont = new Font(FontFamily.DroidSans, FontStyle.Bold, 48); // Large title
        _buttonFont = new Font(FontFamily.DroidSans, FontStyle.Bold, 24); // Button text

        BuildMainMenu();
    }

    public void BuildMainMenu()
    {
        _buttons.Clear();

        // Initialize buttons (matching the image positions)
        int buttonWidth = 230;
        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        AddButton(startX, startY + spacing * 0, buttonWidth, buttonHeight, "PLAY", OnPlayClicked);
        AddButton(startX, startY + spacing * 1, buttonWidth, buttonHeight, "GARAGE", OnGarageClicked);
        AddButton(startX, startY + spacing * 2, buttonWidth, buttonHeight, "WORKSHOP", OnWorkshopClicked);
        AddButton(startX, startY + spacing * 3, buttonWidth, buttonHeight, "SETTINGS", OnSettingsClicked);
        AddButton(startX, startY + spacing * 4, buttonWidth, buttonHeight, "CREDITS", OnClickUnavailable);
        AddButton(startX, startY + spacing * 5, buttonWidth, buttonHeight, "QUIT", OnQuitClicked);

        //AddButton((int)(G.Viewport.X - startX - buttonWidth), startY, buttonWidth, buttonHeight, "LOGOUT", OnLogoutClicked);
        AddButton((int)(G.Viewport.X - startX - buttonWidth), startY, buttonWidth, buttonHeight, "LOGIN", OnLoginClicked);
    }

    private void BuildWorkshopMenu()
    {
        _buttons.Clear();

        // Initialize submenu buttons
        int buttonWidth = 235;
        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        AddButton(startX, startY + spacing * 0, buttonWidth, buttonHeight, "MODEL EDITOR", OnModelEditorClicked);
        AddButton(startX, startY + spacing * 1, buttonWidth, buttonHeight, "STAGE EDITOR", OnStageEditorClicked);
        AddButton(startX, startY + spacing * 2, buttonWidth, buttonHeight, "CAMPAIGN EDITOR", OnClickUnavailable);
        AddButton(startX, startY + spacing * 5, buttonWidth, buttonHeight, "BACK", OnBackClicked);
    }

    private void BuildPlayMenu()
    {
        _buttons.Clear();

        // Initialize submenu buttons
        int buttonWidth = 235;
        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        AddButton(startX, startY + spacing * 0, buttonWidth, buttonHeight, "SINGLEPLAYER", OnSPClicked);
        AddButton(startX, startY + spacing * 1, buttonWidth, buttonHeight, "MULTIPLAYER", OnMPClicked);
        AddButton(startX, startY + spacing * 2, buttonWidth, buttonHeight, "TRAINING", OnTrainingClicked);
        AddButton(startX, startY + spacing * 5, buttonWidth, buttonHeight, "BACK", OnBackClicked);
    }

    private void BuildSPMenu()
    {
        _buttons.Clear();

        // Initialize submenu buttons
        int buttonWidth = 235;
        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        AddButton(startX, startY + spacing * 0, buttonWidth, buttonHeight, "NFM 1", OnClickUnavailable);
        AddButton(startX, startY + spacing * 1, buttonWidth, buttonHeight, "NFM 2", OnClickUnavailable);
        AddButton(startX, startY + spacing * 2, buttonWidth, buttonHeight, "CUSTOM CAMPAIGN", OnClickUnavailable);
        AddButton(startX, startY + spacing * 3, buttonWidth, buttonHeight, "FREE PLAY", OnFreePlayClicked);
        AddButton(startX, startY + spacing * 5, buttonWidth, buttonHeight, "BACK", OnBackClicked);
    }

    private void BuildMPMenu()
    {
        _buttons.Clear();

        // Initialize submenu buttons
        int buttonWidth = 235;
        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        // this should put you in phy's matchmaking system after matchmaking settings have been selected, as well as ability to spectate ongoing games
        AddButton(startX, startY + spacing * 0, buttonWidth, buttonHeight, "COMPETITIVE", OnClickUnavailable);

        // this should put you in a lobby like maxine is developing right now
        AddButton(startX, startY + spacing * 1, buttonWidth, buttonHeight, "CASUAL", OnClickUnavailable);

        AddButton(startX, startY + spacing * 5, buttonWidth, buttonHeight, "BACK", OnBackClicked);
    }

    private void BuildTrainingMenu()
    {
        _buttons.Clear();

        // Initialize submenu buttons
        int buttonWidth = 235;
        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        AddButton(startX, startY + spacing * 0, buttonWidth, buttonHeight, "TIME TRIALS", OnTTClicked);

        //lowkey could challenges be merged in to TTs? not sure
        AddButton(startX, startY + spacing * 1, buttonWidth, buttonHeight, "CHALLENGES", OnClickUnavailable);

        AddButton(startX, startY + spacing * 2, buttonWidth, buttonHeight, "GAME INSTRUCTIONS", OnClickUnavailable);
        AddButton(startX, startY + spacing * 5, buttonWidth, buttonHeight, "BACK", OnBackClicked);
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
            OnClick = onClick ?? (() => { }), // Empty action for non-implemented buttons,
        });
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse)
    {
        base.MouseMoved(x, y, imguiWantsMouse);

        _mouseX = x;
        _mouseY = y;

        // Don't process mouse if ImGui is capturing it
        if (imguiWantsMouse)
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
                Logging.Debug($"Hovering over button: {button.Text} at mouse ({x}, {y})");
            }
            _buttons[i] = button;
        }
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse)
    {
        base.MousePressed(x, y, imguiWantsMouse);

        // Don't process clicks if ImGui is capturing the mouse
        if (imguiWantsMouse)
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

    public override void Render()
    {
        base.Render();

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

        int buttonHeight = 35;
        int startX = 100;
        int startY = 390;
        int spacing = 40;

        string loginText = GameSparker.AccountManager.ActiveAccount is not null ?
            "Logged in as: " + GameSparker.AccountManager.ActiveAccount.Username :
            "Logged Out";

        var b = _buttons.Find(b => b.Text == "LOGIN");

        G.DrawStringAligned(
            loginText,
            b.X + b.Width,
            startY + buttonHeight + spacing,
            driverinterface.TextHorizontalAlignment.Right,
            driverinterface.TextVerticalAlignment.Bottom
        );


        // Debug: Show mouse position
        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Plain, 12));
        G.SetColor(new Color(255, 255, 0)); // Yellow
        G.DrawString($"Mouse: ({_mouseX}, {_mouseY})", 10, 30);

        // Draw tooltip at bottom (similar to image)
        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Plain, 14));
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
                    // Workshop submenu
                    "MODEL EDITOR" => "View and edit custom models.",
                    "STAGE EDITOR" => "Design your own stages.",
                    "CAMPAIGN EDITOR" => "Craft custom experiences.",
                    // Play submenu
                    "SINGLEPLAYER" => "Play the original single player experiences.",
                    "MULTIPLAYER" => "Play online with other players.",
                    "TRAINING" => "Train your skills and learn the game mechanics.",
                    // SP submenu
                    "NFM 1" => "Play the original Need For Madness campaign.",
                    "NFM 2" => "Play the original Need For Madness 2 campaign.",
                    "CUSTOM CAMPAIGN" => "Play custom experiences crafted by the community.",
                    "FREE PLAY" => "The World is your oyster.",
                    // MP submenu
                    "COMPETITIVE" => "Compete against other players via matchmaking.",
                    "CASUAL" => "Play with people in a free relaxed environment.",
                    // Training submenu
                    "TIME TRIALS" => "Flex your fastest time against other people.",
                    "CHALLENGES" => "Complete challenges to sharpen your mechanical skills.",
                    "GAME INSTRUCTIONS" => "Read about the rules and controls of the game.",
                    //
                    "BACK" => "Return to the previous menu.",
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
        int textY = button.Y + 25; // Adjusted for proper vertical centering

        G.DrawString(button.Text, textX, textY);
    }

    private void OnFreePlayClicked()
    {
        GameSparker.InRace.LoadStage("nfm2/15_dwm");
        GameSparker.StartGame();
    }

    private void OnWorkshopClicked()
    {
        _menuHistory.Add(_currentMenuState);
        _currentMenuState = MenuState.Workshop;
        BuildWorkshopMenu();
    }

    private void OnBackClicked()
    {
        if (_menuHistory.Count > 0)
        {
            var previous = _menuHistory[_menuHistory.Count - 1];
            _menuHistory.RemoveAt(_menuHistory.Count - 1);
            _currentMenuState = previous;
            switch (_currentMenuState)
            {
                case MenuState.Main:
                    BuildMainMenu();
                    break;
                case MenuState.Play:
                    BuildPlayMenu();
                    break;
                case MenuState.Workshop:
                    BuildWorkshopMenu();
                    break;
                case MenuState.Singleplayer:
                    BuildSPMenu();
                    break;
                case MenuState.Multiplayer:
                    BuildMPMenu();
                    break;
                case MenuState.Training:
                    BuildTrainingMenu();
                    break;
                case MenuState.Instructions:
                    // If you have an instructions menu, call its builder here
                    break;
                default:
                    BuildMainMenu();
                    break;
            }
        }
        else
        {
            _currentMenuState = MenuState.Main;
            BuildMainMenu();
        }
    }
    private void OnLoginClicked()
    {
        accountManagerMenu ??= new AccountManagerFloatingMenu();
    }

    private void OnLogoutClicked()
    {
        if (GameSparker.AccountManager.LoggedIn)
        {
            GameSparker.AccountManager.LogOut();
        }
    }

    private void OnPlayClicked()
    {
        _menuHistory.Add(_currentMenuState);
        _currentMenuState = MenuState.Play;
        BuildPlayMenu();
    }

    private void OnSPClicked()
    {
        _menuHistory.Add(_currentMenuState);
        _currentMenuState = MenuState.Singleplayer;
        BuildSPMenu();
    }

    private void OnTrainingClicked()
    {
        _menuHistory.Add(_currentMenuState);
        _currentMenuState = MenuState.Training;
        BuildTrainingMenu();
    }

    private void OnTTClicked()
    {
        GameSparker.InRace?.gamemode = GameModes.TimeTrial;

        StageSelectPhase ssp = new(_graphicsDevice);
        ssp.StageSelected += (sender, stage) =>
        {
            GameSparker.InRace.CurrentStage = stage;
            GameSparker.InRace.clientStageRenderer = new ClientStageRenderer(_graphicsDevice, stage);
            GameSparker.InRace.RecreateScene();
            GameSparker.InRace.LoadStageMusic(true);
            GameSparker.SetPhase(this);

            GaragePhase gp = new(_graphicsDevice);
            gp.CarSelected += (sender, car) =>
            {
                GameSparker.InRace.playerCarName = car.FileName;
                GameSparker.SetPhase(GameSparker.InRace);
            };

            gp.CarSelectionCancelled += (sender, _) =>
            {
                GameSparker.SetPhase(GameSparker.MainMenu);
            };

            gp.StageOverride = GameSparker.InRace.CurrentStage;
            gp.ClientStageRendererOverride = GameSparker.InRace.clientStageRenderer;
            GameSparker.SetPhase(gp);
        };

        GameSparker.SetPhase(ssp);
    }

    private void OnGarageClicked()
    {
        GaragePhase gp = new GaragePhase(_graphicsDevice);
        gp.Enter();
        gp.CarSelected += (sender, c) =>
        {
            garageSelectedCar = c;
            GameSparker.SetPhase(this);
        };

        gp.CarSelectionCancelled += (sender, _) =>
        {
            GameSparker.SetPhase(this);
        };

        GameSparker.SetPhase(gp);
    }


    private void OnMPClicked()
    {
        // this should authenticate the player before showing the menu
        _menuHistory.Add(_currentMenuState);
        _currentMenuState = MenuState.Multiplayer;
        BuildMPMenu();
    }

    private void OnModelEditorClicked()
    {
        GameSparker.StartModelViewer();
    }

    private void OnStageEditorClicked()
    {
        GameSparker.StartStageEditor();
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
        result =>
        {
            if (result == MessageWindow.MessageResult.Yes)
            {
                System.Environment.Exit(0);
            }
        });
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;

        // Handle key capture for settings menu
        if (GameSparker.SettingsMenu.IsOpen && GameSparker.SettingsMenu.IsCapturingKey())
        {
            GameSparker.SettingsMenu.HandleKeyCapture(key);
        }
        return;
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        if (accountManagerMenu is not null)
        {
            var res = accountManagerMenu.Process();
            if (res == AccountManagerFloatingMenu.AccountManagerFloatingMenuState.LoggedIn)
            {
                accountManagerMenu.Close();
                accountManagerMenu = null;
            }
            else if (res == AccountManagerFloatingMenu.AccountManagerFloatingMenuState.Canceled)
            {
                accountManagerMenu.Close();
                accountManagerMenu = null;
            }
            ;
        }
    }
}