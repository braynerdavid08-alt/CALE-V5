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
- IDOR asignar grupo, soft-delete respetado, cookies logout, publicar exige banco con preguntas
- **Hoy:** bancos por dueño, mustChangePassword API, published scoped, catalog en exams, errores UI, Admin↔Biblioteca, banner membresía, asignar solo si publicado

---

## P0 — Seguridad / multi-tenant

1. [x] **Bancos por dueño** — `Bank.CreatedById`; listado = oficiales (null) + propios; admin ve todos; import marca dueño.
2. [x] **Review/export por ownership** — review filtrado; export exige dueño (sesión previa).
3. [x] **`mustChangePassword` en API** — middleware 403 + `APP_INITIALIZER` await bootstrap + FE redirect en 403.
4. [x] **Revocar refresh al cambiar contraseña**.

## P1 — Correctitud producto

5. [x] **`GET /api/exams/published` scoped** — teacher solo propios.
6. [x] **CatalogAccess en ExamsController**.
7. [x] **Errores visibles** — banks/groups en library.
8. [x] **Admin ↔ Biblioteca** — `/teacher/library` con roles Teacher+Admin.
9. [x] **Membresía inactiva** — `?membresia=1` + banner en home instructor.
10. [x] **Asignar solo si publicado**.

## P2 — UX / rendimiento

11. [x] Contar preguntas de bancos en un solo SQL (no N+1).
12. [x] `StartExamHandler.ResumeAsync` (+ finish/review) sin loop N+1.
13. [x] Flujo Escuela: Catálogo solo lectura aclarado en nav/UI; published no trata School como estudiante.
14. CI GitHub Actions estable.
15. Tests e2e críticos: login, import Word, publish, assign, student take.

---

## Orden sugerido resto de noche

1. Smoke checklist en `micale.onrender.com` tras deploy
2. CI / e2e cuando toque
