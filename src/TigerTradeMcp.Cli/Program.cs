using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console.Cli;
using TigerTradeMcp.Cli.Commands;
using TigerTradeMcp.Cli.Infrastructure;
using TigerTradeMcp.Core.Abstractions;
using TigerTradeMcp.Core.Configuration;
using TigerTradeMcp.TigerLink;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "TTMCP_")
    .Build();

var services = new ServiceCollection();

services.AddOptions<TigerLinkOptions>()
    .Bind(configuration.GetSection(TigerLinkOptions.SectionName));
services.AddOptions<BrokerOptions>()
    .Bind(configuration.GetSection(BrokerOptions.SectionName));
services.AddOptions<TradingOptions>()
    .Bind(configuration.GetSection(TradingOptions.SectionName));

services.AddLogging(lb => lb
    .AddConsole()
    .SetMinimumLevel(LogLevel.Warning));

services.AddSingleton<ITigerLinkClient, TigerLinkClient>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("tt");
    config.AddCommand<HealthCommand>("health")
        .WithDescription("Check connectivity to the Tiger Trade terminal.");
    config.AddCommand<SymbolCommand>("symbol")
        .WithDescription("Switch the active symbol in a Tiger Trade link-group.")
        .WithExample("symbol", "BINANCE", "SPOT", "BTCUSDT", "--group", "A");
    config.AddCommand<ConfigCommand>("config")
        .WithDescription("Print current server configuration.");
});

return await app.RunAsync(args);
