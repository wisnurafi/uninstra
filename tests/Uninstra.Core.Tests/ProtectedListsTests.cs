namespace Uninstra.Core.Tests;

using FluentAssertions;
using Uninstra.Core.Safety;
using Xunit;

public class ProtectedListsTests
{
    [Fact]
    public void IsProtectedByName_VisualCpp_True()
    {
        ProtectedLists.IsProtectedByName("Microsoft Visual C++ 2019 x64").Should().BeTrue();
    }

    [Fact]
    public void IsProtectedByName_DotNetRuntime_True()
    {
        ProtectedLists.IsProtectedByName("Microsoft .NET Runtime - 8.0.0").Should().BeTrue();
    }

    [Fact]
    public void IsProtectedByName_RandomApp_False()
    {
        ProtectedLists.IsProtectedByName("7-Zip").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_SystemComponent_Protected()
    {
        var (isProtected, reason) = ProtectedLists.Evaluate("Something", "Someone", true, false);
        isProtected.Should().BeTrue();
        reason.Should().Contain("System component");
    }

    [Fact]
    public void Evaluate_Runtime_Protected()
    {
        var (isProtected, reason) = ProtectedLists.Evaluate("Something", "Someone", false, true);
        isProtected.Should().BeTrue();
        reason.Should().Contain("Runtime");
    }

    [Fact]
    public void Evaluate_NormalApp_NotProtected()
    {
        var (isProtected, _) = ProtectedLists.Evaluate("Firefox", "Mozilla", false, false);
        isProtected.Should().BeFalse();
    }
}
