namespace TigerTradeMcp.Core.Configuration;

public sealed class TigerLinkOptions
{
    public const string SectionName = "TigerLink";

    public string Url { get; init; } = "ws://localhost:7819";
    public int ConnectTimeoutSeconds { get; init; } = 5;
    public int ReconnectMaxAttempts { get; init; } = 5;
    public int ReconnectBaseDelayMs { get; init; } = 500;
    public int ReconnectMaxDelayMs { get; init; } = 30_000;
}
