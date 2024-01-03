using System.CommandLine;
using System.CommandLine.Parsing;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace NuGetClear;

internal static class Program
{
    private static IEnumerable<LockFile> EnumerateLockFiles(DirectoryInfo rootDirectory)
    {
        var format = new LockFileFormat();

        foreach (var file in rootDirectory.EnumerateFiles(LockFileFormat.AssetsFileName, SearchOption.AllDirectories)
            .Concat(rootDirectory.EnumerateFiles(LockFileFormat.LockFileName, SearchOption.AllDirectories)))
        {
            LockFile? lockFile = null;

            try
            {
                lockFile = format.Read(file.FullName);
            }
            catch
            {
                Console.WriteLine($"  Failed parsing {file.FullName}. Skipping.");
            }

            if (lockFile != null)
                yield return lockFile;
        }
    }

    private static IEnumerable<PackagesLockFile> EnumeratePackagesLockFile(DirectoryInfo rootDirectory)
    {
        foreach (var file in rootDirectory.EnumerateFiles(PackagesLockFileFormat.LockFileName, SearchOption.AllDirectories))
        {
            PackagesLockFile? lockFile = null;

            try
            {
                lockFile = PackagesLockFileFormat.Read(file.FullName);
            }
            catch
            {
                Console.WriteLine($"  Failed parsing {file.FullName}. Skipping.");
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

    public static int Main(string[] args)
    {
        var directoryOption = new Option<DirectoryInfo>(
            ["-r", "--root-directory"],
            "The root directory to scan for built projects.")
        {
            IsRequired = true,
        }
            .ExistingOnly();
        var globalPackagesOption = new Option<DirectoryInfo>(
            ["-g", "--global-packages"],
            "The nuget global package folders.")
            .ExistingOnly();

        var dryRunOption = new Option<bool>(
            ["--dry-run"],
            "Do not do the actual clean up.");

        var rootCommand = new RootCommand("Cleans nuget packages folder based on dependencies of built projects.")
        {
            directoryOption,
            globalPackagesOption,
            dryRunOption
        };

        rootCommand.SetHandler(
            MainCommand,
            directoryOption,
            globalPackagesOption,
            dryRunOption);

        return rootCommand.Invoke(args);
    }

    private static void MainCommand(DirectoryInfo rootDirectory, DirectoryInfo? globalPackages, bool dryRun)
    {
        var usedPackages = new HashSet<(string Name, string Version)>();

        Console.WriteLine($"Collecting {LockFileFormat.AssetsFileName}, {LockFileFormat.LockFileName} files...");

        usedPackages.AddRange(
            from f in EnumerateLockFiles(rootDirectory)
            from l in f.Libraries
            where l.MSBuildProject is null
            select (l.Name.ToLowerInvariant(), l.Version.ToNormalizedString()));

        Console.WriteLine($"Collecting {PackagesLockFileFormat.LockFileName} files...");

        usedPackages.AddRange(
            from f in EnumeratePackagesLockFile(rootDirectory)
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

        globalPackages ??= new DirectoryInfo(SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance));

        Console.WriteLine($"Using global nuget packages folder at {globalPackages.FullName}");

        var cachedPackages =
            (from p in globalPackages.EnumerateDirectories()
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
