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

    public event Action<InitData>? OnInitReceived;
    public event Action<JsonElement>? OnMessageReceived;
    public event Action<bool>? OnConnectionChanged;
    public event Action<string>? OnError;

    public bool IsConnected => _client?.Connected ?? false;

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "wpf_tcp.log");

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
        }
        catch { }
    }

    public async Task ConnectAsync()
    {
        Log("ConnectAsync start");
        _cts = new CancellationTokenSource();
        _client = new TcpClient();

        try
        {
            Log("Connecting to 127.0.0.1:" + Port + "...");
            await _client.ConnectAsync("127.0.0.1", Port, _cts.Token);
            Log("TCP connected");

            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            Log("Reading init data...");
            var initLine = await _reader.ReadLineAsync();
            if (initLine == null)
            {
                Log("Init data: null (disconnected)");
                OnError?.Invoke("未收到配置数据");
                OnConnectionChanged?.Invoke(false);
                return;
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
                        Log("Init parsed ok, mappings=" + init.ExpressionMappings.Count + " anims=" + init.AnimationList.Count);
                        OnInitReceived?.Invoke(init);
                    }
                }
            }
            catch (JsonException ex) { Log("Init parse error: " + ex.Message); }

            OnConnectionChanged?.Invoke(true);
            Log("Connection complete");
            _ = ReadLoopAsync(_cts.Token);
        }
        catch (OperationCanceledException) { Log("Connect cancelled"); }
        catch (Exception ex)
        {
            Log("Connect error: " + ex.GetType().Name + " - " + ex.Message);
            OnError?.Invoke($"连接失败: {ex.Message}");
            OnConnectionChanged?.Invoke(false);
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        Log("ReadLoop start");
        try
        {
            while (!ct.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line == null) { Log("ReadLoop: null line (disconnected)"); break; }
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
            OnConnectionChanged?.Invoke(false);
        }
    }

    public async Task SendCommand(string action, object? data = null)
    {
        if (_writer == null || !IsConnected) return;
        var json = BuildCommandJson(action, data);
        try { await _writer.WriteLineAsync(json); }
        catch { }
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

    public void Disconnect()
    {
        Log("Disconnect");
        _cts?.Cancel();
        try { _client?.Close(); } catch { }
        _writer?.Dispose();
        _reader?.Dispose();
        _client?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _cts?.Dispose();
    }
}
