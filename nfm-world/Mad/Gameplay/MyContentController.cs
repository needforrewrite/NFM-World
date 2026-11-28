using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;

namespace NFMWorld.Gameplay;

/// <summary>
/// MY CONTENT, reached from the main menu's WORKSHOP: the signed-in account's own uploads.
///
/// It edits the same archive rows the website's edit page does, and it owns the whole surface
/// (list, rename dialog, delete confirmation) so the menu phase only pushes it and routes keys.
/// </summary>
internal sealed class MyContentController : IDisposable
{
    private readonly Action _back;
    private int _generation;
    private bool _disposed;
    private Node? _view;

    public MyContentController(Action back)
    {
        _back = back;
        _ = RefreshAsync();
    }

    public Node BuildUi() => _view = MyContentView.Build(
        new MyContentView.Actions(
            Back: NavigateBack,
            Refresh: () => _ = RefreshAsync(),
            Edit: BeginEdit,
            ToggleVisibility: row => _ = ToggleVisibilityAsync(row),
            Delete: ConfirmDelete,
            Save: SaveEdit,
            CancelEdit: () => UiState.MyContent.Editing.Value = null
        ),
        canNavigate: () => GameSparker.devConsole?.IsOpen() != true
    );

    public void KeyPressed(Key key)
    {
        var state = UiState.MyContent;

        // The editor is a modal: its fields own the keyboard while it is open.

        if (state.Editing.Value is not null)
        {
            if (key == Key.Escape)
                state.Editing.Value = null;

            return;
        }

        if (FocusManager.FocusedNode is TextInput or TextArea)

            return;

        switch (key)
        {
            case Key.Up:
                Move(-1);

                break;
            case Key.Down:
                Move(1);

                break;
            case Key.Enter:
                if (state.Selected is { } selected)
                    BeginEdit(selected);

                break;
            case Key.H:
                if (state.Selected is { IsTakedown: false } row)
                    _ = ToggleVisibilityAsync(row);

                break;
            case Key.Delete:
                if (state.Selected is { } doomed)
                    ConfirmDelete(doomed);

                break;
            case Key.Escape:
                NavigateBack();

                break;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _generation++;
        UiState.MyContent.Reset();
    }

    private void NavigateBack()
    {
        if (UiState.MyContent.Editing.Value is not null)
        {
            UiState.MyContent.Editing.Value = null;

            return;
        }

        _back();
    }

    private void Move(int delta)
    {
        var state = UiState.MyContent;
        var count = state.Items.Value.Count;

        if (count == 0)

            return;

        state.Cursor.Value = (Math.Clamp(state.Cursor.Value, 0, count - 1) + delta + count) % count;
    }

    /// <summary>Loads the account's uploads. Nothing is cached between visits, so a delete elsewhere shows up.</summary>
    private async Task RefreshAsync()
    {
        var state = UiState.MyContent;
        var generation = ++_generation;
        state.Busy.Value = true;
        state.Status.Value = "";

        if (!GameSparker.AccountManager.LoggedIn)
        {
            state.Items.Value = [];
            state.Busy.Value = false;
            state.Status.Value = "Sign in from the main menu to manage your uploads.";

            return;
        }

        try
        {
            var items = await Task.Run(() => GameSparker.AccountManager.ListOwnedAssetsAsync());
            await LoadingWork.NextFrame;

            if (_disposed || generation != _generation)

                return;

            state.Items.Value = items;
            state.Cursor.Value = Math.Clamp(state.Cursor.Value, 0, Math.Max(0, items.Count - 1));
            state.Status.Value = items.Count == 0 ? "" : $"{items.Count} upload(s).";
        }
        catch (Exception ex)
        {
            // Fired and forgotten by the controller, so nothing may escape: an unobserved fault
            // reaches the web build's crash reporter.
            await LoadingWork.NextFrame;
            if (!_disposed && generation == _generation)
                state.Status.Value = $"Could not reach the archive: {ex.Message}";
        }
        finally
        {
            if (!_disposed && generation == _generation)
                state.Busy.Value = false;
        }
    }

    private void BeginEdit(OwnedAsset asset)
    {
        var state = UiState.MyContent;
        state.Editing.Value = asset;
        state.EditName.Value = asset.Name;
        state.EditDescription.Value = asset.Description ?? "";
        state.EditPublic.Value = asset.IsPublic;
        state.Status.Value = "";
    }

    private void SaveEdit()
    {
        if (UiState.MyContent.Editing.Value is { } asset)
            _ = SaveEditAsync(asset);
    }

    private async Task SaveEditAsync(OwnedAsset asset)
    {
        var state = UiState.MyContent;
        var name = state.EditName.Value.Trim();

        if (name.Length == 0)
        {
            state.Status.Value = "A name is required.";

            return;
        }

        state.Busy.Value = true;
        var result = await GameSparker.AccountManager.UpdateAssetAsync(
            asset.Id,
            name,
            state.EditDescription.Value,
            state.EditPublic.Value
        );
        await LoadingWork.NextFrame;

        state.Busy.Value = false;
        state.Status.Value = result.Success ? "Changes saved." : result.Message ?? "The archive refused the change.";

        if (result.Success)
        {
            state.Editing.Value = null;
            await RefreshAsync();
        }
    }

    private async Task ToggleVisibilityAsync(OwnedAsset asset)
    {
        var state = UiState.MyContent;
        state.Busy.Value = true;
        var result = await GameSparker.AccountManager.UpdateAssetAsync(asset.Id, isPublic: !asset.IsPublic);
        await LoadingWork.NextFrame;

        state.Busy.Value = false;
        state.Status.Value = result.Success
            ? asset.IsPublic ? "Upload hidden." : "Upload listed publicly."
            : result.Message ?? "The archive refused the change.";
        if (result.Success)
            await RefreshAsync();
    }

    /// <summary>Deleting is irreversible, so it always goes through a confirmation.</summary>
    private void ConfirmDelete(OwnedAsset asset)
    {
        GameSparker.MessageWindow.ShowYesNo(
            "Delete upload?",
            $"\"{asset.Name}\" will be removed from the archive. Time trials recorded against it stop ranking.",
            result =>
            {
                if (result == MessageWindow.MessageResult.Yes)
                    _ = DeleteAsync(asset);
            }
        );
    }

    private async Task DeleteAsync(OwnedAsset asset)
    {
        var state = UiState.MyContent;
        state.Busy.Value = true;
        var result = await GameSparker.AccountManager.DeleteAssetAsync(asset.Id);
        await LoadingWork.NextFrame;

        state.Busy.Value = false;
        state.Status.Value = result.Success ? "Upload deleted." : result.Message ?? "The archive refused the delete.";

        if (result.Success)
        {
            state.Cursor.Value = Math.Max(0, state.Cursor.Value - 1);
            await RefreshAsync();
        }
    }
}
