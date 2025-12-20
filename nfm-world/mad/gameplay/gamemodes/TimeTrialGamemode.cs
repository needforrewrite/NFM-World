using System.Collections;
using System.Diagnostics;
using NFMWorld.Mad;
using NFMWorld.Util;
using Stride.Core.Mathematics;

public class TimeTrialGamemode : BaseGamemode
{
    private enum TimeTrialState
    {
        NotStarted,
        Countdown,
        InProgress,
        Finished
    }

    private UnlimitedArray<long> thisRunCheckpointMS = new UnlimitedArray<long>();
    private UnlimitedArray<long> bestTimeCheckpointMS = new UnlimitedArray<long>();

    private int _countdownTime = 3;
    // Amount of ticks until we decrease countdown by 1
    private int _innerCountdownTicks = 0;
    private TimeTrialState _currentState = TimeTrialState.NotStarted;

    private int currentCheckpoint = 0;
    private int currentLap = 0;
    private bool writtenData;

    private Stopwatch raceTimer = new Stopwatch();
    private bool loadedBestTimes = false;

    // demo playback and recording
    private bool playback;
    private UnlimitedArray<BitArray> inputs = [];
    private int tick = 0;
    public static bool PlaybackOnReset = true;

    public override void Enter(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        Reset();
        
        carsInRace[0] = new InGameCar(0, GameSparker.GetCar(InRacePhase.playerCarName).Car, 0, 0);

        // ghost
        carsInRace[1] = new InGameCar(carsInRace[0], 0);
        carsInRace[1].Sfx.Mute = true;

        LoadBestSplits(currentStage);
        if(playback) LoadDemoForPlayback(currentStage);

        if(!playback)
        {
            carsInRace[1].CarRef.alphaOverride = 0.0f;
        } else
        {
            carsInRace[1].CarRef.alphaOverride = 0.5f;
        }

        GameSparker.CurrentPhase.RecreateScene();

        _currentState = TimeTrialState.Countdown;
    }

    public override void Exit(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        // Cleanup for Time Trial mode
    }

    public override void Reset()
    {
        base.Reset();

        _currentState = TimeTrialState.NotStarted;
        _countdownTime = 4;
        _innerCountdownTicks = 0; // Tick down immediately
        currentCheckpoint = 0;
        currentLap = 1;
        raceTimer.Reset();
        writtenData = false;

        // splits and best time
        bestTimeCheckpointMS = [];
        thisRunCheckpointMS = [];
        loadedBestTimes = false;

        // demos
        inputs = [];
        tick = 0;
        playback = PlaybackOnReset;
    }

    private void LoadBestSplits(Stage currentStage)
    {
        if(System.IO.File.Exists("data/tts/" + currentStage.Name))
        {
            using(StreamReader reader = new("data/tts/" + currentStage.Name))
            {
                string? line;
                while((line = reader.ReadLine()) != null)
                {
                    long l = long.Parse(line ?? "");

                    bestTimeCheckpointMS[bestTimeCheckpointMS.Count] = l;
                }
            }

            if(bestTimeCheckpointMS.Count == currentStage.checkpoints.Count * currentStage.nlaps)
                    loadedBestTimes = true;
        }
    }

    public override void GameTick(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        FrameTrace.AddMessage($"contox: {carsInRace[0].CarRef.Position.X:0.00}, contoz: {carsInRace[0].CarRef.Position.Z:0.00}, contoy: {carsInRace[0].CarRef.Position.Y:0.00}");
        switch (_currentState)
        {
            case TimeTrialState.NotStarted:
                Enter(carsInRace, currentStage);
                break;
            case TimeTrialState.Countdown:
                CountdownTick();
                break;
            case TimeTrialState.InProgress:
                TimeTrialInRace(carsInRace, currentStage);
                break;
            case TimeTrialState.Finished:
                TimeTrialFinished(carsInRace, currentStage);
                break;
        }
    }

