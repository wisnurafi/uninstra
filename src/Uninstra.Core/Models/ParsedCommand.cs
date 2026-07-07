namespace Uninstra.Core.Models;

public sealed record ParsedCommand
{
    public string ExecutablePath { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public bool IsMsiExec { get; init; }
    public string? MsiProductCode { get; init; }
    public bool IsRundll32 { get; init; }
    public string ParseError { get; init; } = string.Empty;
    public string OriginalCommand { get; init; } = string.Empty;
}
