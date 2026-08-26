namespace Uninstra.Core.Parsing;

using Uninstra.Core.Models;
using Uninstra.Core.Validation;

public static class UninstallCommandParser
{
    public static ParsedCommand Parse(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new ParsedCommand { IsValid = false, ParseError = "Empty command", OriginalCommand = command ?? "" };

        var trimmed = command.Trim();

        // Detect msiexec
        if (trimmed.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("\"msiexec", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMsiExec(trimmed);
        }

        // Detect rundll32
        if (trimmed.StartsWith("rundll32", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("\"rundll32", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedCommand
            {
                ExecutablePath = ResolveSystemExe("rundll32.exe"),
                Arguments = ExtractArguments(trimmed),
                IsValid = true,
                IsRundll32 = true,
                OriginalCommand = command
            };
        }

        // Quoted executable
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote < 0)
                return new ParsedCommand { IsValid = false, ParseError = "Unterminated quote", OriginalCommand = command };

            var exe = trimmed[1..endQuote];
            var args = endQuote + 1 < trimmed.Length ? trimmed[(endQuote + 1)..].TrimStart() : "";
            return ValidateAndReturn(exe, args, command);
        }

        // Unquoted executable — find the path by testing progressively longer prefixes
        // because the path might contain spaces. Accept anything that exists OR carries
        // an executable extension: broken entries whose uninstaller no longer exists
        // must still parse as a full path ("C:\Program Files\...\uninst.exe"), not as
        // a "C:\Program" fragment (which once misflagged live apps as leftovers).
        var parts = trimmed.Split(' ');
        for (int i = 1; i <= parts.Length; i++)
        {
            var candidate = string.Join(' ', parts[..i]);
            if (File.Exists(candidate) || HasExecutableExtension(candidate))
            {
                var args = i < parts.Length ? string.Join(' ', parts[i..]) : "";
                return ValidateAndReturn(candidate, args, command);
            }
        }

        // Fallback: no token boundary matched (path likely contains spaces and the
        // file does not exist). Treat everything up to the LAST space as the exe —
        // uninstall strings put arguments after the executable, so a trailing
        // fragment like "/S /x" must never bleed into the path.
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            return ValidateAndReturn(trimmed[..lastSpace], trimmed[(lastSpace + 1)..], command);
        }

        return ValidateAndReturn(trimmed, "", command);
    }

    private static ParsedCommand ParseMsiExec(string command)
    {
        var msiExe = ResolveSystemExe("msiexec.exe");
        var args = ExtractArguments(command);
        string? productCode = null;

        // Extract GUID from /x{GUID} or /i{GUID} etc.
        var idx = args.IndexOf('{');
        if (idx >= 0)
        {
            var end = args.IndexOf('}', idx);
            if (end > idx)
            {
                var candidate = args[idx..(end + 1)];
                if (GuidValidator.IsValidProductCode(candidate))
                    productCode = GuidValidator.ExtractGuid(candidate);
            }
        }

        return new ParsedCommand
        {
            ExecutablePath = msiExe,
            Arguments = args,
            IsValid = true,
            IsMsiExec = true,
            MsiProductCode = productCode,
            OriginalCommand = command
        };
    }

    private static ParsedCommand ValidateAndReturn(string exe, string args, string original)
    {
        var expanded = Environment.ExpandEnvironmentVariables(exe);
        return new ParsedCommand
        {
            ExecutablePath = expanded,
            Arguments = args.Trim(),
            IsValid = !string.IsNullOrWhiteSpace(expanded),
            OriginalCommand = original
        };
    }

    private static string ExtractArguments(string command)
    {
        // Skip the exe portion
        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            return end >= 0 && end + 1 < trimmed.Length ? trimmed[(end + 1)..].TrimStart() : "";
        }
        var space = trimmed.IndexOf(' ');
        return space >= 0 ? trimmed[(space + 1)..].TrimStart() : "";
    }

    private static bool HasExecutableExtension(string candidate)
    {
        // Executable extensions Windows CreateProcess honors (PATHEXT + .msi/.scr).
        // Registry uninstall strings legitimately use .exe, .com, .bat, .cmd,
        // .msi and even .scr as the launched binary.
        ReadOnlySpan<char> ext = Path.GetExtension(candidate);
        return ext is ".exe" or ".com" or ".bat" or ".cmd" or ".msi" or ".scr";
    }

    private static string ResolveSystemExe(string name)
    {
        var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var full = Path.Combine(sys32, name);
        return File.Exists(full) ? full : name;
    }
}
