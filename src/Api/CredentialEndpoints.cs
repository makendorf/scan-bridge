using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;

namespace ScanBridge.Api;

/// <summary>
/// API endpoints для управления учётными данными.
/// </summary>
public static class CredentialEndpoints
{
    public static void MapCredentialEndpoints(this WebApplication app)
    {
        app.MapGet("/api/credentials", (AppDbContext db) =>
        {
            var credentials = db.Credentials
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Type,
                    c.Domain,
                    c.Username,
                    c.Host,
                    c.Port,
                    c.PassiveMode,
                    c.CreatedAt
                })
                .ToList();
            return Results.Ok(credentials);
        });

        app.MapGet("/api/credentials/{id}", (int id, AppDbContext db) =>
        {
            var credential = db.Credentials.Find(id);
            return credential != null ? Results.Ok(credential) : Results.NotFound();
        });

        app.MapPost("/api/credentials", (CredentialConfig credential, AppDbContext db) =>
        {
            credential.CreatedAt = DateTime.UtcNow;
            db.Credentials.Add(credential);
            db.SaveChanges();
            return Results.Created($"/api/credentials/{credential.Id}", credential);
        });

        app.MapPut("/api/credentials/{id}", (int id, CredentialConfig input, AppDbContext db) =>
        {
            var credential = db.Credentials.Find(id);
            if (credential == null) return Results.NotFound();

            credential.Name = input.Name;
            credential.Type = input.Type;
            credential.Domain = input.Domain;
            credential.Username = input.Username;
            credential.Password = input.Password;
            credential.Host = input.Host;
            credential.Port = input.Port;
            credential.PassiveMode = input.PassiveMode;

            db.SaveChanges();
            return Results.Ok(credential);
        });

        app.MapDelete("/api/credentials/{id}", (int id, AppDbContext db) =>
        {
            var credential = db.Credentials.Find(id);
            if (credential == null) return Results.NotFound();

            db.Credentials.Remove(credential);
            db.SaveChanges();
            return Results.Ok();
        });
    }
}
