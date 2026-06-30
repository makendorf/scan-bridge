using ScanBridge.Services.PostScanActions;

namespace ScanBridge.Services;

public interface IPostScanActionFactory
{
    IPostScanAction? Create(string type, Dictionary<string, string> settings);
}
