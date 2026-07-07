namespace Uninstra.Core.Tests;

using FluentAssertions;
using Uninstra.Core.Validation;
using Xunit;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("My App v2.0.1", "my app")]
    [InlineData("  Firefox  ", "firefox")]
    [InlineData("App (x64)", "app")]
    public void Normalize_RemovesVersionAndArch(string input, string expected)
    {
        NameNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        NameNormalizer.Normalize("").Should().BeEmpty();
    }

    [Theory]
    [InlineData("app", true)]
    [InlineData("setup", true)]
    [InlineData("Firefox", false)]
    [InlineData("", true)]
    [InlineData("ab", true)]
    public void IsGenericName_Tests(string name, bool expected)
    {
        NameNormalizer.IsGenericName(name).Should().Be(expected);
    }
}
