using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NFMWorld.CrashReporter;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sentry;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
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

    public static void Load(bool isHeadless = true)
    {
        if (_loaded)
            return;
        _loaded = true;

        IBackend.Backend = new ServerBackend();
        
        SentrySdk.Init(options =>
        {
            options.Dsn = Logging.SentryDsn;
            options.Debug = false;
            options.TracesSampleRate = 0.05;
            options.Release = Logging.Release;
        });
        SentrySdk.CaptureMessage("Hello world", SentryLevel.Debug);

#if !DEBUG
                    if (!isHeadless && !System.Diagnostics.Debugger.IsAttached)
                    {
                        CrashReportLibrary.Hook(Logging.SentryDsn, Logging.Release);
                    }
            #endif
        
        VFS.MountDirectory(AppDomain.CurrentDomain.BaseDirectory);
        VFS.MountDirectory(Directory.GetCurrentDirectory());
        VFS.MountWriteDestination(Directory.GetCurrentDirectory());
        
        var modsFolder = Path.Combine(Directory.GetCurrentDirectory(), "mods");
        if (Directory.Exists(modsFolder))
            VFS.MountDirectory(modsFolder);

        cars.Add(Collection.NFMM, []);
        FileUtil.LoadFiles(
            "./data/models/nfmm/cars",
            CarRads,
            (ais, fileName) => RadParser.ParseRad(Encoding.UTF8.GetString(ais), "nfmm/" + fileName),
            (id, result) => cars[Collection.NFMM][id] = result  
        );
        
        FileUtil.LoadFiles(
            "./data/models/nfmm/stage",
            StageRads,
            (ais, fileName) => RadParser.ParseRad(Encoding.UTF8.GetString(ais), "nfmm/" + fileName),
            (id, result) => stage_parts[id] = result  
        );

        cars.Add(Collection.World, []);
        FileUtil.LoadFiles(
            "./data/models/world/cars",
            (ais, fileName) => RadParser.ParseRad(Encoding.UTF8.GetString(ais), "world/" + fileName),
            result => cars[Collection.World].Add(result)
        );

        cars.Add(Collection.Elo, []);
        FileUtil.LoadFiles(
            "./data/models/elo/cars",
            (ais, fileName) => RadParser.ParseRad(Encoding.UTF8.GetString(ais), "elo/" + fileName),
            result => cars[Collection.Elo].Add(result)
        );

        cars.Add(Collection.Football, []);
        FileUtil.LoadFiles(
            "./data/models/football/cars",
            (ais, fileName) => RadParser.ParseRad(Encoding.UTF8.GetString(ais), "football/" + fileName),
            result => cars[Collection.Football].Add(result)
        );

        FileUtil.LoadFiles(
            "./data/models/world/stage",
            (ais, fileName) => RadParser.ParseRad(Encoding.UTF8.GetString(ais), "world/" + fileName),
            result => vendor_stage_parts.Add(result)
        );
        
        FileUtil.LoadFiles(
            "./data/models/football/stage",
            (ais, fileName) => RadParser.ParseRad(Encoding.UTF8.GetString(ais), "football/" + fileName),
            result => vendor_stage_parts.Add(result)
        );

        cars.Add(Collection.User, []);
        FileUtil.LoadFiles(
            "./data/models/user/cars",
            (ais, fileName) =>
            {
                try
                {
                    return RadParser.ParseRad(Encoding.UTF8.GetString(ais), "user/" + fileName);
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureEvent(new SentryEvent(ex)
                    {
                        Message = $"Error loading user car part '{fileName}'"
                    });
                    Logging.Info($"Error loading user car '{fileName}': {ex.Message}\n{ex.StackTrace}");
                    return null;
                }
            },
            result =>
            {
                if (result != null)
                    cars[Collection.User].Add(result);
            }
        );

        FileUtil.LoadFiles(
            "./data/models/user/cars",
            (ais, fileName) =>
            {
                try
                {
                    return RadParser.ParseRad(Encoding.UTF8.GetString(ais), "user/" + fileName);
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureEvent(new SentryEvent(ex)
                    {
                        Message = $"Error loading user stage part '{fileName}'"
                    });
                    Logging.Info($"Error loading user car '{fileName}': {ex.Message}\n{ex.StackTrace}");
                    return null;
                }
            },
            result =>
            {
                if (result != null)
                    user_stage_parts.Add(result);
            }
        );

        error_mesh = RadParser.ParseRad(Encoding.UTF8.GetString(VFS.ReadAllBytes("./data/models/error.rad")), "error.rad");
        
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

    private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        static string GetPlatformName()
        {
            if (OperatingSystem.IsWindows())
            {
                return "windows";
            }

            if (OperatingSystem.IsMacOS())
            {
                return  "osx";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux";
            }

            if (OperatingSystem.IsFreeBSD())
            {
                return "freebsd";
            }

            if (OperatingSystem.IsAndroid())
            {
                return "android";
            }

            // What is this platform??
            return "unknown";
        }

        if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
        {
            return NativeLibrary.GetMainProgramHandle(); // statically linked
        }

        string os = GetPlatformName();
        string cpu = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string wordsize = (IntPtr.Size * 8).ToString();
        
#if DEBUG
        string debugLibrarySuffix = "d";
#else
        string debugLibrarySuffix = System.Diagnostics.Debugger.IsAttached ? "d" : string.Empty;
#endif

        if (libraryName == "Kernel32.dll")
        {
            return NativeLibrary.Load("kernel32.dll", assembly, searchPath);
        }

        var newLibraryName = libraryName switch
        {
            _ => os switch
            {
                "windows" => $"{libraryName}.dll",
                "osx" => $"lib{libraryName}.dylib",
                "linux" or "freebsd" or "netbsd" => $"lib{libraryName}.so",
                _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
            }
        };
        
        var dir = os switch
        {
            "windows" => cpu switch
            {
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "arm64",
                "x64" or "x86_64" or "amd64" => "x64",
                "x86" or "x86_32" or "i386" => "x86",
                _ => throw new PlatformNotSupportedException($"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}")
            },
            "osx" => "osx",
            "linux" or "freebsd" or "netbsd" => cpu switch
            {
                "arm32" or "armv7" or "aarch32" or "armeabi-v7a" => "libarmhf",
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "libaarch64",
                "x64" or "x86_64" or "amd64" => "lib64",
                "x86" or "x86_32" or "i386" => "lib32",
                _ => throw new PlatformNotSupportedException($"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}")
            },
            "android" => cpu switch
            {
                "arm32" or "armv7" or "aarch32" or "armeabi-v7a" => "android-armeabi-v7a",
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "android-arm64-v8a",
                "x64" or "x86_64" or "amd64" => "android-x86_64",
                "x86" or "x86_32" or "i386" => "android-x86",
                _ => throw new PlatformNotSupportedException($"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}")
            },
            _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
        };
        
        return NativeLibrary.Load($"libs/{dir}/{newLibraryName}");
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

        if (dynamic_models.TryGetValue(name, out var dynRad))
        {
            return dynRad;
        }
        
        var radpackPath = $"data/models/{name}.radpack";
        if (VFS.FileExists(radpackPath))
        {
            try
            {
                total += dynamic_models.Count;
                var radpack = RadpackSerializer.Deserialize(VFS.ReadAllBytes(radpackPath));
                if (radpack is not RadpackRad3d rad)
                {
                    throw new InvalidOperationException("Radpack does not contain a Rad3d Model");
                }
                return dynamic_models[name] = (total, rad.Rad);
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

        var relativePath = $"data/models/{name}.rad";
        if (VFS.FileExists(relativePath))
        {
            try
            {
                total += dynamic_models.Count;
                var rad = RadParser.ParseRad(VFS.ReadAllText(relativePath), name);
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

        var radpackPath = $"data/models/{name}.radpack";
        if (VFS.FileExists(radpackPath))
        {
            try
            {
                total += dynamic_models.Count;
                var radpack = RadpackSerializer.Deserialize(VFS.ReadAllBytes(radpackPath));
                if (radpack is not RadpackRad3d rad)
                {
                    throw new InvalidOperationException("Radpack does not contain a Rad3d Model");
                }
                return dynamic_models[name] = (total, rad.Rad);
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

        var relativePath = $"data/models/{name}.rad";
        if (VFS.FileExists(relativePath))
        {
            try
            {
                total += dynamic_models.Count;
                var rad = RadParser.ParseRad(VFS.ReadAllText(relativePath), name);
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
}
