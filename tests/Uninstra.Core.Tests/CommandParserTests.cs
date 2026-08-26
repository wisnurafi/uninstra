namespace Uninstra.Core.Tests;

using FluentAssertions;
using Uninstra.Core.Parsing;
using Xunit;

public class CommandParserTests
{
    [Fact]
    public void Parse_QuotedExecutable()
    {
        var result = UninstallCommandParser.Parse("\"C:\\Program Files\\App\\uninstall.exe\" /S");
        result.IsValid.Should().BeTrue();
        result.ExecutablePath.Should().Be(@"C:\Program Files\App\uninstall.exe");
        result.Arguments.Should().Be("/S");
    }

    [Fact]
    public void Parse_MsiExec()
    {
        var result = UninstallCommandParser.Parse("MsiExec.exe /X{12345678-1234-1234-1234-123456789012}");
        result.IsValid.Should().BeTrue();
        result.IsMsiExec.Should().BeTrue();
        result.MsiProductCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_MsiExec_ExtractsGuid()
    {
        var result = UninstallCommandParser.Parse("msiexec /x{ABCDEFAB-1234-5678-9012-ABCDEFABCDEF}");
        result.IsMsiExec.Should().BeTrue();
        result.MsiProductCode.Should().Contain("abcdefab");
    }

    [Fact]
    public void Parse_Rundll32()
    {
        var result = UninstallCommandParser.Parse("rundll32.exe something.dll,Uninstall");
        result.IsValid.Should().BeTrue();
        result.IsRundll32.Should().BeTrue();
    }

    [Fact]
    public void Parse_EmptyCommand_Invalid()
    {
        var result = UninstallCommandParser.Parse("");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_NullCommand_Invalid()
    {
        var result = UninstallCommandParser.Parse(null);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_UnquotedExe()
    {
        var result = UninstallCommandParser.Parse("C:\\App\\uninstall.exe /silent");
        result.IsValid.Should().BeTrue();
        result.Arguments.Should().Contain("silent");
    }

    [Fact]
    public void Parse_UnterminatedQuote_Invalid()
    {
        var result = UninstallCommandParser.Parse("\"C:\\Broken\\path");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_UnquotedMissingFile_SpacedPath_ParsesFullPath()
    {
        // Broken entry: uninstaller file no longer exists, path contains spaces.
        // Must resolve the FULL path, not a "C:\Program" fragment.
        var result = UninstallCommandParser.Parse(
            @"C:\Program Files\Vendor\App\uninstall.exe /S");
        result.IsValid.Should().BeTrue();
        result.ExecutablePath.Should().Be(@"C:\Program Files\Vendor\App\uninstall.exe");
        result.Arguments.Should().Be("/S");
    }

    [Fact]
    public void Parse_BatchUninstaller_MissingFile_StillParses()
    {
        var result = UninstallCommandParser.Parse(
            @"C:\Program Files\My App\unins000.bat /quiet");
        result.IsValid.Should().BeTrue();
        result.ExecutablePath.Should().Be(@"C:\Program Files\My App\unins000.bat");
        result.Arguments.Should().Be("/quiet");
    }

    [Fact]
    public void Parse_NoExecutableExtension_FallbackUsesLastSpaceBoundary()
    {
        // Extension-less command line: last-space split keeps the whole path
        // intact and pushes only the trailing argument into Arguments.
        // (Multi-argument extension-less lines are inherently ambiguous; the
        // parser deliberately favors keeping the path whole over fragmenting it.)
        var result = UninstallCommandParser.Parse(
            @"C:\Program Files\Weird App\uninstaller /S");
        result.ExecutablePath.Should().Be(@"C:\Program Files\Weird App\uninstaller");
        result.Arguments.Should().Be("/S");
    }
}
