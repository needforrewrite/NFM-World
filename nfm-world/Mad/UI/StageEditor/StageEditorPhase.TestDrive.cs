using NFMWorld.Gameplay;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    private static readonly string[] TestDriveModes = ["Solo", "With AI cars"];

    private bool _testDriveWithAi = true;
    private bool _showTestDriveSaveDialog;
    private bool _testDriveLaunched;

    /// <summary>
    /// Test drives the saved stage: garage for the car, then a free-play race, back here on exit.
    /// Unsaved edits have to reach the file first, because the garage and race load it by name.
    /// </summary>
    private void LaunchTestDrive()
    {
        if (ActiveTab is not { Stage: not null } tab || tab.StageFileName is not { Length: > 0 } fileName)
        {
            GameSparker.MessageWindow.ShowMessage("Test drive unavailable", "There is no stage to test drive.");
            return;
        }

        if (tab.HasUnsavedChanges)
        {
            _showTestDriveSaveDialog = true;
            Touch();
            return;
        }

        BeginTestDrive(fileName);
    }

    private void BeginTestDrive(string fileName)
    {
        if (_testDriveLaunched)
            return;

        var stageName = $"user/{fileName}";

        // Loads the stage, so a broken document fails here rather than inside the phase stack.
        GaragePhase garage;
        try
        {
            garage = new GaragePhase(MenuHistory.Car, stageName);
        }
        catch (Exception exception)
        {
            Logging.Error($"Unable to open the garage for test drive: {exception.Message}");
            GameSparker.MessageWindow.ShowMessage("Test drive unavailable", exception.Message);
            return;
        }

        _testDriveLaunched = true;
        // Pushing the garage calls Exit() on this phase; that must not discard the open documents.
        _preserveTabsOnExit = true;

        garage.CarSelected += (_, car) => StartTestDriveRace(stageName, car.FileName);
        garage.CarSelectionCancelled += (_, _) => EndTestDrive();
        GameSparker.PushPhase(garage, PhaseManager.Groups.Event);
    }

    private void StartTestDriveRace(string stageName, string carName)
    {
        BaseGamemodeFactory factory;
        try
        {
            factory = GamemodeFactory.Create(
                "nfmm/pvp",
                new Dictionary<string, object> { ["constraint"] = "both" }
            );
        }
        catch (Exception exception)
        {
            FailTestDrive("Unable to start test drive", exception);
            return;
        }

        var players = FreePlayRoster.Create(carName, _testDriveWithAi);

        LocalRaceHost host;
        try
        {
            host = LocalRaceHost.Create(stageName, factory, new ClientGamemodeParameters { Players = players });
        }
        catch (Exception exception)
        {
            factory.End();
            FailTestDrive("Unable to start test drive", exception);
            return;
        }

        RacePhase race;
        try
        {
            race = new RacePhase(stageName, factory, players, host);
        }
        catch (Exception exception)
        {
            host.Dispose();
            factory.End();
            FailTestDrive("Unable to initialize test drive", exception);
            return;
        }

        race.Exited += (_, _) => EndTestDrive();
        GameSparker.Phases.ReplaceGroup(race, PhaseManager.Groups.Event);
    }

    private void FailTestDrive(string title, Exception exception)
    {
        Logging.Error($"{title}: {exception.Message}");
        GameSparker.MessageWindow.ShowMessage(title, exception.Message);
        EndTestDrive();
    }

    /// <summary>Pops the garage/race event group; the stage editor underneath re-enters.</summary>
    private void EndTestDrive()
    {
        _testDriveLaunched = false;
        GameSparker.PopGroup(PhaseManager.Groups.Event);
    }

    private object?[] TestDriveSections(StageEditorTab tab) =>
    [
        PropertySection(
            "TEST DRIVE",
            360,
            PropertyRow(
                "Opponents",
                Controls.Dropdown(
                    options: () => TestDriveModes,
                    value: Observe(() => _testDriveWithAi ? 1 : 0),
                    onSelect: index =>
                    {
                        _testDriveWithAi = index == 1;
                        Touch();
                    },
                    width: 160,
                    compact: true
                ),
                76
            ),
            SmallButton("START TEST DRIVE", LaunchTestDrive),
            EditorText(
                style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 110)),
                layout: () => Sx.Styles(width: 340, flexShrink: 0),
                text: () => "Opens the garage, then races this stage. Quitting the race returns here."
            ),
            EditorText(
                style: () => Sx.TextStyles(fontSize: 9, foregroundColor: new Color(255, 255, 255, 90)),
                layout: () => Sx.Styles(width: 340, flexShrink: 0),
                text: () => tab.HasUnsavedChanges
                    ? "Unsaved changes are saved before the drive."
                    : "The saved stage is used."
            )
        )
    ];

    private Node TestDriveSaveDialog()
        => Dialog(
            () => _showTestDriveSaveDialog,
            "TEST DRIVE",
            () => [
                EditorText(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: new Color(255, 255, 255, 160)),
                    text: () => "Test drive loads the saved stage. Save your changes first?"
                ),
                DialogButtons(
                    "SAVE & DRIVE",
                    () =>
                    {
                        SaveStage();
                        if (ActiveTab is { } tab && !tab.HasUnsavedChanges
                            && tab.StageFileName is { Length: > 0 } fileName)
                        {
                            _showTestDriveSaveDialog = false;
                            Touch();
                            BeginTestDrive(fileName);
                            return;
                        }

                        // The save failed; leave the prompt up so the user can retry or cancel.
                        Touch();
                    },
                    () =>
                    {
                        _showTestDriveSaveDialog = false;
                        Touch();
                    }
                )
            ]
        );
}
