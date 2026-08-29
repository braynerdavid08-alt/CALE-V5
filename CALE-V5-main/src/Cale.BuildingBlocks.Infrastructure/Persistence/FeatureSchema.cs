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

            await TrySqliteAsync(
                db,
                """
                CREATE TABLE IF NOT EXISTS "LiveSessions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_LiveSessions" PRIMARY KEY AUTOINCREMENT,
                    "HostUserId" INTEGER NOT NULL,
                    "Title" TEXT NOT NULL,
                    "JoinCode" TEXT NOT NULL,
                    "Status" TEXT NOT NULL,
                    "Mode" TEXT NOT NULL,
                    "BankId" INTEGER NOT NULL,
                    "ConfigJson" TEXT NOT NULL,
                    "CurrentQuestionIndex" INTEGER NOT NULL DEFAULT -1,
                    "RevealCorrect" INTEGER NOT NULL DEFAULT 0,
                    "QuestionOpenedAt" TEXT NULL,
                    "QuestionClosesAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "StartedAt" TEXT NULL,
                    "EndedAt" TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveSessions_JoinCode" ON "LiveSessions" ("JoinCode");
                CREATE TABLE IF NOT EXISTS "LiveParticipants" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_LiveParticipants" PRIMARY KEY AUTOINCREMENT,
                    "SessionId" INTEGER NOT NULL,
                    "UserId" INTEGER NULL,
                    "DisplayName" TEXT NOT NULL,
                    "ParticipantToken" TEXT NOT NULL,
                    "ConnectionId" TEXT NULL,
                    "IsConnected" INTEGER NOT NULL DEFAULT 0,
                    "JoinedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveParticipants_ParticipantToken" ON "LiveParticipants" ("ParticipantToken");
                CREATE TABLE IF NOT EXISTS "LiveSessionQuestions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_LiveSessionQuestions" PRIMARY KEY AUTOINCREMENT,
                    "SessionId" INTEGER NOT NULL,
                    "QuestionId" INTEGER NOT NULL,
                    "SortOrder" INTEGER NOT NULL,
                    "SnapshotJson" TEXT NOT NULL,
                    "Topic" TEXT NULL,
                    "Difficulty" TEXT NULL,
                    "IsSurprise" INTEGER NOT NULL DEFAULT 0
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveSessionQuestions_SessionId_SortOrder" ON "LiveSessionQuestions" ("SessionId", "SortOrder");
                CREATE TABLE IF NOT EXISTS "LiveAnswers" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_LiveAnswers" PRIMARY KEY AUTOINCREMENT,
                    "SessionQuestionId" INTEGER NOT NULL,
                    "ParticipantId" INTEGER NOT NULL,
                    "OptionId" INTEGER NOT NULL,
                    "IsCorrect" INTEGER NOT NULL,
                    "AnsweredAtMs" INTEGER NOT NULL,
                    "Points" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveAnswers_SessionQuestionId_ParticipantId" ON "LiveAnswers" ("SessionQuestionId", "ParticipantId");
                CREATE TABLE IF NOT EXISTS "LiveDoubts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_LiveDoubts" PRIMARY KEY AUTOINCREMENT,
                    "SessionId" INTEGER NOT NULL,
                    "ParticipantId" INTEGER NOT NULL,
                    "Text" TEXT NOT NULL,
                    "VoteCount" INTEGER NOT NULL DEFAULT 0,
                    "IsResolved" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_LiveDoubts_SessionId" ON "LiveDoubts" ("SessionId");
                CREATE TABLE IF NOT EXISTS "LiveDoubtVotes" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_LiveDoubtVotes" PRIMARY KEY AUTOINCREMENT,
                    "DoubtId" INTEGER NOT NULL,
                    "ParticipantId" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveDoubtVotes_DoubtId_ParticipantId" ON "LiveDoubtVotes" ("DoubtId", "ParticipantId");
                CREATE TABLE IF NOT EXISTS "SchoolJoinRequests" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SchoolJoinRequests" PRIMARY KEY AUTOINCREMENT,
                    "TeacherUserId" INTEGER NOT NULL,
                    "SchoolUserId" INTEGER NOT NULL,
                    "Status" TEXT NOT NULL,
                    "Message" TEXT NULL,
                    "RejectionReason" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "DecidedAt" TEXT NULL,
                    "DecidedByUserId" INTEGER NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_SchoolJoinRequests_SchoolUserId_Status"
                    ON "SchoolJoinRequests" ("SchoolUserId", "Status");
                CREATE INDEX IF NOT EXISTS "IX_SchoolJoinRequests_TeacherUserId_Status"
                    ON "SchoolJoinRequests" ("TeacherUserId", "Status");
                CREATE TABLE IF NOT EXISTS "TheoryTopics" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TheoryTopics" PRIMARY KEY AUTOINCREMENT,
                    "SchoolUserId" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "Color" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL DEFAULT 1,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_TheoryTopics_SchoolUserId" ON "TheoryTopics" ("SchoolUserId");
                CREATE TABLE IF NOT EXISTS "TheoryClassrooms" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TheoryClassrooms" PRIMARY KEY AUTOINCREMENT,
                    "SchoolUserId" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Identifier" TEXT NULL,
                    "Capacity" INTEGER NOT NULL,
                    "Location" TEXT NULL,
                    "IsActive" INTEGER NOT NULL DEFAULT 1,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "TheoryTrainingSettings" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TheoryTrainingSettings" PRIMARY KEY AUTOINCREMENT,
                    "SchoolUserId" INTEGER NOT NULL,
                    "DefaultDurationMinutes" INTEGER NOT NULL DEFAULT 120,
                    "MinCancelHours" INTEGER NOT NULL DEFAULT 2,
                    "ReservationCloseMinutesBefore" INTEGER NOT NULL DEFAULT 0,
                    "RequiredTheoryHours" INTEGER NOT NULL DEFAULT 20,
                    "SaturdayEnabled" INTEGER NOT NULL DEFAULT 1,
                    "NotifyReservationOpen" INTEGER NOT NULL DEFAULT 1,
                    "NotifyClassReminder24h" INTEGER NOT NULL DEFAULT 1,
                    "NotifyClassReminder1h" INTEGER NOT NULL DEFAULT 1,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TheoryTrainingSettings_SchoolUserId" ON "TheoryTrainingSettings" ("SchoolUserId");
                CREATE TABLE IF NOT EXISTS "TheoryClassSessions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TheoryClassSessions" PRIMARY KEY AUTOINCREMENT,
                    "SchoolUserId" INTEGER NOT NULL,
                    "TopicId" INTEGER NOT NULL,
                    "ClassroomId" INTEGER NOT NULL,
                    "InstructorUserId" INTEGER NULL,
                    "SessionDate" TEXT NOT NULL,
                    "StartTime" TEXT NOT NULL,
                    "EndTime" TEXT NOT NULL,
                    "Capacity" INTEGER NOT NULL,
                    "Status" TEXT NOT NULL,
                    "ReservationOpenAt" TEXT NOT NULL,
                    "ReservationCloseAt" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "CancellationReason" TEXT NULL,
                    "CancelledByUserId" INTEGER NULL,
                    "CancelledAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_TheoryClassSessions_SchoolUserId_SessionDate" ON "TheoryClassSessions" ("SchoolUserId", "SessionDate");
                CREATE TABLE IF NOT EXISTS "TheoryClassReservations" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TheoryClassReservations" PRIMARY KEY AUTOINCREMENT,
                    "ClassSessionId" INTEGER NOT NULL,
                    "StudentUserId" INTEGER NOT NULL,
                    "Status" TEXT NOT NULL,
                    "ReservedAt" TEXT NOT NULL,
                    "CancelledAt" TEXT NULL,
                    "CancellationReason" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_TheoryClassReservations_ClassSessionId" ON "TheoryClassReservations" ("ClassSessionId");
                CREATE INDEX IF NOT EXISTS "IX_TheoryClassReservations_StudentUserId" ON "TheoryClassReservations" ("StudentUserId");
                CREATE TABLE IF NOT EXISTS "TheoryAttendanceRecords" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TheoryAttendanceRecords" PRIMARY KEY AUTOINCREMENT,
                    "ClassSessionId" INTEGER NOT NULL,
                    "StudentUserId" INTEGER NOT NULL,
                    "Status" TEXT NOT NULL,
                    "MarkedByUserId" INTEGER NULL,
                    "MarkedAt" TEXT NULL,
                    "Notes" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TheoryAttendanceRecords_ClassSessionId_StudentUserId" ON "TheoryAttendanceRecords" ("ClassSessionId", "StudentUserId");
                CREATE TABLE IF NOT EXISTS "SchoolStudentEnrollments" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SchoolStudentEnrollments" PRIMARY KEY AUTOINCREMENT,
                    "SchoolUserId" INTEGER NOT NULL,
                    "StudentUserId" INTEGER NOT NULL,
                    "Status" TEXT NOT NULL,
                    "AttendanceDayType" TEXT NULL,
                    "AllowedStartTime" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "AcceptedAt" TEXT NULL,
                    "SuspendedAt" TEXT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchoolStudentEnrollments_SchoolUserId_StudentUserId" ON "SchoolStudentEnrollments" ("SchoolUserId", "StudentUserId");
                CREATE TABLE IF NOT EXISTS "StudentDailyCheckIns" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_StudentDailyCheckIns" PRIMARY KEY AUTOINCREMENT,
                    "StudentUserId" INTEGER NOT NULL,
                    "CheckInDate" TEXT NOT NULL,
                    "CheckInAt" TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_StudentDailyCheckIns_StudentUserId_CheckInDate" ON "StudentDailyCheckIns" ("StudentUserId", "CheckInDate");
                """,
                ct);

            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "LiveAnswers" ADD COLUMN "Points" INTEGER NOT NULL DEFAULT 0;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "LiveSessionQuestions" ADD COLUMN "IsSurprise" INTEGER NOT NULL DEFAULT 0;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN "NotifyReservationOpen" INTEGER NOT NULL DEFAULT 1;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN "NotifyClassReminder24h" INTEGER NOT NULL DEFAULT 1;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN "NotifyClassReminder1h" INTEGER NOT NULL DEFAULT 1;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolStudentEnrollments" ADD COLUMN "AttendanceDayType" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolStudentEnrollments" ADD COLUMN "AllowedStartTime" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "SchoolStudentEnrollments" ADD COLUMN "LicenseCategories" TEXT NULL;""",
                ct);
            await TryAddSqliteColumnAsync(
                db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN "WeekdaysEnabled" INTEGER NOT NULL DEFAULT 1;""",
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

            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "LiveSessions" (
                    "Id" serial PRIMARY KEY,
                    "HostUserId" integer NOT NULL,
                    "Title" varchar(200) NOT NULL,
                    "JoinCode" varchar(12) NOT NULL,
                    "Status" varchar(32) NOT NULL,
                    "Mode" varchar(32) NOT NULL,
                    "BankId" integer NOT NULL,
                    "ConfigJson" varchar(4000) NOT NULL,
                    "CurrentQuestionIndex" integer NOT NULL DEFAULT -1,
                    "RevealCorrect" boolean NOT NULL DEFAULT FALSE,
                    "QuestionOpenedAt" timestamp with time zone NULL,
                    "QuestionClosesAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "StartedAt" timestamp with time zone NULL,
                    "EndedAt" timestamp with time zone NULL
                );
                """,
                ct);
            await TryPostgresAsync(db,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveSessions_JoinCode" ON "LiveSessions" ("JoinCode");""",
                ct);
            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "LiveParticipants" (
                    "Id" serial PRIMARY KEY,
                    "SessionId" integer NOT NULL,
                    "UserId" integer NULL,
                    "DisplayName" varchar(80) NOT NULL,
                    "ParticipantToken" uuid NOT NULL,
                    "ConnectionId" varchar(128) NULL,
                    "IsConnected" boolean NOT NULL DEFAULT FALSE,
                    "JoinedAt" timestamp with time zone NOT NULL
                );
                """,
                ct);
            await TryPostgresAsync(db,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveParticipants_ParticipantToken" ON "LiveParticipants" ("ParticipantToken");""",
                ct);
            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "LiveSessionQuestions" (
                    "Id" serial PRIMARY KEY,
                    "SessionId" integer NOT NULL,
                    "QuestionId" integer NOT NULL,
                    "SortOrder" integer NOT NULL,
                    "SnapshotJson" text NOT NULL,
                    "Topic" varchar(200) NULL,
                    "Difficulty" varchar(64) NULL,
                    "IsSurprise" boolean NOT NULL DEFAULT FALSE
                );
                """,
                ct);
            await TryPostgresAsync(db,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveSessionQuestions_SessionId_SortOrder" ON "LiveSessionQuestions" ("SessionId", "SortOrder");""",
                ct);
            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "LiveAnswers" (
                    "Id" serial PRIMARY KEY,
                    "SessionQuestionId" integer NOT NULL,
                    "ParticipantId" integer NOT NULL,
                    "OptionId" integer NOT NULL,
                    "IsCorrect" boolean NOT NULL,
                    "AnsweredAtMs" integer NOT NULL,
                    "Points" integer NOT NULL DEFAULT 0,
                    "CreatedAt" timestamp with time zone NOT NULL
                );
                """,
                ct);
            await TryPostgresAsync(db,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveAnswers_SessionQuestionId_ParticipantId" ON "LiveAnswers" ("SessionQuestionId", "ParticipantId");""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "LiveAnswers" ADD COLUMN IF NOT EXISTS "Points" integer NOT NULL DEFAULT 0;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "LiveSessionQuestions" ADD COLUMN IF NOT EXISTS "IsSurprise" boolean NOT NULL DEFAULT FALSE;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN IF NOT EXISTS "NotifyReservationOpen" boolean NOT NULL DEFAULT TRUE;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN IF NOT EXISTS "NotifyClassReminder24h" boolean NOT NULL DEFAULT TRUE;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN IF NOT EXISTS "NotifyClassReminder1h" boolean NOT NULL DEFAULT TRUE;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "SchoolStudentEnrollments" ADD COLUMN IF NOT EXISTS "AttendanceDayType" varchar(16) NULL;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "SchoolStudentEnrollments" ADD COLUMN IF NOT EXISTS "AllowedStartTime" time NULL;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "SchoolStudentEnrollments" ADD COLUMN IF NOT EXISTS "LicenseCategories" varchar(32) NULL;""",
                ct);
            await TryPostgresAsync(db,
                """ALTER TABLE "TheoryTrainingSettings" ADD COLUMN IF NOT EXISTS "WeekdaysEnabled" boolean NOT NULL DEFAULT TRUE;""",
                ct);
            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "LiveDoubts" (
                    "Id" serial PRIMARY KEY,
                    "SessionId" integer NOT NULL,
                    "ParticipantId" integer NOT NULL,
                    "Text" varchar(280) NOT NULL,
                    "VoteCount" integer NOT NULL DEFAULT 0,
                    "IsResolved" boolean NOT NULL DEFAULT FALSE,
                    "CreatedAt" timestamp with time zone NOT NULL
                );
                """,
                ct);
            await TryPostgresAsync(db,
                """CREATE INDEX IF NOT EXISTS "IX_LiveDoubts_SessionId" ON "LiveDoubts" ("SessionId");""",
                ct);
            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "LiveDoubtVotes" (
                    "Id" serial PRIMARY KEY,
                    "DoubtId" integer NOT NULL,
                    "ParticipantId" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL
                );
                """,
                ct);
            await TryPostgresAsync(db,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LiveDoubtVotes_DoubtId_ParticipantId" ON "LiveDoubtVotes" ("DoubtId", "ParticipantId");""",
                ct);
            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "SchoolJoinRequests" (
                    "Id" serial PRIMARY KEY,
                    "TeacherUserId" integer NOT NULL,
                    "SchoolUserId" integer NOT NULL,
                    "Status" varchar(32) NOT NULL,
                    "Message" varchar(500) NULL,
                    "RejectionReason" varchar(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "DecidedAt" timestamp with time zone NULL,
                    "DecidedByUserId" integer NULL
                );
                """,
                ct);
            await TryPostgresAsync(db,
                """CREATE INDEX IF NOT EXISTS "IX_SchoolJoinRequests_SchoolUserId_Status" ON "SchoolJoinRequests" ("SchoolUserId", "Status");""",
                ct);
            await TryPostgresAsync(db,
                """CREATE INDEX IF NOT EXISTS "IX_SchoolJoinRequests_TeacherUserId_Status" ON "SchoolJoinRequests" ("TeacherUserId", "Status");""",
                ct);
            await TryPostgresAsync(db,
                """
                CREATE TABLE IF NOT EXISTS "TheoryTopics" (
                    "Id" serial PRIMARY KEY,
                    "SchoolUserId" integer NOT NULL,
                    "Name" varchar(120) NOT NULL,
                    "Description" varchar(500) NULL,
                    "Color" varchar(16) NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT TRUE,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "TheoryClassrooms" (
                    "Id" serial PRIMARY KEY,
                    "SchoolUserId" integer NOT NULL,
                    "Name" varchar(80) NOT NULL,
                    "Identifier" varchar(40) NULL,
                    "Capacity" integer NOT NULL,
                    "Location" varchar(200) NULL,
                    "IsActive" boolean NOT NULL DEFAULT TRUE,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "TheoryTrainingSettings" (
                    "Id" serial PRIMARY KEY,
                    "SchoolUserId" integer NOT NULL UNIQUE,
                    "DefaultDurationMinutes" integer NOT NULL DEFAULT 120,
                    "MinCancelHours" integer NOT NULL DEFAULT 2,
                    "ReservationCloseMinutesBefore" integer NOT NULL DEFAULT 0,
                    "RequiredTheoryHours" integer NOT NULL DEFAULT 20,
                    "SaturdayEnabled" boolean NOT NULL DEFAULT TRUE,
                    "NotifyReservationOpen" boolean NOT NULL DEFAULT TRUE,
                    "NotifyClassReminder24h" boolean NOT NULL DEFAULT TRUE,
                    "NotifyClassReminder1h" boolean NOT NULL DEFAULT TRUE,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "TheoryClassSessions" (
                    "Id" serial PRIMARY KEY,
                    "SchoolUserId" integer NOT NULL,
                    "TopicId" integer NOT NULL,
                    "ClassroomId" integer NOT NULL,
                    "InstructorUserId" integer NULL,
                    "SessionDate" date NOT NULL,
                    "StartTime" time NOT NULL,
                    "EndTime" time NOT NULL,
                    "Capacity" integer NOT NULL,
                    "Status" varchar(32) NOT NULL,
                    "ReservationOpenAt" timestamp with time zone NOT NULL,
                    "ReservationCloseAt" timestamp with time zone NOT NULL,
                    "Notes" varchar(500) NULL,
                    "CancellationReason" varchar(500) NULL,
                    "CancelledByUserId" integer NULL,
                    "CancelledAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "TheoryClassReservations" (
                    "Id" serial PRIMARY KEY,
                    "ClassSessionId" integer NOT NULL,
                    "StudentUserId" integer NOT NULL,
                    "Status" varchar(32) NOT NULL,
                    "ReservedAt" timestamp with time zone NOT NULL,
                    "CancelledAt" timestamp with time zone NULL,
                    "CancellationReason" varchar(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "TheoryAttendanceRecords" (
                    "Id" serial PRIMARY KEY,
                    "ClassSessionId" integer NOT NULL,
                    "StudentUserId" integer NOT NULL,
                    "Status" varchar(32) NOT NULL,
                    "MarkedByUserId" integer NULL,
                    "MarkedAt" timestamp with time zone NULL,
                    "Notes" varchar(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "SchoolStudentEnrollments" (
                    "Id" serial PRIMARY KEY,
                    "SchoolUserId" integer NOT NULL,
                    "StudentUserId" integer NOT NULL,
                    "Status" varchar(32) NOT NULL,
                    "AttendanceDayType" varchar(16) NULL,
                    "AllowedStartTime" time NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "AcceptedAt" timestamp with time zone NULL,
                    "SuspendedAt" timestamp with time zone NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "StudentDailyCheckIns" (
                    "Id" serial PRIMARY KEY,
                    "StudentUserId" integer NOT NULL,
                    "CheckInDate" date NOT NULL,
                    "CheckInAt" timestamp with time zone NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_TheoryClassSessions_SchoolUserId_SessionDate" ON "TheoryClassSessions" ("SchoolUserId", "SessionDate");
                CREATE INDEX IF NOT EXISTS "IX_TheoryClassReservations_ClassSessionId" ON "TheoryClassReservations" ("ClassSessionId");
                CREATE INDEX IF NOT EXISTS "IX_TheoryClassReservations_StudentUserId" ON "TheoryClassReservations" ("StudentUserId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TheoryAttendanceRecords_ClassSessionId_StudentUserId" ON "TheoryAttendanceRecords" ("ClassSessionId", "StudentUserId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchoolStudentEnrollments_SchoolUserId_StudentUserId" ON "SchoolStudentEnrollments" ("SchoolUserId", "StudentUserId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_StudentDailyCheckIns_StudentUserId_CheckInDate" ON "StudentDailyCheckIns" ("StudentUserId", "CheckInDate");
                """,
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

            IF OBJECT_ID(N'dbo.LiveSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LiveSessions (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    HostUserId int NOT NULL,
                    Title nvarchar(200) NOT NULL,
                    JoinCode nvarchar(12) NOT NULL,
                    Status nvarchar(32) NOT NULL,
                    Mode nvarchar(32) NOT NULL,
                    BankId int NOT NULL,
                    ConfigJson nvarchar(4000) NOT NULL,
                    CurrentQuestionIndex int NOT NULL CONSTRAINT DF_LiveSessions_CQI DEFAULT(-1),
                    RevealCorrect bit NOT NULL CONSTRAINT DF_LiveSessions_Reveal DEFAULT(0),
                    QuestionOpenedAt datetime2 NULL,
                    QuestionClosesAt datetime2 NULL,
                    CreatedAt datetime2 NOT NULL,
                    StartedAt datetime2 NULL,
                    EndedAt datetime2 NULL
                );
                CREATE UNIQUE INDEX IX_LiveSessions_JoinCode ON dbo.LiveSessions(JoinCode);
            END

            IF OBJECT_ID(N'dbo.LiveParticipants', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LiveParticipants (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SessionId int NOT NULL,
                    UserId int NULL,
                    DisplayName nvarchar(80) NOT NULL,
                    ParticipantToken uniqueidentifier NOT NULL,
                    ConnectionId nvarchar(128) NULL,
                    IsConnected bit NOT NULL CONSTRAINT DF_LiveParticipants_Conn DEFAULT(0),
                    JoinedAt datetime2 NOT NULL
                );
                CREATE UNIQUE INDEX IX_LiveParticipants_ParticipantToken ON dbo.LiveParticipants(ParticipantToken);
            END

            IF OBJECT_ID(N'dbo.LiveSessionQuestions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LiveSessionQuestions (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SessionId int NOT NULL,
                    QuestionId int NOT NULL,
                    SortOrder int NOT NULL,
                    SnapshotJson nvarchar(max) NOT NULL,
                    Topic nvarchar(200) NULL,
                    Difficulty nvarchar(64) NULL,
                    IsSurprise bit NOT NULL CONSTRAINT DF_LiveSessionQuestions_Surprise DEFAULT(0)
                );
                CREATE UNIQUE INDEX IX_LiveSessionQuestions_SessionId_SortOrder
                    ON dbo.LiveSessionQuestions(SessionId, SortOrder);
            END

            IF OBJECT_ID(N'dbo.LiveAnswers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LiveAnswers (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SessionQuestionId int NOT NULL,
                    ParticipantId int NOT NULL,
                    OptionId int NOT NULL,
                    IsCorrect bit NOT NULL,
                    AnsweredAtMs int NOT NULL,
                    Points int NOT NULL CONSTRAINT DF_LiveAnswers_Points DEFAULT(0),
                    CreatedAt datetime2 NOT NULL
                );
                CREATE UNIQUE INDEX IX_LiveAnswers_SessionQuestionId_ParticipantId
                    ON dbo.LiveAnswers(SessionQuestionId, ParticipantId);
            END

            IF COL_LENGTH('dbo.LiveAnswers', 'Points') IS NULL
                ALTER TABLE dbo.LiveAnswers ADD Points int NOT NULL CONSTRAINT DF_LiveAnswers_Points2 DEFAULT(0);
            IF COL_LENGTH('dbo.LiveSessionQuestions', 'IsSurprise') IS NULL
                ALTER TABLE dbo.LiveSessionQuestions ADD IsSurprise bit NOT NULL CONSTRAINT DF_LiveSessionQuestions_Surprise2 DEFAULT(0);

            IF OBJECT_ID(N'dbo.LiveDoubts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LiveDoubts (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SessionId int NOT NULL,
                    ParticipantId int NOT NULL,
                    Text nvarchar(280) NOT NULL,
                    VoteCount int NOT NULL CONSTRAINT DF_LiveDoubts_Votes DEFAULT(0),
                    IsResolved bit NOT NULL CONSTRAINT DF_LiveDoubts_Resolved DEFAULT(0),
                    CreatedAt datetime2 NOT NULL
                );
                CREATE INDEX IX_LiveDoubts_SessionId ON dbo.LiveDoubts(SessionId);
            END

            IF OBJECT_ID(N'dbo.LiveDoubtVotes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LiveDoubtVotes (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    DoubtId int NOT NULL,
                    ParticipantId int NOT NULL,
                    CreatedAt datetime2 NOT NULL
                );
                CREATE UNIQUE INDEX IX_LiveDoubtVotes_DoubtId_ParticipantId
                    ON dbo.LiveDoubtVotes(DoubtId, ParticipantId);
            END

            IF OBJECT_ID(N'dbo.SchoolJoinRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchoolJoinRequests (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TeacherUserId int NOT NULL,
                    SchoolUserId int NOT NULL,
                    Status nvarchar(32) NOT NULL,
                    Message nvarchar(500) NULL,
                    RejectionReason nvarchar(500) NULL,
                    CreatedAt datetime2 NOT NULL,
                    DecidedAt datetime2 NULL,
                    DecidedByUserId int NULL
                );
                CREATE INDEX IX_SchoolJoinRequests_SchoolUserId_Status
                    ON dbo.SchoolJoinRequests(SchoolUserId, Status);
                CREATE INDEX IX_SchoolJoinRequests_TeacherUserId_Status
                    ON dbo.SchoolJoinRequests(TeacherUserId, Status);
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
