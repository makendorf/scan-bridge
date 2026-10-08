using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanBridgeExtention.Dto;
using ScanBridgeExtention.Models;

namespace ScanBridgeExtention.Controllers
{
    /// <summary>
    /// Контроллер для работы с данными Национального каталога товаров.
    /// Предоставляет эндпоинты для поиска, пакетной загрузки и получения информации о весе товаров по GTIN.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Национальный каталог")] // Группирует все методы этого контроллера в один раздел в Scalar
    public class NationalCatalogController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NationalCatalogController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Получить полный список товаров из каталога.
        /// </summary>
        /// <remarks>
        /// Возвращает все товары, отсортированные по полю `SortOrder`. 
        /// Используется для первичной синхронизации или получения полного справочника.
        /// </remarks>
        /// <response code="200">Список товаров успешно получен.</response>
        [HttpGet("product/all")]
        [ProducesResponseType(typeof(List<ProductResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllProducts()
        {
            var products = await _context.Products
                .OrderBy(p => p.SortOrder)
                .ToListAsync();

            return Ok(await MapToDtosAsync(products, null));
        }

        /// <summary>
        /// Получить информацию о конкретном товаре по его GTIN.
        /// </summary>
        /// <remarks>
        /// Метод автоматически обрабатывает вариации GTIN (например, с разными префиксами или контрольными суммами). 
        /// Если точного совпадения ни с одной вариацией не найдено, возвращается ошибка 404.
        /// </remarks>
        /// <param name="gtin">Глобальный номер торговой единицы (строка).</param>
        /// <response code="200">Товар успешно найден.</response>
        /// <response code="400">Передан некорректный формат GTIN.</response>
        /// <response code="404">Товар с указанным GTIN (или его вариациями) не найден в базе.</response>
        [HttpGet("product/{gtin}")]
        [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductByGtin(string gtin)
        {
            var variations = GtinHelper.GetGtinVariations(gtin);

            if (!variations.Any())
            {
                return BadRequest(new { error = "Invalid GTIN format (GetProductByGtin)" });
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Gtin != null && variations.Contains(p.Gtin));

            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            var mapping = variations.ToDictionary(
                v => v,
                v => gtin,
                StringComparer.OrdinalIgnoreCase
            );

            var result = (await MapToDtosAsync(new List<Product> { product }, mapping)).FirstOrDefault();
            return Ok(result);
        }

        /// <summary>
        /// Пакетный поиск товаров по списку GTIN.
        /// </summary>
        /// <remarks>
        /// **Рекомендуемый способ** получения данных по нескольким товарам. 
        /// </remarks>
        /// <param name="request">Объект запроса, содержащий массив строк `Gtins`.</param>
        /// <response code="200">Список найденных товаров (может быть пустым, если совпадений нет).</response>
        /// <response code="400">Отсутствует или некорректно сформирован массив `gtins` в теле запроса.</response>
        [HttpPost("product/all/batch")]
        [ProducesResponseType(typeof(List<ProductResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductsByGtins([FromBody] BatchGtinRequest request)
        {
            if (request?.Gtins == null)
            {
                return BadRequest(new { error = "Missing 'gtins' array in request body" });
            }

            if (request.Gtins.Count == 0)
            {
                return Ok(new List<ProductResponseDto>());
            }

            var variationToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allVariations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var requestedGtin in request.Gtins)
            {
                var variations = GtinHelper.GetGtinVariations(requestedGtin);
                foreach (var variation in variations)
                {
                    if (!variationToOriginal.ContainsKey(variation))
                    {
                        variationToOriginal[variation] = requestedGtin;
                        allVariations.Add(variation);
                    }
                }
            }

            if (!allVariations.Any())
            {
                return Ok(new List<ProductResponseDto>());
            }

            var variationsList = allVariations.ToList();

            var products = await _context.Products
                .Where(p => p.Gtin != null && variationsList.Contains(p.Gtin))
                .ToListAsync();

            return Ok(await MapToDtosAsync(products, variationToOriginal));
        }

        /// <summary>
        /// Пакетное получение информации о весе товаров по списку GTIN.
        /// </summary>
        /// <remarks>
        /// Возвращает облегченный DTO (`ProductWeigthResponseDto`), содержащий только GTIN и рассчитанный вес в килограммах.
        /// Алгоритм расчета учитывает тип товара (`VariableQty`) и приводит единицы измерения (г, кг) к единому формату (кг).
        /// Если приведение к (кг) не возможно, возвращается 0.
        /// </remarks>
        /// <param name="request">Объект запроса, содержащий массив строк `Gtins`.</param>
        /// <response code="200">Список объектов с GTIN и рассчитанным весом.</response>
        /// <response code="400">Отсутствует или некорректно сформирован массив `gtins` в теле запроса.</response>
        [HttpPost("product/all/batchWeigth")]
        [ProducesResponseType(typeof(List<ProductWeigthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductsWeightByGtins([FromBody] BatchGtinRequest request)
        {
            if (request?.Gtins == null)
            {
                return BadRequest(new { error = "Отсутствуют 'gtins' в теле запроса" });
            }

            if (request.Gtins.Count == 0)
            {
                return Ok(new List<ProductWeigthResponseDto>());
            }

            var variationToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allVariations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var requestedGtin in request.Gtins)
            {
                var variations = GtinHelper.GetGtinVariations(requestedGtin);
                foreach (var variation in variations)
                {
                    if (!variationToOriginal.ContainsKey(variation))
                    {
                        variationToOriginal[variation] = requestedGtin;
                        allVariations.Add(variation);
                    }
                }
            }

            if (!allVariations.Any())
            {
                return Ok(new List<ProductWeigthResponseDto>());
            }

            var variationsList = allVariations.ToList();

            var products = await _context.Products
                .Where(p => p.Gtin != null && variationsList.Contains(p.Gtin))
                .ToListAsync();

            return Ok(await MapToWeigthDtosAsync(products, variationToOriginal));
        }

        /// <summary>
        /// Получить список GTIN и срока годности (shelf_life) для карточек, у которых есть запись в admin_locks.
        /// </summary>
        /// <response code="200">Список объектов с GTIN и сроком годности.</response>
        [HttpGet("product/locked")]
        [ProducesResponseType(typeof(List<ScanBridgeExtention.Dto.ProductGtinShelfDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLockedProducts()
        {
            var items = await _context.AdminLocks
                .Include(al => al.Product)
                .Where(al => al.Product != null && al.Product.Gtin != null)
                .Select(al => new ScanBridgeExtention.Dto.ProductGtinShelfDto
                {
                    Gtin = al.Product.Gtin,
                    ShelfLife = al.Product.ShelfLife
                })
                .ToListAsync();

            // Убрать дубликаты по GTIN, если есть
            var distinct = items
                .GroupBy(x => x.Gtin, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return Ok(distinct);
        }

        // --- Приватные методы остаются без изменений, OpenAPI их игнорирует, но XML-комментарии полезны для разработчиков ---

        /// <summary>
        /// Оптимизированный метод маппинга. 
        /// Вместо выполнения запросов в цикле (N+1), мы делаем 2 запроса для всей партии данных.
        /// </summary>
        private async Task<List<ProductResponseDto>> MapToDtosAsync(
            List<Product> products,
            Dictionary<string, string>? requestedGtinMapping,
            bool onlyWeigth = false)
        {
            if (products == null || products.Count == 0)
                return new List<ProductResponseDto>();

            var productIds = products.Select(p => p.Id).ToList();

            var confirmationsByProductId = await _context.UserConfirmations
                .Where(uc => productIds.Contains(uc.ProductId))
                .GroupBy(uc => uc.ProductId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.ToDictionary(uc => uc.Username, uc => uc.ConfirmedAt)
                );

            var locksByProductId = await _context.AdminLocks
                .Where(al => productIds.Contains(al.ProductId))
                .ToDictionaryAsync(al => al.ProductId);

            return products.Select(p => new ProductResponseDto
            {
                Id = p.Id,
                RowNum = p.RowNum,
                Gtin = (requestedGtinMapping != null && p.Gtin != null && requestedGtinMapping.TryGetValue(p.Gtin, out var requestedGtin))
                    ? requestedGtin
                    : p.Gtin,
                Tnved = p.Tnved,
                CategoryCode = p.CategoryCode,
                FullName = p.FullName,
                Trademark = p.Trademark,
                Okpd2 = p.Okpd2,
                VariableQty = p.VariableQty,
                NetWeightType = p.NetWeightType,
                NetWeightValue = p.NetWeightValue,
                WeightFromType = p.WeightFromType,
                WeightFromValue = p.WeightFromValue,
                WeightToType = p.WeightToType,
                WeightToValue = p.WeightToValue,
                PackType = p.PackType,
                PackMaterial = p.PackMaterial,
                ProductType = p.ProductType,
                ProductionMethod = p.ProductionMethod,
                RawMaterialOrigin = p.RawMaterialOrigin,
                ChildrenFood = p.ChildrenFood,
                ContainsGmo = p.ContainsGmo,
                BjuUnit = p.BjuUnit,
                ProteinType = p.ProteinType,
                ProteinValue = p.ProteinValue,
                FatType = p.FatType,
                FatValue = p.FatValue,
                CarbsType = p.CarbsType,
                CarbsValue = p.CarbsValue,
                EnergyUnit = p.EnergyUnit,
                EnergyKjType = p.EnergyKjType,
                EnergyKjValue = p.EnergyKjValue,
                EnergyKcalUnit = p.EnergyKcalUnit,
                EnergyKcalType = p.EnergyKcalType,
                EnergyKcalValue = p.EnergyKcalValue,
                Ingredients = p.Ingredients,
                VetControl = p.VetControl,
                DeclarationNumber = p.DeclarationNumber,
                ProductTypeVis = p.ProductTypeVis,
                ProductVis = p.ProductVis,
                ProductKindVis = p.ProductKindVis,
                ProductCategory = p.ProductCategory,
                GostType = p.GostType,
                GostValue = p.GostValue,
                EnterpriseProducer = p.EnterpriseProducer,
                TempMin = p.TempMin,
                TempMax = p.TempMax,
                HumidityMin = p.HumidityMin,
                HumidityMax = p.HumidityMax,
                ShelfLife = p.ShelfLife,
                SpecialStorageConditions = p.SpecialStorageConditions,
                PhotoFront = p.PhotoFront,
                CardStatus = p.CardStatus,
                Remarks = p.Remarks,
                InspectorSignature = p.InspectorSignature,
                InspectionDate = p.InspectionDate,
                Code1C = p.Code1C,
                CreatedAt = p.CreatedAt,
                SortOrder = p.SortOrder,
                VatRate = p.VatRate,
                DeclarationStartDate = p.DeclarationStartDate,
                DeclarationEndDate = p.DeclarationEndDate,
                WeeklyProductionKg = p.WeeklyProductionKg,
                Confirmations = confirmationsByProductId.TryGetValue(p.Id, out var confirms)
                    ? confirms
                    : new Dictionary<string, string>(),
                Locked = locksByProductId.TryGetValue(p.Id, out var lockInfo)
                    ? new LockInfoDto
                    {
                        LockedAt = lockInfo.LockedAt,
                        LockedBy = lockInfo.LockedBy
                    }
                    : null
            }).ToList();
        }

        private async Task<List<ProductWeigthResponseDto>> MapToWeigthDtosAsync(
            List<Product> products,
            Dictionary<string, string>? requestedGtinMapping,
            bool onlyWeigth = false)
        {
            if (products == null || products.Count == 0)
                return new List<ProductWeigthResponseDto>();

            var productIds = products.Select(p => p.Id).ToList();

            return products.Select(p => new ProductWeigthResponseDto
            {
                Gtin = (requestedGtinMapping != null && p.Gtin != null && requestedGtinMapping.TryGetValue(p.Gtin, out var requestedGtin))
                       ? requestedGtin
                       : p.Gtin,
                Weigth = CalculateWeight(p)
            }).ToList();
        }

        /// <summary>
        /// Рассчитывает вес товара в килограммах на основе типа товара и единиц измерения.
        /// </summary>
        private double CalculateWeight(Product p)
        {
            string? weightType;
            string? weightValue;

            Console.WriteLine(p.VariableQty);
            if (p.VariableQty?.Trim().ToLower() == "нет")
            {
                weightType = p.NetWeightType;
                weightValue = p.NetWeightValue;
            }
            else
            {
                weightType = p.WeightFromType;
                weightValue = p.WeightFromValue;
            }

            if (string.IsNullOrWhiteSpace(weightValue))
            {
                return 0;
            }

            if (!double.TryParse(weightValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double numericValue))
            {
                return numericValue;
            }

            var normalizedType = weightType?.Trim().ToLower();

            if (normalizedType == "г" || normalizedType == "грамм" || normalizedType == "граммы")
            {
                return numericValue / 1000;
            }
            else if (normalizedType == "кг" || normalizedType == "килограмм" || normalizedType == "килограммы")
            {
                return numericValue;
            }
            else
            {
                return 0;
            }
        }
    }
}