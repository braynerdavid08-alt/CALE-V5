# Catálogo de producto — Luz Verde

**Fecha:** 28 de octubre de 2026  
**Eslogan:** Formación que impulsa tu camino.  
**Uso de este documento:** base para catálogo comercial, propuesta B2B a CEA/escuelas y guion de demostración.  
**Criterio:** solo capacidades verificadas en el producto (Angular + API). Lo parcial o pendiente se marca aparte.

> **Aviso legal:** el sistema legal completo (términos definitivos, política de privacidad final, proveedor de pagos, reembolsos, retención de datos) sigue **pendiente de definición y revisión jurídica**. Ver `docs/legal/PENDIENTE_SISTEMA_LEGAL.md`. No inventar NIT, razón social ni datos de contacto: usar los que configure el administrador en la plataforma.

---

## 1. Qué es Luz Verde

Plataforma digital para **escuelas de conducción (CEA)** y sus aprendices en Colombia. Une en un solo sistema:

1. **Operación de la escuela** (agenda, expedientes, teoría, manejo, exámenes, cartera interna).  
2. **Formación del estudiante** (simulacro, cursos, biblioteca, retos).  
3. **Aula digital del instructor** (vivo, presentaciones, juegos).  
4. **Asistente Kazu (IA)** para consultas y agenda, con cuota diaria.

Roles: **Administrador de plataforma**, **Escuela**, **Instructor**, **Estudiante**.

---

## 2. Resumen para la propuesta (pitch corto)

| Para la escuela | Para el aprendiz | Para el instructor |
|-----------------|------------------|--------------------|
| Expediente digital y cartera de abonos | Simulacro ilimitado del examen teórico | Aula en vivo con código/QR |
| Agenda semanal + reporte Excel del día | Cursos virtuales y biblioteca jurídica | Presentaciones y grupos |
| Teoría, manejo y cupos de examen | Agendar teoría / examen / manejo | 100 Estudiantes Dijeron |
| Operación de hoy (asistencia) | Retos, ranking y logros | Informes de resultados |
| Importar / exportar Excel | Asistente Kazu | — |
| Invitar instructores y estudiantes | App instalable (PWA) | — |

---

## 3. Catálogo por módulo (listo para vender / demostrar)

### 3.1 Panel Escuela — operación diaria

| Producto / función | Qué resuelve | Estado |
|--------------------|--------------|--------|
| **Inicio operativo** | Pendientes, KPIs y accesos a la operación | Disponible |
| **Estudiantes y expedientes** | Listado, filtros, dossier por aprendiz | Disponible |
| **Cartera / abonos** | Saldos y pagos internos del aprendiz (no es cobro a Luz Verde) | Disponible |
| **Agenda semanal** | Teoría + manejo + exámenes en una vista | Disponible |
| **Reporte del día (Excel)** | Descarga de programación del día/mañana (inscritos con nombre, cédula, celular, fecha de agendamiento) | Disponible (envío WhatsApp / cron: preparados, apagados) |
| **Clases teóricas** | Salones, temas, cupos, reservas | Disponible |
| **Clases de manejo / flota** | Sesiones prácticas y vehículos | Disponible |
| **Exámenes teóricos** | Plantillas de horario, cupos, control check-in / no-show | Disponible |
| **Operación de hoy** | Asistencia y actividad del día | Disponible |
| **Resultados y progreso** | Notas e intentos de estudiantes | Disponible |
| **Instructores y usuarios** | Invitaciones / solicitudes de vinculación | Disponible |
| **Importar / exportar Excel** | Carga masiva de expedientes y descargas | Disponible |
| **Bancos y preguntas (consulta)** | Ver bancos oficiales / de escuela | Disponible |
| **Plan y membresía** | Comprobante + revisión admin (oculto si modo gratis) | Parcial |

### 3.2 Panel Estudiante — formación y agenda

