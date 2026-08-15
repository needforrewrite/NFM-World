using System.Diagnostics;
using Lua.IO;
using Lua.Platforms;

namespace NFMWorldLibrary.Util;

public class LuaNfmwPlatform
{
    public static LuaPlatform Instance { get; } = 
        new(
            FileSystem: new FileSystem(),
            OsEnvironment: new SystemOsEnvironment(),
            StandardIO: new ConsoleStandardIO(),
            TimeProvider: TimeProvider.System
        );

    public sealed class SystemOsEnvironment : ILuaOsEnvironment
    {
        private static readonly Process CurrentProcesss = Process.GetCurrentProcess();

        public string? GetEnvironmentVariable(string name)
        {
            return null;
        }

        public ValueTask Exit(int exitCode, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public double GetTotalProcessorTime()
        {
            return CurrentProcesss.TotalProcessorTime.TotalSeconds;
        }
    }

    public sealed class FileSystem : ILuaFileSystem
    {
        public bool IsReadable(string path)
        {
            return VFS.FileExists(path);
        }

        public ValueTask<ILuaStream> Open(string path, LuaFileOpenMode mode, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ILuaStream.CreateFromStream(VFS.OpenRead(path), LuaFileOpenMode.Read));
        }

        public ValueTask Rename(string oldName, string newName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask Remove(string path, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public string GetTempFileName()
        {
            throw new NotSupportedException();
        }

        public ValueTask<ILuaStream> OpenTempFileStream(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}