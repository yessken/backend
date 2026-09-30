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

## Подключение своего Telegram Mini App

1. Создай своего бота в [@BotFather](https://t.me/BotFather); токен никому не пересылай и не добавляй в frontend.
2. Задеплой Mini App на HTTPS. В BotFather привяжи domain и укажи URL Mini App/menu button на frontend origin.
3. Получи свой числовой Telegram user ID для admin allowlist и создай отдельный случайный webhook secret. Bot API допускает в `secret_token` только латинские буквы, цифры, `_`, `-`; не используй bot token как webhook secret.
4. Находясь в каталоге `TusaMap.Api`, задай настройки локально через User Secrets (подставь свои значения в терминале):

    ```powershell
    dotnet user-secrets set "Telegram:BotToken" "<TOKEN_FROM_BOTFATHER>"
    dotnet user-secrets set "Telegram:WebAppUrl" "https://<your-mini-app-host>"
    dotnet user-secrets set "Telegram:WebhookUrl" "https://<your-api-host>/api/telegram/webhook"
    dotnet user-secrets set "Telegram:WebhookSecret" "<RANDOM_WEBHOOK_SECRET>"
    dotnet user-secrets set "Telegram:AdminUserIds:0" "<YOUR_NUMERIC_TELEGRAM_ID>"
    ```

5. Запусти API в `Development`: при старте он зарегистрирует Mini App menu button и webhook. Telegram не сможет обратиться к `localhost`, поэтому для локального тестирования нужен публичный HTTPS tunnel.
6. В frontend передавай исходный `Telegram.WebApp.initData` целиком в заголовке `X-Telegram-Init-Data`. Сервер проверяет HMAC подпись bot token и свежесть `auth_date`; не доверяй `initDataUnsafe`, ID или роли, присланным из UI.

В production задай те же параметры через secret/environment-variable manager хостинга (`Telegram__BotToken`, `Telegram__WebAppUrl`, `Telegram__WebhookUrl`, `Telegram__WebhookSecret`, `Telegram__AdminUserIds__0`). Никогда не коммить credentials в `appsettings*.json` или Git. `Telegram:AdminUserIds` управляет admin-only endpoints.

Для production используй стабильный HTTPS API hostname. Cloudflare Quick Tunnel выдаёт временное имя: при смене hostname обнови `Telegram:WebhookUrl` и frontend production `apiUrl`, перезапусти API и заново разверни frontend. Этот backend содержит TUSA-specific event seeder и сценарии; перед использованием как собственный starter замени бренд, TUSA event/payment defaults и модерационные правила.

## Эндпоинты

- `GET /api/events` — список мероприятий (query: `category`)
- `GET /api/events/{id}` — мероприятие по id
- `GET /api/events/{id}/going` — число отметок участия и состояние текущего пользователя
- `POST /api/events/{id}/going` — переключить участие; требуется проверенный Telegram `initData`
- `POST /api/events/{id}/ticket-categories` — добавить тариф билета владельцу события или admin
- `POST /api/events/{id}/promo-codes` — добавить промокод владельцу события или admin
- `POST /api/events` — отправить мероприятие на модерацию (заголовок `X-Telegram-Init-Data`)
- `POST /api/events/public` — демо-форма заявки без Telegram-авторизации; заявка остаётся `pending`, публично не появляется и требует ручной проверки; ограничение — 5 заявок за 10 минут на API-видимый IP
- `GET /api/events/pending` — список заявок для admin Telegram ID
- `POST /api/events/{id}/approve` — одобрить событие для admin Telegram ID
- `POST /api/events/{id}/reject` — отклонить ожидающую заявку для admin Telegram ID
- `GET /api/events/{id}/going` и `POST /api/events/{id}/going` — счётчик участия и переключение RSVP для проверенного Telegram-пользователя
- `GET /api/tickets/me` — мои билеты (заголовок `X-Telegram-Init-Data`)
- `POST /api/tickets` и `/api/tickets/public` — отключены, чтобы не создавать неоплаченные заказы. Билет оформляется через Telegram invoice.
- `POST /api/tickets/quote` — проверить цену, категорию, лимит и промокод до создания заказа
- `POST /api/payments/webhook` — legacy endpoint отключён (410); event ticket payments подтверждаются только через проверенный Telegram `successful_payment` update.
- `POST /api/telegram/webhook` — Telegram updates; `/terms` и `/paysupport` доступны пользователям. Офлайн-билет оплачивается invoice в KZT через стороннего Telegram provider token; Stars допускаются только для Organizer Pro.
- `/start event_tusa-2026` — показывает условия и создаёт KZT invoice только после явного согласия, при настроенных цене и provider token.
- `/start subscribe_pro` — opens the Organizer Pro subscription invoice when `Payments__TelegramSubscriptionStars` is configured
- `/organizerpro` — показать условия и приобрести/продлить Organizer Pro за Stars, если цена настроена.
- `/refundstars <Telegram_ID>` — admin-only возврат последней Organizer Pro оплаты через Telegram Stars API.

После отправки заявки администраторы получают Telegram-уведомление (если заданы bot token и `Telegram:AdminUserIds`). Очередь доступна в Mini App: `/admin/event-review`. Решения разрешены только аккаунтам из серверного allowlist; повторно обработать уже закрытую заявку нельзя.

На карточке платного события без доступной оплаты кнопка «Сообщить, когда билеты появятся» открывает бота и записывает Telegram-пользователя в список уведомлений. После одобрения события или добавления доступного тарифа бот отправляет ему одну попытку уведомления; успешная отправка отмечается в базе. Это не резервирует билет и не гарантирует доставку, если пользователь заблокировал бота. RSVP («Буду участвовать») хранится отдельно от интереса/ожидания билетов и доступен только после проверки Telegram `initData`.

### Граница доступа

- `GET /api/events` и `GET /api/events/{id}` предназначены для публичного чтения каталога.
- Личные билеты, создание события через `POST /api/events`, quote заказа и organizer/admin endpoints должны вызываться с raw `initData`; admin дополнительно проверяется по allowlist `Telegram:AdminUserIds`.
- `POST /api/events/public` в этом демо оставлен открытым для прототипирования и **не проверяет личность автора**. Есть базовый лимит 5 заявок за 10 минут на API-видимый IP, но перед масштабным публичным запуском добавь captcha/антиспам либо закрой маршрут. За reverse proxy настрой доверенную передачу клиентского IP; не доверяй произвольному `X-Forwarded-For`. Не используй email/username из body как доказательство владельца.
- `POST /api/tickets/public` и `/api/tickets` не создают ticket orders: заказ создаёт и резервирует только Telegram invoice flow после проверки условий.

Для production CORS также следует сузить до конкретного frontend origin вместо разрешения произвольных origin.

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
