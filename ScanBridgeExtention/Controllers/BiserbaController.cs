using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace ScanBridgeExtention.Controllers
{
    /// <summary>
    /// Контроллер для интеграции с Biserba.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Biserba")]
    public class BiserbaController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Models.AppDbContext _context;

        public BiserbaController(IHttpClientFactory httpClientFactory, Models.AppDbContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        private class PrismItem
        {
            public string? Номенклатура { get; set; }
            public string? ВариантМаркировки { get; set; }
            public string? GTIN { get; set; }
            public string? SKU { get; set; }
        }

        private class TokenResponse
        {
            public object? user { get; set; }
            public string? token { get; set; }
        }

        /// <summary>
        /// Нормализует GTIN для безопасного сравнения: удаляет пробелы и ведущие нули.
        /// Если строка состояла только из нулей, возвращает "0".
        /// </summary>
        private static string NormalizeGtin(string? gtin)
        {
            if (string.IsNullOrWhiteSpace(gtin)) return string.Empty;

            var normalized = gtin.Trim().TrimStart('0');
            return string.IsNullOrEmpty(normalized) ? "0" : normalized;
        }

        /// <summary>
        /// Синхронизирует сроки годности из Национального каталога в Biserba (через внешнее API).
        /// 1) Получает список GTIN + ShelfLife из локальной БД (admin_locks -> products).
        /// 2) Запрашивает список SKU/GTIN от Prism.
        /// 3) Сопоставляет по нормализованному GTIN и для каждого совпадения делает PATCH запрос к Biserba API.
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync()
        {
            // 1) Получаем данные из NationalCatalog (через DB запрос, аналогично product/locked)
            var national = await _context.AdminLocks
                .Include(al => al.Product)
                .Where(al => al.Product != null && al.Product.Gtin != null)
                .Select(al => new { Gtin = al.Product!.Gtin!, ShelfLife = al.Product!.ShelfLife })
                .ToListAsync();

            // Нормализуем GTIN при создании словаря, чтобы "0123456789012" и "123456789012" стали одинаковыми ключами
            var nationalByGtin = national
                .Where(n => !string.IsNullOrWhiteSpace(n.Gtin))
                .ToDictionary(
                    n => NormalizeGtin(n.Gtin!),
                    n => n.ShelfLife,
                    StringComparer.OrdinalIgnoreCase
                );

            // 2) Запрашиваем данные из Prism
            var prismUrl = "http://172.16.0.22:8000/prism/hs/exchange/get_sku";
            var prismUsername = "prism_sync";
            var prismPassword = "$prism_09W";

            var client = _httpClientFactory.CreateClient();
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{prismUsername}:{prismPassword}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

            List<PrismItem>? prismItems;
            try
            {
                var resp = await client.GetAsync(prismUrl);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    return StatusCode((int)resp.StatusCode, new { error = "Призм вернул ошибку", body });
                }

                prismItems = System.Text.Json.JsonSerializer.Deserialize<List<PrismItem>>(body);
                if (prismItems == null)
                {
                    return BadRequest(new { error = "Не удалось распарсить ответ Prism" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "Ошибка при вызове Prism", message = ex.Message });
            }

            // 3) Получаем токен у Biserba
            var tokenUrl = "http://172.16.172.162:9997/api/v1/token";
            string? token;
            try
            {
                var tokenClient = _httpClientFactory.CreateClient();
                var credObj = new { userName = "Administrator", password = "12345678" };
                var credJson = System.Text.Json.JsonSerializer.Serialize(credObj);
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
                {
                    Content = new StringContent(credJson, Encoding.UTF8, "application/json")
                };

                var tokenResp = await tokenClient.SendAsync(tokenRequest);
                var tokenBody = await tokenResp.Content.ReadAsStringAsync();
                if (!tokenResp.IsSuccessStatusCode)
                {
                    return StatusCode((int)tokenResp.StatusCode, new { error = "Ошибка получения токена", body = tokenBody });
                }

                var tokenParsed = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(tokenBody);
                token = tokenParsed?.token;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return BadRequest(new { error = "Токен не найден в ответе", body = tokenBody });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "Ошибка при получении токена", message = ex.Message });
            }

            // 4) Для каждого артикула из Prism делаем PATCH
            var biserbaClient = _httpClientFactory.CreateClient();
            biserbaClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var results = new List<object>();

            foreach (var item in prismItems)
            {
                var sku = item.SKU;
                var gtin = item.GTIN;

                if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(gtin))
                {
                    results.Add(new { sku, gtin, ok = false, reason = "Missing sku or gtin" });
                    continue;
                }

                // Нормализуем GTIN из Prism перед поиском в словаре
                var normalizedGtin = NormalizeGtin(gtin);

                if (!nationalByGtin.TryGetValue(normalizedGtin, out var shelfLife))
                {
                    results.Add(new { sku, gtin, ok = false, reason = "No matching shelfLife" });
                    continue;
                }

                var patchUrl = $"http://172.16.172.162:9997/api/v1/articles/{sku}/labeler";
                var patchBody = new[] {
                    new { op = "replace", path = "/articlePLU/shelfLifeDays1", value = shelfLife }
                };
                var patchJson = System.Text.Json.JsonSerializer.Serialize(patchBody);

                try
                {
                    var patchReq = new HttpRequestMessage(HttpMethod.Patch, patchUrl)
                    {
                        Content = new StringContent(patchJson, Encoding.UTF8, "application/json-patch+json")
                    };

                    var patchResp = await biserbaClient.SendAsync(patchReq);
                    var patchRespBody = await patchResp.Content.ReadAsStringAsync();
                    results.Add(new
                    {
                        sku,
                        gtin, // В логе оставляем оригинальный GTIN для удобства отладки
                        ok = patchResp.IsSuccessStatusCode,
                        status = (int)patchResp.StatusCode,
                        body = patchRespBody
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { sku, gtin, ok = false, reason = ex.Message });
                }
            }

            return Ok(results);
        }
    }
}