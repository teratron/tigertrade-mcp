using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TigerTradeMcp.Core;
using TigerTradeMcp.Core.Abstractions;
using TigerTradeMcp.Core.Configuration;

namespace TigerTradeMcp.TigerLink;

public sealed partial class TigerLinkClient : ITigerLinkClient, IAsyncDisposable
{
    private readonly TigerLinkOptions _options;
    private readonly ILogger<TigerLinkClient> _logger;
    private readonly WebSocketClient _ws;

    public TigerLinkClient(IOptions<TigerLinkOptions> options, ILogger<TigerLinkClient> logger, ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _logger = logger;
        _ws = new WebSocketClient(_options, loggerFactory.CreateLogger<WebSocketClient>());
    }

    public async Task<TigerLinkHealth> CheckHealthAsync(CancellationToken ct = default)
    {
        if (_ws.IsConnected)
            return new TigerLinkHealth(Connected: true, Endpoint: _options.Url);

        try
        {
            await _ws.ConnectAsync(ct);
            return new TigerLinkHealth(Connected: true, Endpoint: _options.Url);
        }
        catch (TigerLinkException ex)
        {
            return new TigerLinkHealth(Connected: false, Endpoint: _options.Url, Error: ex.Message);
        }
        catch (Exception ex)
        {
            return new TigerLinkHealth(Connected: false, Endpoint: _options.Url, Error: ex.Message);
        }
    }

    public async Task SetLinkSymbolAsync(
        string exchange,
        string market,
        string symbol,
        string linkGroup,
        CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);

        var command = new
        {
            command = "setLinkSymbol",
            exchange,
            market,
            symbol,
            linkGroup,
        };

        string json = SafeJson.Serialize(command);
        LogSendingCommand(_logger, json);
        await _ws.SendAsync(json, ct);
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_ws.IsConnected) return;
        await _ws.ConnectAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _ws.DisposeAsync();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending setLinkSymbol: {Json}")]
    private static partial void LogSendingCommand(ILogger logger, string json);
}
