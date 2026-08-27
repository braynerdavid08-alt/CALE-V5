# CALE v5.0 — Prompt de producto (mejorado y recortado)

Usa este archivo como **especificación de producto**.  
Para arquitectura, patrones, carpetas, Clean Code y nombres, usa en el **mismo chat** el archivo:

`PROMPT_CALE_ARQUITECTURA_Y_CLEAN_CODE.md`

Pégalos juntos. Si hay conflicto: **gana arquitectura/calidad**. No sacrifiques capas por meter más features.

---

## Rol

Eres un ingeniero senior. Reconstruyes **CALE** (simulador de conducción + aula virtual) **desde cero**.

El código viejo solo sirve como referencia de **negocio, entidades y endpoints**.  
**No copies** shells monolíticos, HTML gigante ni CSS en una sola línea.

---

## Qué es CALE

Plataforma educativa para formar conductores:

```
Admin → plataforma
Teacher → aula (su grupo)
Student → aprender + practicar + evaluar
```

El estudiante entra y debe saber: **¿qué hago ahora?**  
El docente: **¿quién necesita atención?**  
El admin: **¿cómo está la plataforma?**

---

## Stack

- Backend: ASP.NET Core 8, EF Core, SQL Server, JWT, ProblemDetails
- Frontend: Angular 18 standalone, TypeScript, RxJS, Reactive Forms, lazy routes, guards, interceptors
- API: `http://localhost:5000` · UI: `http://localhost:4200`
- DB: `CaleSimuladorDb_Limpia` (Trusted Connection)
- Admin seed: solo en Development (no publicar credenciales en docs/UI)
- JWT: Issuer `Cale.Api`, Audience `Cale.Frontend`
- Scripts: `INICIAR_CALE.bat` debe matar `:5000`, `:4200` y `Cale.Api.exe` **antes** de compilar (evitar MSB3027)

---

## Roles (JWT, inglés)

`Admin` | `Teacher` | `Student`

Legacy: `Alumno`/`Estudiante` → Student · `Profesor` → Teacher  
Registro público **solo** crea Student.

Permisos se validan **siempre en backend** (no basta ocultar botones).

| Capacidad | Admin | Teacher | Student |
|-----------|-------|---------|---------|
| Usuarios | sí | no | no |
| Bancos / preguntas / exámenes | sí | sí (propios) | no |
| Grupos | todos | solo `TeacherId == yo` | unirse por código |
| Aula (avisos, material, actividades) | sí | solo sus grupos | consultar / entregar |
| Calificar | sí | su grupo | consultar |
| Resultados | globales | su grupo | propios |
| Simulador | prueba | prueba | sí |
| Respuestas correctas | admin | admin | **solo después de finish** |
| Valoraciones | gestionar | — | una por intento |

Reglas fijas:

- Aprobar ≥ **80%**
- Tiempo de examen lo marca el **servidor** (`StartedAt`, `ExpiresAt`, `FinishedAt`)
- No iniciar examen antes de `StartsAt` ni después de `EndsAt`
- Respetar `AllowedAttempts`
- Contenido inactivo no se muestra al consumidor
- API/DTOs/código en **inglés**; UI en **español**
- **Cero datos ficticios**. Empty ≠ Error ≠ Loading

---

## Alcance: MVP primero (obligatorio)

No implementes gamificación, plan de estudio, IA ni chat hasta que el MVP esté sólido.

### Auth y perfil
Login, registro, JWT interceptor, guards, perfil, cambio de contraseña, logout.

### Simulador `/student/simulator`
`configure → start → answer → finish → review`  
Start **no** expone `isCorrect` / `explanation`.  
Review solo post-finish.  
Siempre **Volver al panel**. Confirmar abandono.  
Imágenes `/uploads` resueltas contra el **origen de la API**, no `:4200`.  
Resultado: puntaje, aprobado, por tema/bloque, mejor marca, valoración 1–5.

### Preguntas / bancos / exámenes
CRUD admin + teacher.  
Opciones A/B/C… con **radio de respuesta correcta** (nunca índice 0-based).  
Verdadero/falso. Imágenes. Paginación y filtros.

