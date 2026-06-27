using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScanBridge.Models;
using ScanBridge.Parsers;
using ScanBridge.Services;

namespace ScanBridge.Tests.Services;

public class ScannerManagerTests : IDisposable
{
    private readonly Mock<ILogger<ScannerManager>> _loggerMock = new();
    private readonly Mock<ILogger<SerialPortService>> _portLoggerMock = new();
    private readonly Mock<ILogger<ScanProcessorService>> _processorLoggerMock = new();
    private readonly Mock<IBarcodeParser> _parserMock = new();
    private readonly Mock<PostScanManager> _postScanMock;
    private readonly Mock<ScanProcessorService> _processorMock;
    private readonly ServiceCollection _services;
    private readonly ScannerManager _manager;

    public ScannerManagerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        _postScanMock = new Mock<PostScanManager>(
            provider,
            new Mock<ILogger<PostScanManager>>().Object);
        _processorMock = new Mock<ScanProcessorService>(_postScanMock.Object, new ScanTracker(), _processorLoggerMock.Object);
        _services = new ServiceCollection();
        _services.AddSingleton(_portLoggerMock.Object);
        _services.AddSingleton(_processorMock.Object);
        _manager = new ScannerManager(
            _services.BuildServiceProvider(),
            _loggerMock.Object,
            _parserMock.Object,
            _processorMock.Object);
    }

    public void Dispose() => _manager.Dispose();

    private static SerialPortConfig CreateConfig(string name = "Test", string port = "COM99") => new()
    {
        Name = name,
        PortName = port,
        BaudRate = 9600,
        DataBits = 8,
        Parity = "None",
        StopBits = "One",
        Handshake = "RequestToSend",
        ReadTimeout = 5000,
        WriteTimeout = 5000
    };

    [Fact]
    public void GetRunning_InitiallyEmpty()
    {
        var running = _manager.GetRunning();
        Assert.Empty(running);
    }

    [Fact]
    public void CheckPortConflict_NoScanners_ReturnsNull()
    {
        var result = _manager.CheckPortConflict("COM1", "AnyName");
        Assert.Null(result);
    }

    [Fact]
    public void CheckPortConflict_SameName_ReturnsNull()
    {
        var config = CreateConfig("Scanner1", "COM5");
        _manager.StartScanner(config);

        var result = _manager.CheckPortConflict("COM5", "Scanner1");
        Assert.Null(result);
    }

    [Fact]
    public void CheckPortConflict_DifferentName_ReturnsConflict()
    {
        _manager.StartScanner(CreateConfig("Scanner1", "COM5"));

        var result = _manager.CheckPortConflict("COM5", "Scanner2");
        Assert.Equal("Scanner1", result);
    }

    [Fact]
    public void CheckPortConflict_DifferentPort_ReturnsNull()
    {
        _manager.StartScanner(CreateConfig("Scanner1", "COM5"));

        var result = _manager.CheckPortConflict("COM6", "Scanner2");
        Assert.Null(result);
    }

    [Fact]
    public void CheckPortConflict_CaseInsensitivePort()
    {
        _manager.StartScanner(CreateConfig("Scanner1", "COM5"));

        var result = _manager.CheckPortConflict("com5", "Scanner2");
        Assert.Equal("Scanner1", result);
    }

    [Fact]
    public void StopScanner_Nonexistent_DoesNotThrow()
    {
        _manager.StopScanner("Nonexistent");
    }

    [Fact]
    public void StopAll_NoScanners_DoesNotThrow()
    {
        _manager.StopAll();
    }

    [Fact]
    public void RestartScanner_DoesNotThrow()
    {
        var config = CreateConfig("Test", "COM98");
        _manager.RestartScanner(config);
    }

    [Fact]
    public void RestartAll_EmptyList_DoesNotThrow()
    {
        _manager.RestartAll([]);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _manager.Dispose();
        _manager.Dispose();
    }
}
