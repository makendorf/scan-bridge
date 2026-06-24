namespace ScanBridge.Data.Entities;

public class ScannerConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PortName { get; set; } = "COM2";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "RequestToSend";
    public int ReadTimeout { get; set; } = 5000;
    public int WriteTimeout { get; set; } = 5000;
    public int SortOrder { get; set; }
}
