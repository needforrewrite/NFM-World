using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Maxine.Extensions;
using Maxine.VFS;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Mad.Files;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary;

public static class BackendGameSparker
{
    public static Dictionary<Collection, UnlimitedArray<Rad3d>> cars = new();
    public static UnlimitedArray<Rad3d> stage_parts = [];
    public static UnlimitedArray<Rad3d> vendor_stage_parts = [];
    public static UnlimitedArray<Rad3d> user_stage_parts = [];
    public static Dictionary<string, (int Index, Rad3d Rad)> dynamic_models = new();
    public static Rad3d error_mesh;

    public static readonly string[] CarRads =
    [
        "2000tornados", "formula7", "canyenaro", "lescrab", "nimi", "maxrevenge", "leadoxide", "koolkat", "drifter",
        "policecops", "mustang", "king", "audir8", "masheen", "radicalone", "drmonster"
    ];

    public static readonly string[] StageRads =
    [
        "road", "froad", "twister2", "twister1", "turn", "offroad", "bumproad", "offturn", "nroad", "nturn",
        "roblend", "noblend", "rnblend", "roadend", "offroadend", "hpground", "ramp30", "cramp35", "dramp15",
        "dhilo15", "slide10", "takeoff", "sramp22", "offbump", "offramp", "sofframp", "halfpipe", "spikes", "rail",
        "thewall", "checkpoint", "fixpoint", "offcheckpoint", "sideoff", "bsideoff", "uprise", //45
        "riseroad", "sroad", "soffroad", "tside", "launchpad", "thenet", "speedramp", "offhill", "slider", "uphill",
        "roll1", "roll2", "roll3", "roll4", "roll5", "roll6", "opile1", "opile2", "aircheckpoint",
        "tree1", "tree2", "tree3", "tree4",  "tree5", "tree6", "tree7", "tree8", "cac1", "cac2", "cac3",
        "8sroad", "8soffroad"
    ];

    private static bool _loaded;

    public static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;
        
        SentrySdk.Init(options =>
        {
            // A Sentry Data Source Name (DSN) is required.
            // See https://docs.sentry.io/product/sentry-basics/dsn-explainer/
            // You can set it in the SENTRY_DSN environment variable, or you can set it in code here.
            options.Dsn = Logging.SentryDsn;

            // When debug is enabled, the Sentry client will emit detailed debugging information to the console.
            // This might be helpful, or might interfere with the normal operation of your application.
            // We enable it here for demonstration purposes when first trying Sentry.
            // You shouldn't do this in your applications unless you're troubleshooting issues with Sentry.
            options.Debug = false;

            // This option is recommended. It enables Sentry's "Release Health" feature.
            options.AutoSessionTracking = true;

            // Set TracesSampleRate to 1.0 to capture 100%
            // of transactions for tracing.
            // We recommend adjusting this value in production.
            options.TracesSampleRate = 0.05;
            
            // Enable logs to be sent to Sentry
            options.EnableLogs = true;
            
            // Try get NFMWorld assembly version first
            options.Release = Logging.Release;
        });
        SentrySdk.CaptureMessage("Hello world", SentryLevel.Debug);
        
        var realFs = new RelativeFileSystem(Directory.GetCurrentDirectory());
        VFS.MountNewFileTarget(realFs);
        
        // VFS.MountFileSystem(new HttpFileSystem());
        VFS.MountFileSystem(realFs);
        var modsFolder = Path.Combine(Directory.GetCurrentDirectory(), "mods");
        if (Directory.Exists(modsFolder))
            VFS.MountFileSystem(new RelativeFileSystem(modsFolder));

        cars.Add(Collection.NFMM, []);
        FileUtil.LoadFiles("./data/models/nfmm/cars", CarRads, (ais, id, fileName) =>
        {
            cars[Collection.NFMM][id] = RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
            {
                FileName = "nfmm/" + fileName
            };
        });

        FileUtil.LoadFiles("./data/models/nfmm/stage", StageRads, (ais, id, fileName) =>
        {
            stage_parts[id] = RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
            {
                FileName = "nfmm/" + fileName
            };
        });

