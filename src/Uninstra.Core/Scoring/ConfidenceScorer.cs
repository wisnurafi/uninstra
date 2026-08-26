namespace Uninstra.Core.Scoring;

using Uninstra.Core.Enums;

public sealed record ScoringContext
{
    public bool IsExactInstallLocation { get; init; }
    public bool IsMonitoredByInstallSession { get; init; }
    public bool ExecutablePointsToInstallLocation { get; init; }
    public bool ProductCodeMatches { get; init; }
    public bool DigitalSignaturePublisherMatches { get; init; }
    public bool UninstallRegistryOwnershipMatches { get; init; }
    public bool ServiceImagePathInInstallLocation { get; init; }
    public bool ScheduledTaskInInstallLocation { get; init; }
    public bool ShortcutTargetInInstallLocation { get; init; }
    public bool StartupEntryInInstallLocation { get; init; }
    public bool FolderNameMatchesExactly { get; init; }
    public bool PublisherAndNameMatch { get; init; }
    public bool RegistryValuePointsToExecutable { get; init; }
    public bool NormalizedNameMatches { get; init; }
    public DateTime LastModifiedUtc { get; init; }

    // Penalties
    public bool NameVeryShort { get; init; }
    public bool NameTooGeneric { get; init; }
    public bool FolderPossiblyShared { get; init; }
    public bool UsedByOtherProgram { get; init; }
    public bool IsCommonFiles { get; init; }
    public bool IsDependencyOrRuntime { get; init; }
    public bool IsProtectedDirectory { get; init; }
    public bool IsSystemComponent { get; init; }
    public bool OwnershipUnknown { get; init; }
}

public static class ConfidenceScorer
{
    public static (int Score, ConfidenceLevel Level, List<string> Evidence) Calculate(ScoringContext ctx)
    {
        int score = 0;
        var evidence = new List<string>();

        // Positive signals
        if (ctx.IsExactInstallLocation)         { score += 100; evidence.Add("Located in original install location"); }
        if (ctx.IsMonitoredByInstallSession)     { score += 60; evidence.Add("Recorded by Install Monitor session"); }
        if (ctx.ExecutablePointsToInstallLocation) { score += 50; evidence.Add("Executable path points to install location"); }
        if (ctx.ProductCodeMatches)              { score += 45; evidence.Add("ProductCode matches"); }
        if (ctx.DigitalSignaturePublisherMatches){ score += 40; evidence.Add("Digital signature publisher matches"); }
        if (ctx.UninstallRegistryOwnershipMatches) { score += 35; evidence.Add("Uninstall registry ownership matches"); }
        if (ctx.ServiceImagePathInInstallLocation) { score += 30; evidence.Add("Service ImagePath in install location"); }
        if (ctx.ScheduledTaskInInstallLocation)  { score += 30; evidence.Add("Scheduled task runs from install location"); }
        if (ctx.ShortcutTargetInInstallLocation) { score += 25; evidence.Add("Shortcut target points to install location"); }
        if (ctx.StartupEntryInInstallLocation)   { score += 25; evidence.Add("Startup entry points to install location"); }
        if (ctx.FolderNameMatchesExactly)        { score += 20; evidence.Add("Folder name matches exactly"); }
        if (ctx.PublisherAndNameMatch)            { score += 20; evidence.Add("Publisher and program name match"); }
        if (ctx.RegistryValuePointsToExecutable) { score += 15; evidence.Add("Registry value points to executable"); }
        if (ctx.NormalizedNameMatches)            { score += 10; evidence.Add("Normalized name matches"); }

        // Age signal: a directory untouched for 60+ days after an app is gone is stronger evidence
        if (ctx.LastModifiedUtc != default && DateTime.UtcNow - ctx.LastModifiedUtc > TimeSpan.FromDays(60))
        {
            score += 10;
            evidence.Add("Unmodified for more than 60 days");
        }

        // Penalties
        if (ctx.NameVeryShort)        { score -= 30; evidence.Add("Penalty: name very short"); }
        if (ctx.NameTooGeneric)       { score -= 40; evidence.Add("Penalty: name too generic"); }
        if (ctx.FolderPossiblyShared) { score -= 50; evidence.Add("Penalty: folder possibly shared"); }
        if (ctx.UsedByOtherProgram)   { score -= 60; evidence.Add("Penalty: used by another program"); }
        if (ctx.IsCommonFiles)        { score -= 70; evidence.Add("Penalty: Common Files directory"); }
        if (ctx.IsDependencyOrRuntime){ score -= 80; evidence.Add("Penalty: dependency or runtime"); }
        if (ctx.IsProtectedDirectory) { score -= 100; evidence.Add("Penalty: protected directory"); }
        if (ctx.IsSystemComponent)    { score -= 100; evidence.Add("Penalty: system component"); }
        if (ctx.OwnershipUnknown)     { score -= 100; evidence.Add("Penalty: ownership cannot be determined"); }

        score = Math.Clamp(score, 0, 100);
        var level = score switch
        {
            >= 85 => ConfidenceLevel.High,
            >= 60 => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low
        };

        return (score, level, evidence);
    }

    public static bool ShouldAutoSelect(int score, ConfidenceLevel level, bool isProtected)
    {
        if (isProtected) return false;
        return level == ConfidenceLevel.High;
    }
}
