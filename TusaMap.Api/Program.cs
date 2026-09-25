using Microsoft.EntityFrameworkCore;
using TusaMap.Api.Data;
using TusaMap.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
builder.WebHost.UseUrls($"http://*:{port}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = "Data Source=tusamap.db";

builder.Services.AddDbContext<TusaMapDbContext>(options =>
{
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString);
});
builder.Services.AddScoped<IEventsStore, EventsStore>();
builder.Services.AddScoped<ITicketsStore, TicketsStore>();
builder.Services.AddScoped<IUserStore, UserStore>();
builder.Services.AddScoped<ITicketPricingService, TicketPricingService>();
builder.Services.AddSingleton<ITelegramAuthService, TelegramAuthService>();
builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
          policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TusaMapDbContext>();
    db.Database.EnsureCreated();
    DatabaseSchema.EnsureCompatible(db);
}

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ITelegramBotService>().ConfigureWebAppAsync();
    await scope.ServiceProvider.GetRequiredService<ITelegramBotService>().ConfigureWebhookAsync();
}

if (app.Environment.IsDevelopment())
    app.UseSwagger().UseSwaggerUI();

app.UseCors();
app.MapControllers();

app.Run();
