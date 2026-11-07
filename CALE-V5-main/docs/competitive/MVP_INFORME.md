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

## 12. Pruebas (Fase 1 + Fase 2)

### Fase 1 (no repetidas)
| Prueba | Resultado | Evidencia |
|---|---|---|
| Build API / Angular | PASS | builds previos |
| TournamentBracketTests | PASS | 7/7 |
| Smoke 13 jugadores | PASS | `TournamentSmokeTests` |
| Roles HTTP + aislamiento escuelas | PASS | smoke |
| Idempotencia trofeo | PASS | unique index |

### Fase 2
| Prueba | Resultado | Evidencia |
|---|---|---|
| Walkover 60s + sin XP + idempotencia | PASS | `TournamentPhase2Tests.Walkover_after_60s…` |
| Walkover ambos desconectados (determinista) | PASS | gana quien se desconectó más tarde |
| Reconexión / estado persistido vía API | PASS | disconnect/reconnect + GET match conserva respuestas |
| SignalR `JoinTournament` autenticado | PASS | HubConnection contra TestServer |
| Carga 16 jugadores / 8 matches concurrentes | PASS | `elapsedMs=2067; httpErrors=0; finishedFirstRound=8` (máquina local, in-process) |
| Seguridad endpoints (anon/outsider/no-jugador) | PASS | 401/403 |
| UI responsive CSS (listado/bracket/stats) | PASS | media queries 768/480 + overflow-x bracket |
| UI autenticada en navegador real (lobby/match/home) | NOT TESTED | falta API+`ng serve` con cuentas demo; pasos abajo |
| SignalR dos browsers vivos + desconexión de red | NOT TESTED | requiere 2 clientes UI manuales |
| Redis multi-instancia real | BLOCKED | sin Redis contratado; arquitectura documentada en DEPLOY.md |
| Staging/producción remoto | BLOCKED | sin merge/deploy |

### Pasos manuales pendientes (UI navegador)
1. Arrancar API + `ng serve`.
2. Login Teacher → `/teacher/tournaments` → crear torneo capacidad 4.
3. Login 4 Students → listado → lobby → SignalR participantes.
4. Teacher inicia → verificar bracket + match en dos ventanas.
5. Cortar red en uno → esperar 60s → walkover visible en bracket.
6. Revisar ranking/home/perfil/trofeos en Student.
7. Viewports 1440 / 768 / 390.

Corrección Fase 1: BYE nunca vs BYE.  
Corrección Fase 2: walkover con ambos offline elige desconexión más reciente.


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
