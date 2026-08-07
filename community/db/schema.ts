import { sql } from "drizzle-orm";
import { index, integer, sqliteTable, text } from "drizzle-orm/sqlite-core";

export const fireworkSubmissions = sqliteTable(
  "firework_submissions",
  {
    id: integer("id").primaryKey({ autoIncrement: true }),
    trackingId: text("tracking_id").notNull().unique(),
    status: text("status", { enum: ["pending", "approved", "rejected"] }).notNull().default("pending"),
    manufacturer: text("manufacturer").notNull(),
    productName: text("product_name").notNull(),
    productCode: text("product_code"),
    upc: text("upc"),
    category: text("category").notNull(),
    durationMs: integer("duration_ms").notNull(),
    burstShape: text("burst_shape"),
    description: text("description"),
    sourceUrl: text("source_url").notNull(),
    contributorName: text("contributor_name"),
    contributorEmail: text("contributor_email"),
    duplicateKey: text("duplicate_key").notNull(),
    createdAt: text("created_at").notNull().default(sql`CURRENT_TIMESTAMP`),
    reviewedAt: text("reviewed_at"),
    reviewerNotes: text("reviewer_notes"),
  },
  (table) => [
    index("idx_firework_submissions_status_created_at").on(table.status, table.createdAt),
    index("idx_firework_submissions_duplicate_key").on(table.duplicateKey),
  ],
);
