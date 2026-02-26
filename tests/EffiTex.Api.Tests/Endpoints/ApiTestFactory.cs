using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using EffiTex.Api.Data;
using EffiTex.Api.Storage;

namespace EffiTex.Api.Tests.Endpoints;

public class ApiTestFactory : WebApplicationFactory<Program>
{
    public Mock<IDocumentRepository> MockRepo { get; } = new();
    public Mock<IBlobStorageService> MockBlob { get; } = new();
    public Mock<IBackgroundJobClient> MockJobClient { get; } = new();

    static ApiTestFactory()
    {
        // Provide a fallback so Program.cs ?? throw succeeds when no real DB is configured.
        // Preserved as-is if the caller has already set a real connection string.
        Environment.SetEnvironmentVariable("EFFITEX_PG_CONNECTION",
            Environment.GetEnvironmentVariable("EFFITEX_PG_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=effitext;Username=effitex_user;Password=Password123");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            // Replace Hangfire storage with InMemory and remove the server hosted service
            services.AddHangfire(cfg => cfg.UseInMemoryStorage());
            var hangfireHostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.Namespace?.StartsWith("Hangfire") == true)
                .ToList();
            foreach (var d in hangfireHostedServices)
                services.Remove(d);

            // Mock application services
            services.AddScoped<IDocumentRepository>(_ => MockRepo.Object);
            services.AddSingleton<IBlobStorageService>(MockBlob.Object);
            services.AddSingleton<IBackgroundJobClient>(MockJobClient.Object);
        });
    }
}
