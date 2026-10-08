namespace ScanBridgeExtention.Dto
{
    /// <summary>
    /// Информация о блокировке карточки товара администратором.
    /// </summary>
    public class LockInfoDto
    {
        /// <summary>
        /// Дата и время наложения блокировки (в формате ISO 8601).
        /// </summary>
        /// <example>2023-10-25T14:30:00Z</example>
        public string LockedAt { get; set; } = string.Empty;

        /// <summary>
        /// Имя пользователя или идентификатор администратора, выполнившего блокировку.
        /// </summary>
        /// <example>admin_ivanov</example>
        public string LockedBy { get; set; } = string.Empty;
    }

}
