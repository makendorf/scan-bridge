using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace ScanBridgeExtention.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Prism")]
    public class PrismController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private const string BasePrismUrl = "http://172.16.0.22:8000/prism/hs/exchange/";
        private const string PrismUsername = "prism_sync";
        private const string PrismPassword = "$prism_09W";

        public PrismController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Прокси-метод: делает GET к внешнему Prism API и возвращает массив SKU/GTIN.
        /// </summary>
        [HttpGet("sku")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSku()
        {
            var url = $"{BasePrismUrl}get_sku";
            try
            {
                var client = _httpClientFactory.CreateClient();
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PrismUsername}:{PrismPassword}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

                using var resp = await client.GetAsync(url);
                var content = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    return StatusCode((int)resp.StatusCode, new { error = "Ошибка при вызове Prism API", status = resp.StatusCode, body = content });
                }

                return Content(content, "application/json");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "Ошибка сети при обращении к Prism API", message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Внутренняя ошибка сервера", message = ex.Message });
            }
        }

        /// <summary>
        /// Делает POST запрос для получения информации по одному OrderID.
        /// </summary>
        /// <remarks>
        /// Пример запроса:
        /// {
        ///   "orderId": "140a5d56-c252-11f1-98df-a0ad9f276340"
        /// }
        /// </remarks>
        [HttpPost("info-order")]
        [ProducesResponseType(typeof(List<OrderInfoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOrderInfo([FromBody] OrderInfoSingleRequest request)
        {
            if (request == null || request.OrderId == Guid.Empty)
            {
                return BadRequest(new { error = "Некорректный или отсутствующий orderId" });
            }

            var url = $"{BasePrismUrl}InfoOrderID";
            // Преобразуем в формат, который требует внешнее API: массив с одним объектом
            var requestBody = new[] { new { OrderID = request.OrderId.ToString() } };

            return await SendOrderInfoRequestAsync(url, requestBody);
        }

        /// <summary>
        /// Делает POST запрос для получения информации по массиву OrderID.
        /// </summary>
        /// <remarks>
        /// Пример запроса:
        /// {
        ///   "orderId": [
        ///     "140a5d56-c252-11f1-98df-a0ad9f276340",
        ///     "240a5d56-c252-11f1-98df-a0ad9f276341"
        ///   ]
        /// }
        /// </remarks>
        [HttpPost("info-order-batch")]
        [ProducesResponseType(typeof(List<OrderInfoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOrderInfoBatch([FromBody] OrderInfoBatchRequest request)
        {
            if (request == null || request.OrderId == null || !request.OrderId.Any())
            {
                return BadRequest(new { error = "Массив orderId не может быть пустым" });
            }

            var url = $"{BasePrismUrl}InfoOrderID";
            // Преобразуем массив GUID в массив объектов, который требует внешнее API
            var requestBody = request.OrderId.Select(id => new { OrderID = id.ToString() }).ToArray();

            return await SendOrderInfoRequestAsync(url, requestBody);
        }

        /// <summary>
        /// Общий метод для отправки POST-запроса к InfoOrderID и десериализации ответа.
        /// </summary>
        private async Task<IActionResult> SendOrderInfoRequestAsync(string url, object requestBody)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PrismUsername}:{PrismPassword}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

                var jsonContent = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var resp = await client.PostAsync(url, content);
                var responseContent = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    return StatusCode((int)resp.StatusCode, new { error = "Ошибка при вызове Prism API", status = resp.StatusCode, body = responseContent });
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<List<OrderInfoResponse>>(responseContent, options);

                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "Ошибка сети при обращении к Prism API", message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Внутренняя ошибка сервера", message = ex.Message });
            }
        }
    }

    // Модель для запроса с одним GUID
    public class OrderInfoSingleRequest
    {
        public Guid OrderId { get; set; }
    }

    // Модель для запроса с массивом GUID
    public class OrderInfoBatchRequest
    {
        public List<Guid> OrderId { get; set; } = new();
    }

    // Модель ответа от Prism API
    public class OrderInfoResponse
    {
        public string OrderID { get; set; } = string.Empty;
        public DateTime? ДатаВыполненияРабот { get; set; }
    }
}