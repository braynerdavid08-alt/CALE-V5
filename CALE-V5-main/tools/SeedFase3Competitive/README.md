# SeedFase3Competitive (solo laboratorio)

Herramienta **manual** para preparar cuentas `@cale.local` en un archivo **SQLite** local (`cale-dev.db`).

## Garantías

- **No** se ejecuta al arrancar la API.
- **No** está referenciada por producción / Render.
- Solo acepta rutas a archivos `.db` / `.sqlite` (rechaza connection strings Postgres).
- La contraseña **no** va en el código: se lee de `FASE3_LAB_PASSWORD`.

## Uso local

```powershell
cd CALE-V5-main
$env:FASE3_LAB_PASSWORD = "tu-clave-local-min-8"
# opcional: $env:FASE3_LAB_DB = "D:\ruta\cale-dev.db"
dotnet run --project tools/SeedFase3Competitive/SeedFase3Competitive.csproj
```

Luego arranca la API apuntando a ese SQLite (sin `appsettings.Development.local.json` de Postgres) y el frontend con proxy `/api` + `/hubs`.

## E2E

`frontend/e2e/fase3-competitive.spec.ts` usa las mismas variables:

| Variable | Descripción |
|---|---|
| `FASE3_LAB_PASSWORD` | Clave lab (obligatoria para seed y e2e) |
| `E2E_TEACHER_EMAIL` | default `profesor@cale.local` |
| `E2E_STUDENT_EMAILS` | CSV de 4 emails (defaults `estudiante@cale.local`, `estudiante2..4.fase3@cale.local`) |
| `E2E_API_URL` | default `http://127.0.0.1:5000` |
| `E2E_BASE_URL` | default `http://127.0.0.1:4200` |

```powershell
$env:FASE3_LAB_PASSWORD = "tu-clave-local-min-8"
cd frontend
npx playwright test e2e/fase3-competitive.spec.ts --project=chromium
```
