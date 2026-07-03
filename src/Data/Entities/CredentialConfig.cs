using System.ComponentModel.DataAnnotations;

namespace ScanBridge.Data.Entities;

/// <summary>
/// Учётные данные для подключения к внешним ресурсам.
/// </summary>
public class CredentialConfig
{
    public int Id { get; set; }

    /// <summary>
    /// Отображаемое имя учётных данных.
    /// </summary>
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Тип: windows / ftp / sftp.
    /// </summary>
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    // Windows (локальные и сетевые пути \\server\share)
    [MaxLength(100)]
    public string Domain { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;

    // FTP/SFTP
    [MaxLength(200)]
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    /// <summary>
    /// Пассивный режим (только FTP).
    /// </summary>
    public bool PassiveMode { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
