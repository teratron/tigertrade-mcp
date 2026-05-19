using Microsoft.Extensions.Options;
using Spectre.Console.Cli;
using TigerTradeMcp.Core;
using TigerTradeMcp.Core.Configuration;

namespace TigerTradeMcp.Cli.Commands;

internal sealed class ConfigCommand(
    IOptions<TigerLinkOptions> tigerLinkOpts,
    IOptions<BrokerOptions> brokerOpts,
    IOptions<TradingOptions> tradingOpts) : Command
{
    protected override int Execute(CommandContext context, CancellationToken ct)
    {
        var tl = tigerLinkOpts.Value;
        var br = brokerOpts.Value;
        var tr = tradingOpts.Value;

        Console.WriteLine(SafeJson.Serialize(new
        {
            tigerLink = new { url = tl.Url, connectTimeoutSeconds = tl.ConnectTimeoutSeconds, reconnectMaxAttempts = tl.ReconnectMaxAttempts },
            broker = new { type = br.Type },
            trading = new { enabled = tr.Enabled, auditLogPath = tr.AuditLogPath },
        }));
        return 0;
    }
}
