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

## 12. Pruebas
Unit: `TournamentBracketTests` (7 OK). Build API + Angular OK.
Pendiente en entorno real: smoke 13 jugadores + carga 16×8 + roles.

## 13–14. Carga / roles
No ejecutadas en producción aún. No desplegar hasta verificar schema + roles.

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
