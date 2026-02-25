using System.CommandLine;
using EffiTex.Cli;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
CliSetup.ConfigureServices(services);
var provider = services.BuildServiceProvider();

var rootCommand = new RootCommand("EffiTex PDF structure API — local CLI");
rootCommand.AddCommand(InspectCommand.Build(provider));
rootCommand.AddCommand(ExecuteCommand.Build(provider));

return await rootCommand.InvokeAsync(args);
