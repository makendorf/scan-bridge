using System.IO.Pipes;
using ScanBridge.IPC;

namespace ScanBridgeTray;

public class PipeServer
{
    private const string PipeName = "ScanBridgeIPC";
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenTask = ListenLoop(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                await HandleConnection(server, cts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch { }
        }
    }

    private async Task HandleConnection(PipeStream stream, CancellationToken ct)
    {
        var request = await IpcMessages.ReadMessageAsync<IpcRequest>(stream, ct);
        if (request == null) return;

        var response = ExecuteCommand(request);
        await IpcMessages.WriteMessageAsync(stream, response, ct);
    }

    private IpcResponse ExecuteCommand(IpcRequest request)
    {
        try
        {
            return request.Command switch
            {
                IpcCommand.Ping => new IpcResponse { Success = true, Data = "pong" },
                IpcCommand.SimulatePaste => ExecuteSimulatePaste(),
                IpcCommand.SimulateTyping => ExecuteSimulateTyping(request.Data),
                IpcCommand.SetClipboardText => ExecuteSetClipboardText(request.Data),
                IpcCommand.SetForegroundWindow => ExecuteSetForegroundWindow(request.Data),
                IpcCommand.GetForegroundWindow => ExecuteGetForegroundWindow(),
                IpcCommand.FindWindowByTitle => ExecuteFindWindowByTitle(request.WindowTitle),
                IpcCommand.ActivateWindow => ExecuteActivateWindow(request.Data),
                _ => new IpcResponse { Success = false, Error = $"Неизвестная команда: {request.Command}" }
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse { Success = false, Error = ex.Message };
        }
    }

    private IpcResponse ExecuteSimulatePaste()
    {
        Win32InputHelper.SimulatePaste();
        return new IpcResponse { Success = true };
    }

    private IpcResponse ExecuteSimulateTyping(string text)
    {
        Win32InputHelper.SimulateTyping(text);
        return new IpcResponse { Success = true };
    }

    private IpcResponse ExecuteSetClipboardText(string text)
    {
        Win32InputHelper.SetClipboardText(text);
        return new IpcResponse { Success = true };
    }

    private IpcResponse ExecuteSetForegroundWindow(string hwndStr)
    {
        var hwnd = long.Parse(hwndStr);
        Win32InputHelper.SetForegroundWindow(new IntPtr(hwnd));
        return new IpcResponse { Success = true };
    }

    private IpcResponse ExecuteGetForegroundWindow()
    {
        var hwnd = Win32InputHelper.GetForegroundWindow();
        return new IpcResponse { Success = true, Data = hwnd.ToString() };
    }

    private IpcResponse ExecuteFindWindowByTitle(string title)
    {
        var hwnd = Win32InputHelper.FindWindowByTitle(title);
        return new IpcResponse { Success = true, Data = hwnd.ToString() };
    }

    private IpcResponse ExecuteActivateWindow(string hwndStr)
    {
        var hwnd = long.Parse(hwndStr);
        Win32InputHelper.ActivateWindow(new IntPtr(hwnd));
        return new IpcResponse { Success = true };
    }
}
