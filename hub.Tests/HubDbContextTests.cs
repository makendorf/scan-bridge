using Microsoft.EntityFrameworkCore;
using ScanBridgeHub.Data;
using ScanBridgeHub.Models;

namespace ScanBridgeHub.Tests;

public class HubDbContextTests : IDisposable
{
    private readonly string _dbPath;
    private readonly HubDbContext _db;

    public HubDbContextTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hub_test_{Guid.NewGuid():N}.db");
        _db = new HubDbContext($"Data Source={_dbPath}");
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void Instances_CanAddAndRetrieve()
    {
        var instance = new RemoteInstance
        {
            Name = "Test Server",
            Host = "192.168.1.10",
            Port = 5000,
            Enabled = true,
            SortOrder = 0
        };
        _db.Instances.Add(instance);
        _db.SaveChanges();

        var result = _db.Instances.First();
        Assert.Equal("Test Server", result.Name);
        Assert.Equal("192.168.1.10", result.Host);
        Assert.Equal(5000, result.Port);
        Assert.True(result.Enabled);
    }

    [Fact]
    public void Instances_CanUpdate()
    {
        var instance = new RemoteInstance { Name = "Old", Host = "1.1.1.1", Port = 5000 };
        _db.Instances.Add(instance);
        _db.SaveChanges();

        instance.Name = "New";
        instance.Host = "2.2.2.2";
        _db.SaveChanges();

        var result = _db.Instances.First();
        Assert.Equal("New", result.Name);
        Assert.Equal("2.2.2.2", result.Host);
    }

    [Fact]
    public void Instances_CanDelete()
    {
        _db.Instances.Add(new RemoteInstance { Name = "ToDelete", Host = "1.1.1.1", Port = 5000 });
        _db.SaveChanges();
        Assert.Single(_db.Instances);

        _db.Instances.Remove(_db.Instances.First());
        _db.SaveChanges();
        Assert.Empty(_db.Instances);
    }

    [Fact]
    public void Instances_Ordering()
    {
        _db.Instances.Add(new RemoteInstance { Name = "B", Host = "1.1.1.1", Port = 5000, SortOrder = 2 });
        _db.Instances.Add(new RemoteInstance { Name = "A", Host = "2.2.2.2", Port = 5001, SortOrder = 1 });
        _db.Instances.Add(new RemoteInstance { Name = "C", Host = "3.3.3.3", Port = 5002, SortOrder = 3 });
        _db.SaveChanges();

        var ordered = _db.Instances.OrderBy(i => i.SortOrder).ToList();
        Assert.Equal("A", ordered[0].Name);
        Assert.Equal("B", ordered[1].Name);
        Assert.Equal("C", ordered[2].Name);
    }

    [Fact]
    public void Instances_DefaultEnabled()
    {
        var instance = new RemoteInstance { Name = "Test", Host = "1.1.1.1", Port = 5000 };
        _db.Instances.Add(instance);
        _db.SaveChanges();

        Assert.True(_db.Instances.First().Enabled);
    }

    [Fact]
    public void Instances_DefaultSortOrder()
    {
        var a = new RemoteInstance { Name = "A", Host = "1.1.1.1", Port = 5000, SortOrder = 5 };
        var b = new RemoteInstance { Name = "B", Host = "2.2.2.2", Port = 5001, SortOrder = 1 };
        _db.Instances.Add(a);
        _db.Instances.Add(b);
        _db.SaveChanges();

        var first = _db.Instances.OrderBy(i => i.SortOrder).First();
        Assert.Equal("B", first.Name);
    }
}
