# TournamentSchemaGuard — PostgreSQL (staging)

## Qué hace

Al arrancar la API, `TournamentSchemaGuard.EnsureAsync` se ejecuta **siempre** (también en Production con `Database:ApplyFeatureSchema=false`). Aplica DDL idempotente:

| Objeto | Notas |
|---|---|
| `Torneos` | Torneo + capacidad, estado, bracket, campeón |
| `TorneoParticipantes` | UNIQUE `(TournamentId, UserId)` |
| `TorneoRondas` | UNIQUE `(TournamentId, RoundNumber)` |
| `TorneoEnfrentamientos` | UNIQUE `(TournamentId, RoundId, SlotIndex)` |
| `TorneoEnfrentamientoPreguntas` | UNIQUE `(MatchId, Ordinal)` |
| `TorneoRespuestas` | UNIQUE `(MatchQuestionId, UserId)` — idempotencia de respuesta |
| `TrofeosTorneo` | UNIQUE `(TournamentId, UserId)` — un trofeo por campeonato |
| `PartidasJuego.SourceKey` | columna + UNIQUE parcial (idempotencia XP) |

Índices de consulta: escuela/estado, fecha, usuario, status de enfrentamiento.

Si una sentencia falla, se registra warning y se continúa (otras tablas pueden quedar listas). Revisar logs:  
`TournamentSchemaGuard applied (...)`.

## Production

- `AllowEnsureCreated=false`, `ApplyFeatureSchema=false` → el guard de torneos **sigue** corriendo.
- No hay migración EF separada para torneos en este MVP; el guard es la fuente de esquema.
- Una instancia Render: OK. Multi-instancia: Redis para SignalR (ver `DEPLOY.md`).

## Procedimiento staging (NO producción)

1. Provisionar una base **Postgres de staging** (Render free distinto, o Docker local). **Nunca** la URL de `micale` producción.
2. Variables mínimas:
   - `ASPNETCORE_ENVIRONMENT=Staging` (o Development sin `appsettings.Development.local.json` de prod)
   - `ConnectionStrings__Cale=<URL staging>`
   - `Jwt__Key=<secreto ≥32 chars>`
   - `Database__AllowEnsureCreated=false`
   - `Database__ApplyFeatureSchema=false`
3. Arrancar la API una vez y confirmar en logs el mensaje del SchemaGuard sin errores críticos.
4. Smoke:
   - `POST /api/auth/login` Teacher staging
   - `POST /api/tournaments` capacidad 4
   - Join 4 students → `POST .../start`
   - Verificar tablas: `\dt Torneo*` / `TrofeosTorneo` y índice `IX_PartidasJuego_SourceKey`
5. Apagar el entorno staging; no promover datos a producción.

## SQLite vs PostgreSQL

Lab/E2E usan SQLite. El guard tiene **DDL separado** para Postgres. Validar siempre el arranque en Postgres staging antes del primer deploy a producción.
