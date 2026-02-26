using System.CommandLine;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EffiTex.Cli.Tests;

[Collection("Serial")]
public class ProgramTests
{
    private readonly IServiceProvider _provider;

    public ProgramTests()
    {
        var services = new ServiceCollection();
        CliSetup.ConfigureServices(services);
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void DI_CanResolveInterpreter()
    {
        _provider.GetService<Interpreter>().Should().NotBeNull();
    }

    [Fact]
    public void DI_CanResolveInspectHandler()
    {
        _provider.GetService<InspectHandler>().Should().NotBeNull();
    }

    [Fact]
    public void DI_CanResolveInstructionValidator()
    {
        _provider.GetService<InstructionValidator>().Should().NotBeNull();
    }

    [Fact]
    public async Task RootCommand_Help_ExitCodeZeroAndPrintsUsage()
    {
        var root = new RootCommand("EffiTex PDF structure API — local CLI");
        root.Subcommands.Add(InspectCommand.Build(_provider));
        root.Subcommands.Add(ExecuteCommand.Build(_provider));
        using var capture = new ConsoleCapture();

        var exitCode = await root.Parse(new[] { "--help" }).InvokeAsync();
        exitCode.Should().Be(0);
        capture.OutText.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InspectCommand_Help_ExitCodeZero()
    {
        var root = new RootCommand("EffiTex PDF structure API — local CLI");
        root.Subcommands.Add(InspectCommand.Build(_provider));
        var exitCode = await root.Parse(new[] { "inspect", "--help" }).InvokeAsync();
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteCommand_Help_ExitCodeZero()
    {
        var root = new RootCommand("EffiTex PDF structure API — local CLI");
        root.Subcommands.Add(ExecuteCommand.Build(_provider));
        var exitCode = await root.Parse(new[] { "execute", "--help" }).InvokeAsync();
        exitCode.Should().Be(0);
    }
}
