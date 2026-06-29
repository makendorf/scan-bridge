# ScanBridge

Barcode and QR code scanner management system for industrial use.

## Overview

ScanBridge is a server application for receiving data from barcode and QR code scanners connected via COM ports (through COM-to-Ethernet converters). The application processes data and executes configurable actions: logging, character replacement, clipboard paste, and export to files/FTP/SFTP/HTTP API.

## Features

- **Multiple scanners** — parallel operation with unlimited number of scanners
- **Action groups** — post-scan actions organized in groups with scanner bindings
- **Auto-reconnection** — automatic recovery on COM port connection loss
- **Format detection** — recognition of UUID, EAN-8/13, UPC-A, GTIN-14, Code128, GS1-128
- **QR content** — detection of QR code content types (URL, JSON, WiFi, vCard)
- **Flexible actions** — configurable post-scan processing pipeline
- **Web interface** — manage scanners and actions through browser
- **Dashboard** — real-time analytics: KPI, activity charts, format distribution, scanner status
- **Hub** — central hub for monitoring multiple ScanBridge instances (separate repo: [scanbridge-hub](https://github.com/makendorf/scanbridge-hub))

## Tech Stack

- .NET 10 / ASP.NET Core
- SQLite + Entity Framework Core
- Serilog (database logging)
- FluentFTP (FTP client)
- SSH.NET (SFTP client)
- System.IO.Ports (COM ports)

## Project Structure

```
ScanBridge/
├── src/
│   ├── Api/                    # Extension methods for API endpoints
│   │   ├── DashboardEndpoints.cs
│   │   ├── ScannerEndpoints.cs
│   │   ├── LogEndpoints.cs
│   │   ├── PortEndpoints.cs
│   │   ├── PostScanEndpoints.cs
│   │   └── SettingsEndpoints.cs
│   ├── Data/                   # EF Core DbContext
│   │   ├── AppDbContext.cs
│   │   └── Entities/
│   │       ├── AppSetting.cs
│   │       ├── LogRecord.cs
│   │       ├── PostScanAction.cs
│   │       ├── ReconnectEvent.cs
│   │       ├── ScanHistory.cs
│   │       └── ScannerConfig.cs
│   ├── Models/                 # Data models
│   │   ├── PostScanActionConfig.cs
│   │   ├── ScanResult.cs
│   │   └── SerialPortConfig.cs
│   ├── Parsers/                # Barcode parsers
│   │   ├── IBarcodeParser.cs
│   │   ├── QRContentDetector.cs
│   │   └── SimpleBarcodeParser.cs
│   ├── Services/               # Business logic
│   │   ├── ScanHistoryService.cs
│   │   ├── ScannerManager.cs
│   │   ├── ScanProcessorService.cs
│   │   ├── SerialPortService.cs
│   │   └── PostScanActions/
│   ├── wwwroot/                # Web interface + dashboard
│   │   ├── css/dashboard.css
│   │   ├── js/dashboard.js
│   │   └── index.html
│   └── Program.cs
├── tests/                      # Tests (xUnit + Moq)
├── wiki/                       # Documentation
└── ScanBridge.slnx
```

## Post-Scan Actions

| Action | Description |
|--------|-------------|
| **Log** | Write scan data to database |
| **Replacement** | Substitute substrings in data (Find → Replace, mode: All / Start / End) |
| **Clipboard Paste** | Paste data into active window via Ctrl+V (WinAPI) |
| **Window Paste** | Paste data into selected window by title (WinAPI) |
| **Export** | Export to JSON/XML with configurable tags |
| **Validation** | Validate data (regex, dictionary, numeric range, format) |
| **DataEnrichment** | Enrich data via HTTP request |
| **Aggregation** | Aggregate scans with periodic output |
| **DatabaseQuery** | Query external database |
| **Telegram** | Telegram notifications |
| **Email** | Email notifications |

### Export Destinations

- **Local folder** — save files to disk (including UNC paths)
- **FTP** — upload to FTP server (active/passive mode)
- **SFTP** — upload to SSH server
- **HTTP POST** — send POST request to API (with headers and Content-Type)

## Configuration

Settings are stored in SQLite database `scanbridge.db`. Managed through web interface:

- **Scanners** — add, edit, restart scanners
- **Actions** — configure processing pipeline for each scanner
- **Logs** — view entries with level filtering

## Running

```bash
cd src
dotnet run
```

Web interface: `http://localhost:5000`

## COM Port Configuration

Example scanner configuration via web interface:

| Parameter | Value |
|-----------|-------|
| Name | Main |
| Port | COM2 |
| Baud Rate | 9600 |
| Data Bits | 8 |
| Parity | None |
| Stop Bits | One |
| Flow Control | RequestToSend |

## Architecture

```
COM Port → SerialPortService → SimpleBarcodeParser → ScanProcessorService
    → ScanTracker + ScanHistoryService → PostScanManager → [actions...]
```

- Each scanner runs in a separate `BackgroundService`
- Parser detects barcode format and QR content
- Actions execute sequentially, order is configurable
- Data changes in one action are available to subsequent ones

## Testing

```bash
dotnet test
```

Project contains 192 tests: parsers, services, post-scan actions.

## License

MIT
