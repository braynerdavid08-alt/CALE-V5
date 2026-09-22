-- Hotfix for production if Migrate-at-startup is delayed.
-- Safe to run multiple times.

ALTER TABLE "IntentosPreguntas"
ADD COLUMN IF NOT EXISTS "SnapshotJson" text NOT NULL DEFAULT '{}';

ALTER TABLE "Intentos"
ADD COLUMN IF NOT EXISTS "ExpiresAt" timestamp with time zone NULL;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'Intentos'
      AND column_name = 'Modo'
      AND data_type = 'character varying'
  ) THEN
    ALTER TABLE "Intentos" ALTER COLUMN "Modo" TYPE text;
  END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Intentos_OpenExam"
    ON "Intentos" ("UsuarioId", "ExamenId")
    WHERE "FinEn" IS NULL AND "ExamenId" IS NOT NULL;
