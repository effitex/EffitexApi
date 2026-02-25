using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
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
        var pdfArg = new Argument<FileInfo>("pdf-path", "Path to the input PDF file");
        var instructionsArg = new Argument<FileInfo>("instructions-path", "Path to the YAML or JSON instruction file");
        var outputArg = new Argument<FileInfo>("output-path", "Path where the result PDF will be written");

        command.AddArgument(pdfArg);
        command.AddArgument(instructionsArg);
        command.AddArgument(outputArg);

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var pdfPath = ctx.ParseResult.GetValueForArgument(pdfArg);
            var instructionsPath = ctx.ParseResult.GetValueForArgument(instructionsArg);
            var outputPath = ctx.ParseResult.GetValueForArgument(outputArg);

            if (!pdfPath.Exists)
            {
                StandardStreamWriter.WriteLine(ctx.Console.Error, $"Error: Input file not found: {pdfPath.FullName}");
                ctx.ExitCode = 1;
                return;
            }

            if (!instructionsPath.Exists)
            {
                StandardStreamWriter.WriteLine(ctx.Console.Error, $"Error: Input file not found: {instructionsPath.FullName}");
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
                StandardStreamWriter.WriteLine(ctx.Console.Error,
                    $"Error: Unrecognized instruction file format '{ext}'. Use .yaml, .yml, or .json.");
                ctx.ExitCode = 1;
                return;
            }

            try
            {
                var content = await File.ReadAllTextAsync(instructionsPath.FullName);
                var deserializer = provider.GetRequiredService<InstructionDeserializer>();
                var instructions = deserializer.Deserialize(content, contentType);

                var validator = provider.GetRequiredService<InstructionValidator>();
                var validation = validator.Validate(instructions);

                if (!validation.IsValid)
                {
                    StandardStreamWriter.WriteLine(ctx.Console.Error, "Validation failed:");
                    foreach (var error in validation.Errors)
                    {
                        StandardStreamWriter.WriteLine(ctx.Console.Error, $"  - {error.Field}: {error.Message}");
                    }
                    ctx.ExitCode = 2;
                    return;
                }

                var interpreter = provider.GetRequiredService<Interpreter>();
                using var inputStream = pdfPath.OpenRead();
                var resultStream = interpreter.Execute(inputStream, instructions);

                using var outputStream = File.Create(outputPath.FullName);
                resultStream.Position = 0;
                await resultStream.CopyToAsync(outputStream);
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
