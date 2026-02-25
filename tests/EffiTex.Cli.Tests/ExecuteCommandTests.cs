using System.CommandLine;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using FluentAssertions;
using iText.Kernel.Pdf;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EffiTex.Cli.Tests;

public class ExecuteCommandTests
{
    private static readonly string FIXTURES_PATH = System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures");

    private readonly IServiceProvider _provider;

    public ExecuteCommandTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<BboxResolver>();
        services.AddSingleton<MetadataHandler>();
        services.AddSingleton<StructureHandler>();
        services.AddSingleton<ContentTaggingHandler>();
        services.AddSingleton<ArtifactHandler>();
        services.AddSingleton<AnnotationHandler>();
        services.AddSingleton<FontHandler>();
        services.AddSingleton<OcrHandler>();
        services.AddSingleton<BookmarkHandler>();
        services.AddSingleton<Interpreter>();
        services.AddSingleton<InstructionDeserializer>();
        services.AddSingleton<InstructionValidator>();
        _provider = services.BuildServiceProvider();
    }

    private RootCommand BuildRoot()
    {
        var root = new RootCommand();
        root.AddCommand(ExecuteCommand.Build(_provider));
        return root;
    }

    [Fact]
    public async Task Execute_ValidMetadataYaml_ExitCodeZero()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var yamlPath = System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.yaml");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, yamlPath, outputPath });
            exitCode.Should().Be(0);
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_ValidMetadataYaml_OutputPdfExists()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var yamlPath = System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.yaml");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        try
        {
            await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, yamlPath, outputPath });
            System.IO.File.Exists(outputPath).Should().BeTrue();
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_ValidMetadataYaml_OutputIsValidPdf()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var yamlPath = System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.yaml");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        try
        {
            await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, yamlPath, outputPath });

            var act = () =>
            {
                using var reader = new PdfReader(outputPath);
                using var pdf = new PdfDocument(reader);
                return pdf.GetNumberOfPages();
            };
            act.Should().NotThrow();
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_InvalidYaml_ExitCodeTwo()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        var yamlPath = System.IO.Path.GetTempFileName() + ".yaml";
        await System.IO.File.WriteAllTextAsync(yamlPath,
            "version: \"1.0\"\nmetadata:\n  language: \"en-US\"\n  tab_order: \"invalid_value\"\n");
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, yamlPath, outputPath });
            exitCode.Should().Be(2);
        }
        finally
        {
            if (System.IO.File.Exists(yamlPath)) System.IO.File.Delete(yamlPath);
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_InvalidYaml_AllErrorsInStderr()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        var yamlPath = System.IO.Path.GetTempFileName() + ".yaml";
        await System.IO.File.WriteAllTextAsync(yamlPath,
            "version: \"1.0\"\nmetadata:\n  language: \"\"\n  tab_order: \"invalid_value\"\n");
        var console = new TestOutputConsole();
        try
        {
            await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, yamlPath, outputPath }, console);
            console.ErrorText.Should().Contain("Validation failed:");
            console.ErrorText.Should().Contain("language");
            console.ErrorText.Should().Contain("tab_order");
        }
        finally
        {
            if (System.IO.File.Exists(yamlPath)) System.IO.File.Delete(yamlPath);
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_NonExistentPdf_ExitCodeOne()
    {
        var yamlPath = System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.yaml");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        try
        {
            var console = new TestOutputConsole();
            var exitCode = await BuildRoot().InvokeAsync(
                new[] { "execute", "./nonexistent.pdf", yamlPath, outputPath }, console);
            exitCode.Should().Be(1);
            console.ErrorText.Should().Contain("Error:");
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_NonExistentInstructions_ExitCodeOne()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        try
        {
            var console = new TestOutputConsole();
            var exitCode = await BuildRoot().InvokeAsync(
                new[] { "execute", pdfPath, "./nonexistent.yaml", outputPath }, console);
            exitCode.Should().Be(1);
            console.ErrorText.Should().Contain("Error:");
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_NonExistentOutputDir_ExitCodeOne()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var yamlPath = System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.yaml");
        var outputPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "nonexistent_dir_execute987", "output.pdf");
        var console = new TestOutputConsole();

        var exitCode = await BuildRoot().InvokeAsync(
            new[] { "execute", pdfPath, yamlPath, outputPath }, console);
        exitCode.Should().Be(1);
        console.ErrorText.Should().Contain("Error:");
    }

    [Fact]
    public async Task Execute_YamlExtension_ExitCodeZero()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var yamlPath = System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.yaml");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, yamlPath, outputPath });
            exitCode.Should().Be(0);
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_YmlExtension_ExitCodeZero()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var ymlPath = System.IO.Path.GetTempFileName() + ".yml";
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        var content = await System.IO.File.ReadAllTextAsync(
            System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.yaml"));
        await System.IO.File.WriteAllTextAsync(ymlPath, content);
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, ymlPath, outputPath });
            exitCode.Should().Be(0);
        }
        finally
        {
            if (System.IO.File.Exists(ymlPath)) System.IO.File.Delete(ymlPath);
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_JsonExtension_ExitCodeZero()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var jsonPath = System.IO.Path.Combine(FIXTURES_PATH, "metadata_only.json");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(new[] { "execute", pdfPath, jsonPath, outputPath });
            exitCode.Should().Be(0);
        }
        finally
        {
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Execute_UnrecognizedExtension_ExitCodeOne()
    {
        var pdfPath = System.IO.Path.Combine(FIXTURES_PATH, "untagged_simple.pdf");
        var outputPath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".pdf");
        var txtPath = System.IO.Path.GetTempFileName() + ".txt";
        await System.IO.File.WriteAllTextAsync(txtPath, "version: \"1.0\"");
        var console = new TestOutputConsole();
        try
        {
            var exitCode = await BuildRoot().InvokeAsync(
                new[] { "execute", pdfPath, txtPath, outputPath }, console);
            exitCode.Should().Be(1);
            console.ErrorText.Should().Contain("Error:");
        }
        finally
        {
            if (System.IO.File.Exists(txtPath)) System.IO.File.Delete(txtPath);
            if (System.IO.File.Exists(outputPath)) System.IO.File.Delete(outputPath);
        }
    }
}
