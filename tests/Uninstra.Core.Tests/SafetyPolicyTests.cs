namespace Uninstra.Core.Tests;

using FluentAssertions;
using Uninstra.Core.Safety;
using Xunit;

public class SafetyPolicyTests
{
    [Theory]
    [InlineData(@"C:\Windows", true)]
    [InlineData(@"C:\Windows\System32", true)]
    [InlineData(@"C:\", true)]
    [InlineData(@"D:\", true)]
    public void IsProtectedPath_SystemPaths(string path, bool expected)
    {
        SafetyPolicy.IsProtectedPath(path).Should().Be(expected);
    }

    [Fact]
    public void ContainsPathTraversal_WithDotDot_ReturnsTrue()
    {
        SafetyPolicy.ContainsPathTraversal(@"C:\Program Files\..\Windows").Should().BeTrue();
    }

    [Fact]
    public void ContainsPathTraversal_NormalPath_ReturnsFalse()
    {
        SafetyPolicy.ContainsPathTraversal(@"C:\Program Files\MyApp").Should().BeFalse();
    }

    [Fact]
    public void ContainsPathTraversal_NullByte_ReturnsTrue()
    {
        SafetyPolicy.ContainsPathTraversal("C:\\test\0hack").Should().BeTrue();
    }

    [Fact]
    public void NormalizePath_ExpandsEnvironmentVars()
    {
        var result = SafetyPolicy.NormalizePath("%TEMP%");
        result.Should().NotBeNull();
        result.Should().NotContain("%");
    }

    [Fact]
    public void NormalizePath_TraversalReturnsNull()
    {
        SafetyPolicy.NormalizePath(@"C:\test\..\Windows\System32").Should().BeNull();
    }

    [Fact]
    public void EvaluatePath_CommonFiles_IsProtected()
    {
        var (isProtected, _) = SafetyPolicy.EvaluatePath(@"C:\Program Files\Common Files\Something");
        isProtected.Should().BeTrue();
    }

    [Fact]
    public void IsProtectedApplication_VisualCpp_ReturnsTrue()
    {
        SafetyPolicy.IsProtectedApplication("Microsoft Visual C++ 2019 Redistributable").Should().BeTrue();
    }

    [Fact]
    public void IsProtectedApplication_RandomApp_ReturnsFalse()
    {
        SafetyPolicy.IsProtectedApplication("My Cool App").Should().BeFalse();
    }
}
