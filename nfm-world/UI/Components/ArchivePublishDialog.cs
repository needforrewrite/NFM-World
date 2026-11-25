using System.Net.Http;
using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary.Radpack;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

internal sealed class ArchivePublishDialog
{
    private sealed record Document(
        AssetKind Kind,
        string Name,
        Account? Account,
        Func<ArchivePublication, Task<AssetUploadResult>> Publish,
        Func<string, string?>? Export);

    private readonly Signal<Document?> _document = new(null);
    private readonly Signal<IReadOnlyList<OwnedAsset>> _uploads = new([]);
    private readonly Signal<int?> _targetId = new(null);
    private readonly Signal<string> _search = new("");
    private readonly Signal<string> _name = new("");
    private readonly Signal<string> _description = new("");
    private readonly Signal<bool> _public = new(true);
    private readonly Signal<bool> _busy = new(false);
    private readonly Signal<bool> _loading = new(false);
    private readonly Signal<string> _listError = new("");
    private readonly Signal<string> _status = new("");
    private readonly Signal<string> _uploadLimit = new("");
    private int _uploadLimitRequest;

    public bool IsOpen => _document.Value is not null;

    public void Open(
        AssetKind kind,
        string name,
        Func<ArchivePublication, Task<AssetUploadResult>> publish,
        Func<string, string?>? export = null
    )
    {
        if (IsOpen) return;
        var document = new Document(kind, name, GameSparker.AccountManager.ActiveAccount, publish, export);
        _uploads.Value = [];
        _search.Value = "";
        _loading.Value = false;
        _listError.Value = "";
        _uploadLimit.Value = "Loading Archive upload limit…";
        Select(document, null);
        _document.Value = document;
        _ = RefreshAsync(document);
        _ = RefreshUploadLimitAsync(document);
    }

    public void Close()
    {
        if (!_busy.Value)
            _document.Value = null;
    }

    public Node Build() => Sx.Show(() => IsOpen, () => BuildDialog(_document.Value!));

    private Node BuildDialog(Document document) => Sx.View(
        name: () => "Archive publishing",
        style: () => Sx.Styles(
            position: Position.Absolute,
            left: 0,
            right: 0,
            top: 0,
            bottom: 0,
            zIndex: 60,
            backgroundColor: new Color(3, 8, 20, 200),
            alignItems: Align.Center,
            justifyContent: Justify.Center
        ),
        onmousedown: e => e.StopPropagation(),
        children: Sx.View(
            style: () => Sx.Styles(
                width: 850,
                maxWidth: 94.Percent(),
                height: 85.Percent(),
                maxHeight: 600,
                flexDirection: FlexDirection.Column,
                gapRow: 12,
                paddingTop: 18,
                paddingBottom: 18,
                paddingLeft: 20,
                paddingRight: 20,
                backgroundColor: new Color(12, 22, 42, 250),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.AccentBorder,
                borderRadius: 10
            ),
            children:
            [
                Label(() => "PUBLISH TO ARCHIVE", 20, Theme.TextPrimary),
                Label(
                    () => $"{(document.Kind == AssetKind.StagePiece ? "Stage part" : AssetKinds.Name((byte)document.Kind))} · {document.Name}",
                    13,
                    Theme.TextSecondary
                ),
                Sx.View(
                    style: () => Sx.Styles(height: 0, minHeight: 0, flexGrow: 1, gapColumn: 14),
                    children: [UploadList(document), PublicationForm()]
                ),
                Label(() => _status.Value, 12, Theme.TextPrimary),
                Sx.View(
                    style: () => Sx.Styles(flexShrink: 0, justifyContent: Justify.FlexEnd, gapColumn: 8),
                    children:
                    [
                        Button.Create(() => "CLOSE", Close, ButtonVariant.Ghost, size: ButtonSize.Sm, consumeGesture: true),
                        ..(document.Export is null ? Array.Empty<Node>() :
                            [Button.Create(() => "EXPORT PACK", () => Export(document), size: ButtonSize.Sm)]),
                        Sx.Show(
                            () => !_busy.Value && !_loading.Value && document.Account is not null
                                  && (_targetId.Value is null || _uploads.Value.Any(
                                      asset => asset.Id == _targetId.Value && !asset.IsTakedown
                                  )),
                            () => Button.Create(
                                () => _targetId.Value is null ? "PUBLISH NEW" : "UPDATE UPLOAD",
                                () => _ = SubmitAsync(document),
                                ButtonVariant.Primary,
                                size: ButtonSize.Sm
                            )
                        )
                    ]
                )
            ]
        )
    );

