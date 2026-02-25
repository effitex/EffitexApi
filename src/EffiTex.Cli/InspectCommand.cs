using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Text.Json;
using EffiTex.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace EffiTex.Cli;

public static class InspectCommand
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static Command Build(IServiceProvider provider)
    {
        var command = new Command("inspect", "Inspect the structure of a PDF file");
        var pdfArg = new Argument<FileInfo>("pdf-path", "Path to the input PDF file");
        var outputArg = new Argument<FileInfo>("output-path", "Path where the JSON result will be written");

        command.AddArgument(pdfArg);
        command.AddArgument(outputArg);

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var pdfPath = ctx.ParseResult.GetValueForArgument(pdfArg);
            var outputPath = ctx.ParseResult.GetValueForArgument(outputArg);

            if (!pdfPath.Exists)
            {
                StandardStreamWriter.WriteLine(ctx.Console.Error, $"Error: Input file not found: {pdfPath.FullName}");
                ctx.ExitCode = 1;
                return;
            }

            if (outputPath.Directory == null || !outputPath.Directory.Exists)
            {
                var dirPath = outputPath.Directory?.FullName ?? System.IO.Path.GetDirectoryName(outputPath.FullName) ?? "unknown";
                StandardStreamWriter.WriteLine(ctx.Console.Error, $"Error: Output directory not found: {dirPath}");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                var handler = provider.GetRequiredService<InspectHandler>();
                using var stream = pdfPath.OpenRead();
                var result = handler.Inspect(stream);
                var json = JsonSerializer.Serialize(result, JSON_OPTIONS);
                await File.WriteAllTextAsync(outputPath.FullName, json);
                ctx.ExitCode = 0;
            }
            catch (Exception ex)
            {
                StandardStreamWriter.WriteLine(ctx.Console.Error, $"Error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return command;
    }
}
