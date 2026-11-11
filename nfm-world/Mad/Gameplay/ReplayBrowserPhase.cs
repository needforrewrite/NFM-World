using NFMWorld.Accounts;
using NFMWorld.Reactor;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Files;
using SignalsDotnet;

namespace NFMWorld.Gameplay;

/// <summary>
/// Lists the recordings stored through the VFS and opens one in the viewer.
/// </summary>
/// <remarks>
/// A malformed file is skipped with a count rather than failing the browser, and the list is
/// bounded so a directory full of recordings cannot stall the menu.
/// </remarks>
public sealed class ReplayBrowserPhase : BasePhase
{
    private const int MaxListed = 200;

    private readonly Signal<IReadOnlyList<ReplayBrowserEntry>> _entries = new([]);
    private readonly Signal<string> _message = new("");

    public ReplayBrowserPhase()
    {
        Refresh();
    }

    public override Node? BuildUi() => ReplayBrowserView.Build(
        _entries,
        _message,
        new ReplayBrowserView.Actions(Open: Open, Retry: Retry, Back: GameSparker.PopPhase)
    );

    private void Refresh()
    {
        var pending = TimeTrialSubmissions.List();
        var entries = new List<ReplayBrowserEntry>();
        var malformed = 0;

        if (VFS.DirectoryExists("data/tts"))
        {
            foreach (var file in VFS.EnumerateFiles("data/tts", "*.timetrial", SearchOption.AllDirectories)
                .Take(MaxListed))
            {
                SavedTimeTrial? replay;
                try
                {
                    replay = SavedTimeTrial.Load(file);
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException)
                {
                    malformed++;
                    continue;
                }

                if (replay is null)
                {
                    malformed++;
                    continue;
                }

                var submission = pending.FirstOrDefault(s =>
                    s.StageName == replay.StageName && s.CarName == replay.CarName
                );
                var status = submission?.Status switch
                {
                    "uploaded" => "Uploaded",
                    "pending" or "preparing" => "Upload pending",
                    "publish-required" => "Publish required",
                    "rejected" => "Upload refused",
                    _ => "Local only"
                };
                var retryable = submission is { Status: "pending" or "preparing" or "publish-required" };

                entries.Add(
                    new ReplayBrowserEntry(
                        file,
                        replay.StageName,
                        replay.CarName,
                        FormatTime(replay.DemoData.Ticks.Count),
                        (replay.Version ?? 0) == SavedTimeTrial.CURRENT_VERSION,
                        status,
                        retryable
                    )
                );
            }
        }

        _entries.Value = entries;
        _message.Value = malformed == 0
            ? ""
            : $"{malformed} recording(s) could not be read and were skipped.";
    }

    private static string FormatTime(int ticks)
    {
        var seconds = ticks / 63.0;
        var minutes = (int)(seconds / 60);
        return $"{minutes}:{seconds - minutes * 60:00.000}";
    }

    private void Open(string file)
    {
        var replay = SavedTimeTrial.Load(file);
        if (replay is null)
        {
            _message.Value = "That recording could not be read.";
            return;
        }

        GameSparker.PushPhase(new ReplayViewerPhase(replay));
    }

    private void Retry(string file)
    {
        var replay = SavedTimeTrial.Load(file);
        if (replay is null)
        {
            _message.Value = "That recording could not be read.";
            return;
        }

        var submission = TimeTrialSubmissions.List().FirstOrDefault(s =>
            s.StageName == replay.StageName && s.CarName == replay.CarName
        );
        if (submission is null)
        {
            _message.Value = "There is no pending submission for this recording.";
            return;
        }

        TimeTrialUploader.Retry(submission);
        _message.Value = "Retrying upload...";
        Refresh();
    }
}