    private Node UploadList(Document document) => Sx.View(
        style: () => Sx.Styles(width: 0, minWidth: 0, flexGrow: 1, flexDirection: FlexDirection.Column, gapRow: 6),
        children:
        [
            Label(() => "YOUR ARCHIVE UPLOADS", 12, Theme.TextSecondary),
            Button.Create(
                () => "NEW UPLOAD",
                () => Select(document, null),
                active: () => _targetId.Value is null,
                size: ButtonSize.Sm,
                style: () => Sx.Styles(justifyContent: Justify.FlexStart, flexShrink: 0)
            ),
            Controls.TextField(() => _search.Value, value => _search.Value = value, () => "Search your uploads"),
            Sx.View(
                style: () => Sx.Styles(
                    height: 0,
                    minHeight: 0,
                    flexGrow: 1,
                    flexDirection: FlexDirection.Column,
                    overflow: Overflow.Scroll,
                    paddingRight: 8,
                    gapRow: 4
                ),
                children: Sx.For(
                    () => _uploads.Value.Where(MatchesSearch).ToArray(),
                    (asset, _) => UploadRow(document, asset)
                )
            ),
            Label(() => document.Account is null ? "Sign in from the main menu to publish."
                : _loading.Value ? "Loading your uploads…"
                : _listError.Value.Length > 0 ? _listError.Value
                : _uploads.Value.Count == 0 ? "You have no uploads in this category."
                : _uploads.Value.Any(MatchesSearch) ? "" : "No matching uploads.", 12, Theme.TextSecondary),
            Sx.Show(
                () => document.Account is not null && !_loading.Value,
                () => Button.Create(
                    () => "REFRESH",
                    () => _ = RefreshAsync(document),
                    ButtonVariant.Ghost,
                    size: ButtonSize.Sm,
                    style: () => Sx.Styles(alignSelf: Align.FlexStart, flexShrink: 0)
                )
            )
        ]
    );

    private bool MatchesSearch(OwnedAsset asset)
        => asset.Name.Contains(_search.Value.Trim(), StringComparison.OrdinalIgnoreCase);