    private void TimeTrialInRace(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        if(playback)
        {
            if(tick < inputs.Count)
            {
                carsInRace[1].Control.Decode(inputs[tick]);   
            } else
            {
                carsInRace[1].Control.Reset();
            }
            
            carsInRace[1].Drive();
        }
        
        RecordControl(carsInRace[0].Control);
        carsInRace[0].Drive();

        if (currentStage.checkpoints.Count == 0)
        {
            // lol
            return;
        }

        G.SetColor(new NFMWorld.Util.Color(0, 0, 0));
        G.DrawString($"Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds / 10:D2}", 100, 200);
        G.DrawString($"Lap: {currentLap}/{currentStage.nlaps}", 100, 250);

        CheckPoint nextCheckpoint = currentStage.checkpoints[currentCheckpoint];
        Vector3 carPos = carsInRace[0].CarRef.Position;

        if((currentCheckpoint != 0 || currentLap != 1) && loadedBestTimes)
        {
            long diff = thisRunCheckpointMS[thisRunCheckpointMS.Count - 1] - bestTimeCheckpointMS[thisRunCheckpointMS.Count - 1];
            if(diff > 0)
            {
                G.SetColor(new NFMWorld.Util.Color(255, 128, 128));
            } else if (diff < 0)
            {
                G.SetColor(new NFMWorld.Util.Color(128, 255, 128));
            }

            long diffSeconds = Math.Abs(diff/1000);
            long diffMs = Math.Abs(diff%1000);

            string fmt = $"{(diff > 0 ? "+" : "-")}{diffSeconds}s {diffMs}ms";

            G.DrawString("This Split: " + fmt, 100, 350);
        }

        if (nextCheckpoint.CheckPointRot == CheckPoint.CheckPointRotation.None)
        {
            if (Math.Abs(carPos.Z - nextCheckpoint.Position.Z) <
                        60.0F + Math.Abs(carsInRace[0].Mad.Scz[0] + carsInRace[0].Mad.Scz[1] + carsInRace[0].Mad.Scz[2] + carsInRace[0].Mad.Scz[3]) / 4.0F &&
                        Math.Abs(carsInRace[0].CarRef.Position.X - nextCheckpoint.Position.X) < 700 &&
                        Math.Abs(carsInRace[0].CarRef.Position.Y - nextCheckpoint.Position.Y + 350) < 450)
            {
                thisRunCheckpointMS[thisRunCheckpointMS.Count] = raceTimer.ElapsedMilliseconds;
                currentCheckpoint++;
                SfxLibrary.checkpoint?.Play();
                if (currentCheckpoint >= currentStage.checkpoints.Count)
                {
                    currentCheckpoint = 0;
                    currentLap++;
                }
            }
        }
        else // None
        {
            if (Math.Abs(carPos.X - nextCheckpoint.Position.X) <
                        60.0F + Math.Abs(carsInRace[0].Mad.Scx[0] + carsInRace[0].Mad.Scx[1] + carsInRace[0].Mad.Scx[2] + carsInRace[0].Mad.Scx[3]) / 4.0F &&
                        Math.Abs(carPos.Z - nextCheckpoint.Position.Z) < 700 &&
                        Math.Abs(carPos.Y - nextCheckpoint.Position.Y + 350) < 450)
            {
                thisRunCheckpointMS[thisRunCheckpointMS.Count] = raceTimer.ElapsedMilliseconds;
                SfxLibrary.checkpoint?.Play();
                currentCheckpoint++;
                if (currentCheckpoint >= currentStage.checkpoints.Count)
                {
                    currentCheckpoint = 0;
                    currentLap++;
                }
            }
        }

        if (currentCheckpoint == currentStage.checkpoints.Count - 1 && currentLap == currentStage.nlaps)
        {
            currentStage.checkpoints[^1].Finish = true;
        }
        else
        {
            currentStage.checkpoints[^1].Finish = false;
        }

        if (currentCheckpoint > 0)
        {
            currentStage.checkpoints[currentCheckpoint - 1].Glow = false;
        }
        else
        {
            currentStage.checkpoints[^1].Glow = false;
        }

        if (currentCheckpoint < currentStage.checkpoints.Count)
        {
            currentStage.checkpoints[currentCheckpoint].Glow = true;
        }

        if (currentLap > currentStage.nlaps)
        {
            _currentState = TimeTrialState.Finished;
            raceTimer.Stop();
        }

        tick++;
    }

