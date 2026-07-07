namespace Uninstra.Core.Validation;

public static class GuidValidator
{
    public static bool IsValidProductCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out _);
    }

    public static string? ExtractGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            trimmed = trimmed[1..^1];
        return Guid.TryParse(trimmed, out var guid) ? $"{{{guid}}}" : null;
    }
}
