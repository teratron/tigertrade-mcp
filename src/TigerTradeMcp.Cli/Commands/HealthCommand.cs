using Spectre.Console.Cli;
using TigerTradeMcp.Core;
using TigerTradeMcp.Core.Abstractions;

namespace TigerTradeMcp.Cli.Commands;

internal sealed class HealthCommand(ITigerLinkClient tigerLink) : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        var health = await tigerLink.CheckHealthAsync(ct);
        Console.WriteLine(SafeJson.Serialize(health));
        return health.Connected ? 0 : 1;
    }
}
