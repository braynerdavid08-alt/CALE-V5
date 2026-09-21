# EF Core migrations (PostgreSQL)

Mi CALE now ships an initial EF Core migration generated against **PostgreSQL**
(`Persistence/Migrations/InitialCreate`).

## Local commands

```bash
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet ef migrations add <Name> --project src/Cale.Api --startup-project src/Cale.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Cale.Api --startup-project src/Cale.Api
```

Design-time factory: `Cale.Api/Persistence/CaleDbContextFactory.cs`.

## Startup flags

| Flag | Purpose |
|------|---------|
| `Database:UseEfMigrations` | When `true`, runs `Database.MigrateAsync()` and skips `EnsureCreated` / FeatureSchema |
| `Database:AllowEnsureCreated` | Legacy bootstrap for empty local DBs (dev only) |
| `Database:ApplyFeatureSchema` | Legacy ad-hoc DDL patches (dev only; ignored when migrations are on) |

Production defaults keep `UseEfMigrations=false` until the existing database is baselined.

## Existing production database (baseline)

Do **not** turn on `UseEfMigrations` until history is recorded, or Migrate will try to recreate tables.

1. Take a backup of Postgres.
2. Confirm the live schema roughly matches `InitialCreate`.
3. Insert the migration row (adjust id if the file name differs):

```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
  "MigrationId" character varying(150) NOT NULL,
  "ProductVersion" character varying(32) NOT NULL,
  CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260921123944_InitialCreate', '8.0.11')
ON CONFLICT DO NOTHING;
```

4. Set env:

```env
Database__UseEfMigrations=true
Database__AllowEnsureCreated=false
Database__ApplyFeatureSchema=false
```

5. Redeploy and verify `/health` (or startup logs show migrations applied / already up to date).

## Empty database (greenfield)

```env
Database__UseEfMigrations=true
Database__AllowEnsureCreated=false
Database__ApplyFeatureSchema=false
```

Migrate will create the full schema on first boot.
