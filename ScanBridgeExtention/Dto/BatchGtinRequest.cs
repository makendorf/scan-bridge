namespace ScanBridgeExtention.Dto
{
    /// <summary>
    /// Запрос для пакетной обработки или поиска данных по списку GTIN.
    /// </summary>
    public class BatchGtinRequest
    {
        /// <summary>
        /// Список глобальных номеров торговой единицы (GTIN) для поиска.
        /// </summary>
        /// <example>["04607077972436", "04607077976199", "04607077976205"]</example>
        public List<string> Gtins { get; set; } = new();
    }
}
