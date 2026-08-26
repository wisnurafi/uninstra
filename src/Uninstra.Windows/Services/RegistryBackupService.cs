namespace Uninstra.Windows.Services;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Core.Safety;

/// <summary>
/// Exports registry keys/values to .reg files under
/// %LOCALAPPDATA%/Uninstra/Backups/&lt;operationId&gt;/ BEFORE deletion,
/// so every registry cleanup is reversible with a double-click.
/// </summary>
public sealed class RegistryBackupService
{
    private readonly ILogger<RegistryBackupService> _logger;
    private readonly string _backupRoot;

    public RegistryBackupService(ILogger<RegistryBackupService> logger)
    {
        _logger = logger;
        _backupRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uninstra", "Backups");
        Directory.CreateDirectory(_backupRoot);
    }

    /// <summary>
    /// Backs up the given registry leftovers for one operation.
    /// Returns the backup file path, or null when nothing was backed up.
    /// </summary>
    public string? BackupRegistryItems(
        IReadOnlyList<LeftoverCandidate> items, string operationId)
    {
        var regItems = items.Where(i =>
            i.Type is LeftoverType.RegistryKey or LeftoverType.RegistryValue or LeftoverType.StartupEntry &&
            !string.IsNullOrWhiteSpace(i.RegistryPath)).ToList();

        if (regItems.Count == 0) return null;

        try
        {
            var opDir = Path.Combine(_backupRoot, operationId);
            Directory.CreateDirectory(opDir);
            var backupFile = Path.Combine(opDir, $"registry-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.reg");

            // Build the .reg content manually — reg.exe export only handles full keys,
            // while we also need individual value-level backups.
            var lines = new List<string> { "Windows Registry Editor Version 5.00", "" };
            int backedUp = 0;

            foreach (var item in regItems)
            {
                try
                {
                    var hive = item.RegistryHive == RegistryHiveType.LocalMachine
                        ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
                    var fullPath = $@"[{hive}\{item.RegistryPath.Replace('/', '\\')}]";

                    if (item.Type == LeftoverType.RegistryKey || item.Type == LeftoverType.StartupEntry && string.IsNullOrEmpty(item.RegistryValueName))
                    {
                        // Whole key: verify it still exists, then emit an empty section header
                        // (deletes nothing on import; recreates the empty key shell on restore)
                        if (!KeyExists(item)) continue;
                        lines.Add(fullPath);
                        lines.Add("");
                    }
                    else
                    {
                        // Value-level backup: capture actual data so import restores it fully
                        var value = ReadValue(item);
                        if (value is null) continue;
                        lines.Add(fullPath);
                        lines.Add($"\"{EscapeRegValueName(item.RegistryValueName)}\"={FormatRegValue(value.Value)}");
                        lines.Add("");
                    }
                    backedUp++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not back up registry item {Item}", item.DisplayName);
                }
            }

            if (backedUp == 0) return null;

            File.WriteAllLines(backupFile, lines);
            _logger.LogInformation("Backed up {Count} registry items to {File}", backedUp, backupFile);
            return backupFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry backup failed for operation {Op}", operationId);
            return null;
        }
    }

    private static bool KeyExists(LeftoverCandidate item)
    {
        try
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(
                item.RegistryHive == RegistryHiveType.LocalMachine
                    ? Microsoft.Win32.RegistryHive.LocalMachine
                    : Microsoft.Win32.RegistryHive.CurrentUser,
                Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.OpenSubKey(item.RegistryPath);
            return key is not null;
        }
        catch { return false; }
    }

    private static (object? Data, Microsoft.Win32.RegistryValueKind Kind)? ReadValue(LeftoverCandidate item)
    {
        try
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(
                item.RegistryHive == RegistryHiveType.LocalMachine
                    ? Microsoft.Win32.RegistryHive.LocalMachine
                    : Microsoft.Win32.RegistryHive.CurrentUser,
                Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.OpenSubKey(item.RegistryPath);
            if (key is null) return null;
            var data = key.GetValue(item.RegistryValueName);
            if (data is null) return null;
            return (data, key.GetValueKind(item.RegistryValueName));
        }
        catch { return null; }
    }

    private static string EscapeRegValueName(string name) =>
        name.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string FormatRegValue((object? Data, Microsoft.Win32.RegistryValueKind Kind) value)
    {
        return value.Kind switch
        {
            Microsoft.Win32.RegistryValueKind.DWord =>
                $"dword:{Convert.ToUInt32(value.Data ?? 0):x8}",
            Microsoft.Win32.RegistryValueKind.QWord =>
                $"hex(b):{ToHex(BitConverter.GetBytes(Convert.ToUInt64(value.Data ?? 0)))}",
            Microsoft.Win32.RegistryValueKind.Binary when value.Data is byte[] bytes =>
                $"hex:{ToHex(bytes)}",
            Microsoft.Win32.RegistryValueKind.ExpandString or Microsoft.Win32.RegistryValueKind.String =>
                $"\"{EscapeRegStringValue(Convert.ToString(value.Data) ?? string.Empty)}\"",
            Microsoft.Win32.RegistryValueKind.MultiString when value.Data is string[] multi =>
                $"hex(7):{ToHex(MultiStringToBytes(multi))}",
            _ => $"\"{EscapeRegStringValue(value.Data?.ToString() ?? string.Empty)}\""
        };
    }

    private static string EscapeRegStringValue(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ToHex(byte[] bytes) =>
        bytes.Length == 0 ? string.Empty : Convert.ToHexString(bytes).ToLowerInvariant();

    private static byte[] MultiStringToBytes(string[] strings)
    {
        var joined = string.Join("\0", strings) + "\0\0";
        return System.Text.Encoding.Unicode.GetBytes(joined);
    }
}
