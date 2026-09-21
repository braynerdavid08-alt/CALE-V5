-- Baseline an existing production Postgres DB for EF Core InitialCreate.
-- Run AFTER backup and BEFORE setting Database__UseEfMigrations=true.
-- See docs/MIGRATIONS.md.

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
  "MigrationId" character varying(150) NOT NULL,
  "ProductVersion" character varying(32) NOT NULL,
  CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260921123944_InitialCreate', '8.0.11')
ON CONFLICT DO NOTHING;
