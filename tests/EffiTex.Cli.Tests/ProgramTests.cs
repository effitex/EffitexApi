using System.CommandLine;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EffiTex.Cli.Tests;

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
        root.AddCommand(InspectCommand.Build(_provider));
        root.AddCommand(ExecuteCommand.Build(_provider));
        var console = new TestOutputConsole();

        var exitCode = await root.InvokeAsync(new[] { "--help" }, console);
        exitCode.Should().Be(0);
        console.OutText.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InspectCommand_Help_ExitCodeZero()
    {
        var root = new RootCommand("EffiTex PDF structure API — local CLI");
        root.AddCommand(InspectCommand.Build(_provider));
        var console = new TestOutputConsole();

        var exitCode = await root.InvokeAsync(new[] { "inspect", "--help" }, console);
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteCommand_Help_ExitCodeZero()
    {
        var root = new RootCommand("EffiTex PDF structure API — local CLI");
        root.AddCommand(ExecuteCommand.Build(_provider));
        var console = new TestOutputConsole();

        var exitCode = await root.InvokeAsync(new[] { "execute", "--help" }, console);
        exitCode.Should().Be(0);
    }
}
