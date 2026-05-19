using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace TigerTradeMcp.Server.Tools;

[McpServerToolType]
public static class ServerInfoTool
{
    [McpServerTool(Name = "server_info")]
    [Description("Returns metadata about the running tigertrade-mcp server: name, version, process id.")]
    public static string GetServerInfo()
    {
        var payload = new
        {
            name = "tigertrade-mcp",
            version = ThisAssembly.InformationalVersion,
            pid = Environment.ProcessId,
            os = Environment.OSVersion.ToString()
        };
        return JsonSerializer.Serialize(payload);
    }
}

internal static class ThisAssembly
{
    public static string InformationalVersion =>
        typeof(ThisAssembly).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "0.0.0-dev";
}
