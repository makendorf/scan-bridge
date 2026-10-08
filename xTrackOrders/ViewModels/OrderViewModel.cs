using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using xTrackOrders.Models.Order;
using xTrackOrders.Services;

namespace xTrackOrders.ViewModels;

/// <summary>
/// ViewModel для работы с заказами
/// </summary>
public partial class OrderViewModel : ObservableObject
{
    private readonly IOrderApiService _orderApiService;

    // Поля для привязки к UI
    [ObservableProperty]
    private string _gtin = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _count = string.Empty;

    [ObservableProperty]
    private string _lineId = string.Empty;

    [ObservableProperty]
    private string _inn = string.Empty;

    [ObservableProperty]
    private string _kpp = string.Empty;

    [ObservableProperty]
    private string _lotNumber = string.Empty;

    [ObservableProperty]
    private DateTime _productDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _expDate = DateTime.Today.AddYears(1);

    [ObservableProperty]
    private string _equipmentTaskMode = "scanTasks";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _orderId = string.Empty;

    public OrderViewModel(IOrderApiService orderApiService)
    {
        _orderApiService = orderApiService;
    }

    /// <summary>
    /// Команда создания заказа
    /// </summary>
    [RelayCommand]
    private async Task CreateOrderAsync()
    {
        // Валидация
        if (string.IsNullOrWhiteSpace(Gtin))
        {
            await Shell.Current.DisplayAlert("Ошибка", "GTIN обязателен для заполнения", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(Inn) || string.IsNullOrWhiteSpace(Kpp))
        {
            await Shell.Current.DisplayAlert("Ошибка", "ИНН и КПП организации обязательны", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(LotNumber))
        {
            await Shell.Current.DisplayAlert("Ошибка", "Номер серии обязателен", "OK");
            return;
        }

        IsLoading = true;
        StatusMessage = "Отправка запроса...";

        try
        {
            // Формируем запрос
            var request = new CreateOrderRequest
            {
                Type = "serialisationaggregation", // Тип операции по умолчанию
                Gtin = Gtin,
                Sku = string.IsNullOrWhiteSpace(Sku) ? null : Sku,
                Count = string.IsNullOrWhiteSpace(Count) ? null : Count,
                LineId = string.IsNullOrWhiteSpace(LineId) ? null : LineId,
                EquipmentTaskCreationMode = EquipmentTaskMode,
                Organisation = new Organisation
                {
                    Inn = Inn,
                    Kpp = Kpp
                },
                ProductSeries = new ProductSeries
                {
                    LotNumber = LotNumber,
                    ProductDate = ProductDate.ToString("dd.MM.yyyy"),
                    ExpDate = ExpDate.ToString("dd.MM.yyyy")
                }
            };

            // Отправляем запрос
            var response = await _orderApiService.CreateOrderAsync(request);

            if (response.IsSuccess)
            {
                OrderId = response.OrderId!;
                StatusMessage = $"Заказ успешно создан! ID: {OrderId}";
                await Shell.Current.DisplayAlert("Успех", $"Заказ создан с ID: {OrderId}", "OK");
            }
            else
            {
                StatusMessage = $"Ошибка: {response.Error}";
                await Shell.Current.DisplayAlert("Ошибка", response.Error ?? "Неизвестная ошибка", "OK");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Исключение: {ex.Message}";
            await Shell.Current.DisplayAlert("Ошибка", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }
}