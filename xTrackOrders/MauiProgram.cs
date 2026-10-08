using Microsoft.Extensions.Logging;
using xTrackOrders.Services;
using xTrackOrders.ViewModels;
using xTrackOrders.Views;

namespace xTrackOrders
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            // ВАЖНО: Замените эти значения на реальные данные вашего сервера
            var baseUrl = "http://your-server-name:port";
            var username = "your_username";
            var password = "your_password";

            // Регистрация API-сервиса как Singleton
            builder.Services.AddSingleton<IOrderApiService>(sp =>
                new OrderApiService(baseUrl, username, password));

            // Регистрация ViewModel и страниц (пример)
            builder.Services.AddTransient<OrderViewModel>();
            builder.Services.AddTransient<OrderPage>();


#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
