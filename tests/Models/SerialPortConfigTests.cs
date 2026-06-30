using System.Text.Json;
using ScanBridge.Models;

namespace ScanBridge.Tests.Models;

public class SerialPortConfigTests
{
    [Fact]
    public void DefaultValues_PortNameIsCOM2()
    {
        var config = new SerialPortConfig();
        Assert.Equal("COM2", config.PortName);
    }

    [Fact]
    public void DefaultValues_BaudRateIs9600()
    {
        var config = new SerialPortConfig();
        Assert.Equal(9600, config.BaudRate);
    }

    [Fact]
    public void DefaultValues_DataBitsIs8()
    {
        var config = new SerialPortConfig();
        Assert.Equal(8, config.DataBits);
    }

    [Fact]
    public void DefaultValues_ParityIsNone()
    {
        var config = new SerialPortConfig();
        Assert.Equal("None", config.Parity);
    }

    [Fact]
    public void DefaultValues_StopBitsIsOne()
    {
        var config = new SerialPortConfig();
        Assert.Equal("One", config.StopBits);
    }

    [Fact]
    public void DefaultValues_HandshakeIsRequestToSend()
    {
        var config = new SerialPortConfig();
        Assert.Equal("RequestToSend", config.Handshake);
    }

    [Fact]
    public void DefaultValues_ReadTimeoutIs5000()
    {
        var config = new SerialPortConfig();
        Assert.Equal(5000, config.ReadTimeout);
    }

    [Fact]
    public void DefaultValues_WriteTimeoutIs5000()
    {
        var config = new SerialPortConfig();
        Assert.Equal(5000, config.WriteTimeout);
    }

    [Fact]
    public void DefaultValues_NameIsEmpty()
    {
        var config = new SerialPortConfig();
        Assert.Equal(string.Empty, config.Name);
    }

    [Fact]
    public void Serialization_CamelCase()
    {
        var config = new SerialPortConfig
        {
            Name = "Test",
            PortName = "COM3",
            BaudRate = 19200
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(config, options);

        Assert.Contains("\"name\"", json);
        Assert.Contains("\"portName\"", json);
        Assert.Contains("\"baudRate\"", json);
        Assert.Contains("COM3", json);
    }

    [Fact]
    public void Deserialization_FromCamelCase()
    {
        var json = """
        {
            "name": "Main",
            "portName": "COM5",
            "baudRate": 38400,
            "dataBits": 7,
            "parity": "Even",
            "stopBits": "Two",
            "handshake": "None",
            "readTimeout": 1000,
            "writeTimeout": 2000
        }
        """;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var config = JsonSerializer.Deserialize<SerialPortConfig>(json, options)!;

        Assert.Equal("Main", config.Name);
        Assert.Equal("COM5", config.PortName);
        Assert.Equal(38400, config.BaudRate);
        Assert.Equal(7, config.DataBits);
        Assert.Equal("Even", config.Parity);
        Assert.Equal("Two", config.StopBits);
        Assert.Equal("None", config.Handshake);
        Assert.Equal(1000, config.ReadTimeout);
        Assert.Equal(2000, config.WriteTimeout);
    }

    [Fact]
    public void Deserialization_RoundTrip()
    {
        var original = new SerialPortConfig
        {
            Name = "Scanner1",
            PortName = "COM10",
            BaudRate = 115200,
            DataBits = 8,
            Parity = "Mark",
            StopBits = "OnePointFive",
            Handshake = "RequestToSendXOnXOff",
            ReadTimeout = 3000,
            WriteTimeout = 4000
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<SerialPortConfig>(json, options)!;

        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.PortName, deserialized.PortName);
        Assert.Equal(original.BaudRate, deserialized.BaudRate);
        Assert.Equal(original.DataBits, deserialized.DataBits);
        Assert.Equal(original.Parity, deserialized.Parity);
        Assert.Equal(original.StopBits, deserialized.StopBits);
        Assert.Equal(original.Handshake, deserialized.Handshake);
        Assert.Equal(original.ReadTimeout, deserialized.ReadTimeout);
        Assert.Equal(original.WriteTimeout, deserialized.WriteTimeout);
    }
}
