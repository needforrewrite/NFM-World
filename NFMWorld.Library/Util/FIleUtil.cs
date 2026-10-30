namespace NFMWorldLibrary.Util;

public class FileUtil
{
    public static void LoadFiles(string folder, string[] fileNames, Action<byte[], int, string> action)
    {
        if (!VFS.DirectoryExists(folder))
        {
            Logging.Info($"Folder not found: {folder}");
            return;
        }

        var extraFiles = new List<string>();
        foreach (var file in VFS.EnumerateFiles(folder))
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var a = fileNames.IndexOf(fileNameWithoutExtension);
            if (a != -1)
            {
                action(VFS.ReadAllBytes(file), a, fileNameWithoutExtension);
            }
            else
            {
                Logging.Debug($"Extra file found: {file}");
                extraFiles.Add(file);
            }
        }

        var idx = fileNames.Length;
        foreach (var file in extraFiles)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            action(VFS.ReadAllBytes(file), idx++, fileNameWithoutExtension);
        }
    }
    
    public static void LoadFiles(string folder, Action<byte[], string> action)
    {
        if (!VFS.DirectoryExists(folder))
        {
            Logging.Info($"Folder not found: {folder}");
            return;
        }
        foreach (var file in VFS.EnumerateFiles(folder))
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            action(VFS.ReadAllBytes(file), fileNameWithoutExtension);
        }
    }
    
    public static void LoadFiles<T>(string folder, string[] fileNames, Func<byte[], string, T> action, Action<int, T> save)
    {
        if (!VFS.DirectoryExists(folder))
        {
            Logging.Info($"Folder not found: {folder}");
            return;
        }

        var extraFiles = new List<T>();
        
        foreach (var (index, result) in VFS.EnumerateFiles(folder)
                     .AsParallel()
                     .Select(file =>
                     {
                         var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                         var a = fileNames.IndexOf(fileNameWithoutExtension);
                         if (a != -1)
                         {
                             return (Index: a, Result: action(VFS.ReadAllBytes(file), fileNameWithoutExtension));
                         }
                         else
                         {
                             Logging.Debug($"Extra file found: {file}");
                             return (Index: -1, Result: action(VFS.ReadAllBytes(file), fileNameWithoutExtension));
                         }
                     })
                     .ToArray())
        {
            if (index > -1)
                save(index, result);
            else
                extraFiles.Add(result);
        }

        var idx = fileNames.Length;
        foreach (var result in extraFiles)
        {
            save(idx++, result);
        }
    }
    
    public static void LoadFiles<T>(string folder, Func<byte[], string, T> action, Action<T> save)
    {
        if (!VFS.DirectoryExists(folder))
        {
            Logging.Info($"Folder not found: {folder}");
            return;
        }
        
        foreach (var result in VFS.EnumerateFiles(folder)
                     .AsParallel()
                     .Select(file =>
                     {
                         var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                         return action(VFS.ReadAllBytes(file), fileNameWithoutExtension);
                     })
                     .ToArray())
        {
            save(result);
        }
    }
}