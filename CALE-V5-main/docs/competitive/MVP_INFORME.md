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
| Redis multi-instancia real | BLOCKED | sin Redis contratado; arquitectura documentada en DEPLOY.md |
| Staging/producción remoto | BLOCKED | sin merge/deploy |

### Fase 3 — navegador real (2026-11-06 / 07)
Entorno: API local **SQLite** (`cale-dev.db`) + `ng serve` con proxy `/api` y `/hubs`.  
Cuentas locales sembradas con `tools/SeedFase3Competitive` (no producción).  
E2E: `frontend/e2e/fase3-competitive.spec.ts` (Playwright Chromium, 2 browser contexts).  
Evidencias: `docs/competitive/fase3-evidencias/`.

| Prueba | Resultado | Cómo se verificó |
|---|---|---|
| Recorrido autenticado Teacher (login/sesión, listado, crear, lobby, admin) | PASS | Navegador real Playwright + capturas `01-*`, `02-*` |
| Recorrido autenticado Student (lobby, partida, bracket, ranking, perfil, sin admin) | PASS | Navegador real + capturas `03-*`, `05-*`, `07-*`–`10-*`; botón Iniciar ausente para student |
| SignalR dos navegadores independientes | PASS | Dos `BrowserContext` Chromium en la misma sala/partida; chip en vivo / estado compartido |
| Desconexión + reconexión de un cliente | PASS | `context.setOffline(true/false)` + reload; estado UI recuperado (`05b-reconexion-s2`) |
| Avance de bracket / campeón / trofeos / XP / ranking | PASS | Partidas aceleradas vía API (misma lógica aprobada F1/F2); UI bracket/perfil/ranking en navegador |
| Responsive 1366 / 768 / 480 / 375 | PASS | Capturas `11-responsive-*` + assert sin overflow horizontal en 375 |
| Walkover 60s observado visualmente en UI | NOT TESTED | Cubierto en Fase 2 automatizado; Fase 3 validó corte/reconexión corta, no el timer completo de 60s |
| SignalR TestServer | PASS (Fase 2) | No sustituye la prueba de dos browsers (ya hecha arriba) |

Corrección Fase 1: BYE nunca vs BYE.  
Corrección Fase 2: walkover con ambos offline elige desconexión más reciente.  
Corrección Fase 3: proxy Angular `/hubs` (ws) para SignalR vía `ng serve`.


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
