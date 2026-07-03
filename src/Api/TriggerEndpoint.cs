using ScanBridge.Services;

namespace ScanBridge.Api;

/// <summary>
/// API endpoint для HTTP триггеров сценариев.
/// </summary>
public static class TriggerEndpoint
{
    public static void MapTriggerEndpoints(this WebApplication app)
    {
        app.MapPost("/api/trigger/{routePath}", async (string routePath, HttpRequest request, TriggerDispatcher dispatcher) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();

            var headers = new Dictionary<string, string>();
            foreach (var h in request.Headers)
            {
                headers[h.Key] = h.Value.ToString();
            }

            await dispatcher.DispatchHttpTriggerAsync(routePath, body, headers, CancellationToken.None);
            return Results.Ok(new { status = "accepted", route = routePath });
        });
    }
}
