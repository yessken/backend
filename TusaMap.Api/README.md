# TusaMap API (.NET 10)

Бэкенд для TusaMap: мероприятия, билеты, авторизация через Telegram initData.

## Запуск

```bash
cd backend/TusaMap.Api
dotnet run
```

API: http://localhost:5001. Swagger: http://localhost:5001/swagger

## Локальный режим без сервера

Если `ConnectionStrings:Default` пустой, API использует SQLite-файл `tusamap.db` в папке приложения. Это подходит для разработки на личном компьютере: данные переживают перезапуск, но компьютер должен оставаться включённым.

Запуск:

```powershell
$env:PORT = "5001"
dotnet run --project .\TusaMap.Api\TusaMap.Api.csproj
```

Для frontend в `src/environments/environment.ts` используется `http://localhost:5001`.

> Telegram не сможет обратиться к `localhost` на твоём компьютере пользователя. Для теста внутри Telegram нужен HTTPS-туннель, например Cloudflare Tunnel, который направляет публичный URL на `localhost:5001`.

## Токен бота

Задай токен одним из способов:

- **User Secrets** (рекомендуется для разработки):
  ```bash
  dotnet user-secrets set "Telegram:BotToken" "ТВОЙ_ТОКЕН"
  ```
- **Переменная окружения**: `Telegram__BotToken`
- **appsettings.Development.json** (не коммить токен в репозиторий)

## Эндпоинты

- `GET /api/events` — список мероприятий (query: `category`)
- `GET /api/events/{id}` — мероприятие по id
- `POST /api/events/{id}/ticket-categories` — добавить тариф билета владельцу события или admin
- `POST /api/events/{id}/promo-codes` — добавить промокод владельцу события или admin
- `POST /api/events` — отправить мероприятие на модерацию (заголовок `X-Telegram-Init-Data`)
- `GET /api/events/pending` — список заявок для admin Telegram ID
- `POST /api/events/{id}/approve` — одобрить событие для admin Telegram ID
- `GET /api/tickets/me` — мои билеты (заголовок `X-Telegram-Init-Data`)
- `POST /api/tickets` — создать заказ со статусом `pending` (body: `{ "eventId": "1", "paymentMethod": "kaspi" }`)
- `POST /api/tickets/public` — legacy web order endpoint; web checkout now redirects to Telegram instead
- `POST /api/tickets/quote` — проверить цену, категорию, лимит и промокод до создания заказа
- `POST /api/payments/webhook` — подтвердить оплату секретным webhook-запросом; после этого создаётся QR-код
- `POST /api/telegram/webhook` — Telegram updates, включая web deep-link `/start event_{eventId}` and Stars payments
- `/start subscribe_pro` — opens the Organizer Pro subscription invoice when `Payments__TelegramSubscriptionStars` is configured

Для production задай:

- `ConnectionStrings__Default` — PostgreSQL connection string;
- `Telegram__BotToken` — токен бота;
- `Telegram__AdminUserIds__0` — Telegram ID администратора;
- `Payments__WebhookSecret` — секрет платёжного webhook;
- `Payments__TelegramStarsPerKzt` — conversion rate used for Telegram Stars invoices; set explicitly before enabling payments (for example, `0` keeps payment disabled);
- `Payments__TelegramSubscriptionStars` — fixed Telegram Stars price for 30 days of Organizer Pro (keep `0` until the price is decided);
- `Payments__TelegramTestMode` — when `true` in Development, event invoices cost exactly 1 Star for testing; keep `false` in production;
- `Telegram__WebhookSecret` — secret used when registering the Telegram webhook;
- `Cors__Origins__0` — разрешённый frontend origin.

The public web checkout deliberately does not create an anonymous order. It opens the bot with an event deep link, and the Telegram webhook creates the order only when it can create a Stars invoice. This prevents seats from being reserved by abandoned browser sessions.

### TUSA 2026 private venue

The launch event is configured for Friday, 2 October 2026 at 19:00 Astana time, with a 500 Stars ticket and a venue address reveal 24 hours before start. The venue address is intentionally not committed to configuration and is never included in public event API responses. On the API host, enter the final address on Thursday and restart the API:

```powershell
dotnet user-secrets set "Events:Tusa2026:PrivateAddress" "YOUR PRIVATE VENUE ADDRESS"
```

Do this before Thursday, 1 October at 19:00 Astana time. The background notifier sends the address only to paid, non-cancelled ticket holders. If the address is not configured by reveal time, the notifier keeps retrying after the address is added and the API restarts.
