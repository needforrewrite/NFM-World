using System.Globalization;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using SignalsDotnet;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    // Chrome metrics. Kept compact so the viewport, not the panels, owns the window.
    private const float MenuBarHeight = 32;
    private const float TabStripHeight = 30;
    private const float ChromeTop = MenuBarHeight + TabStripHeight;
    private const float HierarchyWidth = 200;
    private const float InspectorWidth = 240;
    private const float BottomPanelHeight = 224;
    private const float PartTileSize = 64;
    private const float PartPreviewWidth = 54;
    private const float PartPreviewHeight = 38;

    // Built once. Slots read the live editor fields; Touch() republishes them after an action.
    private readonly Signal<int> _uiRevision = new(0);
    private readonly Signal<int> _previewRevision = new(0);
    private readonly Signal<(int X, int Y)> _cursorPosition = new((0, 0));
    private View? _viewportView;
    private long _lastInspectorEditAt;
    private int _lastSelectionStamp;

    // Hierarchy drag/context state. Rows are rebuilt from _uiRevision, so plain fields are enough.
    private readonly List<int> _hierDragPieceIds = [];
    private int _hierDragGroupId = -1;
    private bool _hierDragging;
    private float _hierDragStartX;
    private float _hierDragStartY;
    private HierarchyRowVm? _hierDropRow;
    private bool _hierDropAbove;
    private bool _contextMenuOpen;
    private HierarchyRowVm? _contextRow;
    private float _contextMenuX;
    private float _contextMenuY;

    private void Touch() => _uiRevision.Value = _uiRevision.Value + 1;

    private bool EditorModalOpen() => _showNewStageDialog || _showLoadStageDialog || _showExportDialog
                                      || _showPublishDialog || _showExitWarningDialog
                                      || _showCloseTabWarningDialog || _showRenameGroupDialog
                                      || _showTestDriveSaveDialog || _contextMenuOpen
                                      || _openEditorMenu.Value is not null;

    private int SelectionStamp()
    {
        var tab = ActiveTab;
        return HashCode.Combine(tab?.ActivePieceId ?? -1, tab?.SelectedWallId ?? -1, tab?.SelectedPieceIds.Count ?? 0);
    }

    public override Node? BuildUi() => Sx.View(
        style: () => Sx.Styles(width: 100.Percent(), height: 100.Percent(), flexDirection: FlexDirection.Column),
        onmount: view => view.MouseReleased = _ => CommitHierarchyDrag(),
        children:
        [
            TopBar(),
            TabStrip(),
            LeftPanel(),
            CenterViewport(),
            RightPanel(),
            BottomPanel(),
            Dialogs(),
            RectSelectionOverlay(),
            MenuOverlay(),
            ContextMenuOverlay()
        ]
    );

    private static Node SmallButton(string label, Action action)
    {
        var button = Button.Create(
            label: () => label,
            onPress: action,
            size: ButtonSize.Sm,
            style: CompactStyle(),
            textStyle: SmallButtonText
        );
        button.Name = "Editor action " + label;
        return button;
    }

    private Node SmallButton(Func<string> label, Action action)
        => Button.Create(
            label: Observe(label),
            onPress: action,
            size: ButtonSize.Sm,
            style: CompactStyle(),
            textStyle: SmallButtonText
        );

    private static TextStyles SmallButtonText() => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold);

    private Func<T> Observe<T>(Func<T> read) => () =>
    {
        _ = _uiRevision.Value;
        return read();
    };

    private Node EditorText(Func<TextStyles>? style = null, Func<string?>? text = null, Func<Styles>? layout = null)
        => Sx.Text(
            style: style is null ? null : Observe(style),
            text: text is null ? null : Observe(text),
            layout: layout is null ? null : Observe(layout)
        );

    private Node EditorShow(Func<bool> when, Func<object?> then, Func<object?>? otherwise = null)
        => Sx.Show(Observe(when), then, otherwise);

    private Node EditorField(
        Func<string> value,
        Action<string> onChange,
        Func<string?>? placeholder = null,
        float minWidth = 160
    )
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(
                minWidth: minWidth,
                paddingTop: 3,
                paddingBottom: 3,
                paddingLeft: 8,
                paddingRight: 8,
                backgroundColor: new Color(22, 34, 54),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 65),
                borderRadius: 6
            ),
            text: Observe(value),
            onchange: onChange,
            placeholder: placeholder is null ? null : Observe(placeholder)
        );
        input.TextStyles = Sx.TextStyles(fontSize: 11);
        return input;
    }

    private Node EditorCheckbox(Func<bool> value, Action toggle, Func<string?>? label = null)
        => Controls.Checkbox(Observe(value), toggle, label);

    private static string ShortName(string name)
    {
        var slash = name.LastIndexOf('/');
        return slash < 0 ? name : name[(slash + 1)..];
    }

    private readonly Signal<string?> _openEditorMenu = new(null);
    private bool _dismissedEditorOverlay;
    private readonly Dictionary<string, View> _editorMenuTriggers = new();

    private Node TopBar()
        => Sx.View(
            name: () => "Stage editor menu bar",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                top: 0,
                height: MenuBarHeight,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 4,
                paddingLeft: 12,
                paddingRight: 12,
                backgroundColor: new Color(8, 16, 32),
                borderBottom: 1,
                borderColor: new Color(128, 170, 255, 45),
                zIndex: 20
            ),
            children:
            [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold, foregroundColor: new Color(128, 170, 255)),
                    layout: () => Sx.Styles(marginRight: 16, flexShrink: 0),
                    text: () => "STAGE EDITOR"
                ),
                MenuTrigger("File"), MenuTrigger("Edit"), MenuTrigger("View"),
                Sx.View(style: () => Sx.Styles(width: 10, flexShrink: 0)),
                ModeButton(
                    () => "3D SCENE",
                    () => ActiveTab?.ViewMode == StageEditorTab.ViewModeEnum.Scene,
                    () => SetViewMode(StageEditorTab.ViewModeEnum.Scene)
                ),
                ModeButton(
                    () => "TOP DOWN",
                    () => ActiveTab?.ViewMode == StageEditorTab.ViewModeEnum.TopDown,
                    () => SetViewMode(StageEditorTab.ViewModeEnum.TopDown)
                ),
                ModeButton(
                    () => ActiveTab?.TopDownOrtho == true ? "ORTHO" : "PERSPECTIVE",
                    () => ActiveTab?.TopDownOrtho == true,
                    () =>
                    {
                        if (ActiveTab is { } tab)
                        {
                            tab.TopDownOrtho = !tab.TopDownOrtho;
                            UpdateCameraPosition();
                        }

                        Touch();
                    }
                ),
                Sx.View(style: () => Sx.Styles(flexGrow: 1, pointerEvents: false)),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(140, 166, 202)),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => ActiveTab is { Stage: { } stage } tab
                        ? $"{tab.ScenePieces.Count} pieces  /  {stage.Nlaps} laps" : "No document"
                )
            ]
        );

    private Node MenuTrigger(string label)
    {
        var trigger = Button.Create(
            label: () => label + " ▾",
            onPress: () => _openEditorMenu.Value = _openEditorMenu.Value == label ? null : label,
            active: () => _openEditorMenu.Value == label,
            variant: ButtonVariant.Ghost,
            size: ButtonSize.Sm,
            style: CompactStyle(minWidth: 56),
            textStyle: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold)
        );
        trigger.Name = "Editor menu " + label;
        _editorMenuTriggers[label] = trigger;
        return trigger;
    }

    private Node ModeButton(Func<string> label, Func<bool> active, Action onPress)
        => Button.Create(
            label: label,
            onPress: onPress,
            active: Observe(active),
            size: ButtonSize.Sm,
            style: CompactStyle(),
            textStyle: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold)
        );

    private static Func<Styles> CompactStyle(float minWidth = 0)
        => () => Sx.Styles(
            flexShrink: 0,
            minWidth: minWidth,
            paddingTop: 2,
            paddingBottom: 2,
            paddingLeft: 8,
            paddingRight: 8,
            borderRadius: 4
        );

    private Node MenuOverlay()
        => Sx.Show(
            when: () => _openEditorMenu.Value is not null,
            then: () => Sx.View(
                name: () => "Editor menu overlay",
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    right: 0,
                    top: MenuBarHeight,
                    bottom: 0,
                    zIndex: 40
                ),
                onmousedown: e => { _dismissedEditorOverlay = true; _openEditorMenu.Value = null; e.StopPropagation(); },
                children: Sx.View(
                    name: () => "Editor menu popup",
                    style: () => Sx.Styles(
                        position: Position.Absolute,
                        left: _editorMenuTriggers[_openEditorMenu.Value!].LayoutBorderPosition.X / TheGraphics.G.Scale,
                        top: 0,
                        width: 220,
                        flexDirection: FlexDirection.Column,
                        paddingTop: 6,
                        paddingBottom: 6,
                        backgroundColor: new Color(15, 28, 50),
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: new Color(128, 170, 255, 85),
                        borderRadius: 6
                    ),
                    onmousedown: e => e.StopPropagation(),
                    children: Sx.For(
                        each: () => new[] { _openEditorMenu.Value! },
                        row: (menu, _) => Sx.View(
                        style: () => Sx.Styles(flexDirection: FlexDirection.Column),
                        children: menu switch
                    {
                        "File" => new object[]
                        {
                            MenuAction("New stage…", "", OpenNewStage),
                            MenuAction(
                                "Open stage…",
                                "",
                                () => { RefreshAvailableStages(); _selectedStageIndex = -1; _showLoadStageDialog = true; }
                            ),
                            MenuAction("Save", "Ctrl+S", () => { if (ActiveTab?.Stage != null) SaveStage(); }),
                            MenuAction("Publish / export pack…", "", () =>
                                {
                                    if (ActiveTab is null) return;
                                    _publishName = ActiveTab.TabName;
                                    _publishStatus = "";
                                    _showPublishDialog = true;
                                }
                            ),
                            MenuAction(
                                "Export top-down image…",
                                "",
                                () => { _exportResultMessage = ""; _showExportDialog = true; }
                            ),
                            MenuAction("Close tab", "", () => { if (ActiveTab != null) CloseTab(_activeTabIndex); }),
                            MenuAction("Exit editor", "", RequestExit)
                        },
                        "Edit" => new object[]
                        {
                            MenuAction("Undo", "Ctrl+Z", PerformUndo),
                            MenuAction("Redo", "Ctrl+Y", PerformRedo),
                            MenuAction("Delete selection", "Delete", DeleteSelection),
                            MenuAction("Group selection", "", CreateGroupFromSelection)
                        },
                        _ => new object[]
                        {
                            MenuAction("3D scene", "", () => SetViewMode(StageEditorTab.ViewModeEnum.Scene)),
                            MenuAction("Top down", "", () => SetViewMode(StageEditorTab.ViewModeEnum.TopDown)),
                            MenuAction(_autoGeneratePolys ? "Disable ground polys" : "Enable ground polys", "", () =>
                                {
                                    _autoGeneratePolys = !_autoGeneratePolys;
                                    if (ActiveTab != null) RecreateEnvironment();
                                }
                            )
                        }
                    }
                        )
                    )
                )
            )
        );

    private Node MenuAction(string label, string shortcut, Action action)
    {
        var hovered = new Signal<bool>(false);
        return KeyboardNavigation.Bind(
            Sx.View(
                name: () => "Editor menu item " + label,
                style: () => Sx.Styles(
                    flexDirection: FlexDirection.Row,
                    justifyContent: Justify.SpaceBetween,
                    paddingTop: 8,
                    paddingBottom: 8,
                    paddingLeft: 12,
                    paddingRight: 12,
                    backgroundColor: hovered.Value ? new Color(39, 66, 108) : new Color(0, 0, 0, 0)
                ),
                onmouseenter: _ => hovered.Value = true,
                onmouseleave: _ => hovered.Value = false,
                onmousedown: e =>
                {
                    if (e.Button != MouseButton.Primary) return;
                    _dismissedEditorOverlay = true;
                    _openEditorMenu.Value = null;
                    action();
                    Touch();
                    e.StopPropagation();
                },
                children:
                [
                    EditorText(
                        style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(211, 228, 252)),
                        text: () => label
                    ),
                    EditorText(
                        style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(128, 155, 190)),
                        text: () => shortcut
                    )
                ]
            ),
            key =>
            {
                if (key is not (Key.Enter or Key.Space)) return false;
                _openEditorMenu.Value = null;
                action();
                Touch();
                return true;
            },
            focused => hovered.Value = focused
        );
    }

    private void OpenNewStage()
    {
        _newStageName = "";
        _showNewStageDialog = true;
        Touch();
    }

    private Node TabStrip()
        => KeyboardNavigation.Row(Sx.View(
            name: () => "Stage document tabs",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                top: MenuBarHeight,
                height: TabStripHeight,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Stretch,
                paddingLeft: 8,
                backgroundColor: new Color(7, 14, 28),
                borderBottom: 1,
                borderColor: new Color(128, 170, 255, 45)
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexGrow: 1, minWidth: 0, overflow: Overflow.Scroll),
                    children: Sx.For(each: Observe(() => _tabs.ToArray()), row: (tab, _) => TabChip(tab))
                ),
                SmallButton("+", OpenNewStage)
            ]
        ));

    private Node TabChip(StageEditorTab tab)
    {
        var hovered = new Signal<bool>(false);
        var active = Observe(() => ReferenceEquals(ActiveTab, tab));
        void Select() => SelectTab(_tabs.IndexOf(tab));
        return KeyboardNavigation.Bind(
            Sx.View(
                style: () => Sx.Styles(
                    position: Position.Relative,
                    flexDirection: FlexDirection.Row,
                    alignItems: Align.Center,
                    justifyContent: Justify.SpaceBetween,
                    gapColumn: 12,
                    flexShrink: 0,
                    minWidth: 140,
                    maxWidth: 260,
                    paddingLeft: 14,
                    paddingRight: 6,
                    backgroundColor: active() ? new Color(22, 42, 80) : hovered.Value ? new Color(17, 32, 57) : new Color(
                        0,
                        0,
                        0,
                        0
                    ),
                    borderRight: 1,
                    borderColor: new Color(128, 170, 255, 30)
                ),
                onmouseenter: _ => hovered.Value = true,
                onmouseleave: _ => hovered.Value = false,
                onmousedown: e => { if (e.Button == MouseButton.Primary) Select(); },
                children:
                [
                    EditorText(
                        style: () => Sx.TextStyles(
                            fontSize: 11,
                            fontStyle: FontStyle.Bold,
                            foregroundColor: active() ? new Color(205, 227, 255) : new Color(130, 155, 195)
                        ),
                        text: () => tab.TabName + (tab.HasUnsavedChanges ? " •" : "")
                    ),
                    Button.Create(
                        label: () => "×",
                        onPress: () => { CloseTab(_tabs.IndexOf(tab)); Touch(); },
                        variant: ButtonVariant.Ghost,
                        size: ButtonSize.Sm
                    ),
                    Sx.View(style: () => Sx.Styles(
                        position: Position.Absolute,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        height: 3,
                        backgroundColor: active() ? new Color(91, 163, 255) : new Color(0, 0, 0, 0),
                        pointerEvents: false
                    ))
                ]
            ),
            key => { if (key is not (Key.Enter or Key.Space)) return false; Select(); return true; },
            focused => hovered.Value = focused
        );
    }

    private void SelectTab(int index)
    {
        if (index == _activeTabIndex || index < 0 || index >= _tabs.Count)
            return;

        _activeTabIndex = index;
        StopMusicPreview(notify: false);
        UpdateCameraPosition();
        ApplyTabWorldValuesToWorld();
        RecreateEnvironment();
        RebuildAllWalls();
        RecreateScene();
        Touch();
    }

    private void RequestExit()
    {
        if (_tabs.Any(t => t.HasUnsavedChanges))
            _showExitWarningDialog = true;
        else
            GameSparker.ReturnToMainMenu();
        Touch();
    }

    private Node LeftPanel()
        => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: ChromeTop,
                bottom: BottomPanelHeight,
                width: HierarchyWidth,
                flexDirection: FlexDirection.Column,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 8,
                paddingRight: 8,
                gapRow: 6,
                backgroundColor: new Color(12, 22, 42, 250),
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 40)
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 6, flexShrink: 0),
                    children:
                    [
                        EditorText(style: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold), text: () => "Hierarchy"),
                        EditorField(
                            () => _hierarchySearch,
                            value => { _hierarchySearch = value; Touch(); },
                            () => "Search pieces"
                        )
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        minHeight: 0,
                        overflow: Overflow.Scroll,
                        // Keep rows and their buttons out from under the scrollbar.
                        paddingRight: 8,
                        gapRow: 2
                    ),
                    children:
                    [
                        Sx.For(each: HierarchyRows, row: (row, _) => HierarchyRow(row)),
                        EditorShow(
                            when: () => !ActiveTabHasPieces(),
                            then: () => EditorText(
                                style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(255, 255, 255, 90)),
                                text: () => "No pieces yet."
                            )
                        )
                    ]
                ),
                EditorShow(
                    when: () =>
                    {
                        _uiRevision.Value.ToString();
                        return ActiveTab is { } tab && tab.SelectedPieceIds.Count > 0;
                    },
                    then: () => SmallButton("+ GROUP FROM SELECTION", CreateGroupFromSelection)
                )
            ]
        );

    private enum HierarchyRowKind
    {
        Borders,
        Wall,
        Group,
        Ungrouped,
        Piece
    }

    private readonly record struct HierarchyRowVm(
        HierarchyRowKind Kind,
        string Label,
        int PieceId,
        int WallId,
        int GroupId,
        int GroupIndex
    );

    private IReadOnlyList<HierarchyRowVm> HierarchyRows()
    {
        _uiRevision.Value.ToString();
        if (ActiveTab is not { } tab)
            return [];

        var query = _hierarchySearch.Trim();
        var rows = new List<HierarchyRowVm> { new(HierarchyRowKind.Borders, "STAGE BORDERS", -1, -1, -1, -1) };

        foreach (var wall in tab.StageWalls)
        {
            if (Matches(wall.GetDisplayName(), query))
                rows.Add(
                    new HierarchyRowVm(HierarchyRowKind.Wall, $"{wall.GetDisplayName()} ({wall.Count})", -1, wall.Id, -1, -1)
                );
        }

        var groupedIds = new HashSet<int>(tab.HierarchyGroups.SelectMany(g => g.PieceIds));
        var ungroupedSlot = tab.UngroupedOrderIndex >= 0
            ? Math.Clamp(tab.UngroupedOrderIndex, 0, tab.HierarchyGroups.Count)
            : tab.HierarchyGroups.Count;

        for (var index = 0; index <= tab.HierarchyGroups.Count; index++)
        {
            if (index == ungroupedSlot)
            {
                var ungrouped = tab.ScenePieces
                    .Where(p => !p.PiecePlacement.IsWall && !groupedIds.Contains(p.Id) && Matches(p, query))
                    .ToArray();
                rows.Add(new HierarchyRowVm(HierarchyRowKind.Ungrouped, $"UNGROUPED ({ungrouped.Length})", -1, -1, -1, -1));
                rows.AddRange(ungrouped.Select(piece => PieceRow(piece, -1, -1)));
            }

            if (index == tab.HierarchyGroups.Count)
                break;

            var group = tab.HierarchyGroups.ByIndex(index);
            var members = group.PieceIds
                .Select(id => tab.ScenePieces.GetValueOrDefault(id))
                .Where(piece => piece is not null && Matches(piece, query))
                .ToArray();
            rows.Add(
                new HierarchyRowVm(HierarchyRowKind.Group, $"[G] {group.Name} ({group.PieceIds.Count})", -1, -1, group.Id, index)
            );
            rows.AddRange(members.Select(piece => PieceRow(piece!, group.Id, index)));
        }

        return rows;
    }

    private static HierarchyRowVm PieceRow(StagePieceInstance piece, int groupId, int groupIndex)
    {
        var tag = piece.PiecePlacement.Type switch
        {
            PiecePlacementType.CheckPoint => " [Chk]",
            PiecePlacementType.FixHoop => " [Fix]",
            _ => " [Set]"
        };
        return new HierarchyRowVm(
            HierarchyRowKind.Piece,
            $"{ShortName(piece.Name)}{tag} (ID: {piece.Id})",
            piece.Id,
            -1,
            groupId,
            groupIndex
        );
    }

    private static bool Matches(string text, string query)
        => query.Length == 0 || text.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(StagePieceInstance piece, string query)
        => Matches(piece.Name, query);

    private bool ActiveTabHasPieces()
    {
        _uiRevision.Value.ToString();
        return ActiveTab is { } tab && (tab.ScenePieces.Count > 0 || tab.StageWalls.Count > 0);
    }

    private Node HierarchyRow(HierarchyRowVm row)
    {
        if (row.Kind == HierarchyRowKind.Borders)
            return BordersHeader();

        var heading = row.Kind is HierarchyRowKind.Group or HierarchyRowKind.Ungrouped;
        var wall = row.Kind == HierarchyRowKind.Wall;
        var view = Sx.View(
            style: () =>
            {
                _uiRevision.Value.ToString();
                var selected = IsRowSelected(row);
                var dropTarget = _hierDropRow == row;
                return Sx.Styles(
                    position: Position.Relative,
                    flexShrink: 0,
                    marginTop: heading ? 4 : 0,
                    paddingTop: 3,
                    paddingBottom: 3,
                    paddingLeft: heading ? 6 : 12,
                    paddingRight: 8,
                    borderRadius: 4,
                    // A band behind group headers: pieces must not read as siblings.
                    backgroundColor: selected ? new Color(55, 90, 150, 180)
                        : dropTarget ? new Color(55, 90, 150, ShowDropLine(row) ? 70 : 150)
                        : heading ? new Color(128, 170, 255, 24)
                        : new Color(0, 0, 0, 0)
                );
            },
            children:
            [
                EditorText(
                    style: () =>
                    {
                        _uiRevision.Value.ToString();
                        return Sx.TextStyles(
                            fontSize: 10,
                            fontStyle: heading ? FontStyle.Bold : FontStyle.Plain,
                            foregroundColor: heading ? new Color(128, 170, 255)
                                : IsRowSelected(row) ? new Color(170, 210, 255)
                                : wall ? new Color(150, 185, 225)
                                : new Color(211, 228, 252)
                        );
                    },
                    text: () => row.Label
                ),
                DropIndicatorLine(row)
            ]
        );

        // The handlers need the row's own bounds to tell the insertion half.
        view.MousePressed = e => OnHierarchyRowPressed(row, e);
        view.MouseEntered = e => OnHierarchyRowHovered(row, e, view);
        view.MouseMoved = e => OnHierarchyRowHovered(row, e, view);
        return view;
    }

    /// <summary>An insertion line rather than a row highlight when the drop is an ordering change.</summary>
    private bool ShowDropLine(HierarchyRowVm row)
        => _hierDropRow == row && (row.Kind == HierarchyRowKind.Piece || _hierDragGroupId >= 0);

    private Node DropIndicatorLine(HierarchyRowVm row)
        => Sx.View(
            style: () =>
            {
                _ = _uiRevision.Value;
                var styles = Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    right: 0,
                    height: 2,
                    backgroundColor: new Color(128, 200, 255),
                    borderRadius: 1,
                    pointerEvents: false,
                    display: ShowDropLine(row) ? Display.Flex : Display.None
                );

                // Top edge means "insert before", bottom edge means "insert after".
                if (_hierDropAbove)
                    styles.Top = 0;
                else
                    styles.Bottom = 0;

                return styles;
            }
        );

    private Node BordersHeader()
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 3, paddingTop: 2, paddingBottom: 4, flexShrink: 0),
            children:
            [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, fontStyle: FontStyle.Bold, foregroundColor: new Color(128, 170, 255)),
                    text: () => "STAGE BORDERS"
                ),
                PropertyCheckbox(
                    "Auto-update walls",
                    () => _autoUpdateWalls,
                    () => { _autoUpdateWalls = !_autoUpdateWalls; Touch(); }
                ),
                SmallButton(
                    "AUTO-GENERATE BORDERS",
                    () =>
                    {
                        if (!HasPlaceableStageGeometry())
                            return;
                        PushUndoSnapshot();
                        AutoGenerateStageBordersFromGeometry();
                        Touch();
                    }
                )
            ]
        );

    private bool IsRowSelected(HierarchyRowVm row)
    {
        if (ActiveTab is not { } tab)
            return false;

        return row.Kind switch
        {
            HierarchyRowKind.Wall => tab.SelectedWallId == row.WallId,
            HierarchyRowKind.Piece => tab.SelectedPieceIds.Contains(row.PieceId),
            _ => false
        };
    }

    private void OnHierarchyRowPressed(HierarchyRowVm row, MouseEvent e)
    {
        if (ActiveTab is not { } tab)
            return;

        if (e.Button == MouseButton.Secondary)
        {
            OpenHierarchyContextMenu(row);
            e.StopPropagation();
            return;
        }

        if (e.Button != MouseButton.Primary)
            return;

        switch (row.Kind)
        {
            case HierarchyRowKind.Piece:
                // Pressing a row that is already part of a multi-selection drags the whole
                // selection; pressing another row replaces the selection (ctrl toggles).
                if (e.CtrlKey || !tab.SelectedPieceIds.Contains(row.PieceId) || tab.SelectedPieceIds.Count <= 1)
                    SelectHierarchyPiece(tab, row.PieceId, e.CtrlKey);
                else
                    tab.ActivePieceId = row.PieceId;

                _hierDragPieceIds.Clear();
                if (tab.SelectedPieceIds.Contains(row.PieceId))
                {
                    _hierDragPieceIds.AddRange(tab.SelectedPieceIds.OrderBy(id => tab.ScenePieces.FindIndex(p => p.Id == id)));
                }

                _hierDragStartX = e.Position.X;
                _hierDragStartY = e.Position.Y;
                break;
            case HierarchyRowKind.Group:
                SelectGroup(tab, row.GroupId);
                _hierDragGroupId = row.GroupId;
                _hierDragStartX = e.Position.X;
                _hierDragStartY = e.Position.Y;
                break;
            case HierarchyRowKind.Wall:
                tab.SelectedWallId = row.WallId;
                tab.ActivePieceId = -1;
                tab.SelectedPieceIds.Clear();
                break;
        }

        Touch();
    }

    private static void SelectHierarchyPiece(StageEditorTab tab, int pieceId, bool ctrl)
    {
        if (ctrl)
        {
            if (!tab.SelectedPieceIds.Remove(pieceId))
                tab.SelectedPieceIds.Add(pieceId);
        }
        else
        {
            tab.SelectedPieceIds.Clear();
            tab.SelectedPieceIds.Add(pieceId);
        }

        // Ctrl-clicking a selected row unselects it; the primary piece must stay in the set.
        tab.ActivePieceId = tab.SelectedPieceIds.Contains(pieceId)
            ? pieceId
            : tab.SelectedPieceIds.Count > 0 ? tab.SelectedPieceIds.First() : -1;
        tab.SelectedWallId = -1;
    }

    private static void SelectGroup(StageEditorTab tab, int groupId)
    {
        if (tab.HierarchyGroups.GetValueOrDefault(groupId) is not { } group)
            return;

        tab.SelectedPieceIds.Clear();
        foreach (var id in group.PieceIds)
            tab.SelectedPieceIds.Add(id);

        tab.ActivePieceId = group.PieceIds.Count > 0 ? group.PieceIds[^1] : -1;
        tab.SelectedWallId = -1;
    }

    /// <summary>Hover only counts as a drop target once the pressed row has actually moved.</summary>
    private void OnHierarchyRowHovered(HierarchyRowVm row, MouseMoveEvent e, Component rowView)
    {
        if (_hierDragPieceIds.Count == 0 && _hierDragGroupId < 0)
            return;

        if (!_hierDragging)
        {
            if (Math.Abs(e.Position.X - _hierDragStartX) < 4 && Math.Abs(e.Position.Y - _hierDragStartY) < 4)
                return;
            _hierDragging = true;
        }

        if (!IsValidHierarchyDrop(row))
        {
            if (_hierDropRow is null)
                return;

            _hierDropRow = null;
            Touch();
            return;
        }

        // Top or bottom half decides insert-before vs insert-after.
        var above = e.Position.Y < rowView.LayoutBorderPosition.Y + (rowView.LayoutBorderSize.Y / 2f);
        if (_hierDropRow == row && _hierDropAbove == above)
            return;

        _hierDropRow = row;
        _hierDropAbove = above;
        Touch();
    }

    private bool IsValidHierarchyDrop(HierarchyRowVm row)
    {
        if (_hierDragGroupId >= 0)
            return row.Kind == HierarchyRowKind.Group;

        return row.Kind is HierarchyRowKind.Piece or HierarchyRowKind.Group or HierarchyRowKind.Ungrouped;
    }

    private void CommitHierarchyDrag()
    {
        var target = _hierDropRow;
        var pieceIds = _hierDragPieceIds.ToArray();
        var groupId = _hierDragGroupId;
        var dragging = _hierDragging;
        var dropAbove = _hierDropAbove;

        _hierDragPieceIds.Clear();
        _hierDragGroupId = -1;
        _hierDragging = false;
        _hierDropRow = null;
        _hierDropAbove = false;

        if (!dragging || target is not { } destination || ActiveTab is not { } tab)
        {
            if (dragging)
                Touch();
            return;
        }

        if (pieceIds.Length > 0)
        {
            if (destination.Kind == HierarchyRowKind.Piece)
            {
                if (pieceIds.Contains(destination.PieceId))
                {
                    Touch();
                    return;
                }

                PushUndoSnapshot();
                ReorderPieces(pieceIds, destination.PieceId, after: !dropAbove);
                tab.HasUnsavedChanges = true;
            }
            else if (destination.Kind is HierarchyRowKind.Group or HierarchyRowKind.Ungrouped)
            {
                MovePiecesToGroup(
                    pieceIds,
                    destination.Kind == HierarchyRowKind.Group ? destination.GroupId : -1
                );
            }
        }
        else if (groupId >= 0 && destination.Kind == HierarchyRowKind.Group)
        {
            // ReorderGroup inserts before the given index; +1 makes a bottom-half drop land after.
            ReorderGroup(groupId, destination.GroupIndex + (dropAbove ? 0 : 1));
        }

        Touch();
    }

    private void ReorderGroup(int groupId, int targetIndex)
    {
        if (ActiveTab is not { } tab)
            return;

        var from = -1;
        for (var i = 0; i < tab.HierarchyGroups.Count; i++)
        {
            if (tab.HierarchyGroups.ByIndex(i).Id != groupId)
                continue;
            from = i;
            break;
        }

        if (from < 0 || from == targetIndex)
            return;

        PushUndoSnapshot();
        var group = tab.HierarchyGroups.ByIndex(from);
        tab.HierarchyGroups.RemoveAt(from);
        var insert = targetIndex > from ? targetIndex - 1 : targetIndex;
        tab.HierarchyGroups.Insert(Math.Clamp(insert, 0, tab.HierarchyGroups.Count), group);
        if (tab.UngroupedOrderIndex >= 0)
            tab.UngroupedOrderIndex = Math.Clamp(tab.UngroupedOrderIndex, 0, tab.HierarchyGroups.Count);
        ReorderScenePiecesForGroupOrder();
        tab.HasUnsavedChanges = true;
    }

    private void CreateGroupFromSelection()
    {
        if (ActiveTab is not { } tab)
            return;

        PushUndoSnapshot();
        var group = new HierarchyGroup { Id = tab.GetNextGroupId(), Name = "Group" };
        foreach (var id in tab.SelectedPieceIds)
        {
            foreach (var existing in tab.HierarchyGroups)
                existing.PieceIds.Remove(id);
            group.PieceIds.Add(id);
        }

        tab.HierarchyGroups.Add(group);
        tab.HasUnsavedChanges = true;

        _groupContextMenuGroupId = group.Id;
        _renameGroupBuffer = group.Name;
        _showRenameGroupDialog = true;
        Touch();
    }

    private readonly record struct MenuRowVm(string Label, string Command, int PieceId, int GroupId);

    private IReadOnlyList<MenuRowVm> ContextMenuRows()
    {
        _uiRevision.Value.ToString();
        if (_contextRow is not { } row || ActiveTab is not { } tab)
            return [];

        var rows = new List<MenuRowVm>();
        switch (row.Kind)
        {
            case HierarchyRowKind.Piece:
                if (row.GroupId >= 0)
                    rows.Add(new MenuRowVm("REMOVE FROM GROUP", "ungroup", row.PieceId, -1));
                foreach (var group in tab.HierarchyGroups)
                    if (group.Id != row.GroupId)
                        rows.Add(new MenuRowVm($"MOVE TO {group.Name}", "move", row.PieceId, group.Id));
                break;
            case HierarchyRowKind.Group:
                rows.Add(new MenuRowVm("RENAME…", "rename", -1, row.GroupId));
                rows.Add(new MenuRowVm("SELECT ALL IN GROUP", "selectall", -1, row.GroupId));
                rows.Add(new MenuRowVm("DELETE GROUP (KEEP PIECES)", "delete", -1, row.GroupId));
                break;
        }

        return rows;
    }

    private Node ContextMenuItem(MenuRowVm row)
        => Sx.View(
            style: () => Sx.Styles(paddingTop: 5, paddingBottom: 5, paddingLeft: 10, paddingRight: 10),
            onmousedown: e =>
            {
                if (e.Button != MouseButton.Primary)
                    return;
                _dismissedEditorOverlay = true;
                RunMenuCommand(row);
                e.StopPropagation();
            },
            children: EditorText(
                style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(185, 211, 255)),
                text: () => row.Label
            )
        );

    private void RunMenuCommand(MenuRowVm row)
    {
        CloseHierarchyContextMenu();
        if (ActiveTab is not { } tab)
            return;

        switch (row.Command)
        {
            case "ungroup":
            case "move":
                // Acts on the whole selection when the clicked row is part of one.
                MovePiecesToGroup(
                    tab.SelectedPieceIds.Contains(row.PieceId) && tab.SelectedPieceIds.Count > 1
                        ? tab.SelectedPieceIds.ToArray()
                        : [row.PieceId],
                    row.Command == "ungroup" ? -1 : row.GroupId
                );
                break;
            case "selectall":
                SelectGroup(tab, row.GroupId);
                break;
            case "rename":
                _groupContextMenuGroupId = row.GroupId;
                _renameGroupBuffer = tab.HierarchyGroups.GetValueOrDefault(row.GroupId)?.Name ?? "";
                _showRenameGroupDialog = true;
                break;
            case "delete":
                PushUndoSnapshot();
                tab.HierarchyGroups.RemoveAll(g => g.Id == row.GroupId);
                if (tab.UngroupedOrderIndex >= 0)
                    tab.UngroupedOrderIndex = Math.Clamp(tab.UngroupedOrderIndex, 0, tab.HierarchyGroups.Count);
                tab.HasUnsavedChanges = true;
                break;
        }

        Touch();
    }

    private void OpenHierarchyContextMenu(HierarchyRowVm row)
    {
        if (row.Kind is not (HierarchyRowKind.Piece or HierarchyRowKind.Group))
            return;

        _contextRow = row;
        _contextMenuOpen = true;
        _contextMenuX = _mouseX;
        _contextMenuY = _mouseY;
        Touch();
    }

    private void CloseHierarchyContextMenu()
    {
        _contextMenuOpen = false;
        _contextRow = null;
    }

    private Node ContextMenuOverlay()
        => Sx.View(
            style: () =>
            {
                _uiRevision.Value.ToString();
                return Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                    display: _contextMenuOpen ? Display.Flex : Display.None
                );
            },
            onmousedown: e =>
            {
                if (!_contextMenuOpen)
                    return;
                _dismissedEditorOverlay = true;
                CloseHierarchyContextMenu();
                Touch();
                e.StopPropagation();
            },
            children: Sx.View(
                style: () =>
                {
                    // The menu is placed from mouse pixels, so convert into the scaled unit space.
                    var scale = Math.Max(TheGraphics.G.Scale, 0.01f);
                    var maxLeft = GameSparker.Game.Window.Width / scale - 218;
                    var maxTop = GameSparker.Game.Window.Height / scale - 40;
                    return Sx.Styles(
                        position: Position.Absolute,
                        left: Math.Clamp(_contextMenuX / scale, 0, Math.Max(0, maxLeft)),
                        top: Math.Clamp(_contextMenuY / scale, 0, Math.Max(0, maxTop)),
                        width: 210,
                        flexDirection: FlexDirection.Column,
                        paddingTop: 4,
                        paddingBottom: 4,
                        backgroundColor: new Color(12, 22, 42, 255),
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: new Color(128, 170, 255, 90),
                        borderRadius: 6,
                        zIndex: 20
                    );
                },
                onmousedown: e => e.StopPropagation(),
                children: [Sx.For(each: ContextMenuRows, row: (row, _) => ContextMenuItem(row))]
            )
        );

    private Node CenterViewport()
    {
        var surface = Sx.View(
            name: () => "Stage viewport",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: HierarchyWidth,
                right: InspectorWidth,
                top: ChromeTop,
                bottom: BottomPanelHeight,
                pointerEvents: false
            ),
            children: EditorShow(
                when: () => ActiveTab?.Stage != null,
                then: () => EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 140)),
                    layout: () => Sx.Styles(position: Position.Absolute, top: 8, left: 12),
                    text: StatusText
                ),
                otherwise: () => Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, alignItems: Align.Center, justifyContent: Justify.Center),
                    children: EditorText(
                        style: () => Sx.TextStyles(fontSize: 14, foregroundColor: new Color(255, 255, 255, 130)),
                        text: () => "No stage is loaded. Use NEW or LOAD."
                    )
                )
            )
        );
        _viewportView = surface;

        return surface;
    }

    private void SetViewMode(StageEditorTab.ViewModeEnum mode)
    {
        if (ActiveTab is not { } tab || tab.ViewMode == mode)
            return;
        tab.ViewMode = mode;
        UpdateCameraPosition();
        Touch();
    }

    private string StatusText()
    {
        _uiRevision.Value.ToString();
        if (ActiveTab is not { } tab)
            return "";

        var snap = _snapEnabled ? "Snap: ON" : "Snap: OFF";
        var grid = _gridSnapEnabled ? $"Grid: {_gridSnapSize:0}" : "Grid: OFF";
        var placement = _pendingPlacementPartIndex >= 0 && _pendingPlacementPartIndex < _availableParts.Count
            ? $"  ·  Placing {ShortName(_availableParts[_pendingPlacementPartIndex].FileName)}  [Q/E] rotate  [LMB] place  [Esc] cancel"
            : "";
        var cursor = _cursorPosition.Value;
        return $"Cursor {cursor.X},{cursor.Y}  ·  {ActiveTab.ViewMode}  ·  {snap}  ·  {grid}{placement}";
    }

    private Node RightPanel()
        => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                right: 0,
                top: ChromeTop,
                bottom: BottomPanelHeight,
                width: InspectorWidth,
                flexDirection: FlexDirection.Column,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 10,
                paddingRight: 10,
                gapRow: 6,
                backgroundColor: new Color(12, 22, 42, 250),
                borderLeft: 1,
                borderColor: new Color(128, 170, 255, 40)
            ),
            children:
            [
                EditorText(style: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold), text: () => "Inspector"),
                Sx.View(
                    style: () => Sx.Styles(
                        flexGrow: 1,
                        flexShrink: 1,
                        overflow: Overflow.Scroll,
                        flexDirection: FlexDirection.Column,
                        paddingRight: 8,
                        gapRow: 6
                    ),
                    children: Sx.For(
                        each: () => SelectedPiece() is { } piece ? new object[] { piece }
                            : SelectedWall() is { } wall ? new object[] { wall } : [],
                        row: (selected, _) => selected is StagePieceInstance ? PieceInspector() : WallInspector()
                    )
                )
            ]
        );

    private StagePieceInstance? SelectedPiece()
    {
        _uiRevision.Value.ToString();
        return ActiveTab is { } tab && tab.ActivePieceId >= 0 ? tab.ScenePieces.GetValueOrDefault(tab.ActivePieceId) : null;
    }

    private EditorStageWall? SelectedWall()
    {
        _uiRevision.Value.ToString();
        return ActiveTab is { } tab && tab.SelectedWallId >= 0 ? tab.StageWalls.GetValueOrDefault(tab.SelectedWallId) : null;
    }

    private Node PieceInspector()
    {
        var piece = SelectedPiece()!;
        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4, flexShrink: 0),
            children:
            [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 12, fontStyle: FontStyle.Bold, foregroundColor: new Color(170, 255, 170)),
                    text: () => ShortName(piece.Name)
                ),
                NumberField("X", () => (float)piece.Position.X, value => SetPieceX(piece, value)),
                NumberField("Y", () => (float)piece.Position.Y - 250, value => SetPieceY(piece, value), "(0 = ground)"),
                NumberField("Z", () => (float)piece.Position.Z, value => SetPieceZ(piece, value)),
                NumberField("Yaw", () => (float)piece.Rotation.Yaw.Degrees, value => SetPieceYaw(piece, value)),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 110)),
                    text: () => "Type"
                ),
                Controls.Dropdown(
                    options: () => ["Object", "Checkpoint", "Fix Hoop"],
                    value: Observe(() => (int)piece.PiecePlacement.Type),
                    onSelect: index =>
                    {
                        BeginInspectorEdit();
                        piece.PiecePlacement = piece.PiecePlacement with { Type = (PiecePlacementType)index };
                        ActiveTab!.HasUnsavedChanges = true;
                        Touch();
                    },
                    width: 210
                ),
                SmallButton(() => ActiveTab?.SelectedPieceIds.Count > 1 ? "DELETE ALL SELECTED" : "DELETE PIECE", DeleteSelection),
                SmallButton(() => _isSwapMode ? "CANCEL SWAP" : "SWAP PIECE…", () =>
                    {
                        _isSwapMode = !_isSwapMode;
                        _pendingPlacementPartIndex = -1;
                        Touch();
                    }
                )
            ]
        );
    }

    private Node WallInspector()
    {
        var wall = SelectedWall()!;
        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4, flexShrink: 0),
            children:
            [
                EditorText(style: () => Sx.TextStyles(fontSize: 12, fontStyle: FontStyle.Bold), text: () => wall.GetDisplayName()),
                NumberField("Count", () => wall.Count, value =>
                    {
                        BeginInspectorEdit();
                        wall.Count = Math.Clamp((int)value, 1, 100);
                        ActiveTab!.HasUnsavedChanges = true;
                        RebuildAllWalls();
                        Touch();
                    }
                ),
                NumberField("Position", () => wall.Position, value =>
                    {
                        BeginInspectorEdit();
                        wall.Position = (int)value;
                        ActiveTab!.HasUnsavedChanges = true;
                        RebuildAllWalls();
                        Touch();
                    }
                ),
                NumberField("Offset", () => wall.Offset, value =>
                    {
                        BeginInspectorEdit();
                        wall.Offset = (int)value;
                        ActiveTab!.HasUnsavedChanges = true;
                        RebuildAllWalls();
                        Touch();
                    }
                ),
                SmallButton("DELETE BORDER", () =>
                    {
                        PushUndoSnapshot();
                        ActiveTab!.StageWalls.Remove(wall);
                        ActiveTab.SelectedWallId = -1;
                        ActiveTab.HasUnsavedChanges = true;
                        RebuildAllWalls();
                        Touch();
                    }
                )
            ]
        );
    }

    private Node NumberField(string label, Func<float> value, Action<float> set, string? hint = null)
    {
        // Holds the text while the field is edited; null means "mirror the model", which is what
        // makes a gizmo drag show its new position without reselecting the piece.
        var draft = new Signal<string?>(null);
        var coordinate = label is "X" or "Y" or "Z";
        TextInput? input = null;
        input = Sx.TextInput(
            style: () => Sx.Styles(
                flexGrow: 1,
                minWidth: 0,
                width: 0,
                paddingTop: 3,
                paddingBottom: 3,
                paddingLeft: 8,
                paddingRight: 8,
                backgroundColor: new Color(22, 34, 54),
                borderRadius: 6,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 65)
            ),
            text: Observe(() => input is { IsFocused: true } && draft.Value is { } typed
                ? typed
                : value().ToString("0.##", CultureInfo.InvariantCulture)),
            onchange: text =>
            {
                draft.Value = text;
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !float
                    .IsFinite(parsed))
                    return;
                set(parsed);
                Touch();
            }
        );
        input.TextStyles = Sx.TextStyles(fontSize: 11);
        return Sx.View(
            name: () => "Inspector field " + label,
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center, gapColumn: 6, flexShrink: 0),
            children:
            [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 130)),
                    layout: () => Sx.Styles(width: coordinate ? 22 : 60, flexShrink: 0),
                    text: () => label
                ),
                input,
                coordinate ? EditorText(
                    style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 110)),
                    layout: () => Sx.Styles(width: 68, flexShrink: 0),
                    text: () => hint ?? ""
                ) : null
            ]
        );
    }

    private void BeginInspectorEdit()
    {
        var now = System.Environment.TickCount64;
        if (now - _lastInspectorEditAt > 700)
            PushUndoSnapshot();
        _lastInspectorEditAt = now;
    }

    private void SetPieceX(StagePieceInstance piece, float x)
    {
        BeginInspectorEdit();
        piece.Position = new f64Vector3((fix64)x, piece.Position.Y, piece.Position.Z);
        ActiveTab!.HasUnsavedChanges = true;
    }

    private void SetPieceY(StagePieceInstance piece, float y)
    {
        BeginInspectorEdit();
        piece.Position = new f64Vector3(piece.Position.X, (fix64)(y + 250), piece.Position.Z);
        ActiveTab!.HasUnsavedChanges = true;
    }

    private void SetPieceZ(StagePieceInstance piece, float z)
    {
        BeginInspectorEdit();
        piece.Position = new f64Vector3(piece.Position.X, piece.Position.Y, (fix64)z);
        ActiveTab!.HasUnsavedChanges = true;
    }

    private void SetPieceYaw(StagePieceInstance piece, float yaw)
    {
        BeginInspectorEdit();
        piece.Rotation = new f64Euler(f64AngleSingle.FromDegrees((fix64)yaw), piece.Rotation.Pitch, piece.Rotation.Roll);
        ActiveTab!.HasUnsavedChanges = true;
    }

    private void DeleteSelection()
    {
        if (ActiveTab is not { } tab || tab.Stage is null)
            return;

        PushUndoSnapshot();
        if (tab.SelectedPieceIds.Count > 1)
        {
            foreach (var id in tab.SelectedPieceIds.ToArray())
            {
                var piece = tab.ScenePieces.GetValueOrDefault(id);
                if (piece is null) continue;
                tab.Stage.Pieces.Remove(piece.Obj);
                tab.ScenePieces.Remove(piece);
                foreach (var group in tab.HierarchyGroups)
                    group.PieceIds.Remove(id);
            }

            tab.SelectedPieceIds.Clear();
            tab.ActivePieceId = -1;
        }
        else if (tab.ScenePieces.GetValueOrDefault(tab.ActivePieceId) is { } piece)
        {
            tab.Stage.Pieces.Remove(piece.Obj);
            tab.ScenePieces.Remove(piece);
            tab.SelectedPieceIds.Remove(piece.Id);
            foreach (var group in tab.HierarchyGroups)
                group.PieceIds.Remove(piece.Id);
            tab.ActivePieceId = -1;
        }

        tab.HasUnsavedChanges = true;
        RebuildClientRenderer();
        Touch();
    }

    private Node PartsContent()
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minHeight: 0, gapRow: 6),
            children: [PartsToolbar(), PartsGrid()]
        );

    private Node PartsToolbar()
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                flexShrink: 0,
                minHeight: 26,
                gapRow: 6,
                gapColumn: 6,
                flexWrap: Wrap.Wrap
            ),
            children:
            [
                        ModeButton(() => "ALL", () => _partsCategory == 0, () => { _partsCategory = 0; Touch(); }),
                        ModeButton(() => "NFMM", () => _partsCategory == 1, () => { _partsCategory = 1; Touch(); }),
                        ModeButton(() => "VENDOR", () => _partsCategory == 2, () => { _partsCategory = 2; Touch(); }),
                        ModeButton(() => "USER", () => _partsCategory == 3, () => { _partsCategory = 3; Touch(); }),
                        EditorField(() => _partsSearch, value => { _partsSearch = value; Touch(); }, () => "Search parts"),
                        SmallButton(() => _snapEnabled ? "SNAP ON" : "SNAP OFF", () => { _snapEnabled = !_snapEnabled; Touch(); }),
                        SmallButton(
                            () => _gridSnapEnabled ? $"GRID {_gridSnapSize:0}" : "GRID OFF",
                            () => { _gridSnapEnabled = !_gridSnapEnabled; Touch(); }
                        ),
                        EditorShow(
                            when: () => { _uiRevision.Value.ToString(); return _gridSnapEnabled; },
                            then: () => Controls.Dropdown(
                                options: () => SnapPresets.Select(size => size.ToString("0")).ToArray(),
                                value: Observe(() => _snapPresetIndex),
                                onSelect: index =>
                                {
                                    _snapPresetIndex = index;
                                    _gridSnapSize = SnapPresets[index];
                                    Touch();
                                },
                                width: 90
                            )
                        ),
                        EditorShow(
                            when: () => { _uiRevision.Value.ToString(); return _pendingPlacementPartIndex >= 0; },
                            then: () => Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center, gapColumn: 4),
                                children:
                                [
                                    EditorText(
                                        style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(26, 230, 255)),
                                        text: () => $"Yaw {_pendingPlacementYaw:0}°"
                                    ),
                                    SmallButton("-45", () =>
                                        {
                                            _pendingPlacementYaw = ((_pendingPlacementYaw - 45f) % 360f + 360f) % 360f;
                                            Touch();
                                        }
                                    ),
                                    SmallButton("+45", () =>
                                        {
                                            _pendingPlacementYaw = (_pendingPlacementYaw + 45f) % 360f;
                                            Touch();
                                        }
                                    ),
                                    SmallButton("RESET", () => { _pendingPlacementYaw = 0f; Touch(); })
                                ]
                            )
                        )
            ]
        );

    private Node PartsGrid()
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                flexWrap: Wrap.Wrap,
                gapColumn: 4,
                gapRow: 4,
                overflow: Overflow.Scroll,
                flexGrow: 1,
                minHeight: 0,
                alignContent: Align.FlexStart,
                alignItems: Align.FlexStart,
                paddingTop: 4,
                paddingBottom: 4,
                paddingRight: 8
            ),
            children: Sx.For(each: FilteredParts, row: (part, _) => PartButton(part))
        );

    private IReadOnlyList<(int Index, Rad3d Part)> FilteredParts()
    {
        _uiRevision.Value.ToString();
        var query = _partsSearch.Trim();
        var result = new List<(int, Rad3d)>();
        for (var i = 0; i < _availableParts.Count; i++)
        {
            var part = _availableParts[i];
            var name = part.FileName;
            if (query.Length > 0 && !name.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            var category = name.StartsWith("nfmm/", StringComparison.OrdinalIgnoreCase) ? 1
                : name.StartsWith("nfmw/", StringComparison.OrdinalIgnoreCase)
                  || name.StartsWith("vendor/", StringComparison.OrdinalIgnoreCase) ? 2 : 3;
            if (_partsCategory != 0 && category != _partsCategory)
                continue;

            result.Add((i, part));
        }

        return result;
    }

    private Node PartButton((int Index, Rad3d Part) part)
    {
        QueuePartPreview(part.Part.FileName, part.Part);

        var active = () => { _uiRevision.Value.ToString(); return _pendingPlacementPartIndex == part.Index; };
        var swapTarget = () =>
        {
            _uiRevision.Value.ToString();
            return _isSwapMode && ActiveTab is { ActivePieceId: >= 0 } tab
                   && tab.ScenePieces.GetValueOrDefault(tab.ActivePieceId)?.Name == part.Part.FileName;
        };
        var preview = () =>
        {
            _previewRevision.Value.ToString();
            return _partPreviews.TryGetValue(part.Part.FileName, out var stored) ? stored.Image : null;
        };

        return Sx.View(
            style: () =>
            {
                // DrawVariableBorderRect strokes whenever a colour is set, so the swap
                // outline has to be absent rather than zero-width.
                var swapping = swapTarget();
                return Sx.Styles(
                    width: PartTileSize,
                    height: PartTileSize,
                    flexShrink: 0,
                    flexDirection: FlexDirection.Column,
                    alignItems: Align.Center,
                    gapRow: 2,
                    paddingTop: 4,
                    paddingBottom: 4,
                    borderRadius: 4,
                    backgroundColor: active() ? new Color(55, 90, 150, 220) : new Color(22, 42, 80, 120),
                    borderTop: swapping ? 2 : 0,
                    borderBottom: swapping ? 2 : 0,
                    borderLeft: swapping ? 2 : 0,
                    borderRight: swapping ? 2 : 0,
                    borderColor: swapping ? new Color(255, 190, 30) : null
                );
            },
            onmousedown: _ =>
            {
                if (_isSwapMode)
                {
                    SwapSelection(part.Part);
                    return;
                }

                _pendingPlacementPartIndex = part.Index;
                _hasValidPlacementPos = false;
                _pendingPlacementYaw = 0;
                _pendingPlacementYOff = 0;
                Touch();
            },
            children:
            [
                EditorShow(
                    when: () => preview() is not null,
                    then: () => new NFMWorld.DriverInterface.UI.Image
                    {
                        ImageData = preview(),
                        Styles = Sx.Styles(
                            width: PartPreviewWidth,
                            height: PartPreviewHeight,
                            flexShrink: 0,
                            pointerEvents: false
                        )
                    },
                    otherwise: () => Sx.View(
                        style: () => Sx.Styles(
                            width: PartPreviewWidth,
                            height: PartPreviewHeight,
                            flexShrink: 0,
                            borderRadius: 3,
                            backgroundColor: PartTileColor(part.Part.FileName)
                        )
                    )
                ),
                EditorText(
                    style: () => Sx.TextStyles(
                        fontSize: 9,
                        foregroundColor: active() ? new Color(128, 170, 255) : new Color(185, 211, 255)
                    ),
                    text: () => ShortName(part.Part.FileName)
                )
            ]
        );
    }

    private static Color PartTileColor(string name)
    {
        if (name.Contains("checkpoint") || name.Contains("chk"))
            return new Color(46, 140, 46, 220);
        if (name.Contains("fix") || name.Contains("hoop"))
            return new Color(166, 51, 51, 220);
        if (name.Contains("road") || name.Contains("ramp") || name.Contains("roll"))
            return new Color(97, 77, 51, 220);
        if (name.Contains("wall") || name.Contains("border"))
            return new Color(64, 64, 115, 220);
        if (name.Contains("turn") || name.Contains("twist") || name.Contains("bend"))
            return new Color(115, 77, 26, 220);
        return new Color(64, 77, 97, 220);
    }

    private void SwapSelection(Rad3d replacement)
    {
        if (ActiveTab is not { } tab || tab.ActivePieceId < 0)
            return;

        PushUndoSnapshot();
        foreach (var piece in tab.ScenePieces.Where(p => tab.SelectedPieceIds.Contains(p.Id)).ToArray())
        {
            // StageObject.Rad is immutable and the renderer caches by object, so the piece's
            // stage object has to be replaced, not just its placement record.
            var old = piece.Obj;
            var replaced = StageObject.CreateDefaultObject(
                replacement,
                piece.Position,
                piece.Rotation,
                piece.PiecePlacement.Type,
                piece.PiecePlacement.NodeKind,
                piece.PiecePlacement.IsSpecial,
                piece.PiecePlacement.IsWall
            );

            if (tab.Stage is { } stage)
            {
                var index = stage.Pieces.IndexOf(old);
                if (index >= 0)
                    stage.Pieces[index] = replaced;
            }

            piece.ReplaceObject(replaced);
        }

        tab.HasUnsavedChanges = true;
        _isSwapMode = false;
        RebuildClientRenderer();
        Touch();
    }

    private Node Dialogs()
        => Sx.View(
            style: () => Sx.Styles(display: Display.Contents),
            children:
            [
                NewStageDialog(),
                LoadStageDialog(),
                ExportDialog(),
                ExitDialog(),
                CloseTabDialog(),
                RenameGroupDialog(),
                PublishDialog(),
                TestDriveSaveDialog()
            ]
        );

    private Node NewStageDialog()
        => Dialog(
            () => _showNewStageDialog,
            "NEW STAGE",
            () => [
                EditorField(() => _newStageName, value => { _newStageName = value; Touch(); }, () => "Stage name"),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 11),
                    text: () => $"File: {ConvertStageNameToFilename(_newStageName)}.txt"
                ),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(140, 166, 202)),
                    text: () => "Starting road"
                ),
                Controls.Dropdown(
                    options: () => _newStageStartPartOptions,
                    value: Observe(() => _newStageStartPartIndex),
                    onSelect: index => { _newStageStartPartIndex = index; Touch(); },
                    width: 370
                ),
                DialogButtons(
                    "CREATE",
                    () =>
                    {
                        if (string.IsNullOrWhiteSpace(_newStageName)) return;
                        var start = _newStageStartPartIndex > 0 ? _newStageStartPartOptions[_newStageStartPartIndex] : null;
                        CreateEmptyStage(_newStageName, start);
                        _showNewStageDialog = false;
                        Touch();
                    },
                    () => { _showNewStageDialog = false; Touch(); }
                )
            ]
        );

    private Node LoadStageDialog()
        => Dialog(
            () => _showLoadStageDialog,
            "LOAD STAGE",
            () => [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        overflow: Overflow.Scroll,
                        maxHeight: 300,
                        paddingRight: 8
                    ),
                    children: Sx.For(
                        each: () => { _uiRevision.Value.ToString(); return _availableStages.ToArray(); },
                        row: (stage, _) => Sx.View(
                            style: Observe(() => Sx.Styles(
                                flexShrink: 0,
                                paddingTop: 7,
                                paddingBottom: 7,
                                paddingLeft: 10,
                                paddingRight: 10,
                                borderRadius: 4,
                                backgroundColor: _selectedStageIndex == _availableStages.IndexOf(stage)
                                    ? new Color(39, 66, 108) : new Color(0, 0, 0, 0)
                            )
                            ),
                            onmousedown: _ => { _selectedStageIndex = _availableStages.IndexOf(stage); Touch(); },
                            children: EditorText(style: () => Sx.TextStyles(fontSize: 12), text: () => stage)
                        )
                    )
                ),
                DialogButtons(
                    "LOAD",
                    () =>
                    {
                        if (_selectedStageIndex >= 0 && _selectedStageIndex < _availableStages.Count)
                            LoadStage(_availableStages[_selectedStageIndex]);
                        _showLoadStageDialog = false;
                        Touch();
                    },
                    () => { _showLoadStageDialog = false; Touch(); }
                )
            ]
        );

    private Node ExportDialog()
        => Dialog(
            () => _showExportDialog,
            "EXPORT TOP-DOWN",
            () => [
                NumberField("Width", () => _exportWidth, value => _exportWidth = (int)value),
                NumberField("Height", () => _exportHeight, value => _exportHeight = (int)value),
                NumberField("Padding", () => _exportPadding, value => _exportPadding = (int)value),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 255, 255, 110)),
                    text: () => _exportResultMessage
                ),
                DialogButtons(
                    "EXPORT",
                    () => { _exportRequested = true; Touch(); },
                    () => { _showExportDialog = false; Touch(); }
                )
            ]
        );

    private Node PublishDialog()
        => Dialog(
            () => _showPublishDialog,
            "PUBLISH TO ARCHIVE",
            () => [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 200, 120)),
                    text: () => "Publication is public. Unsaved edits are included."
                ),
                EditorField(() => _publishName, value => { _publishName = value; Touch(); }, () => "Name"),
                EditorField(
                    () => _publishDescription,
                    value => { _publishDescription = value; Touch(); },
                    () => "Description"
                ),
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 255, 255, 120)),
                    text: () => _publishStatus
                ),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 8),
                    children:
                    [
                        SmallButton("PUBLISH", PublishActiveStage),
                        SmallButton("EXPORT PACK", ExportActiveStagePack),
                        SmallButton("CANCEL", () => { _showPublishDialog = false; Touch(); })
                    ]
                )
            ]
        );

    private void PublishActiveStage()
    {
        if (SnapshotActiveStage() is not { } stage)
        {
            _publishStatus = "The stage could not be snapshotted.";
            Touch();
            return;
        }

        var name = string.IsNullOrWhiteSpace(_publishName) ? (ActiveTab?.TabName ?? "stage") : _publishName;
        _publishStatus = "Publishing...";
        Touch();
        _ = PublishAsync(stage, name);
    }

    private async Task PublishAsync(NFMWorldLibrary.StageLoader stage, string name)
    {
        var result = await NFMWorld.Accounts.ArchivePublisher.PublishStageAsync(stage, name, _publishDescription);
        _publishStatus = result.Message;
        Touch();
    }

    private void ExportActiveStagePack()
    {
        if (SnapshotActiveStage() is not { } stage)
        {
            _publishStatus = "The stage could not be snapshotted.";
            Touch();
            return;
        }

        var name = string.IsNullOrWhiteSpace(_publishName) ? (ActiveTab?.TabName ?? "stage") : _publishName;
        var path = NFMWorld.Accounts.ArchivePublisher.ExportLocalStage(stage, name);
        _publishStatus = path is null ? "The stage could not be exported." : $"Exported to {path}.";
        Touch();
    }

    /// <summary>
    /// Serializes the live document (unsaved edits included) and resolves it into a detached
    /// stage. Neither step touches the tab, its dirty flag or its undo history.
    /// </summary>
    private NFMWorldLibrary.StageLoader? SnapshotActiveStage()
    {
        if (BuildStageText() is not { } text)
            return null;

        var id = "user/" + ConvertStageNameToFilename(_publishName.Length > 0 ? _publishName : ActiveTab!.TabName);
        try
        {
            return NFMWorldLibrary.StageLoader.FromLines(id, text.Split('\n'));
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not snapshot the stage: {ex.Message}");
            return null;
        }
    }

    private Node ExitDialog()
        => Dialog(
            () => _showExitWarningDialog,
            "UNSAVED CHANGES",
            () => [
                EditorText(style: () => Sx.TextStyles(fontSize: 12), text: () => "One or more stages have unsaved changes."),
                DialogButtons(
                    "EXIT WITHOUT SAVING",
                    () => { _showExitWarningDialog = false; GameSparker.ReturnToMainMenu(); },
                    () => { _showExitWarningDialog = false; Touch(); }
                )
            ]
        );

    private Node CloseTabDialog()
        => Dialog(
            () => _showCloseTabWarningDialog,
            "CLOSE TAB?",
            () => [
                EditorText(style: () => Sx.TextStyles(fontSize: 12), text: () => "This stage has unsaved changes."),
                DialogButtons(
                    "CLOSE WITHOUT SAVING",
                    () =>
                    {
                        if (_tabToClose >= 0)
                            PerformCloseTab(_tabToClose);
                        _tabToClose = -1;
                        _showCloseTabWarningDialog = false;
                        Touch();
                    },
                    () => { _tabToClose = -1; _showCloseTabWarningDialog = false; Touch(); }
                )
            ]
        );

    private Node RenameGroupDialog()
        => Dialog(
            () => _showRenameGroupDialog,
            "RENAME GROUP",
            () => [
                EditorField(
                    () => _renameGroupBuffer,
                    value => { _renameGroupBuffer = value; Touch(); },
                    () => "Group name"
                ),
                DialogButtons(
                    "RENAME",
                    () =>
                    {
                        if (ActiveTab is { } tab && tab.HierarchyGroups.GetValueOrDefault(_groupContextMenuGroupId) is { } group)
                        {
                            if (!string.IsNullOrWhiteSpace(_renameGroupBuffer))
                                group.Name = _renameGroupBuffer;
                            tab.HasUnsavedChanges = true;
                        }

                        _showRenameGroupDialog = false;
                        Touch();
                    },
                    () => { _showRenameGroupDialog = false; Touch(); }
                )
            ]
        );

    private Node Dialog(Func<bool> open, string title, Func<Node[]> content)
        => EditorShow(
            when: () => { _uiRevision.Value.ToString(); return open(); },
            then: () => Sx.View(
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: new Color(3, 8, 20, 200),
                    zIndex: 60,
                    alignItems: Align.Center,
                    justifyContent: Justify.Center
                ),
                children: Sx.View(
                    style: () => Sx.Styles(
                        width: 460,
                        maxHeight: 90.Percent(),
                        // Clipped, not scrollable: these are bounded forms, and a scroll container
                        // here draws a bar even when the content fits (layout rounding).
                        overflow: Overflow.Hidden,
                        flexDirection: FlexDirection.Column,
                        gapRow: 8,
                        paddingTop: 16,
                        paddingBottom: 16,
                        paddingLeft: 18,
                        paddingRight: 18,
                        backgroundColor: new Color(12, 22, 42, 250),
                        borderTop: 1,
                        borderBottom: 1,
                        borderLeft: 1,
                        borderRight: 1,
                        borderColor: new Color(128, 170, 255, 90),
                        borderRadius: 10
                    ),
                    children:
                    [
                        EditorText(
                            style: () => Sx.TextStyles(
                                fontSize: 16,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: new Color(128, 170, 255)
                            ),
                            text: () => title
                        ),
                        ..content()
                    ],
                    onmousedown: e => e.StopPropagation()
                )
            )
        );

    private Node DialogButtons(string confirm, Action onConfirm, Action onCancel)
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 8),
            children: [SmallButton(confirm, onConfirm), SmallButton("CANCEL", onCancel)]
        );

    private Node RectSelectionOverlay()
        => Sx.View(
            style: () =>
            {
                _uiRevision.Value.ToString();
                var (minX, minY, maxX, maxY) = RectSelectionBounds();

                // Mouse coordinates are window pixels; style lengths are multiplied by G.Scale.
                var scale = Math.Max(TheGraphics.G.Scale, 0.01f);
                return Sx.Styles(
                    position: Position.Absolute,
                    left: minX / scale,
                    top: minY / scale,
                    width: (maxX - minX) / scale,
                    height: (maxY - minY) / scale,
                    display: _isRectSelecting && maxX > minX && maxY > minY ? Display.Flex : Display.None,
                    pointerEvents: false,
                    backgroundColor: new Color(66, 150, 250, 40),
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: new Color(66, 150, 250, 230)
                );
            }
        );
}