    private void TimeTrialFinished(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        bool newBest = false;

        if (!writtenData)
        {
            writtenData = true;
            if(!loadedBestTimes || thisRunCheckpointMS[thisRunCheckpointMS.Count - 1] < bestTimeCheckpointMS[bestTimeCheckpointMS.Count - 1] ) 
            {
                newBest = true;
                // if new best, save it
                if (!Directory.Exists("data/tts"))
                {
                    Directory.CreateDirectory("data/tts");
                }

                using (StreamWriter outputFile = new StreamWriter("data/tts/" + currentStage.Name))
                {
                    foreach (long time in thisRunCheckpointMS) {
                        outputFile.WriteLine(time.ToString());
                    }
                }

                // save demo if new best time
                if(!Directory.Exists("data/tts/demo"))
                {
                    Directory.CreateDirectory("data/tts/demo");
                }


                using (BinaryWriter outputFile = new BinaryWriter(System.IO.File.OpenWrite("data/tts/demo/" + currentStage.Name)))
                {
                    foreach (BitArray enc in inputs) {
                        outputFile.Write(UMath.getIntFromBitArray(enc));
                    }
                }
            }
        }

        G.SetColor(new NFMWorld.Util.Color(128, 255, 128));
        G.DrawString($"Finished! Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds:D2}", 300, 200);

        if(loadedBestTimes || newBest)
        {
            long bestTimeMs = Math.Min(thisRunCheckpointMS[thisRunCheckpointMS.Count - 1], loadedBestTimes ? bestTimeCheckpointMS[bestTimeCheckpointMS.Count - 1] : long.MaxValue);

            TimeSpan t = TimeSpan.FromMilliseconds(bestTimeMs);

            string time = string.Format("{0:D2}:{1:D2}:{2:D2}",
                        t.Minutes, 
                        t.Seconds, 
                        t.Milliseconds);

            G.DrawString("Best time: " + time, 300, 225);
        }
        G.DrawString($"Press R to restart", 300, 250);

        carsInRace[0].Mad.Halted = true;
        carsInRace[0].Drive();
    }

    private void CountdownTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            SfxLibrary.countdown[_countdownTime].Play();
            _innerCountdownTicks = (int)(10 * (1 / GameSparker.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
            {
                _currentState = TimeTrialState.InProgress;
                raceTimer.Start();
            }
        }

        G.SetColor(new NFMWorld.Util.Color(0, 0, 0));
        G.DrawString($"Starting in {_countdownTime}", 400, 300);
    }

    private void LoadDemoForPlayback(Stage currentStage)
    {
        if(System.IO.File.Exists("data/tts/demo/" + currentStage.Name))
        {
            using(BinaryReader reader = new(System.IO.File.OpenRead("data/tts/demo/" + currentStage.Name)))
            {
                int entry;
                while(reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    entry = entry = reader.ReadInt32();
                    BitArray ba = new([entry]);

                    inputs[inputs.Count] = ba;
                }
            }
        } else
        {
            GameSparker.Writer.WriteLine("Demo for stage " + currentStage.Name + " does not exist!");
            playback = false;
        }
    }

    private void RecordControl(Control control)
    {
        BitArray enc = control.Encode();
        inputs[inputs.Count] = enc;
    }

    public override void KeyPressed(Keys key)
    {
        // Handle key presses specific to Time Trial mode
        if (key == Keys.R)
        {
            Reset();
        }
    }

    public override void KeyReleased(Keys key)
    {
        // Handle key releases specific to Time Trial mode
    }

    public override void Render(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        // Time Trial specific rendering logic
    }
}