| Producto / función | Qué resuelve | Estado |
|--------------------|--------------|--------|
| **Inicio / panel** | Accesos, progreso, guía de bienvenida (tour con Kazu) | Disponible |
| **Simulador de examen** | Práctica tipo examen teórico | Disponible |
| **Mis evaluaciones** | Evaluaciones asignadas | Disponible |
| **Cursos virtuales** | Malla de lecciones (normas, señales, vía, vehículo, primeros auxilios, categorías A2/B1/C1) | Disponible |
| **Biblioteca jurídica** | Normas de tránsito consultables | Disponible |
| **Reto diario / juegos / ranking / logros** | Práctica gamificada y motivación | Disponible |
| **Mis clases** | Materiales de la CEA | Disponible |
| **Teoría / agendar examen / manejo** | Reserva de cupos (requiere vinculación a escuela) | Disponible |
| **Asistente Kazu** | Consultas de horas, agenda y ayuda con confirmación antes de cambios | Disponible (cuota diaria) |
| **Notificaciones / perfil / PWA** | Avisos, preferencias, instalar en el teléfono | Disponible |
| **Tienda y donaciones** | Catálogo de equipo + apoyo voluntario | Disponible |

### 3.3 Panel Instructor — clase y contenido

| Producto / función | Qué resuelve | Estado |
|--------------------|--------------|--------|
| **Aula en vivo** | Sesión con código/QR, preguntas y ranking | Disponible |
| **Presentaciones** | Materiales para proyectar / presentar | Disponible |
| **Grupos** | Organización de grupos de clase | Disponible |
| **100 Estudiantes Dijeron** | Juego de refuerzo en clase | Disponible |
| **Cursos virtuales** | Acceso a la malla | Disponible |
| **Biblioteca / exámenes / bancos / preguntas** | Contenido pedagógico | Disponible |
| **Informes** | Resultados de estudiantes | Disponible |

### 3.4 Plataforma y administración Luz Verde

| Producto / función | Qué resuelve | Estado |
|--------------------|--------------|--------|
| **Directorio / aprobación de escuelas** | Alta y control de CEAs | Disponible |
| **Usuarios, instructores, estudiantes** | Soporte transversal | Disponible |
| **Contenido** | Preguntas, bancos, exámenes, cursos, Señal relámpago | Disponible |
| **Reportes** | Actividad, uso, resultados, valoraciones | Disponible |
| **Homepage / tienda CMS** | Portada y catálogo de cascos/equipo (pedido por WhatsApp) | Disponible |
| **Documentos legales versionados** | Términos, privacidad, datos, IA (borradores + aceptación) | Disponible (textos aún borrador) |
| **PQR y derechos del titular** | Radicado, seguimiento, panel admin | Disponible |
| **Menores / representante** | Flujo de autorización (revisión jurídica pendiente) | Parcial / revisión legal |
| **Notificaciones push / in-app** | Avisos a usuarios | Disponible |

### 3.5 Inteligencia artificial — Kazu

| Capacidad | Detalle | Estado |
|-----------|---------|--------|
| Chat para **estudiante** y **escuela** | Consultas rápidas sin gastar cuota + chat con IA | Disponible |
| Confirmación antes de cambios | Agenda / reservas con paso de confirmación | Disponible |
| Límites diarios | Por usuario, escuela y presupuesto global | Disponible |
| Privacidad en el diseño | Sin pedir documento/celular en el chat; aviso de errores posibles | Disponible |
| Envío automático WhatsApp de reportes | Preparado en código; **apagado** (sin costo Meta/Cron) | Pendiente de activación |

---

## 4. Experiencia pública (sin login)

- Landing / portada configurable.  
- Catálogo de **cursos** públicos.  
- **Tienda** de cascos y equipo (consulta por WhatsApp `wa.me`, sin pasarela).  
- Páginas legales públicas (`/legal/*`).  
- Formulario **PQR** público.  
- Donaciones voluntarias (QR / enlace configurado).

---

## 5. Paquetes sugeridos para el catálogo comercial

> Modelo: **estudiantes practican gratis**; **escuelas pagan** membresía; **instructores** entran con el plan del CEA. Precios de referencia: **Mensual $500.000 COP** y **Anual $5.000.000 COP** (`SchoolPlans`). `FreeForAll` es solo override de piloto (default producto `false`). Si Render aún tiene `Access__FreeForAll=true`, no anunciar cobro automático hasta apagarlo. Cupos “0” en código = sin tope fijo; **no decir “ilimitado”** sin confirmar política.

### Paquete A — Operación CEA

