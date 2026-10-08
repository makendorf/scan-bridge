using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using ScanBridgeExtention.Models;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() // Минимальный уровень логирования
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // Меньше шума от системных логов ASP.NET
    .WriteTo.Console() // Оставляем вывод в консоль
    .WriteTo.File(
        path: "logs/log-.txt",          // Путь к папке logs и префикс имени файла
        rollingInterval: RollingInterval.Day, // Создавать новый файл каждый день (например, log20260808.txt)
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", // Формат строки лога
        retainedFileCountLimit: 30      // Автоматически удалять логи старше 30 дней
    )
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    var connectionString = builder.Configuration.GetConnectionString("NationalCataloge");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
    builder.Services.AddControllers();
    // Регистрируем HttpClientFactory для выполнения внешних HTTP-запросов
    builder.Services.AddHttpClient();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            // 1. Центральный реестр описаний для всех тегов вашего API
            // Добавляйте новые контроллеры сюда, просто добавляя новую пару ключ-значение
            var tagDescriptions = new Dictionary<string, string>
            {
                {
                    "Национальный каталог",
                    """
                    Набор эндпоинтов для взаимодействия с реестром товаров. 
                    Поддерживает **умный поиск по вариациям GTIN** и оптимизированные пакетные запросы для высоконагруженных сценариев.
                    """
                },
                {
                    "xTrack Интеграция",
                    """
                    Эндпоинты для интеграции с внешней системой учета **xTrack**.
                    Позволяет создавать производственные задания (заказы) с автоматической подстановкой параметров оборудования и встроенной Basic-аутентификацией.
                    """
                }
                // Сюда можно легко добавить "Пользователи", "Отчеты" и т.д.
            };

            // 2. Гарантируем, что коллекция тегов инициализирована (защита от NullReferenceException)
            document.Tags ??= new HashSet<OpenApiTag>();

            // 3. Проходим по всем описаниям и либо обновляем существующий тег, либо добавляем новый
            foreach (var kvp in tagDescriptions)
            {
                var existingTag = document.Tags.FirstOrDefault(t => t.Name == kvp.Key);

                if (existingTag != null)
                {
                    existingTag.Description = kvp.Value;
                }
                else
                {
                    document.Tags.Add(new OpenApiTag
                    {
                        Name = kvp.Key,
                        Description = kvp.Value
                    });
                }
            }

            return Task.CompletedTask;
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    //if (app.Environment.IsDevelopment())
    //{
    //    app.MapOpenApi();
    //}
    app.MapOpenApi();
    app.MapScalarApiReference();


    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение было неожиданно остановлено");
}
finally
{
    Log.CloseAndFlush();
}