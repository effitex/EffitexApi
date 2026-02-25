using System.CommandLine;
using System.Text.Json;
using EffiTex.Engine;
using EffiTex.Engine.Models.Inspect;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EffiTex.Cli.Tests;

public class InspectCommandTests
{
    private static readonly string FIXTURES_PATH = System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures");

    private readonly IServiceProvider _provider;

    public InspectCommandTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InspectHandler>();
        _provider = services.BuildServiceProvider();
    }

    private RootCommand BuildRoot()
    {
        var root = new RootCommand();
        root.AddCommand(InspectCommand.Build(_provider));
        return root;
    }

    [Fact]
    public async Task Inspect_ValidPdf_ExitCodeZero()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".json");
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(new[] { "inspect", pdfPath, outputPath });
            exitCode.Should().Be(0);
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Inspect_ValidPdf_OutputFileExists()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".json");
        try
        {
            await BuildRoot().InvokeAsync(new[] { "inspect", pdfPath, outputPath });
            System.IO.File.Exists(outputPath).Should().BeTrue();
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Inspect_ValidPdf_OutputContainsValidJson()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".json");
        try
        {
            await BuildRoot().InvokeAsync(new[] { "inspect", pdfPath, outputPath });
            var json = await System.IO.File.ReadAllTextAsync(outputPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = JsonSerializer.Deserialize<InspectResponse>(json, options);
            response.Should().NotBeNull();
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Inspect_ValidPdf_PageCountMatchesKnownValue()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".json");
        try
        {
            await BuildRoot().InvokeAsync(new[] { "inspect", pdfPath, outputPath });
            var json = await System.IO.File.ReadAllTextAsync(outputPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = JsonSerializer.Deserialize<InspectResponse>(json, options);
            response.Document.PageCount.Should().Be(1);
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Inspect_NonExistentPdf_ExitCodeOne()
    {
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".json");
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(new[] { "inspect", "./nonexistent.pdf", outputPath });
            exitCode.Should().Be(1);
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Inspect_NonExistentPdf_WritesErrorToStderr()
    {
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".json");
        var console = new TestOutputConsole();
        try
        {
            await BuildRoot().InvokeAsync(new[] { "inspect", "./nonexistent.pdf", outputPath }, console);
            console.ErrorText.Should().Contain("Error:");
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Inspect_NonExistentOutputDir_ExitCodeOne()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "nonexistent_dir_inspect987", "output.json");

        var exitCode = await BuildRoot().InvokeAsync(new[] { "inspect", pdfPath, outputPath });
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Inspect_NonExistentOutputDir_WritesErrorToStderr()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "nonexistent_dir_inspect987", "output.json");
        var console = new TestOutputConsole();

        await BuildRoot().InvokeAsync(new[] { "inspect", pdfPath, outputPath }, console);
        console.ErrorText.Should().Contain("Error:");
    }
}
