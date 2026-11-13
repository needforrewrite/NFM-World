using NFMWorld.UI.Routes;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Gameplay;

/// <summary>
/// The lobby's create dialog stage list. LOCAL offers the shipped stages a session may use;
/// ONLINE is the public archive, which the phase searches because it owns the network work.
/// </summary>
/// <remarks>
/// The list lives on <c>UiState.Lobby.CreateStages</c> so the selector renders one state either
/// way, and so switching source does not rebuild the dialog. Everything here is pure: the phase
/// keeps the network and the session's chosen stage.
/// </remarks>
internal static class LobbyStageSelection
{
    public const string LocalStageRestriction =
        "Local stages have to be published before they can be raced online.";

    /// <summary>The local stages a multiplayer session may use, labelled and grouped.</summary>
    public static IReadOnlyList<StageOption> LocalStages(
        IReadOnlyList<string> stageIds,
        IReadOnlyDictionary<string, string> names
    ) => stageIds
        .Where(MultiplayerContentPolicy.IsAcceptedStage)
        .Select(id => OptionFor(id, names))
        .ToArray();

    /// <summary>
    /// Search results as selectable stages. An archive stage is named by its content hash, which
    /// is what the lobby and the game master resolve, and by the author that published it.
    /// </summary>
    public static IReadOnlyList<StageOption> RemoteStages(IEnumerable<RemoteAsset> items)
        => items
            .Where(item => item.Eligible && item.ContentHash.Length > 0)
            .Select(item => new StageOption(
                    RadpackReader.ArchivePrefix + item.ContentHash.ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(item.Name) ? ShortHash(item.ContentHash) : item.Name,
                    string.IsNullOrWhiteSpace(item.Author) ? null : item.Author
                )
            )
            .ToArray();

    private static string ShortHash(string hash) => hash[..Math.Min(8, hash.Length)];

    /// <summary>Shows the local list, keeping the session's stage when it is still in it.</summary>
    public static void ShowLocal(StageSelectState state, IReadOnlyList<StageOption> local, string selectedStage)
    {
        state.OnlineSource.Value = false;
        state.Search.Value = "";
        StageSelectView.ClearSearch();
        Show(state, local, selectedStage);
        // A stage the list cannot offer (a user/ stage, say) is not left looking chosen.
        Select(state, state.Stages.Value.Any(option => option.Id == selectedStage) ? selectedStage : "");
    }

    /// <summary>
    /// Shows archive search results. The session's stage is kept: it stays the chosen one, and
    /// <see cref="WithSelection"/> adds its row when the results no longer contain it.
    /// </summary>
    public static void ShowRemote(StageSelectState state, IReadOnlyList<StageOption> remote)
    {
        state.OnlineSource.Value = true;
        Show(state, remote, state.Selected.Value);
        Select(state, state.Selected.Value);
    }

    /// <summary>
    /// Adds the session's stage back when the visible list does not contain it: a public stage can
    /// still be the chosen one after switching away from the archive, or after a search that no
    /// longer matches it. Without the row the highlight would point at nothing and the list would
    /// look like nothing is chosen. It goes first, where the list starts scrolled to.
    /// </summary>
    private static IReadOnlyList<StageOption> WithSelection(
        IReadOnlyList<StageOption> options,
        string selectedStage
    )
    {
        if (!MultiplayerContentPolicy.IsCanonicalArchiveId(selectedStage)
            || options.Any(option => option.Id == selectedStage))
            return options;

        return [ArchivePlaceholder(selectedStage), .. options];
    }

    /// <summary>Narrows the local list to the search box's text.</summary>
    public static void ApplyFilter(StageSelectState state, IReadOnlyList<StageOption> local, string query)
    {
        var search = query.Trim();
        state.Search.Value = query;
        var matches = search.Length == 0
            ? local
            : local
                .Where(option => option.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
                                 || option.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                )
                .ToArray();

        // A published stage has no local file to browse; its archive id is the only handle, so
        // one typed into search is offered directly.
        if (MultiplayerContentPolicy.IsCanonicalArchiveId(search))
            matches = [ArchivePlaceholder(search), .. matches];

        state.Stages.Value = WithSelection(matches, state.Selected.Value);
        // Filtering the list does not drop the session's stage; it stays chosen even when the
        // search hides its row.
        state.ConfirmEnabled.Value = state.Selected.Value.Length > 0;
    }

    /// <summary>
    /// Points the selector at a stage. Returns false when the lobby would refuse it, in which case
    /// the caller must not hand it to the session - a refused choice must not look accepted.
    /// </summary>
    public static bool Select(StageSelectState state, string stageId)
    {
        state.Selected.Value = stageId;

        if (stageId.Length == 0)
        {
            state.Restriction.Value = "";
            state.ConfirmEnabled.Value = false;
            return false;
        }

        if (MultiplayerContentPolicy.IsAcceptedStage(stageId))
        {
            state.Restriction.Value = "";
            state.ConfirmEnabled.Value = true;
            return true;
        }

        state.Restriction.Value = LocalStageRestriction;
        return false;
    }

    private static void Show(StageSelectState state, IReadOnlyList<StageOption> options, string selectedStage)
    {
        state.Busy.Value = false;
        state.Status.Value = "";
        state.Restriction.Value = "";
        state.Stages.Value = WithSelection(options, selectedStage);
    }

    private static StageOption ArchivePlaceholder(string stageId)
        => new(stageId, "Public stage " + stageId[^8..], "public archive");

    private static StageOption OptionFor(string stageId, IReadOnlyDictionary<string, string> names)
    {
        var slash = stageId.LastIndexOf('/');
        return new StageOption(
            stageId,
            names.TryGetValue(stageId, out var name) ? name : slash < 0 ? stageId : stageId[(slash + 1)..],
            CollectionOf(stageId)
        );
    }

    private static string CollectionOf(string stageId)
    {
        var slash = stageId.IndexOf('/');
        return slash < 0 ? "stages" : stageId[..slash];
    }
}
