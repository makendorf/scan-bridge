using System.Diagnostics;

namespace ScanBridgeTray;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly PipeServer _pipeServer;

    public TrayApplicationContext()
    {
        _pipeServer = new PipeServer();
        _pipeServer.Start();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "1.ico");
        var icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

        _trayIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "ScanBridge",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть Web UI", null, (_, _) => OpenWebUI());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Exit());
        return menu;
    }

    private static void OpenWebUI()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:5000",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void Exit()
    {
        _pipeServer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pipeServer.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
