using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using xTrackOrders.Models.Order;

namespace xTrackOrders.Services;

/// <summary>
/// Сервис для работы с API заказов xTrack
/// </summary>
public class OrderApiService : IOrderApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;

    /// <summary>
    /// Конструктор сервиса
    /// </summary>
    /// <param name="baseUrl">Базовый URL сервера (например: http://server-name:port)</param>
    /// <param name="username">Логин пользователя xTrack</param>
    /// <param name="password">Пароль пользователя xTrack</param>
    public OrderApiService(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _username = username;
        _password = password;

        _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Настройка Basic Authentication
        var authBytes = Encoding.ASCII.GetBytes($"{_username}:{_password}");
        var base64Auth = Convert.ToBase64String(authBytes);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);

        // Указываем, что отправляем и принимаем JSON
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc/>
    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Формируем URL
            var url = $"{_baseUrl}/hs/api/v1/order/create";

            // Сериализуем запрос в JSON
            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Отправляем POST-запрос
            var response = await _httpClient.PostAsync(url, httpContent, cancellationToken);

            // Читаем ответ
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Если сервер вернул HTTP 400 (Bad Request)
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return new CreateOrderResponse
                {
                    Error = $"Ошибка сервера: {responseContent}"
                };
            }

            // Если другая ошибка HTTP
            if (!response.IsSuccessStatusCode)
            {
                return new CreateOrderResponse
                {
                    Error = $"HTTP ошибка: {response.StatusCode}. {responseContent}"
                };
            }

            // Десериализуем успешный ответ
            var result = JsonSerializer.Deserialize<CreateOrderResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return result ?? new CreateOrderResponse { Error = "Пустой ответ от сервера" };
        }
        catch (TaskCanceledException)
        {
            return new CreateOrderResponse { Error = "Превышено время ожидания запроса" };
        }
        catch (HttpRequestException ex)
        {
            return new CreateOrderResponse { Error = $"Ошибка сети: {ex.Message}" };
        }
        catch (JsonException ex)
        {
            return new CreateOrderResponse { Error = $"Ошибка обработки JSON: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new CreateOrderResponse { Error = $"Неизвестная ошибка: {ex.Message}" };
        }
    }
}