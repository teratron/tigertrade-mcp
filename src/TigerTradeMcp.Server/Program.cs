using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Serilog;
using TigerTradeMcp.Core.Abstractions;
using TigerTradeMcp.Core.Configuration;
using TigerTradeMcp.TigerLink;

namespace TigerTradeMcp.Server;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "TTMCP_");

        builder.Services
            .AddOptions<TigerLinkOptions>()
            .Bind(builder.Configuration.GetSection(TigerLinkOptions.SectionName))
            .ValidateOnStart();
        builder.Services
            .AddOptions<BrokerOptions>()
            .Bind(builder.Configuration.GetSection(BrokerOptions.SectionName))
            .ValidateOnStart();
        builder.Services
            .AddOptions<TradingOptions>()
            .Bind(builder.Configuration.GetSection(TradingOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services));

        builder.Services.AddSingleton<ITigerLinkClient, TigerLinkClient>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();

        Log.Information("tigertrade-mcp-server starting (PID {Pid})", Environment.ProcessId);

        try
        {
            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error in tigertrade-mcp-server");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
