ALTER TABLE "PurchaseOrders" ADD COLUMN IF NOT EXISTS "ExpectedAt" timestamp NULL;
ALTER TABLE "PurchaseOrders" ADD COLUMN IF NOT EXISTS "Auto" boolean NOT NULL DEFAULT false;