### Grupos
Nombre, código `CALE-XXXXXXXX` (case-insensitive), descripción, fecha inicio, docente, activo.  
Unirse, agregar por correo, retirar, copiar código, archivar.  
Al unirse, heredar exámenes ya programados al grupo.

### Aula virtual (por grupo)
Avisos, material por módulo, actividades/talleres/trabajos, entregas (única por actividad+usuario), calificar, exámenes programados al grupo.

### Dashboards reales
- Student: nombre, grupo, docente, pendientes, progreso, avisos, notificaciones
- Teacher: atención requerida (entregas, bajo rendimiento), sus grupos
- Admin: métricas reales + usuarios, valoraciones, resultados, actividad, configuración útil

### Notificaciones reales
Al publicar aviso/material/actividad/examen, al entregar y al calificar.  
Leer / marcar leída. Contador unread.

---

## Después del MVP (no ahora)

Solo cuando el MVP pase `dotnet test` + `ng build` + smoke de 3 roles:

1. Repasar errores + recomendaciones por tema  
2. Preguntas problemáticas + reportar pregunta  
3. Calendario académico  
4. Analítica avanzada (docente/admin)  
5. Gamificación ligera (logros/racha)  
6. Plan de estudio  
7. Comunicación contextual (no chat)  
8. Preparar puerto `IStudyAssistantService` (IA futura, no improvisada)

---

## Estados de ítems (UI + backend)

`Pending | Available | InProgress | Submitted | Graded | Expired | Exhausted`  
UI en español: Pendiente, Disponible, En progreso, Entregado, Calificado, Vencido, Agotado.

Cada petición HTTP: **Loading / Success / Empty / Error**. Nunca pantalla vacía si falló la API.

---

## Datos / DB

Reutilizar tablas legacy (nombres ES en columnas si hace falta) con propiedades C# en inglés (`Active` → `Activo`/`Activa`).

Entidades aula: avisos, materiales, actividades, entregas, examen-grupo.  
Índices únicos: rating↔attempt, entrega↔(activity,user), examen↔grupo.

Schema incremental (`IF NOT EXISTS`), **sin borrar datos**.

---

## Endpoints (REST, inglés)

```
/api/auth
/api/profile
/api/student
/api/teacher
/api/admin
/api/exams          (take: start/finish/review/banks/published)
/api/questions
/api/groups
/api/classroom
/api/notifications
/api/ratings
/api/requests
/api/analytics      (solo después del MVP)
```

Paginación estándar: `{ items, page, pageSize, totalItems, totalPages }`.

---

## UX / diseño

Tema navy actual. Tokens CSS en `:root`.  
1 objetivo + 1 acción principal por pantalla.  
Responsive, prioridad móvil del estudiante.  
Accesible (label, focus, contraste; no solo color).  
Simulador usable en celular.

---

## Scripts y docs

`INICIAR_CALE.bat`, `DETENER_CALE.bat`, `START_API.bat`, `START_FRONTEND.bat`  
Docs mínimas: `README.md`, `ARCHITECTURE.md`, `API.md`

---

## Fases (cortas)

1. Solución + Clean Architecture + auth + seed + design tokens + scripts  
2. Preguntas, bancos, exámenes (API + UI admin/teacher)  
3. Simulador start/finish/review + media  
4. Grupos + aula + notificaciones  
5. Dashboards 3 roles + valoraciones + resultados + actividad  
6. Tests + E2E smoke + pulido responsive  

Tras cada fase: `dotnet test` && `ng build` && login de 3 roles.

---

## Criterio de “terminado” (MVP)

- Arquitectura del prompt de arquitectura respetada  
- Datos reales, permisos reales, 3 roles OK  
- Estudiante entiende qué hacer; docente qué atender  
- Errores visibles; empty ≠ error  
- Código mantenible (límites de archivo del prompt de arquitectura)  
- Tests verdes  

**Empieza por Fase 1.** Primero estructura y contratos, luego código. No shells gigantes.
