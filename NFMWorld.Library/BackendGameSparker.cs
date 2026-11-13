using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Maxine.Extensions;
using NFMWorld.CrashReporter;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.PluginSystem;
using NFMWorld.Sentry;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary;

[PluginVisible]
public static class BackendGameSparker
{
    [PluginVisible] public static Dictionary<Collection, UnlimitedArray<Rad3d>> cars = new();
    public static UnlimitedArray<Rad3d> stage_parts = [];
    public static UnlimitedArray<Rad3d> vendor_stage_parts = [];
    public static UnlimitedArray<Rad3d> user_stage_parts = [];

    /// <summary>Models registered at runtime (test drives), keyed by name. Cars only.</summary>
    public static Dictionary<string, (int Index, Rad3d Rad)> dynamic_models = new();

    // File-backed models, separated by kind and canonical identity so a car and a stage piece
    // with the same name cannot collide.
    private static readonly Dictionary<string, (int Index, Rad3d Rad)> _carFiles =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (int Index, Rad3d Rad)> _stagePartFiles =
        new(StringComparer.OrdinalIgnoreCase);

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
        "tree1", "tree2", "tree3", "tree4", "tree5", "tree6", "tree7", "tree8", "cac1", "cac2", "cac3",
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
            }
        );
        SentrySdk.CaptureMessage("Hello world", SentryLevel.Debug);

#if !DEBUG
        if (!isHeadless && !System.Diagnostics.Debugger.IsAttached)
        {
            CrashReportLibrary.Hook(Logging.SentryDsn, Logging.Release);
        }

#endif

#if DEBUG
        // mount live project path for debug
        if (ProjectUtils.TryGetProjectDirectory() is { } projectDirectory)
        {
            VFS.MountDirectory(projectDirectory);
            VFS.MountDirectory(Path.GetFullPath(Path.Combine(projectDirectory, "..", "NFMWorld.Library")));
        }

