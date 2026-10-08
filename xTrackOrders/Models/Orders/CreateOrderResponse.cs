using System.Text.Json.Serialization;

namespace xTrackOrders.Models.Order;

/// <summary>
/// Ответ от сервера при создании заказа
/// </summary>
public class CreateOrderResponse
{
    /// <summary>
    /// Идентификатор созданного заказа (при успехе)
    /// </summary>
    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }

    /// <summary>
    /// Текст ошибки (при неудаче)
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Проверяет, был ли запрос успешным
    /// </summary>
    public bool IsSuccess => !string.IsNullOrEmpty(OrderId) && string.IsNullOrEmpty(Error);
}