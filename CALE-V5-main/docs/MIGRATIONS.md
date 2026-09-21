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

### Render (Mi CALE)

1. Take a backup: Render → PostgreSQL → **…** → create backup / export.
2. Confirm the live schema roughly matches `InitialCreate` (app already works = schema exists).
3. Apply history (pick one):

   **A — Script (local)**  
   Copy the **External Database URL** from Render → PostgreSQL → Connect, then:

   ```powershell
   $env:CALE_DATABASE_URL = 'postgresql://USER:PASSWORD@HOST:5432/DBNAME'
   .\scripts\BASELINE_PROD.ps1
   ```

   **B — Render PSQL / Shell**  
   Paste and run `scripts/baseline-ef-migrations.sql`.

4. Render → Web Service **MICALE** → Environment:

```env
Database__UseEfMigrations=true
Database__AllowEnsureCreated=false
Database__ApplyFeatureSchema=false
```

5. **Manual Deploy** (clear build cache) and verify `https://micale.onrender.com/api/health`.

### SQL (same as the script)

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

## Empty database (greenfield)

```env
Database__UseEfMigrations=true
Database__AllowEnsureCreated=false
Database__ApplyFeatureSchema=false
```

Migrate will create the full schema on first boot.
