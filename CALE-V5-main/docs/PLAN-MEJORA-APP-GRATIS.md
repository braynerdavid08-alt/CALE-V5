# Plan de mejora: Mi CALE gratis (control y calidad)

Fecha: 30 sep 2026. Contexto: la app pasó a ser gratis para todos (PR #129), con solicitudes de usuarios (PR #130) y ajustes de juegos (PR #131 a #134). Al haber más usuarios sin filtro de pago, hace falta más control del administrador.

## Revisión hecha: imágenes subidas

- Las imágenes de preguntas y solicitudes se guardan en la **base de datos** (`ICatalogMediaStore`, URL `/api/media/{guid}`). **No se pierden al redesplegar** en Render.
- Riesgo 1: las rutas antiguas `/uploads/...` viven en el disco del contenedor y **sí** se pierden al redesplegar. Hay que revisar si quedan preguntas con esas rutas y migrarlas a la base.
- Riesgo 2: cada imagen ocupa espacio en PostgreSQL. Las imágenes de solicitudes rechazadas o retiradas quedan huérfanas y hacen crecer la base.
- Detalle: el tope de 20 imágenes por día se cuenta en memoria y se reinicia si el servidor se reinicia (aceptable).

## Resuelto el 1 oct 2026

- Límite de intentos **por IP** (antes el de inicio de sesión era global para toda la app): inicio de sesión 10/min, registro 20 cada 10 min, confirmación y reenvío de código 10 cada 10 min, reporte de errores 30/min. Respuesta 429 en español.
- El tope de 20 imágenes por día (estudiantes y escuelas) se cuenta en la base de datos (últimas 24 h), así que ya no se reinicia con el servidor.
- Al rechazar, retirar o aceptar con cambios una solicitud, se borran las imágenes que ya no use nada (y se quitan del historial de la solicitud).
- Migración automática al arrancar: las imágenes `/uploads/...` de preguntas y respuestas se copian a la base de datos si el archivo aún existe. Las que ya se perdieron quedan listadas en el log como advertencia.
- Aviso crítico en el log si `Jwt:Key` es la clave de ejemplo o tiene menos de 32 caracteres.
- Corregida la prueba de arquitectura que fallaba (`SchoolJoinRequestHandler` usaba EF Core directamente).
- **3.1** Página *Contenido → Señal relámpago*: preguntas en el juego, avance por examen de señales y lista de preguntas que no entran (sin imagen, sin clave, sin una única respuesta correcta) con botón «Editar».
- **3.2** Contador de solicitudes pendientes en el menú del administrador (se actualiza cada minuto y al aceptar o rechazar).

**Pendiente de verificar en Render (manual):** que exista la variable `Jwt__Key` con un secreto propio, y revisar en el log si aparece la advertencia de imágenes `/uploads` perdidas.

## Fase 0: tareas manuales en producción (administrador)

1. Copia de seguridad de la base de datos.
2. Borrar los 3 bancos de ejemplo y los duplicados de CEA VIP.
3. Marcar con «Hacer oficial» los bancos buenos (alimentan el reto diario, el duelo y Señal relámpago).
4. Confirmar que `braynerdavid08` tiene rol Admin (si no, Señal relámpago solo toma las preguntas de sus exámenes que estén en bancos oficiales).

## Fase 1: seguridad y abuso (prioridad alta)

| # | Tarea | Por qué |
|---|-------|---------|
| 1.1 | Revisar si en producción el registro exige código por correo. Si no, configurar el envío de correo. | Sin verificación se pueden crear cuentas falsas sin límite. |
| 1.2 | Límite de registros e inicios de sesión por IP (rate limiting de ASP.NET Core). | Frenar bots y fuerza bruta. |
| 1.3 | Captcha opcional en el registro (Cloudflare Turnstile, gratis). | Segunda barrera si 1.2 no basta. |
| 1.4 | Pasar el contador de subidas por día a la base de datos (contar en la tabla de medios por usuario y fecha). | Que el tope no se reinicie con el servidor. |

## Fase 2: almacenamiento (prioridad media)

| # | Tarea | Por qué |
|---|-------|---------|
| 2.1 | Script o endpoint de administrador que liste las preguntas con imágenes `/uploads/...` y las migre a la base si el archivo aún existe. | Evitar imágenes rotas tras un redespliegue. |
| 2.2 | Borrar automáticamente las imágenes de solicitudes rechazadas o retiradas (si no las usa ninguna pregunta). | Ahorrar espacio en PostgreSQL. |
| 2.3 | Panel con el tamaño total de las imágenes en la base. | Vigilar el límite del plan de base de datos. |
| 2.4 | Comprimir o redimensionar las imágenes al subirlas (por ejemplo, máximo 1200 px, WebP). | Menos espacio y carga más rápida en el celular. |

## Fase 3: herramientas del administrador (prioridad media)

| # | Tarea | Por qué |
|---|-------|---------|
| 3.1 | Indicador «Señal relámpago»: cuántas preguntas entran y lista de las de exámenes de señales **sin imagen**, con enlace para editarlas. | Saber qué completar. |
| 3.2 | Contador de solicitudes pendientes junto a «Solicitudes de usuarios» en el menú. | Que no se acumulen sin revisar. |
| 3.3 | Opción de bloquear a un usuario para enviar solicitudes (si abusa). | Control directo. |
| 3.4 | Resumen semanal para el administrador: usuarios nuevos, solicitudes, actividad en juegos. | Ver el uso de la app gratis. |

## Fase 4: calidad del contenido (prioridad baja)

| # | Tarea | Por qué |
|---|-------|---------|
| 4.1 | Botón «Reportar pregunta» en simulacros y juegos (respuesta mal marcada, imagen rota). Llega como solicitud al administrador. | Los estudiantes ayudan a depurar el banco. |
| 4.2 | Detectar preguntas duplicadas al aceptar una solicitud (texto parecido). | Evitar repetidas en el banco. |
| 4.3 | Estadística por pregunta (porcentaje de aciertos) para detectar preguntas confusas o mal marcadas. | Mejorar la calidad del banco. |

## Orden sugerido para mañana

1. Fase 0 (manual, tú).
2. 1.1 y 1.2 (seguridad básica).
3. 2.1 (revisar imágenes antiguas en disco).
4. 3.1 y 3.2 (indicadores en el panel).
5. Lo demás según prioridad.
