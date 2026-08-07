using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScanBridgeExtention.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductionOrderController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProductionOrderController> _logger;

        public ProductionOrderController(IHttpClientFactory httpClientFactory, ILogger<ProductionOrderController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            _logger.LogInformation("Получен запрос на создание заказа. DeviceName: {DeviceName}, LineId: {LineId}", request?.DeviceName, request?.LineId);

            if (request == null || string.IsNullOrWhiteSpace(request.DeviceName) || string.IsNullOrWhiteSpace(request.LineId))
            {
                _logger.LogWarning("Запрос отклонен: не переданы обязательные параметры DeviceName или LineId.");
                return BadRequest("Необходимо передать DeviceName и LineId в теле запроса.");
            }

            // --- Жестко забитые значения из примера 1С ---
            var organisation = new { inn = "3127007309", kpp = "312701001" };
            string type = "serialisationaggregation";
            string equipmentTaskCreationMode = "printTaskCapture";
            string count = "1";

            // --- Значения, которые я придумал самостоятельно ---
            string gtin = "04607077976199";
            string sku = "998";
            double orderVolumeInKg = 1;

            var productSeries = new
            {
                lotNumber = "API",
                productDate = "07.08.2026",
                expDate = "07.08.2222"
            };

            var requestBody = new
            {
                organisation,
                equipmentTaskCreationMode,
                gtin,
                productSeries,
                count,
                type,
                sku,
                OrderVolumeInKg = orderVolumeInKg
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            string apiUrl = "http://172.16.0.44/xTrack-copy/hs/api/v1/order/create";
            string login = "ScanBridge";
            string password = "Wi9wuzeb";
            var byteArray = Encoding.ASCII.GetBytes($"{login}:{password}");

            var client = _httpClientFactory.CreateClient();
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            try
            {
                // ==========================================
                // ШАГ 1: Создание заказа на производство
                // ==========================================
                _logger.LogInformation("Шаг 1: Отправка POST запроса на создание заказа в хТрек...");
                var response = await client.SendAsync(requestMessage);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ошибка при создании заказа. Статус: {StatusCode}, Ответ: {Response}", response.StatusCode, responseString);
                    return StatusCode((int)response.StatusCode, $"Ошибка при создании заказа: {responseString}");
                }

                var responseObj = JsonSerializer.Deserialize<XtrackResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (responseObj == null || string.IsNullOrEmpty(responseObj.OrderId))
                {
                    if (responseObj != null && !string.IsNullOrEmpty(responseObj.Error))
                    {
                        _logger.LogError("Ошибка API хТрек: {Error}", responseObj.Error);
                        return BadRequest($"Ошибка API хТрек: {responseObj.Error}");
                    }
                    _logger.LogError("Не удалось получить orderId из ответа хТрек. Ответ: {Response}", responseString);
                    return BadRequest("Не удалось получить orderId из ответа хТрек.");
                }

                string orderId = responseObj.OrderId;
                _logger.LogInformation("Шаг 1 успешно завершен. Получен orderId: {OrderId}", orderId);

                // ==========================================
                // ШАГ 2: GET запрос к оборудованию
                // ==========================================
                string equipmentUrl = $"http://172.16.172.162:2020/ConnectService/json/SendMessage?connectName={Uri.EscapeDataString(request.DeviceName)}&message=A!GW7D|0&timeout=200";
                _logger.LogInformation("Шаг 2: Отправка GET запроса на оборудование по адресу: {Url}", equipmentUrl);

                var equipmentResponse = await client.GetAsync(equipmentUrl);
                if (!equipmentResponse.IsSuccessStatusCode)
                {
                    var eqError = await equipmentResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Ошибка при отправке сообщения на оборудование. Статус: {StatusCode}, Ответ: {Response}", equipmentResponse.StatusCode, eqError);
                    return StatusCode((int)equipmentResponse.StatusCode, $"Ошибка при отправке сообщения на оборудование: {eqError}");
                }
                _logger.LogInformation("Шаг 2 успешно завершен. Сообщение отправлено на оборудование.");

                // ==========================================
                // ШАГ 3: POST запрос на обновление заказа
                // ==========================================
                _logger.LogInformation("Шаг 3: Отправка POST запроса на обновление заказа (order/update)...");
                string updateApiUrl = "http://172.16.0.44/xTrack-copy/hs/api/v1/order/update";

                var updateRequestBody = new
                {
                    Orders = new[]
                    {
                        new
                        {
                            lineId = request.LineId,
                            orderId = orderId
                        }
                    }
                };

                var updateJsonContent = JsonSerializer.Serialize(updateRequestBody);
                var updateContent = new StringContent(updateJsonContent, Encoding.UTF8, "application/json");

                var updateRequestMessage = new HttpRequestMessage(HttpMethod.Post, updateApiUrl)
                {
                    Content = updateContent
                };
                updateRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                var updateResponse = await client.SendAsync(updateRequestMessage);
                var updateResponseString = await updateResponse.Content.ReadAsStringAsync();

                if (!updateResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Ошибка при обновлении заказа. Статус: {StatusCode}, Ответ: {Response}", updateResponse.StatusCode, updateResponseString);
                    return StatusCode((int)updateResponse.StatusCode, $"Ошибка при обновлении заказа (order/update): {updateResponseString}");
                }

                _logger.LogInformation("Шаг 3 успешно завершен. Заказ обновлен. Возвращаем orderId: {OrderId}", orderId);
                return Ok(orderId);
            }
            catch (Exception ex)
            {
                // Логируем полный стектрейс исключения в консоль
                _logger.LogError(ex, "Внутренняя ошибка сервера при обработке запроса на создание заказа.");
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }
    }

    public class CreateOrderRequest
    {
        public string DeviceName { get; set; }
        public string LineId { get; set; }
    }

    public class XtrackResponse
    {
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }
}