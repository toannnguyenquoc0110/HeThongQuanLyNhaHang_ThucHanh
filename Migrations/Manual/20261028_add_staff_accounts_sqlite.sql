-- Bổ sung bảng tài khoản nhân viên cho SQLite hiện có; sao lưu database trước khi chạy.
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
CREATE UNIQUE INDEX IF NOT EXISTS "IX_StaffAccounts_Email" ON "StaffAccounts" ("Email");
