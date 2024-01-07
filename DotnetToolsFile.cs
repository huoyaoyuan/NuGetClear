namespace NuGetClear
{
    internal class DotnetToolsFile
    {
        public const string FileName = "dotnet-tools.json";

        public required int Version { get; set; }

        public bool IsRoot { get; set; }

        public required IDictionary<string, DotnetToolInfo> Tools { get; set; }
    }

    internal class DotnetToolInfo
    {
        public required string Version { get; set; }

        public required IEnumerable<string> Commands { get; set; }
    }
}
