using System.ComponentModel;
using Spectre.Console.Cli;
using TigerTradeMcp.Core;
using TigerTradeMcp.Core.Abstractions;

namespace TigerTradeMcp.Cli.Commands;

internal sealed class SymbolCommand(ITigerLinkClient tigerLink) : AsyncCommand<SymbolCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<exchange>")]
        [Description("Exchange identifier (e.g. BINANCE, NYSE, MOEX)")]
        public string Exchange { get; init; } = string.Empty;

        [CommandArgument(1, "<market>")]
        [Description("Market segment (e.g. SPOT, PERP, FUTURES)")]
        public string Market { get; init; } = string.Empty;

        [CommandArgument(2, "<symbol>")]
        [Description("Ticker symbol (e.g. BTCUSDT, AAPL, SBER)")]
        public string Symbol { get; init; } = string.Empty;

        [CommandOption("-g|--group")]
        [Description("Link group letter: A–H (default: A)")]
        [DefaultValue("A")]
        public string LinkGroup { get; init; } = "A";
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        try
        {
            await tigerLink.SetLinkSymbolAsync(settings.Exchange, settings.Market, settings.Symbol, settings.LinkGroup, ct);
            Console.WriteLine(SafeJson.Serialize(new
            {
                ok = true,
                exchange = settings.Exchange,
                market = settings.Market,
                symbol = settings.Symbol,
                linkGroup = settings.LinkGroup,
            }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(SafeJson.Serialize(new { ok = false, error = ex.Message }));
            return 1;
        }
    }
}