#endif

        // The browser has no install directory and no writable one: the head mounts the asset
        // archive and a MEMFS write root before calling this, and every path below resolves
        // through those. See NFMWorld.Platform.BrowserBoot in the game project.
        if (!OperatingSystem.IsBrowser())
        {
            VFS.MountDirectory(AppDomain.CurrentDomain.BaseDirectory);
            VFS.MountDirectory(Directory.GetCurrentDirectory());
            if (OperatingSystem.IsMacOS() && !isHeadless)
            {
                var applicationData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(applicationData))
                    applicationData = Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                        "Library",
                        "Application Support"
                    );
                var userDirectory = Path.Combine(applicationData, "NFMWorld");
                VFS.MountWriteDestination(userDirectory);
                VFS.MountDirectory(userDirectory, prepend: true);
                Logging.Info($"User data directory: {userDirectory}");
            }
            else
            {
                VFS.MountWriteDestination(Directory.GetCurrentDirectory());
            }

            var modsFolder = Path.Combine(Directory.GetCurrentDirectory(), "mods");
            if (Directory.Exists(modsFolder))
                VFS.MountDirectory(modsFolder);
        }

        // After the write root exists, and before any network mount, so a prepared asset is never
        // fetched again. Both only answer archive/<hash> paths. The browser keeps its cache in
        // MEMFS, which lasts for the session; without this mount every prepare there fails.
        ContentCache.Mount();
        if (!OperatingSystem.IsBrowser())
            ArchiveMount.MountIfConfigured(ArchiveEndpoint.Url);

        cars.Add(Collection.NFMM, []);
        LoadModelDirectory(
            "./data/models/nfmm/cars",
            CarRads,
            RadpackType.Car,
            "nfmm/",
            (id, result) => cars[Collection.NFMM][id] = result,
            null,
            tolerant: false
        );

        LoadModelDirectory(
            "./data/models/nfmm/stage",
            StageRads,
            RadpackType.TrackPiece,
            "nfmm/",
            (id, result) => stage_parts[id] = result,
            null,
            tolerant: false
        );

        cars.Add(Collection.World, []);
        LoadModelDirectory(
            "./data/models/world/cars",
            null,
            RadpackType.Car,
            "world/",
            null,
            result => cars[Collection.World].Add(result),
            tolerant: false
        );

        cars.Add(Collection.Elo, []);
        LoadModelDirectory(
            "./data/models/elo/cars",
            null,
            RadpackType.Car,
            "elo/",
            null,
            result => cars[Collection.Elo].Add(result),
            tolerant: false
        );

        cars.Add(Collection.Football, []);
        LoadModelDirectory(
            "./data/models/football/cars",
            null,
            RadpackType.Car,
            "football/",
            null,
            result => cars[Collection.Football].Add(result),
            tolerant: false
        );

        LoadModelDirectory(
            "./data/models/world/stage",
            null,
            RadpackType.TrackPiece,
            "world/",
            null,
            result => vendor_stage_parts.Add(result),
            tolerant: false
        );

        LoadModelDirectory(
            "./data/models/football/stage",
            null,
            RadpackType.TrackPiece,
            "football/",
            null,
            result => vendor_stage_parts.Add(result),
            tolerant: false
        );

        cars.Add(Collection.User, []);
        LoadModelDirectory(
            "./data/models/user/cars",
            null,
            RadpackType.Car,
            "user/",
            null,
            result => cars[Collection.User].Add(result),
            tolerant: true
        );

        // User stage pieces have their own folder. This used to read the car folder, so a user
        // car could be registered as a stage piece.
        LoadModelDirectory(
            "./data/models/user/stage",
            null,
            RadpackType.TrackPiece,
            "user/",
            null,
            result => user_stage_parts.Add(result),
            tolerant: true
        );

        error_mesh = RadParser.ParseRad(Encoding.UTF8.GetString(VFS.ReadAllBytes("./data/models/error.rad")), "error.rad");

        for (var i = 0; i < StageRads.Length; i++)
        {
            if (stage_parts[i] == null)
            {
                SentrySdk.CaptureMessage(
                    "No valid ContO (Stage Part) has been assigned to ID " + i + " (" + StageRads[i] + ")",
                    SentryLevel.Error
                );
                throw new Exception("No valid ContO (Stage Part) has been assigned to ID " + i + " (" + StageRads[i] + ")");
            }
        }

        for (var i = 0; i < CarRads.Length; i++)
        {
            if (cars[Collection.NFMM][i] == null)
            {
                SentrySdk.CaptureMessage(
                    "No valid ContO (Vehicle) has been assigned to ID " + i + " (" + StageRads[i] + ")",
                    SentryLevel.Error
                );
                throw new Exception("No valid ContO (Vehicle) has been assigned to ID " + i + " (" + StageRads[i] + ")");
            }
        }
    }

    /// <summary>
    /// Loads a folder of <c>.rad</c> text models and <c>.radpack</c> models of one kind.
    /// </summary>
    /// <remarks>
    /// The kind is passed to the reader rather than inferred from the file, so a car radpack in a
    /// stage-piece folder is rejected instead of loaded as a piece. Shipped folders are strict
    /// (a broken stock model fails the load); user folders skip the broken file and keep going.
    /// </remarks>
    private static void LoadModelDirectory(
        string folder,
        string[]? orderedNames,
        RadpackType kind,
        string labelPrefix,
        Action<int, Rad3d>? saveIndexed,
        Action<Rad3d>? save,
        bool tolerant
    )
    {
        if (!VFS.DirectoryExists(folder))
        {
            Logging.Info($"Folder not found: {folder}");
            return;
        }

        var extras = new List<Rad3d>();

        foreach (var file in VFS.EnumerateFiles(folder))
        {
            var extension = Path.GetExtension(file);
            if (!extension.Equals(".rad", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".radpack", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = Path.GetFileNameWithoutExtension(file);
            var index = orderedNames?.IndexOf(name) ?? -1;
            if (orderedNames is not null && index == -1)
                Logging.Debug($"Extra file found: {file}");

            Rad3d? model;
            try
            {
                model = ReadModelFile(file, extension, kind, labelPrefix + name);
            }
            catch (Exception ex) when (tolerant)
            {
                SentrySdk.CaptureEvent(new SentryEvent(ex) { Message = $"Error loading user model '{file}'" });
                Logging.Info($"Error loading model '{file}': {ex.Message}\n{ex.StackTrace}");
                continue;
            }

            if (model is null)
                continue;

            if (index > -1)
                saveIndexed!(index, model);
            else
                extras.Add(model);
        }

        var next = orderedNames?.Length ?? 0;
        foreach (var model in extras)
        {
            if (saveIndexed is not null)
                saveIndexed(next++, model);
            else
                save!(model);
        }
    }

    private static Rad3d? ReadModelFile(string file, string extension, RadpackType kind, string label)
    {
        if (extension.Equals(".radpack", StringComparison.OrdinalIgnoreCase))
        {
            var asset = RadpackReader.Read(file, kind);
            return asset is RadpackRad3d rad ? rad.Rad : null;
        }

        return RadParser.ParseRad(Encoding.UTF8.GetString(VFS.ReadAllBytes(file)), label);
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
                return "osx";
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
                _ => throw new PlatformNotSupportedException(
                    $"Unsupported platform: {os}, please update {nameof(ImportResolver)}"
                )
            }
        };

        var dir = os switch
        {
            "windows" => cpu switch
            {
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "arm64",
                "x64" or "x86_64" or "amd64" => "x64",
                "x86" or "x86_32" or "i386" => "x86",
                _ => throw new PlatformNotSupportedException(
                    $"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}"
                )
            },
            "osx" => "osx",
            "linux" or "freebsd" or "netbsd" => cpu switch
            {
                "arm32" or "armv7" or "aarch32" or "armeabi-v7a" => "libarmhf",
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "libaarch64",
                "x64" or "x86_64" or "amd64" => "lib64",
                "x86" or "x86_32" or "i386" => "lib32",
                _ => throw new PlatformNotSupportedException(
                    $"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}"
                )
            },
            "android" => cpu switch
            {
                "arm32" or "armv7" or "aarch32" or "armeabi-v7a" => "android-armeabi-v7a",
                "arm64" or "armv8" or "armv8-a" or "aarch64" or "arm64-v8a" => "android-arm64-v8a",
                "x64" or "x86_64" or "amd64" => "android-x86_64",
                "x86" or "x86_32" or "i386" => "android-x86",
                _ => throw new PlatformNotSupportedException(
                    $"Unsupported CPU architecture: {cpu}, please update {nameof(ImportResolver)}"
                )
            },
            _ => throw new PlatformNotSupportedException($"Unsupported platform: {os}, please update {nameof(ImportResolver)}")
        };

        return NativeLibrary.Load($"libs/{dir}/{newLibraryName}");
    }

    [PluginVisible]
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

        var identity = RadpackReader.Identity(name);
        if (_carFiles.TryGetValue(identity, out var cachedCar))
            return cachedCar;

        var model = RadpackReader.TryReadModel(name, RadpackType.Car, out var modelError);
        if (modelError is { } carError)
        {
            // A file that exists but is the wrong kind or unreadable is a real error: falling
            // through would substitute a legacy download of the same name for it.
            Logging.Error($"Error loading car '{name}': {carError}");
            return (-1, null!);
        }

        if (model is not null)
        {
            var index = total + _carFiles.Count + dynamic_models.Count;
            return _carFiles[identity] = (index, model);
        }

        // Read-only legacy import: a .radq the user dropped in data/legacy/cars first, then the
        // original download host.
        if ((LegacyAssets.LegacyAssetImporter.TryReadLocal("cars", name)
             ?? LegacyAssets.LegacyAssetImporter.TryFetch("cars", name)) is { } legacyRad)
        {
            try
            {
                total += _carFiles.Count + dynamic_models.Count;
                var rad = RadParser.ParseRad(legacyRad, name);
                Logging.Info($"Imported legacy car '{name}' from .radq.");
                return _carFiles[identity] = (total, rad);
            }
            catch (Exception ex)
            {
                Logging.Info($"Error importing legacy car '{name}': {ex.Message}");
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

        var identity = RadpackReader.Identity(name);
        if (_stagePartFiles.TryGetValue(identity, out var cachedPart))
            return cachedPart;

        // Stage pieces are a different kind from cars: sharing a cache with GetCar let a car
        // named like a piece be used as that piece.
        var model = RadpackReader.TryReadModel(name, RadpackType.TrackPiece, out var partError);
        if (partError is { } error)
        {
            Logging.Error($"Error loading stage part '{name}': {error}");
            return (-1, null!);
        }

        if (model is not null)
        {
            var index = total + _stagePartFiles.Count;
            return _stagePartFiles[identity] = (index, model);
        }

        // Read-only legacy import, same on-demand path as GetCar. Only a phy:/ds:/nfm: label
        // names a download; a bare part name has no source outside the shipped archive.
        if ((LegacyAssets.LegacyAssetImporter.TryReadLocal("stage", name)
             ?? LegacyAssets.LegacyAssetImporter.TryFetchModel(name)) is { } legacyPart)
        {
            try
            {
                total += _stagePartFiles.Count;
                var rad = RadParser.ParseRad(legacyPart, name);
                Logging.Info($"Imported legacy stage part '{name}'.");
                return _stagePartFiles[identity] = (total, rad);
            }
            catch (Exception ex)
            {
                Logging.Info($"Error importing legacy stage part '{name}': {ex.Message}");
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
