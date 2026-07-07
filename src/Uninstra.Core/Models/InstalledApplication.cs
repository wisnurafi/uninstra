namespace Uninstra.Core.Models;

using Uninstra.Core.Enums;

public sealed record InstalledApplication
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string NormalizedName { get; init; } = string.Empty;
    public string DisplayVersion { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public DateTime? InstallDate { get; init; }
    public string InstallLocation { get; init; } = string.Empty;
    public string UninstallCommand { get; init; } = string.Empty;
    public string QuietUninstallCommand { get; init; } = string.Empty;
    public string ModifyCommand { get; init; } = string.Empty;
    public string DisplayIconPath { get; init; } = string.Empty;
    public long EstimatedSizeBytes { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public InstallerType InstallerType { get; init; } = InstallerType.Unknown;
    public AppArchitecture Architecture { get; init; } = AppArchitecture.Unknown;
    public ApplicationCategory ApplicationCategory { get; init; } = ApplicationCategory.UserApplication;
    public RegistryHiveType RegistryHive { get; init; }
    public string RegistryKeyPath { get; init; } = string.Empty;
    public RegistryViewType RegistryView { get; init; }
    public bool IsSystemComponent { get; init; }
    public bool IsRuntime { get; init; }
    public bool IsUpdate { get; init; }
    public bool IsDriverRelated { get; init; }
    public bool IsStoreApplication { get; init; }
    public bool IsProtected { get; init; }
    public string ProtectionReason { get; init; } = string.Empty;
    public string DigitalSignaturePublisher { get; init; } = string.Empty;
    public byte[]? Icon { get; init; }
    public List<string> DetectionEvidence { get; init; } = [];
    public string UrlInfoAbout { get; init; } = string.Empty;
    public string HelpLink { get; init; } = string.Empty;
    public string Comments { get; init; } = string.Empty;
    public DateTime? LastUsed { get; init; }
}