    private Node UploadRow(Document document, OwnedAsset asset)
    {
        var hovered = new Signal<bool>(false);
        var visibility = asset.IsTakedown ? "Taken down" : asset.IsPublic ? "Public" : "Hidden";
        if (!asset.IsTakedown && asset.ReviewStatus is "Pending" or "Rejected")
            visibility = asset.ReviewLabel!;
        return Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
                flexDirection: FlexDirection.Column,
                gapRow: 3,
                paddingTop: 7,
                paddingBottom: 7,
                paddingLeft: 10,
                paddingRight: 10,
                borderRadius: 5,
                backgroundColor: _targetId.Value == asset.Id ? Theme.AccentSoftStrong
                    : hovered.Value ? Theme.SurfaceHover : Theme.Surface
            ),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: e => { e.StopPropagation(); Select(document, asset); },
            children:
            [
                Label(() => asset.Name, 13, Theme.TextPrimary),
                Label(() => $"{visibility} · {asset.DownloadCount} downloads · #{asset.Id}", 11, Theme.TextSecondary)
            ]
        );
    }

    private Node PublicationForm() => Sx.View(
        style: () => Sx.Styles(
            width: 0,
            minWidth: 0,
            flexGrow: 1,
            flexDirection: FlexDirection.Column,
            overflow: Overflow.Scroll,
            paddingRight: 8,
            gapRow: 8
        ),
        children:
        [
            Label(() => _targetId.Value is null ? "NEW LISTING" : "UPDATE EXISTING LISTING", 12, Theme.TextSecondary),
            Label(() => "NAME", 11, Theme.TextSecondary),
            Controls.TextField(() => _name.Value, value => { if (!_busy.Value) _name.Value = value; }, () => "Name"),
            Label(() => "DESCRIPTION", 11, Theme.TextSecondary),
            Controls.TextField(
                () => _description.Value,
                value => { if (!_busy.Value) _description.Value = value; },
                () => "Optional"
            ),
            Controls.Checkbox(
                () => _public.Value,
                () => { if (!_busy.Value) _public.Value = !_public.Value; },
                () => "List publicly"
            ),
            Label(() => "The upload includes unsaved editor changes.", 12, Theme.TextSecondary),
            Label(() => _uploadLimit.Value, 12, Theme.TextSecondary),
            Sx.Show(
                () => _targetId.Value is not null,
                () => Label(
                    () => "Replacing content can unrank time trials that used the previous version.",
                    12,
                    Theme.TextSecondary
                )
            )
        ]
    );

    private static Node Label(Func<string> text, int size, Color color) => Sx.Text(
        text: text,
        style: () => Sx.TextStyles(
            fontSize: size,
            foregroundColor: color,
            breakType: BreakType.Word,
            overflowBehavior: OverflowBehavior.ContinueVertically
        ),
        layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0)
    );

    private void Select(Document document, OwnedAsset? asset)
    {
        if (_busy.Value) return;
        _targetId.Value = asset?.Id;
        _name.Value = asset?.Name ?? document.Name;
        _description.Value = asset?.Description ?? "";
        _public.Value = asset?.IsPublic ?? true;
        _status.Value = asset?.IsTakedown == true ? "The Archive has taken down this upload." : "";
    }

    private async Task RefreshAsync(Document document)
    {
        if (document.Account is null || _loading.Value || _busy.Value
            || document.Account.Token != GameSparker.AccountManager.ActiveAccount?.Token) return;
        _loading.Value = true;
        _listError.Value = "";
        IReadOnlyList<OwnedAsset>? uploads = null;
        string? error = null;
        try
        {
            uploads = await GameSparker.AccountManager.ListOwnedAssetsAsync(AssetKinds.Name((byte)document.Kind));
        }
        catch (Exception ex)
        {
            error = $"Could not load your uploads: {ex.Message}";
        }

        await LoadingWork.NextFrame;
        if (!ReferenceEquals(_document.Value, document)) return;
        _loading.Value = false;
        _listError.Value = error ?? "";
        if (uploads is not null) _uploads.Value = uploads;
    }

    private async Task RefreshUploadLimitAsync(Document document)
    {
        var request = ++_uploadLimitRequest;
        var limit = await GameSparker.AccountManager.GetAssetUploadLimitAsync();
        await LoadingWork.NextFrame;
        if (request != _uploadLimitRequest || !ReferenceEquals(_document.Value, document)) return;

        _uploadLimit.Value = limit is { } bytes
            ? $"Maximum Archive upload size: {bytes / (1024d * 1024):0.##} MiB."
            : "The Archive's upload limit is unavailable.";
    }

    private async Task SubmitAsync(Document document)
    {
        if (_busy.Value || _loading.Value || document.Account is null) return;
        if (document.Account.Token != GameSparker.AccountManager.ActiveAccount?.Token)
        {
            _status.Value = "Your account changed. Reopen publishing to continue.";
            return;
        }

        var name = _name.Value.Trim();
        if (name.Length == 0)
        {
            _status.Value = "Enter a name for the upload.";
            return;
        }

        if (_targetId.Value is { } id && !_uploads.Value.Any(asset => asset.Id == id && !asset.IsTakedown))
        {
            _status.Value = "Choose an available upload to update.";
            return;
        }

        var publication = new ArchivePublication(name, _description.Value.Trim(), _public.Value, _targetId.Value);
        _busy.Value = true;
        _status.Value = publication.AssetId is null ? "Publishing…" : "Updating upload…";
        AssetUploadResult result;
        try
        {
            result = await document.Publish(publication);
        }
        catch (Exception ex)
        {
            result = new(false, $"Could not finish publishing: {ex.Message}", null);
        }

        await LoadingWork.NextFrame;
        _busy.Value = false;
        _status.Value = result.Message;
        _ = RefreshUploadLimitAsync(document);
        if (!result.Success) return;

        await RefreshAsync(document);
        if (ReferenceEquals(_document.Value, document) && publication.AssetId is null && _targetId.Value is null
            && result.ContentHash is { } hash && _uploads.Value.FirstOrDefault(asset => asset.ContentHash == hash) is { } saved)
        {
            Select(document, saved);
            _status.Value = result.Message;
        }
    }

    private void Export(Document document)
    {
        if (_busy.Value || document.Export is null) return;
        var name = _name.Value.Trim();
        var path = document.Export(name.Length > 0 ? name : document.Name);
        _status.Value = path is null ? "The pack could not be exported." : $"Exported to {path}.";
    }
}
