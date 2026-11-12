using Maxine.Extensions;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    /// <summary>
    /// Reorders a dragged block of pieces as one unit, keeping the block's internal order.
    /// <paramref name="after"/> places it after the target, otherwise before it.
    /// </summary>
    private void ReorderPieces(IReadOnlyList<int> draggedIds, int targetId, bool after)
    {
        if (ActiveTab is not { } tab || draggedIds.Count == 0) return;
        if (draggedIds.Contains(targetId)) return;
        if (tab.ScenePieces.GetValueOrDefault(targetId) is not { } target) return;

        var block = OrderedPieces(tab, draggedIds);
        if (block.Count == 0) return;

        foreach (var piece in block)
            tab.ScenePieces.RemoveAt(tab.ScenePieces.FindIndex(p => p.Id == piece.Id));

        var targetIndex = tab.ScenePieces.FindIndex(p => p.Id == target.Id);
        if (targetIndex < 0) targetIndex = tab.ScenePieces.Count - 1;

        var insertAt = Math.Clamp(after ? targetIndex + 1 : targetIndex, 0, tab.ScenePieces.Count);
        for (var i = 0; i < block.Count; i++)
            tab.ScenePieces.Insert(insertAt + i, block[i]);

        // Keep group membership in the same order. The destination is the target's group (or
        // ungrouped); a drop inside one group is the same operation.
        var destination = tab.HierarchyGroups.FirstOrDefault(g => g.PieceIds.Contains(target.Id));
        foreach (var piece in block)
        {
            foreach (var group in tab.HierarchyGroups)
                group.PieceIds.Remove(piece.Id);
        }

        if (destination is not null)
        {
            var targetPosition = destination.PieceIds.IndexOf(target.Id);
            var groupInsertAt = targetPosition < 0
                ? destination.PieceIds.Count
                : after ? targetPosition + 1 : targetPosition;
            for (var i = 0; i < block.Count; i++)
                destination.PieceIds.Insert(groupInsertAt + i, block[i].Id);
        }

        SyncStagePieceOrder(tab);
        tab.HasUnsavedChanges = true;
    }

    /// <summary>Moves a block of pieces into a group (null = ungrouped), keeping their order.</summary>
    private void MovePiecesToGroup(IReadOnlyList<int> ids, int groupId)
    {
        if (ActiveTab is not { } tab || ids.Count == 0) return;

        var target = groupId >= 0 ? tab.HierarchyGroups.GetValueOrDefault(groupId) : null;
        var block = OrderedPieces(tab, ids);
        if (block.Count == 0) return;

        var changed = target is null
            ? block.Any(piece => tab.HierarchyGroups.Any(group => group.PieceIds.Contains(piece.Id)))
            : block.Any(piece => !target.PieceIds.Contains(piece.Id));
        if (!changed) return;

        PushUndoSnapshot();
        foreach (var group in tab.HierarchyGroups)
        {
            foreach (var piece in block)
                group.PieceIds.Remove(piece.Id);
        }

        if (target is not null)
        {
            foreach (var piece in block)
                target.PieceIds.Add(piece.Id);
        }

        tab.HasUnsavedChanges = true;
    }

    private static List<StagePieceInstance> OrderedPieces(StageEditorTab tab, IReadOnlyList<int> ids)
        => ids
            .Select(id => tab.ScenePieces.GetValueOrDefault(id))
            .Where(piece => piece is not null)
            .Cast<StagePieceInstance>()
            .OrderBy(piece => tab.ScenePieces.FindIndex(p => p.Id == piece.Id))
            .ToList();

    private static void SyncStagePieceOrder(StageEditorTab tab)
    {
        if (tab.Stage is null) return;

        // Only non-wall pieces live in Stage.pieces.
        tab.Stage.Pieces.Clear();
        foreach (var piece in tab.ScenePieces)
            if (!piece.PiecePlacement.IsWall)
                tab.Stage.Pieces.Add(piece.Obj);
    }

    // Reorders ScenePieces so pieces appear in group order (grouped first, then ungrouped).
    // Call after any group reorder so the save file reflects the visual hierarchy.
    private void ReorderScenePiecesForGroupOrder()
    {
        if (ActiveTab == null) return;
        var walls = ActiveTab.ScenePieces
            .Where(p => p.PiecePlacement.IsWall)
            .ToList();
        var nonWalls = ActiveTab.ScenePieces
            .Where(p => !p.PiecePlacement.IsWall)
            .ToList();
        var allGroupedIds = new HashSet<int>(ActiveTab.HierarchyGroups.SelectMany(g => g.PieceIds));
        var grouped = new List<StagePieceInstance>();
        foreach (var group in ActiveTab.HierarchyGroups)
        foreach (var id in group.PieceIds)
        {
            var piece = nonWalls.Find(p => p.Id == id);
            if (piece != null) grouped.Add(piece);
        }

        var ungroupedPieces = nonWalls.Where(p => !allGroupedIds.Contains(p.Id)).ToList();
        ActiveTab.ScenePieces.Clear();
        ActiveTab.ScenePieces.AddRange(walls);
        ActiveTab.ScenePieces.AddRange(grouped);
        ActiveTab.ScenePieces.AddRange(ungroupedPieces);
    }
}
