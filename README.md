# NuGetClear

Clear local .nuget/packages folder and preserve all packages in use.

### Usage

```
Usage:
  NuGetClear [options]

Options:
  -r, --root-directory <root-directory> (REQUIRED)  The root directory to scan for built projects.
  -g, --global-packages <global-packages>           The nuget global package folders.
  --dry-run                                         Do not do the actual clean up.
  --cu <cu>                                         Catalog output for used packages.
  --cc <cc>                                         Catalog output for cached packages.
  --version                                         Show version information
  -?, -h, --help                                    Show help and usage information
```

### Explaination

The tool will scan `root-directory` for restore results of NuGet, 
typically `project.assets.json` files under `obj` output of projects.
Successfully restored packages are considered in use. 
`dotnet-tools.json` are also collected for local dotnet tools. 
Global dotnet tools are stored in a separated location with all dependencies and won't be affected.

The tool supports multiple `root-directory` to search for build outputs and one `global-packages` to clean up.