        cars.Add(Collection.World, []);
        FileUtil.LoadFiles("./data/models/world/cars", (ais, fileName) =>
        {
            cars[Collection.World].Add(RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
            {
                FileName = "world/" + fileName
            });
        });

        cars.Add(Collection.Elo, []);
        FileUtil.LoadFiles("./data/models/elo/cars", (ais, fileName) =>
        {
            cars[Collection.Elo].Add(RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
            {
                FileName = "elo/" + fileName
            });
        });

        cars.Add(Collection.Football, []);
        FileUtil.LoadFiles("./data/models/football/cars", (ais, fileName) =>
        {
            cars[Collection.Football].Add(RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
            {
                FileName = "football/" + fileName
            });
        });

        FileUtil.LoadFiles("./data/models/world/stage", (ais, fileName) =>
        {
            vendor_stage_parts.Add(RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
            {
                FileName = "world/" + fileName
            });
        });

        FileUtil.LoadFiles("./data/models/football/stage", (ais, fileName) =>
        {
            vendor_stage_parts.Add(RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
            {
                FileName = "football/" + fileName
            });
        });

        cars.Add(Collection.User, []);
        FileUtil.LoadFiles("./data/models/user/cars", (ais, fileName) =>
        {
            try
            {
                cars[Collection.User].Add(RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
                {
                    FileName = "user/" + fileName
                });
            }
            catch (Exception ex)
            {
                Logging.Info($"Error loading user car '{fileName}': {ex.Message}\n{ex.StackTrace}");
            }
        });

        FileUtil.LoadFiles("./data/models/user/stage", (ais, fileName) =>
        {
            try
            {
                user_stage_parts.Add(RadParser.ParseRad(Encoding.UTF8.GetString(ais)) with
                {
                    FileName = "user/" + fileName
                });
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureEvent(new SentryEvent(ex)
                {
                    Message = $"Error loading user stage part '{fileName}'"
                });
                Logging.Info($"Error loading user stage part '{fileName}': {ex.Message}\n{ex.StackTrace}");
            }
        });

        error_mesh = RadParser.ParseRad(Encoding.UTF8.GetString(VFS.ReadAllBytes("./data/models/error.rad"))) with
        {
            FileName = "error.rad"
        };
        
