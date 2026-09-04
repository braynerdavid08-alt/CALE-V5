# Auditoría CALE — checklist para esta noche

Fecha: 2026-09-04  
Actualizado: fixes aplicados en `fix/audit-night-fixes`

Canvas presentaciones: **1920 × 1080** (legacy 960 × 540 se escala)

---

## Completado

### Bloque A — Live / Presentaciones (P0)
1. [x] Presentar no marca correcta en proyector (live/embed)
2. [x] Dedupe server (misma quick) + localStorage compartido host/clicker
3. [x] Start no reabre índice 0 si ya hay pregunta activa
4. [x] Slide 0 auto-abre desde host al cargar deck

### Bloque B — Live UX (P1)
5. [x] Clamp índice + clicker escucha PresentationSlideChanged
6. [x] Exam: host conserva highlight tras RevealUpdated (merge FE + broadcast)
7. [x] Presentar en vivo usa questionCount 0 (deck + preguntas en slides)
8. [x] Deck móvil compact más usable
9. [x] Seed reconocimiento: `replaceExisting: true`

### Bloque C — Roles / acceso (P1)
10. [x] catalogAccessGuard en banks y library
11. [x] Admin questions → `/admin/questions/...`
12. [x] Admin results → redirect `/admin/results`
13. [x] Join live sin plan: se deja (estudiantes deben poder unirse)

### Bloque D — Polish (P2)
14. [ ] Multi-select resize (aplazado)
15. [x] Undo pregunta: sin pushHistory por tecla
16. [x] questionToLivePayload no inventa correcta si opción vacía
17. [x] Simulador en nav estudiante
18. [x] Token live por sessionId

---

## Probar en Render tras deploy
Admin / Escuela / Docente / Estudiante — Presentar en vivo, pregunta en slide, roster, join.
