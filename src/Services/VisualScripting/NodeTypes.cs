namespace ScanBridge.Services.VisualScripting;

/// <summary>
/// Статический реестр доступных типов узлов для визуального редактора.
/// </summary>
public static class NodeTypes
{
    public static readonly Dictionary<string, NodeTypeDefinition> Definitions = new()
    {
        ["Start"] = new("Старт", "Точка входа (legacy)", [], ["output_1"]),
        ["Scanner"] = new("Сканер", "Точка входа данных от сканера", [], ["output_1"]),
        ["Fork"] = new("Ветвление", "Разделение потока на несколько веток", ["input_1"], ["output_1", "output_2", "output_3"]),
        ["Condition"] = new("Условие", "Ветвление по условию", ["input_1"], ["output_1", "output_2"]),
        ["While"] = new("Цикл", "Повторение тела цикла пока условие истинно", ["input_1"], ["output_1", "output_2"]),
        ["End"] = new("Конец", "Точка выхода", ["input_1"], []),

        // Действия
        ["Log"] = new("Логирование", "Запись данных в системный лог", ["input_1"], ["output_1"]),
        ["Replacement"] = new("Замена символов", "Замена или удаление подстрок", ["input_1"], ["output_1"]),
        ["ClipboardPaste"] = new("Вставка в окно", "Вставка данных в активное окно", ["input_1"], ["output_1"]),
        ["WindowPaste"] = new("Вставка в окно по заголовку", "Находит окно и вставляет данные", ["input_1"], ["output_1"]),
        ["Export"] = new("Экспорт", "Сохранение в файл/FTP/SFTP/HTTP", ["input_1"], ["output_1"]),
        ["Telegram"] = new("Telegram", "Уведомление в Telegram", ["input_1"], ["output_1"]),
        ["Email"] = new("Email", "Отправка email-уведомления", ["input_1"], ["output_1"]),
        ["DataEnrichment"] = new("Обогащение данных", "Добавление дополнительных данных", ["input_1"], ["output_1"]),
        ["Validation"] = new("Валидация", "Проверка данных по правилам", ["input_1"], ["output_1"]),
        ["Aggregation"] = new("Агрегация", "Накопление и объединение данных", ["input_1"], ["output_1"]),
        ["DatabaseQuery"] = new("Запрос к БД", "Выполнение запроса к базе данных", ["input_1"], ["output_1"]),
        ["Pause"] = new("Пауза", "Приостановка выполнения", ["input_1"], ["output_1"]),
    };
}

/// <summary>
/// Определение типа узла.
/// </summary>
public record NodeTypeDefinition(
    string Name,
    string Description,
    List<string> Inputs,
    List<string> Outputs
);
