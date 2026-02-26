using Azure.Storage.Blobs;
using EffiTex.Api;
using EffiTex.Api.Data;
using EffiTex.Api.Endpoints;
using EffiTex.Api.Jobs;
using EffiTex.Api.Storage;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using Hangfire;
using Hangfire.Common;
using Hangfire.PostgreSql;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var pgConnection = builder.Configuration["EFFITEX_PG_CONNECTION"]
    ?? throw new InvalidOperationException("EFFITEX_PG_CONNECTION is required");
var storageConnection = builder.Configuration["EFFITEX_STORAGE_CONNECTION"];
if (storageConnection == null && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("EFFITEX_STORAGE_CONNECTION is required");
var ttlHours = int.Parse(builder.Configuration["EFFITEX_TTL_HOURS"] ?? "24");

// EF Core
builder.Services.AddDbContext<EffiTexDbContext>(opts =>
    opts.UseNpgsql(pgConnection));

// Repository
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Blob Storage — skipped in Testing (test factories provide a mock via ConfigureTestServices)
if (storageConnection != null)
{
    var uploadContainer = builder.Configuration["EFFITEX_UPLOAD_CONTAINER"] ?? "effitex-upload";
    var inspectContainer = builder.Configuration["EFFITEX_INSPECT_CONTAINER"] ?? "effitex-inspect";
    var executeContainer = builder.Configuration["EFFITEX_EXECUTE_CONTAINER"] ?? "effitex-execute";
    builder.Services.AddSingleton(_ => new BlobServiceClient(storageConnection));
    builder.Services.AddSingleton<IBlobStorageService>(sp =>
        new BlobStorageService(sp.GetRequiredService<BlobServiceClient>(), uploadContainer, inspectContainer, executeContainer));
}

// TTL
builder.Services.AddSingleton(new EffiTexOptions { TtlHours = ttlHours });

// EffiTex.Engine — all handlers and interpreter
builder.Services.AddSingleton<BboxResolver>();
builder.Services.AddSingleton<MetadataHandler>();
builder.Services.AddSingleton<StructureHandler>();
builder.Services.AddSingleton<ContentTaggingHandler>();
builder.Services.AddSingleton<ArtifactHandler>();
builder.Services.AddSingleton<AnnotationHandler>();
builder.Services.AddSingleton<FontHandler>();
builder.Services.AddSingleton<OcrHandler>();
builder.Services.AddSingleton<BookmarkHandler>();
builder.Services.AddSingleton<InspectHandler>();
builder.Services.AddSingleton<Interpreter>();

// Runner wrappers
builder.Services.AddSingleton<IInspectRunner, InspectRunner>();
builder.Services.AddSingleton<IExecuteRunner, ExecuteRunner>();

// EffiTex.Core — deserialization and validation
builder.Services.AddSingleton<InstructionDeserializer>();
builder.Services.AddSingleton<InstructionValidator>();

// Background jobs
builder.Services.AddScoped<InspectJob>();
builder.Services.AddScoped<ExecuteJob>();
builder.Services.AddScoped<CleanupJob>();

// Hangfire
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(pgConnection)));
builder.Services.AddHangfireServer();

var app = builder.Build();

// Apply EF migrations on startup
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EffiTexDbContext>();
    db.Database.Migrate();
}

// Hangfire dashboard — development only
if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire");

// Register recurring cleanup job
var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
recurringJobs.AddOrUpdate<CleanupJob>(
    "effitex-cleanup",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly);

app.MapDocumentEndpoints();
app.MapJobEndpoints();

app.Run();

public partial class Program { }
