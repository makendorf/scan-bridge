using ScanBridge.Models;

namespace ScanBridge.Parsers;

public interface IBarcodeParser
{
    ScanResult Parse(string rawData);
}
