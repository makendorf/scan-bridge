using Scalar.AspNetCore;
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

    builder.Services.AddControllers();
    // Регистрируем HttpClientFactory для выполнения внешних HTTP-запросов
    builder.Services.AddHttpClient();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    app.UseCors("AllowAll");

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