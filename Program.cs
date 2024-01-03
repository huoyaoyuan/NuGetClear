using NuGet.Packaging;
using NuGet.ProjectModel;

namespace NuGetClear;

internal static class Program
{
    private static IEnumerable<LockFile> EnumerateLockFiles(string rootDirectory)
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

    private static IEnumerable<PackagesLockFile> EnumeratePackagesLockFile(string rootDirectory)
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

    private static long DirectorySize(DirectoryInfo directory)
    {
        return directory.EnumerateFiles().Select(f => f.Length).Sum()
            + directory.EnumerateDirectories().Select(DirectorySize).Sum();
    }

    private static string FormatLength(long length) => length switch
    {
        < (1L << 10) => $"{length} B",
        < (1L << 20) => $"{(length / (double)(1L << 10)):F2} KiB",
        < (1L << 30) => $"{(length / (double)(1L << 20)):F2} MiB",
        < (1L << 40) => $"{(length / (double)(1L << 30)):F2} GiB",
        _ => $"{(length / (double)(1L << 40)):F2} TiB",
    };

    public static void Main()
    {
        Console.Write("Root directory:");
        string path = Console.ReadLine()!;

        var usedPackages = new HashSet<(string Name, string Version)>();

        Console.WriteLine($"Collecting {LockFileFormat.AssetsFileName}, {LockFileFormat.LockFileName} files...");

        usedPackages.AddRange(
            from f in EnumerateLockFiles(path)
            from l in f.Libraries
            where l.MSBuildProject is null
            select (l.Name.ToLowerInvariant(), l.Version.ToNormalizedString()));

        Console.WriteLine($"Collecting {PackagesLockFileFormat.LockFileName} files...");

        usedPackages.AddRange(
            from f in EnumeratePackagesLockFile(path)
            from t in f.Targets
            from d in t.Dependencies
            where d.Type != PackageDependencyType.Project
            select (d.Id.ToLowerInvariant(), d.ResolvedVersion.ToNormalizedString()));

        Console.WriteLine($"Totally {usedPackages.Count} versions of {usedPackages.DistinctBy(p => p.Name).Count()} package are in use.");

        //File.WriteAllLines("used.txt",
        //    from p in usedPackages
        //    orderby p.Name, p.Version
        //    select p.Name + " " + p.Version);

        Console.WriteLine("Collecting .nuget cache...");

        var nugetRoot = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"));
        var cachedPackages =
            (from p in nugetRoot.EnumerateDirectories()
             from v in p.EnumerateDirectories()
             select (Name: p.Name, VersionPath: v))
            .ToList();

        //File.WriteAllLines("cached.txt",
        //    from p in cachedPackages
        //    orderby p.Name, p.VersionPath.Name
        //    select p.Name + " " + p.VersionPath.Name);

        Console.WriteLine($"Totally {cachedPackages.Count} versions of {cachedPackages.DistinctBy(p => p.Name).Count()} package are in cache.");

        var trimmablePackages = cachedPackages
            .Where(p => !usedPackages.Contains((p.Name.ToLowerInvariant(), p.VersionPath.Name)))
            .ToList();

        Console.WriteLine($"Totally {trimmablePackages.Count} versions of {trimmablePackages.DistinctBy(p => p.Name).Count()} package are trimmable.");

        long trimmableSize = trimmablePackages.Select(p => DirectorySize(p.VersionPath)).Sum();
        long cachedSize = cachedPackages.Select(p => DirectorySize(p.VersionPath)).Sum();

        Console.WriteLine($"{FormatLength(trimmableSize)} of {FormatLength(cachedSize)} are trimmable. Ratio: {(double)trimmableSize / cachedSize:P}.");
    }
}
