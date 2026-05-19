namespace TigerTradeMcp.Core.Configuration;

public sealed class TradingOptions
{
    public const string SectionName = "Trading";

    public bool Enabled { get; init; }
    public string RequireConfirmText { get; init; } = "I-CONFIRM-LIVE-ORDER";
    public string AuditLogPath { get; init; } = "logs/trading-audit-.jsonl";
}
