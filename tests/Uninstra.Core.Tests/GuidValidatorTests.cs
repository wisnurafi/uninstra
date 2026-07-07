namespace Uninstra.Core.Tests;

using FluentAssertions;
using Uninstra.Core.Validation;
using Xunit;

public class GuidValidatorTests
{
    [Theory]
    [InlineData("{12345678-1234-1234-1234-123456789012}", true)]
    [InlineData("12345678-1234-1234-1234-123456789012", true)]
    [InlineData("not-a-guid", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidProductCode_Tests(string? input, bool expected)
    {
        GuidValidator.IsValidProductCode(input).Should().Be(expected);
    }

    [Fact]
    public void ExtractGuid_ValidGuid_ReturnsFormatted()
    {
        var result = GuidValidator.ExtractGuid("{12345678-1234-1234-1234-123456789012}");
        result.Should().NotBeNull();
        result.Should().StartWith("{").And.EndWith("}");
    }

    [Fact]
    public void ExtractGuid_Invalid_ReturnsNull()
    {
        GuidValidator.ExtractGuid("nope").Should().BeNull();
    }
}
