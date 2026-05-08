namespace NFMWorldLibrary.Util;

public class FileUtil
{
    public static void LoadFiles(string folder, string[] fileNames, Action<byte[], int, string> action)
    {
        if (!VFS.Exists(folder))
        {
            Logging.Info($"Folder not found: {folder}");
            return;
        }
        foreach (var file in VFS.GetFiles(folder))
        {
            var fileNameWithoutExtension = VFS.Path.GetFileNameWithoutExtension(file);
            var a = fileNames.IndexOf(fileNameWithoutExtension);
            if (a != -1)
            {
                action(VFS.ReadAllBytes(file), a, fileNameWithoutExtension);
            }
        }
    }
    public static void LoadFiles(string folder, Action<byte[], string> action)
    {
        if (!VFS.Exists(folder))
        {
            Logging.Info($"Folder not found: {folder}");
            return;
        }
        foreach (var file in VFS.GetFiles(folder))
        {
            var fileNameWithoutExtension = VFS.Path.GetFileNameWithoutExtension(file);
            action(VFS.ReadAllBytes(file), fileNameWithoutExtension);
        }
    }
}