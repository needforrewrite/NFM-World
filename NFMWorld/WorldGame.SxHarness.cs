using System.Globalization;
using System.Numerics;
using System.Text.Json;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI;
using NFMWorld.Util;
using NFMWorldLibrary;

namespace NFMWorld;

public partial class WorldGame
{
    private static void RunSxHarness(string[] args)
    {
        var scriptPath = args.Single(arg => arg.StartsWith("--sx-harness=", StringComparison.Ordinal))[13..];
        using var script = JsonDocument.Parse(File.ReadAllText(scriptPath));
        var config = script.RootElement;
        var width = config.TryGetProperty("width", out var w) ? w.GetInt32() : 1280;
        var height = config.TryGetProperty("height", out var h) ? h.GetInt32() : 720;
        var scaleOverride = args.FirstOrDefault(arg => arg.StartsWith("--sx-scale=", StringComparison.Ordinal));
        var scale = scaleOverride is not null ? float.Parse(scaleOverride[11..], CultureInfo.InvariantCulture)
            : config.TryGetProperty("scale", out var s) ? s.GetSingle() : height / 720f;
        if (width is < 320 or > 7680 || height is < 240 or > 4320 || !float.IsFinite(scale) || scale is < 0.25f or > 4f)
            throw new ArgumentOutOfRangeException(nameof(args), "Unsupported harness dimensions or scale.");
        var output = Path.GetFullPath(
            config.GetProperty("output").GetString()!,
            Path.GetDirectoryName(Path.GetFullPath(scriptPath))!
        );
        if (scaleOverride is not null)
            output = Path.Combine(output, "scale-" + scale.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(output);
        VFS.MountDirectory(AppContext.BaseDirectory);
        VFS.MountWriteDestination(Path.Combine(output, "sandbox"));
        using var game = new WorldGame();
        GameSparker.Game = game;
        GameSparker.GraphicsDevice = game._device;
        game._device.Swapchain.Resize(width, height, 1);
        using var graphics = new AposRenderer(game._device);
        TheGraphics.G.Scale = scale;
        Effects.Initialize(game._device);
        using var ui = new WorldXaml.UI.Yoga.UiRenderer();
        GameSparker.UiRenderer = ui;
        var viewName = config.TryGetProperty("view", out var view) ? view.GetString() : "stage-editor";
        using NFMWorld.Gameplay.BasePhase phase = viewName switch
        {
            "stage-editor" => new StageEditorPhase(game._device),
            "model-editor" => new ModelEditorPhase(game._device),
            _ => throw new InvalidOperationException($"Unknown harness view: {viewName}")
        };
        if (config.TryGetProperty("pieceCount", out var count))
        {
            if (phase is not StageEditorPhase)
                throw new InvalidOperationException("pieceCount requires the stage-editor view.");
            var tab = new StageEditorTab { TabName = "Layout fixture", Stage = new EditorStage() };
            var rad = new NFMWorldLibrary.Rad.Rad3d([], false, "fixture/road");
            for (var i = 0; i < count.GetInt32(); i++)
            {
                var obj = NFMWorldLibrary.Backend.StageObject.CreateDefaultObject(
                    rad,
                    new f64Vector3(0, 250, 0),
                    NFMWorldLibrary.FixedMath.f64Euler.Identity
                );
                tab.ScenePieces.Add(new StagePieceInstance(obj, i));
            }

            tab.ActivePieceId = 0;
            tab.SelectedPieceIds.Add(0);
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags
                .NonPublic;
            ((List<StageEditorTab>)typeof(StageEditorPhase).GetField("_tabs", fields)!.GetValue(phase)!).Add(tab);
            typeof(StageEditorPhase).GetField("_activeTabIndex", fields)!.SetValue(phase, 0);
        }

        ui.ShowPhase(phase);
        using var target = game._device.CreateRenderTarget(new RenderTargetDesc(width, height, TextureFormat.Rgba8));
        var pixels = new byte[checked(width * height * 4)];

        IEnumerable<Component> Nodes(Component parent)
        {
            yield return parent;
            foreach (var child in parent.VisualChildren.OfType<Component>())
            foreach (var node in Nodes(child))
                yield return node;
        }

        Component Find(string name) => Nodes(ui.ActiveRoot!).Single(node => node.Name == name);

        Vector2 Point(JsonElement step)
        {
            if (step.TryGetProperty("target", out var name))
            {
                var node = Find(name.GetString()!);
                return node.LayoutBorderPosition + node.LayoutBorderSize / 2;
            }

            return new Vector2(step.GetProperty("x").GetSingle(), step.GetProperty("y").GetSingle());
        }

        void Render()
        {
            NFMWorld.Reactor.Sx.Renderer.Flush();
            var cb = game._device.AcquireCommandBuffer();
            cb.SetRenderTarget(target);
            cb.SetViewport(new Viewport(0, 0, width, height));
            cb.SetScissorRect(new ScissorRect(0, 0, width, height));
            cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0.12f, 0.16f, 0.22f, 1));
            graphics.BeginFrame(cb);
            ui.Render();
            graphics.Render();
            game._device.Submit(cb);
        }

