using System.Data.Common;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие запроса к базе данных.
/// Выполняет SQL-запрос с плейсхолдером {data} и извлекает результат.
/// </summary>
public class DatabaseQueryAction : IPostScanAction
{
    public string Type => "DatabaseQuery";

    private readonly ILogger<DatabaseQueryAction> _logger;
    private readonly DbProviderFactory? _factory;
    private readonly string _connectionString;
    private readonly string _queryTemplate;
    private readonly string _resultField;
    private readonly int _timeoutSeconds;

    public DatabaseQueryAction(ILogger<DatabaseQueryAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        var connectionType = (settings.TryGetValue("ConnectionType", out var ct) ? ct : "mysql").ToLowerInvariant();
        _connectionString = settings.TryGetValue("ConnectionString", out var cs) ? cs : "";
        _queryTemplate = settings.TryGetValue("QueryTemplate", out var qt) ? qt : "";
        _resultField = settings.TryGetValue("ResultField", out var rf) ? rf : "";
        _timeoutSeconds = settings.TryGetValue("TimeoutSeconds", out var tsStr)
            && int.TryParse(tsStr, out var ts) ? ts : 30;

        _factory = connectionType switch
        {
            "mysql" => GetFactory("MySqlConnector.MySqlConnectorFactory, MySqlConnector"),
            "postgresql" => GetFactory("Npgsql.NpgsqlFactory, Npgsql"),
            "mssql" => GetFactory("Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient"),
            _ => null
        };

        if (_factory == null)
            _logger.LogWarning("DatabaseQuery: неподдерживаемый тип БД '{Type}'", connectionType);
        if (string.IsNullOrWhiteSpace(_connectionString))
            _logger.LogWarning("DatabaseQuery: строка подключения не задана");
        if (string.IsNullOrWhiteSpace(_queryTemplate))
            _logger.LogWarning("DatabaseQuery: шаблон запроса не задан");
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (_factory == null || string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(_queryTemplate))
            return;

        try
        {
            using var connection = _factory.CreateConnection()!;
            connection.ConnectionString = _connectionString;
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = _queryTemplate.Replace("{data}", scan.ParsedData);
            cmd.CommandTimeout = _timeoutSeconds;

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var value = string.IsNullOrEmpty(_resultField)
                    ? reader[0]?.ToString() ?? ""
                    : reader[_resultField]?.ToString() ?? "";
                scan.ParsedData = value;
                _logger.LogInformation("DatabaseQuery: получен результат, длина={Length}", value.Length);
            }
            else
            {
                _logger.LogWarning("DatabaseQuery: запрос не вернул результатов");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DatabaseQuery: ошибка выполнения запроса");
        }
    }

    private static DbProviderFactory? GetFactory(string assemblyQualifiedName)
    {
        try
        {
            var resolvedType = System.Type.GetType(assemblyQualifiedName);
            if (resolvedType == null) return null;

            var field = resolvedType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return field?.GetValue(null) as DbProviderFactory;
        }
        catch
        {
            return null;
        }
    }
}