Agenda, expedientes, cartera interna, teoría, manejo, exámenes, asistencia, import/export, reporte Excel del día, usuarios.

### Paquete B — Formación del aprendiz

Simulador, evaluaciones, cursos virtuales, biblioteca jurídica, retos/ranking, agenda estudiante, Kazu.

### Paquete C — Aula digital

Aula en vivo, presentaciones, grupos, GameShow, informes instructor.

### Paquete D — Plataforma completa

A + B + C + tienda + PQR + legales versionados + métricas admin.

### Add-ons (futuro / no vender como activo aún)

| Add-on | Nota |
|--------|------|
| WhatsApp Cloud + reportes 14:00 / 17:00 | Código listo; requiere Meta + costos + activación |
| Pasarela PSE / tarjeta | No implementada |
| Facturación electrónica DIAN | No implementada |
| Roles “secretaria” | Solo en planes documentales |

---

## 6. Qué NO anunciar como producto cerrado

| Tema | Motivo |
|------|--------|
| Cobro automático de membresía | Suele estar en modo gratis; flujo = comprobante + admin |
| Envío WhatsApp automático de programación | Apagado a propósito |
| Marketplace completo de instructores | Directorio / parcial |
| Creación masiva de cuentas por la escuela | Retirado; vinculación por invitación/solicitud |
| Textos legales “definitivos” | Borradores; falta abogado y datos empresariales |
| Ilimitado de estudiantes/IA | Límites existen o están por confirmar |

---

## 7. Argumentos de valor (para la propuesta)

1. **Una sola plataforma** para operar la CEA y formar al aprendiz.  
2. **Menos Excel y WhatsApp sueltos** en agenda, cupos y expedientes.  
3. **Trazabilidad** de horas, autorizaciones y asistencia.  
4. **Práctica tipo examen** antes del día real.  
5. **Instructor con herramientas de clase** (vivo + juegos + presentaciones).  
6. **Reporte del día descargable** para compartir por el canal que la escuela ya usa.  
7. **Kazu** reduce preguntas repetitivas de horarios y cupos (con confirmación).

---

## 8. Demo sugerida (20–25 min)

1. Login **escuela** → Inicio → Expediente + cartera.  
2. Agenda semanal → Reporte del día (descargar Excel).  
3. Cupos de examen + operación de hoy.  
4. Login **estudiante** → Simulador 2 min → Cursos / biblioteca.  
5. Login **instructor** → Aula en vivo o GameShow (1 ronda).  
6. Abrir **Kazu** (consulta rápida de horas o agenda).  
7. Cerrar con paquete A/B/C y siguientes pasos (piloto / membresía).

---

## 9. Materiales relacionados en el repo

| Documento | Contenido |
|-----------|-----------|
| `docs/comercial/01-auditoria-funcional-luz-verde.md` | Auditoría detallada por estado |
| `docs/comercial/02-propuesta-comercial-luz-verde.md` | Propuesta narrativa B2B |
| `docs/comercial/inventario-funcionalidades-comerciales.md` | Tabla corta V / P / Pend |
| `docs/comercial/04-guion-ventas-y-demostracion.md` | Guion de demo |
| `docs/school/REPORTES_PROGRAMACION.md` | Reportes Excel / WhatsApp futuro |
| `docs/school/programacion-hoy-ejemplo.xlsx` | Ejemplo visual del Excel |
| `docs/legal/PENDIENTE_SISTEMA_LEGAL.md` | Legal pendiente |

---

## 10. Próximos pasos comerciales (checklist)

- [ ] Confirmar precios y cupos oficiales (¿vigentes $500k / $5M?).  
- [ ] En Render: apagar `FreeForAll` tras cortesía/planes a escuelas actuales (código ya alineado a membresía).  
- [ ] Completar datos legales empresariales y revisión de abogado.  
- [ ] Decidir cuándo activar WhatsApp Cloud + Cron (costos Meta/Render).  
- [ ] Armar PDF/one-pager a partir de las secciones 2, 5 y 7 de este catálogo.  
- [ ] Agendar demos con capturas reales (`_capturas/` en el monorepo).

---

*Documento generado a partir del estado del producto al 28/10/2026. Actualizar este archivo cuando se activen módulos nuevos o cambien precios.*
