using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using EffiTex.Api.Data;
using EffiTex.Api.Storage;

namespace EffiTex.Api.Tests.Endpoints;

public class ApiTestFactory : WebApplicationFactory<Program>
{
    public Mock<IDocumentRepository> MockRepo { get; } = new();
    public Mock<IBlobStorageService> MockBlob { get; } = new();
    public Mock<IBackgroundJobClient> MockJobClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IDocumentRepository>(_ => MockRepo.Object);
            services.AddSingleton<IBlobStorageService>(MockBlob.Object);
            services.AddSingleton<IBackgroundJobClient>(MockJobClient.Object);
        });
    }
}
