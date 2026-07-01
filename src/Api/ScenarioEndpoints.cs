using ScanBridge.Models;
using ScanBridge.Services;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Api;

/// <summary>
/// API endpoints для управления сценариями.
/// </summary>
public static class ScenarioEndpoints
{
    public static void MapScenarioEndpoints(this WebApplication app)
    {
        app.MapGet("/api/scenarios", (ScenarioService service) =>
        {
            var scenarios = service.GetAll();
            return Results.Ok(scenarios);
        });

        app.MapGet("/api/scenarios/{id}", (int id, ScenarioService service) =>
        {
            var scenario = service.GetById(id);
            return scenario != null ? Results.Ok(scenario) : Results.NotFound();
        });

        app.MapPost("/api/scenarios", (ScenarioConfig config, ScenarioService service, PostScanManager postScanManager, ScenarioExecutor executor) =>
        {
            var validation = service.Validate(config);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new { errors = validation.Errors });
            }

            var id = service.Create(config);
            postScanManager.ReloadScenarios(service, executor);
            return Results.Created($"/api/scenarios/{id}", new { id });
        });

        app.MapPut("/api/scenarios/{id}", (int id, ScenarioConfig config, ScenarioService service, PostScanManager postScanManager, ScenarioExecutor executor) =>
        {
            var validation = service.Validate(config);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new { errors = validation.Errors });
            }

            try
            {
                service.Update(id, config);
                postScanManager.ReloadScenarios(service, executor);
                return Results.Ok();
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapDelete("/api/scenarios/{id}", (int id, ScenarioService service, PostScanManager postScanManager, ScenarioExecutor executor) =>
        {
            try
            {
                service.Delete(id);
                postScanManager.ReloadScenarios(service, executor);
                return Results.Ok();
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/api/scenarios/{id}/validate", (int id, ScenarioService service) =>
        {
            var scenario = service.GetById(id);
            if (scenario == null) return Results.NotFound();

            var validation = service.Validate(scenario);
            return Results.Ok(validation);
        });

        app.MapGet("/api/scenarios/node-types", () =>
        {
            return Results.Ok(NodeTypes.Definitions);
        });
    }
}
