namespace brigen;

internal static class FileSystemExtensions
{
    public static void CopyDirectory(string srcPath, string dstFolder)
    {
        if (!Directory.Exists(dstFolder))
            Directory.CreateDirectory(dstFolder);

        string[] files = Directory.GetFiles(srcPath);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            string dest = Path.Combine(dstFolder, name);
            File.Copy(file, dest);
        }

        string[] folders = Directory.GetDirectories(srcPath);
        foreach (string folder in folders)
        {
            string name = Path.GetFileName(folder);
            string dest = Path.Combine(dstFolder, name);
            CopyDirectory(folder, dest);
        }
    }
}