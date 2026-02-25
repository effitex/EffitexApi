using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using EffiTex.Functions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EffiTex.Functions.Tests;

public class StartupTests
{
    private readonly IServiceProvider _serviceProvider;

    public StartupTests()
    {
        var host = new HostBuilder()
            .ConfigureServices((context, services) =>
            {
                Startup.ConfigureServices(context, services);
            })
            .Build();

        _serviceProvider = host.Services;
    }

    [Fact]
    public void Startup_CanResolveInterpreter()
    {
        var interpreter = _serviceProvider.GetService<Interpreter>();
        interpreter.Should().NotBeNull();
    }

    [Fact]
    public void Startup_CanResolveInspectHandler()
    {
        var handler = _serviceProvider.GetService<InspectHandler>();
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Startup_CanResolveAllEngineHandlers()
    {
        _serviceProvider.GetService<MetadataHandler>().Should().NotBeNull();
        _serviceProvider.GetService<StructureHandler>().Should().NotBeNull();
        _serviceProvider.GetService<ContentTaggingHandler>().Should().NotBeNull();
        _serviceProvider.GetService<ArtifactHandler>().Should().NotBeNull();
        _serviceProvider.GetService<AnnotationHandler>().Should().NotBeNull();
        _serviceProvider.GetService<FontHandler>().Should().NotBeNull();
        _serviceProvider.GetService<OcrHandler>().Should().NotBeNull();
        _serviceProvider.GetService<BookmarkHandler>().Should().NotBeNull();
        _serviceProvider.GetService<BboxResolver>().Should().NotBeNull();
    }

    [Fact]
    public void Startup_CanResolveCoreServices()
    {
        _serviceProvider.GetService<InstructionDeserializer>().Should().NotBeNull();
        _serviceProvider.GetService<InstructionValidator>().Should().NotBeNull();
    }

    [Fact]
    public void Startup_CanResolveJobProcessor()
    {
        var processor = _serviceProvider.GetService<IJobProcessor>();
        processor.Should().NotBeNull();
    }
}
