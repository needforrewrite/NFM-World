namespace NFMWorld.Util;

internal class FileUtil
{
    public static void LoadFiles(string folder, string[] fileNames, Action<byte[], int, string> action)
    {
        if (!Directory.Exists(folder))
        {
            Console.WriteLine($"Folder not found: {folder}");
            return;
        }
        foreach (var file in Directory.GetFiles(folder))
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var a = fileNames.IndexOf(fileNameWithoutExtension);
            if (a != -1)
            {
                action(System.IO.File.ReadAllBytes(file), a, fileNameWithoutExtension);
            }
        }
    }
    public static void LoadFiles(string folder, Action<byte[], string> action)
    {
        if (!Directory.Exists(folder))
        {
            Console.WriteLine($"Folder not found: {folder}");
            return;
        }
        foreach (var file in Directory.GetFiles(folder))
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            action(System.IO.File.ReadAllBytes(file), fileNameWithoutExtension);
        }
    }
}