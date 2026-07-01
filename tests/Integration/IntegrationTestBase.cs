using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScanBridge.Data;

namespace ScanBridge.Tests.Integration;

public abstract class IntegrationTestBase : IDisposable
{
    protected AppDbContext DbContext { get; private set; }
    protected IServiceProvider ServiceProvider { get; private set; }
    private readonly ServiceProvider _rootProvider;

    protected IntegrationTestBase()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        _rootProvider = services.BuildServiceProvider();
        ServiceProvider = _rootProvider;

        using var scope = _rootProvider.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        DbContext?.Dispose();
        _rootProvider?.Dispose();
    }
}
