using System.Collections.ObjectModel;
using Hexa.NET.ImGui;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    private EditorSnapshot CaptureEditorSnapshot()
    {
        if (ActiveTab == null)
            return new EditorSnapshot([], []);

        var pieces = ActiveTab.ScenePieces
            .Select(p => new PieceSnapshot(p.PiecePlacement, p.Obj, p.Id))
            .ToList();

        var walls = ActiveTab.StageWalls
            .Select(CreateWallSnapshot)
            .ToList();

        return new EditorSnapshot(pieces, walls);
    }

    private void PushUndoSnapshot()
    {
        if (ActiveTab == null) return;
        _undoStack.Push(CaptureEditorSnapshot());
        _redoStack.Clear();
    }

    private void ApplySnapshot(EditorSnapshot snapshot)
    {
        if (ActiveTab?.Stage == null) return;

        var currentObjs  = new HashSet<StageObject?>(ActiveTab.ScenePieces.Select(p => p.Obj));
        var snapshotObjs = new HashSet<StageObject?>(snapshot.Pieces.Select(p => p.Obj));
        bool needsRebuild = !currentObjs.SetEquals(snapshotObjs);

        var currentWalls = ActiveTab.StageWalls.Select(CreateWallSnapshot).ToList();
        bool wallsChanged = currentWalls.Count != snapshot.Walls.Count ||
                            !currentWalls.SequenceEqual(snapshot.Walls);

        // Rebuild Stage.pieces to match snapshot order (for Save correctness)
        if (needsRebuild)
        {
            ActiveTab.Stage.Pieces.Clear();
            foreach (var s in snapshot.Pieces)
                ActiveTab.Stage.Pieces.Add(s.Obj);
        }

        // Rebuild ScenePieces list
        var newPieces = snapshot.Pieces.Select(s =>
        {
            var existing = ActiveTab.ScenePieces.FirstOrDefault(p => p.Obj == s.Obj);
            if (existing != null)
            {
                existing.Position = s.Piece.Position;
                existing.Rotation = s.Piece.Rotation;
                return existing;
            }
            // Piece was deleted — resurrect it
            var inst = new StagePieceInstance(s.Obj, s.Id)
            {
                PiecePlacement = s.Piece,
            };
            return inst;
        });

        ActiveTab.ScenePieces.Clear();
        ActiveTab.ScenePieces.AddRange(newPieces);

        if (wallsChanged)
        {
            var rebuiltWalls = KeyedCollection.From<int, EditorStageWall>(w => w.Id);
            foreach (var wall in snapshot.Walls)
                rebuiltWalls.Add(new EditorStageWall(wall.Direction, wall.Count, wall.Position, wall.Offset, wall.Id));
            ActiveTab.StageWalls = rebuiltWalls;
        }
        
        ActiveTab.ActivePieceId = -1;
        ActiveTab.SelectedWallId = -1;
        ActiveTab.SelectedPieceIds.Clear();
        ActiveTab.HasUnsavedChanges = true;

        if (needsRebuild)
            RebuildClientRenderer();
        else if (wallsChanged)
            RebuildAllWalls();
    }

    private void PerformUndo()
    {
        if (_undoStack.Count == 0 || ActiveTab == null) return;
        _redoStack.Push(CaptureEditorSnapshot());
        ApplySnapshot(_undoStack.Pop());
    }

    private void PerformRedo()
    {
        if (_redoStack.Count == 0 || ActiveTab == null) return;
        _undoStack.Push(CaptureEditorSnapshot());
        ApplySnapshot(_redoStack.Pop());
    }

    // ── Hierarchy reordering ─────────────────────────────────────────────────

}
