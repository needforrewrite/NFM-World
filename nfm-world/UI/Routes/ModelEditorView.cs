extern alias SDL3New;

using System.Globalization;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorldLibrary;
using NFMWorldLibrary.FixedMath;
using SignalsDotnet;
using SDL = SDL3New::SDL3.SDL;

namespace NFMWorld.UI.Routes;

/// <summary>
/// The model editor's 2D chrome: file actions, open-model tabs, the code and polygon
/// editors, and the viewport control panel. The 3D viewport and picking stay in
/// <see cref="ModelEditorPhase"/>.
/// </summary>
public sealed class ModelEditorView
{
    private readonly ModelEditorPhase _phase;

    // The renderer only re-reads an accessor when a signal it read changes, so every
    // accessor that depends on the phase's mutable tab state reads this first.
    private readonly Signal<int> _revision = new(0);
    private readonly Signal<ModelEditorTab?> _activeTab = new(null);
    private readonly Signal<int> _contentVersion = new(0);
    private readonly Signal<int> _polygonVersion = new(0);

    private readonly Signal<BottomTab> _bottomTab = new(BottomTab.Controls);
    private readonly Signal<bool> _newModelOpen = new(false);
    private readonly Signal<bool> _loadOpen = new(false);
    private readonly Signal<bool> _newIsCar = new(true);
    private readonly Signal<string> _newName = new("");
    private readonly Signal<bool> _newOpenInTab = new(true);
    private readonly Signal<bool> _newImport = new(false);
    private readonly Signal<int> _newReference = new(0);
    private readonly Signal<bool> _loadOpenInTab = new(true);
    private readonly Signal<IReadOnlyList<string>> _loadFiles = new([]);
    private readonly Signal<string> _loadSearch = new("");
    private readonly Signal<string> _scaleXField = new("100");
    private readonly Signal<string> _scaleYField = new("100");
    private readonly Signal<string> _scaleZField = new("100");
    private readonly Signal<string> _scaleDivField = new("10");
    private readonly Signal<bool> _divIsIdiv = new(false);
    private readonly Signal<int> _scaleStateVersion = new(0);
    private readonly Signal<int> _selectedWheelIndex = new(0);
    private readonly Signal<int> _wheelStateVersion = new(0);
    private readonly Signal<string> _wheelX = new("0");
    private readonly Signal<string> _wheelY = new("0");
    private readonly Signal<string> _wheelZ = new("0");
    private readonly Signal<string> _wheelWidth = new("10");
    private readonly Signal<string> _wheelHeight = new("10");
    private readonly Signal<string> _wheelRotates = new("0");
    private readonly Signal<string> _rimR = new("140");
    private readonly Signal<string> _rimG = new("140");
    private readonly Signal<string> _rimB = new("140");
    private readonly Signal<string> _rimSize = new("20");
    private readonly Signal<string> _rimDepth = new("20");
    private readonly Signal<int> _statsStateVersion = new(0);
    private readonly Dictionary<string, Signal<string>> _statFields = CreateStatSignals();

    private bool _statsApplyPending;
    private long _statsApplyDue;
    private ModelEditorTab? _statsApplyTab;
    private bool _statsApplying;

    private readonly Signal<int> _testDriveMode = new(1);
    private readonly Signal<bool> _mirrorWheelEdit = new(true);
    private readonly Signal<bool> _gizmoVisible = new(false);
    private readonly Signal<Vector2> _gizmoOrigin = new(Vector2.Zero);
    private readonly Signal<Vector2> _gizmoX = new(Vector2.Zero);
    private readonly Signal<Vector2> _gizmoY = new(Vector2.Zero);
    private readonly Signal<Vector2> _gizmoZ = new(Vector2.Zero);

    private int _gizmoAxis = -1;
    private Vector2 _gizmoDragStart;
    private Vector2 _gizmoAxisVector;
    private ModelEditorPhase.WheelSpec _gizmoStartSpec;

    private bool _rimApplyPending;
    private long _rimApplyDue;
    private ModelEditorTab? _rimApplyTab;

    private sealed class LiveEditorTicker(ModelEditorView view) : View
    {
        protected override void GameTick()
        {
            base.GameTick();
            view.TickLiveEdits();
        }
    }

    private readonly List<TextSnapshot> _undoHistory = [];
    private readonly List<TextSnapshot> _redoHistory = [];
    private bool _historySuspended;
    private bool _lastHistoryCoalesced;
    private long _lastHistoryAt;
    private TextArea? _codeEditor;

    private sealed record TextSnapshot(string Text, int Caret, int? Anchor);

    private const int MaxHistoryEntries = 200;
    private const long HistoryCoalesceMs = 700;

    private readonly Signal<ConfirmRequest?> _confirm = new(null);
    private readonly Signal<ErrorRequest?> _error = new(null);

    private enum BottomTab
    {
        Controls,
        Render,
        Color,
        Scale,
        Wheels,
        Stats,
        Physics,
        TestDrive
    }

    private sealed record ConfirmRequest(string Title, string Message, IReadOnlyList<ConfirmAction> Actions);
    private sealed record ConfirmAction(string Label, Action Run);
    private sealed record ErrorRequest(string Title, string Message);

    private static readonly Color Blue = new(128, 170, 255);
    private static readonly Color TextBlue = new(185, 211, 255);
    private static readonly Color Panel = new(12, 22, 42, 245);
    private static readonly Color PanelSoft = new(22, 42, 80, 220);
    private static readonly Color Border = new(128, 170, 255, 100);
    private static readonly Color Bar = new(8, 16, 32, 235);

    private static readonly Styles BarStyle = new()
    {
        Position = Position.Absolute,
        Left = 0,
        Right = 0,
        Top = 0,
        Height = 46,
        FlexDirection = FlexDirection.Row,
        AlignItems = Align.Center,
        PaddingLeft = 14,
        PaddingRight = 14,
        GapColumn = 8,
        GapRow = 8,
        BackgroundColor = Bar,
        BorderBottom = 1,
        BorderColor = Border,
        ZIndex = 10,
    };

    private static readonly Styles TabStripStyle = new()
    {
        Position = Position.Absolute,
        Left = 0,
        Right = 0,
        Top = 46,
        Height = 32,
        FlexDirection = FlexDirection.Row,
        AlignItems = Align.Stretch,
        PaddingLeft = 8,
        PaddingRight = 8,
        GapColumn = 4,
        GapRow = 4,
        BackgroundColor = new Color(6, 12, 24, 220),
        BorderBottom = 1,
        BorderColor = new Color(128, 170, 255, 45),
        ZIndex = 10,
    };

    /// <summary>The editor panels that dock over the left of the viewport (code and polygon).</summary>
    private Styles LeftPanelStyle => new()
    {
        Position = Position.Absolute,
        Top = 86,
        Left = 10,
        Bottom = BottomPanelHeight + 10,
        Width = MeasurementWidthHeight.Percent(40),
        MinWidth = 340,
        MaxWidth = 600,
        FlexDirection = FlexDirection.Column,
        BackgroundColor = Panel,
        BorderTop = 1,
        BorderBottom = 1,
        BorderLeft = 1,
        BorderRight = 1,
        BorderColor = Border,
        BorderRadius = 10,
        ZIndex = 10,
    };

    public ModelEditorView(ModelEditorPhase phase)
    {
        _phase = phase;
    }

    /// <summary>Any modal or the polygon editor has the screen, so the viewport must not.</summary>
    public bool ModalOpen => _newModelOpen.Value || _loadOpen.Value
        || _confirm.Value is not null || _error.Value is not null;

    /// <summary>Whether the left dock (code or polygon editor) is covering part of the viewport.</summary>
    public bool LeftPanelOpen => _phase.ActiveTab is { } tab && (tab.TextEditorExpanded || tab.ShowPolygonEditor);

    /// <summary>Whether the Wheels tab is showing, for the 3D wheel highlight.</summary>
    public bool WheelsTabActive => _bottomTab.Value == BottomTab.Wheels;

    public int HighlightedWheelIndex => _selectedWheelIndex.Value;

    public void Refresh() => _revision.Value = _revision.Value + 1;

    /// <summary>Screen-space gizmo update from the 3D pass. Axis vectors are per raw w() unit.</summary>
    public void UpdateWheelGizmo(bool visible, Vector2 origin, Vector2 axisX, Vector2 axisY, Vector2 axisZ)
    {
        _gizmoVisible.Value = visible;
        if (!visible)
            return;

        _gizmoOrigin.Value = origin;
        _gizmoX.Value = axisX;
        _gizmoY.Value = axisY;
        _gizmoZ.Value = axisZ;
    }

    public void HideWheelGizmo() => _gizmoVisible.Value = false;

    /// <summary>Applies debounced edits that outlive the Wheels tab being unmounted.</summary>
    private void TickLiveEdits()
    {
        var now = System.Environment.TickCount64;

        if (_statsApplyPending && now >= _statsApplyDue)
        {
            _statsApplyPending = false;
            if (ReferenceEquals(_phase.ActiveTab, _statsApplyTab))
                ApplyStats(reportErrors: false, coalesce: true);
        }

        if (!_rimApplyPending || now < _rimApplyDue)
            return;

        _rimApplyPending = false;
        if (!ReferenceEquals(_phase.ActiveTab, _rimApplyTab))
            return;

        ApplyRims(live: true);
    }

    private void ScheduleRimApply()
    {
        _rimApplyPending = true;
        _rimApplyTab = _phase.ActiveTab;
        _rimApplyDue = System.Environment.TickCount64 + 180;
    }

    private void ClearHistory()
    {
        _undoHistory.Clear();
        _redoHistory.Clear();
        _lastHistoryCoalesced = false;
    }

    /// <summary>Snapshots the active tab before a change. Coalesces bursts of typing/dragging.</summary>
    private void PushHistory(bool coalesce)
    {
        if (_historySuspended || _phase.ActiveTab is not { } tab)
            return;

        var now = System.Environment.TickCount64;
        if (coalesce && _lastHistoryCoalesced && now - _lastHistoryAt <= HistoryCoalesceMs)
        {
            _lastHistoryAt = now;
            return;
        }

        _undoHistory.Add(CaptureSnapshot());
        if (_undoHistory.Count > MaxHistoryEntries)
            _undoHistory.RemoveAt(0);
        _redoHistory.Clear();
        _lastHistoryCoalesced = coalesce;
        _lastHistoryAt = now;
    }

    private TextSnapshot CaptureSnapshot()
    {
        var text = _phase.ActiveTab?.TextContent ?? "";
        var editor = LiveCodeEditor();
        return new TextSnapshot(text, editor?.CursorIndex ?? 0, editor?.SelectionAnchor);
    }

    private void ScrollCodeToStart()
    {
        if (LiveCodeEditor() is not { } editor)
            return;

        editor.ScrollTop = 0;
        editor.ScrollLeft = 0;
    }

    /// <summary>The code editor only exists while its panel is mounted.</summary>
    private TextArea? LiveCodeEditor()
    {
        var tab = _phase.ActiveTab;
        return tab is { TextEditorExpanded: true, ShowPolygonEditor: false } ? _codeEditor : null;
    }

    public void UndoDocument()
    {
        if (_historySuspended || _phase.ActiveTab is not { } tab || _undoHistory.Count == 0)
            return;

        _redoHistory.Add(CaptureSnapshot());
        var entry = _undoHistory[^1];
        _undoHistory.RemoveAt(_undoHistory.Count - 1);
        ApplySnapshot(tab, entry);
        _lastHistoryCoalesced = false;
    }

    public void RedoDocument()
    {
        if (_historySuspended || _phase.ActiveTab is not { } tab || _redoHistory.Count == 0)
            return;

        _undoHistory.Add(CaptureSnapshot());
        var entry = _redoHistory[^1];
        _redoHistory.RemoveAt(_redoHistory.Count - 1);
        ApplySnapshot(tab, entry);
        _lastHistoryCoalesced = false;
    }

    private void ApplySnapshot(ModelEditorTab tab, TextSnapshot entry)
    {
        _historySuspended = true;
        try
        {
            tab.TextContent = entry.Text;
            if (LiveCodeEditor() is { } editor)
            {
                editor.SetTextExternal(entry.Text);
                editor.SetCaret(entry.Caret, entry.Anchor);
            }

            if (_phase.ReloadActiveTabFromText() is { } error)
                ShowError("Undo", error);

            tab.TextEditorDirty = !string.Equals(tab.TextContent, tab.SavedText, StringComparison.Ordinal);

            // Rebuild the docks so their local edit fields re-read the restored source.
            _scaleStateVersion.Value++;
            _wheelStateVersion.Value++;
            _statsStateVersion.Value++;
            Refresh();
        }
        finally
        {
            _historySuspended = false;
        }
    }

    /// <summary>The active tab changed identity: reload the editors' external text.</summary>
    public void OnActiveTabChanged()
    {
        ClearHistory();
        _activeTab.Value = _phase.ActiveTab;
        _contentVersion.Value++;
        _polygonVersion.Value++;
        _scaleStateVersion.Value++;
        _wheelStateVersion.Value++;
        _statsStateVersion.Value++;
        _statsApplyPending = false;
        _statsApplyTab = null;
        Refresh();
    }

    public void OnPolygonContentChanged() => _polygonVersion.Value++;

    /// <summary>The model's source text changed outside the code editor (polygon apply).</summary>
    public void OnContentChanged()
    {
        _contentVersion.Value++;
        if (!_statsApplying)
            _statsStateVersion.Value++;
    }

    /// <summary>Escape: close the topmost dialog, else report false so the phase can exit.</summary>
    public bool HandleEscape()
    {
        if (_error.Value is not null) { _error.Value = null; return true; }
        if (_confirm.Value is not null) { _confirm.Value = null; return true; }
        if (_newModelOpen.Value) { _newModelOpen.Value = false; return true; }
        if (_loadOpen.Value) { _loadOpen.Value = false; return true; }
        if (_phase.ActiveTab is { ShowPolygonEditor: true })
        {
            RequestClosePolygonEditor();
            return true;
        }
        return false;
    }

