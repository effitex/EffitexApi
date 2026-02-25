using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using EffiTex.Core.Deserialization;
using EffiTex.Core.Validation;
using EffiTex.Engine;
using EffiTex.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        Startup.ConfigureServices(context, services);
    })
    .Build();

host.Run();
