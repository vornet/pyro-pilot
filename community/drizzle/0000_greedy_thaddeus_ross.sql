CREATE TABLE `firework_submissions` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`tracking_id` text NOT NULL,
	`status` text DEFAULT 'pending' NOT NULL,
	`manufacturer` text NOT NULL,
	`product_name` text NOT NULL,
	`product_code` text,
	`upc` text,
	`category` text NOT NULL,
	`duration_ms` integer NOT NULL,
	`burst_shape` text,
	`description` text,
	`source_url` text NOT NULL,
	`contributor_name` text,
	`contributor_email` text,
	`duplicate_key` text NOT NULL,
	`created_at` text DEFAULT CURRENT_TIMESTAMP NOT NULL,
	`reviewed_at` text,
	`reviewer_notes` text
);
--> statement-breakpoint
CREATE UNIQUE INDEX `firework_submissions_tracking_id_unique` ON `firework_submissions` (`tracking_id`);--> statement-breakpoint
CREATE INDEX `idx_firework_submissions_status_created_at` ON `firework_submissions` (`status`,`created_at`);--> statement-breakpoint
CREATE INDEX `idx_firework_submissions_duplicate_key` ON `firework_submissions` (`duplicate_key`);