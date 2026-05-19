using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using TigerTradeMcp.Core.Configuration;

namespace TigerTradeMcp.TigerLink;

internal sealed partial class WebSocketClient : IAsyncDisposable
{
    private readonly TigerLinkOptions _options;
    private readonly ILogger<WebSocketClient> _logger;
    private readonly Uri _uri;
    private ClientWebSocket _ws = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketState State => _ws.State;
    public bool IsConnected => _ws.State == WebSocketState.Open;

    public WebSocketClient(TigerLinkOptions options, ILogger<WebSocketClient> logger)
    {
        _options = options;
        _logger = logger;
        _uri = new Uri(options.Url);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected) return;

        for (int attempt = 1; attempt <= _options.ReconnectMaxAttempts; attempt++)
        {
            try
            {
                if (_ws.State is WebSocketState.Aborted or WebSocketState.Closed)
                {
                    _ws.Dispose();
                    _ws = new ClientWebSocket();
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

                LogConnecting(_logger, _uri, attempt, _options.ReconnectMaxAttempts);
                await _ws.ConnectAsync(_uri, cts.Token);
                LogConnected(_logger, _uri);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= _options.ReconnectMaxAttempts)
                {
                    LogConnectFailed(_logger, ex, _uri, _options.ReconnectMaxAttempts);
                    throw new TigerLinkException($"Could not connect to Tiger API at {_uri} after {_options.ReconnectMaxAttempts} attempts", ex);
                }

                int delayMs = Math.Min(_options.ReconnectBaseDelayMs * (1 << (attempt - 1)), _options.ReconnectMaxDelayMs);
                LogRetrying(_logger, attempt, _options.ReconnectMaxAttempts, ex.Message, delayMs);
                await Task.Delay(delayMs, ct);
            }
        }
    }

    public async Task SendAsync(string json, CancellationToken ct = default)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(ct);
        try
        {
            await _ws.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<string?> SendAndReceiveAsync(string json, CancellationToken ct = default)
    {
        await SendAsync(json, ct);
        return await ReceiveOneAsync(ct);
    }

    public async Task<string?> ReceiveOneAsync(CancellationToken ct = default)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        ValueWebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer.AsMemory(), ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                LogConnectionClosed(_logger);
                return null;
            }
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "client shutdown", CancellationToken.None);
            }
            catch
            {
                // best-effort close
            }
        }
        _ws.Dispose();
        _sendLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connecting to {Uri} (attempt {Attempt}/{Max})")]
    private static partial void LogConnecting(ILogger logger, Uri uri, int attempt, int max);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Tiger API at {Uri}")]
    private static partial void LogConnected(ILogger logger, Uri uri);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to connect to {Uri} after {Max} attempts")]
    private static partial void LogConnectFailed(ILogger logger, Exception ex, Uri uri, int max);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempt {Attempt}/{Max} failed ({Error}); retrying in {DelayMs}ms")]
    private static partial void LogRetrying(ILogger logger, int attempt, int max, string error, int delayMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tiger API closed the WebSocket connection")]
    private static partial void LogConnectionClosed(ILogger logger);
}
