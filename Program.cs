using NuGet.ProjectModel;

Console.Write("Root directory:");
string path = Console.ReadLine()!;

Console.WriteLine($"Collecting {LockFileFormat.AssetsFileName}, {LockFileFormat.LockFileName} files...");

var assets = EnumerateLockFiles(path).ToList();

static IEnumerable<LockFile> EnumerateLockFiles(string rootDirectory)
{
    var format = new LockFileFormat();

    foreach (string fileName in Directory.EnumerateFiles(rootDirectory, LockFileFormat.AssetsFileName, SearchOption.AllDirectories)
        .Concat(Directory.EnumerateFiles(rootDirectory, LockFileFormat.LockFileName, SearchOption.AllDirectories)))
    {
        LockFile? lockFile = null;

        try
        {
            lockFile = format.Read(fileName);
        }
        catch
        {
            Console.WriteLine($"  Failed parsing {fileName}. Skipping.");
        }

        if (lockFile != null)
            yield return lockFile;
    }
}

Console.WriteLine($"Successfully collected {assets.Count} files.");
