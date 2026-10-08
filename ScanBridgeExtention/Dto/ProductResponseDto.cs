using ScanBridgeExtention.Models;

namespace ScanBridgeExtention.Dto
{
    /// <summary>
    /// Расширенная модель ответа товара. 
    /// Наследует все базовые поля из сущности <see cref="Product"/>, 
    /// и добавляет информацию о пользовательских подтверждениях и административных блокировках.
    /// </summary>
    public class ProductResponseDto : Product
    {
        /// <summary>
        /// Словарь подтверждений от пользователей. 
        /// Ключ: имя пользователя, Значение: дата и время подтверждения.
        /// </summary>
        /// <example>
        /// {
        ///   "user_petrov": "2023-11-01T10:00:00Z",
        ///   "user_sidorov": "2023-11-02T15:30:00Z"
        /// }
        /// </example>
        public Dictionary<string, string> Confirmations { get; set; } = new();

        /// <summary>
        /// Информация о текущей блокировке товара. 
        /// Если товар не заблокирован, возвращает <c>null</c>.
        /// </summary>
        public LockInfoDto? Locked { get; set; }
    }

    /// <summary>
    /// Облегченная модель ответа, возвращающая только GTIN и рассчитанный вес товара.
    /// Используется в высоконагруженных пакетных запросах для экономии трафика.
    /// </summary>
    public class ProductWeigthResponseDto
    {
        /// <summary>
        /// Глобальный номер торговой единицы (исходный или найденный по вариации).
        /// </summary>
        /// <example>04607077972436</example>
        public string? Gtin { get; set; }

        /// <summary>
        /// Рассчитанный вес товара, приведенный к килограммам (кг).
        /// </summary>
        /// <example>1.5</example>
        public double? Weigth { get; set; }
        // Примечание: имя свойства оставлено как Weigth для совместимости с вашим кодом, 
        // но в документации указано, что это вес.
    }
}