        void Save(string name)
        {
            if (Path.GetFileName(name) != name)
                throw new InvalidOperationException("Capture names must be plain file names.");
            game._device.ReadTexture(target.ColorTexture, 0, 0, width, height, pixels);
            if (game._device.HasBottomLeftFramebufferOrigin)
            {
                var stride = width * 4;
                var row = new byte[stride];
                for (var y = 0; y < height / 2; y++)
                {
                    pixels.AsSpan(y * stride, stride).CopyTo(row);
                    pixels.AsSpan((height - 1 - y) * stride, stride).CopyTo(pixels.AsSpan(y * stride, stride));
                    row.CopyTo(pixels.AsSpan((height - 1 - y) * stride, stride));
                }
            }

            using (var stream = File.Create(Path.Combine(output, name + ".png")))
                PngCodec.Encode(stream, pixels, width, height);
            var layout = Nodes(ui.ActiveRoot!).Select(node => new
            {
                node.Name,
                Type = node.GetType().Name,
                X = node.LayoutBorderPosition.X,
                Y = node.LayoutBorderPosition.Y,
                Width = node.LayoutBorderSize.X,
                Height = node.LayoutBorderSize.Y,
                node.IsHovered,
                node.IsDisplayed
            });
            File.WriteAllText(
                Path.Combine(output, name + ".json"),
                JsonSerializer.Serialize(layout, new JsonSerializerOptions
                {
                    WriteIndented = true
                }
                )
            );
            Console.WriteLine($"SX HARNESS: {name} ({width}x{height}, scale {scale.ToString(CultureInfo.InvariantCulture)})");
        }

        Render();
        foreach (var step in config.GetProperty("steps").EnumerateArray())
        {
            switch (step.GetProperty("action").GetString())
            {
                case "click":
                case "hover":
                    var point = Point(step);
                    ui.HandleMouseMoved((int)point.X, (int)point.Y, MouseButtons.None, false, false, false);
                    NFMWorld.Reactor.Sx.Renderer.Flush();
                    if (step.GetProperty("action").GetString() == "click")
                    {
                        ui.HandleMousePressed(
                            (int)point.X,
                            (int)point.Y,
                            MouseButton.Primary,
                            MouseButtons.Primary,
                            false,
                            false,
                            false
                        );
                        ui.HandleMouseReleased(
                            (int)point.X,
                            (int)point.Y,
                            MouseButton.Primary,
                            MouseButtons.None,
                            false,
                            false,
                            false
                        );
                    }

                    break;
                case "text":
                    foreach (var character in step.GetProperty("value").GetString()!)
                        ui.HandleKeyTyped(character);
                    break;
                case "key":
                    var key = Enum.Parse<Key>(step.GetProperty("value").GetString()!);
                    ui.HandleKeyPressed(key, default);
                    ui.HandleKeyReleased(key, default);
                    break;
                case "assert-input-aligned":
                    var field = Find(step.GetProperty("target").GetString()!);
                    var otherField = Find(step.GetProperty("anchor").GetString()!);
                    var input = field.VisualChildren.OfType<NFMWorld.Reactor.TextInput>().Single();
                    var otherInput = otherField.VisualChildren.OfType<NFMWorld.Reactor.TextInput>().Single();
                    if (Math.Abs(input.LayoutBorderPosition.X - otherInput.LayoutBorderPosition.X) > 1
                        || Math.Abs(input.LayoutBorderSize.X - otherInput.LayoutBorderSize.X) > 1)
                        throw new InvalidOperationException($"Inputs do not align: {step}");
                    break;
                case "assert-min-height":
                    if (Find(step.GetProperty("target").GetString()!).LayoutBorderSize.Y < step.GetProperty("height").GetSingle()
                        * scale)
                        throw new InvalidOperationException($"Control was squeezed: {step}");
                    break;
                case "assert-hover":
                    if (!Find(step.GetProperty("target").GetString()!).IsHovered)
                        throw new InvalidOperationException($"Expected hovered target: {step}");
                    break;
                case "assert-aligned":
                    var popup = Find(step.GetProperty("target").GetString()!);
                    var anchor = Find(step.GetProperty("anchor").GetString()!);
                    if (Math.Abs(popup.LayoutBorderPosition.X - anchor.LayoutBorderPosition.X) > 1)
                        throw new InvalidOperationException(
                            $"Popup X={popup.LayoutBorderPosition.X}, anchor X={anchor.LayoutBorderPosition.X}"
                        );
                    break;
                case "assert-visible":
                    if (!Find(step.GetProperty("target").GetString()!).IsDisplayed)
                        throw new InvalidOperationException($"Expected visible target: {step}");
                    break;
                case "scroll":
                    var scrollPoint = Point(step);
                    ui.HandleMouseScrolled(
                        (int)scrollPoint.X,
                        (int)scrollPoint.Y,
                        step.GetProperty("delta").GetInt32(),
                        MouseButtons.None,
                        false,
                        false,
                        false
                    );
                    break;
                case "capture":
                    break;
                default:
                    throw new InvalidOperationException($"Unknown harness action: {step}");
            }

            Render();
            if (step.TryGetProperty("capture", out var capture))
                Save(capture.GetString()!);
        }

        GameSparker.UiRenderer = null;
    }
}
