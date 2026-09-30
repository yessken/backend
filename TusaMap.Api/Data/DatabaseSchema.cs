using Microsoft.EntityFrameworkCore;
using System.Data;

namespace TusaMap.Api.Data;

public static class DatabaseSchema
{
    public static void EnsureCompatible(TusaMapDbContext db)
    {
        if (db.Database.IsNpgsql())
        {
            EnsurePostgresCompatible(db);
            return;
        }
        if (!db.Database.IsSqlite()) return;
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        CreateTable(db, "TicketCategories", """
            CREATE TABLE IF NOT EXISTS TicketCategories (
                Id TEXT NOT NULL PRIMARY KEY, EventId TEXT NOT NULL, Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '', Price TEXT NOT NULL DEFAULT '0',
                Capacity INTEGER NOT NULL DEFAULT 0, Sold INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);
        CreateTable(db, "PromoCodes", """
            CREATE TABLE IF NOT EXISTS PromoCodes (
                Id TEXT NOT NULL PRIMARY KEY, Code TEXT NOT NULL, EventId TEXT NULL,
                TicketCategoryId TEXT NULL, DiscountType TEXT NOT NULL, Value TEXT NOT NULL DEFAULT '0',
                MaxUses INTEGER NULL, UsedCount INTEGER NOT NULL DEFAULT 0, PerUserLimit INTEGER NOT NULL DEFAULT 1,
                StartsAt TEXT NULL, ExpiresAt TEXT NULL, IsActive INTEGER NOT NULL DEFAULT 1
            )
            """);
        CreateTable(db, "PromoRedemptions", """
            CREATE TABLE IF NOT EXISTS PromoRedemptions (
                Id TEXT NOT NULL PRIMARY KEY, PromoCodeId TEXT NOT NULL, TelegramUserId INTEGER NOT NULL,
                TicketId TEXT NOT NULL, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);
        CreateTable(db, "FunnelEvents", """
            CREATE TABLE IF NOT EXISTS FunnelEvents (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL,
                EventId TEXT NULL, Ref TEXT NULL, TelegramUserId INTEGER NULL,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);
        CreateTable(db, "EventInterests", """
            CREATE TABLE IF NOT EXISTS EventInterests (
                EventId TEXT NOT NULL, TelegramUserId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (EventId, TelegramUserId)
            )
            """);
        CreateTable(db, "EventParticipations", """
            CREATE TABLE IF NOT EXISTS EventParticipations (
                EventId TEXT NOT NULL, TelegramUserId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (EventId, TelegramUserId)
            )
            """);
        CreateTable(db, "OrganizerSubscriptions", """
            CREATE TABLE IF NOT EXISTS OrganizerSubscriptions (
                TelegramUserId INTEGER NOT NULL PRIMARY KEY, Plan TEXT NOT NULL DEFAULT 'starter',
                Status TEXT NOT NULL DEFAULT 'inactive', ExpiresAt TEXT NULL,
                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                LastTelegramChargeId TEXT NULL, TermsAcceptedAt TEXT NULL, TermsVersion TEXT NULL
            )
            """);
        CreateTable(db, "TicketCheckIns", """
            CREATE TABLE IF NOT EXISTS TicketCheckIns (
                Id TEXT NOT NULL PRIMARY KEY, TicketId TEXT NOT NULL,
                CheckedByTelegramUserId INTEGER NOT NULL, CheckedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);
        CreateTable(db, "BotMessageLogs", """
            CREATE TABLE IF NOT EXISTS BotMessageLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, UpdateId INTEGER NOT NULL,
                MessageId INTEGER NOT NULL, ChatId INTEGER NOT NULL, TelegramUserId INTEGER NOT NULL,
                SenderName TEXT NOT NULL DEFAULT '', Username TEXT NULL, MessageType TEXT NOT NULL DEFAULT 'text',
                Content TEXT NOT NULL DEFAULT '', ReceivedAt TEXT NOT NULL
            )
            """);

        AddColumnIfMissing(db, "Events", "OrganizerTelegramId", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Events", "OrganizerEmail", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Events", "OrganizerPhone", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Events", "Status", "TEXT NOT NULL DEFAULT 'approved'");
        AddColumnIfMissing(db, "Events", "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddColumnIfMissing(db, "Events", "AddressIsPrivate", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Events", "AddressRevealAt", "TEXT NULL");
        AddColumnIfMissing(db, "Events", "PrivateAddress", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Events", "IsDemo", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Tickets", "PaymentMethod", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Tickets", "PaymentStatus", "TEXT NOT NULL DEFAULT 'pending'");
        AddColumnIfMissing(db, "Tickets", "TelegramUserId", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Tickets", "PaymentReference", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "TelegramPaymentChargeId", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "ProviderPaymentChargeId", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "PrivateAddressSentAt", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "TermsAcceptedAt", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "TermsVersion", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "PaymentCheckoutAt", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "TicketCategoryId", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Tickets", "TicketCategoryName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Tickets", "Quantity", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(db, "Tickets", "BaseAmount", "TEXT NOT NULL DEFAULT '0'");
        AddColumnIfMissing(db, "Tickets", "DiscountAmount", "TEXT NOT NULL DEFAULT '0'");
        AddColumnIfMissing(db, "Tickets", "CommissionAmount", "TEXT NOT NULL DEFAULT '0'");
        AddColumnIfMissing(db, "Tickets", "TotalAmount", "TEXT NOT NULL DEFAULT '0'");
        AddColumnIfMissing(db, "Tickets", "PromoCode", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "RefundStatus", "TEXT NOT NULL DEFAULT 'none'");
        AddColumnIfMissing(db, "Tickets", "CancelledAt", "TEXT NULL");
        AddColumnIfMissing(db, "FunnelEvents", "VisitorId", "TEXT NULL");
        AddColumnIfMissing(db, "OrganizerSubscriptions", "LastTelegramChargeId", "TEXT NULL");
        AddColumnIfMissing(db, "OrganizerSubscriptions", "TermsAcceptedAt", "TEXT NULL");
        AddColumnIfMissing(db, "OrganizerSubscriptions", "TermsVersion", "TEXT NULL");
        AddColumnIfMissing(db, "EventInterests", "TicketAvailabilityNotifiedAt", "TEXT NULL");

        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_PromoCodes_Code ON PromoCodes (Code)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PromoRedemptions_Code_User ON PromoRedemptions (PromoCodeId, TelegramUserId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FunnelEvents_Name_Event_Ref ON FunnelEvents (Name, EventId, Ref)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_TicketCheckIns_TicketId ON TicketCheckIns (TicketId)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_BotMessageLogs_UpdateId ON BotMessageLogs (UpdateId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_EventInterests_EventId ON EventInterests (EventId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_EventParticipations_EventId ON EventParticipations (EventId)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Tickets_TelegramPaymentChargeId ON Tickets (TelegramPaymentChargeId)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Tickets_ProviderPaymentChargeId ON Tickets (ProviderPaymentChargeId)");
        db.Database.ExecuteSqlRaw("UPDATE Events SET IsDemo = 1 WHERE Id IN ('1','2','3','4') AND OrganizerTelegramId = 0");
    }

    private static void EnsurePostgresCompatible(TusaMapDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "OrganizerSubscriptions" (
                "TelegramUserId" BIGINT PRIMARY KEY,
                "Plan" TEXT NOT NULL DEFAULT 'starter',
                "Status" TEXT NOT NULL DEFAULT 'inactive',
                "ExpiresAt" TIMESTAMPTZ NULL,
                "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "LastTelegramChargeId" TEXT NULL,
                "TermsAcceptedAt" TIMESTAMPTZ NULL,
                "TermsVersion" TEXT NULL
            )
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "BotMessageLogs" (
                "Id" BIGSERIAL PRIMARY KEY, "UpdateId" BIGINT NOT NULL,
                "MessageId" BIGINT NOT NULL, "ChatId" BIGINT NOT NULL, "TelegramUserId" BIGINT NOT NULL,
                "SenderName" VARCHAR(160) NOT NULL DEFAULT '', "Username" VARCHAR(64) NULL,
                "MessageType" VARCHAR(40) NOT NULL DEFAULT 'text', "Content" VARCHAR(4096) NOT NULL DEFAULT '',
                "ReceivedAt" TIMESTAMPTZ NOT NULL
            )
            """);
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_BotMessageLogs_UpdateId\" ON \"BotMessageLogs\" (\"UpdateId\")");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"TelegramPaymentChargeId\" TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"ProviderPaymentChargeId\" TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"PrivateAddressSentAt\" TIMESTAMPTZ NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"TermsAcceptedAt\" TIMESTAMPTZ NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"TermsVersion\" TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"PaymentCheckoutAt\" TIMESTAMPTZ NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"OrganizerSubscriptions\" ADD COLUMN IF NOT EXISTS \"LastTelegramChargeId\" TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"OrganizerSubscriptions\" ADD COLUMN IF NOT EXISTS \"TermsAcceptedAt\" TIMESTAMPTZ NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"OrganizerSubscriptions\" ADD COLUMN IF NOT EXISTS \"TermsVersion\" TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Events\" ADD COLUMN IF NOT EXISTS \"AddressIsPrivate\" BOOLEAN NOT NULL DEFAULT FALSE");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Events\" ADD COLUMN IF NOT EXISTS \"AddressRevealAt\" TIMESTAMPTZ NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Events\" ADD COLUMN IF NOT EXISTS \"PrivateAddress\" TEXT NOT NULL DEFAULT ''");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Events\" ADD COLUMN IF NOT EXISTS \"IsDemo\" BOOLEAN NOT NULL DEFAULT FALSE");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"FunnelEvents\" ADD COLUMN IF NOT EXISTS \"VisitorId\" TEXT NULL");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"EventInterests\" (\"EventId\" TEXT NOT NULL, \"TelegramUserId\" BIGINT NOT NULL, \"CreatedAt\" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (\"EventId\", \"TelegramUserId\"))");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_EventInterests_EventId\" ON \"EventInterests\" (\"EventId\")");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"EventInterests\" ADD COLUMN IF NOT EXISTS \"TicketAvailabilityNotifiedAt\" TIMESTAMPTZ NULL");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_EventInterests_EventId_TicketAvailabilityNotifiedAt\" ON \"EventInterests\" (\"EventId\", \"TicketAvailabilityNotifiedAt\")");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"EventParticipations\" (\"EventId\" TEXT NOT NULL, \"TelegramUserId\" BIGINT NOT NULL, \"CreatedAt\" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (\"EventId\", \"TelegramUserId\"))");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_EventParticipations_EventId\" ON \"EventParticipations\" (\"EventId\")");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Tickets_TelegramPaymentChargeId\" ON \"Tickets\" (\"TelegramPaymentChargeId\")");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Tickets_ProviderPaymentChargeId\" ON \"Tickets\" (\"ProviderPaymentChargeId\")");
        db.Database.ExecuteSqlRaw("UPDATE \"Events\" SET \"IsDemo\" = TRUE WHERE \"Id\" IN ('1','2','3','4') AND \"OrganizerTelegramId\" = 0");
    }

    private static void CreateTable(TusaMapDbContext db, string _, string sql) => db.Database.ExecuteSqlRaw(sql);

    private static void AddColumnIfMissing(TusaMapDbContext db, string table, string column, string definition)
    {
        using (var columns = db.Database.GetDbConnection().CreateCommand())
        {
            columns.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var reader = columns.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
            }
        }
        using var alter = db.Database.GetDbConnection().CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
        alter.ExecuteNonQuery();
    }
}
