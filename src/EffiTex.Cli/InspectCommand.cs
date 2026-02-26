using System.CommandLine;
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
        var pdfArg = new Argument<FileInfo>("pdf-path") { Description = "Path to the input PDF file" };
        var outputArg = new Argument<FileInfo>("output-path") { Description = "Path where the JSON result will be written" };

        command.Arguments.Add(pdfArg);
        command.Arguments.Add(outputArg);

        command.SetAction(async (parseResult, ct) =>
        {
            var pdfPath = parseResult.GetValue(pdfArg);
            var outputPath = parseResult.GetValue(outputArg);

            if (!pdfPath.Exists)
            {
                Console.Error.WriteLine($"Error: Input file not found: {pdfPath.FullName}");
                return 1;
            }

            if (outputPath.Directory == null || !outputPath.Directory.Exists)
            {
                var dirPath = outputPath.Directory?.FullName ?? System.IO.Path.GetDirectoryName(outputPath.FullName) ?? "unknown";
                Console.Error.WriteLine($"Error: Output directory not found: {dirPath}");
                return 1;
            }

            try
            {
                var handler = provider.GetRequiredService<InspectHandler>();
                using var stream = pdfPath.OpenRead();
                var result = handler.Inspect(stream);
                var json = JsonSerializer.Serialize(result, JSON_OPTIONS);
                await File.WriteAllTextAsync(outputPath.FullName, json, ct);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }
}
