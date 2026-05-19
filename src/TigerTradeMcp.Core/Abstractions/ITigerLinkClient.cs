namespace TigerTradeMcp.Core.Abstractions;

public interface ITigerLinkClient
{
    Task<TigerLinkHealth> CheckHealthAsync(CancellationToken ct = default);

    Task SetLinkSymbolAsync(
        string exchange,
        string market,
        string symbol,
        string linkGroup,
        CancellationToken ct = default);
}

public sealed record TigerLinkHealth(bool Connected, string Endpoint, string? Error = null);
