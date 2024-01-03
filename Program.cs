using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Versioning;

Console.Write("Root directory:");
string path = Console.ReadLine()!;

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

static IEnumerable<PackagesLockFile> EnumeratePackagesLockFile(string rootDirectory)
{
    foreach (string fileName in Directory.EnumerateFiles(rootDirectory, PackagesLockFileFormat.LockFileName, SearchOption.AllDirectories))
    {
        PackagesLockFile? lockFile = null;

        try
        {
            lockFile = PackagesLockFileFormat.Read(fileName);
        }
        catch
        {
            Console.WriteLine($"  Failed parsing {fileName}. Skipping.");
        }

        if (lockFile != null)
            yield return lockFile;
    }
}

var usedPackages = new HashSet<(string Name, NuGetVersion Version)>();

Console.WriteLine($"Collecting {LockFileFormat.AssetsFileName}, {LockFileFormat.LockFileName} files...");

usedPackages.AddRange(
    from f in EnumerateLockFiles(path)
    from l in f.Libraries
    select (l.Name, l.Version));

Console.WriteLine($"Collecting {PackagesLockFileFormat.LockFileName} files...");

usedPackages.AddRange(
    from f in EnumeratePackagesLockFile(path)
    from t in f.Targets
    from d in t.Dependencies
    select (d.Id, d.ResolvedVersion));

Console.WriteLine($"Totally {usedPackages.Count} versions of {usedPackages.DistinctBy(p => p.Name).Count()} package are in use.");
