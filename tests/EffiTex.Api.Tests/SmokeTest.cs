using FluentAssertions;
using Xunit;

namespace EffiTex.Api.Tests;

public class SmokeTest
{
    [Fact]
    public void Smoke_ProjectCompilesAndTestRunnerWires_Pass()
    {
        true.Should().BeTrue();
    }
}
