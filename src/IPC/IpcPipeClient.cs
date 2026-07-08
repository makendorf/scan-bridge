using System.IO.Pipes;

namespace ScanBridge.IPC;

public static class IpcPipeClient
{
    private const string PipeName = "ScanBridgeIPC";
    private const int ConnectTimeoutMs = 2000;

    public static async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken ct = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.None);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ConnectTimeoutMs);

            await client.ConnectAsync(ConnectTimeoutMs, cts.Token);

            await IpcMessages.WriteMessageAsync(client, request, cts.Token);
            var response = await IpcMessages.ReadMessageAsync<IpcResponse>(client, cts.Token);

            return response ?? new IpcResponse { Success = false, Error = "Пустой ответ от tray-приложения" };
        }
        catch (Exception ex)
        {
            return new IpcResponse { Success = false, Error = $"Tray-приложение недоступно: {ex.Message}" };
        }
    }

    public static Task<IpcResponse> SimulatePasteAsync(CancellationToken ct = default)
        => SendAsync(new IpcRequest { Command = IpcCommand.SimulatePaste }, ct);

    public static Task<IpcResponse> SimulateTypingAsync(string text, CancellationToken ct = default)
        => SendAsync(new IpcRequest { Command = IpcCommand.SimulateTyping, Data = text }, ct);

    public static Task<IpcResponse> SetClipboardTextAsync(string text, CancellationToken ct = default)
        => SendAsync(new IpcRequest { Command = IpcCommand.SetClipboardText, Data = text }, ct);

    public static Task<IpcResponse> SetForegroundWindowAsync(long hwnd, CancellationToken ct = default)
        => SendAsync(new IpcRequest { Command = IpcCommand.SetForegroundWindow, Data = hwnd.ToString() }, ct);

    public static Task<IpcResponse> FindWindowByTitleAsync(string title, CancellationToken ct = default)
        => SendAsync(new IpcRequest { Command = IpcCommand.FindWindowByTitle, WindowTitle = title }, ct);

    public static Task<IpcResponse> ActivateWindowAsync(long hwnd, CancellationToken ct = default)
        => SendAsync(new IpcRequest { Command = IpcCommand.ActivateWindow, Data = hwnd.ToString() }, ct);

    public static bool IsAvailable()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.None);
            client.Connect(ConnectTimeoutMs);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
