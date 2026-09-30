using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TusaMap.Api.Data;
using TusaMap.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
builder.WebHost.UseUrls($"http://*:{port}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-event-submissions", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

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
builder.Services.AddScoped<IEventInterestNotifier, EventInterestNotifier>();
builder.Services.AddSingleton<ITelegramAuthService, TelegramAuthService>();
builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();
builder.Services.AddHostedService<PrivateVenueNotifier>();
builder.Services.AddHostedService<PendingTelegramTicketCleanupService>();

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
    Tusa2026EventSeeder.Ensure(db, builder.Configuration);
}

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ITelegramBotService>().ConfigureWebAppAsync();
    await scope.ServiceProvider.GetRequiredService<ITelegramBotService>().ConfigureWebhookAsync();
}

if (app.Environment.IsDevelopment())
    app.UseSwagger().UseSwaggerUI();

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();

app.Run();
