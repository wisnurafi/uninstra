namespace Uninstra.Core.Safety;

public static class ProtectedLists
{
    public static readonly HashSet<string> ProtectedPublishers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Corporation",
        "Microsoft Windows",
        "Microsoft",
        "NVIDIA Corporation",
        "AMD",
        "Intel Corporation",
        "Intel(R) Corporation",
        "Realtek Semiconductor Corp.",
        "Qualcomm",
        "Broadcom",
        "Synaptics Incorporated"
    };

    public static readonly HashSet<string> ProtectedApplicationPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Visual C++",
        "Microsoft .NET",
        ".NET Runtime",
        ".NET Desktop Runtime",
        ".NET SDK",
        ".NET Framework",
        "Windows App Runtime",
        "Microsoft Edge WebView2",
        "Windows Security",
        "Microsoft Store",
        "App Installer",
        "Windows Defender",
        "Microsoft OneDrive",
        "Windows Update",
        "Microsoft Edge",
        "Visual Studio Build Tools",
        "Windows Software Development Kit",
        "Windows Driver Kit"
    };

    public static bool IsProtectedByName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return false;
        return ProtectedApplicationPatterns.Any(p =>
            displayName.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsProtectedByPublisher(string publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher)) return false;
        // Only protect specific Microsoft system components, not all MS software
        return false; // Individual checks in the scanner will be more nuanced
    }

    public static (bool IsProtected, string Reason) Evaluate(string displayName, string publisher, bool isSystemComponent, bool isRuntime)
    {
        if (isSystemComponent)
            return (true, "System component");
        if (isRuntime)
            return (true, "Runtime or framework dependency");
        if (IsProtectedByName(displayName))
            return (true, $"Protected application: {displayName}");
        return (false, string.Empty);
    }
}
