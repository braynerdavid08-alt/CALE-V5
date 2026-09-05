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

## P2 — UX / rendimiento (pendiente)

11. Contar preguntas de bancos en un solo SQL (no N+1).
12. `StartExamHandler.ResumeAsync` sin loop N+1.
13. Flujo Escuela: crear/asignar exámenes o aclarar Biblioteca read-only.
14. CI GitHub Actions estable.
15. Tests e2e críticos: login, import Word, publish, assign, student take.

---

## Checklist smoke esta noche (manual)

### Auth
- [ ] Login → reload mantiene sesión
- [ ] Logout → sin cookies /me 401
- [ ] Usuario mustChangePassword: API 403 hasta cambiar clave

### Instructor Biblioteca
- [ ] Import Word → revisar claves → publicar → asignar **grupo propio**
- [ ] Solo ves bancos oficiales + tuyos
- [ ] Intentar asignar grupo ajeno → 403
- [ ] Publicar sin banco / sin claves → error claro
- [ ] Asignar sin publicar → error `exam_not_published`
- [ ] Eliminar → desaparece; no se puede editar

### Roles
- [ ] Teacher sin membresía: banner claro
- [ ] Admin entra a Biblioteca
- [ ] Student: ve examen asignado publicado

### Público
- [ ] Landing con `/api/public/home` ok

---

## Orden sugerido resto de noche

1. Smoke checklist en `micale.onrender.com` tras deploy
2. P2 N+1 / CI / e2e cuando toque
