namespace ScanBridgeExtention.Dto
{
    public static class GtinHelper
    {
        /// <summary>
        /// Возвращает все возможные варианты написания GTIN (с ведущим нулем и без него).
        /// </summary>
        public static HashSet<string> GetGtinVariations(string? gtin)
        {
            var variations = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(gtin)) return variations;

            // 1. Очищаем от возможных пробелов, дефисов и нецифровых символов
            var cleanGtin = new string(gtin.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(cleanGtin)) return variations;

            // 2. Базовый вариант (как пришел после очистки)
            variations.Add(cleanGtin);

            // 3. Вариант без ведущих нулей
            var noLeadingZeros = cleanGtin.TrimStart('0');
            if (!string.IsNullOrEmpty(noLeadingZeros))
            {
                variations.Add(noLeadingZeros);
            }

            // 4. Вариант с одним ведущим нулем (преобразование EAN-13 в GTIN-14)
            variations.Add("0" + noLeadingZeros);

            return variations;
        }
    }
}
