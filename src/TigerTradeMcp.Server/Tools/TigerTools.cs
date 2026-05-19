using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using TigerTradeMcp.Core;
using TigerTradeMcp.Core.Abstractions;
using TigerTradeMcp.Core.Configuration;

namespace TigerTradeMcp.Server.Tools;

[McpServerToolType]
public static class TigerTools
{
    [McpServerTool(Name = "tiger_health_check")]
    [Description("Checks connectivity to the locally running Tiger Trade terminal via the Tiger API WebSocket (ws://localhost:7819). Returns connection status and endpoint URL.")]
    public static async Task<string> HealthCheck(
        ITigerLinkClient tigerLink,
        CancellationToken ct)
    {
        var health = await tigerLink.CheckHealthAsync(ct);
        return ToolResponse.Success(health).ToJson();
    }

    [McpServerTool(Name = "tiger_set_link_symbol")]
    [Description("Switches the active symbol in a Tiger Trade link-group via the Tiger API. Requires the Tiger Trade terminal to be running with Local Signal Server enabled (Settings → Local Signal Server → Enable Signal Server).")]
    public static async Task<string> SetLinkSymbol(
        ITigerLinkClient tigerLink,
        [Description("Exchange identifier (e.g. BINANCE, NYSE, MOEX)")] string exchange,
        [Description("Market segment (e.g. SPOT, PERP, FUTURES)")] string market,
        [Description("Symbol ticker (e.g. BTCUSDT, AAPL, SBER)")] string symbol,
        [Description("Link group letter: A, B, C, D, E, F, G, or H")] string linkGroup,
        CancellationToken ct)
    {
        try
        {
            await tigerLink.SetLinkSymbolAsync(exchange, market, symbol, linkGroup, ct);
            return ToolResponse.Success(new { exchange, market, symbol, linkGroup, switched = true }).ToJson();
        }
        catch (Exception ex)
        {
            return ToolResponse.Failure(ex.Message).ToJson();
        }
    }

    [McpServerTool(Name = "tiger_get_config")]
    [Description("Returns the current non-secret server configuration: TigerLink endpoint, broker type, and trading safety flags.")]
    public static string GetConfig(
        IOptions<TigerLinkOptions> tigerLinkOpts,
        IOptions<BrokerOptions> brokerOpts,
        IOptions<TradingOptions> tradingOpts)
    {
        var tl = tigerLinkOpts.Value;
        var br = brokerOpts.Value;
        var tr = tradingOpts.Value;

        return ToolResponse.Success(new
        {
            tigerLink = new
            {
                url = tl.Url,
                connectTimeoutSeconds = tl.ConnectTimeoutSeconds,
                reconnectMaxAttempts = tl.ReconnectMaxAttempts,
            },
            broker = new { type = br.Type },
            trading = new
            {
                enabled = tr.Enabled,
                auditLogPath = tr.AuditLogPath,
            },
        }).ToJson();
    }
}
