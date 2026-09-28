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
- `POST /api/tickets` и `/api/tickets/public` — отключены, чтобы не создавать неоплаченные заказы. Билет оформляется через Telegram invoice.
- `POST /api/tickets/quote` — проверить цену, категорию, лимит и промокод до создания заказа
- `POST /api/payments/webhook` — legacy endpoint отключён (410); event ticket payments подтверждаются только через проверенный Telegram `successful_payment` update.
- `POST /api/telegram/webhook` — Telegram updates; `/terms` и `/paysupport` доступны пользователям. Офлайн-билет оплачивается invoice в KZT через стороннего Telegram provider token; Stars допускаются только для Organizer Pro.
- `/start event_tusa-2026` — показывает условия и создаёт KZT invoice только после явного согласия, при настроенных цене и provider token.
- `/start subscribe_pro` — opens the Organizer Pro subscription invoice when `Payments__TelegramSubscriptionStars` is configured
- `/refundstars <Telegram_ID>` — admin-only возврат последней Organizer Pro оплаты через Telegram Stars API.

Для production задай:

- `ConnectionStrings__Default` — PostgreSQL connection string;
- `Telegram__BotToken` — токен бота;
- `Telegram__AdminUserIds__0` — Telegram ID администратора;
- `Payments__TelegramSubscriptionStars` — fixed Telegram Stars price for 30 days of Organizer Pro (keep `0` until the price is decided);
- `Payments__TelegramPhysicalProviderToken` — токен физического платёжного провайдера, подключённого в BotFather; хранить только в User Secrets или secret store.
- `Events__Tusa2026__TicketPriceKzt` — базовая цена билета в тенге до комиссии TUSA 10%; `0` оставляет продажи выключенными.
- `Events__Tusa2026__Capacity` — лимит мест (по умолчанию 100).
- `Telegram__WebhookSecret` — secret used when registering the Telegram webhook;
- `Cors__Origins__0` — разрешённый frontend origin.

Telegram must offer a provider available to the merchant in Kazakhstan and able to process KZT. Keep the provider token in User Secrets, not appsettings or Git. Tickets are reserved for 15 minutes after invoice creation; an unfinished pre-checkout expires after 5 minutes. Payments arriving after expiry are flagged for support/manual refund through the provider portal. Organizer Pro is digital and keeps Stars checkout with prior terms consent and `/paysupport`.

For local setup, obtain a physical-goods provider token under BotFather → `/mybots` → bot → Bot Settings → Payments, then set `Payments:TelegramPhysicalProviderToken` and `Events:Tusa2026:TicketPriceKzt` in User Secrets and restart the API. First confirm that the selected provider is available for this merchant and KZT in Kazakhstan. Do not enable the event category until a test invoice/payment has been completed and cancellation/refund procedures are known.
