using xTrackOrders.Models.Order;

namespace xTrackOrders.Services;

/// <summary>
/// Интерфейс для работы с API заказов
/// </summary>
public interface IOrderApiService
{
    /// <summary>
    /// Создает заказ на производство
    /// </summary>
    /// <param name="request">Данные заказа</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат создания заказа</returns>
    Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
}