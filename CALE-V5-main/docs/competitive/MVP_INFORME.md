# Informe MVP — Ecosistema competitivo Luz Verde

## 1. Auditoría
- Duelos: memoria + polling; se mantienen.
- Logros/XP/ranking: Play (`BadgeCatalog`, `UserAchievement`, `GameResult`, ranking 2 años).
- Torneos/trofeos: no existían (greenfield).
- SignalR útil como patrón: Live/GameShow; no se reutiliza su motor.

## 2. Reutilizado
Banco de preguntas, `GameResult` (+ `SourceKey`), `BadgeCatalog`/`UserAchievement`, `XpRules` de duelo, scopes de ranking, JWT/roles, patrones hub.

## 3. Entidades nuevas
`Tournament`, `TournamentParticipant`, `TournamentRound`, `TournamentMatch`, `TournamentMatchQuestion`, `TournamentAnswer`, `TournamentTrophy`.

## 4. Migraciones / schema
`TournamentSchemaGuard` (Postgres/Sqlite) + `PartidasJuego.SourceKey`. Arranque vía `WebApplicationExtensions`.

## 5. Servicios
`TournamentService` (+ Start/Match/Rewards), `TournamentBracket`, `TournamentBroadcaster`, `TournamentTimeoutWorker`, extensiones `PlayService`.

## 6. Endpoints
`/api/tournaments` (CRUD lobby, join, start, bracket, match/answer, trophies, profile). Ranking: `?period=&metric=`.

## 7. SignalR
Hub `/hubs/tournaments`: JoinTournament, eventos lobby/match/champion/achievements.

## 8. XP
`GameKinds.Tournament` con mismas reglas que duelo; sin XP por BYE/walkover; idempotencia `SourceKey`.

## 9. Trofeos
Un `TournamentTrophy` por campeonato (snapshot de nombre).

## 10. Logros
`campeon_torneo`, `campeon_torneo_5`, `campeon_torneo_10`.

## 11. Ranking
Pesos `RankingWeightsV1` (50/30/20). Métricas: general/xp/trophies/achievements. Periodos: 30d–all (default 2y). Default scope escuela.

## 12. Pruebas (validación MVP)

| Prueba | Resultado | Evidencia |
|---|---|---|
| Build API | PASS | `dotnet build` |
| Build Angular | PASS | `ng build --configuration=development` |
| TournamentBracketTests | PASS | 7/7 |
| Smoke 13 jugadores (API real in-process SQLite) | PASS | `TournamentSmokeTests` — create → join×13 → start → 3 BYE → partidas → campeón → trofeo → `campeon_torneo` → sin XP por BYE → SourceKey único |
| Roles Admin/School/Teacher/Student (HTTP) | PASS | mismos smoke tests (permisos create/start/join/list/ranking) |
| Aislamiento entre escuelas | PASS | join/start cross-school → 403 |
| Idempotencia trofeo (unique index) | PASS | segundo insert falla |
| UI Angular visual en navegador | NOT TESTED | requiere entorno local/staging con datos |
| SignalR reconexión / walkover 60s | NOT TESTED | worker deshabilitado en host de test; falta prueba dedicada |
| Carga 16×8 simultáneos | NOT TESTED | falta entorno de carga |
| Redis multi-instancia | BLOCKED | no hay Redis en Render Free; documentado en DEPLOY.md |
| Smoke en producción / staging remoto | BLOCKED | no desplegado (política: sin merge/deploy) |

Corrección durante validación: BYEs se emparejan siempre contra jugadores (nunca BYE vs BYE).

## 13–14. Carga / roles
Roles HTTP: PASS en smoke. Carga 16×8: NOT TESTED. No merge ni deploy a producción.

## 15. Archivos clave
Backend: `Services/Tournaments/*`, `TournamentHub`, `TournamentSchemaGuard`, Play ranking/badges.
Frontend: `play/api/tournament.api.ts`, páginas tournament-*, ranking/home.

## 16. Commits
Ver historial de la rama `feat/theory-instructor-competitive-ecosystem`.

## 17. Teoría / profesor
Incluido en commit 1 de la rama.

## 18. V2
Ver `docs/competitive/V2_PENDIENTES.md` (Redis multi-instancia, 32/64, ledger XP, etc.).

## 19. Deploy Render
1. Desplegar API (schema guard crea tablas al arrancar).
2. Una instancia: OK.
3. Multi-instancia: configurar Redis backplane antes.
4. No activar hasta smoke de roles y torneo 13 jugadores con BYE.
