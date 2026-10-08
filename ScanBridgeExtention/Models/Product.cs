using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanBridgeExtention.Models
{
    /// <summary>
    /// Базовая сущность товара в Национальном каталоге.
    /// Содержит исчерпывающую информацию о карточке товара, включая нутриенты, условия хранения и сертификацию.
    /// </summary>
    [Table("products")]
    public class Product
    {
        /// <summary>
        /// Уникальный внутренний идентификатор записи товара в базе данных.
        /// </summary>
        /// <example>10542</example>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Порядковый номер строки (используется при пакетном импорте/выгрузке).
        /// </summary>
        /// <example>1</example>
        [Column("row_num")]
        public int? RowNum { get; set; }

        /// <summary>
        /// Поле для кастомной сортировки товаров в каталоге.
        /// </summary>
        /// <example>100</example>
        [Column("sort_order")]
        public int? SortOrder { get; set; }

        /// <summary>
        /// Глобальный номер торговой единицы (штрихкод / GTIN).
        /// </summary>
        /// <example>04607077972436</example>
        [Display(Name = "GTIN")]
        [Column("gtin")]
        [MaxLength(255)]
        public string? Gtin { get; set; }

        /// <summary>
        /// Код Товарной номенклатуры внешнеэкономической деятельности (ТН ВЭД ЕАЭС).
        /// </summary>
        /// <example>1901200000</example>
        [Display(Name = "Код ТНВЭД")]
        [Column("tnved")]
        public string? Tnved { get; set; }

        /// <summary>
        /// Внутренний или внешний код категории товара.
        /// </summary>
        /// <example>CAT-001</example>
        [Display(Name = "Код категории")]
        [Column("category_code")]
        public string? CategoryCode { get; set; }

        /// <summary>
        /// Полное наименование товара согласно этикетке.
        /// </summary>
        /// <example>Молоко питьевое пастеризованное 3.2%, 1л</example>
        [Display(Name = "Полное наименование товара")]
        [Column("full_name")]
        public string? FullName { get; set; }

        /// <summary>
        /// Название бренда или товарного знака.
        /// </summary>
        /// <example>Домик в деревне</example>
        [Display(Name = "Товарный знак")]
        [Column("trademark")]
        public string? Trademark { get; set; }

        /// <summary>
        /// Код по Общероссийскому классификатору продукции по видам экономической деятельности (ОКПД 2).
        /// </summary>
        /// <example>10.51.11</example>
        [Display(Name = "Код ОКПД2")]
        [Column("okpd2")]
        public string? Okpd2 { get; set; }

        /// <summary>
        /// Признак товара с переменным количеством (например, весовой товар).
        /// </summary>
        /// <example>Нет</example>
        [Display(Name = "Товар с переменным количеством")]
        [Column("variable_qty")]
        public string? VariableQty { get; set; }

        /// <summary>
        /// Единица измерения массы нетто.
        /// </summary>
        /// <example>г</example>
        [Display(Name = "Масса нетто (тип)")]
        [Column("net_weight_type")]
        public string? NetWeightType { get; set; }

        /// <summary>
        /// Числовое значение массы нетто.
        /// </summary>
        /// <example>1000</example>
        [Display(Name = "Масса нетто (значение)")]
        [Column("net_weight_value")]
        public string? NetWeightValue { get; set; }

        /// <summary>
        /// Единица измерения нижнего предела диапазона веса.
        /// </summary>
        [Display(Name = "Диапазон веса от (тип)")]
        [Column("weight_from_type")]
        public string? WeightFromType { get; set; }

        /// <summary>
        /// Значение нижнего предела диапазона веса.
        /// </summary>
        [Display(Name = "Диапазон веса от (значение)")]
        [Column("weight_from_value")]
        public string? WeightFromValue { get; set; }

        /// <summary>
        /// Единица измерения верхнего предела диапазона веса.
        /// </summary>
        [Display(Name = "Диапазон веса до (тип)")]
        [Column("weight_to_type")]
        public string? WeightToType { get; set; }

        /// <summary>
        /// Значение верхнего предела диапазона веса.
        /// </summary>
        [Display(Name = "Диапазон веса до (значение)")]
        [Column("weight_to_value")]
        public string? WeightToValue { get; set; }

        /// <summary>
        /// Тип упаковки товара.
        /// </summary>
        /// <example>Бутылка</example>
        [Display(Name = "Тип упаковки")]
        [Column("pack_type")]
        public string? PackType { get; set; }

        /// <summary>
        /// Материал упаковки.
        /// </summary>
        /// <example>Пластик (ПЭТ)</example>
        [Display(Name = "Материал упаковки")]
        [Column("pack_material")]
        public string? PackMaterial { get; set; }

        /// <summary>
        /// Тип продукта.
        /// </summary>
        [Display(Name = "Тип продукта")]
        [Column("product_type")]
        public string? ProductType { get; set; }

        /// <summary>
        /// Способ производства или обработки (например, "Пастеризация", "Копчение").
        /// </summary>
        [Display(Name = "Способ производства/обработки")]
        [Column("production_method")]
        public string? ProductionMethod { get; set; }

        /// <summary>
        /// Происхождение сырья (например, "Животное", "Растительное").
        /// </summary>
        [Display(Name = "Происхождение сырья")]
        [Column("raw_material_origin")]
        public string? RawMaterialOrigin { get; set; }

        /// <summary>
        /// Признак продукции для детского питания.
        /// </summary>
        /// <example>Нет</example>
        [Display(Name = "Продукция для детского питания")]
        [Column("children_food")]
        public string? ChildrenFood { get; set; }

        /// <summary>
        /// Признак наличия ГМО в составе.
        /// </summary>
        /// <example>Нет</example>
        [Display(Name = "Содержит ГМО")]
        [Column("contains_gmo")]
        public string? ContainsGmo { get; set; }

        /// <summary>
        /// Единица измерения БЖУ (Белки, Жиры, Углеводы).
        /// </summary>
        /// <example>г</example>
        [Display(Name = "Единица измерения БЖУ")]
        [Column("bju_unit")]
        public string? BjuUnit { get; set; }

        /// <summary>
        /// Единица измерения белков (обычно совпадает с БЖУ).
        /// </summary>
        [Display(Name = "Белки (тип)")]
        [Column("protein_type")]
        public string? ProteinType { get; set; }

        /// <summary>
        /// Количество белков на 100г/мл продукта.
        /// </summary>
        /// <example>2.9</example>
        [Display(Name = "Белки (количество)")]
        [Column("protein_value")]
        public string? ProteinValue { get; set; }

        /// <summary>
        /// Единица измерения жиров.
        /// </summary>
        [Display(Name = "Жиры (тип)")]
        [Column("fat_type")]
        public string? FatType { get; set; }

        /// <summary>
        /// Количество жиров на 100г/мл продукта.
        /// </summary>
        /// <example>3.2</example>
        [Display(Name = "Жиры (количество)")]
        [Column("fat_value")]
        public string? FatValue { get; set; }

        /// <summary>
        /// Единица измерения углеводов.
        /// </summary>
        [Display(Name = "Углеводы (тип)")]
        [Column("carbs_type")]
        public string? CarbsType { get; set; }

        /// <summary>
        /// Количество углеводов на 100г/мл продукта.
        /// </summary>
        /// <example>4.7</example>
        [Display(Name = "Углеводы (количество)")]
        [Column("carbs_value")]
        public string? CarbsValue { get; set; }

        /// <summary>
        /// Базовая единица измерения энергетической ценности.
        /// </summary>
        [Display(Name = "Единица измерения энерг. ценности")]
        [Column("energy_unit")]
        public string? EnergyUnit { get; set; }

        /// <summary>
        /// Единица измерения энергии в килоджоулях.
        /// </summary>
        [Display(Name = "Энерг. ценность кДж (тип)")]
        [Column("energy_kj_type")]
        public string? EnergyKjType { get; set; }

        /// <summary>
        /// Значение энергетической ценности в килоджоулях (кДж).
        /// </summary>
        /// <example>250</example>
        [Display(Name = "Энерг. ценность кДж (значение)")]
        [Column("energy_kj_value")]
        public string? EnergyKjValue { get; set; }

        /// <summary>
        /// Единица измерения энергии в килокалориях.
        /// </summary>
        [Display(Name = "Единица измерения энерг. ценности ккал")]
        [Column("energy_kcal_unit")]
        public string? EnergyKcalUnit { get; set; }

        /// <summary>
        /// Единица измерения ккал.
        /// </summary>
        [Display(Name = "Энерг. ценность ккал (тип)")]
        [Column("energy_kcal_type")]
        public string? EnergyKcalType { get; set; }

        /// <summary>
        /// Значение энергетической ценности в килокалориях (ккал).
        /// </summary>
        /// <example>60</example>
        [Display(Name = "Энерг. ценность ккал (значение)")]
        [Column("energy_kcal_value")]
        public string? EnergyKcalValue { get; set; }

        /// <summary>
        /// Полный состав продукта (текстовое описание ингредиентов).
        /// </summary>
        /// <example>Нормализованное молоко.</example>
        [Display(Name = "Состав")]
        [Column("ingredients")]
        public string? Ingredients { get; set; }

        /// <summary>
        /// Признак необходимости ветеринарного контроля.
        /// </summary>
        /// <example>Да</example>
        [Display(Name = "Подлежит ветеринарному контролю")]
        [Column("vet_control")]
        public string? VetControl { get; set; }

        /// <summary>
        /// Регистрационный номер декларации о соответствии (ЕАС).
        /// </summary>
        /// <example>ЕАЭС N RU Д-RU.АБ12.В.34567/21</example>
        [Display(Name = "Регистрационный № декларации")]
        [Column("declaration_number")]
        public string? DeclarationNumber { get; set; }

        /// <summary>
        /// Тип продукции согласно классификатору ФГИС "ВетИС".
        /// </summary>
        [Display(Name = "Тип продукции (ВетИС)")]
        [Column("product_type_vis")]
        public string? ProductTypeVis { get; set; }

        /// <summary>
        /// Наименование продукции в системе ВетИС.
        /// </summary>
        [Display(Name = "Продукция (ВетИС)")]
        [Column("product_vis")]
        public string? ProductVis { get; set; }

        /// <summary>
        /// Вид продукции в системе ВетИС.
        /// </summary>
        [Display(Name = "Вид продукции (ВетИС)")]
        [Column("product_kind_vis")]
        public string? ProductKindVis { get; set; }

        /// <summary>
        /// Категория продукции.
        /// </summary>
        [Display(Name = "Категория продукции")]
        [Column("product_category")]
        public string? ProductCategory { get; set; }

        /// <summary>
        /// Тип стандарта (например, "ГОСТ", "ТУ").
        /// </summary>
        /// <example>ГОСТ</example>
        [Display(Name = "ГОСТ (тип)")]
        [Column("gost_type")]
        public string? GostType { get; set; }

        /// <summary>
        /// Номер и год стандарта качества.
        /// </summary>
        /// <example>31450-2013</example>
        [Display(Name = "ГОСТ (значение)")]
        [Column("gost_value")]
        public string? GostValue { get; set; }

        /// <summary>
        /// Наименование предприятия-производителя.
        /// </summary>
        /// <example>ОАО "Вимм-Билль-Данн"</example>
        [Display(Name = "Предприятие-производитель")]
        [Column("enterprise_producer")]
        public string? EnterpriseProducer { get; set; }

        /// <summary>
        /// Минимально допустимая температура хранения (°С).
        /// </summary>
        /// <example>2</example>
        [Display(Name = "Мин. температура °С")]
        [Column("temp_min")]
        public string? TempMin { get; set; }

        /// <summary>
        /// Максимально допустимая температура хранения (°С).
        /// </summary>
        /// <example>6</example>
        [Display(Name = "Макс. температура °С")]
        [Column("temp_max")]
        public string? TempMax { get; set; }

        /// <summary>
        /// Минимально допустимая относительная влажность хранения (%).
        /// </summary>
        [Display(Name = "Мин. относительная влажность %")]
        [Column("humidity_min")]
        public string? HumidityMin { get; set; }

        /// <summary>
        /// Максимально допустимая относительная влажность хранения (%).
        /// </summary>
        [Display(Name = "Макс. относительная влажность %")]
        [Column("humidity_max")]
        public string? HumidityMax { get; set; }

        /// <summary>
        /// Гарантийный срок годности в сутках.
        /// </summary>
        /// <example>14</example>
        [Display(Name = "Срок годности товара (сут)")]
        [Column("shelf_life")]
        public string? ShelfLife { get; set; }

        /// <summary>
        /// Текстовое описание особых условий хранения и транспортировки.
        /// </summary>
        /// <example>Хранить при температуре от +2 до +6 °С.</example>
        [Display(Name = "Особые условия хранения")]
        [Column("special_storage_conditions")]
        public string? SpecialStorageConditions { get; set; }

        /// <summary>
        /// URL или относительный путь к изображению товара (вид спереди).
        /// </summary>
        /// <example>/images/products/10542_front.jpg</example>
        [Display(Name = "Фотоконтент — вид спереди")]
        [Column("photo_front")]
        public string? PhotoFront { get; set; }

        /// <summary>
        /// Текущий статус карточки товара в системе модерации.
        /// </summary>
        /// <example>Одобрено</example>
        [Display(Name = "Статус карточки")]
        [Column("card_status")]
        public string? CardStatus { get; set; }

        /// <summary>
        /// Замечания, комментарии или рекомендации проверяющего инспектора.
        /// </summary>
        [Display(Name = "Замечания / рекомендовано")]
        [Column("remarks")]
        public string? Remarks { get; set; }

        /// <summary>
        /// ФИО или идентификатор инспектора, проводившего проверку.
        /// </summary>
        [Display(Name = "Подпись проверяющего")]
        [Column("inspector_signature")]
        public string? InspectorSignature { get; set; }

        /// <summary>
        /// Дата проведения последней проверки карточки товара.
        /// </summary>
        /// <example>2023-10-15</example>
        [Display(Name = "Дата проверки")]
        [Column("inspection_date")]
        public string? InspectionDate { get; set; }

        /// <summary>
        /// Внутренний код номенклатуры в системе 1С.
        /// </summary>
        /// <example>00-0012345</example>
        [Display(Name = "Код 1C")]
        [Column("code_1c")]
        public string? Code1C { get; set; }

        /// <summary>
        /// Дата и время создания записи в базе данных.
        /// </summary>
        /// <example>2023-10-01T12:00:00Z</example>
        [Display(Name = "Дата создания")]
        [Column("created_at")]
        public string? CreatedAt { get; set; }

        /// <summary>
        /// Применимая ставка НДС для данного товара.
        /// </summary>
        /// <example>20%</example>
        [Display(Name = "Ставка НДС")]
        [Column("vat_rate")]
        [MaxLength(50)]
        public string? VatRate { get; set; }

        /// <summary>
        /// Дата начала действия декларации о соответствии.
        /// </summary>
        /// <example>2021-11-01</example>
        [Display(Name = "Дата начала действия")]
        [Column("declaration_start_date", TypeName = "date")]
        public DateOnly? DeclarationStartDate { get; set; }

        /// <summary>
        /// Дата окончания действия декларации о соответствии.
        /// </summary>
        /// <example>2026-10-31</example>
        [Display(Name = "Дата окончания действия")]
        [Column("declaration_end_date", TypeName = "date")]
        public DateOnly? DeclarationEndDate { get; set; }

        /// <summary>
        /// Объем производства данной продукции за неделю (в килограммах).
        /// </summary>
        /// <example>1500.500</example>
        [Display(Name = "Количество кг продукции за неделю")]
        [Column("weekly_production_kg", TypeName = "decimal(18,3)")]
        public decimal? WeeklyProductionKg { get; set; }
    }
}