        for (var i = 0; i < StageRads.Length; i++) {
            if (stage_parts[i] == null) {
                SentrySdk.CaptureMessage("No valid ContO (Stage Part) has been assigned to ID " + i + " (" + StageRads[i] + ")", SentryLevel.Error);
                throw new Exception("No valid ContO (Stage Part) has been assigned to ID " + i + " (" + StageRads[i] + ")");
            }
        }
        for (var i = 0; i < CarRads.Length; i++) {
            if (cars[Collection.NFMM][i] == null)
            {
                SentrySdk.CaptureMessage("No valid ContO (Vehicle) has been assigned to ID " + i + " (" + StageRads[i] + ")", SentryLevel.Error);
                throw new Exception("No valid ContO (Vehicle) has been assigned to ID " + i + " (" + StageRads[i] + ")");
            }
        }
    }

    public static (int Id, Rad3d? Rad) GetCar(string name)
    {
        var total = 0;
        foreach (var t in cars.Values)
        {
            foreach (var car in t)
            {
                if (string.Equals(car.FileName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (total, car);
                }

                total++;
            }
        }

        if (Path.IsPathRooted(name))
        {
            if (dynamic_models.TryGetValue(name, out var dynRad))
            {
                return dynRad;
            }
            try
            {
                total += dynamic_models.Count;
                var rad = RadParser.ParseRad(System.IO.File.ReadAllText(name)) with
                {
                    FileName = name
                };
                return dynamic_models[name] = (total, rad);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureEvent(new SentryEvent(ex)
                {
                    Message = $"Error loading dynamic model '{name}'"
                });
                Logging.Info($"Error loading dynamic model '{name}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        SentrySdk.CaptureMessage("No results for GetCar: " + name, SentryLevel.Warning);
        Logging.Info("No results for GetCar: " + name);
        return (-1, null!);
    }

    public static (int Id, Rad3d? Rad) GetStagePart(string name)
    {
        IReadOnlyList<Rad3d>[] arrays = [stage_parts, vendor_stage_parts, user_stage_parts];

        var total = 0;
        foreach (var t in arrays)
        {
            foreach (var part in t)
            {
                if (string.Equals(part.FileName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (total, part);
                }

                total++;
            }
        }

        if (Path.IsPathRooted(name))
        {
            if (dynamic_models.TryGetValue(name, out var dynRad))
            {
                return dynRad;
            }
            try
            {
                total += dynamic_models.Count;
                var rad = RadParser.ParseRad(System.IO.File.ReadAllText(name)) with
                {
                    FileName = name
                };
                return dynamic_models[name] = (total, rad);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureEvent(new SentryEvent(ex)
                {
                    Message = $"Error loading dynamic model '{name}'"
                });
                Logging.Info($"Error loading dynamic model '{name}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        SentrySdk.CaptureMessage("No results for GetStagePart: " + name, SentryLevel.Warning);
        Logging.Info("No results for GetStagePart: " + name);
        return (-1, null!);
    }
    
    public static string GetModelName(int index, bool forCar = false)
    {
        var models = forCar ? CarRads : StageRads;
        
        if (index >= 0 && index < models.Length)
        {
            return models[index];
        }
        
        return "";
    }
    
    [UnmanagedCallersOnly(EntryPoint = "nfmw_get_tt_info", CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe GetTTInfoResult GetTTInfo(GetTTInfoArgs* args)
    {
        try
        {
            using var timeTrialMemory =
                new UnmanagedMemoryManager<byte>(args->TimeTrialData, args->TimeTrialDataLength);
            var timeTrial = SavedTimeTrial.Load(timeTrialMemory.Memory);
            if (timeTrial == null)
            {
                SentrySdk.CaptureMessage("Failed to load time trial data", SentryLevel.Error);
                throw new InvalidOperationException("Failed to load time trial data");
            }

            return new GetTTInfoResult
            {
                CheckpointCount = timeTrial.Splits.SplitTimes.Count,
                ReplayVersion = timeTrial.Version ?? 0,
                BackendVersion = SavedTimeTrial.CURRENT_VERSION,
                TickCount = timeTrial.DemoData.Ticks.Count,
                HasError = false
            };
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            return new GetTTInfoResult
            {
                CheckpointCount = -1,
                ReplayVersion = -1,
                BackendVersion = SavedTimeTrial.CURRENT_VERSION,
                TickCount = -1,
                HasError = true,
                Exception = NativeException.FromException(ex)
            };
        }
    }
    [InlineArray(16384)]
    public struct ErrorBuffer
    {
        public byte Data;
        public Span<byte> AsSpan()
        {
            unsafe
            {
                fixed (byte* ptr = &Data)
                {
                    return new Span<byte>(ptr, 16384);
                }
            }
        }
    }
    [InlineArray(1024)]
    public struct ErrorMessageBuffer
    {
        public byte Data;
        public Span<byte> AsSpan()
        {
            unsafe
            {
                fixed (byte* ptr = &Data)
                {
                    return new Span<byte>(ptr, 1024);
                }
            }
        }
    }
        
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeException
    {
        public ErrorMessageBuffer TypeName;
        public ErrorMessageBuffer Message;
        public ErrorBuffer StackTrace;
            
        public static NativeException FromException(Exception ex)
        {
            var typeNameBytes = Encoding.UTF8.GetBytes(ex.GetType().FullName ?? "UnknownException");
            var messageBytes = Encoding.UTF8.GetBytes(ex.Message);
            var stackTraceBytes = Encoding.UTF8.GetBytes(ex.StackTrace ?? "");

            var nativeEx = new NativeException();
            typeNameBytes.AsSpan(0, Math.Min(typeNameBytes.Length, 1024)).CopyTo(nativeEx.TypeName.AsSpan());
            messageBytes.AsSpan(0, Math.Min(messageBytes.Length, 1024)).CopyTo(nativeEx.Message.AsSpan());
            stackTraceBytes.AsSpan(0, Math.Min(stackTraceBytes.Length, 16384)).CopyTo(nativeEx.StackTrace.AsSpan());
                
            return nativeEx;
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GetTTInfoArgs
    {
        // Pointer to time trial data
        public byte* TimeTrialData;
        // Length of time trial data
        public int TimeTrialDataLength;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct GetTTInfoResult
    {
        // Number of checkpoints in the time trial
        public required int CheckpointCount;
        public required int TickCount;
        public required int ReplayVersion;
        public required int BackendVersion;

        // Whether an error occurred
        public required bool HasError;
        // Error information
        public NativeException Exception;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LoadResult
    {
        // Whether an error occurred
        public required bool HasError;
        // Error information
        public NativeException Exception;
    }

    /// <summary>
    /// Loads the backend.
    /// </summary>
    /// <returns></returns>
    [UnmanagedCallersOnly(EntryPoint = "nfmw_load", CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe LoadResult LoadUnmanaged()
    {
        try
        {
            Load();
            return new LoadResult
            {
                HasError = false
            };
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            return new LoadResult
            {
                HasError = true,
                Exception = NativeException.FromException(ex)
            };
        }
    }

    /// <summary>
    /// Simulates a time trial to completion with a limit of 100M ticks. Returns the number of elapsed ticks, or -1 on
    /// timeout.
    /// </summary>
    /// <param name="args">The args</param>
    /// <returns></returns>
    [UnmanagedCallersOnly(EntryPoint = "nfmw_simulate_tt", CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe SimulateTimeTrialResult SimulateTimeTrial(SimulateTimeTrialArgs* args)
    {
        try
        {
            using var timeTrialMemory =
                new UnmanagedMemoryManager<byte>(args->TimeTrialData, args->TimeTrialDataLength);
            var timeTrial = SavedTimeTrial.Load(timeTrialMemory.Memory);

            var simulator = timeTrial.StageData is {} stageData
                ? BackendRaceValues.Create(Encoding.UTF8.GetString(args->StageName), stageData)
                : BackendRaceValues.Create(Encoding.UTF8.GetString(args->StageName));

            var gamemode = new TimeTrialSimulationGamemode(new BaseGamemodeParameters()
            {
                PlayerCarIndex = 0,
                Players =
                [
                    new PlayerParameters()
                    {
                        PlayerName = "Player",
                        CarName = Encoding.UTF8.GetString(args->Cars[0].CarName),
                        Color = new Color3(255, 0, 0),
                        IsBot = false
                    }
                ]
            }, simulator, timeTrial);

            return new SimulateTimeTrialResult
            {
                ElapsedTicks = gamemode.SimulateToCompletion(timeTrial.DemoData.Ticks.Count + 500) ?? -1,
                ExpectedTicks = timeTrial.DemoData.Ticks.Count,
                HasError = false
            };
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            return new SimulateTimeTrialResult
            {
                ElapsedTicks = -1,
                ExpectedTicks = -1,
                HasError = true,
                Exception = NativeException.FromException(ex)
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SimulateTimeTrialResult
    {
        // The result code: number of ticks elapsed, or -1 on timeout or error
        public required int ElapsedTicks;
        // Number of input ticks in the replay
        public required int ExpectedTicks;

        // Whether an error occurred
        public required bool HasError;
        // Error information
        public NativeException Exception;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SimulateTimeTrialArgs
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct CarInfoUnmanaged
        {
            // Pointer to UTF-8 encoded car name, null-terminated
            public byte* CarName;
        }

        // Pointer to UTF-8 encoded stage name, null-terminated
        public byte* StageName;
        
        // Pointer to array of CarInfoUnmanaged
        public CarInfoUnmanaged* Cars;
        // Number of cars
        public int CarCount;
        
        // Pointer to time trial data
        public byte* TimeTrialData;
        // Length of time trial data
        public int TimeTrialDataLength;
    }
}