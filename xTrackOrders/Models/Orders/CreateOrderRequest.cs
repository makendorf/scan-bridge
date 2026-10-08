using System.Text.Json.Serialization;

namespace xTrackOrders.Models.Order;

/// <summary>
/// Запрос на создание заказа на производство
/// </summary>
public class CreateOrderRequest
{
    /// <summary>
    /// Тип операции (обязательный)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// GTIN продукции (обязательный)
    /// </summary>
    [JsonPropertyName("gtin")]
    public string Gtin { get; set; } = string.Empty;

    /// <summary>
    /// SKU номенклатуры (обязательный если включены характеристики)
    /// </summary>
    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    /// <summary>
    /// Планируемое количество продукции
    /// </summary>
    [JsonPropertyName("count")]
    public string? Count { get; set; }

    /// <summary>
    /// Идентификатор производственной линии
    /// </summary>
    [JsonPropertyName("lineId")]
    public string? LineId { get; set; }

    /// <summary>
    /// Режим создания заданий на оборудование
    /// Допустимые значения: printTask, scanTasks, printAndScanTasks
    /// </summary>
    [JsonPropertyName("equipmentTaskCreationMode")]
    public string? EquipmentTaskCreationMode { get; set; }

    /// <summary>
    /// Сведения об организации (обязательный)
    /// </summary>
    [JsonPropertyName("organisation")]
    public Organisation Organisation { get; set; } = new();

    /// <summary>
    /// Производственная серия (обязательный)
    /// </summary>
    [JsonPropertyName("productSeries")]
    public ProductSeries ProductSeries { get; set; } = new();

    /// <summary>
    /// Дополнительные параметры заказа
    /// </summary>
    [JsonPropertyName("AdditionalFields")]
    public AdditionalFields? AdditionalFields { get; set; }

    /// <summary>
    /// Объем заказа в кг
    /// </summary>
    [JsonPropertyName("OrderVolumeInKg")]
    public decimal? OrderVolumeInKg { get; set; }
}

/// <summary>
/// Сведения об организации
/// </summary>
public class Organisation
{
    /// <summary>
    /// ИНН организации (обязательный)
    /// </summary>
    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    /// <summary>
    /// КПП организации (обязательный)
    /// </summary>
    [JsonPropertyName("kpp")]
    public string Kpp { get; set; } = string.Empty;
}

/// <summary>
/// Производственная серия
/// </summary>
public class ProductSeries
{
    /// <summary>
    /// Номер серии (обязательный)
    /// </summary>
    [JsonPropertyName("lotNumber")]
    public string LotNumber { get; set; } = string.Empty;

    /// <summary>
    /// Дата производства (обязательный, формат: dd.MM.yyyy)
    /// </summary>
    [JsonPropertyName("productDate")]
    public string ProductDate { get; set; } = string.Empty;

    /// <summary>
    /// Срок годности (обязательный, формат: dd.MM.yyyy)
    /// </summary>
    [JsonPropertyName("expDate")]
    public string ExpDate { get; set; } = string.Empty;
}

/// <summary>
/// Дополнительные параметры заказа
/// </summary>
public class AdditionalFields
{
    // Строковые поля
    [JsonPropertyName("textField1")]
    public string? TextField1 { get; set; }

    [JsonPropertyName("textField2")]
    public string? TextField2 { get; set; }

    [JsonPropertyName("textField3")]
    public string? TextField3 { get; set; }

    [JsonPropertyName("textField4")]
    public string? TextField4 { get; set; }

    [JsonPropertyName("textField5")]
    public string? TextField5 { get; set; }

    [JsonPropertyName("DisplayText")]
    public string? DisplayText { get; set; }

    [JsonPropertyName("LineNumber1")]
    public string? LineNumber1 { get; set; }

    [JsonPropertyName("LineNumber2")]
    public string? LineNumber2 { get; set; }

    // Числовые поля
    [JsonPropertyName("NumericField1")]
    public decimal? NumericField1 { get; set; }

    [JsonPropertyName("NumericField2")]
    public decimal? NumericField2 { get; set; }

    [JsonPropertyName("NumericField3")]
    public decimal? NumericField3 { get; set; }

    [JsonPropertyName("NumericField4")]
    public decimal? NumericField4 { get; set; }

    [JsonPropertyName("NumericField5")]
    public decimal? NumericField5 { get; set; }

    [JsonPropertyName("CustomerNumber")]
    public decimal? CustomerNumber { get; set; }

    [JsonPropertyName("LabelTemplateNumber")]
    public decimal? LabelTemplateNumber { get; set; }

    [JsonPropertyName("QuantumBox")]
    public decimal? QuantumBox { get; set; }

    [JsonPropertyName("BoxMeasurement")]
    public decimal? BoxMeasurement { get; set; }

    [JsonPropertyName("QuantumPallet")]
    public decimal? QuantumPallet { get; set; }

    [JsonPropertyName("PalletMeasurement")]
    public decimal? PalletMeasurement { get; set; }
}