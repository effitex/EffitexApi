using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace EffiTex.Functions.Tests.Helpers;

public static class FunctionContextHelper
{
    public static Mock<FunctionContext> CreateMockContext()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new JsonObjectSerializer();
        });
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var contextMock = new Mock<FunctionContext>();
        contextMock.SetupProperty(c => c.InstanceServices, serviceProvider);
        return contextMock;
    }
}
