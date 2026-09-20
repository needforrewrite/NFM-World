using Hexa.NET.ImGui;

namespace NFMWorld.UI;

/// <summary>
/// The game's custom dark-purple/orange ImGui style, ported from the old FNA-based
/// <c>NFMWWindow.cs</c> (see git history, commit "Re-add imgui") onto <c>Hexa.NET.ImGui</c> - applied
/// once after <see cref="SdlImGuiRenderer"/> creates its ImGui context.
/// </summary>
public static class ImGuiTheme
{
    public static void Apply()
    {
        ImGui.StyleColorsDark();

        var style = ImGui.GetStyle();

        // Rounding
        style.WindowRounding = 4.0f;
        style.FrameRounding = 6.0f;
        style.GrabRounding = 4.0f;
        style.PopupRounding = 6.0f;
        style.ScrollbarRounding = 6.0f;
        style.TabRounding = 4.0f;

        // Spacing and padding
        style.WindowPadding = new System.Numerics.Vector2(12, 12);
        style.FramePadding = new System.Numerics.Vector2(8, 4);
        style.ItemSpacing = new System.Numerics.Vector2(8, 6);

        // Border
        style.WindowBorderSize = 2.0f;
        style.FrameBorderSize = 2.0f;

        System.Numerics.Vector4 Rgb(int r, int g, int b, float a = 1.0f) => new(r / 255f, g / 255f, b / 255f, a);

        var colors = style.Colors;

        // Windows and backgrounds
        colors[(int)ImGuiCol.WindowBg] = Rgb(31, 26, 46, 0.95f);          // Dark purple
        colors[(int)ImGuiCol.ChildBg] = Rgb(26, 20, 38, 0.90f);           // Darker purple
        colors[(int)ImGuiCol.PopupBg] = Rgb(26, 20, 38, 0.95f);           // Darker purple
        colors[(int)ImGuiCol.MenuBarBg] = Rgb(38, 31, 56, 1.0f);          // Medium purple

        // Borders
        colors[(int)ImGuiCol.Border] = Rgb(230, 128, 26, 0.8f);           // Orange
        colors[(int)ImGuiCol.BorderShadow] = Rgb(0, 0, 0, 0.5f);          // Black shadow

        // Text
        colors[(int)ImGuiCol.Text] = Rgb(255, 191, 51, 1.0f);             // Light orange/yellow
        colors[(int)ImGuiCol.TextDisabled] = Rgb(153, 115, 38, 1.0f);     // Dimmed orange

        // Title bar
        colors[(int)ImGuiCol.TitleBg] = Rgb(38, 31, 64, 1.0f);            // Dark purple
        colors[(int)ImGuiCol.TitleBgActive] = Rgb(51, 38, 89, 1.0f);      // Medium purple
        colors[(int)ImGuiCol.TitleBgCollapsed] = Rgb(31, 26, 51, 0.75f);  // Very dark purple

        // Frames (inputs, etc)
        colors[(int)ImGuiCol.FrameBg] = Rgb(38, 31, 56, 0.9f);            // Medium purple
        colors[(int)ImGuiCol.FrameBgHovered] = Rgb(64, 51, 89, 1.0f);     // Lighter purple
        colors[(int)ImGuiCol.FrameBgActive] = Rgb(77, 64, 102, 1.0f);     // Even lighter purple

        // Buttons (dark with orange on hover)
        colors[(int)ImGuiCol.Button] = Rgb(38, 31, 64, 1.0f);             // Dark purple
        colors[(int)ImGuiCol.ButtonHovered] = Rgb(64, 51, 89, 1.0f);      // Lighter purple
        colors[(int)ImGuiCol.ButtonActive] = Rgb(128, 77, 3, 0.8f);       // Dark orange

        // Headers
        colors[(int)ImGuiCol.Header] = Rgb(51, 38, 77, 1.0f);             // Medium purple
        colors[(int)ImGuiCol.HeaderHovered] = Rgb(230, 128, 26, 0.6f);    // Orange
        colors[(int)ImGuiCol.HeaderActive] = Rgb(128, 77, 3, 0.8f);       // Dark orange

        // Tabs
        colors[(int)ImGuiCol.Tab] = Rgb(38, 31, 64, 1.0f);                          // Dark purple (inactive)
        colors[(int)ImGuiCol.TabHovered] = Rgb(230, 128, 26, 0.8f);                 // Orange (hovered)
        colors[(int)ImGuiCol.TabSelected] = Rgb(128, 77, 3, 1.0f);                  // Orange (active/selected)
        colors[(int)ImGuiCol.TabDimmed] = Rgb(31, 26, 51, 1.0f);                    // Very dark purple (unfocused)
        colors[(int)ImGuiCol.TabDimmedSelected] = Rgb(128, 77, 26, 0.8f);           // Dimmed orange (unfocused selected)
        colors[(int)ImGuiCol.TabDimmedSelectedOverline] = Rgb(230, 128, 26, 1.0f);  // Orange underline
        colors[(int)ImGuiCol.TabSelectedOverline] = Rgb(230, 128, 26, 1.0f);        // Orange underline (focused)

        // Checkmarks and sliders (orange)
        colors[(int)ImGuiCol.CheckMark] = Rgb(255, 179, 51, 1.0f);        // Light orange
        colors[(int)ImGuiCol.SliderGrab] = Rgb(230, 128, 26, 1.0f);       // Orange
        colors[(int)ImGuiCol.SliderGrabActive] = Rgb(255, 166, 51, 1.0f); // Lighter orange

        // Scrollbar
        colors[(int)ImGuiCol.ScrollbarBg] = Rgb(26, 20, 38, 0.9f);            // Dark purple
        colors[(int)ImGuiCol.ScrollbarGrab] = Rgb(64, 51, 89, 1.0f);          // Medium purple
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = Rgb(89, 71, 115, 1.0f);  // Lighter purple
        colors[(int)ImGuiCol.ScrollbarGrabActive] = Rgb(230, 128, 26, 1.0f);  // Orange

        // Separators (orange)
        colors[(int)ImGuiCol.Separator] = Rgb(230, 128, 26, 0.5f);        // Orange
        colors[(int)ImGuiCol.SeparatorHovered] = Rgb(230, 128, 26, 0.8f); // Orange
        colors[(int)ImGuiCol.SeparatorActive] = Rgb(255, 153, 51, 1.0f);  // Lighter orange

        // Resize grip
        colors[(int)ImGuiCol.ResizeGrip] = Rgb(230, 128, 26, 0.3f);        // Orange
        colors[(int)ImGuiCol.ResizeGripHovered] = Rgb(230, 128, 26, 0.6f); // Orange
        colors[(int)ImGuiCol.ResizeGripActive] = Rgb(255, 153, 51, 1.0f);  // Lighter orange

        // The original code re-set these three after the color block - preserved verbatim
        // (final values win: overrides the Rounding/Spacing block above).
        style.FrameRounding = 3.0f;
        style.WindowPadding = new System.Numerics.Vector2(10, 10);
        style.FramePadding = new System.Numerics.Vector2(5, 3);
        style.ItemSpacing = new System.Numerics.Vector2(8, 4);
    }
}
