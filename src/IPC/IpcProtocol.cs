using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScanBridge.IPC;

public enum IpcCommand
{
    Ping,
    SimulatePaste,
    SimulateTyping,
    SetClipboardText,
    SetForegroundWindow,
    GetForegroundWindow,
    FindWindowByTitle,
    ActivateWindow
}

public class IpcRequest
{
    [JsonPropertyName("command")]
    public IpcCommand Command { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("windowTitle")]
    public string WindowTitle { get; set; } = "";
}

public class IpcResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

public static class IpcMessages
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        var lenBuf = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lenBuf, bytes.Length);
        await stream.WriteAsync(lenBuf, ct);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<T?> ReadMessageAsync<T>(Stream stream, CancellationToken ct = default)
    {
        var lenBuf = new byte[4];
        int read = await ReadExactAsync(stream, lenBuf, 0, 4, ct);
        if (read < 4) return default;

        int len = BinaryPrimitives.ReadInt32BigEndian(lenBuf);
        if (len <= 0 || len > 1024 * 1024) return default;

        var buf = new byte[len];
        read = await ReadExactAsync(stream, buf, 0, len, ct);
        if (read < len) return default;

        var json = Encoding.UTF8.GetString(buf, 0, read);
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), ct);
            if (n == 0) return totalRead;
            totalRead += n;
        }
        return totalRead;
    }
}
