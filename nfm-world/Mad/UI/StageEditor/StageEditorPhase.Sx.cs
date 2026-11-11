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
using SignalsDotnet;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    // Built once. Slots read the live editor fields; Touch() republishes them after an action.
    private readonly Signal<int> _uiRevision = new(0);
    private readonly Signal<int> _previewRevision = new(0);
    private View? _viewportView;
    private long _lastInspectorEditAt;
    private int _lastSelectionStamp;

    // Hierarchy drag/context state. Rows are rebuilt from _uiRevision, so plain fields are enough.
    private int _hierDragPieceId = -1;
    private int _hierDragGroupId = -1;
    private bool _hierDragging;
    private float _hierDragStartX;
    private float _hierDragStartY;
    private HierarchyRowVm? _hierDropRow;
    private bool _contextMenuOpen;
    private HierarchyRowVm? _contextRow;
    private float _contextMenuX;
    private float _contextMenuY;

    private void Touch() => _uiRevision.Value = _uiRevision.Value + 1;

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
            PartsPanel(),
            Dialogs(),
            RectSelectionOverlay(),
            ContextMenuOverlay()
        ]
    );

    private static Node SmallButton(string label, Action action)
        => Button.Create(label: () => label, onPress: action, size: ButtonSize.Sm);

    private static Node SmallButton(Func<string> label, Action action)
        => Button.Create(label: label, onPress: action, size: ButtonSize.Sm);

    private static string ShortName(string name)
    {
        var slash = name.LastIndexOf('/');
        return slash < 0 ? name : name[(slash + 1)..];
    }

    private Node TopBar()
        => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                top: 0,
                height: 36,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 6,
                paddingTop: 6,
                paddingBottom: 6,
                paddingLeft: 8,
                paddingRight: 8,
                backgroundColor: new Color(8, 16, 32, 240)
            ),
            children:
            [
                SmallButton("NEW", () => { _newStageName = ""; _showNewStageDialog = true; Touch(); }),
                SmallButton(
                    "LOAD",
                    () => { RefreshAvailableStages(); _selectedStageIndex = -1; _showLoadStageDialog = true; Touch(); }
                ),
                SmallButton("SAVE", () => { if (ActiveTab?.Stage != null) SaveStage(); Touch(); }),
                SmallButton("EXPORT TOP-DOWN", () => { _exportResultMessage = ""; _showExportDialog = true; Touch(); }),
                SmallButton(
                    "PROPERTIES",
                    () =>
                    {
                        if (ActiveTab?.Stage != null)
                        {
                            LoadPropertiesFromTab();
                            _showPropertiesDialog = true;
                            Touch();
                        }
                    }
                ),
                SmallButton("EXIT", RequestExit),
                SmallButton(
                    () => _autoGeneratePolys ? "POLYS ON" : "POLYS OFF",
                    () => { _autoGeneratePolys = !_autoGeneratePolys; if (_autoGeneratePolys) RecreateEnvironment(); Touch(); }
                ),
                Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(185, 211, 255)),
                    text: () =>
                    {
                        _uiRevision.Value.ToString();
                        return ActiveTab is { Stage: { } stage }
                        ? $"{ActiveTab.TabName}{(ActiveTab.HasUnsavedChanges ? " *" : "")}  ·  {ActiveTab.ScenePieces.Count} pieces  ·  {stage.Nlaps} lap(s)"
                        : "No stage loaded — NEW or LOAD";
                    }
                )
            ]
        );

    private Node TabStrip()
        => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                top: 36,
                height: 34,
                flexDirection: FlexDirection.Row,
                gapColumn: 4,
                paddingTop: 4,
                paddingBottom: 4,
                paddingLeft: 8,
                paddingRight: 8,
                backgroundColor: new Color(12, 22, 42, 230)
            ),
            children: Sx.For(
                each: () => { _uiRevision.Value.ToString(); return Enumerable.Range(0, _tabs.Count).ToArray(); },
                row: (index, _) => TabChip(index)
            )
        );

    private Node TabChip(int index)
    {
        var active = () => { _uiRevision.Value.ToString(); return _activeTabIndex == index; };
        return Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 6,
                paddingTop: 5,
                paddingBottom: 5,
                paddingLeft: 12,
                paddingRight: 8,
                borderRadius: 5,
                backgroundColor: active() ? new Color(55, 90, 150, 220) : new Color(22, 42, 80, 160)
            ),
            onmousedown: _ => SelectTab(index),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 13,
                        foregroundColor: active() ? new Color(128, 170, 255) : new Color(185, 211, 255)
                    ),
                    text: () => index < _tabs.Count ? _tabs[index].TabName + (_tabs[index].HasUnsavedChanges ? "*" : "") : ""
                ),
                SmallButton("×", () => { CloseTab(index); Touch(); })
            ]
        );
    }

    private void SelectTab(int index)
    {
        if (index == _activeTabIndex || index < 0 || index >= _tabs.Count)
            return;

        _activeTabIndex = index;
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
                top: 70,
                bottom: 240,
                width: 250,
                flexDirection: FlexDirection.Column,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 8,
                paddingRight: 8,
                gapRow: 6,
                backgroundColor: new Color(12, 22, 42, 250)
            ),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold), text: () => "Hierarchy"),
                Controls.TextField(() => _hierarchySearch, value => { _hierarchySearch = value; Touch(); }, () => "Search pieces"),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexGrow: 1,
                        flexShrink: 1,
                        overflow: Overflow.Scroll,
                        gapRow: 2
                    ),
                    children:
                    [
                        Sx.For(each: HierarchyRows, row: (row, _) => HierarchyRow(row)),
                        Sx.Show(
                            when: () => !ActiveTabHasPieces(),
                            then: () => Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(255, 255, 255, 90)),
                                text: () => "No pieces yet."
                            )
                        )
                    ]
                ),
                Sx.Show(
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

            var group = tab.HierarchyGroups[index];
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
        return Sx.View(
            style: () =>
            {
                _uiRevision.Value.ToString();
                var selected = IsRowSelected(row);
                var hovered = _hierDropRow == row;
                return Sx.Styles(
                    flexShrink: 0,
                    paddingTop: 4,
                    paddingBottom: 4,
                    paddingLeft: heading ? 4 : 10,
                    paddingRight: 8,
                    borderRadius: 4,
                    backgroundColor: selected ? new Color(55, 90, 150, 180)
                        : hovered ? new Color(55, 90, 150, 90)
                        : new Color(0, 0, 0, 0)
                );
            },
            onmousedown: e => OnHierarchyRowPressed(row, e),
            onmouseenter: e => OnHierarchyRowHovered(row, e),
            children: Sx.Text(
                style: () =>
                {
                    _uiRevision.Value.ToString();
                    return Sx.TextStyles(
                        fontSize: heading ? 11 : 12,
                        fontStyle: heading ? FontStyle.Bold : FontStyle.Plain,
                        foregroundColor: heading ? new Color(170, 200, 255)
                            : IsRowSelected(row) ? new Color(128, 170, 255)
                            : new Color(185, 211, 255)
                    );
                },
                text: () => row.Label
            )
        );
    }

    private Node BordersHeader()
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 3, paddingTop: 2, paddingBottom: 4),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold, foregroundColor: new Color(170, 200, 255)),
                    text: () => "STAGE BORDERS"
                ),
                Controls.Checkbox(
                    () => _autoUpdateWalls,
                    () => { _autoUpdateWalls = !_autoUpdateWalls; Touch(); },
                    () => "Auto-update walls"
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
                SelectHierarchyPiece(tab, row.PieceId, e.CtrlKey);
                _hierDragPieceId = row.PieceId;
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

        tab.ActivePieceId = pieceId;
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
    private void OnHierarchyRowHovered(HierarchyRowVm row, MouseMoveEvent e)
    {
        if (_hierDragPieceId < 0 && _hierDragGroupId < 0)
            return;

        if (!_hierDragging)
        {
            if (Math.Abs(e.Position.X - _hierDragStartX) < 4 && Math.Abs(e.Position.Y - _hierDragStartY) < 4)
                return;
            _hierDragging = true;
        }

        if (_hierDropRow == row)
            return;

        _hierDropRow = row;
        Touch();
    }

    private void CommitHierarchyDrag()
    {
        var target = _hierDropRow;
        var pieceId = _hierDragPieceId;
        var groupId = _hierDragGroupId;
        var dragging = _hierDragging;

        _hierDragPieceId = -1;
        _hierDragGroupId = -1;
        _hierDragging = false;
        _hierDropRow = null;

        if (!dragging || target is not { } destination || ActiveTab is not { } tab)
        {
            if (dragging)
                Touch();
            return;
        }

        if (pieceId >= 0)
        {
            if (destination.Kind == HierarchyRowKind.Piece && destination.PieceId != pieceId)
            {
                PushUndoSnapshot();
                ReorderPiece(pieceId, destination.PieceId);
                tab.HasUnsavedChanges = true;
            }
            else if (destination.Kind is HierarchyRowKind.Group or HierarchyRowKind.Ungrouped)
            {
                MovePieceToGroup(pieceId, destination.Kind == HierarchyRowKind.Group ? destination.GroupId : -1);
            }
        }
        else if (groupId >= 0 && destination.Kind == HierarchyRowKind.Group)
        {
            ReorderGroup(groupId, destination.GroupIndex);
        }

        Touch();
    }

    private void MovePieceToGroup(int pieceId, int groupId)
    {
        if (ActiveTab is not { } tab)
            return;

        var target = groupId >= 0 ? tab.HierarchyGroups.GetValueOrDefault(groupId) : null;
        if (target is not null && target.PieceIds.Contains(pieceId))
            return;

        if (target is null && !tab.HierarchyGroups.Any(g => g.PieceIds.Contains(pieceId)))
            return;

        PushUndoSnapshot();
        foreach (var group in tab.HierarchyGroups)
            group.PieceIds.Remove(pieceId);
        target?.PieceIds.Add(pieceId);
        tab.HasUnsavedChanges = true;
    }

    private void ReorderGroup(int groupId, int targetIndex)
    {
        if (ActiveTab is not { } tab)
            return;

        var from = -1;
        for (var i = 0; i < tab.HierarchyGroups.Count; i++)
        {
            if (tab.HierarchyGroups[i].Id != groupId)
                continue;
            from = i;
            break;
        }

        if (from < 0 || from == targetIndex)
            return;

        PushUndoSnapshot();
        var group = tab.HierarchyGroups[from];
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
                RunMenuCommand(row);
                e.StopPropagation();
            },
            children: Sx.Text(
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
                MovePieceToGroup(row.PieceId, -1);
                break;
            case "move":
                MovePieceToGroup(row.PieceId, row.GroupId);
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
                CloseHierarchyContextMenu();
                Touch();
                e.StopPropagation();
            },
            children: Sx.View(
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: Math.Min(_contextMenuX, Math.Max(0, GameSparker.Game.Window.Width - 220)),
                    top: Math.Min(_contextMenuY, Math.Max(0, GameSparker.Game.Window.Height - 40)),
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
                ),
                onmousedown: e => e.StopPropagation(),
                children: [Sx.For(each: ContextMenuRows, row: (row, _) => ContextMenuItem(row))]
            )
        );

    private Node CenterViewport()
    {
        var surface = Sx.View(
            style: () => Sx.Styles(flexGrow: 1, flexShrink: 1, minHeight: 0, pointerEvents: false),
            children: Sx.Show(
                when: () => ActiveTab?.Stage != null,
                then: () => Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(255, 255, 255, 140)),
                    layout: () => Sx.Styles(position: Position.Absolute, top: 8, left: 12),
                    text: StatusText
                ),
                otherwise: () => Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, alignItems: Align.Center, justifyContent: Justify.Center),
                    children: Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 16, foregroundColor: new Color(255, 255, 255, 130)),
                        text: () => "No stage is loaded. Use NEW or LOAD."
                    )
                )
            )
        );
        _viewportView = surface;

        var view = Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 250,
                right: 300,
                top: 70,
                bottom: 240,
                flexDirection: FlexDirection.Column
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        flexShrink: 0,
                        gapColumn: 6,
                        paddingTop: 4,
                        paddingBottom: 4
                    ),
                    children:
                    [
                        SmallButton("3D SCENE", () => SetViewMode(StageEditorTab.ViewModeEnum.Scene)),
                        SmallButton("TOP DOWN", () => SetViewMode(StageEditorTab.ViewModeEnum.TopDown)),
                        SmallButton(() => ActiveTab?.TopDownOrtho == true ? "ORTHO" : "PERSPECTIVE", () =>
                            {
                                if (ActiveTab is { } tab)
                                {
                                    tab.TopDownOrtho = !tab.TopDownOrtho;
                                    UpdateCameraPosition();
                                }

                                Touch();
                            }
                        )
                    ]
                ),
                surface
            ]
        );

        return view;
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
        return $"Cursor {_mouseX},{_mouseY}  ·  {ActiveTab.ViewMode}  ·  {snap}  ·  {grid}{placement}";
    }

    private Node RightPanel()
        => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                right: 0,
                top: 70,
                bottom: 240,
                width: 300,
                flexDirection: FlexDirection.Column,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 10,
                paddingRight: 10,
                gapRow: 6,
                backgroundColor: new Color(12, 22, 42, 250)
            ),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold), text: () => "Inspector"),
                Sx.View(
                    style: () => Sx.Styles(
                        flexGrow: 1,
                        flexShrink: 1,
                        overflow: Overflow.Scroll,
                        flexDirection: FlexDirection.Column,
                        gapRow: 6
                    ),
                    children: Sx.Show(
                        when: () => SelectedPiece() is not null || SelectedWall() is not null,
                        then: () => Sx.View(
                            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 6),
                            children:
                            [
                                Sx.Show(when: () => SelectedPiece() is not null, then: PieceInspector),
                                Sx.Show(when: () => SelectedWall() is not null, then: WallInspector)
                            ]
                        ),
                        otherwise: () => Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(255, 255, 255, 90)),
                            text: () => "Nothing selected. Click a piece in the viewport or the hierarchy."
                        )
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
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold, foregroundColor: new Color(170, 255, 170)),
                    text: () => ShortName(piece.Name)
                ),
                NumberField("X", () => (float)piece.Position.X, value => SetPieceX(piece, value)),
                NumberField("Y (0 = ground)", () => (float)piece.Position.Y - 250, value => SetPieceY(piece, value)),
                NumberField("Z", () => (float)piece.Position.Z, value => SetPieceZ(piece, value)),
                NumberField("Yaw", () => (float)piece.Rotation.Yaw.Degrees, value => SetPieceYaw(piece, value)),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 255, 255, 110)),
                    text: () => "Type"
                ),
                Controls.Dropdown(
                    options: () => ["Object", "Checkpoint", "Fix Hoop"],
                    value: () => (int)piece.PiecePlacement.Type,
                    onSelect: index =>
                    {
                        BeginInspectorEdit();
                        piece.PiecePlacement = piece.PiecePlacement with { Type = (PiecePlacementType)index };
                        ActiveTab!.HasUnsavedChanges = true;
                        Touch();
                    },
                    width: 270
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
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold), text: () => wall.GetDisplayName()),
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

    private Node NumberField(string label, Func<float> value, Action<float> set)
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center, gapColumn: 6),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 255, 255, 130)),
                    text: () => label
                ),
                Controls.TextField(
                    () => value().ToString("0.##", CultureInfo.InvariantCulture),
                    text =>
                    {
                        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                            return;
                        set(parsed);
                        Touch();
                    }
                )
            ]
        );

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
            tab.ActivePieceId = -1;
        }

        tab.HasUnsavedChanges = true;
        RebuildClientRenderer();
        Touch();
    }

    private Node PartsPanel()
        => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                bottom: 0,
                height: 240,
                flexDirection: FlexDirection.Column,
                paddingTop: 6,
                paddingBottom: 6,
                paddingLeft: 10,
                paddingRight: 10,
                gapRow: 6,
                backgroundColor: new Color(8, 16, 32, 240)
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        gapColumn: 6,
                        flexWrap: Wrap.Wrap
                    ),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold), text: () => "Parts"),
                        SmallButton("ALL", () => { _partsCategory = 0; Touch(); }),
                        SmallButton("NFMM", () => { _partsCategory = 1; Touch(); }),
                        SmallButton("VENDOR", () => { _partsCategory = 2; Touch(); }),
                        SmallButton("USER", () => { _partsCategory = 3; Touch(); }),
                        Controls.TextField(() => _partsSearch, value => { _partsSearch = value; Touch(); }, () => "Search parts"),
                        SmallButton(() => _snapEnabled ? "SNAP ON" : "SNAP OFF", () => { _snapEnabled = !_snapEnabled; Touch(); }),
                        SmallButton(
                            () => _gridSnapEnabled ? $"GRID {_gridSnapSize:0}" : "GRID OFF",
                            () => { _gridSnapEnabled = !_gridSnapEnabled; Touch(); }
                        ),
                        Sx.Show(
                            when: () => { _uiRevision.Value.ToString(); return _gridSnapEnabled; },
                            then: () => Controls.Dropdown(
                                options: () => SnapPresets.Select(size => size.ToString("0")).ToArray(),
                                value: () => _snapPresetIndex,
                                onSelect: index =>
                                {
                                    _snapPresetIndex = index;
                                    _gridSnapSize = SnapPresets[index];
                                    Touch();
                                },
                                width: 90
                            )
                        ),
                        Sx.Show(
                            when: () => { _uiRevision.Value.ToString(); return _pendingPlacementPartIndex >= 0; },
                            then: () => Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center, gapColumn: 4),
                                children:
                                [
                                    Sx.Text(
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
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        flexWrap: Wrap.Wrap,
                        gapColumn: 4,
                        gapRow: 4,
                        overflow: Overflow.Scroll,
                        flexGrow: 1
                    ),
                    children: Sx.For(each: FilteredParts, row: (part, _) => PartButton(part))
                )
            ]
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
            style: () => Sx.Styles(
                width: 68,
                flexDirection: FlexDirection.Column,
                alignItems: Align.Center,
                gapRow: 2,
                paddingTop: 4,
                paddingBottom: 4,
                borderRadius: 4,
                backgroundColor: active() ? new Color(55, 90, 150, 220) : new Color(22, 42, 80, 120),
                borderTop: swapTarget() ? 2 : 0,
                borderBottom: swapTarget() ? 2 : 0,
                borderLeft: swapTarget() ? 2 : 0,
                borderRight: swapTarget() ? 2 : 0,
                borderColor: new Color(255, 190, 30)
            ),
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
                Sx.Show(
                    when: () => preview() is not null,
                    then: () => new NFMWorld.DriverInterface.UI.Image
                    {
                        ImageData = preview(),
                        Styles = Sx.Styles(width: 64, height: 52, flexShrink: 0)
                    },
                    otherwise: () => Sx.View(
                        style: () => Sx.Styles(
                            width: 64,
                            height: 52,
                            flexShrink: 0,
                            borderRadius: 3,
                            backgroundColor: PartTileColor(part.Part.FileName)
                        )
                    )
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 10,
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
            piece.PiecePlacement = piece.PiecePlacement with { Object = replacement };

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
                PropertiesDialog(),
                ExitDialog(),
                CloseTabDialog(),
                RenameGroupDialog(),
                PublishDialog()
            ]
        );

    private Node NewStageDialog()
        => Dialog(
            () => _showNewStageDialog,
            "NEW STAGE",
            [
                Controls.TextField(() => _newStageName, value => { _newStageName = value; Touch(); }, () => "Stage name"),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11),
                    text: () => $"File: {ConvertStageNameToFilename(_newStageName)}.txt"
                ),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 6),
                    children: _newStageStartPartOptions.Select((option, index) => SmallButton(option, () =>
                        {
                            _newStageStartPartIndex = index;
                            Touch();
                        }
                    )
                    ).ToArray()
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
            [
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, overflow: Overflow.Scroll, maxHeight: 240),
                    children: Sx.For(
                        each: () => { _uiRevision.Value.ToString(); return _availableStages; },
                        row: (stage, _) => Sx.View(
                            style: () => Sx.Styles(flexShrink: 0, paddingTop: 4, paddingBottom: 4),
                            onmousedown: _ => { _selectedStageIndex = _availableStages.IndexOf(stage); Touch(); },
                            children: Sx.Text(style: () => Sx.TextStyles(fontSize: 12), text: () => stage)
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
            [
                NumberField("Width", () => _exportWidth, value => _exportWidth = (int)value),
                NumberField("Height", () => _exportHeight, value => _exportHeight = (int)value),
                NumberField("Padding", () => _exportPadding, value => _exportPadding = (int)value),
                Sx.Text(
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

    private Node PropertiesDialog()
        => Dialog(
            () => _showPropertiesDialog,
            "STAGE PROPERTIES",
            [
                Controls.TextField(() => _editStageName, value => { _editStageName = value; Touch(); }, () => "Stage name"),
                ColorSliders(
                    "Sky",
                    () => _editSkyColor,
                    c
                    => { _editSkyColor = c; World.Sky = c; if (ActiveTab?.StageRenderer != null) ActiveTab.StageRenderer.sky = new Sky(GameSparker.GraphicsDevice); Touch(); }
                ),
                ColorSliders("Fog", () => _editFogColor, c => { _editFogColor = c; World.Fog = c; Touch(); }),
                ColorSliders("Ground", () => _editGroundColor, c => { _editGroundColor = c; World.GroundColor = c; Touch(); }),
                Controls.Checkbox(() => _editPolysEnabled, () =>
                    {
                        _editPolysEnabled = !_editPolysEnabled;
                        World.HasPolys = _editPolysEnabled;
                        World.DrawPolys = _editPolysEnabled;
                        if (ActiveTab?.StageRenderer != null)
                            ActiveTab.StageRenderer.polys = _editPolysEnabled && ActiveTab.Stage != null
                            ? Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count)
                            : null;
                        Touch();
                    }, () => "Ground polys"
                ),
                Controls.Checkbox(() => _editCloudsEnabled, () =>
                    {
                        _editCloudsEnabled = !_editCloudsEnabled;
                        World.HasClouds = _editCloudsEnabled;
                        World.DrawClouds = _editCloudsEnabled;
                        Touch();
                    }, () => "Clouds"
                ),
                Controls.Checkbox(() => _editMountainsEnabled, () =>
                    {
                        _editMountainsEnabled = !_editMountainsEnabled;
                        World.DrawMountains = _editMountainsEnabled;
                        Touch();
                    }, () => "Mountains"
                ),
                NumberField(
                    "Fade from",
                    () => _editFadeFrom,
                    value => { _editFadeFrom = (int)value; World.FadeFrom = _editFadeFrom; }
                ),
                DialogButtons(
                    "APPLY",
                    () => { ApplyPropertiesDialog(); _showPropertiesDialog = false; Touch(); },
                    () => { ApplyTabWorldValuesToWorld(); RecreateEnvironment(); _showPropertiesDialog = false; Touch(); }
                )
            ]
        );

    private Node PublishDialog()
        => Dialog(
            () => _showPublishDialog,
            "PUBLISH TO ARCHIVE",
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 200, 120)),
                    text: () => "Publication is public. Unsaved edits are included."
                ),
                Controls.TextField(() => _publishName, value => { _publishName = value; Touch(); }, () => "Name"),
                Controls.TextField(
                    () => _publishDescription,
                    value => { _publishDescription = value; Touch(); },
                    () => "Description"
                ),
                Sx.Text(
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
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 12), text: () => "One or more stages have unsaved changes."),
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
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 12), text: () => "This stage has unsaved changes."),
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
            [
                Controls.TextField(
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

    private Node Dialog(Func<bool> open, string title, Node[] content)
        => Sx.Show(
            when: () => { _uiRevision.Value.ToString(); return open(); },
            then: () => Sx.View(
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: new Color(3, 8, 20, 180),
                    alignItems: Align.Center,
                    justifyContent: Justify.Center
                ),
                children: Sx.View(
                    style: () => Sx.Styles(
                        width: 420,
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
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 18,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: new Color(128, 170, 255)
                            ),
                            text: () => title
                        ),
                        ..content
                    ]
                )
            )
        );

    private Node DialogButtons(string confirm, Action onConfirm, Action onCancel)
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 8),
            children: [SmallButton(confirm, onConfirm), SmallButton("CANCEL", onCancel)]
        );

    private Node ColorSliders(string label, Func<Color3> read, Action<Color3> write)
        => Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 2),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 255, 255, 130)),
                    text: () => label
                ),
                Controls.Slider(() => read().R, value => write(new Color3((short)value, read().G, read().B)), 0, 255, 1),
                Controls.Slider(() => read().G, value => write(new Color3(read().R, (short)value, read().B)), 0, 255, 1),
                Controls.Slider(() => read().B, value => write(new Color3(read().R, read().G, (short)value)), 0, 255, 1)
            ]
        );

    private Node RectSelectionOverlay()
        => Sx.View(
            style: () =>
            {
                _uiRevision.Value.ToString();
                return Sx.Styles(
                    position: Position.Absolute,
                    left: Math.Min(_rectSelectStartX, _rectSelectEndX),
                    top: Math.Min(_rectSelectStartY, _rectSelectEndY),
                    width: Math.Abs(_rectSelectEndX - _rectSelectStartX),
                    height: Math.Abs(_rectSelectEndY - _rectSelectStartY),
                    display: _isRectSelecting ? Display.Flex : Display.None,
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

    private void LoadPropertiesFromTab()
    {
        if (ActiveTab is not { } tab) return;
        _editStageName = tab.TabName;
        _editSkyColor = tab.SkyColor;
        _editFogColor = tab.FogColor;
        _editGroundColor = tab.GroundColor;
        _editPolysColor = tab.PolysColor;
        _editPolysEnabled = tab.PolysEnabled;
        _editCloudsEnabled = tab.CloudsEnabled;
        _editCloudsColor = tab.CloudsColor;
        _editCloudsParam4 = tab.CloudsParam4;
        _editCloudsHeight = tab.CloudsHeight;
        _editCloudCoverage = tab.CloudCoverage;
        _editMountainsEnabled = tab.MountainsEnabled;
        _editMountainsSeed = tab.MountainsSeed;
        _editSnapA = tab.SnapA;
        _editSnapB = tab.SnapB;
        _editSnapC = tab.SnapC;
        _editFadeFrom = tab.FadeFrom;
    }

    private void ApplyPropertiesDialog()
    {
        if (ActiveTab is not { } tab) return;
        tab.TabName = _editStageName;
        tab.SkyColor = _editSkyColor;
        tab.FogColor = _editFogColor;
        tab.GroundColor = _editGroundColor;
        tab.PolysColor = _editPolysColor;
        tab.PolysEnabled = _editPolysEnabled;
        tab.CloudsEnabled = _editCloudsEnabled;
        tab.CloudsColor = _editCloudsColor;
        tab.CloudsParam4 = _editCloudsParam4;
        tab.CloudsHeight = _editCloudsHeight;
        tab.CloudCoverage = _editCloudCoverage;
        tab.MountainsEnabled = _editMountainsEnabled;
        tab.MountainsSeed = _editMountainsSeed;
        tab.SnapA = _editSnapA;
        tab.SnapB = _editSnapB;
        tab.SnapC = _editSnapC;
        tab.FadeFrom = _editFadeFrom;
        ApplyTabWorldValuesToWorld();
        RecreateEnvironment();
        RecreateScene();
        tab.HasUnsavedChanges = true;
    }
}
