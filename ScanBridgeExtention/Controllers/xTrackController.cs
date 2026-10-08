using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScanBridgeExtention.Controllers
{
    /// <summary>
    /// Контроллер для интеграции с внешней системой маркировки/учета xTrack.
    /// </summary>
    /// <remarks>
    /// Предоставляет эндпоинты для создания производственных заданий (заказов) 
    /// через внешний API системы xTrack с использованием Basic-аутентификации.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("xTrack Интеграция")] // Группирует методы в Scalar под этим заголовком
    public class xTrackController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<xTrackController> _logger;

        public xTrackController(IHttpClientFactory httpClientFactory, ILogger<xTrackController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Создать новый заказ (задание) в системе xTrack.
        /// </summary>
        /// <remarks>
        /// **Логика работы метода:**
        /// 1. Проверяет наличие обязательных полей `DeviceName` и `LineId`.
        /// 2. Если `GTIN` или `SKU` не переданы (пустые), система **автоматически подставляет** значения по умолчанию в зависимости от `LineId`.
        /// 3. Формирует и отправляет запрос к внешнему API xTrack (`/xTrack/hs/api/v1/order/create`).
        /// 4. При успехе возвращает строку с `orderId`.
        /// 
        /// </remarks>
        /// <param name="request">Данные для создания заказа (устройство, линия, опционально GTIN и SKU).</param>
        /// <response code="200">Заказ успешно создан. В теле ответа возвращается строка `orderId`.</response>
        /// <response code="400">Некорректные входные данные (отсутствуют DeviceName/LineId) или ошибка, возвращенная внешним API xTrack.</response>
        /// <response code="500">Внутренняя ошибка сервера (например, таймаут или недоступность внешнего API xTrack).</response>
        [HttpPost("createClosingOrder")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateСlosingOrderRequest request)
        {
            _logger.LogInformation("Получен запрос на создание заказа. DeviceName: {DeviceName}, LineId: {LineId}", request?.DeviceName, request?.LineId);

            if (request == null || string.IsNullOrWhiteSpace(request.DeviceName) || string.IsNullOrWhiteSpace(request.LineId))
            {
                _logger.LogWarning("Запрос отклонен: не переданы обязательные параметры DeviceName или LineId.");
                return BadRequest("Необходимо передать DeviceName и LineId в теле запроса.");
            }

            if (request.GTIN == "")
            {
                switch (request.LineId)
                {
                    case "1":
                        request.GTIN = "04607077972436";
                        break;
                    case "2":
                        request.GTIN = "04607077976199";
                        break;
                    case "3":
                        request.GTIN = "04607077976205";
                        break;
                }
            }

            if (request.SKU == "")
            {
                request.SKU = "998";
            }

            var organisation = new { inn = "3127007309", kpp = "312701001" };
            string type = "serialisationaggregation";
            string equipmentTaskCreationMode = "printTaskCapture";
            string count = "1";

            var date = DateTime.Now.ToShortDateString();
            var productSeries = new
            {
                lotNumber = "API" + date.Replace(".", ""),
                productDate = date,
                expDate = "07.08.2222"
            };

            var requestBody = new
            {
                organisation,
                equipmentTaskCreationMode,
                gtin = request.GTIN,
                productSeries,
                count,
                type,
                sku = request.SKU,
                lineId = request.LineId
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            string apiUrl = "http://172.16.0.44/xTrack/hs/api/v1/order/create";
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

                return Ok(orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Внутренняя ошибка сервера при обработке запроса на создание заказа.");
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Модель запроса для создания заказа закрытия в системе xTrack.
    /// </summary>
    public class CreateСlosingOrderRequest
    {
        /// <summary>
        /// Имя устройства или сканера, инициирующего запрос.
        /// </summary>
        /// <example>Scanner_01</example>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Идентификатор производственной линии. Используется для автоподстановки GTIN, если он не передан явно.
        /// </summary>
        /// <example>1</example>
        public string? LineId { get; set; }

        /// <summary>
        /// Глобальный номер торговой единицы. Если передан пустым, подставляется автоматически в зависимости от LineId.
        /// </summary>
        /// <example>04607077972436</example>
        public string? GTIN { get; set; }

        /// <summary>
        /// Артикул (SKU). Если передан пустым, по умолчанию устанавливается значение "998".
        /// </summary>
        /// <example>998</example>
        public string? SKU { get; set; }
    }

    /// <summary>
    /// Модель ответа от внешнего API системы xTrack.
    /// </summary>
    public class XtrackResponse
    {
        /// <summary>
        /// Уникальный идентификатор успешно созданного заказа.
        /// </summary>
        [JsonPropertyName("orderId")]
        public string? OrderId { get; set; }

        /// <summary>
        /// Текст ошибки, если создание заказа не удалось.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}