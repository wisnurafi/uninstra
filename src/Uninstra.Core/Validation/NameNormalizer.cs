namespace Uninstra.Core.Validation;

using System.Text.RegularExpressions;

public static partial class NameNormalizer
{
    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var result = name.Trim().ToLowerInvariant();
        result = ArchPattern().Replace(result, "");
        result = VersionPattern().Replace(result, "");
        result = WhitespacePattern().Replace(result, " ");
        return result.Trim();
    }

    public static bool IsGenericName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        if (name.Length < 3) return true;

        var generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "app", "application", "setup", "install", "installer",
            "update", "updater", "helper", "service", "server",
            "client", "agent", "manager", "tool", "tools", "data",
            "temp", "cache", "config", "settings", "log", "logs"
        };

        return generic.Contains(name.Trim());
    }

    [GeneratedRegex(@"\s*v?\d+(\.\d+)*\s*", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\s*\(?(x86|x64|64-bit|32-bit|amd64|arm64)\)?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ArchPattern();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex WhitespacePattern();
}
