using System.IO.Ports;
using Serilog;

namespace ScanBridge.Api;

public static class PortEndpoints
{
    public static WebApplication MapPortEndpoints(this WebApplication app)
    {
        app.MapGet("/api/ports", () =>
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                return Results.Ok(ports);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Не удалось получить список COM-портов");
                return Results.Ok(Array.Empty<string>());
            }
        });

        return app;
    }
}
