using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace QuanLyNhaHang.Data;

/// <summary>
/// Bổ sung an toàn các bảng/cột được thêm sau khi database SQLite đã được tạo.
/// Không xóa database, không xóa dữ liệu; mọi thay đổi đều có kiểm tra trước.
/// Với thay đổi schema lớn hơn, hãy tạo migration EF Core chính thức.
/// </summary>
public static class SqliteSchemaRepair
{
    public static async Task ApplyAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlite()) return;
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await Exec(connection, """
                CREATE TABLE IF NOT EXISTS "StaffAccounts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_StaffAccounts" PRIMARY KEY AUTOINCREMENT,
                    "Email" TEXT NOT NULL,
                    "FullName" TEXT NOT NULL,
                    "PasswordHash" TEXT NOT NULL,
                    "Role" INTEGER NOT NULL DEFAULT 0,
                    "BranchId" INTEGER NOT NULL DEFAULT 1,
                    "Active" INTEGER NOT NULL DEFAULT 1,
                    "CreatedAt" TEXT NOT NULL
                );
                """);
            await Exec(connection, """CREATE UNIQUE INDEX IF NOT EXISTS "IX_StaffAccounts_Email" ON "StaffAccounts" ("Email");""");

            await Exec(connection, """
                CREATE TABLE IF NOT EXISTS "CustomerAccounts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_CustomerAccounts" PRIMARY KEY AUTOINCREMENT,
                    "FullName" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "PasswordHash" TEXT NOT NULL,
                    "Phone" TEXT NOT NULL DEFAULT '',
                    "DefaultAddress" TEXT NOT NULL DEFAULT '',
                    "CreatedAt" TEXT NOT NULL,
                    "Active" INTEGER NOT NULL DEFAULT 1
                );
                """);
            await Exec(connection, """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CustomerAccounts_Email" ON "CustomerAccounts" ("Email");""");

            // Các cột online/kho được thêm vào các bản database cũ. Chỉ thêm nếu thiếu.
            await AddColumnIfTableExists(connection, "Orders", "CustomerAccountId", "INTEGER NULL");
            await AddColumnIfTableExists(connection, "Orders", "FulfillmentType", "TEXT NOT NULL DEFAULT 'Tại quán'");
            await AddColumnIfTableExists(connection, "Orders", "DeliveryAddress", "TEXT NULL");
            await AddColumnIfTableExists(connection, "Orders", "DeliveryPhone", "TEXT NULL");
            await AddColumnIfTableExists(connection, "Orders", "DriverNote", "TEXT NULL");
            await AddColumnIfTableExists(connection, "Orders", "RejectReason", "TEXT NULL");
            await AddColumnIfTableExists(connection, "Orders", "StockDeducted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfTableExists(connection, "Orders", "StockDeductedAt", "TEXT NULL");
            await AddColumnIfTableExists(connection, "Orders", "PaymentMethod", "TEXT NOT NULL DEFAULT 'COD'");
            await AddColumnIfTableExists(connection, "Orders", "PaymentStatus", "TEXT NOT NULL DEFAULT 'Chưa thanh toán'");
            await AddColumnIfTableExists(connection, "StockMovements", "OperationKey", "TEXT NULL");
            if (await TableExists(connection, "StockMovements"))
                await Exec(connection, """CREATE UNIQUE INDEX IF NOT EXISTS "IX_StockMovements_OperationKey" ON "StockMovements" ("OperationKey");""");
            if (await TableExists(connection, "Orders"))
                await Exec(connection, """CREATE INDEX IF NOT EXISTS "IX_Orders_CustomerAccountId_OpenedAt" ON "Orders" ("CustomerAccountId", "OpenedAt");""");
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task AddColumnIfTableExists(DbConnection c, string table, string column, string definition)
    {
        if (!await TableExists(c, table)) return;
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        }
        await reader.CloseAsync();
        await Exec(c, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};");
    }

    private static async Task<bool> TableExists(DbConnection c, string table)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1;";
        var p = cmd.CreateParameter(); p.ParameterName = "$name"; p.Value = table; cmd.Parameters.Add(p);
        return await cmd.ExecuteScalarAsync() != null;
    }

    private static async Task Exec(DbConnection c, string sql)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
