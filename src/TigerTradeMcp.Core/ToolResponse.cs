namespace TigerTradeMcp.Core;

public sealed record ToolResponse
{
    public bool Ok { get; init; }
    public object? Data { get; init; }
    public string? Error { get; init; }

    public static ToolResponse Success(object data) => new() { Ok = true, Data = data };
    public static ToolResponse Failure(string error) => new() { Ok = false, Error = error };

    public string ToJson() => SafeJson.Serialize(this);
}
