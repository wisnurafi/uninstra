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
}
