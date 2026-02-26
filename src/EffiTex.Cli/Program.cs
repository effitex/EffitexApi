using System.CommandLine;
using EffiTex.Cli;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
CliSetup.ConfigureServices(services);
var provider = services.BuildServiceProvider();

var rootCommand = new RootCommand("EffiTex PDF structure API — local CLI");
rootCommand.Subcommands.Add(InspectCommand.Build(provider));
rootCommand.Subcommands.Add(ExecuteCommand.Build(provider));

return await rootCommand.Parse(args).InvokeAsync();
