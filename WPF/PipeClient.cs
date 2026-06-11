using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AliceBotSettings;

public class PipeClient : IDisposable
{
    private const int Port = 19876;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    private readonly object _reconnectLock = new();
    private bool _reconnecting;

    public event Action<InitData>? OnInitReceived;
    public event Action<JsonElement>? OnMessageReceived;
    public event Action<bool>? OnConnectionChanged;
    public event Action<string>? OnError;

    public bool IsConnected => _writer != null && !_reconnecting;

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "wpf_tcp.log");

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
        }
        catch { }
    }

    public void StartReconnect()
    {
        lock (_reconnectLock)
        {
            if (_reconnecting || _disposed) return;
            _reconnecting = true;
        }
        OnConnectionChanged?.Invoke(false);
        _ = ReconnectLoopAsync();
    }

    private async Task ReconnectLoopAsync()
    {
        CancelAndRenewCts();
        try
        {
            while (!_cts!.IsCancellationRequested && !_disposed)
            {
                try
                {
                    await TryConnectAsync(_cts.Token);
                    lock (_reconnectLock) { _reconnecting = false; }
                    return;
                }
                catch (OperationCanceledException) { lock (_reconnectLock) { _reconnecting = false; } return; }
                catch (Exception ex)
                {
                    Log("Connect failed: " + ex.GetType().Name + " - " + ex.Message);
                    try { await Task.Delay(2000, _cts.Token); } catch (OperationCanceledException) { break; }
                }
            }
        }
        finally { lock (_reconnectLock) { _reconnecting = false; } }
    }

    private async Task TryConnectAsync(CancellationToken ct)
    {
        Log("Connecting to 127.0.0.1:" + Port + "...");
        CleanupConnection();

        _client = new TcpClient();
        await _client.ConnectAsync("127.0.0.1", Port, ct);
        Log("TCP connected");

        var stream = _client.GetStream();
        stream.ReadTimeout = 30000;
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        Log("Reading init data...");
        var initLine = await _reader.ReadLineAsync(ct);
        if (initLine == null)
        {
            Log("Init data: null");
            throw new IOException("No init data received");
        }
        Log("Init received: " + initLine.Length + " bytes");

        try
        {
            var doc = JsonDocument.Parse(initLine);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type == "init")
            {
                var init = JsonSerializer.Deserialize<InitData>(root.GetProperty("data").GetRawText(), JsonConfig.Options);
                if (init != null)
                {
                    Log("Init parsed ok");
                    OnInitReceived?.Invoke(init);
                }
            }
        }
        catch (JsonException ex) { Log("Init parse error: " + ex.Message); }

        OnConnectionChanged?.Invoke(true);
        Log("Connection complete");

        _ = ReadLoopAsync(ct);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        Log("ReadLoop start");
        try
        {
            while (!ct.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line == null) { Log("ReadLoop: null (disconnected)"); break; }
                try
                {
                    var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();
                    if (type == "init")
                    {
                        var init = JsonSerializer.Deserialize<InitData>(root.GetProperty("data").GetRawText(), JsonConfig.Options);
                        if (init != null) OnInitReceived?.Invoke(init);
                    }
                    else
                    {
                        OnMessageReceived?.Invoke(root);
                    }
                }
                catch (JsonException) { }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException ex) { Log("ReadLoop IO error: " + ex.Message); }
        catch (Exception ex) { Log("ReadLoop error: " + ex.GetType().Name + " - " + ex.Message); }
        finally
        {
            Log("ReadLoop ended");
            CleanupConnection();
            if (!_disposed) StartReconnect();
        }
    }

    public async Task SendCommand(string action, object? data = null)
    {
        if (_writer == null || _reconnecting) return;
        var json = BuildCommandJson(action, data);
        try { await _writer.WriteLineAsync(json); }
        catch (Exception ex)
        {
            Log("SendCommand error: " + ex.Message);
            CleanupConnection();
            StartReconnect();
        }
    }

    private void CleanupConnection()
    {
        try { _client?.Close(); } catch { }
        _writer?.Dispose();
        _reader?.Dispose();
        _client?.Dispose();
        _writer = null;
        _reader = null;
        _client = null;
    }

    private void CancelAndRenewCts()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }

    private static string BuildCommandJson(string action, object? data)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"cmd\",\"action\":\"");
        sb.Append(action);
        sb.Append('"');
        if (data != null)
        {
            var dataJson = JsonSerializer.Serialize(data, JsonConfig.Options);
            if (dataJson.Length > 2)
            {
                sb.Append(',');
                sb.Append(dataJson.AsSpan(1, dataJson.Length - 2));
            }
        }
        sb.Append('}');
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        CleanupConnection();
        _cts?.Dispose();
    }
}
