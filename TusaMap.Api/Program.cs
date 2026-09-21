using TusaMap.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://*:{port}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IEventsStore, EventsStore>();
builder.Services.AddSingleton<ITicketsStore, TicketsStore>();
builder.Services.AddSingleton<ITelegramAuthService, TelegramAuthService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        if (origins.Length == 0)
            throw new InvalidOperationException("Configure at least one Cors:Origins value before starting the API.");

        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .WithHeaders("Content-Type", "X-Telegram-Init-Data");
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseSwagger().UseSwaggerUI();

app.UseCors();
app.MapControllers();

app.Run();
