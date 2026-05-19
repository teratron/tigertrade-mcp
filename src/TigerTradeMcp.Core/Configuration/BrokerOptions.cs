namespace TigerTradeMcp.Core.Configuration;

public sealed class BrokerOptions
{
    public const string SectionName = "Broker";

    public string Type { get; init; } = "Mock";
    public Dictionary<string, string> Settings { get; init; } = new();
}
