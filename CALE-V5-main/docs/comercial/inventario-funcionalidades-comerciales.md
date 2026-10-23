# Inventario de funcionalidades comerciales — Luz Verde

**Fecha:** 23 de octubre de 2026  
**Criterio:** solo lo verificado en código (FE + BE). No se anuncia en la propuesta lo parcial o pendiente como «disponible».

Leyenda: **V** = verificada · **P** = parcial · **Pend** = pendiente / no lista para vender

---

## Verificadas (aptas para la propuesta B2B)

| Área | Funcionalidad | Evidencia principal |
|------|---------------|---------------------|
| Escuela | Agenda semanal | `school-agenda.page.ts`, `ExamScheduleController` |
| Escuela | Aprendices + expediente | `school-apprentices`, `school-apprentice-dossier` |
| Escuela | Abonos / saldo del aprendiz (cartera interna) | `SchoolApprenticeController` |
| Escuela | Asistencia del día | `school-attendance-today` |
| Escuela | Clases teóricas | `school-theory.page.ts` |
| Escuela | Clases de manejo / flota | `school-practical`, `school-practical-fleet` |
| Escuela | Horarios y control de examen | `school-exam-schedule`, `school-exam-control` |
| Escuela | Resultados | `school-results.page.ts` |
| Escuela | Invitar / aceptar instructores y estudiantes | `school-users`, join-requests |
| Escuela | Import / export Excel | `school-import`, exports |
| Instructor | Aula en vivo | `teacher-live-*`, `LiveController` |
| Instructor | Presentaciones | `PresentationsController` |
| Instructor | Grupos / materiales | `GroupsController`, `ClassroomController` |
| Instructor | Biblioteca e informes | teacher library / results |
| Instructor | 100 Estudiantes Dijeron | `GameShowController` |
| Estudiante | Simulador y evaluaciones | `simulator.page.ts` |
| Estudiante | Cursos virtuales / biblioteca jurídica | rutas student |
| Estudiante | Progreso, retos, ranking | módulo Play |
| Estudiante | Agendar examen / teoría / práctica (con escuela) | student exam/practical |
| Plataforma | Asistente IA (estudiante y escuela) con cuota diaria | `AssistantService`, `AssistantUsageStore` |
| Comercial | Catálogo planes Mensual $500.000 / Anual $5.000.000 | `SchoolPlans.cs`, `CommercialPlans` seed |
| Comercial | Activación manual / cortesía / pago interno | `SchoolProfile`, admin memberships |

## Parciales (no anunciar como producto completo)

| Tema | Estado | Nota |
|------|--------|------|
| Membresía con cobro efectivo | P | Flujo existe; `FreeForAll` suele estar activo en piloto |
| Marketplace de instructores | P | Directorio público; no es marketplace de la CEA |
| Clases particulares (teacher) | P | Ruta sin entrada clara en menú |
| Validación QA formal de teoría/flota | P | En app; cobertura de pruebas limitada |

## Pendientes (no vender)

| Tema | Nota |
|------|------|
| Pasarela PSE / tarjeta | No implementada |
| Facturación electrónica DIAN | No implementada |
| Cobro automático de excesos de IA | No implementado (solo corte + contactar admin) |
| Crear cuentas masivas desde la escuela | Retirado; solo invitación/solicitud |
| Roles «secretaria» | Solo en planes documentales |

## Precios vs catálogo

| Fuente | Mensual | Anual |
|--------|---------|-------|
| Oferta comercial de esta propuesta | $500.000 | $5.000.000 |
| `SchoolPlans.cs` (fallback) | $500.000 | $5.000.000 |
| Seed `CommercialPlans` | $500.000 | $5.000.000 |

**Coherentes.** Cupos en catálogo: `0` / `null` = sin tope fijo en código; la propuesta **no** dice «ilimitado» — indica «límites por confirmar».

## Capturas reales disponibles (repo)

Carpeta `_capturas/` (raíz del monorepo), entre otras:

- `diseno-escuela.png` — panel escuela  
- `appr-pc-list.png` / `appr-pc-dossier.png` — aprendices / expediente  
- `student-home-pc.png` / `auto-home-pc.png` — estudiante  
- `asistente-pc.png` — asistente IA  
- `diseno-instructor.png` — instructor  

Logo oficial: `frontend/public/brand/luz-verde-logo.png`
