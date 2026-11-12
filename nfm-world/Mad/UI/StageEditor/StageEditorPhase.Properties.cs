using System.Globalization;
using NFMWorld.Audio;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Platform.SDL3;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorldLibrary;
using SignalsDotnet;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    /// <summary>The docked bottom panel: the parts library, plus one tab per property group.</summary>
    private enum BottomTab
    {
        Parts,
        Stage,
        Atmosphere,
        Ground,
        Scenery,
        Music,
        Physics,
        TestDrive
    }

    private BottomTab _bottomTab = BottomTab.Parts;

    private enum MusicTarget
    {
        Track,
        Remaster
    }

    private const string MusicNoneEntry = "(none)";

    private MusicTarget _musicTarget = MusicTarget.Track;
    private string[]? _musicEntries;
    private IRadicalMusic? _previewMusic;
    private string? _previewMusicPath;
    private IRadicalMusic? _musicBeforePreview;

    private void SelectBottomTab(BottomTab tab)
    {
        _bottomTab = tab;
        Touch();
    }

    private Node BottomPanel()
        => Sx.View(
            name: () => "Stage editor bottom panel",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                bottom: 0,
                height: BottomPanelHeight,
                flexDirection: FlexDirection.Column,
                backgroundColor: new Color(8, 16, 32, 235),
                borderTop: 1,
                borderColor: new Color(128, 170, 255, 100)
            ),
            children: [BottomTabBar(), BottomContent()]
        );

    /// <summary>Same shape as the model editor's bottom dock: flush tab strip, then padded content.</summary>
    private Node BottomTabBar()
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Stretch,
                flexWrap: Wrap.Wrap,
                flexShrink: 0,
                paddingLeft: 10,
                paddingRight: 10,
                gapColumn: 2,
                gapRow: 2,
                backgroundColor: new Color(7, 14, 28, 180),
                borderBottom: 1,
                borderColor: new Color(128, 170, 255, 45)
            ),
            children:
            [
                BottomTabButton(BottomTab.Parts, "PARTS"),
                BottomTabButton(BottomTab.Stage, "STAGE"),
                BottomTabButton(BottomTab.Atmosphere, "ATMOSPHERE"),
                BottomTabButton(BottomTab.Ground, "GROUND"),
                BottomTabButton(BottomTab.Scenery, "SCENERY"),
                BottomTabButton(BottomTab.Music, "MUSIC"),
                BottomTabButton(BottomTab.Physics, "PHYSICS"),
                BottomTabButton(BottomTab.TestDrive, "TEST DRIVE")
            ]
        );

    private Node BottomTabButton(BottomTab tab, string label)
    {
        var hovered = new Signal<bool>(false);
        return Sx.View(
            style: () =>
            {
                _ = _uiRevision.Value;
                return Sx.Styles(
                    position: Position.Relative,
                    paddingTop: 6,
                    paddingBottom: 6,
                    paddingLeft: 10,
                    paddingRight: 10,
                    alignItems: Align.Center,
                    justifyContent: Justify.Center,
                    backgroundColor: _bottomTab == tab ? new Color(22, 42, 80, 220)
                        : hovered.Value ? new Color(22, 42, 80, 110)
                        : new Color(0, 0, 0, 0)
                );
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: _ => SelectBottomTab(tab),
            children: EditorText(
                style: () =>
                {
                    var active = _bottomTab == tab;
                    return Sx.TextStyles(
                        fontSize: 10,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: active ? new Color(185, 211, 255) : new Color(255, 255, 255, 150)
                    );
                },
                text: () => label
            )
        );
    }

    private Node BottomContent()
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexGrow: 1,
                minHeight: 0,
                paddingTop: 6,
                paddingBottom: 6,
                paddingLeft: 10,
                paddingRight: 10
            ),
            children:
            [
                EditorShow(
                    when: () => _bottomTab == BottomTab.Parts,
                    then: () => PartsContent(),
                    otherwise: () => Sx.For(
                        // Keyed by the document too: switching stage tabs rebuilds the form.
                        each: () =>
                        {
                            _ = _uiRevision.Value;
                            IReadOnlyList<(BottomTab Tab, StageEditorTab? Document)> items =
                                [(Tab: _bottomTab, Document: ActiveTab)];
                            return items;
                        },
                        row: (key, _) => PropertySections(key.Tab, key.Document)
                    )
                )
            ]
        );

    private Node PropertySections(BottomTab tab, StageEditorTab? tabDocument)
    {
        if (tabDocument is not { } document)
            return Sx.View(style: () => Sx.Styles(display: Display.Contents));

        return Sx.View(
            style: () => Sx.Styles(
                flexGrow: 1,
                minHeight: 0,
                overflow: Overflow.Scroll,
                flexDirection: FlexDirection.Row,
                flexWrap: Wrap.Wrap,
                alignContent: Align.FlexStart,
                alignItems: Align.FlexStart,
                gapColumn: 20,
                gapRow: 8,
                paddingTop: 2,
                paddingRight: 8
            ),
            children: tab switch
            {
                BottomTab.Stage => StageSections(document),
                BottomTab.Atmosphere => AtmosphereSections(document),
                BottomTab.Ground => GroundSections(document),
                BottomTab.Scenery => ScenerySections(document),
                BottomTab.Music => MusicSections(document),
                BottomTab.Physics => PhysicsSections(document),
                BottomTab.TestDrive => TestDriveSections(document),
                _ => Array.Empty<object?>()
            }
        );
    }

    // ── Sections ───────────────────────────────────────────────────────

    private object?[] StageSections(StageEditorTab tab) =>
    [
        PropertySection(
            "STAGE",
            250,
            PropertyRow(
                "Name",
                EditorField(() => tab.TabName, value => { tab.TabName = value; StagePropertyChanged(); }, () => "Stage name", 180),
                40
            ),
            PropertyRow(
                "Laps",
                PropertyNumberField(
                    () => tab.Stage?.Nlaps ?? 3,
                    value =>
                    {
                        if (tab.Stage is { } stage)
                            stage.Nlaps = (ushort)Math.Clamp((int)value, 1, 99);
                        StagePropertyChanged();
                    }
                ),
                40
            ),
            PropertyCheckbox(
                "Aerial stage",
                () => tab.Aerial,
                () => { tab.Aerial = !tab.Aerial; StagePropertyChanged(); }
            )
        )
    ];

    private object?[] AtmosphereSections(StageEditorTab tab) =>
    [
        PropertySection(
            "SKY",
            200,
            ColorField("Sky", () => tab.SkyColor, color => { tab.SkyColor = color; StageSkyChanged(); })
        ),
        PropertySection(
            "FOG",
            236,
            ColorField("Fog", () => tab.FogColor, color => { tab.FogColor = color; StageFogChanged(); }),
            PropertyRow(
                "Density",
                PropertyOptionalIntField(
                    () => tab.FogDensity,
                    value =>
                    {
                        tab.FogDensity = value is { } density ? Math.Clamp(density, 0, 14) : null;
                        StagePropertyChanged();
                    }
                )
            ),
            PropertyRow(
                "Fade from",
                PropertyIntField(() => tab.FadeFrom, value => { tab.FadeFrom = value; StagePropertyChanged(); })
            )
        ),
        PropertySection(
            "LIGHTING",
            176,
            PropertyCheckbox(
                "Lights on",
                () => tab.LightsOn,
                () => { tab.LightsOn = !tab.LightsOn; StagePropertyChanged(); }
            ),
            PropertyRow(
                "Dir X",
                PropertyNumberField(
                    () => tab.LightDirection.X,
                    value => SetLightDirection(tab, value, static (d, v) => new Vector3(v, d.Y, d.Z))
                )
            ),
            PropertyRow(
                "Dir Y",
                PropertyNumberField(
                    () => tab.LightDirection.Y,
                    value => SetLightDirection(tab, value, static (d, v) => new Vector3(d.X, v, d.Z))
                )
            ),
            PropertyRow(
                "Dir Z",
                PropertyNumberField(
                    () => tab.LightDirection.Z,
                    value => SetLightDirection(tab, value, static (d, v) => new Vector3(d.X, d.Y, v))
                )
            )
        )
    ];

    private void SetLightDirection(StageEditorTab tab, float value, Func<Vector3, float, Vector3> change)
    {
        tab.LightDirection = change(tab.LightDirection, value);
        StagePropertyChanged();
    }

    private object?[] GroundSections(StageEditorTab tab) =>
    [
        PropertySection(
            "GROUND",
            236,
            ColorField("Ground", () => tab.GroundColor, color => { tab.GroundColor = color; StageGroundChanged(); })
        ),
        PropertySection(
            "GROUND POLYS",
            236,
            PropertyCheckbox(
                "Enabled",
                () => tab.PolysEnabled,
                () =>
                {
                    tab.PolysEnabled = !tab.PolysEnabled;
                    StagePolysChanged();
                }
            ),
            ColorField("Polys", () => tab.PolysColor, color => { tab.PolysColor = color; StagePolysChanged(); })
        ),
        PropertySection(
            "SNAP INTENSITY",
            200,
            SnapField(tab, "R", 0),
            SnapField(tab, "G", 1),
            SnapField(tab, "B", 2),
            EditorText(
                style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 110)),
                layout: () => Sx.Styles(width: 200, flexShrink: 0),
                text: () => "Percent added to each colour channel."
            )
        ),
        PropertySection(
            "TEXTURE",
            140,
            TextureField(tab, 0, "A"),
            TextureField(tab, 1, "B"),
            TextureField(tab, 2, "C"),
            TextureField(tab, 3, "Blend")
        )
    ];

    private Node TextureField(StageEditorTab tab, int index, string label)
        => PropertyRow(
            label,
            PropertyIntField(
                () => tab.Texture is { } texture && index < texture.Length ? texture[index] : 0,
                value =>
                {
                    var texture = tab.Texture is { Length: 4 } existing
                        ? (int[])existing.Clone()
                        : new[] { 0, 0, 0, 50 };
                    texture[index] = value;
                    tab.Texture = texture;
                    StagePolysChanged();
                }
            ),
            40
        );

    private object?[] ScenerySections(StageEditorTab tab) =>
    [
        PropertySection(
            "CLOUDS",
            236,
            PropertyCheckbox(
                "Clouds",
                () => tab.CloudsEnabled,
                () => { tab.CloudsEnabled = !tab.CloudsEnabled; StageSceneryChanged(); }
            ),
            ColorField("Cloud", () => tab.CloudsColor, color => { tab.CloudsColor = color; StageSceneryChanged(); }),
            PropertyRow(
                "Param",
                PropertyIntField(() => tab.CloudsParam4, value => { tab.CloudsParam4 = value; StageSceneryChanged(); })
            ),
            PropertyRow(
                "Height",
                PropertyIntField(() => tab.CloudsHeight, value => { tab.CloudsHeight = value; StageSceneryChanged(); })
            ),
            PropertyRow(
                "Coverage",
                PropertyNumberField(
                    () => tab.CloudCoverage,
                    value => { tab.CloudCoverage = value; StageSceneryChanged(); }
                )
            )
        ),
        PropertySection(
            "MOUNTAINS",
            236,
            PropertyCheckbox(
                "Mountains",
                () => tab.MountainsEnabled,
                () => { tab.MountainsEnabled = !tab.MountainsEnabled; StageSceneryChanged(); }
            ),
            PropertyRow(
                "Seed",
                PropertyIntField(() => tab.MountainsSeed, value => { tab.MountainsSeed = value; StageSceneryChanged(); })
            ),
            PropertyRow(
                "Coverage",
                PropertyNumberField(
                    () => tab.MountainCoverage ?? 1f,
                    value => { tab.MountainCoverage = value; StageSceneryChanged(); }
                )
            )
        )
    ];

    private object?[] MusicSections(StageEditorTab tab) =>
    [
        PropertySection(
            "SOUNDTRACK",
            640,
            Sx.View(
                style: () => Sx.Styles(
                    flexDirection: FlexDirection.Row,
                    alignItems: Align.Center,
                    gapColumn: 4,
                    flexShrink: 0
                ),
                children:
                [
                    ModeButton(
                        () => "TRACK",
                        () => _musicTarget == MusicTarget.Track,
                        () => SelectMusicTarget(MusicTarget.Track)
                    ),
                    ModeButton(
                        () => "REMASTER",
                        () => _musicTarget == MusicTarget.Remaster,
                        () => SelectMusicTarget(MusicTarget.Remaster)
                    ),
                    Sx.View(style: () => Sx.Styles(width: 8, flexShrink: 0)),
                    Sx.View(
                        style: () => Sx.Styles(width: 280, flexShrink: 0),
                        children: Controls.Dropdown(
                            options: MusicDropdownEntries,
                            value: Observe(() => MusicEntryIndex(tab)),
                            onSelect: index => SetMusicTargetPath(tab, MusicDropdownEntries()[index]),
                            unselectedLabel: "(none)",
                            width: 280,
                            compact: true
                        )
                    ),
                    SmallButton(() => "IMPORT", ImportMusic),
                    SmallButton(() => "PREVIEW", PreviewSelectedMusic),
                    SmallButton(() => "STOP", () => StopMusicPreview())
                ]
            ),
            EditorText(
                style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 110)),
                text: () => "Imports are copied into data/music/custom."
            )
        ),
        PropertySection(
            "PLAYBACK",
            140,
            PropertyRow(
                "Freq x",
                PropertyDoubleField(
                    () => tab.MusicFreqMul,
                    value => { tab.MusicFreqMul = value; StagePropertyChanged(); }
                ),
                46
            ),
            PropertyRow(
                "Tempo x",
                PropertyDoubleField(
                    () => tab.MusicTempoMul,
                    value => { tab.MusicTempoMul = value; StagePropertyChanged(); }
                ),
                46
            )
        )
    ];

    private void SelectMusicTarget(MusicTarget target)
    {
        _musicTarget = target;
        Touch();
    }

    private string MusicTargetPath(StageEditorTab tab)
        => _musicTarget == MusicTarget.Track ? tab.MusicPath : tab.RemasteredMusicPath;

    private void SetMusicTargetPath(StageEditorTab tab, string path)
    {
        if (path == MusicNoneEntry)
            path = "";

        if (_musicTarget == MusicTarget.Track)
            tab.MusicPath = path;
        else
            tab.RemasteredMusicPath = path;

        StagePropertyChanged();
    }

    /// <summary>
    /// Every playable file under <c>data/music</c>, relative to that folder, with an empty
    /// entry first so a stage can have no soundtrack.
    /// </summary>
    private string[] MusicEntries()
        => _musicEntries ??=
        [
            MusicNoneEntry,
            .. VFS.EnumerateFiles("data/music", "*.*", SearchOption.AllDirectories)
            .Select(path => path.StartsWith("data/music/", StringComparison.OrdinalIgnoreCase)
                    ? path["data/music/".Length..]
                    : path)
            .Where(IsMusicFile)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
        ];

    /// <summary>
    /// Everything <c>AudioDecoder</c> can play; the dotted form drives validation and the
    /// dotless form becomes the native picker's filter pattern.
    /// </summary>
    private static readonly string[] MusicExtensions =
    [
        ".wav", ".wave", ".mp3", ".mpga", ".ogg", ".oga", ".flac", ".opus", ".aif", ".aiff", ".aifc",
        ".zip", ".zipo", ".radq",
        ".mod", ".xm", ".it", ".s3m", ".mptm", ".stm", ".nst", ".mtm", ".669", ".amf", ".ams", ".dbm",
        ".digi", ".dmf", ".dsm", ".far", ".gdm", ".ice", ".imf", ".j2b", ".m15", ".mdl", ".med", ".mo3",
        ".mt2", ".okt", ".plm", ".psm", ".pt36", ".ptm", ".sfx", ".sfx2", ".st26", ".ult", ".wow", ".symmod"
    ];

    private static bool IsMusicFile(string path)
        => MusicExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Dropdown options: (none), every file in the music folder, and the current path if missing.</summary>
    private IReadOnlyList<string> MusicDropdownEntries()
    {
        _ = _uiRevision.Value;
        var entries = MusicEntries();
        var current = ActiveTab is { } tab ? MusicTargetPath(tab) : "";
        if (current.Length == 0 || entries.Contains(current, StringComparer.OrdinalIgnoreCase))
            return entries;

        var list = entries.ToList();
        list.Insert(1, current);
        return list;
    }

    private int MusicEntryIndex(StageEditorTab tab)
    {
        var current = MusicTargetPath(tab);
        var entries = MusicDropdownEntries();
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i], current, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    /// <summary>Native picker; the chosen files are copied into the music folder.</summary>
    private void ImportMusic()
    {
        if (OperatingSystem.IsBrowser())
            return;

        try
        {
            // SDL filter patterns are bare, semicolon-separated extensions ("mp3;ogg"), or a
            // lone "*" for everything. "*.<ext>" is rejected by SDL's validator.
            var musicPattern = string.Join(';', MusicExtensions.Select(extension => extension[1..]));
            SdlFileDialog.OpenFile(
                GameSparker.Game.Window.Handle,
                [("Music files", musicPattern), ("All files", "*")],
                null,
                ImportedMusic
            );
        }
        catch (Exception exception)
        {
            Logging.Warning($"Could not open the music picker: {exception.Message}");
        }
    }

    private void ImportedMusic(IReadOnlyList<string> files)
    {
        if (ActiveTab is not { } tab)
            return;

        string? last = null;
        foreach (var file in files)
        {
            if (CopyMusicFile(file) is { } entry)
                last = entry;
        }

        if (last is null)
            return;

        _musicEntries = null;
        SetMusicTargetPath(tab, last);
    }

    private static string? CopyMusicFile(string source)
    {
        try
        {
            var fileName = SanitizeMusicFileName(Path.GetFileName(source));
            if (fileName.Length == 0 || !IsMusicFile(fileName))
            {
                Logging.Warning($"'{source}' is not a supported music file.");
                return null;
            }

            var entry = "custom/" + fileName;
            var extension = Path.GetExtension(fileName);
            var stem = Path.GetFileNameWithoutExtension(fileName);
            for (var n = 2; VFS.FileExists("data/music/" + entry); n++)
                entry = $"custom/{stem} ({n}){extension}";

            using (var input = File.OpenRead(source))
            using (var output = VFS.OpenWrite("data/music/" + entry))
                input.CopyTo(output);

            Logging.Info($"Imported music '{source}' as '{entry}'.");
            return entry;
        }
        catch (Exception exception)
        {
            Logging.Warning($"Could not import '{source}': {exception.Message}");
            return null;
        }
    }

    /// <summary>soundtrack() splits its arguments on ',' and ')', so those cannot appear in a name.</summary>
    private static string SanitizeMusicFileName(string name)
        => name.Replace(',', '_').Replace('(', '_').Replace(')', '_').Trim();

    private void PreviewSelectedMusic()
    {
        if (ActiveTab is not { } tab)
            return;

        var path = MusicTargetPath(tab);
        if (string.IsNullOrEmpty(path))
        {
            StopMusicPreview();
            return;
        }

        if (string.Equals(_previewMusicPath, path, StringComparison.OrdinalIgnoreCase))
        {
            StopMusicPreview();
            return;
        }

        StopMusicPreview(notify: false);
        var previous = GameSparker.CurrentMusic;

        // Only the plain soundtrack is pitch/tempo adjusted; the remaster plays as authored.
        var adjusted = _musicTarget == MusicTarget.Track;
        var tempo = adjusted ? tab.MusicTempoMul : 1d;
        var frequency = adjusted ? tab.MusicFreqMul : 1d;
        var vfsPath = "data/music/" + path;

        try
        {
            IRadicalMusic music;
            if (OperatingSystem.IsBrowser())
            {
                using var stream = VFS.OpenRead(vfsPath);
                music = new WebAudioMusic(stream, Path.GetExtension(vfsPath), tempo);
            }
            else
            {
                music = IBackend.Backend.LoadMusic(vfsPath, tempo);
            }

            music.SetFreqMultiplier(frequency);
            _previewMusic = music;
            _previewMusicPath = path;
            _musicBeforePreview = previous;
            GameSparker.CurrentMusic = music;
        }
        catch (Exception exception)
        {
            Logging.Warning($"Could not preview '{path}': {exception.Message}");
        }

        Touch();
    }

    private void StopMusicPreview(bool notify = true)
    {
        if (_previewMusic is { } music)
        {
            var previous = _musicBeforePreview;
            _previewMusic = null;
            _previewMusicPath = null;
            _musicBeforePreview = null;

            // Hand whatever was playing back (the menu's track), or silence.
            if (ReferenceEquals(GameSparker.CurrentMusic, music))
                GameSparker.CurrentMusic = previous is null || ReferenceEquals(previous, music) ? null : previous;

            music.Dispose();
        }

        if (notify)
            Touch();
    }

    private object?[] PhysicsSections(StageEditorTab tab) =>
    [
        PropertySection(
            "PHYSICS",
            240,
            PropertyCheckbox(
                "Recharged stats",
                () => tab.Physics.RechargedStats,
                () => SetPhysics(p => p with { RechargedStats = !p.RechargedStats })
            ),
            PropertyRow("Gravity", PhysicsField(tab, p => p.Gravity, (p, v) => p with { Gravity = v }), 96),
            PropertyRow("Grip", PhysicsField(tab, p => p.Grip, (p, v) => p with { Grip = v }), 96),
            PropertyRow("Power loss", PhysicsField(tab, p => p.Powerloss, (p, v) => p with { Powerloss = v }), 96),
            PropertyRow("Damage", PhysicsField(tab, p => p.Damage, (p, v) => p with { Damage = v }), 96),
            PropertyRow("Lava", PhysicsField(tab, p => p.Lava, (p, v) => p with { Lava = v }), 96),
            PropertyRow("Max hit", PhysicsField(tab, p => p.MaxHit, (p, v) => p with { MaxHit = v }), 96),
            PropertyRow("Recoil", PhysicsField(tab, p => p.Recoil, (p, v) => p with { Recoil = v }), 96),
            PropertyRow("Rage", PhysicsField(tab, p => p.Rage, (p, v) => p with { Rage = v }), 96),
            PropertyCheckbox(
                "No arrow",
                () => tab.Physics.NoArrow,
                () => SetPhysics(p => p with { NoArrow = !p.NoArrow })
            )
        )
    ];

    private Node PhysicsField(
        StageEditorTab tab,
        Func<StagePhysics, float> read,
        Func<StagePhysics, float, StagePhysics> change
    )
        => PropertyNumberField(
            () => read(tab.Physics),
            value => SetPhysics(physics => change(physics, value))
        );

    private void SetPhysics(Func<StagePhysics, StagePhysics> change)
    {
        if (ActiveTab is not { } tab)
            return;

        tab.Physics = change(tab.Physics);
        StagePropertyChanged();
    }

    // ── Property pieces ────────────────────────────────────────────────

    private Node PropertySection(string title, float width, params object?[] children)
        => Sx.View(
            style: () => Sx.Styles(width: width, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 3),
            children:
            [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: new Color(128, 170, 255)),
                    layout: () => Sx.Styles(marginBottom: 1),
                    text: () => title
                ),
                ..children
            ]
        );

    private Node PropertyRow(string label, object? control, float labelWidth = 84)
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center, gapColumn: 6, flexShrink: 0),
            children:
            [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 140)),
                    layout: () => Sx.Styles(width: labelWidth, flexShrink: 0),
                    text: () => label
                ),
                control
            ]
        );

    private Node PropertyCheckbox(string label, Func<bool> value, Action toggle)
    {
        // The value is a plain tab field, so the fill/check must re-read it when Touch() bumps
        // _uiRevision - without this the box only updated when the tab switched and rebuilt it.
        var observed = Observe(value);
        return Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 6,
                flexShrink: 0,
                height: 20
            ),
            onmousedown: _ => toggle(),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        width: 13,
                        height: 13,
                        flexShrink: 0,
                        borderRadius: 3,
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: observed() ? new Color(128, 170, 255) : new Color(255, 255, 255, 64),
                        backgroundColor: observed() ? new Color(91, 163, 255) : new Color(255, 255, 255, 20)
                    ),
                    children: Sx.Show(
                        observed,
                        () => Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 9,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: new Color(10, 16, 28)
                            ),
                            text: () => "✓"
                        )
                    )
                ),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 190)),
                    text: () => label
                )
            ]
        );
    }

    private Node SnapField(StageEditorTab tab, string label, int channel)
        => PropertyRow(
            label,
            PropertyIntField(
                () => channel switch { 0 => tab.SnapA, 1 => tab.SnapB, _ => tab.SnapC },
                value =>
                {
                    switch (channel)
                    {
                        case 0: tab.SnapA = value; break;
                        case 1: tab.SnapB = value; break;
                        default: tab.SnapC = value; break;
                    }

                    StageEnvironmentChanged();
                }
            ),
            16
        );

    /// <summary>Swatch, hex readout and one compact slider per channel.</summary>
    private Node ColorField(string label, Func<Color3> read, Action<Color3> write)
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 1, flexShrink: 0),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        gapColumn: 6,
                        height: 16
                    ),
                    children:
                    [
                        Sx.View(
                            style: () => Sx.Styles(
                                width: 26,
                                height: 12,
                                flexShrink: 0,
                                borderRadius: 3,
                                backgroundColor: ObservedColor(read),
                                borderTop: 1,
                                borderBottom: 1,
                                borderLeft: 1,
                                borderRight: 1,
                                borderColor: new Color(255, 255, 255, 90)
                            )
                        ),
                        EditorText(
                            style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 170)),
                            text: () => label
                        ),
                        Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                        EditorText(
                            style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 120)),
                            text: () => HexOf(read())
                        )
                    ]
                ),
                ColorChannel("R", () => read().R, value => write(new Color3(value, read().G, read().B))),
                ColorChannel("G", () => read().G, value => write(new Color3(read().R, value, read().B))),
                ColorChannel("B", () => read().B, value => write(new Color3(read().R, read().G, value)))
            ]
        );

    private Node ColorChannel(string channel, Func<short> read, Action<short> write)
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 4,
                height: 16
            ),
            children:
            [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 120)),
                    layout: () => Sx.Styles(width: 8, flexShrink: 0),
                    text: () => channel
                ),
                Controls.Slider(Observe(() => (double)read()), value => write((short)value), 0, 255, 1, compact: true),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 170)),
                    layout: () => Sx.Styles(width: 22, flexShrink: 0),
                    text: () => read().ToString()
                )
            ]
        );

    /// <summary>A colour read inside a style effect, so the swatch follows slider edits.</summary>
    private Color ObservedColor(Func<Color3> read)
    {
        _ = _uiRevision.Value;
        return read();
    }

    private static string HexOf(Color3 color)
        => $"#{Math.Clamp((int)color.R, 0, 255):X2}{Math.Clamp((int)color.G, 0, 255):X2}{Math.Clamp((int)color.B, 0, 255):X2}";

    private Node PropertyNumberField(Func<float> value, Action<float> set, float width = 72)
        => NumberDraftInput(
            value().ToString("0.###", CultureInfo.InvariantCulture),
            text =>
            {
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && float
                    .IsFinite(parsed))
                    set(parsed);
            },
            width
        );

    private Node PropertyDoubleField(Func<double> value, Action<double> set, float width = 72)
        => NumberDraftInput(
            value().ToString("0.###", CultureInfo.InvariantCulture),
            text =>
            {
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double
                    .IsFinite(parsed))
                    set(parsed);
            },
            width
        );

    private Node PropertyIntField(Func<int> value, Action<int> set, float width = 72)
        => NumberDraftInput(
            value().ToString(CultureInfo.InvariantCulture),
            text =>
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    set(parsed);
            },
            width
        );

    private Node PropertyOptionalIntField(Func<int?> value, Action<int?> set, float width = 72)
        => NumberDraftInput(
            value()?.ToString(CultureInfo.InvariantCulture) ?? "",
            text =>
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    set(null);
                    return;
                }

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    set(parsed);
            },
            width
        );

    /// <summary>
    /// A field with its own draft, so a re-publish of the model does not overwrite what is
    /// being typed. The value commits only when it parses.
    /// </summary>
    private Node NumberDraftInput(string initial, Action<string> changed, float width)
    {
        var draft = new Signal<string>(initial);
        var input = Sx.TextInput(
            style: () => Sx.Styles(
                width: width,
                minWidth: 0,
                height: 20,
                minHeight: 0,
                flexShrink: 0,
                paddingTop: 2,
                paddingBottom: 2,
                paddingLeft: 6,
                paddingRight: 6,
                backgroundColor: new Color(22, 34, 54),
                borderRadius: 4,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 65)
            ),
            text: () => draft.Value,
            onchange: text =>
            {
                draft.Value = text;
                changed(text);
            }
        );
        input.TextStyles = Sx.TextStyles(fontSize: 10);
        return input;
    }

    // ── Applying ───────────────────────────────────────────────────────

    private void StagePropertyChanged()
    {
        if (ActiveTab is { } tab)
            tab.HasUnsavedChanges = true;

        ApplyTabWorldValuesToWorld();
        Touch();
    }

    /// <summary>Ground colour is baked into the ground quad's vertices.</summary>
    private void StageGroundChanged()
    {
        if (ActiveTab is { } tab)
            tab.HasUnsavedChanges = true;

        ApplyTabWorldValuesToWorld();
        RecreateGround();
        Touch();
    }

    /// <summary>The sky gradient bakes the sky and fog colours; cloud tint follows the sky.</summary>
    private void StageSkyChanged()
    {
        if (ActiveTab is not { } tab)
            return;

        tab.HasUnsavedChanges = true;
        ApplyTabWorldValuesToWorld();
        RecreateSky();
        if (tab.CloudsEnabled)
            RecreateScenery();
        Touch();
    }

    private void StageFogChanged()
    {
        if (ActiveTab is { } tab)
            tab.HasUnsavedChanges = true;

        ApplyTabWorldValuesToWorld();
        RecreateSky();
        Touch();
    }

    /// <summary>The polys mesh bakes the ground/polys colours.</summary>
    private void StagePolysChanged()
    {
        if (ActiveTab is { } tab)
            tab.HasUnsavedChanges = true;

        ApplyTabWorldValuesToWorld();
        RecreatePolys();
        Touch();
    }

    /// <summary>A property whose object has to be rebuilt (snap tints every environment mesh).</summary>
    private void StageEnvironmentChanged()
    {
        if (ActiveTab is { } tab)
            tab.HasUnsavedChanges = true;

        ApplyTabWorldValuesToWorld();
        RecreateEnvironment();
        Touch();
    }

    /// <summary>Clouds/mountains: cheaper than <see cref="StageEnvironmentChanged"/>.</summary>
    private void StageSceneryChanged()
    {
        if (ActiveTab is { } tab)
            tab.HasUnsavedChanges = true;

        ApplyTabWorldValuesToWorld();
        RecreateScenery();
        Touch();
    }
}
