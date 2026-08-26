namespace Uninstra.IntegrationTests;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Uninstra.Application.Interfaces;
using Uninstra.Infrastructure.Services;
using Xunit;

/// <summary>
/// Regression tests for settings persistence.
/// - NaN/Infinity doubles once made every Save() throw (System.Text.Json refuses
///   non-finite numbers), so settings.json was never written at all. The window
///   position fields are now nullable doubles; these tests lock that in.
/// - Save() must be atomic: an interrupted write can never truncate the file.
/// </summary>
public class JsonSettingsRoundTripTests : IDisposable
{
    private readonly string _path;
    private readonly JsonSettingsService _svc;

    public JsonSettingsRoundTripTests()
    {
        _path = Path.Combine(
            Path.GetTempPath(), $"uninstra-settings-{Guid.NewGuid():N}.json");
        _svc = new JsonSettingsService(
            NullLogger<JsonSettingsService>.Instance, _path);
    }

    [Fact]
    public void Save_DefaultSettings_DoesNotThrow_AndWritesFile()
    {
        var act = () => _svc.Save(new AppSettings());
        act.Should().NotThrow();

        File.Exists(_path).Should().BeTrue("settings file must be created");
    }

    [Fact]
    public void RoundTrip_PreservesAllValues()
    {
        var original = new AppSettings
        {
            StartPage = "JunkCleaner",
            Theme = "Dark",
            LogLevel = "Debug",
            QuarantineRetentionDays = 30,
            WindowWidth = 1400,
            WindowHeight = 900,
            WindowLeft = 120.5,
            WindowTop = 80.25,
            SelectedCategory = "Large Programs",
            SortColumn = "Size",
            SortAscending = false
        };

        _svc.Save(original);
        var loaded = _svc.Load();

        loaded.StartPage.Should().Be(original.StartPage);
        loaded.Theme.Should().Be(original.Theme);
        loaded.LogLevel.Should().Be(original.LogLevel);
        loaded.QuarantineRetentionDays.Should().Be(30);
        loaded.WindowWidth.Should().Be(1400);
        loaded.WindowHeight.Should().Be(900);
        loaded.WindowLeft.Should().Be(120.5);
        loaded.WindowTop.Should().Be(80.25);
        loaded.SelectedCategory.Should().Be(original.SelectedCategory);
        loaded.SortColumn.Should().Be(original.SortColumn);
        loaded.SortAscending.Should().BeFalse();
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = _svc.Load();
        s.StartPage.Should().Be("Programs");
        s.QuarantineRetentionDays.Should().Be(14);
        s.WindowLeft.Should().BeNull();
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        File.WriteAllText(_path, "{ not valid json !!!");
        var s = _svc.Load();
        s.StartPage.Should().Be("Programs");
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        _svc.Save(new AppSettings { Theme = "Dark" });
        File.Exists(_path + ".tmp").Should().BeFalse(
            "atomic move should consume the temp file");
    }

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".tmp"); } catch { }
    }
}
