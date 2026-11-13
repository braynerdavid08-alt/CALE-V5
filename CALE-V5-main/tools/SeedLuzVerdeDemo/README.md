# SeedLuzVerdeDemo

Crea **3 CEAs de demostración** (Norte / Centro / Sur) con propietarios `School`, instructores, 14 escenarios académicos por escuela, teoría, práctica, exámenes y cartera.

**Objetivo principal:** cuentas reales en la misma plataforma publicada (`https://www.cealuzverde.com`), misma autenticación, mismos módulos Angular. No es otra app ni otro dominio.

También puede usarse en laboratorio SQLite local.

## Marcadores DEMO

- Correos `demo.*@cealuzverde.com` (legacy local: `*@demo.luzverde.test`, también purgable)
- Razones sociales `CEA DEMO …`
- Tag `DEMO-LV` / examen y banco con nombre `DEMO …`
- Courtesy operativa; `PaymentAdminStatus=None` (sin directorio comercial)

## Laboratorio local (SQLite)

La clave de los propietarios DEMO **no** está en el código. Usa una de estas opciones:

```powershell
# Opción A — variable de entorno (recomendada)
$env:CALE_DEMO_LAB_OWNER_PASSWORD = "<tu-clave-local>"
dotnet run --project tools/SeedLuzVerdeDemo -- --seed
dotnet run --project tools/SeedLuzVerdeDemo -- --purge
dotnet run --project tools/SeedLuzVerdeDemo -- --set-owners-password

# Opción B — argumento CLI
dotnet run --project tools/SeedLuzVerdeDemo -- --seed --lab-owner-password "<tu-clave-local>"
```

Si `--seed` corre sin env ni argumento, se genera una clave segura y se escribe en `docs/demo-luzverde-credenciales.local.md` (gitignored). `--set-owners-password` **exige** env o `--lab-owner-password`.

Por defecto: `src/Cale.Api/cale-demo-luzverde.db`.

## Producción (cealuzverde.com / Postgres Render)

Aditivo: solo crea/regenera registros DEMO. **No** migra esquema. **No** toca clientes reales.

```powershell
# 1) Dry-run (obligatorio revisar)
dotnet run --project tools/SeedLuzVerdeDemo -- `
  --promote-production --dry-run `
  --confirm-host "TU_HOST.render.com" `
  --confirm-database "micale" `
  --confirm-backup 2026-11-04 `
  --i-understand-additive-only `
  --connection "<cadena postgres>"

# 2) Tras backup confirmado y dry-run limpio:
dotnet run --project tools/SeedLuzVerdeDemo -- `
  --promote-production --seed `
  --confirm-host "TU_HOST.render.com" `
  --confirm-database "micale" `
  --confirm-backup 2026-11-04 `
  --i-understand-additive-only `
  --connection "<cadena postgres>"
```

También puede leer `appsettings.Development.local.json` **solo** con `--promote-production` (si tiene la cadena de prod).

### Propietarios

| Escuela | Correo |
|---|---|
| CEA DEMO NORTE | `demo.norte@cealuzverde.com` |
| CEA DEMO CENTRO | `demo.centro@cealuzverde.com` |
| CEA DEMO SUR | `demo.sur@cealuzverde.com` |

En promote, las contraseñas son **seguras e individuales**. Entrega: `docs/demo-luzverde-credenciales.local.md` (gitignored).

### Purge solo DEMO en producción

```powershell
dotnet run --project tools/SeedLuzVerdeDemo -- `
  --promote-production --purge `
  --confirm-host "..." --confirm-database "..." --confirm-backup YYYY-MM-DD `
  --i-understand-additive-only --connection "<postgres>"
```

## Reglas comerciales

- Courtesy para operar módulos internos
- Sin `PaymentAdminStatus=Confirmed`
- Sin FreeAccess global
- Sin publicación en directorio comercial

## Seguridad

Sin `--promote-production` la herramienta **aborta** si detecta Render/Production.
Con promote exige host, database, backup ≤7 días y flag aditivo.
Clave de laboratorio: `CALE_DEMO_LAB_OWNER_PASSWORD` o `--lab-owner-password` (nunca hardcodeada en el repo).
