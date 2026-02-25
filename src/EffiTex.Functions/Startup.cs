using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EffiTex.Functions;

public static class Startup
{
    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var config = context.Configuration;
        var storageConnection = config["AzureWebJobsStorage"] ?? "UseDevelopmentStorage=true";

        // Azure Storage clients
        services.AddSingleton(new BlobServiceClient(storageConnection));
        services.AddSingleton(new QueueServiceClient(storageConnection));
        services.AddSingleton(new TableServiceClient(storageConnection));

        // Core services
        services.AddSingleton<InstructionDeserializer>();
        services.AddSingleton<InstructionValidator>();

        // Engine handlers
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

        // Job processing
        services.AddSingleton<IJobProcessor, JobProcessor>();

        // HTTP client for callbacks
        services.AddHttpClient();

        // JSON serialization with camelCase
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
    }
}
