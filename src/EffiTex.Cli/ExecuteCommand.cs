using System.CommandLine;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace EffiTex.Cli;

public static class ExecuteCommand
{
    public static Command Build(IServiceProvider provider)
    {
        var command = new Command("execute", "Execute instructions against a PDF file");
        var pdfArg = new Argument<FileInfo>("pdf-path") { Description = "Path to the input PDF file" };
        var instructionsArg = new Argument<FileInfo>("instructions-path") { Description = "Path to the YAML or JSON instruction file" };
        var outputArg = new Argument<FileInfo>("output-path") { Description = "Path where the result PDF will be written" };

        command.Arguments.Add(pdfArg);
        command.Arguments.Add(instructionsArg);
        command.Arguments.Add(outputArg);

        command.SetAction(async (parseResult, ct) =>
        {
            var pdfPath = parseResult.GetValue(pdfArg);
            var instructionsPath = parseResult.GetValue(instructionsArg);
            var outputPath = parseResult.GetValue(outputArg);

            if (!pdfPath.Exists)
            {
                Console.Error.WriteLine($"Error: Input file not found: {pdfPath.FullName}");
                return 1;
            }

            if (!instructionsPath.Exists)
            {
                Console.Error.WriteLine($"Error: Input file not found: {instructionsPath.FullName}");
                return 1;
            }

            if (outputPath.Directory == null || !outputPath.Directory.Exists)
            {
                var dirPath = outputPath.Directory?.FullName ?? System.IO.Path.GetDirectoryName(outputPath.FullName) ?? "unknown";
                Console.Error.WriteLine($"Error: Output directory not found: {dirPath}");
                return 1;
            }

            var ext = instructionsPath.Extension.ToLowerInvariant();
            string contentType;

            if (ext == ".yaml" || ext == ".yml")
            {
                contentType = "application/yaml";
            }
            else if (ext == ".json")
            {
                contentType = "application/json";
            }
            else
            {
                Console.Error.WriteLine(
                    $"Error: Unrecognized instruction file format '{ext}'. Use .yaml, .yml, or .json.");
                return 1;
            }

            try
            {
                var content = await File.ReadAllTextAsync(instructionsPath.FullName, ct);
                var deserializer = provider.GetRequiredService<InstructionDeserializer>();
                var instructions = deserializer.Deserialize(content, contentType);

                var validator = provider.GetRequiredService<InstructionValidator>();
                var validation = validator.Validate(instructions);

                if (!validation.IsValid)
                {
                    Console.Error.WriteLine("Validation failed:");
                    foreach (var error in validation.Errors)
                    {
                        Console.Error.WriteLine($"  - {error.Field}: {error.Message}");
                    }
                    return 2;
                }

                var interpreter = provider.GetRequiredService<Interpreter>();
                using var inputStream = pdfPath.OpenRead();
                var resultStream = interpreter.Execute(inputStream, instructions);

                using var outputStream = File.Create(outputPath.FullName);
                resultStream.Position = 0;
                await resultStream.CopyToAsync(outputStream, ct);
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
