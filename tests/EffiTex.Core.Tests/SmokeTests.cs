using FluentAssertions;
using Xunit;

namespace EffiTex.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void TestRunner_IsWired_ReturnsTrue()
    {
        true.Should().BeTrue();
    }
}
