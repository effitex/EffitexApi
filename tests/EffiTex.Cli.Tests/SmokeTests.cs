using System.CommandLine;
using FluentAssertions;
using Xunit;

namespace EffiTex.Cli.Tests;

public class SmokeTests
{
    [Fact]
    public async Task RootCommand_Help_ExitCodeZero()
    {
        var rootCommand = new RootCommand("EffiTex PDF structure API — local CLI");
        var exitCode = await rootCommand.Parse("--help").InvokeAsync();
        exitCode.Should().Be(0);
    }
}
