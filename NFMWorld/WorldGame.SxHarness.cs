using System.Globalization;
using System.Numerics;
using System.Text.Json;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
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
            "garage" => new FixturePhase(GarageFixture),
            "stage-select" => new FixturePhase(StageSelectFixture),
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
                node.IsDisplayed,
                node.ScrollableWidth,
                node.ScrollableHeight
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
                case "assert-no-horizontal-scroll":
                    var scrolled = Find(step.GetProperty("target").GetString()!);
                    if (scrolled.ScrollableWidth > 0.5f)
                        throw new InvalidOperationException(
                            $"Expected no horizontal overflow, found {scrolled.ScrollableWidth}: {step}"
                        );
                    break;
                case "assert-size":
                    var sized = Find(step.GetProperty("target").GetString()!);
                    if (step.TryGetProperty("width", out var expectedWidth)
                        && Math.Abs(sized.LayoutBorderSize.X - expectedWidth.GetSingle() * scale) > 1)
                        throw new InvalidOperationException(
                            $"Expected width {expectedWidth.GetSingle() * scale}, got {sized.LayoutBorderSize.X}: {step}"
                        );
                    if (step.TryGetProperty("height", out var expectedHeight)
                        && Math.Abs(sized.LayoutBorderSize.Y - expectedHeight.GetSingle() * scale) > 1)
                        throw new InvalidOperationException(
                            $"Expected height {expectedHeight.GetSingle() * scale}, got {sized.LayoutBorderSize.Y}: {step}"
                        );
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

    /// <summary>Mounts a menu tree with no gameplay behind it, for layout captures.</summary>
    private sealed class FixturePhase(Func<Node?> build) : NFMWorld.Gameplay.BasePhase
    {
        public override Node? BuildUi() => build();
    }

    private static Node GarageFixture()
    {
        var garage = UiState.Garage;
        CarStatsViewModel Car(string name, string collection, string fileName = "")
            => new(fileName.Length > 0 ? fileName : name, name, collection, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        CarStatsViewModel WithStats(CarStatsViewModel car) => car with
        {
            TopSpeed = 0.82,
            Acceleration = 0.64,
            Handling = 0.71,
            PowerSave = 0.55,
            Strength = 0.48,
            MaxHealth = 0.9,
            Stunting = 0.76,
            Hypergliding = 0.38,
            Abing = 0.6
        };

        var local = new[] { Car("Bullet", "NFMM"), Car("Monster", "NFMM"), Car("Mega", "NFMM"), Car("F1", "NFMM") };
        var user = new[] { Car("My Custom Car", "User"), Car("Prototype", "User") };
        var online = new List<CarStatsViewModel>
        {
            Car("Sunset Racer", "Online", "archive/" + new string('a', 64)),
            Car("Night Runner", "Online", "archive/" + new string('b', 64)),
            Car("Rally Boss", "Online", "archive/" + new string('c', 64)),
            Car("A Very Long Public Car Name That Should Not Move The List Sideways", "Online", "archive/" + new string('d', 64))
        };
        for (var i = 0; i < 16; i++)
            online.Add(Car($"Public Car {i + 1}", "Online", "archive/" + new string((char)('e' + i % 10), 64)));
        var onlineCars = online.ToArray();

        garage.Collections.Value = [new CarCollectionViewModel("NFMM", local), new CarCollectionViewModel("User", user)];
        garage.CurrentCollection.Value = "NFMM";
        garage.CurrentCar.Value = WithStats(local[0]);
        garage.OnlineSource.Value = false;
        garage.OnlineStatus.Value = "";

        return GarageView.Build(new GarageView.Actions(
            CarSelected: (collection, fileName) =>
            {
                var cars = collection == "Online" ? onlineCars : collection == "User" ? user : local;
                if (cars.FirstOrDefault(car => car.FileName == fileName) is { } picked)
                    garage.CurrentCar.Value = WithStats(picked);
                garage.CurrentCollection.Value = collection;
            },
            CollectionSelected: collection => garage.CurrentCollection.Value = collection,
            Confirm: () => { },
            Back: () => { },
            SourceSelected: isOnline =>
            {
                garage.OnlineSource.Value = isOnline;
                garage.OnlineStatus.Value = isOnline ? "3 public car(s). Select one to preview it." : "";
                garage.Collections.Value = isOnline
                    ? [new CarCollectionViewModel("Online", onlineCars)]
                    : [new CarCollectionViewModel("NFMM", local), new CarCollectionViewModel("User", user)];
            },
            OnlineSearch: _ => garage.OnlineStatus.Value =
                "Could not search the archive: Connection refused (localhost:5288) while requesting assets/search?type=Car"
        ));
    }

    private static Node StageSelectFixture()
    {
        var state = new StageSelectState();
        var local = new[]
        {
            new StageOption("nfmm/stage1", "Stage 1"),
            new StageOption("nfmm/stage2", "Stage 2"),
            new StageOption("nfmm/stage3", "Stage 3"),
            new StageOption("nfmm/stage4", "Stage 4"),
            new StageOption("nfmm/stage5", "Stage 5"),
            new StageOption("nfmm/stage6", "Stage 6")
        };
        var online = new[]
        {
            new StageOption("archive/" + new string('a', 64), "Sunset Loop", "maxine"),
            new StageOption("archive/" + new string('b', 64), "Night Run", "uwx"),
            new StageOption("archive/" + new string('c', 64), "Harbour Sprint", "maxine"),
            new StageOption(
                "archive/" + new string('d', 64),
                "A Very Long Public Stage Name That Should Not Move The List Sideways",
                "a-rather-long-author-name"
            )
        }.Concat(Enumerable.Range(1, 18).Select(i => new StageOption(
            "archive/" + new string((char)('e' + i % 10), 64),
            $"Public Stage {i}",
            "another-author"
        ))).ToArray();

        state.Collections.Value = ["nfmm", "user"];
        state.Collection.Value = "nfmm";
        state.Stages.Value = local;
        state.Selected.Value = local[0].Id;
        state.ConfirmEnabled.Value = true;

        return StageSelectView.Build(
            state,
            new StageSelectView.Actions(
                CollectionSelected: collection =>
                {
                    state.Collection.Value = collection;
                    state.Stages.Value = local
                        .Where(stage => stage.Id.StartsWith(collection + "/", StringComparison.Ordinal))
                        .ToArray();
                },
                StageSelected: id => state.Selected.Value = id,
                SearchChanged: query =>
                {
                    if (state.OnlineSource.Value)
                        return;

                    var search = query.Trim();
                    state.Stages.Value = local
                        .Where(stage => search.Length == 0 || stage.Label.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                },
                Confirm: () => { },
                Back: () => { },
                SourceSelected: isOnline =>
                {
                    state.OnlineSource.Value = isOnline;
                    state.Stages.Value = isOnline ? online : local;
                    state.Selected.Value = "";
                    state.Status.Value = isOnline ? "3 public stage(s). Pick one to preview it." : "";
                    state.Busy.Value = false;
                },
                OnlineSearch: _ => state.Status.Value =
                    "Could not search the archive: Connection refused (localhost:5288) while requesting assets/search?type=Stage"
            )
        );
    }
}
