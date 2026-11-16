-- Additive migration for an existing SQLite vinha.db. Back up the DB before execution.
-- Run manually against a COPY first; do not execute automatically at app startup.
CREATE TABLE IF NOT EXISTS "CustomerAccounts" (
  "Id" INTEGER NOT NULL CONSTRAINT "PK_CustomerAccounts" PRIMARY KEY AUTOINCREMENT,
  "FullName" TEXT NOT NULL, "Email" TEXT NOT NULL, "PasswordHash" TEXT NOT NULL,
  "Phone" TEXT NULL, "DefaultAddress" TEXT NULL, "CreatedAt" TEXT NOT NULL, "Active" INTEGER NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CustomerAccounts_Email" ON "CustomerAccounts" ("Email");
ALTER TABLE "Orders" ADD COLUMN "CustomerAccountId" INTEGER NULL REFERENCES "CustomerAccounts"("Id") ON DELETE SET NULL;
ALTER TABLE "Orders" ADD COLUMN "FulfillmentType" TEXT NOT NULL DEFAULT 'Tại quán';
ALTER TABLE "Orders" ADD COLUMN "DeliveryAddress" TEXT NULL;
ALTER TABLE "Orders" ADD COLUMN "DeliveryPhone" TEXT NULL;
ALTER TABLE "Orders" ADD COLUMN "DriverNote" TEXT NULL;
ALTER TABLE "Orders" ADD COLUMN "RejectReason" TEXT NULL;
ALTER TABLE "Orders" ADD COLUMN "StockDeducted" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "Orders" ADD COLUMN "StockDeductedAt" TEXT NULL;
ALTER TABLE "StockMovements" ADD COLUMN "OperationKey" TEXT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_StockMovements_OperationKey" ON "StockMovements" ("OperationKey");
CREATE INDEX IF NOT EXISTS "IX_Orders_CustomerAccountId_OpenedAt" ON "Orders" ("CustomerAccountId", "OpenedAt");
ALTER TABLE "Orders" ADD COLUMN "PaymentMethod" TEXT NOT NULL DEFAULT 'COD';
ALTER TABLE "Orders" ADD COLUMN "PaymentStatus" TEXT NOT NULL DEFAULT 'Chưa thanh toán';
