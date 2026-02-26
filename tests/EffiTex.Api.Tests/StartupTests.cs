using System.Net;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using EffiTex.Api.Data;
using EffiTex.Api.Jobs;
using EffiTex.Api.Storage;
using EffiTex.Engine;
using Hangfire;
using Hangfire.InMemory;

namespace EffiTex.Api.Tests;

public class StartupTestFactory : WebApplicationFactory<Program>
{
    static StartupTestFactory()
    {
        // Must be set before WebApplicationFactory starts the host so that
        // builder.Configuration["..."] ?? throw succeeds in Program.cs
        Environment.SetEnvironmentVariable("EFFITEX_PG_CONNECTION", "Host=stub;Database=stub;Username=stub;Password=stub");
        Environment.SetEnvironmentVariable("EFFITEX_STORAGE_CONNECTION", "UseDevelopmentStorage=true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Replace EF Core with InMemory by swapping DbContextOptions directly
            var inMemoryOptions = new DbContextOptionsBuilder<EffiTexDbContext>()
                .UseInMemoryDatabase("startup-test")
                .Options;

            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<EffiTexDbContext>))
                .ToList();
            foreach (var d in dbDescriptors)
                services.Remove(d);

            services.AddSingleton<DbContextOptions<EffiTexDbContext>>(inMemoryOptions);
            services.AddSingleton<DbContextOptions>(inMemoryOptions);

            // Replace Hangfire with InMemory storage
            services.AddHangfire(cfg => cfg.UseInMemoryStorage());

            // Replace BlobServiceClient and IBlobStorageService with mocks
            var blobClientDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(BlobServiceClient));
            if (blobClientDesc != null)
                services.Remove(blobClientDesc);

            var blobServiceDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(IBlobStorageService));
            if (blobServiceDesc != null)
                services.Remove(blobServiceDesc);

            services.AddSingleton<IBlobStorageService>(new Mock<IBlobStorageService>().Object);
        });
    }
}

public class StartupTests : IClassFixture<StartupTestFactory>
{
    private readonly StartupTestFactory _factory;

    public StartupTests(StartupTestFactory factory) => _factory = factory;

    [Fact]
    public void IDocumentRepository_Resolves_WithoutException()
    {
        using var scope = _factory.Services.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        act.Should().NotThrow();
    }

    [Fact]
    public void IBlobStorageService_Resolves_WithoutException()
    {
        using var scope = _factory.Services.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        act.Should().NotThrow();
    }

    [Fact]
    public void Interpreter_Resolves_WithoutException()
    {
        using var scope = _factory.Services.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<Interpreter>();
        act.Should().NotThrow();
    }

    [Fact]
    public void InspectJob_Resolves_WithoutException()
    {
        using var scope = _factory.Services.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<InspectJob>();
        act.Should().NotThrow();
    }

    [Fact]
    public void ExecuteJob_Resolves_WithoutException()
    {
        using var scope = _factory.Services.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<ExecuteJob>();
        act.Should().NotThrow();
    }

    [Fact]
    public void CleanupJob_Resolves_WithoutException()
    {
        using var scope = _factory.Services.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<CleanupJob>();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetJobStatus_UnknownJobId_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/jobs/00000000-0000-0000-0000-000000000000/status");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
