using Hexa.NET.ImGui;

namespace NFMWorld.UI;

/// <summary>The main menu's navy and blue palette, applied after ImGui context creation.</summary>
public static class ImGuiTheme
{
    public static void Apply()
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding = 6;
        style.ChildRounding = 4;
        style.FrameRounding = 6;
        style.GrabRounding = 4;
        style.PopupRounding = 6;
        style.ScrollbarRounding = 6;
        style.TabRounding = 4;
        style.WindowPadding = new Vector2(10, 10);
        style.FramePadding = new Vector2(5, 3);
        style.ItemSpacing = new Vector2(8, 4);
        style.WindowBorderSize = 1;
        style.ChildBorderSize = 1;
        style.PopupBorderSize = 1;
        style.FrameBorderSize = 1;

        static Vector4 Rgb(int r, int g, int b, float a = 1) => new(r / 255f, g / 255f, b / 255f, a);

        var panel = Rgb(12, 22, 42, 0.96f);
        var inset = Rgb(7, 14, 28, 0.94f);
        var surface = Rgb(22, 42, 80);
        var hovered = Rgb(55, 90, 150);
        var active = Rgb(70, 110, 180);
        var blue = Rgb(128, 170, 255);
        var highlight = Rgb(205, 227, 255);
        var colors = style.Colors;

        colors[(int)ImGuiCol.WindowBg] = panel;
        colors[(int)ImGuiCol.ChildBg] = inset;
        colors[(int)ImGuiCol.PopupBg] = panel;
        colors[(int)ImGuiCol.MenuBarBg] = surface;
        colors[(int)ImGuiCol.Border] = Rgb(128, 170, 255, 0.76f);
        colors[(int)ImGuiCol.BorderShadow] = Rgb(0, 0, 0, 0.25f);
        colors[(int)ImGuiCol.Text] = Rgb(185, 211, 255);
        colors[(int)ImGuiCol.TextDisabled] = Rgb(104, 127, 164);
        colors[(int)ImGuiCol.TitleBg] = Rgb(16, 30, 56);
        colors[(int)ImGuiCol.TitleBgActive] = surface;
        colors[(int)ImGuiCol.TitleBgCollapsed] = Rgb(12, 22, 42, 0.8f);
        colors[(int)ImGuiCol.FrameBg] = surface;
        colors[(int)ImGuiCol.FrameBgHovered] = hovered;
        colors[(int)ImGuiCol.FrameBgActive] = active;
        colors[(int)ImGuiCol.Button] = surface;
        colors[(int)ImGuiCol.ButtonHovered] = hovered;
        colors[(int)ImGuiCol.ButtonActive] = active;
        colors[(int)ImGuiCol.Header] = surface;
        colors[(int)ImGuiCol.HeaderHovered] = hovered;
        colors[(int)ImGuiCol.HeaderActive] = active;
        colors[(int)ImGuiCol.Tab] = surface;
        colors[(int)ImGuiCol.TabHovered] = hovered;
        colors[(int)ImGuiCol.TabSelected] = active;
        colors[(int)ImGuiCol.TabDimmed] = inset;
        colors[(int)ImGuiCol.TabDimmedSelected] = surface;
        colors[(int)ImGuiCol.TabSelectedOverline] = blue;
        colors[(int)ImGuiCol.TabDimmedSelectedOverline] = Rgb(128, 170, 255, 0.5f);
        colors[(int)ImGuiCol.CheckMark] = highlight;
        colors[(int)ImGuiCol.SliderGrab] = blue;
        colors[(int)ImGuiCol.SliderGrabActive] = highlight;
        colors[(int)ImGuiCol.ScrollbarBg] = inset;
        colors[(int)ImGuiCol.ScrollbarGrab] = Rgb(46, 68, 106);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = hovered;
        colors[(int)ImGuiCol.ScrollbarGrabActive] = blue;
        colors[(int)ImGuiCol.Separator] = Rgb(128, 170, 255, 0.35f);
        colors[(int)ImGuiCol.SeparatorHovered] = blue;
        colors[(int)ImGuiCol.SeparatorActive] = highlight;
        colors[(int)ImGuiCol.ResizeGrip] = Rgb(128, 170, 255, 0.25f);
        colors[(int)ImGuiCol.ResizeGripHovered] = Rgb(128, 170, 255, 0.65f);
        colors[(int)ImGuiCol.ResizeGripActive] = highlight;
        colors[(int)ImGuiCol.TableHeaderBg] = surface;
        colors[(int)ImGuiCol.TableBorderStrong] = Rgb(128, 170, 255, 0.45f);
        colors[(int)ImGuiCol.TableBorderLight] = Rgb(128, 170, 255, 0.18f);
        colors[(int)ImGuiCol.TableRowBg] = Rgb(0, 0, 0, 0);
        colors[(int)ImGuiCol.TableRowBgAlt] = Rgb(128, 170, 255, 0.04f);
        colors[(int)ImGuiCol.TextLink] = blue;
        colors[(int)ImGuiCol.TextSelectedBg] = Rgb(55, 90, 150, 0.8f);
        colors[(int)ImGuiCol.DragDropTarget] = highlight;
        colors[(int)ImGuiCol.NavCursor] = highlight;
        colors[(int)ImGuiCol.NavWindowingHighlight] = Rgb(205, 227, 255, 0.7f);
        colors[(int)ImGuiCol.NavWindowingDimBg] = Rgb(3, 8, 20, 0.45f);
        colors[(int)ImGuiCol.ModalWindowDimBg] = Rgb(3, 8, 20, 0.55f);
    }
}
