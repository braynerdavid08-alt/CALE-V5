# Plan de mejoras Mi CALE

Última actualización: 2026-09-05. Continuamos en la noche.

## Estado producción (smoke rápido)

- `GET /api/health` → ok
- `GET /api/public/home` → 200
- `GET /api/public/schools` → 200

## Ya entregado esta semana (sesión)

- Home a prueba de fallos CMS
- Menú instructor = Escuela/Admin
- Import/export Word + panel crear inline
- Revisar claves post-import
- Editar examen
- Bloqueo publicar sin claves / sin banco
- Asignar/publicar post-import
- Eliminar examen (soft-delete)
- **Hoy (parcial):** IDOR asignar grupo, soft-delete respetado en update/publish/assign, cookies logout correctas, publicar exige banco con preguntas

---

## P0 — Seguridad / multi-tenant (hacer primero esta noche)

1. **Bancos por dueño/escuela** — Import crea bancos globales; todos los instructors los ven. Scope por `CreatedById` / escuela + bancos oficiales CALE.
2. **Review/export por ownership** — `GET /api/questions/review` y export no deben leer bancos ajenos.
3. **`mustChangePassword` en API** — Hoy solo el front bloquea; JWT sigue válido. Middleware/policy + await `bootstrap()` antes de rutas.
4. **Revocar refresh al cambiar contraseña** — Evitar sesión vieja tras cambio forzado.

## P1 — Correctitud producto

5. **`GET /api/exams/published` scoped** — Teacher/Admin no deben ver todos los publicados del sistema.
6. **CatalogAccess en ExamsController** — Alinear UI (`catalogAccessGuard`) con API.
7. **Errores visibles** — Banks/groups en library/live: mostrar error, no lista vacía silenciosa.
8. **Admin ↔ Biblioteca unificada** — Admin hoy no entra a `/teacher/library`.
9. **Membresía inactiva** — Mensaje claro (no redirect silencioso a `/teacher`).
10. **Asignar solo si publicado** (o marcar assignment draft) — Evitar notificar examen no abierto.

## P2 — UX / rendimiento

11. Contar preguntas de bancos en un solo SQL (no N+1).
12. `StartExamHandler.ResumeAsync` sin loop N+1.
13. Flujo Escuela: crear/asignar exámenes o aclarar Biblioteca read-only.
14. CI GitHub Actions estable (`.github/workflows` aún sin subir bien).
15. Tests e2e críticos: login, import Word, publish, assign, student take.

---

## Checklist smoke esta noche (manual)

### Auth
- [ ] Login → reload mantiene sesión
- [ ] Logout → sin cookies /me 401
- [ ] Usuario mustChangePassword: no entra a teacher hasta cambiar clave

### Instructor Biblioteca
- [ ] Import Word → revisar claves → publicar → asignar **grupo propio**
- [ ] Intentar asignar grupo ajeno → 403
- [ ] Publicar sin banco / sin claves → error claro
- [ ] Eliminar → desaparece; no se puede editar
- [ ] Editar nombre/minutos → guarda

### Roles
- [ ] Teacher sin membresía: mensaje claro
- [ ] School: catálogo lectura
- [ ] Student: ve examen asignado publicado

### Público
- [ ] Landing con `/api/public/home` ok
- [ ] (si se puede) home caído → fallback SPA

---

## Orden sugerido esta noche

1. Terminar P0 bancos/ownership + mustChangePassword API  
2. P1 published scoped + errores UI  
3. Smoke checklist completo en `micale.onrender.com`  
4. Commit/PR solo cuando cada bloque esté verde  
