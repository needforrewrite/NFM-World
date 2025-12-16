namespace NFMWorld.Util;

internal class FileUtil
{
    public static void LoadFiles(string folder, string[] fileNames, Action<byte[], int, string> action)
    {
        fileNames = fileNames.CloneArray();
        foreach (var file in Directory.GetFiles(folder))
        {
            var a = fileNames.IndexOf(Path.GetFileNameWithoutExtension(file));
            if (a != -1)
            {
                action(System.IO.File.ReadAllBytes(file), a, file);
            }
        }
    }
}