    public Node Build()
    {
        ClearHistory();
        _activeTab.Value = _phase.ActiveTab;
        _contentVersion.Value++;
        _polygonVersion.Value++;
        _statsStateVersion.Value++;

        return Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, left: 0, top: 0, right: 0, bottom: 0),
            children:
            [
                TopBar(),
                TabStrip(),
                SelectionPanel(),
                CodePanel(),
                BottomPanel(),
                EmptyState(),
                NewModelDialog(),
                LoadModelDialog(),
                PolygonEditorDialog(),
                ConfirmDialog(),
                ErrorDialog(),
                WheelGizmoOverlay(),
                ..WheelGizmoHandles(),
                new LiveEditorTicker(this)
                {
                    Styles = Sx.Styles(position: Position.Absolute, width: 0, height: 0,
                        pointerEvents: false)
                }
            ]);
    }

    // ── Chrome ─────────────────────────────────────────────────────────

    private Node TopBar() => Sx.View(
        style: () => BarStyle,
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 18, fontFamily: FontFamily.Adventure,
                    fontStyle: FontStyle.Bold, foregroundColor: Blue),
                layout: () => Sx.Styles(flexShrink: 0),
                text: () => "MODEL EDITOR"),
            Sx.View(style: () => Sx.Styles(flexGrow: 1)),
            Button.Create(() => "CODE", ToggleCodeEditor,
                active: () => { _ = _revision.Value; return _phase.ActiveTab is { TextEditorExpanded: true, ShowPolygonEditor: false }; },
                size: ButtonSize.Sm, style: () => Sx.Styles(minWidth: 68, flexShrink: 0)),
            BarButton("NEW", OpenNewModelDialog, ButtonVariant.Primary),
            BarButton("OPEN", OpenLoadDialog),
            BarButton("BACK TO MENU", RequestExit)
        ]);

    private static View BarButton(string label, Action action, ButtonVariant variant = ButtonVariant.Default)
        => Button.Create(() => label, action, variant, size: ButtonSize.Sm,
            style: () => Sx.Styles(minWidth: 70, flexShrink: 0));

    private Node TabStrip() => Sx.View(
        style: () => TabStripStyle,
        // Snapshot: RenderScope.Bind compares the computed value by equality, and the
        // phase's tab list keeps its identity, so returning it directly would never
        // rebuild the rows after a tab was opened.
        children: Sx.For(
            each: () => { _ = _revision.Value; return _phase.Tabs.ToList(); },
            row: (tab, _) => TabChip(tab)));

    private Node TabChip(ModelEditorTab tab)
    {
        var hovered = new Signal<bool>(false);

        bool Active()
        {
            _ = _revision.Value;
            return _phase.ActiveTab == tab;
        }

        return Sx.View(
            style: () => new Styles
            {
                Position = Position.Relative,
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                GapColumn = 6,
                GapRow = 6,
                PaddingLeft = 10,
                PaddingRight = 6,
                MaxWidth = 220,
                MinWidth = 100,
                FlexShrink = 0,
                BackgroundColor = Active() ? PanelSoft : hovered.Value ? new Color(22, 42, 80, 110) : Theme.Transparent,
                BorderTop = 1,
                BorderLeft = 1,
                BorderRight = 1,
                BorderColor = Active() ? Border : Theme.Transparent,
                BorderTopLeftRadius = 6,
                BorderTopRightRadius = 6,
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: _ => _phase.SetActiveTab(IndexOfTab(tab)),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minWidth: 0),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 12,
                                fontStyle: Active() ? FontStyle.Bold : FontStyle.Plain,
                                foregroundColor: Active() ? TextBlue : new Color(255, 255, 255, 170)),
                            layout: () => Sx.Styles(flexShrink: 0),
                            text: () => { _ = _revision.Value; return tab.GetDisplayName(); })
                    ]),
                Sx.View(
                    style: () => Sx.Styles(width: 18, height: 18, alignItems: Align.Center,
                        justifyContent: Justify.Center, borderRadius: 4),
                    onmousedown: e =>
                    {
                        e.StopPropagation();
                        RequestCloseTab(IndexOfTab(tab));
                    },
                    children: Sx.Text(style: () => Sx.TextStyles(fontSize: 14,
                        foregroundColor: new Color(255, 255, 255, 150)), text: () => "×"))
            ]);
    }

    private int IndexOfTab(ModelEditorTab tab)
    {
        for (var i = 0; i < _phase.Tabs.Count; i++)
            if (ReferenceEquals(_phase.Tabs[i], tab))
                return i;
        return -1;
    }

    private Node EmptyState() => Sx.Show(
        // Only with no open tab: an open tab owns the editor even when its parse failed.
        when: () => { _ = _revision.Value; return _phase.Tabs.Count == 0; },
        then: () => Sx.View(
            // Centre in the 3D viewport, not under the anchored footer - and stop at the
            // footer so it can never swallow the footer's clicks.
            style: () => Sx.Styles(position: Position.Absolute, top: 78, left: 0, right: 0, bottom: BottomPanelHeight,
                alignItems: Align.Center, justifyContent: Justify.Center, zIndex: 5),
            children: Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Column, alignItems: Align.Center,
                    gapRow: 10, paddingTop: 22, paddingBottom: 22, paddingLeft: 30, paddingRight: 30,
                    backgroundColor: Panel, borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                    borderColor: Border, borderRadius: 12),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 24, fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold, foregroundColor: Blue), text: () => "NO MODEL OPEN"),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue),
                        text: () => "Open an existing model or create a new one."),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10,
                            gapRow: 10, marginTop: 6),
                        children:
                        [
                            Button.Create(() => "NEW MODEL", OpenNewModelDialog, ButtonVariant.Primary,
                                style: () => Sx.Styles(minWidth: 130)),
                            Button.Create(() => "OPEN MODEL", OpenLoadDialog,
                                style: () => Sx.Styles(minWidth: 130))
                        ])
                ])));

    // ── Selection / edit mode ──────────────────────────────────────────

    private Node SelectionPanel() => Sx.Show(
        when: () => { _ = _revision.Value; return _phase.ActiveTab?.Object != null; },
        then: () => Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, top: 86, right: 10,
                width: 380, flexDirection: FlexDirection.Column, gapRow: 6,
                paddingTop: 10, paddingBottom: 10, paddingLeft: 12, paddingRight: 12,
                backgroundColor: Panel, borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                borderColor: Border, borderRadius: 10, zIndex: 10),
            children:
            [
                Sx.Show(
                    when: () => { _ = _revision.Value; return _phase.ActiveTab?.ModelType == ModelEditorTab.ModelTypeEnum.Stage; },
                    then: () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row,
                            alignItems: Align.Center, gapColumn: 8, gapRow: 8),
                        children:
                        [
                            Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                                text: () => "EDIT MODE"),
                            ModeButton("POLYGON", ModelEditorTab.EditModeEnum.Polygon),
                            ModeButton("COLLISION", ModelEditorTab.EditModeEnum.Collision)
                        ])),
                Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue,
                        breakType: BreakType.Word, overflowBehavior: OverflowBehavior.ContinueVertically),
                    layout: () => Sx.Styles(width: "100%"),
                    text: SelectionText),
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 8, gapRow: 8),
                    children:
                    [
                        Sx.Show(() => HasSelection(), then: () => Button.Create(
                            () => _phase.ActiveTab?.EditMode == ModelEditorTab.EditModeEnum.Collision
                                ? "EDIT COLLISION" : "EDIT PIECE",
                            EditSelectedPiece, ButtonVariant.Primary, size: ButtonSize.Sm)),
                        Sx.Show(() => HasSelection(), then: () => Button.Create(
                            () => "DESELECT", _phase.ClearSelection, variant: ButtonVariant.Ghost,
                            size: ButtonSize.Sm))
                    ])
            ]));

    private bool HasSelection()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab || tab.Object is null)
            return false;

        return tab.EditMode == ModelEditorTab.EditModeEnum.Collision
            ? tab.SelectedCollisionIndex >= 0
            : tab.SelectedPolygonIndex >= 0 || (tab.SelectedWheelIndex >= 0 && tab.SelectedWheelPolygonIndex >= 0);
    }

    private string SelectionText()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab || tab.Object is null)
            return "";

        if (tab.EditMode == ModelEditorTab.EditModeEnum.Collision)
        {
            if (tab.SelectedCollisionIndex >= 0 && tab.SelectedCollisionIndex < tab.Object.Boxes.Count)
            {
                var box = tab.Object.Boxes[tab.SelectedCollisionIndex];
                return $"[ Collision {tab.SelectedCollisionIndex + 1} of {tab.Object.Boxes.Count} selected ]\n" +
                       $"Angle: xy={box.Xy:F0}° zy={box.Zy:F0}°   Radius: [{box.Radius.X:F0}, {box.Radius.Y:F0}, {box.Radius.Z:F0}]   Offset: [{box.Translation.X:F0}, {box.Translation.Y:F0}, {box.Translation.Z:F0}]";
            }

            return "Click a collision box in the 3D view to select it.";
        }

        if (tab.SelectedWheelIndex >= 0 && tab.SelectedWheelPolygonIndex >= 0 &&
            tab.SelectedWheelIndex < tab.Object.WheelObjects.Count)
        {
            var wheel = tab.Object.WheelObjects[tab.SelectedWheelIndex];
            return $"[ Wheel {tab.SelectedWheelIndex + 1} of {tab.Object.WheelObjects.Count}, Piece {tab.SelectedWheelPolygonIndex + 1} of {wheel.Mesh.Polys.Length} selected ]";
        }

        if (tab.SelectedPolygonIndex >= 0 && tab.SelectedPolygonIndex < tab.Object.Mesh.Polys.Length)
            return $"[ Piece {tab.SelectedPolygonIndex + 1} of {tab.Object.Mesh.Polys.Length} selected ]";

        return "Click a polygon in the 3D view to select it.";
    }

    private Node ModeButton(string label, ModelEditorTab.EditModeEnum mode)
    {
        bool Active()
        {
            _ = _revision.Value;
            return _phase.ActiveTab?.EditMode == mode;
        }

        return Button.Create(() => label, () => _phase.SetEditMode(mode), active: Active, size: ButtonSize.Sm);
    }

    private void EditSelectedPiece()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return;

        if (tab.EditMode == ModelEditorTab.EditModeEnum.Collision)
            _phase.JumpToSelectedCollisionInText();
        else if (tab.SelectedWheelIndex >= 0 && tab.SelectedWheelPolygonIndex >= 0)
            _phase.JumpToSelectedWheelPolygonInText();
        else
            _phase.JumpToSelectedPolygonInText();
    }

    // ── Code editor ────────────────────────────────────────────────────

    private Node CodePanel() => Sx.Show(
        when: () => { _ = _revision.Value; return _phase.ActiveTab is { TextEditorExpanded: true, ShowPolygonEditor: false }; },
        then: () => Sx.View(
            style: () => LeftPanelStyle,
            children:
            [
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                        gapColumn: 6, gapRow: 6, paddingTop: 8, paddingBottom: 8,
                        paddingLeft: 10, paddingRight: 10, flexShrink: 0),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontFamily: FontFamily.Adventure,
                            fontStyle: FontStyle.Bold, foregroundColor: Blue),
                            text: () => "CODE"),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 200, 100)),
                            layout: () => Sx.Styles(flexShrink: 0),
                            text: () => { _ = _revision.Value; return _phase.ActiveTab?.TextEditorDirty == true ? "MODIFIED" : ""; }),
                        Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                        CodeAction("SAVE", () => SaveActiveCode(false)),
                        CodeAction("SAVE & PREVIEW", () => SaveActiveCode(true)),
                        CodeAction("EXTERNAL", OpenActiveCodeExternally),
                        CodeAction("HIDE", () =>
                        {
                            if (_phase.ActiveTab is { } tab)
                            {
                                tab.TextEditorExpanded = false;
                                Refresh();
                            }
                        })
                    ]),
                Sx.View(style: () => Sx.Styles(flexGrow: 1, minHeight: 0, paddingLeft: 8,
                        paddingRight: 8, paddingBottom: 8),
                    children: CreateTextArea(
                        () => Sx.Styles(flexGrow: 1, minHeight: 0),
                        () => { _ = _contentVersion.Value; return _activeTab.Value?.TextContent ?? ""; },
                        value =>
                        {
                            if (_activeTab.Value is not { } tab)
                                return;
                            tab.TextContent = value;
                            tab.TextEditorDirty = true;
                            Refresh();
                        }, localHistory: false))
            ]));

    private TextArea CreateTextArea(Func<Styles> style, Func<string?> text, Action<string> onchange,
        bool localHistory = true)
    {
        var area = Sx.TextArea(
            style: style,
            // RobotoMono's baked fields render inverted through this text pipeline (light
            // glyph boxes with dark counters), so the editor uses the readable UI font.
            textStyle: () => Sx.TextStyles(fontFamily: FontFamily.NotoSans, fontSize: 12,
                foregroundColor: new Color(220, 232, 255)),
            text: text,
            onchange: onchange);
        area.GetClipboardText = SDL.SDL_GetClipboardText;
        area.SetClipboardText = value => SDL.SDL_SetClipboardText(value);
        area.UseLocalHistory = localHistory;

        if (!localHistory)
        {
            _codeEditor = area;
            area.EditStarted = () => PushHistory(coalesce: true);
        }

        return area;
    }

    private static View CodeAction(string label, Action action)
        => Button.Create(() => label, action, size: ButtonSize.Sm,
            style: () => Sx.Styles(flexShrink: 0));

    private void ToggleCodeEditor()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return;

        if (tab.ShowPolygonEditor)
        {
            RequestClosePolygonEditor();
            return;
        }

        tab.TextEditorExpanded = !tab.TextEditorExpanded;
        Refresh();
    }

    /// <summary>Ctrl+S: commit any debounced field edits, then save/reload the model.</summary>
    public void SaveActiveDocument()
    {
        if (_phase.ActiveTab is not { } tab)
            return;

        if (_statsApplyPending && ReferenceEquals(tab, _statsApplyTab))
        {
            _statsApplyPending = false;
            ApplyStats(reportErrors: false, coalesce: true);
        }

        if (_rimApplyPending && ReferenceEquals(tab, _rimApplyTab))
        {
            _rimApplyPending = false;
            ApplyRims(live: true);
        }

        SaveActiveCode(false);
    }

    private void SaveActiveCode(bool preview)
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return;

        if (_phase.SaveTab(tab, preview) is { } error)
        {
            ShowError("Save Failed", error);
            return;
        }

        // A save reparses after hand edits, so re-read the source-backed docks.
        _scaleStateVersion.Value++;
        _wheelStateVersion.Value++;
        _statsStateVersion.Value++;
        Refresh();
    }

    private void OpenActiveCodeExternally()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is { } tab && _phase.OpenInExternalEditor(tab) is { } error)
            ShowError("External Editor", error);
    }

    // ── Bottom controls ────────────────────────────────────────────────

    private const float BottomPanelHeight = 172;

    private Node BottomPanel() => Sx.View(
        style: () => new Styles
        {
            Position = Position.Absolute,
            Left = 0,
            Right = 0,
            Bottom = 0,
            Height = BottomPanelHeight,
            FlexDirection = FlexDirection.Column,
            BackgroundColor = Bar,
            BorderTop = 1,
            BorderColor = Border,
            ZIndex = 10,
        },
        children:
        [
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Stretch,
                    paddingLeft: 10, paddingRight: 10, gapColumn: 2, gapRow: 2,
                    backgroundColor: new Color(7, 14, 28, 180), borderBottom: 1,
                    borderColor: new Color(128, 170, 255, 45), flexShrink: 0),
                children:
                [
                    BottomTabButton("3D CONTROLS", BottomTab.Controls),
                    BottomTabButton("RENDER", BottomTab.Render),
                    BottomTabButton("COLOR EDIT", BottomTab.Color),
                    BottomTabButton("SCALE & ALIGN", BottomTab.Scale),
                    BottomTabButton("WHEELS", BottomTab.Wheels),
                    BottomTabButton("STATS & CLASS", BottomTab.Stats),
                    BottomTabButton("PHYSICS", BottomTab.Physics),
                    BottomTabButton("TEST DRIVE", BottomTab.TestDrive),
                    Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                    Sx.View(style: () => Sx.Styles(alignSelf: Align.Center),
                        children: SmallButton("SAVE MODEL", () => SaveActiveCode(false), minWidth: 78))
                ]),
            Sx.Switch(_bottomTab, new Dictionary<BottomTab, Func<Node>>
            {
                [BottomTab.Controls] = ControlsTab,
                [BottomTab.Render] = RenderTab,
                [BottomTab.Color] = ColorTab,
                [BottomTab.Scale] = ScaleTab,
                [BottomTab.Wheels] = WheelsTab,
                [BottomTab.Stats] = StatsTab,
                [BottomTab.Physics] = PhysicsTab,
                [BottomTab.TestDrive] = TestDriveTab
            })
        ]);

    private Node BottomTabButton(string label, BottomTab tab)
    {
        var hovered = new Signal<bool>(false);

        return Sx.View(
            style: () => Sx.Styles(position: Position.Relative, paddingTop: 6, paddingBottom: 6,
                paddingLeft: 10, paddingRight: 10, alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: _bottomTab.Value == tab ? PanelSoft
                    : hovered.Value ? new Color(22, 42, 80, 110) : Theme.Transparent),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: _ => _bottomTab.Value = tab,
            children: Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold,
                    foregroundColor: _bottomTab.Value == tab ? TextBlue : new Color(255, 255, 255, 150)),
                text: () => label));
    }

    private Node ControlsTab() => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexGrow: 1, gapColumn: 16, gapRow: 8,
            paddingTop: 8, paddingBottom: 8, paddingLeft: 12, paddingRight: 12, overflow: Overflow.Scroll),
        children:
        [
            HelpColumn("KEYBOARD", ["W/S: Zoom In/Out", "A/D: Orbit Left/Right", "Shift+W/S: Pan Up/Down",
                "Shift+A/D: Pan Left/Right", "Arrows: Orbit Camera", "+/-: Zoom In/Out"]),
            HelpColumn("MOUSE", ["Drag: Orbit camera", "Shift+Drag: Pan model", "Scroll: Zoom in/out",
                "Click: Select polygon"]),
            Sx.View(style: () => Sx.Styles(flexGrow: 1, minWidth: 280, flexDirection: FlexDirection.Column,
                    gapRow: 1),
                children:
                [
                    SliderRow("X (Pitch)", () =>
                    {
                        var tab = ActiveTabValue();
                        return tab is null ? 0 : (float)tab.ModelRotation.X;
                    }, v => _phase.SetModelRotation(0, v), -180, 180, 1, v => $"{v:F0}°"),
                    SliderRow("Y (Yaw)", () =>
                    {
                        var tab = ActiveTabValue();
                        return tab is null ? 0 : (float)tab.ModelRotation.Y;
                    }, v => _phase.SetModelRotation(1, v), -180, 180, 1, v => $"{v:F0}°"),
                    SliderRow("Z (Roll)", () =>
                    {
                        var tab = ActiveTabValue();
                        return tab is null ? 0 : (float)tab.ModelRotation.Z;
                    }, v => _phase.SetModelRotation(2, v), -180, 180, 1, v => $"{v:F0}°"),
                    SliderRow("Height", () =>
                    {
                        var tab = ActiveTabValue();
                        return tab is null ? 0 : (float)tab.ModelPosition.Y;
                    }, _phase.SetModelHeight, -500, 500, 1, v => $"{v:F0}"),
                    SliderRow("Camera Distance", () => ActiveTabValue()?.CameraDistance ?? 800,
                        _phase.SetCameraDistance, 300, 5000, 10, v => $"{v:F0}"),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, marginTop: 2),
                        children: Button.Create(() => "RESET VIEW", _phase.ResetActiveView, size: ButtonSize.Sm))
                ])
        ]);

    private ModelEditorTab? ActiveTabValue()
    {
        _ = _revision.Value;
        return _phase.ActiveTab;
    }

    private static Node HelpColumn(string title, IReadOnlyList<string> lines) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 1, flexShrink: 0),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold,
                foregroundColor: Blue), text: () => title),
            ..lines.Select(line => Sx.Text(style: () => Sx.TextStyles(fontSize: 10,
                foregroundColor: TextBlue), text: () => line))
        ]);

    private Node SliderRow(string label, Func<double> value, Action<double> set,
        double min, double max, double step, Func<double, string> display) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
            gapColumn: 8, gapRow: 8, height: 21, flexShrink: 0),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                layout: () => Sx.Styles(width: 96, flexShrink: 0),
                text: () => label),
            Controls.Slider(() => { _ = _revision.Value; return value(); }, v =>
            {
                set(v);
                Refresh();
            }, min, max, step),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 160)),
                layout: () => Sx.Styles(width: 58, flexShrink: 0),
                text: () => { _ = _revision.Value; return display(value()); })
        ]);

    // ── Polygon colours ────────────────────────────────────────────────

    private Node ColorTab() => Sx.Show(
        when: () => { _ = _revision.Value; return _phase.ActiveTab is { ModelType: ModelEditorTab.ModelTypeEnum.Car }; },
        then: () => Sx.View(
            style: () => Sx.Styles(flexGrow: 1, flexDirection: FlexDirection.Column, gapRow: 4,
                paddingTop: 6, paddingBottom: 6, paddingLeft: 10, paddingRight: 10, overflow: Overflow.Scroll),
            children:
            [
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                        gapColumn: 8, gapRow: 8),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold,
                            foregroundColor: Blue), text: () => "COLOR SLOTS")
                    ]),
                Sx.Show(
                    when: () => !BothPrimaryColorsDefined(),
                    then: DefineColorsPrompt),
                Sx.Show(
                    when: () => DefinedColorSlots().Count > 0,
                    then: () => Sx.View(
                        style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexWrap: Wrap.Wrap,
                            gapColumn: 10, gapRow: 6, alignItems: Align.FlexStart),
                        children: Sx.For(
                            each: DefinedColorSlots,
                            row: (slot, _) => ColorCard(slot))))
            ]),
        otherwise: () => PlaceholderTab("Color editing is available for car models."));

    private List<int> DefinedColorSlots()
    {
        _ = _revision.Value;
        return _phase.ActiveTab is { } tab
            ? _phase.GetColorDefinitions(tab).Select(definition => definition.Slot).ToList()
            : [];
    }

    private Color3? SlotColor(int slot)
    {
        _ = _revision.Value;
        return _phase.ActiveTab is { } tab ? _phase.GetSlotColor(tab, slot) : null;
    }

    private bool BothPrimaryColorsDefined()
    {
        _ = _revision.Value;
        return _phase.ActiveTab is { } tab
               && _phase.GetSlotColor(tab, 0) is not null
               && _phase.GetSlotColor(tab, 1) is not null;
    }

    private string MissingPrimaryColorsText()
    {
        _ = _revision.Value;
        var tab = _phase.ActiveTab;
        if (tab is null)
            return "[ No model open ]";

        var first = _phase.GetSlotColor(tab, 0) is not null;
        var second = _phase.GetSlotColor(tab, 1) is not null;
        return (first, second) switch
        {
            (false, false) => "[ First & Second Color not defined yet ]",
            (false, true) => "[ First Color not defined yet ]",
            (true, false) => "[ Second Color not defined yet ]",
            _ => ""
        };
    }

    private string DefineColorsButtonLabel()
    {
        _ = _revision.Value;
        var tab = _phase.ActiveTab;
        if (tab is null)
            return "DEFINE COLORS";

        var first = _phase.GetSlotColor(tab, 0) is not null;
        var second = _phase.GetSlotColor(tab, 1) is not null;
        return (first, second) switch
        {
            (false, false) => "DEFINE 1ST AND 2ND COLOR",
            (false, true) => "DEFINE 1ST COLOR",
            (true, false) => "DEFINE 2ND COLOR",
            _ => "DEFINE COLORS"
        };
    }

    private Node DefineColorsPrompt() => Sx.View(
        style: () => Sx.Styles(flexGrow: 1, flexDirection: FlexDirection.Column, alignItems: Align.Center,
            justifyContent: Justify.Center, gapRow: 8),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue,
                    horizontalAlignment: TextHorizontalAlignment.Center),
                text: MissingPrimaryColorsText),
            Button.Create(DefineColorsButtonLabel, ConfirmDefineColors, ButtonVariant.Primary,
                style: () => Sx.Styles(minWidth: 190))
        ]);

    private void ConfirmDefineColors()
    {
        ShowConfirm("Define Colors",
            "The editor will now find the first and second colors automatically. Make sure they are the correct colors!\n\nThese are also the colors that will be editable in the multiplayer game.",
            [
                new ConfirmAction("OK", () =>
                {
                    PushHistory(coalesce: false);
                    if (_phase.DefineDefaultColors() is { } error)
                        ShowError("Define Colors", error);
                })
            ]);
    }

    private Node ColorCard(int slot) => Sx.View(
        style: () => new Styles
        {
            Width = 250,
            FlexShrink = 0,
            FlexDirection = FlexDirection.Column,
            GapRow = 2,
            PaddingTop = 6,
            PaddingBottom = 6,
            PaddingLeft = 8,
            PaddingRight = 8,
            BackgroundColor = PanelSoft,
            BorderTop = 1,
            BorderBottom = 1,
            BorderLeft = 1,
            BorderRight = 1,
            BorderColor = Border,
            BorderRadius = 8,
        },
        children:
        [
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                    gapColumn: 8, gapRow: 8, height: 16),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 12, fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold, foregroundColor: Blue),
                        text: () => ModelEditorPhase.ColorSlotName(slot)),
                    Sx.View(style: () => Sx.Styles(width: 28, height: 12, flexShrink: 0,
                        borderRadius: 3,
                        borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                        borderColor: new Color(255, 255, 255, 70),
                        backgroundColor: SlotColor(slot) is { } color
                            ? new Color((byte)color.R, (byte)color.G, (byte)color.B)
                            : Theme.Transparent))
                ]),
            ColorChannelRow(slot, "R", 0),
            ColorChannelRow(slot, "G", 1),
            ColorChannelRow(slot, "B", 2)
        ]);

    private Node ColorChannelRow(int slot, string label, int channel) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
            gapColumn: 6, gapRow: 6, height: 20),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                layout: () => Sx.Styles(width: 10, flexShrink: 0),
                text: () => label),
            Controls.Slider(
                () => { _ = _revision.Value; return ColorChannelValue(slot, channel); },
                value =>
                {
                    PushHistory(coalesce: true);
                    _phase.SetSlotChannel(slot, channel,
                        (short)Math.Clamp((int)Math.Round(value), 0, 255));
                }, 0, 255, 1),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 160)),
                layout: () => Sx.Styles(width: 28, flexShrink: 0),
                text: () => { _ = _revision.Value; return ((int)ColorChannelValue(slot, channel)).ToString(); })
        ]);

    private double ColorChannelValue(int slot, int channel)
    {
        if (SlotColor(slot) is not { } color)
            return 0;

        return channel switch
        {
            0 => color.R,
            1 => color.G,
            _ => color.B
        };
    }

    private Node RenderTab() => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 8, flexGrow: 1,
            paddingTop: 14, paddingBottom: 14, paddingLeft: 16, paddingRight: 16),
        children:
        [
            Controls.Checkbox(
                () => { _ = _revision.Value; return GameSparker.devRenderTrackers; },
                () => { GameSparker.devRenderTrackers = !GameSparker.devRenderTrackers; Refresh(); },
                () => "Display collision boxes"),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 255, 255, 120)),
                text: () => "Shows the collision volumes of stage scenery.")
        ]);

    private Node ScaleTab() => Sx.Switch<(ModelEditorTab? Tab, int Version)>(
        Sx.Computed(() => (_phase.ActiveTab, _scaleStateVersion.Value)),
        new Dictionary<(ModelEditorTab? Tab, int Version), Func<Node>>(),
        () =>
        {
            if (_phase.ActiveTab is { } tab)
                LoadScaleFields(tab);
            return BuildScaleTab();
        });

    private Node BuildScaleTab()
    {
        if (_phase.ActiveTab is null)
            return PlaceholderTab("Open a model to scale or align it.");

        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.FlexStart,
                gapColumn: 16, gapRow: 6, flexGrow: 1, paddingTop: 6, paddingBottom: 6,
                paddingLeft: 8, paddingRight: 8, overflow: Overflow.Scroll),
            children:
            [
                ScaleGroup(),
                AlignGroup(),
                CompareGroup()
            ]);
    }

    private void LoadScaleFields(ModelEditorTab tab)
    {
        var settings = _phase.GetScaleSettings(tab);
        _scaleXField.Value = settings.X.ToString();
        _scaleYField.Value = settings.Y.ToString();
        _scaleZField.Value = settings.Z.ToString();
        _scaleDivField.Value = settings.DivValue.ToString();
        _divIsIdiv.Value = settings.IsIdiv;
    }

    private ModelEditorPhase.ScaleMode CurrentScaleMode()
    {
        _ = _revision.Value;
        return _phase.ActiveTab is { } tab
            ? _phase.GetScaleSettings(tab).Mode
            : ModelEditorPhase.ScaleMode.None;
    }

    private Node ScaleGroup() => Sx.View(
        style: () => Sx.Styles(width: 300, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 2),
        children:
        [
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                    gapColumn: 4, gapRow: 4),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: Blue),
                        layout: () => Sx.Styles(marginRight: 4, flexShrink: 0),
                        text: () => "SCALE"),
                    ScaleModeButton("SCALE X/Y/Z", true),
                    ScaleModeButton("DIV / IDIV", false)
                ]),
            Sx.Show(
                when: () => CurrentScaleMode() is ModelEditorPhase.ScaleMode.Percent
                    or ModelEditorPhase.ScaleMode.Mixed or ModelEditorPhase.ScaleMode.None,
                then: () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 2),
                    children:
                    [
                        ScaleAxisRow("X", _scaleXField),
                        ScaleAxisRow("Y", _scaleYField),
                        ScaleAxisRow("Z", _scaleZField),
                        Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 4, gapRow: 4),
                            children:
                            [
                                SmallButton("APPLY", ApplyPercentScale),
                                SmallButton("SCALE ALL", ScaleAllAxes),
                                SmallButton("RESET", ResetPercentScale)
                            ])
                    ])),
            Sx.Show(
                when: () => CurrentScaleMode() is ModelEditorPhase.ScaleMode.Div
                    or ModelEditorPhase.ScaleMode.Mixed,
                then: () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 2),
                    children:
                    [
                        Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                                gapColumn: 4, gapRow: 4),
                            children:
                            [
                                DivKindButton("DIV", false),
                                DivKindButton("IDIV", true),
                                ScaleNumberField(_scaleDivField),
                                SmallButton("APPLY", ApplyDivScale),
                                SmallButton("RESET", ResetDivScale)
                            ]),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 120)),
                            text: () => "div: size × n/10 · idiv: size × n/100")
                    ])),
            Sx.Show(
                when: () => CurrentScaleMode() == ModelEditorPhase.ScaleMode.Mixed,
                then: () => Sx.Text(style: () => Sx.TextStyles(fontSize: 10,
                        foregroundColor: new Color(255, 190, 90),
                        breakType: BreakType.Word, overflowBehavior: OverflowBehavior.ContinueVertically),
                    layout: () => Sx.Styles(width: "100%"),
                    text: () => "ScaleX/Y/Z and div/idiv are both present; choosing a mode removes the other."))
        ]);

    private Node ScaleModeButton(string label, bool percent)
    {
        bool Active()
        {
            _ = _revision.Value;
            var mode = _phase.ActiveTab is { } tab ? _phase.GetScaleSettings(tab).Mode : ModelEditorPhase.ScaleMode.None;
            return percent
                ? mode is ModelEditorPhase.ScaleMode.Percent or ModelEditorPhase.ScaleMode.Mixed or ModelEditorPhase.ScaleMode.None
                : mode is ModelEditorPhase.ScaleMode.Div or ModelEditorPhase.ScaleMode.Mixed;
        }

        return Button.Create(() => label, () =>
        {
            _ = _revision.Value;
            if (_phase.ActiveTab is not { } tab)
                return;
            PushHistory(coalesce: false);
            if (_phase.UseScaleMode(percent) is { } error)
                ShowError("Scale", error);
            else
            {
                LoadScaleFields(tab);
                ScrollCodeToStart();
                Refresh();
            }
        }, active: Active, size: ButtonSize.Sm);
    }

    private Node ScaleAxisRow(string label, Signal<string> field) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
            gapColumn: 4, gapRow: 4, height: 20),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                layout: () => Sx.Styles(width: 10, flexShrink: 0),
                text: () => label),
            ScaleNumberField(field),
            SmallButton("-", () => StepScale(field, -5)),
            SmallButton("+", () => StepScale(field, 5))
        ]);

    private static Node ScaleNumberField(Signal<string> field, Action? onChanged = null)
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(width: 50, minWidth: 0, height: 20, minHeight: 0,
                flexShrink: 0, paddingTop: 1, paddingBottom: 1,
                paddingLeft: 4, paddingRight: 4, backgroundColor: Theme.Surface,
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                borderColor: Theme.SurfaceBorder, borderRadius: 4),
            text: () => field.Value,
            onchange: value =>
            {
                field.Value = value;
                onChanged?.Invoke();
            });
        input.TextStyles = Sx.TextStyles(fontSize: 10);
        return input;
    }

    private void StepScale(Signal<string> field, int delta)
    {
        if (!int.TryParse(field.Value, out var value))
            return;

        field.Value = Math.Clamp(value + delta, 1, 1000).ToString();
        ApplyPercentScale();
    }

    private void ApplyPercentScale()
    {
        if (!TryParseScaleField(_scaleXField, out var x)
            || !TryParseScaleField(_scaleYField, out var y)
            || !TryParseScaleField(_scaleZField, out var z))
        {
            ShowError("Scale", "Scale values must be whole numbers.");
            return;
        }

        PushHistory(coalesce: false);
        if (_phase.SetScalePercent(x, y, z) is { } error)
            ShowError("Scale", error);
        else if (_phase.ActiveTab is { } tab)
        {
            LoadScaleFields(tab);
            ScrollCodeToStart();
        }
    }

    private void ScaleAllAxes()
    {
        if (!TryParseScaleField(_scaleXField, out var x))
        {
            ShowError("Scale", "Scale X must be a whole number.");
            return;
        }

        _scaleYField.Value = x.ToString();
        _scaleZField.Value = x.ToString();
        ApplyPercentScale();
    }

    private void ResetPercentScale()
    {
        _scaleXField.Value = "100";
        _scaleYField.Value = "100";
        _scaleZField.Value = "100";
        ApplyPercentScale();
    }

    private void ApplyDivScale()
    {
        if (!int.TryParse(_scaleDivField.Value, out var value))
        {
            ShowError("Scale", "Divider must be a whole number.");
            return;
        }

        PushHistory(coalesce: false);
        if (_phase.SetScaleDiv(_divIsIdiv.Value, value) is { } error)
            ShowError("Scale", error);
        else if (_phase.ActiveTab is { } tab)
        {
            LoadScaleFields(tab);
            ScrollCodeToStart();
        }
    }

    private static bool TryParseScaleField(Signal<string> field, out int value)
        => int.TryParse(field.Value, out value);

    private Node DivKindButton(string label, bool idiv) => Button.Create(() => label, () =>
    {
        if (_divIsIdiv.Value != idiv && int.TryParse(_scaleDivField.Value, out var value))
        {
            // div(n) scales by n/10, idiv(n) by n/100; convert so the size stays the same.
            var converted = idiv ? value * 10 : Math.Max(1, value / 10);
            _scaleDivField.Value = Math.Clamp(converted, 1, 1000).ToString();
        }

        _divIsIdiv.Value = idiv;
    }, active: () => _divIsIdiv.Value == idiv, size: ButtonSize.Sm);

    private void ResetDivScale()
    {
        _scaleDivField.Value = (_divIsIdiv.Value ? 100 : 10).ToString();
        ApplyDivScale();
    }

    private Node AlignGroup() => Sx.View(
        style: () => Sx.Styles(width: 260, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 2),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: Blue),
                layout: () => Sx.Styles(height: 14, flexShrink: 0),
                text: () => "ALIGN"),
            AlignRow("X", 0),
            AlignRow("Y", 1),
            AlignRow("Z", 2),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 4, gapRow: 4),
                children:
                [
                    SmallButton("RESET ALIGN", ResetAlign)
                ])
        ]);

    private Node AlignRow(string label, int axis) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
            gapColumn: 4, gapRow: 4, height: 20),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                layout: () => Sx.Styles(width: 10, flexShrink: 0),
                text: () => label),
            SmallButton("ROT 90°", () => TransformAlign(axis, 0, rotate: true), minWidth: 64),
            SmallButton("-10", () => TransformAlign(axis, -10, rotate: false)),
            SmallButton("+10", () => TransformAlign(axis, 10, rotate: false))
        ]);

    private void TransformAlign(int axis, int delta, bool rotate)
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is null)
            return;

        PushHistory(coalesce: false);
        var error = rotate ? _phase.RotateModel(axis) : _phase.OffsetModel(axis, delta);
        if (error is not null)
            ShowError("Align", error);
    }

    private void ResetAlign()
    {
        _ = _revision.Value;
        PushHistory(coalesce: false);
        if (_phase.ResetAlignment() is { } error)
            ShowError("Align", error);
        Refresh();
    }

    private Node CompareGroup() => Sx.View(
        style: () => Sx.Styles(width: 285, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 3),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: Blue),
                layout: () => Sx.Styles(height: 14, flexShrink: 0),
                text: () => "COMPARE"),
            Controls.Checkbox(
                () => { _ = _revision.Value; return _phase.ActiveTab?.ShowReferenceOverlay == true; },
                () => { _ = _revision.Value; _phase.SetReferenceOverlay(_phase.ActiveTab?.ShowReferenceOverlay != true); },
                () => "Show reference car"),
            Sx.Show(
                when: () => { _ = _revision.Value; return _phase.ActiveTab?.ShowReferenceOverlay == true; },
                then: () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
                    children:
                    [
                        Controls.Dropdown(
                            options: () => { _ = _revision.Value; return BackendGameSparker.CarRads; },
                            value: () => { _ = _revision.Value; return _phase.ActiveTab?.ReferenceCarIndex ?? 0; },
                            onSelect: _phase.SetReferenceCar),
                        Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                                gapColumn: 6, gapRow: 6),
                            children:
                            [
                                Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                                    layout: () => Sx.Styles(width: 54, flexShrink: 0),
                                    text: () => "OPACITY"),
                                Controls.Slider(
                                    () => { _ = _revision.Value; return _phase.ActiveTab?.ReferenceOpacity ?? 0.5f; },
                                    v => { _phase.SetReferenceOpacity(v); Refresh(); }, 0.1, 1.0, 0.05),
                                Sx.Text(style: () => Sx.TextStyles(fontSize: 10,
                                        foregroundColor: new Color(255, 255, 255, 160)),
                                    layout: () => Sx.Styles(width: 34, flexShrink: 0),
                                    text: () => { _ = _revision.Value; return $"{(_phase.ActiveTab?.ReferenceOpacity ?? 0.5f) * 100:F0}%"; })
                            ])
                    ]))
        ]);

    private static Node SmallButton(string label, Action action, float minWidth = 30) =>
        Button.Create(() => label, action, size: ButtonSize.Sm,
            style: () => Sx.Styles(minWidth: minWidth, paddingLeft: 6, paddingRight: 6,
                paddingTop: 3, paddingBottom: 3, flexShrink: 0));

    private Node TestDriveTab() => Sx.Show(
        when: () => { _ = _revision.Value; return _phase.ActiveTab is { ModelType: ModelEditorTab.ModelTypeEnum.Car }; },
        then: () => Sx.View(
            style: () => Sx.Styles(flexGrow: 1, flexDirection: FlexDirection.Column,
                gapRow: 10, paddingTop: 16, paddingBottom: 16, paddingLeft: 20, paddingRight: 20),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold,
                    foregroundColor: Blue), text: () => "TEST DRIVE THE CAR"),
                Sx.View(style: () => Sx.Styles(width: 230),
                    children: Controls.Dropdown(
                        () => TestDriveModes,
                        () => _testDriveMode.Value,
                        index => _testDriveMode.Value = index,
                        width: 230)),
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row),
                    children: Button.Create(() => "TEST DRIVE!", BeginTestDrive,
                        ButtonVariant.Primary, size: ButtonSize.Md)),
                Sx.Text(style: () => Sx.TextStyles(fontSize: 10,
                        foregroundColor: new Color(255, 255, 255, 120),
                        breakType: BreakType.Word, overflowBehavior: OverflowBehavior.ContinueVertically),
                    layout: () => Sx.Styles(width: 460))
            ]),
        otherwise: () => PlaceholderTab("Test drive is available for car models."));

    private void BeginTestDrive()
    {
        if (_statsApplyPending && ReferenceEquals(_phase.ActiveTab, _statsApplyTab))
        {
            _statsApplyPending = false;
            ApplyStats(reportErrors: false, coalesce: true);
        }

        if (_rimApplyPending && ReferenceEquals(_phase.ActiveTab, _rimApplyTab))
        {
            _rimApplyPending = false;
            ApplyRims(live: true);
        }

        if (_phase.StartTestDrive(_testDriveMode.Value == 1) is { } error)
            ShowError("Test Drive", error);
    }

    // ── Stats & physics ────────────────────────────────────────────────

    private static readonly string[] TestDriveModes = ["Alone", "With other cars"];

    private static readonly string[] EngineOptions =
    [
        "Normal Engine", "V8 Engine", "Retro Engine", "Power Engine",
        "Diesel Engine", "Track Engine", "Rocket Engine"
    ];

    private static readonly string[] StatKeys =
    [
        "class", "name", "enginesignature",
        "switsX", "switsY", "switsZ", "acelfX", "acelfY", "acelfZ",
        "handb", "airs", "airc", "turn", "grip", "bounce", "simag", "moment",
        "comprad", "push", "revpush", "lift", "revlift", "powerloss", "flipy",
        "msquash", "clrad", "dammult", "maxmag", "dishandle", "outdam",
        "turnradius", "roadgrip", "offroadgrip", "offtrackgrip"
    ];

    private static Dictionary<string, Signal<string>> CreateStatSignals()
    {
        var fields = new Dictionary<string, Signal<string>>(StringComparer.Ordinal);
        foreach (var key in StatKeys)
            fields[key] = new Signal<string>("");
        return fields;
    }

    private Node StatsTab() => Sx.Switch<(ModelEditorTab? Tab, int Version)>(
        Sx.Computed(() => (_phase.ActiveTab, _statsStateVersion.Value)),
        new Dictionary<(ModelEditorTab? Tab, int Version), Func<Node>>(),
        () =>
        {
            var tab = _phase.ActiveTab;
            if (tab is not { ModelType: ModelEditorTab.ModelTypeEnum.Car })
                return PlaceholderTab("Stats editing is available for car models.");
            if (tab.Object is null)
                return PlaceholderTab("The model must parse before stats can be edited.");

            LoadStatFields(tab);
            return Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexGrow: 1,
                    gapColumn: 14, gapRow: 6, paddingTop: 3, paddingBottom: 3,
                    paddingLeft: 10, paddingRight: 10, overflow: Overflow.Visible),
                children:
                [
                    StatGroup("CAR", [
                        StatRow("Class (class)", "class", 80, 150),
                        StatRow("Name (name)", "name", 80, 150),
                        StatEngineRow(),
                        StatRow("Strength (moment)", "moment", 50, 150),
                        StatRow("Durability (maxmag)", "maxmag", 60, 150),
                        StatRow("Damage Mult (dammult)", "dammult", 50, 150)
                    ], width: 300),
                    StatGroup("PERFORMANCE", [
                        StatTripleRow("Speed 1/2/3 (swits)", "switsX", "switsY", "switsZ"),
                        StatTripleRow("Accel 1/2/3 (acelf)", "acelfX", "acelfY", "acelfZ"),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 9,
                                foregroundColor: new Color(255, 255, 255, 110)),
                            text: () => "Values are 1st, 2nd and 3rd gear.")
                    ]),
                    StatGroup("COMBAT", [
                        StatRow("Pushes Others (push)", "push", 50, 160),
                        StatRow("Gets Pushed (revpush)", "revpush", 50, 160),
                        StatRow("Lifts Others (lift)", "lift", 50, 160),
                        StatRow("Gets Lifted (revlift)", "revlift", 50, 160)
                    ]),
                    StatGroup("STAT BARS (UI)", [
                        StatRow("Endurance (outdam)", "outdam", 50, 160),
                        StatRow("Steering Rating (dishandle)", "dishandle", 50, 160),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 9,
                                foregroundColor: new Color(255, 255, 255, 110),
                                breakType: BreakType.Word, overflowBehavior: OverflowBehavior.ContinueVertically),
                            layout: () => Sx.Styles(width: 210),
                            text: () => "Used by the car-select meters only; no physics effect.")
                    ]),
                    StatActions()
                ]);
        });

    private Node PhysicsTab() => Sx.Switch<(ModelEditorTab? Tab, int Version)>(
        Sx.Computed(() => (_phase.ActiveTab, _statsStateVersion.Value)),
        new Dictionary<(ModelEditorTab? Tab, int Version), Func<Node>>(),
        () =>
        {
            var tab = _phase.ActiveTab;
            if (tab is not { ModelType: ModelEditorTab.ModelTypeEnum.Car })
                return PlaceholderTab("Physics editing is available for car models.");
            if (tab.Object is null)
                return PlaceholderTab("The model must parse before physics can be edited.");

            LoadStatFields(tab);
            return Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexGrow: 1,
                    gapColumn: 14, gapRow: 6, paddingTop: 3, paddingBottom: 3,
                    paddingLeft: 10, paddingRight: 10, overflow: Overflow.Visible),
                children:
                [
                    StatGroup("HANDLING", [
                        StatRow("Handbrake (handb)", "handb", 50, 150),
                        StatRow("Turn Sens (turn)", "turn", 50, 150),
                        StatRow("Tire Grip (grip)", "grip", 50, 150),
                        StatRow("Bouncing (bounce)", "bounce", 50, 150)
                    ]),
                    StatGroup("AIR & MOTION", [
                        StatRow("Aerial Rotation (airs)", "airs", 50, 170),
                        StatRow("Aerial Control (airc)", "airc", 50, 170),
                        StatRow("Power Loss (powerloss)", "powerloss", 60, 170),
                        StatRow("Turn Radius (turnradius)", "turnradius", 60, 170),
                        StatRow("Dust (simag, visual)", "simag", 50, 170),
                        StatRow("Roof Height (flipy, visual)", "flipy", 50, 170)
                    ]),
                    StatGroup("COLLISION", [
                        StatRow("Hitbox Radius (comprad)", "comprad", 50, 170),
                        StatRow("Collision Radius (clrad)", "clrad", 60, 170),
                        StatRow("Roof Damage (msquash)", "msquash", 60, 170)
                    ]),
                    StatGroup("SURFACES", [
                        StatRow("Road Grip (roadgrip)", "roadgrip", 50, 170),
                        StatRow("Offroad Grip (offroadgrip)", "offroadgrip", 50, 170),
                        StatRow("Offtrack Grip (offtrackgrip)", "offtrackgrip", 50, 170),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 10,
                                foregroundColor: new Color(255, 255, 255, 110)),
                            text: () => "Optional surface-specific grip overrides")
                    ]),
                    StatActions()
                ]);
        });

    private static Node StatGroup(string title, IReadOnlyList<Node> rows, float width = 240) => Sx.View(
        style: () => Sx.Styles(width: width, flexShrink: 0, flexDirection: FlexDirection.Column,
            gapRow: 2),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold,
                foregroundColor: Blue), text: () => title),
            ..rows
        ]);

    private Node StatRow(string label, string key, float inputWidth, float labelWidth) =>
        Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                gapColumn: 6, gapRow: 2, height: 17, flexShrink: 0),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                    layout: () => Sx.Styles(width: labelWidth, flexShrink: 0),
                    text: () => label),
                StatInput(key, inputWidth)
            ]);

    private Node StatEngineRow() => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
            gapColumn: 6, gapRow: 2, height: 20, flexShrink: 0),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                layout: () => Sx.Styles(width: 150, flexShrink: 0),
                text: () => "Engine (enginsignature)"),
            Sx.View(style: () => Sx.Styles(width: 144, flexShrink: 0),
                children: Controls.Dropdown(
                    () => EngineOptions,
                    EngineIndex,
                    index =>
                    {
                        _statFields["enginesignature"].Value = index.ToString(CultureInfo.InvariantCulture);
                        ScheduleStatsApply();
                    },
                    width: 144,
                    compact: true))
        ]);

    private int EngineIndex()
    {
        var value = _statFields["enginesignature"].Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? Math.Clamp(index, 0, EngineOptions.Length - 1)
            : 0;
    }

    private Node StatTripleRow(string label, string key1, string key2, string key3) =>
        Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                gapColumn: 6, gapRow: 2, height: 17, flexShrink: 0),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                    layout: () => Sx.Styles(width: 116, flexShrink: 0),
                    text: () => label),
                StatInput(key1, 34),
                StatInput(key2, 34),
                StatInput(key3, 34)
            ]);

    private Node StatInput(string key, float width)
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(width: width, minWidth: 0, height: 16, minHeight: 0,
                flexShrink: 0, paddingTop: 1, paddingBottom: 1,
                paddingLeft: 4, paddingRight: 4, backgroundColor: Theme.Surface,
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                borderColor: Theme.SurfaceBorder, borderRadius: 4),
            text: () => _statFields[key].Value,
            onchange: value =>
            {
                _statFields[key].Value = value;
                ScheduleStatsApply();
            });
        input.TextStyles = Sx.TextStyles(fontSize: 10);
        return input;
    }

    private Node StatActions() => Sx.View(
        style: () => Sx.Styles(width: 112, flexShrink: 0, flexDirection: FlexDirection.Column,
            gapRow: 4, alignSelf: Align.Center),
        children:
        [
            SmallButton("APPLY", ApplyStatsNow, minWidth: 72),
            SmallButton("REVERT", RevertStats, minWidth: 72),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 110),
                    breakType: BreakType.Word, overflowBehavior: OverflowBehavior.ContinueVertically),
                layout: () => Sx.Styles(width: 100),
                text: () => "Changes apply automatically")
        ]);

    private void ScheduleStatsApply()
    {
        _statsApplyPending = true;
        _statsApplyTab = _phase.ActiveTab;
        _statsApplyDue = System.Environment.TickCount64 + 350;
    }

    private void ApplyStatsNow()
    {
        _statsApplyPending = false;
        ApplyStats(reportErrors: true, coalesce: false);
    }

    private void RevertStats()
    {
        _statsApplyPending = false;
        if (_phase.ActiveTab is { ModelType: ModelEditorTab.ModelTypeEnum.Car, Object: not null } tab)
            LoadStatFields(tab);
    }

    private void ApplyStats(bool reportErrors, bool coalesce)
    {
        var tab = _phase.ActiveTab;
        if (tab is not { ModelType: ModelEditorTab.ModelTypeEnum.Car } || tab.Object is null)
            return;

        var (definitions, error) = BuildStatDefinitions();
        if (error is not null)
        {
            if (reportErrors)
                ShowError("Stats", error);
            return;
        }

        if (definitions.Count == 0)
            return;

        PushHistory(coalesce);
        _statsApplying = true;
        try
        {
            if (_phase.SetStats(definitions) is { } message && reportErrors)
                ShowError("Stats", message);
        }
        finally
        {
            _statsApplying = false;
        }
    }

    private (List<string> Definitions, string? Error) BuildStatDefinitions()
    {
        var definitions = new List<string>();
        string? error = null;
        var invariant = CultureInfo.InvariantCulture;

        string? Field(string key)
        {
            var value = _statFields[key].Value.Trim();
            return value.Length == 0 ? null : value;
        }

        bool AddInt(string key, string directive, string label)
        {
            var value = Field(key);
            if (value is null)
                return true;
            if (!int.TryParse(value, NumberStyles.Integer, invariant, out var parsed))
            {
                error = $"{label} must be a whole number.";
                return false;
            }

            definitions.Add($"{directive}({parsed})");
            return true;
        }

        bool AddSByte(string key, string directive, string label)
        {
            var value = Field(key);
            if (value is null)
                return true;
            if (!sbyte.TryParse(value, NumberStyles.Integer, invariant, out var parsed))
            {
                error = $"{label} must be a whole number between -128 and 127.";
                return false;
            }

            definitions.Add($"{directive}({parsed})");
            return true;
        }

        bool AddFixed(string key, string directive, string label)
        {
            var value = Field(key);
            if (value is null)
                return true;
            if (!double.TryParse(value, NumberStyles.Float, invariant, out var parsed))
            {
                error = $"{label} must be a number.";
                return false;
            }

            definitions.Add($"{directive}({parsed.ToString("0.######", invariant)})");
            return true;
        }

        var className = Field("class");
        if (className is not null)
            definitions.Add($"class({className})");

        var name = Field("name");
        if (name is not null)
            definitions.Add($"name({name})");

        AddSByte("enginesignature", "enginsignature", "Engine");

        var speedLow = Field("switsX");
        var speedMed = Field("switsY");
        var speedHigh = Field("switsZ");
        if (speedLow is not null || speedMed is not null || speedHigh is not null)
        {
            if (speedLow is null || speedMed is null || speedHigh is null
                || !int.TryParse(speedLow, NumberStyles.Integer, invariant, out var low)
                || !int.TryParse(speedMed, NumberStyles.Integer, invariant, out var med)
                || !int.TryParse(speedHigh, NumberStyles.Integer, invariant, out var high))
            {
                error = "Speed needs all three whole-number values.";
            }
            else
            {
                definitions.Add($"swits({low},{med},{high})");
            }
        }

        var accelHigh = Field("acelfX");
        var accelMed = Field("acelfY");
        var accelLow = Field("acelfZ");
        if (accelHigh is not null || accelMed is not null || accelLow is not null)
        {
            if (accelHigh is null || accelMed is null || accelLow is null
                || !double.TryParse(accelHigh, NumberStyles.Float, invariant, out var high)
                || !double.TryParse(accelMed, NumberStyles.Float, invariant, out var med)
                || !double.TryParse(accelLow, NumberStyles.Float, invariant, out var low))
            {
                error = "Acceleration needs all three numeric values.";
            }
            else
            {
                definitions.Add($"acelf({high.ToString("0.######", invariant)},"
                    + $"{med.ToString("0.######", invariant)},"
                    + $"{low.ToString("0.######", invariant)})");
            }
        }

        AddInt("handb", "handb", "Handbrake");
        AddFixed("airs", "airs", "Aerial rotation");
        AddInt("airc", "airc", "Aerial control");
        AddInt("turn", "turn", "Turn sensitivity");
        AddFixed("grip", "grip", "Tire grip");
        AddFixed("bounce", "bounce", "Bouncing");
        AddFixed("simag", "simag", "Dust");
        AddFixed("moment", "moment", "Strength");
        AddFixed("comprad", "comprad", "Comp radius");
        AddFixed("push", "push", "Push");
        AddFixed("revpush", "revpush", "Rev push");
        AddInt("lift", "lift", "Lift");
        AddInt("revlift", "revlift", "Rev lift");
        AddInt("powerloss", "powerloss", "Power loss");
        AddInt("flipy", "flipy", "Roof height");
        AddInt("msquash", "msquash", "Roof damage");
        AddInt("clrad", "clrad", "Crash radius");
        AddFixed("dammult", "dammult", "Damage multiplier");
        AddInt("maxmag", "maxmag", "Durability");
        AddFixed("dishandle", "dishandle", "Visual steering");
        AddFixed("outdam", "outdam", "Out damage");
        AddInt("turnradius", "turnradius", "Turn radius");
        AddFixed("roadgrip", "roadgrip", "Road grip");
        AddFixed("offroadgrip", "offroadgrip", "Offroad grip");
        AddFixed("offtrackgrip", "offtrackgrip", "Offtrack grip");

        return (definitions, error);
    }

    private void LoadStatFields(ModelEditorTab tab)
    {
        // If the tab is being remounted while an edit is still debounced, commit it
        // before reading the source back, or the rebuild would discard it.
        if (_statsApplyPending && ReferenceEquals(tab, _statsApplyTab))
        {
            _statsApplyPending = false;
            ApplyStats(reportErrors: false, coalesce: true);
        }

        if (tab.Object?.Rad.Stats is not { } stats)
            return;

        SetStat("class", ReadDirectiveValue(tab.TextContent, "class"));
        SetStat("name", stats.Name);
        SetStat("enginesignature", stats.Enginsignature.ToString(CultureInfo.InvariantCulture));
        SetStat("switsX", FormatStatInt(stats.Swits.X));
        SetStat("switsY", FormatStatInt(stats.Swits.Y));
        SetStat("switsZ", FormatStatInt(stats.Swits.Z));
        SetStat("acelfX", FormatStatFixed(stats.Acelf.X));
        SetStat("acelfY", FormatStatFixed(stats.Acelf.Y));
        SetStat("acelfZ", FormatStatFixed(stats.Acelf.Z));
        SetStat("handb", FormatStatInt(stats.Handb));
        SetStat("airs", FormatStatFixed(stats.Airs));
        SetStat("airc", FormatStatInt(stats.Airc));
        SetStat("turn", stats.Turn == fix64.MinValue ? "" : ((int)stats.Turn).ToString(CultureInfo.InvariantCulture));
        SetStat("grip", FormatStatFixed(stats.Grip));
        SetStat("bounce", FormatStatFixed(stats.Bounce));
        SetStat("simag", FormatStatFixed(stats.Simag));
        SetStat("moment", FormatStatFixed(stats.Moment));
        SetStat("comprad", FormatStatFixed(stats.Comprad));
        SetStat("push", FormatStatFixed(stats.Push));
        SetStat("revpush", FormatStatFixed(stats.Revpush));
        SetStat("lift", FormatStatInt(stats.Lift));
        SetStat("revlift", FormatStatInt(stats.Revlift));
        SetStat("powerloss", FormatStatInt(stats.Powerloss));
        SetStat("flipy", FormatStatInt(stats.Flipy));
        SetStat("msquash", FormatStatInt(stats.Msquash));
        SetStat("clrad", FormatStatInt(stats.Clrad));
        SetStat("dammult", FormatStatFixed(stats.Dammult));
        SetStat("maxmag", FormatStatInt(stats.Maxmag));
        SetStat("dishandle", FormatStatFixed(stats.Dishandle));
        SetStat("outdam", FormatStatFixed(stats.Outdam));
        SetStat("turnradius", FormatStatInt(stats.TurnRadius));
        SetStat("roadgrip", FormatStatNullableFixed(stats.RoadGrip));
        SetStat("offroadgrip", FormatStatNullableFixed(stats.OffRoadGrip));
        SetStat("offtrackgrip", FormatStatNullableFixed(stats.OffTrackGrip));
    }

    private void SetStat(string key, string value)
    {
        if (_statFields[key].Value != value)
            _statFields[key].Value = value;
    }

    private static string FormatStatInt(int value)
        => value == int.MinValue ? "" : value.ToString(CultureInfo.InvariantCulture);

    private static string FormatStatFixed(fix64 value)
        => value == fix64.MinValue ? "" : ((float)value).ToString("0.######", CultureInfo.InvariantCulture);

    private static string FormatStatNullableFixed(fix64? value)
        => value is { } fixedValue ? FormatStatFixed(fixedValue) : "";

    private static string ReadDirectiveValue(string text, string key)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith(key + "(", StringComparison.Ordinal))
                continue;

            var close = line.LastIndexOf(')');
            return close > key.Length + 1 ? line[(key.Length + 1)..close].Trim() : "";
        }

        return "";
    }

    // ── Wheels ─────────────────────────────────────────────────────────

    private Node WheelsTab() => Sx.Switch<(ModelEditorTab? Tab, int Wheel, int Version)>(
        Sx.Computed(() => (_phase.ActiveTab, _selectedWheelIndex.Value, _wheelStateVersion.Value)),
        new Dictionary<(ModelEditorTab? Tab, int Wheel, int Version), Func<Node>>(),
        () =>
        {
            if (_phase.ActiveTab is { } tab)
            {
                LoadWheelFields(tab);
                LoadRimsFields(tab);
            }

            return BuildWheelsTab();
        });

    private Node BuildWheelsTab()
    {
        var tab = _phase.ActiveTab;
        if (tab is null)
            return PlaceholderTab("Open a car model to edit its wheels.");
        if (tab.ModelType != ModelEditorTab.ModelTypeEnum.Car)
            return PlaceholderTab("Wheel editing is available for car models.");

        var specs = _phase.GetWheelSpecs(tab);
        if (specs.Count == 0)
        {
            return Sx.View(
                style: () => Sx.Styles(flexGrow: 1, flexDirection: FlexDirection.Column,
                    alignItems: Align.Center, justifyContent: Justify.Center, gapRow: 10),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue,
                            horizontalAlignment: TextHorizontalAlignment.Center),
                        text: () => "No wheel definitions (w(...)) found in this model."),
                    Button.Create(() => "CREATE 4 BASIC WHEELS", AddBasicWheels, ButtonVariant.Primary,
                        style: () => Sx.Styles(minWidth: 220)),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 10,
                            foregroundColor: new Color(255, 255, 255, 120)),
                        text: () => "Creates front and rear pairs; adjust them in this tab.")
                ]);
        }

        var selected = Math.Clamp(_selectedWheelIndex.Value, 0, specs.Count - 1);

        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.FlexStart,
                gapColumn: 14, gapRow: 6, flexGrow: 1, paddingTop: 6, paddingBottom: 6,
                paddingLeft: 8, paddingRight: 8, overflow: Overflow.Scroll),
            children:
            [
                WheelList(specs),
                WheelEditor(specs[selected], selected),
                RimsEditor()
            ]);
    }

    private void AddBasicWheels()
    {
        PushHistory(coalesce: false);
        if (_phase.CreateBasicWheels() is { } error)
        {
            ShowError("Wheels", error);
            return;
        }

        _selectedWheelIndex.Value = 0;
        _wheelStateVersion.Value++;
        Refresh();
    }

    private void LoadWheelFields(ModelEditorTab tab)
    {
        var specs = _phase.GetWheelSpecs(tab);
        if (specs.Count == 0)
            return;

        var index = Math.Clamp(_selectedWheelIndex.Value, 0, specs.Count - 1);
        _selectedWheelIndex.Value = index;
        var spec = specs[index];
        _wheelX.Value = spec.X.ToString();
        _wheelY.Value = spec.Y.ToString();
        _wheelZ.Value = spec.Z.ToString();
        _wheelWidth.Value = spec.Width.ToString();
        _wheelHeight.Value = spec.Height.ToString();
        _wheelRotates.Value = spec.Rotates.ToString();
    }

    private void LoadRimsFields(ModelEditorTab tab)
    {
        var rims = _phase.GetRimsSpec(tab);
        _rimR.Value = rims.R.ToString();
        _rimG.Value = rims.G.ToString();
        _rimB.Value = rims.B.ToString();
        _rimSize.Value = rims.Size.ToString();
        _rimDepth.Value = rims.Depth.ToString();
    }

    private static List<int> WheelIndices(int count)
    {
        var result = new List<int>(count);
        for (var i = 0; i < count; i++)
            result.Add(i);
        return result;
    }

    private Node WheelList(IReadOnlyList<ModelEditorPhase.WheelSpec> specs) => Sx.View(
        style: () => Sx.Styles(width: 180, flexShrink: 0,
            flexDirection: FlexDirection.Column, gapRow: 3),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: Blue),
                text: () => $"WHEELS · {specs.Count}"),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexWrap: Wrap.Wrap,
                    gapColumn: 4, gapRow: 4),
                children: Sx.For(
                    each: () => WheelIndices(specs.Count),
                    row: (index, _) => WheelListButton(specs[index], index))),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 110)),
                text: () => "* custom mesh")
        ]);

    private Node WheelListButton(ModelEditorPhase.WheelSpec spec, int index) => Button.Create(
        () => spec.Custom ? $"W{index + 1}*" : $"W{index + 1}",
        () => _selectedWheelIndex.Value = index,
        active: () => _selectedWheelIndex.Value == index,
        size: ButtonSize.Sm,
        style: () => Sx.Styles(width: 40, minWidth: 40, paddingLeft: 0, paddingRight: 0,
            justifyContent: Justify.Center));

    private Node WheelEditor(ModelEditorPhase.WheelSpec spec, int index) => Sx.View(
        style: () => Sx.Styles(width: 340, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 2),
        children:
        [
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                    gapColumn: 6, gapRow: 6),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: Blue),
                        text: () => $"WHEEL {index + 1}"),
                    Sx.Show(
                        when: () => spec.Custom,
                        then: () => Sx.Text(style: () => Sx.TextStyles(fontSize: 10,
                            foregroundColor: new Color(120, 220, 160)), text: () => "CUSTOM MESH")),
                    Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                    SmallButton("APPLY", () => ApplyWheel(index), minWidth: 50)
                ]),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                    gapColumn: 6, gapRow: 6, height: 20),
                children:
                [
                    WheelField("X", _wheelX, onChanged: () => ApplyWheel(index, live: true)),
                    WheelField("Y", _wheelY, onChanged: () => ApplyWheel(index, live: true)),
                    WheelField("Z", _wheelZ, onChanged: () => ApplyWheel(index, live: true))
                ]),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                    gapColumn: 6, gapRow: 6, height: 20),
                children:
                [
                    WheelField("W", _wheelWidth, onChanged: () => ApplyWheel(index, live: true)),
                    WheelField("H", _wheelHeight, onChanged: () => ApplyWheel(index, live: true)),
                    WheelField("ROT", _wheelRotates, onChanged: () => ApplyWheel(index, live: true))
                ]),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexWrap: Wrap.Wrap,
                    alignItems: Align.Center, gapColumn: 6, gapRow: 4),
                children:
                [
                    Button.Create(() => "STEER", () => ToggleSteer(index),
                        active: () => _wheelRotates.Value == "11", size: ButtonSize.Sm,
                        style: () => Sx.Styles(flexShrink: 0)),
                    SmallButton("FLIP SIDE", () => FlipWheelSide(index), minWidth: 72),
                    Button.Create(() => "MIRROR", () => _mirrorWheelEdit.Value = !_mirrorWheelEdit.Value,
                        active: () => _mirrorWheelEdit.Value, size: ButtonSize.Sm,
                        style: () => Sx.Styles(flexShrink: 0))
                ]),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 120)),
                text: () => "ROT 11 = steering")
        ]);

    private static Node WheelField(string label, Signal<string> field, float labelWidth = 22,
        Action? onChanged = null) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
            gapColumn: 4, gapRow: 4),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                layout: () => Sx.Styles(width: labelWidth, flexShrink: 0),
                text: () => label),
            ScaleNumberField(field, onChanged)
        ]);

    private void ToggleSteer(int index)
    {
        _wheelRotates.Value = _wheelRotates.Value == "11" ? "0" : "11";
        ApplyWheel(index, live: true);
    }

    private void ApplyWheel(int index, bool live = false)
    {
        if (!int.TryParse(_wheelX.Value, out var x)
            || !int.TryParse(_wheelY.Value, out var y)
            || !int.TryParse(_wheelZ.Value, out var z)
            || !int.TryParse(_wheelWidth.Value, out var width)
            || !int.TryParse(_wheelHeight.Value, out var height)
            || !int.TryParse(_wheelRotates.Value, out var rotates))
        {
            if (!live)
                ShowError("Wheel", "Wheel values must be whole numbers.");
            return;
        }

        PushHistory(coalesce: live);
        if (_phase.SetWheels(BuildWheelEdits(index, x, y, z, rotates, width, height)) is { } error)
        {
            if (!live)
                ShowError("Wheel", error);
        }
        else if (!live && _phase.ActiveTab is { } tab)
        {
            LoadWheelFields(tab);
        }
    }

    private List<ModelEditorPhase.WheelEdit> BuildWheelEdits(int index, int x, int y, int z,
        int rotates, int width, int height)
    {
        var edits = new List<ModelEditorPhase.WheelEdit>(2)
        {
            new(index, x, y, z, rotates, width, height)
        };

        if (!_mirrorWheelEdit.Value || _phase.ActiveTab is not { } tab)
            return edits;

        var partner = _phase.FindMirrorPartner(tab, index);
        if (partner < 0 || partner == index)
            return edits;

        var specs = _phase.GetWheelSpecs(tab);
        if (partner >= specs.Count)
            return edits;

        var partnerSpec = specs[partner];
        edits.Add(new ModelEditorPhase.WheelEdit(partner, -x, y, z,
            partnerSpec.Rotates, partnerSpec.Width, partnerSpec.Height));
        return edits;
    }

    // ── Wheel gizmo ────────────────────────────────────────────────────

    private const float GizmoAxisLength = 64f;

    private Node WheelGizmoOverlay() => Sx.View(
        style: () => Sx.Styles(position: Position.Absolute, left: 0, top: 0,
            width: "100%", height: "100%", pointerEvents: false, zIndex: 5),
        children: new WheelGizmoCanvas(this));

    private IEnumerable<object?> WheelGizmoHandles() =>
    [
        GizmoHandle(0, _gizmoX, 0.55f),
        GizmoHandle(0, _gizmoX, 1f),
        GizmoHandle(1, _gizmoY, 0.55f),
        GizmoHandle(1, _gizmoY, 1f),
        GizmoHandle(2, _gizmoZ, 0.55f),
        GizmoHandle(2, _gizmoZ, 1f)
    ];

    private Node GizmoHandle(int axis, Signal<Vector2> vector, float along) => Sx.View(
        style: () => new Styles
        {
            Position = Position.Absolute,
            Left = GizmoHandlePosition(vector.Value, along).X,
            Top = GizmoHandlePosition(vector.Value, along).Y,
            Width = 26f / UiScale,
            Height = 26f / UiScale,
            BackgroundColor = Color.Transparent,
            ZIndex = 6,
            Display = GizmoHandleVisible(vector.Value) ? Display.Flex : Display.None,
        },
        onmousedown: e =>
        {
            e.StopPropagation();
            BeginGizmoDrag(axis, e.Position, vector.Value);
        });

    private static float UiScale => Math.Max(G.Scale, 0.01f);

    private bool GizmoHandleVisible(Vector2 axisVector)
        => _gizmoVisible.Value && axisVector.LengthSquared() > 0.25f;

    private Vector2 GizmoHandlePosition(Vector2 axisVector, float along)
    {
        var origin = _gizmoOrigin.Value;
        var position = axisVector.LengthSquared() < 0.0001f
            ? origin
            : origin + Vector2.Normalize(axisVector) * (GizmoAxisLength * along);

        return (position - new Vector2(13f, 13f)) / UiScale;
    }

    private sealed class WheelGizmoCanvas : Component
    {
        private readonly ModelEditorView _view;

        public WheelGizmoCanvas(ModelEditorView view)
        {
            _view = view;
            Styles = Sx.Styles(width: "100%", height: "100%", pointerEvents: false);
        }

        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            if (!_view._gizmoVisible.Value)
                return;

            var origin = _view._gizmoOrigin.Value;
            DrawAxis(origin, _view._gizmoX.Value, new Color(235, 80, 80));
            DrawAxis(origin, _view._gizmoY.Value, new Color(80, 210, 120));
            DrawAxis(origin, _view._gizmoZ.Value, new Color(80, 140, 255));

            var left = (int)MathF.Round(origin.X - 7f);
            var top = (int)MathF.Round(origin.Y - 7f);
            G.SetColor(new Color(235, 238, 245));
            G.FillRect(left, top, 14, 14);
            G.SetColor(new Color(20, 25, 35));
            G.DrawRect(left, top, 14, 14);
        }

        private static void DrawAxis(Vector2 origin, Vector2 axis, Color color)
        {
            if (axis.LengthSquared() < 0.25f)
                return;

            var direction = Vector2.Normalize(axis);
            var perpendicular = new Vector2(-direction.Y, direction.X);
            var start = origin + direction * 10f;
            var shaftEnd = origin + direction * 52f;
            var tip = origin + direction * GizmoAxisLength;
            const float halfWidth = 1.5f;

            Span<int> xs = stackalloc int[4];
            Span<int> ys = stackalloc int[4];
            xs[0] = (int)MathF.Round(start.X + perpendicular.X * halfWidth);
            ys[0] = (int)MathF.Round(start.Y + perpendicular.Y * halfWidth);
            xs[1] = (int)MathF.Round(shaftEnd.X + perpendicular.X * halfWidth);
            ys[1] = (int)MathF.Round(shaftEnd.Y + perpendicular.Y * halfWidth);
            xs[2] = (int)MathF.Round(shaftEnd.X - perpendicular.X * halfWidth);
            ys[2] = (int)MathF.Round(shaftEnd.Y - perpendicular.Y * halfWidth);
            xs[3] = (int)MathF.Round(start.X - perpendicular.X * halfWidth);
            ys[3] = (int)MathF.Round(start.Y - perpendicular.Y * halfWidth);

            G.SetColor(color);
            G.FillPolygon(xs, ys, 4);

            xs[0] = (int)MathF.Round(tip.X);
            ys[0] = (int)MathF.Round(tip.Y);
            xs[1] = (int)MathF.Round(shaftEnd.X + perpendicular.X * 6f);
            ys[1] = (int)MathF.Round(shaftEnd.Y + perpendicular.Y * 6f);
            xs[2] = (int)MathF.Round(shaftEnd.X - perpendicular.X * 6f);
            ys[2] = (int)MathF.Round(shaftEnd.Y - perpendicular.Y * 6f);
            G.FillPolygon(xs, ys, 3);
        }
    }

    private void BeginGizmoDrag(int axis, Vector2 position, Vector2 axisVector)
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return;

        var specs = _phase.GetWheelSpecs(tab);
        var index = _selectedWheelIndex.Value;
        if (index < 0 || index >= specs.Count)
            return;

        _gizmoAxis = axis;
        _gizmoDragStart = position;
        _gizmoAxisVector = axisVector;
        _gizmoStartSpec = specs[index];
    }

    public void WheelGizmoMouseMoved(int x, int y) => UpdateGizmoDrag(new Vector2(x, y));

    public void EndWheelGizmoDrag() => _gizmoAxis = -1;

    private void UpdateGizmoDrag(Vector2 position)
    {
        if (_gizmoAxis < 0 || _phase.ActiveTab is not { } tab)
            return;

        var lengthSquared = _gizmoAxisVector.LengthSquared();
        if (lengthSquared < 0.0001f)
            return;

        var index = _selectedWheelIndex.Value;
        var specs = _phase.GetWheelSpecs(tab);
        if (index < 0 || index >= specs.Count)
            return;

        var rawDelta = Vector2.Dot(position - _gizmoDragStart, _gizmoAxisVector) / lengthSquared;
        var delta = (int)Math.Round(rawDelta);
        var spec = _gizmoStartSpec;
        var x = spec.X;
        var y = spec.Y;
        var z = spec.Z;

        switch (_gizmoAxis)
        {
            case 0: x += delta; break;
            case 1: y += delta; break;
            default: z += delta; break;
        }

        PushHistory(coalesce: true);
        var edits = BuildWheelEdits(index, x, y, z, spec.Rotates, spec.Width, spec.Height);
        _phase.SetWheels(edits);

        _wheelX.Value = x.ToString();
        _wheelY.Value = y.ToString();
        _wheelZ.Value = z.ToString();
    }

    private void FlipWheelSide(int index)
    {
        if (!int.TryParse(_wheelWidth.Value, out var width))
            return;

        _wheelWidth.Value = (-width).ToString();
        ApplyWheel(index, live: true);
    }

    private Node RimsEditor() => Sx.View(
        style: () => Sx.Styles(width: 340, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 2),
        children:
        [
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                    gapColumn: 6, gapRow: 6),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: Blue),
                        text: () => "RIMS"),
                    Sx.View(style: () => Sx.Styles(width: 28, height: 14, flexShrink: 0,
                        borderRadius: 3,
                        borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                        borderColor: new Color(255, 255, 255, 70),
                        backgroundColor: RimSwatchColor())),
                    Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                    SmallButton("APPLY", () => ApplyRims(), minWidth: 50)
                ]),
            RimSliderRow("R", _rimR),
            RimSliderRow("G", _rimG),
            RimSliderRow("B", _rimB),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                    gapColumn: 6, gapRow: 6, height: 20),
                children:
                [
                    WheelField("SIZE", _rimSize, 30, ScheduleRimApply),
                    WheelField("DEPTH", _rimDepth, 34, ScheduleRimApply),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 120)),
                        text: () => "global")
                ])
        ]);

    private Node RimSliderRow(string label, Signal<string> channel) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
            gapColumn: 4, gapRow: 4, height: 20),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                layout: () => Sx.Styles(width: 10, flexShrink: 0),
                text: () => label),
            Controls.Slider(
                () => { _ = _revision.Value; return RimChannel(channel); },
                value =>
                {
                    channel.Value = Math.Clamp((int)Math.Round(value), 0, 255).ToString();
                    ScheduleRimApply();
                }, 0, 255, 1),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 160)),
                layout: () => Sx.Styles(width: 28, flexShrink: 0),
                text: () => { _ = _revision.Value; return ((int)RimChannel(channel)).ToString(); })
        ]);

    private static double RimChannel(Signal<string> signal)
        => int.TryParse(signal.Value, out var value) ? Math.Clamp(value, 0, 255) : 0;

    private Color RimSwatchColor()
    {
        static int Component(Signal<string> signal)
            => int.TryParse(signal.Value, out var value) ? Math.Clamp(value, 0, 255) : 0;

        return new Color(Component(_rimR), Component(_rimG), Component(_rimB));
    }

    private void ApplyRims(bool live = false)
    {
        if (!int.TryParse(_rimR.Value, out var r)
            || !int.TryParse(_rimG.Value, out var g)
            || !int.TryParse(_rimB.Value, out var b)
            || !int.TryParse(_rimSize.Value, out var size)
            || !int.TryParse(_rimDepth.Value, out var depth))
        {
            if (!live)
                ShowError("Rims", "Rim values must be whole numbers.");
            return;
        }

        PushHistory(coalesce: live);
        if (_phase.SetRims(r, g, b, size, depth) is { } error)
        {
            if (!live)
                ShowError("Rims", error);
        }
        else if (!live && _phase.ActiveTab is { } tab)
        {
            LoadRimsFields(tab);
        }
    }

    private Node PlaceholderTab(string message) => Sx.View(
        style: () => Sx.Styles(flexGrow: 1, paddingTop: 16, paddingBottom: 16, paddingLeft: 16, paddingRight: 16),
        children: Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue),
            text: () => message));

    // ── Tabs / files ───────────────────────────────────────────────────

    private void RequestCloseTab(int index)
    {
        if (index < 0)
            return;

        if (!_phase.TabHasUnsavedChanges(index))
        {
            _phase.PerformCloseTab(index);
            return;
        }

        var tab = _phase.Tabs[index];
        ShowConfirm("Unsaved Changes", $"Save changes to {System.IO.Path.GetFileName(tab.ModelPath)}?",
        [
            new ConfirmAction("Save", () =>
            {
                if (_phase.SaveTab(tab) is { } error)
                {
                    ShowError("Save Failed", error);
                    return;
                }
                _phase.PerformCloseTab(IndexOfTab(tab));
            }),
            new ConfirmAction("Don't Save", () => _phase.PerformCloseTab(IndexOfTab(tab))),
            new ConfirmAction("Cancel", () => { })
        ]);
    }

    public void RequestExit()
    {
        var dirty = _phase.Tabs.Where(tab => tab.TextEditorDirty && tab.ModelPath != null).ToList();
        if (dirty.Count == 0)
        {
            _phase.ExitModelViewer();
            return;
        }

        var names = string.Join(", ", dirty.Select(tab => System.IO.Path.GetFileName(tab.ModelPath)));
        ShowConfirm("Unsaved Changes", $"Save changes to: {names}?",
        [
            new ConfirmAction("Save All", () =>
            {
                foreach (var tab in dirty)
                    if (_phase.SaveTab(tab) is { } error)
                    {
                        ShowError("Save Failed", error);
                        return;
                    }
                _phase.ExitModelViewer();
            }),
            new ConfirmAction("Don't Save", _phase.ExitModelViewer),
            new ConfirmAction("Cancel", () => { })
        ]);
    }

    private void OpenNewModelDialog()
    {
        _newName.Value = "";
        _newImport.Value = false;
        _newReference.Value = 0;
        _newModelOpen.Value = true;
    }

    private void OpenLoadDialog()
    {
        _loadSearch.Value = "";
        RefreshLoadFiles();
        _loadOpen.Value = true;
    }

    private void RefreshLoadFiles()
    {
        _phase.RefreshUserModels();
        _loadFiles.Value = _phase.UserModelNames;
        Refresh();
    }

    /// <summary>Files in a category, filtered by the search box and sorted by name.</summary>
    private List<string> FilteredFiles(string prefix)
    {
        _ = _revision.Value;
        var query = _loadSearch.Value.Trim();

        return _loadFiles.Value
            .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(file => query.Length == 0 || file.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => System.IO.Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadSelectedModel(string path)
    {
        var openInNewTab = _loadOpenInTab.Value;

        void Load()
        {
            _loadOpen.Value = false;
            if (_phase.LoadModel(path, openInNewTab) is { } error)
                ShowError("Parse Error", error);
        }

        _ = _revision.Value;
        var tab = _phase.ActiveTab;
        if (!openInNewTab && tab != null && tab.TextEditorDirty && tab.ModelPath != null)
        {
            ShowConfirm("Unsaved Changes", $"Save changes to {System.IO.Path.GetFileName(tab.ModelPath)}?",
            [
                new ConfirmAction("Save", () =>
                {
                    if (_phase.SaveTab(tab) is { } error)
                    {
                        ShowError("Save Failed", error);
                        return;
                    }
                    Load();
                }),
                new ConfirmAction("Don't Save", Load),
                new ConfirmAction("Cancel", () => { })
            ]);
            return;
        }

        Load();
    }

    // ── Dialogs ────────────────────────────────────────────────────────

    private Node NewModelDialog() => Sx.Show(
        when: () => _newModelOpen.Value,
        then: () => Modal(
            () => _newModelOpen.Value = false,
            Sx.View(
                style: () => Sx.Styles(width: 470, flexDirection: FlexDirection.Column,
                    gapRow: 14, paddingTop: 22, paddingBottom: 22, paddingLeft: 24, paddingRight: 24,
                    backgroundColor: Panel, borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                    borderColor: Border, borderRadius: 12),
                onmousedown: e => e.StopPropagation(),
                children:
                [
                    DialogTitle("NEW MODEL", "Create a model in data/models/user."),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: TextBlue),
                        text: () => "MODEL TYPE"),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 8),
                        children:
                        [
                            TypeButton("CAR", true),
                            TypeButton("STAGE SCENERY", false)
                        ]),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: TextBlue),
                        text: () => "MODEL NAME"),
                    Controls.TextField(() => _newName.Value, value => _newName.Value = value,
                        () => "model_name"),
                    Controls.Checkbox(() => _newOpenInTab.Value, () => _newOpenInTab.Value = !_newOpenInTab.Value,
                        () => "Open in new tab"),
                    Controls.Checkbox(() => _newImport.Value, () => _newImport.Value = !_newImport.Value,
                        () => "Import reference model"),
                    Sx.Show(
                        when: () => _newImport.Value,
                        then: () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 6),
                            children:
                            [
                                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: TextBlue),
                                    text: () => "REFERENCE"),
                                Controls.Dropdown(
                                    options: () => _newIsCar.Value ? BackendGameSparker.CarRads : BackendGameSparker.StageRads,
                                    value: () => _newReference.Value,
                                    onSelect: index => _newReference.Value = index)
                            ])),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, justifyContent: Justify.FlexEnd,
                            gapColumn: 10, gapRow: 10, marginTop: 4),
                        children:
                        [
                            Button.Create(() => "CANCEL", () => _newModelOpen.Value = false,
                                variant: ButtonVariant.Ghost, style: () => Sx.Styles(minWidth: 100)),
                            Button.Create(() => "CREATE", CreateModel, ButtonVariant.Primary,
                                style: () => Sx.Styles(minWidth: 120))
                        ])
                ])));

    private Node TypeButton(string label, bool car)
    {
        return Button.Create(() => label, () =>
        {
            _newIsCar.Value = car;
            _newReference.Value = 0;
        }, active: () => _newIsCar.Value == car, size: ButtonSize.Sm,
            style: () => Sx.Styles(minWidth: 100));
    }

    private void CreateModel()
    {
        if (_phase.CreateNewModel(_newName.Value, _newIsCar.Value, _newImport.Value,
                _newReference.Value, _newOpenInTab.Value) is { } error)
        {
            ShowError("Create Model", error);
            return;
        }

        _newModelOpen.Value = false;
        _newName.Value = "";
        _newImport.Value = false;
    }

    private Node LoadModelDialog() => Sx.Show(
        when: () => _loadOpen.Value,
        then: () => Modal(
            () => _loadOpen.Value = false,
            Sx.View(
                style: () => Sx.Styles(width: 640, height: "76%", maxHeight: 700,
                    flexDirection: FlexDirection.Column, gapRow: 10, paddingTop: 20, paddingBottom: 20,
                    paddingLeft: 22, paddingRight: 22, backgroundColor: Panel, borderTop: 1, borderBottom: 1,
                    borderLeft: 1, borderRight: 1, borderColor: Border, borderRadius: 12),
                onmousedown: e => e.StopPropagation(),
                children:
                [
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.FlexStart,
                            gapColumn: 12, gapRow: 12),
                        children:
                        [
                            Sx.View(style: () => Sx.Styles(flexGrow: 1),
                                children: DialogTitle("OPEN MODEL", "Models in data/models/user. Click one to open it.")),
                            Button.Create(() => "REFRESH", RefreshLoadFiles, size: ButtonSize.Sm,
                                style: () => Sx.Styles(minWidth: 94, flexShrink: 0))
                        ]),
                    Controls.TextField(() => _loadSearch.Value, value => _loadSearch.Value = value,
                        () => "Search models..."),
                    Controls.Checkbox(() => _loadOpenInTab.Value, () => _loadOpenInTab.Value = !_loadOpenInTab.Value,
                        () => "Open in new tab"),
                    Sx.View(style: () => Sx.Styles(flexGrow: 1, minHeight: 0, overflow: Overflow.Scroll,
                            flexDirection: FlexDirection.Column, gapRow: 2, paddingTop: 4, paddingBottom: 4),
                        children:
                        [
                            Sx.Show(
                                when: () => _loadSearch.Value.Trim().Length == 0 && _loadFiles.Value.Count == 0,
                                then: () => Sx.Text(style: () => Sx.TextStyles(fontSize: 13,
                                        foregroundColor: new Color(255, 255, 255, 130)),
                                    layout: () => Sx.Styles(marginTop: 8),
                                    text: () => "No user models found in data/models/user.")),
                            Sx.Show(
                                when: () => _loadSearch.Value.Trim().Length > 0
                                          && FilteredFiles("cars/").Count == 0
                                          && FilteredFiles("stage/").Count == 0,
                                then: () => Sx.Text(style: () => Sx.TextStyles(fontSize: 13,
                                        foregroundColor: new Color(255, 255, 255, 130)),
                                    layout: () => Sx.Styles(marginTop: 8),
                                    text: () => $"No models match \"{_loadSearch.Value.Trim()}\".")),
                            FileSection("CARS", "cars/"),
                            FileSection("STAGE", "stage/")
                        ]),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                            gapColumn: 10, gapRow: 10),
                        children:
                        [
                            Sx.Text(style: () => Sx.TextStyles(fontSize: 11,
                                    foregroundColor: new Color(255, 255, 255, 120)),
                                layout: () => Sx.Styles(flexGrow: 1),
                                text: () => $"{FilteredFiles("cars/").Count + FilteredFiles("stage/").Count} models"),
                            Button.Create(() => "CANCEL", () => _loadOpen.Value = false,
                                variant: ButtonVariant.Ghost, style: () => Sx.Styles(minWidth: 100))
                        ])
                ])));

    private Node FileSection(string title, string prefix) => Sx.Show(
        when: () => FilteredFiles(prefix).Count > 0,
        then: () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 2),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold, foregroundColor: Blue),
                    layout: () => Sx.Styles(marginTop: 6, marginBottom: 2),
                    text: () => $"{title} · {FilteredFiles(prefix).Count}"),
                Sx.For(
                    each: () => FilteredFiles(prefix),
                    row: (file, _) => FileRow(file))
            ]));

    private Node FileRow(string file)
    {
        var hovered = new Signal<bool>(false);
        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                paddingTop: 7, paddingBottom: 7, paddingLeft: 10, paddingRight: 10, borderRadius: 5,
                backgroundColor: hovered.Value ? new Color(55, 90, 150, 150) : Theme.Transparent),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: _ => LoadSelectedModel("./data/models/user/" + file),
            children: Sx.Text(style: () => Sx.TextStyles(fontSize: 13,
                foregroundColor: hovered.Value ? new Color(255, 255, 255) : TextBlue),
                text: () => System.IO.Path.GetFileName(file)));
    }

    private Node PolygonEditorDialog() => Sx.Show(
        when: () => { _ = _revision.Value; return _phase.ActiveTab is { ShowPolygonEditor: true }; },
        then: () => Sx.View(
            style: () => LeftPanelStyle with { ZIndex = 20 },
            children:
            [
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                        gapColumn: 6, gapRow: 6, paddingTop: 8, paddingBottom: 8,
                        paddingLeft: 10, paddingRight: 10, flexShrink: 0),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontFamily: FontFamily.Adventure,
                            fontStyle: FontStyle.Bold, foregroundColor: Blue),
                            text: PolygonTitle),
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 11,
                                foregroundColor: new Color(255, 200, 100)),
                            layout: () => Sx.Styles(flexShrink: 0),
                            text: () => { _ = _revision.Value; return _phase.ActiveTab?.PolygonEditorDirty == true ? "MODIFIED" : ""; }),
                        Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                        CodeAction("APPLY", ApplyPolygonEditor),
                        CodeAction("CANCEL", RequestClosePolygonEditor)
                    ]),
                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: TextBlue),
                    layout: () => Sx.Styles(paddingLeft: 10, paddingRight: 10, paddingBottom: 4,
                        flexShrink: 0),
                    text: PolygonInfo),
                Sx.View(style: () => Sx.Styles(flexGrow: 1, minHeight: 0, paddingLeft: 8,
                        paddingRight: 8),
                    children: CreateTextArea(
                        () => Sx.Styles(flexGrow: 1, minHeight: 0),
                        () => { _ = _polygonVersion.Value; return _phase.ActiveTab?.PolygonEditorContent ?? ""; },
                        value =>
                        {
                            if (_phase.ActiveTab is not { } tab)
                                return;
                            tab.PolygonEditorContent = value;
                            tab.PolygonEditorDirty = true;
                            Refresh();
                        })),
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                        gapColumn: 6, gapRow: 6, flexWrap: Wrap.Wrap,
                        paddingTop: 6, paddingBottom: 8, paddingLeft: 10, paddingRight: 10),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                            text: () => "SPECIAL FLAGS"),
                        ..PolygonFlagButtons()
                    ])
            ]));

    private IEnumerable<object?> PolygonFlagButtons()
    {
        _ = _revision.Value;
        var collision = _phase.ActiveTab?.PolygonEditorIsCollision == true;
        var flags = collision ? new[] { "dam()", "notwall()" } : ["gr(-18)", "decal", "noOutline()"];
        return flags.Select(flag => (object?)Button.Create(() => flag, () => _phase.InsertPolygonFlag(flag),
            size: ButtonSize.Sm));
    }

    private string PolygonTitle()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return "EDIT POLYGON";

        if (tab.PolygonEditorIsCollision)
            return "EDIT COLLISION";
        return tab.PolygonEditorIsWheel
            ? $"EDIT WHEEL {tab.PolygonEditorWheelIndex + 1} POLYGON"
            : "EDIT POLYGON";
    }

    private string PolygonInfo()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return "";

        if (tab.PolygonEditorIsCollision)
            return $"Editing collision {tab.SelectedCollisionIndex + 1} of {tab.Object?.Boxes.Count ?? 0}";

        if (tab.PolygonEditorIsWheel)
        {
            var wheelIndex = tab.PolygonEditorWheelIndex;
            if (tab.Object is not null && wheelIndex >= 0 && wheelIndex < tab.Object.WheelObjects.Count)
                return $"Editing wheel {wheelIndex + 1}, polygon {tab.PolygonEditorLastSelectedIndex + 1} of {tab.Object.WheelObjects[wheelIndex].Mesh.Polys.Length}";
            return $"Editing wheel polygon {tab.PolygonEditorLastSelectedIndex + 1}";
        }

        return $"Editing polygon {tab.PolygonEditorLastSelectedIndex + 1} of {tab.Object?.Mesh.Polys.Length ?? 0}";
    }

    private void ApplyPolygonEditor()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return;

        if (string.IsNullOrWhiteSpace(tab.PolygonEditorContent))
        {
            var itemType = tab.PolygonEditorIsCollision ? "collision"
                : tab.PolygonEditorIsWheel ? "wheel polygon" : "polygon";
            var index = tab.PolygonEditorIsCollision ? tab.SelectedCollisionIndex
                : tab.PolygonEditorIsWheel ? tab.SelectedWheelPolygonIndex
                : tab.SelectedPolygonIndex;
            ShowConfirm("Remove Element",
                $"Are you sure you want to remove this {itemType}?\n\nThis will delete {itemType} {index + 1} from the model.",
                [new ConfirmAction("Remove", () => ApplyPolygonEditor(removeElement: true))]);
            return;
        }

        ApplyPolygonEditor(removeElement: false);
    }

    private void ApplyPolygonEditor(bool removeElement)
    {
        PushHistory(coalesce: false);
        if (_phase.ApplyPolygonEditorChanges(removeElement) is { } error)
            ShowError("Parse Error", error);
    }

    private void RequestClosePolygonEditor()
    {
        _ = _revision.Value;
        if (_phase.ActiveTab is not { } tab)
            return;

        if (!tab.PolygonEditorDirty)
        {
            tab.ShowPolygonEditor = false;
            Refresh();
            return;
        }

        ShowConfirm("Unsaved Changes", "Apply changes to the polygon before closing?",
        [
            new ConfirmAction("Apply", () =>
            {
                ApplyPolygonEditor();
                if (_phase.ActiveTab is { PolygonEditorDirty: false })
                    _phase.ActiveTab.ShowPolygonEditor = false;
                Refresh();
            }),
            new ConfirmAction("Discard", () =>
            {
                tab.ShowPolygonEditor = false;
                tab.PolygonEditorDirty = false;
                Refresh();
            }),
            new ConfirmAction("Cancel", () => { })
        ]);
    }

    private Node ConfirmDialog() => Sx.Show(
        when: () => _confirm.Value is not null,
        then: () => Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, top: 0, left: 0, right: 0, bottom: 0,
                alignItems: Align.Center, justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 190), zIndex: 40),
            onmousedown: _ => _confirm.Value = null,
            children: Sx.View(
                style: () => Sx.Styles(width: 460, flexDirection: FlexDirection.Column,
                    gapRow: 14, paddingTop: 24, paddingBottom: 24, paddingLeft: 26, paddingRight: 26,
                    backgroundColor: Panel, borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                    borderColor: Border, borderRadius: 12),
                onmousedown: e => e.StopPropagation(),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 18, fontFamily: FontFamily.Adventure,
                            fontStyle: FontStyle.Bold, foregroundColor: Blue),
                        text: () => _confirm.Value?.Title ?? ""),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue,
                            breakType: BreakType.Word, overflowBehavior: OverflowBehavior.ContinueVertically),
                        layout: () => Sx.Styles(width: "100%"),
                        text: () => _confirm.Value?.Message ?? ""),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, justifyContent: Justify.FlexEnd,
                            gapColumn: 10, gapRow: 10),
                        children: Sx.For(
                            each: () => _confirm.Value?.Actions ?? (IReadOnlyList<ConfirmAction>)[],
                            row: (action, index) => Button.Create(() => action.Label, () =>
                            {
                                _confirm.Value = null;
                                action.Run();
                            }, index == 0 ? ButtonVariant.Primary : ButtonVariant.Default)))
                ])));

    private Node ErrorDialog() => Sx.Show(
        when: () => _error.Value is not null,
        then: () => Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, top: 0, left: 0, right: 0, bottom: 0,
                alignItems: Align.Center, justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 190), zIndex: 50),
            onmousedown: _ => _error.Value = null,
            children: Sx.View(
                style: () => Sx.Styles(width: 500, flexDirection: FlexDirection.Column,
                    gapRow: 14, paddingTop: 24, paddingBottom: 24, paddingLeft: 26, paddingRight: 26,
                    backgroundColor: Panel, borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                    borderColor: new Color(255, 120, 120, 160), borderRadius: 12),
                onmousedown: e => e.StopPropagation(),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 18, fontFamily: FontFamily.Adventure,
                            fontStyle: FontStyle.Bold, foregroundColor: new Color(255, 140, 140)),
                        text: () => _error.Value?.Title ?? ""),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue,
                            breakType: BreakType.Word, overflowBehavior: OverflowBehavior.ContinueVertically),
                        layout: () => Sx.Styles(width: "100%"),
                        text: () => _error.Value?.Message ?? ""),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, justifyContent: Justify.FlexEnd),
                        children: Button.Create(() => "OK", () => _error.Value = null, ButtonVariant.Primary,
                            style: () => Sx.Styles(minWidth: 100)))
                ])));

    private static Node Modal(Action onDismiss, Node child) => Sx.View(
        style: () => Sx.Styles(position: Position.Absolute, top: 0, left: 0, right: 0, bottom: 0,
            alignItems: Align.Center, justifyContent: Justify.Center,
            backgroundColor: new Color(3, 8, 20, 175), zIndex: 30),
        onmousedown: _ => onDismiss(),
        children: child);

    private static Node DialogTitle(string title, string subtitle) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 22, fontFamily: FontFamily.Adventure,
                fontStyle: FontStyle.Bold, foregroundColor: Blue), text: () => title),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                text: () => subtitle)
        ]);

    private void ShowConfirm(string title, string message, IReadOnlyList<ConfirmAction> actions)
        => _confirm.Value = new ConfirmRequest(title, message, actions);

    private void ShowError(string title, string message) => _error.Value = new ErrorRequest(title, message);
}
