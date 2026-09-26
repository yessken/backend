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
        CreateTable(db, "OrganizerSubscriptions", """
            CREATE TABLE IF NOT EXISTS OrganizerSubscriptions (
                TelegramUserId INTEGER NOT NULL PRIMARY KEY, Plan TEXT NOT NULL DEFAULT 'starter',
                Status TEXT NOT NULL DEFAULT 'inactive', ExpiresAt TEXT NULL,
                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);
        CreateTable(db, "TicketCheckIns", """
            CREATE TABLE IF NOT EXISTS TicketCheckIns (
                Id TEXT NOT NULL PRIMARY KEY, TicketId TEXT NOT NULL,
                CheckedByTelegramUserId INTEGER NOT NULL, CheckedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """);

        AddColumnIfMissing(db, "Events", "OrganizerTelegramId", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Events", "OrganizerEmail", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Events", "OrganizerPhone", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Events", "Status", "TEXT NOT NULL DEFAULT 'approved'");
        AddColumnIfMissing(db, "Events", "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddColumnIfMissing(db, "Tickets", "PaymentMethod", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(db, "Tickets", "PaymentStatus", "TEXT NOT NULL DEFAULT 'pending'");
        AddColumnIfMissing(db, "Tickets", "TelegramUserId", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Tickets", "PaymentReference", "TEXT NULL");
        AddColumnIfMissing(db, "Tickets", "TelegramStarsAmount", "INTEGER NULL");
        AddColumnIfMissing(db, "Tickets", "TelegramPaymentChargeId", "TEXT NULL");
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
        AddColumnIfMissing(db, "OrganizerSubscriptions", "LastTelegramChargeId", "TEXT NULL");

        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_PromoCodes_Code ON PromoCodes (Code)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PromoRedemptions_Code_User ON PromoRedemptions (PromoCodeId, TelegramUserId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_FunnelEvents_Name_Event_Ref ON FunnelEvents (Name, EventId, Ref)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_TicketCheckIns_TicketId ON TicketCheckIns (TicketId)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Tickets_TelegramPaymentChargeId ON Tickets (TelegramPaymentChargeId)");
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
                "LastTelegramChargeId" TEXT NULL
            )
            """);
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"TelegramStarsAmount\" INTEGER NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"Tickets\" ADD COLUMN IF NOT EXISTS \"TelegramPaymentChargeId\" TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE \"OrganizerSubscriptions\" ADD COLUMN IF NOT EXISTS \"LastTelegramChargeId\" TEXT NULL");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Tickets_TelegramPaymentChargeId\" ON \"Tickets\" (\"TelegramPaymentChargeId\")");
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
