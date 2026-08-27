using Microsoft.EntityFrameworkCore;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

public static class FeatureSchema
{
    public static async Task EnsureAsync(
        CaleDbContext db,
        CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SchoolProfiles" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SchoolProfiles" PRIMARY KEY AUTOINCREMENT,
                    "UserId" INTEGER NOT NULL,
                    "LegalName" TEXT NOT NULL,
                    "TaxId" TEXT NOT NULL,
                    "BillingEmail" TEXT NOT NULL,
                    "Phone" TEXT NOT NULL,
                    "Address" TEXT NOT NULL,
                    "City" TEXT NOT NULL,
                    "Department" TEXT NOT NULL,
                    "PlanCode" TEXT NOT NULL,
                    "PlanPriceCop" TEXT NOT NULL,
                    "SubscriptionStatus" TEXT NOT NULL,
                    "MembershipStartsAt" TEXT NULL,
                    "MembershipEndsAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchoolProfiles_UserId"
                    ON "SchoolProfiles" ("UserId");
                CREATE INDEX IF NOT EXISTS "IX_SchoolProfiles_TaxId"
                    ON "SchoolProfiles" ("TaxId");
                """,
                ct);

            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Usuarios" ADD COLUMN "SchoolId" INTEGER NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Usuarios" ADD COLUMN "UltimoAccesoEn" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Usuarios" ADD COLUMN "DebeCambiarClave" INTEGER NOT NULL DEFAULT 0;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Usuarios" ADD COLUMN "EmailConfirmado" INTEGER NOT NULL DEFAULT 1;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Usuarios" ADD COLUMN "EmailCodigoHash" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Usuarios" ADD COLUMN "EmailCodigoExpiraEn" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "MembershipStartsAt" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "MembershipEndsAt" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "RequestedPlanCode" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "PaymentProofUrl" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "PaymentReference" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "RejectionReason" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "RequestedAt" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "ProofSubmittedAt" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "LastDecisionAt" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "RenewalStatus" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "SuspensionReason" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "TeachersMaxOverride" INTEGER NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolProfiles" ADD COLUMN "StudentsMaxOverride" INTEGER NULL;""",
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "SchoolProfiles"
                SET "SubscriptionStatus" = 'UnderReview'
                WHERE "SubscriptionStatus" = 'PaymentSubmitted';
                """,
                ct);
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "SchoolProfiles"
                SET "RenewalStatus" = 'None'
                WHERE "RenewalStatus" IS NULL OR TRIM("RenewalStatus") = '';
                """,
                ct);
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "SchoolProfiles"
                SET "RenewalStatus" = CASE
                    WHEN "PaymentProofUrl" IS NOT NULL AND TRIM("PaymentProofUrl") != '' THEN 'UnderReview'
                    ELSE 'PendingPayment'
                END
                WHERE "SubscriptionStatus" = 'Active'
                  AND "RequestedPlanCode" IS NOT NULL
                  AND TRIM("RequestedPlanCode") != ''
                  AND ("RenewalStatus" IS NULL OR "RenewalStatus" = 'None');
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "MembershipEvents" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_MembershipEvents" PRIMARY KEY AUTOINCREMENT,
                    "SchoolUserId" INTEGER NOT NULL,
                    "EventType" TEXT NOT NULL,
                    "PlanCode" TEXT NULL,
                    "PlanPriceCop" TEXT NULL,
                    "ActorUserId" INTEGER NULL,
                    "Note" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_MembershipEvents_SchoolUserId"
                    ON "MembershipEvents" ("SchoolUserId");
                CREATE INDEX IF NOT EXISTS "IX_MembershipEvents_EventType"
                    ON "MembershipEvents" ("EventType");
                CREATE INDEX IF NOT EXISTS "IX_MembershipEvents_CreatedAt"
                    ON "MembershipEvents" ("CreatedAt");
                """,
                ct);

            // Concurrency: WAL allows readers during writers; busy_timeout retries locks.
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
            await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;", ct);
            await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct);

            await TrySqliteAsync(
                db,
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_RespuestasIntento_Attempt_Question"
                    ON "RespuestasIntento" ("IntentoId", "PreguntaId");
                """,
                ct);

            await TrySqliteAsync(
                db,
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Intentos_OpenExam"
                    ON "Intentos" ("UsuarioId", "ExamenId")
                    WHERE "FinEn" IS NULL AND "ExamenId" IS NOT NULL;
                """,
                ct);

            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Notificaciones" ADD COLUMN "LeidaEn" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Notificaciones" ADD COLUMN "Link" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Notificaciones" ADD COLUMN "Prioridad" TEXT NOT NULL DEFAULT 'normal';""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Notificaciones" ADD COLUMN "DedupeKey" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "Notificaciones" ADD COLUMN "Archivada" INTEGER NOT NULL DEFAULT 0;""",
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "NotificationPreferences" (
                    "UserId" INTEGER NOT NULL CONSTRAINT "PK_NotificationPreferences" PRIMARY KEY,
                    "AcademicEnabled" INTEGER NOT NULL,
                    "MembershipEnabled" INTEGER NOT NULL,
                    "AdminEnabled" INTEGER NOT NULL,
                    "SystemEnabled" INTEGER NOT NULL
                );
                """,
                ct);

            await TrySqliteAsync(
                db,
                """
                CREATE INDEX IF NOT EXISTS "IX_Notificaciones_User_Unread"
                    ON "Notificaciones" ("UsuarioId", "Leida", "Archivada");
                """,
                ct);

            await TrySqliteAsync(
                db,
                """
                CREATE TABLE IF NOT EXISTS "HomepageSettings" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_HomepageSettings" PRIMARY KEY,
                    "HeroBadge" TEXT NOT NULL,
                    "HeroTitle" TEXT NOT NULL,
                    "HeroTitleHighlight" TEXT NOT NULL,
                    "HeroDescription" TEXT NOT NULL,
                    "HeroCtaPrimaryLabel" TEXT NOT NULL,
                    "HeroCtaPrimaryPath" TEXT NOT NULL,
                    "HeroCtaSecondaryLabel" TEXT NOT NULL,
                    "HeroVideoUrl" TEXT NULL,
                    "HeroImageUrl" TEXT NULL,
                    "HeroImageUrlMobile" TEXT NULL,
                    "HeroImageAlt" TEXT NOT NULL,
                    "HeroImageEnabled" INTEGER NOT NULL,
                    "HeroVisible" INTEGER NOT NULL,
                    "BenefitsJson" TEXT NOT NULL,
                    "StepsJson" TEXT NOT NULL,
                    "StepsSectionTitle" TEXT NOT NULL,
                    "StepsSectionSubtitle" TEXT NOT NULL,
                    "SchoolsSectionVisible" INTEGER NOT NULL,
                    "InstructorsSectionVisible" INTEGER NOT NULL,
                    "StatsSectionVisible" INTEGER NOT NULL,
                    "BenefitsSectionVisible" INTEGER NOT NULL,
                    "StepsSectionVisible" INTEGER NOT NULL,
                    "SeoTitle" TEXT NOT NULL,
                    "SeoDescription" TEXT NOT NULL,
                    "ContactEmail" TEXT NOT NULL,
                    "ContactPhone" TEXT NOT NULL,
                    "AboutHtml" TEXT NOT NULL,
                    "BlogIntro" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "UpdatedByUserId" INTEGER NULL
                );
                """,
                ct);

            await TrySqliteAsync(
                db,
                """
                CREATE TABLE IF NOT EXISTS "HomepageStatSettings" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_HomepageStatSettings" PRIMARY KEY AUTOINCREMENT,
                    "Key" TEXT NOT NULL,
                    "Label" TEXT NOT NULL,
                    "SubLabel" TEXT NOT NULL,
                    "Icon" TEXT NOT NULL,
                    "Mode" TEXT NOT NULL,
                    "ManualValue" TEXT NULL,
                    "LastComputedValue" TEXT NULL,
                    "LastComputedDisplay" TEXT NULL,
                    "Visible" INTEGER NOT NULL,
                    "SortOrder" INTEGER NOT NULL,
                    "LastComputedAt" TEXT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_HomepageStatSettings_Key"
                    ON "HomepageStatSettings" ("Key");
                """,
                ct);

            await TrySqliteAsync(
                db,
                """
                CREATE TABLE IF NOT EXISTS "HomepageAudits" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_HomepageAudits" PRIMARY KEY AUTOINCREMENT,
                    "ActorUserId" INTEGER NOT NULL,
                    "Area" TEXT NOT NULL,
                    "StatKey" TEXT NULL,
                    "PreviousValue" TEXT NULL,
                    "NewValue" TEXT NULL,
                    "Note" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_HomepageAudits_CreatedAt"
                    ON "HomepageAudits" ("CreatedAt");
                """,
                ct);

            await TrySqliteAsync(
                db,
                """
                CREATE TABLE IF NOT EXISTS "Presentaciones" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Presentaciones" PRIMARY KEY AUTOINCREMENT,
                    "OwnerId" INTEGER NOT NULL,
                    "SchoolId" INTEGER NULL,
                    "GroupId" INTEGER NULL,
                    "Title" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "Category" TEXT NOT NULL,
                    "ThumbnailUrl" TEXT NULL,
                    "SlideCount" INTEGER NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "UpdatedByUserId" INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_Presentaciones_OwnerId"
                    ON "Presentaciones" ("OwnerId");
                CREATE INDEX IF NOT EXISTS "IX_Presentaciones_UpdatedAt"
                    ON "Presentaciones" ("UpdatedAt");
                CREATE TABLE IF NOT EXISTS "PresentacionDiapositivas" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_PresentacionDiapositivas" PRIMARY KEY AUTOINCREMENT,
                    "PresentationId" INTEGER NOT NULL,
                    "Position" INTEGER NOT NULL,
                    "Title" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "BackgroundJson" TEXT NOT NULL,
                    "ElementsJson" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_PresentacionDiapositivas_PresentationId_Position"
                    ON "PresentacionDiapositivas" ("PresentationId", "Position");
                """,
                ct);

            return;
        }

        if (db.Database.IsNpgsql())
        {
            // EnsureCreated already built the model; these ALTERs are for older DBs only.
            // Run one statement at a time (safer with Npgsql).
            await TryPostgresAsync(db,
                """ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "EmailConfirmado" boolean NOT NULL DEFAULT TRUE;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "EmailCodigoHash" varchar(128) NULL;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "EmailCodigoExpiraEn" timestamp with time zone NULL;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "SchoolId" integer NULL;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "UltimoAccesoEn" timestamp with time zone NULL;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "DebeCambiarClave" boolean NOT NULL DEFAULT FALSE;""",
                ct);
            return;
        }

        if (!db.Database.IsSqlServer())
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.Intentos', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Intentos', N'ExpiresAt') IS NULL
                ALTER TABLE dbo.Intentos ADD ExpiresAt datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchoolProfiles (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId int NOT NULL,
                    LegalName nvarchar(250) NOT NULL,
                    TaxId nvarchar(32) NOT NULL,
                    BillingEmail nvarchar(320) NOT NULL,
                    Phone nvarchar(40) NOT NULL,
                    Address nvarchar(300) NOT NULL,
                    City nvarchar(120) NOT NULL,
                    Department nvarchar(120) NOT NULL,
                    PlanCode nvarchar(32) NOT NULL,
                    PlanPriceCop decimal(18,2) NOT NULL,
                    SubscriptionStatus nvarchar(32) NOT NULL,
                    MembershipStartsAt datetime2 NULL,
                    MembershipEndsAt datetime2 NULL,
                    CreatedAt datetime2 NOT NULL
                );
                CREATE UNIQUE INDEX IX_SchoolProfiles_UserId ON dbo.SchoolProfiles(UserId);
                CREATE INDEX IX_SchoolProfiles_TaxId ON dbo.SchoolProfiles(TaxId);
            END

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'MembershipStartsAt') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD MembershipStartsAt datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'MembershipEndsAt') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD MembershipEndsAt datetime2 NULL;

            IF COL_LENGTH(N'dbo.Usuarios', N'SchoolId') IS NULL
                ALTER TABLE dbo.Usuarios ADD SchoolId int NULL;

            IF COL_LENGTH(N'dbo.Usuarios', N'UltimoAccesoEn') IS NULL
                ALTER TABLE dbo.Usuarios ADD UltimoAccesoEn datetime2 NULL;

            IF COL_LENGTH(N'dbo.Usuarios', N'DebeCambiarClave') IS NULL
                ALTER TABLE dbo.Usuarios ADD DebeCambiarClave bit NOT NULL CONSTRAINT DF_Usuarios_DebeCambiarClave DEFAULT(0);

            IF COL_LENGTH(N'dbo.Usuarios', N'EmailConfirmado') IS NULL
                ALTER TABLE dbo.Usuarios ADD EmailConfirmado bit NOT NULL CONSTRAINT DF_Usuarios_EmailConfirmado DEFAULT(1);

            IF COL_LENGTH(N'dbo.Usuarios', N'EmailCodigoHash') IS NULL
                ALTER TABLE dbo.Usuarios ADD EmailCodigoHash nvarchar(128) NULL;

            IF COL_LENGTH(N'dbo.Usuarios', N'EmailCodigoExpiraEn') IS NULL
                ALTER TABLE dbo.Usuarios ADD EmailCodigoExpiraEn datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'RequestedPlanCode') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD RequestedPlanCode nvarchar(32) NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'PaymentProofUrl') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD PaymentProofUrl nvarchar(500) NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'PaymentReference') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD PaymentReference nvarchar(120) NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'RejectionReason') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD RejectionReason nvarchar(500) NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'RequestedAt') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD RequestedAt datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'ProofSubmittedAt') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD ProofSubmittedAt datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'LastDecisionAt') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD LastDecisionAt datetime2 NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'RenewalStatus') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD RenewalStatus nvarchar(32) NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'SuspensionReason') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD SuspensionReason nvarchar(500) NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'TeachersMaxOverride') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD TeachersMaxOverride int NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.SchoolProfiles', N'StudentsMaxOverride') IS NULL
                ALTER TABLE dbo.SchoolProfiles ADD StudentsMaxOverride int NULL;

            IF OBJECT_ID(N'dbo.SchoolProfiles', N'U') IS NOT NULL
            BEGIN
                UPDATE dbo.SchoolProfiles
                SET SubscriptionStatus = N'UnderReview'
                WHERE SubscriptionStatus = N'PaymentSubmitted';

                UPDATE dbo.SchoolProfiles
                SET RenewalStatus = N'None'
                WHERE RenewalStatus IS NULL OR LTRIM(RTRIM(RenewalStatus)) = N'';

                UPDATE dbo.SchoolProfiles
                SET RenewalStatus = CASE
                    WHEN PaymentProofUrl IS NOT NULL AND LTRIM(RTRIM(PaymentProofUrl)) <> N'' THEN N'UnderReview'
                    ELSE N'PendingPayment'
                END
                WHERE SubscriptionStatus = N'Active'
                  AND RequestedPlanCode IS NOT NULL
                  AND LTRIM(RTRIM(RequestedPlanCode)) <> N''
                  AND (RenewalStatus IS NULL OR RenewalStatus = N'None');
            END

            IF OBJECT_ID(N'dbo.MembershipEvents', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MembershipEvents (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SchoolUserId int NOT NULL,
                    EventType nvarchar(32) NOT NULL,
                    PlanCode nvarchar(32) NULL,
                    PlanPriceCop decimal(18,2) NULL,
                    ActorUserId int NULL,
                    Note nvarchar(500) NULL,
                    CreatedAt datetime2 NOT NULL
                );
                CREATE INDEX IX_MembershipEvents_SchoolUserId ON dbo.MembershipEvents(SchoolUserId);
                CREATE INDEX IX_MembershipEvents_EventType ON dbo.MembershipEvents(EventType);
                CREATE INDEX IX_MembershipEvents_CreatedAt ON dbo.MembershipEvents(CreatedAt);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Usuarios_SchoolId' AND object_id = OBJECT_ID(N'dbo.Usuarios'))
                CREATE INDEX IX_Usuarios_SchoolId ON dbo.Usuarios(SchoolId);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Usuarios_UltimoAccesoEn' AND object_id = OBJECT_ID(N'dbo.Usuarios'))
                CREATE INDEX IX_Usuarios_UltimoAccesoEn ON dbo.Usuarios(UltimoAccesoEn);

            IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Notificaciones', N'LeidaEn') IS NULL
                ALTER TABLE dbo.Notificaciones ADD LeidaEn datetime2 NULL;

            IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Notificaciones', N'Link') IS NULL
                ALTER TABLE dbo.Notificaciones ADD Link nvarchar(300) NULL;

            IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Notificaciones', N'Prioridad') IS NULL
                ALTER TABLE dbo.Notificaciones ADD Prioridad nvarchar(20) NOT NULL CONSTRAINT DF_Notificaciones_Prioridad DEFAULT(N'normal');

            IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Notificaciones', N'DedupeKey') IS NULL
                ALTER TABLE dbo.Notificaciones ADD DedupeKey nvarchar(120) NULL;

            IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Notificaciones', N'Archivada') IS NULL
                ALTER TABLE dbo.Notificaciones ADD Archivada bit NOT NULL CONSTRAINT DF_Notificaciones_Archivada DEFAULT(0);

            IF OBJECT_ID(N'dbo.NotificationPreferences', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.NotificationPreferences (
                    UserId int NOT NULL PRIMARY KEY,
                    AcademicEnabled bit NOT NULL,
                    MembershipEnabled bit NOT NULL,
                    AdminEnabled bit NOT NULL,
                    SystemEnabled bit NOT NULL
                );
            END

            IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Notificaciones_User_Unread'
                      AND object_id = OBJECT_ID(N'dbo.Notificaciones'))
                CREATE INDEX IX_Notificaciones_User_Unread
                    ON dbo.Notificaciones(UsuarioId, Leida, Archivada);

            IF OBJECT_ID(N'dbo.HomepageSettings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.HomepageSettings (
                    Id int NOT NULL PRIMARY KEY,
                    HeroBadge nvarchar(120) NOT NULL,
                    HeroTitle nvarchar(200) NOT NULL,
                    HeroTitleHighlight nvarchar(200) NOT NULL,
                    HeroDescription nvarchar(2000) NOT NULL,
                    HeroCtaPrimaryLabel nvarchar(80) NOT NULL,
                    HeroCtaPrimaryPath nvarchar(200) NOT NULL,
                    HeroCtaSecondaryLabel nvarchar(80) NOT NULL,
                    HeroVideoUrl nvarchar(500) NULL,
                    HeroImageUrl nvarchar(500) NULL,
                    HeroImageUrlMobile nvarchar(500) NULL,
                    HeroImageAlt nvarchar(200) NOT NULL,
                    HeroImageEnabled bit NOT NULL,
                    HeroVisible bit NOT NULL,
                    BenefitsJson nvarchar(max) NOT NULL,
                    StepsJson nvarchar(max) NOT NULL,
                    StepsSectionTitle nvarchar(200) NOT NULL,
                    StepsSectionSubtitle nvarchar(500) NOT NULL,
                    SchoolsSectionVisible bit NOT NULL,
                    InstructorsSectionVisible bit NOT NULL,
                    StatsSectionVisible bit NOT NULL,
                    BenefitsSectionVisible bit NOT NULL,
                    StepsSectionVisible bit NOT NULL,
                    SeoTitle nvarchar(200) NOT NULL,
                    SeoDescription nvarchar(500) NOT NULL,
                    ContactEmail nvarchar(200) NOT NULL,
                    ContactPhone nvarchar(80) NOT NULL,
                    AboutHtml nvarchar(max) NOT NULL,
                    BlogIntro nvarchar(2000) NOT NULL,
                    UpdatedAt datetime2 NOT NULL,
                    UpdatedByUserId int NULL
                );
            END

            IF OBJECT_ID(N'dbo.HomepageStatSettings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.HomepageStatSettings (
                    Id int NOT NULL IDENTITY(1,1) PRIMARY KEY,
                    [Key] nvarchar(40) NOT NULL,
                    Label nvarchar(120) NOT NULL,
                    SubLabel nvarchar(120) NOT NULL,
                    Icon nvarchar(40) NOT NULL,
                    Mode nvarchar(20) NOT NULL,
                    ManualValue nvarchar(80) NULL,
                    LastComputedValue nvarchar(80) NULL,
                    LastComputedDisplay nvarchar(80) NULL,
                    Visible bit NOT NULL,
                    SortOrder int NOT NULL,
                    LastComputedAt datetime2 NULL,
                    UpdatedAt datetime2 NOT NULL
                );
                CREATE UNIQUE INDEX IX_HomepageStatSettings_Key ON dbo.HomepageStatSettings([Key]);
            END

            IF OBJECT_ID(N'dbo.HomepageAudits', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.HomepageAudits (
                    Id bigint NOT NULL IDENTITY(1,1) PRIMARY KEY,
                    ActorUserId int NOT NULL,
                    Area nvarchar(80) NOT NULL,
                    StatKey nvarchar(40) NULL,
                    PreviousValue nvarchar(200) NULL,
                    NewValue nvarchar(200) NULL,
                    Note nvarchar(500) NULL,
                    CreatedAt datetime2 NOT NULL
                );
                CREATE INDEX IX_HomepageAudits_CreatedAt ON dbo.HomepageAudits(CreatedAt);
            END

            IF OBJECT_ID(N'dbo.Presentaciones', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Presentaciones (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    OwnerId int NOT NULL,
                    SchoolId int NULL,
                    GroupId int NULL,
                    Title nvarchar(200) NOT NULL,
                    Description nvarchar(1000) NULL,
                    Category nvarchar(80) NOT NULL,
                    ThumbnailUrl nvarchar(500) NULL,
                    SlideCount int NOT NULL,
                    IsActive bit NOT NULL,
                    CreatedAt datetime2 NOT NULL,
                    UpdatedAt datetime2 NOT NULL,
                    UpdatedByUserId int NOT NULL
                );
                CREATE INDEX IX_Presentaciones_OwnerId ON dbo.Presentaciones(OwnerId);
                CREATE INDEX IX_Presentaciones_UpdatedAt ON dbo.Presentaciones(UpdatedAt);
            END

            IF OBJECT_ID(N'dbo.PresentacionDiapositivas', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.PresentacionDiapositivas (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    PresentationId int NOT NULL,
                    Position int NOT NULL,
                    Title nvarchar(200) NOT NULL,
                    Notes nvarchar(max) NULL,
                    BackgroundJson nvarchar(max) NOT NULL,
                    ElementsJson nvarchar(max) NOT NULL,
                    CreatedAt datetime2 NOT NULL,
                    UpdatedAt datetime2 NOT NULL
                );
                CREATE INDEX IX_PresentacionDiapositivas_PresentationId_Position
                    ON dbo.PresentacionDiapositivas(PresentationId, Position);
            END
            """,
            ct);
    }

    private static async Task TryAddSqliteColumnAsync(
        CaleDbContext db,
        string sql,
        CancellationToken ct) =>
        await TrySqliteAsync(db, sql, ct);

    private static async Task TryPostgresAsync(
        CaleDbContext db,
        string sql,
        CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // Table not ready yet or column already exists.
        }
    }

    private static async Task TrySqliteAsync(
        CaleDbContext db,
        string sql,
        CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // Column/index already exists, or unique index blocked by duplicates.
        }
    }
}
