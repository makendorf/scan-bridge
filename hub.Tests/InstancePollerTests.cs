using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ScanBridgeHub.Data;
using ScanBridgeHub.Models;
using ScanBridgeHub.Services;

namespace ScanBridgeHub.Tests;

public class InstancePollerTests : IDisposable
{
    private readonly InstancePoller _poller;
    private readonly string _dbPath;

    public InstancePollerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hub_test_{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContext<HubDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProvider);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var httpClient = CreateMockHttpClient(new Dictionary<string, string>
        {
            ["/api/scanners/status"] = "[]",
            ["/api/logs"] = "[]",
            ["/api/postscan/actions"] = "{\"configs\":[]}",
            ["/api/scanners/lastscan"] = "{\"time\":\"0001-01-01T00:00:00\",\"scannerName\":\"\"}"
        });

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _poller = new InstancePoller(
            scopeFactoryMock.Object,
            new Mock<ILogger<InstancePoller>>().Object,
            httpClientFactoryMock.Object);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void SetPollInterval_ClampsToMinimum()
    {
        _poller.SetPollInterval(100);
        Assert.Equal(1000, _poller.PollIntervalMs);
    }

    [Fact]
    public void SetPollInterval_ClampsToMaximum()
    {
        _poller.SetPollInterval(100000);
        Assert.Equal(60000, _poller.PollIntervalMs);
    }

    [Fact]
    public void SetPollInterval_AcceptsValidValue()
    {
        _poller.SetPollInterval(5000);
        Assert.Equal(5000, _poller.PollIntervalMs);
    }

    [Fact]
    public void GetCached_UnknownId_ReturnsNull()
    {
        Assert.Null(_poller.GetCached(999));
    }

    [Fact]
    public void GetAllCached_EmptyInitially()
    {
        var all = _poller.GetAllCached();
        Assert.Empty(all);
    }

    [Fact]
    public void CachedInstanceData_DefaultValues()
    {
        var cached = new CachedInstanceData();
        Assert.False(cached.Online);
        Assert.Equal(DateTime.MinValue, cached.LastPoll);
        Assert.Null(cached.Scanners);
        Assert.Null(cached.Logs);
        Assert.Null(cached.Actions);
        Assert.Equal(string.Empty, cached.LastScannerName);
    }

    [Fact]
    public async Task ExecuteAsync_RunsAndStopsGracefully()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await _poller.StartAsync(cts.Token);
        await Task.Delay(500, CancellationToken.None);
        await _poller.StopAsync(CancellationToken.None);
    }

    private static HttpClient CreateMockHttpClient(Dictionary<string, string> responses)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                var body = responses.TryGetValue(path, out var resp) ? resp : "[]";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                };
            });

        return new HttpClient(handlerMock.Object);
    }
}
