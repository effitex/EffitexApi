using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace EffiTex.Cli;

public static class CliSetup
{
    public static void ConfigureServices(IServiceCollection services)
    {
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
        services.AddSingleton<InspectHandler>();
        services.AddSingleton<InstructionDeserializer>();
        services.AddSingleton<InstructionValidator>();
    }
}
