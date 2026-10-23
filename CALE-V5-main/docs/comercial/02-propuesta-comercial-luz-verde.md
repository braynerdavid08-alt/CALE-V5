# Luz Verde  
## Formación que impulsa tu camino.

# Propuesta de digitalización y gestión para escuelas de conducción

**Documento:** propuesta comercial B2B (CEA / escuelas de conducción — Colombia)  
**Fecha de elaboración:** 22 de octubre de 2026  
**Versión:** borrador comercial basado en auditoría del repositorio (ver `01-auditoria-funcional-luz-verde.md`)  
**Contacto:** el correo y teléfono se publican en la página **Contacto** de la plataforma cuando el administrador los configura. Este documento no inventa datos de contacto.

> **Aviso:** el sistema legal completo (términos definitivos, proveedor de pagos, reembolsos, retención de datos) sigue pendiente de definición formal. Las condiciones contractuales finales deben revisarse con asesoría jurídica colombiana antes de firmar con clientes.

---

## 1. Presentación

**Luz Verde** es una plataforma digital pensada para acompañar la formación vial y la operación diaria de una escuela de conducción: desde la agenda y el expediente del aprendiz hasta el simulacro del examen teórico, el aula en vivo con el instructor y el seguimiento del progreso.

No es una hoja de cálculo ni un conjunto de chats sueltos. Es un sistema con paneles diferenciados para:

- la **escuela** (operación, aprendices, horarios, asistencia, teoría, práctica, resultados);
- el **instructor** (aula en vivo, presentaciones, grupos, biblioteca, informes);
- el **estudiante** (simulador, evaluaciones, cursos, agenda de examen y clases, progreso);
- y un **administrador de la plataforma** (aprobación de escuelas, contenido y soporte).

El propósito de esta propuesta es que usted, como propietario o gerente, pueda evaluar si Luz Verde le ayuda a **organizar la información, reducir trabajo manual repetitivo y ofrecer a sus aprendices una experiencia de estudio más clara**, con funciones que ya existen en el producto y que pueden demostrarse en una sesión de trabajo.

---

## 2. Problemas y oportunidades

Las siguientes situaciones son comunes en la operación de muchas escuelas. Luz Verde **puede ayudar a mejorarlas** cuando se usan las funciones indicadas. No afirmamos que su escuela tenga todos estos problemas.

### 2.1 Información de aprendices dispersa

| | |
|--|--|
| **Cómo suele verse** | Datos en Excel, carpetas y WhatsApp; difícil saber de un vistazo el estado de un aprendiz. |
| **Qué hace Luz Verde** | Listado de aprendices y **expediente digital** (dossier) con progreso y movimientos de cartera interna de la escuela. |
| **Cómo se usa** | La escuela registra o importa información, invita al estudiante a vincularse y consulta el expediente cuando lo necesita. |
| **Beneficio potencial** | Menos búsqueda entre archivos; una sola vista operativa. |
| **Cómo demostrarlo** | Abrir un expediente de prueba en la demo y mostrar filtros de la lista. |

### 2.2 Organización de horarios y clases

| | |
|--|--|
| **Cómo suele verse** | Agenda en papel o grupos de chat; choques de cupo o citas mal comunicadas. |
| **Qué hace Luz Verde** | **Agenda semanal**, programación de teoría, práctica, horarios de examen con cupos y control de examen en curso. |
| **Cómo se usa** | La escuela define horarios y cupos; el estudiante agenda según lo autorizado; el personal ve el día en curso. |
| **Beneficio potencial** | Mejor visibilidad del día y de la semana para el equipo. |
| **Cómo demostrarlo** | Mostrar agenda + pantalla de control de examen. |

### 2.3 Seguimiento de teoría, asistencia y práctica

| | |
|--|--|
| **Cómo suele verse** | Firmas sueltas, dudas sobre horas cumplidas. |
| **Qué hace Luz Verde** | Asistencia del día, clases teóricas, clases de manejo, flota del día y horas registrables en el flujo académico. |
| **Cómo se usa** | El personal marca asistencia y gestiona la operación teórica/práctica desde el panel escuela. |
| **Beneficio potencial** | Trazabilidad más clara frente a revisiones internas. |
| **Cómo demostrarlo** | Flujo de asistencia + vista de práctica/flota. |

### 2.4 Visibilidad del progreso del estudiante

| | |
|--|--|
| **Cómo suele verse** | «¿Ya está listo para el examen?» se responde a ojo. |
| **Qué hace Luz Verde** | Progreso del estudiante, resultados, simulador, evaluaciones y retos de práctica. |
| **Cómo se usa** | El aprendiz practica; la escuela e instructor consultan informes y el expediente. |
| **Beneficio potencial** | Decisiones de autorización de examen con más contexto. |
| **Cómo demostrarlo** | Simulador + pantalla de progreso/resultados. |

### 2.5 Tareas administrativas repetitivas

| | |
|--|--|
| **Cómo suele verse** | Reescribir listas, reenviar horarios, buscar comprobantes internos del aprendiz. |
| **Qué hace Luz Verde** | Importación Excel, notificaciones in-app, vinculación por invitación/solicitud, cartera de abonos del aprendiz (gestión de la escuela). |
| **Cómo se usa** | Importar, invitar, notificar y consultar saldos internos. |
| **Beneficio potencial** | Menos retrabajo administrativo. |
| **Cómo demostrarlo** | Importación (si aplica) + invitación de usuario. |

### 2.6 Refuerzo de conocimientos y experiencia digital

| | |
|--|--|
| **Cómo suele verse** | El estudiante estudia con PDFs o videos sueltos; poca práctica tipo examen. |
| **Qué hace Luz Verde** | Simulador, bancos, cursos virtuales, biblioteca jurídica, aula en vivo, presentaciones, juegos de refuerzo y **asistente de IA** (si está habilitado). |
| **Cómo se usa** | El estudiante entra con su cuenta vinculada a la escuela y usa las herramientas de estudio. |
| **Beneficio potencial** | Más práctica estructurada y acompañamiento digital. |
| **Cómo demostrarlo** | Login estudiante + 3 minutos de simulador o aula. |

### 2.7 Pagos de la escuela hacia Luz Verde

Hoy el producto contempla **membresía de escuela con comprobante de transferencia y activación por el administrador de la plataforma**. No hay pasarela electrónica integrada. El cobro automático y la facturación formal son **condiciones por definir** (ver sección 8 y lista de verificación).

La **cartera de abonos del aprendiz** es un módulo distinto: ayuda a la escuela a llevar saldos de sus estudiantes; no es el pago de la suscripción a Luz Verde.

---

## 3. Funcionalidades verificadas

Solo se listan capacidades encontradas en el producto. Detalle técnico: auditoría `01`.

### 3.1 Disponibles para demostrar (implementadas)

**Gestión de la escuela**

- Panel de operaciones e inicio  
- Agenda semanal  
- Aprendices, expediente, abonos internos, progreso  
- Asistencia teórica del día  
- Clases teóricas y de manejo; flota del día  
- Horarios y control de exámenes teóricos  
- Resultados  
- Instructores y usuarios (invitar / aceptar solicitudes)  
- Importación y exportación Excel  

**Instructores**

- Aula en vivo  
- Presentaciones  
- Grupos y materiales  
- Biblioteca (preguntas, bancos, exámenes, normas)  
- Informes  
- Actividad «100 Estudiantes Dijeron»  

**Estudiantes**

- Simulador y evaluaciones  
- Cursos virtuales y biblioteca jurídica  
- Aula en vivo (ingreso)  
- Progreso, retos, ranking y logros  
- Agendar examen / teoría / práctica cuando la escuela lo habilita  
- Notificaciones y perfil  

**Plataforma**

- Aprobación de escuelas por administrador  
- Notificaciones y push opcional  
- PQR (canales de solicitud)  
- Tienda pública de cascos (catálogo + WhatsApp)  

**Inteligencia artificial (si hay clave de proveedor configurada)**

- Asistente para **estudiante** y **escuela**, con límites diarios de uso  
- Clave del proveedor solo en el servidor  

### 3.2 Parciales o con condiciones

| Tema | Situación honesta |
|------|-------------------|
| Membresía / planes de Luz Verde | Flujo de comprobante existe; hoy puede estar **desactivado** por modo acceso gratis. |
| Marketplace de instructores | Directorio público de instructores independientes; no sustituye la gestión interna de su CEA. |
| Clases particulares (instructor) | Existe pantalla; no está destacada en el menú. |
| Precios en el código | Hay montos históricos distintos a los de esta propuesta (ver §8). |

### 3.3 No disponibles aún (no se venden como hechos)

- Pasarela de pagos con tarjeta / PSE automática  
- Facturación electrónica DIAN  
- Panel de overage de IA con cobro adicional automatizado  
- Roles «secretaria» con permisos reducidos  
- Creación de cuentas de estudiantes por la escuela (solo vinculación con consentimiento)  

---

## 4. Beneficios por perfil

### Propietario / gerente

- Visión centralizada de agenda, aprendices y resultados.  
- Menos dependencia de archivos sueltos.  
- Base para profesionalizar la imagen digital ante el aprendiz.  
- Relacionado con: dashboard, agenda, resultados, expediente.

### Personal administrativo

- Listas, importaciones, asistencia del día, control de citas de examen.  
- Relacionado con: aprendices, import Excel, attendance, exam control.

### Instructores

- Herramientas de clase (vivo, presentaciones, grupos) e informes.  
- Relacionado con: live, presentations, groups, results.

### Estudiantes

- Práctica tipo examen, seguimiento de su proceso y canales claros con la escuela.  
- Relacionado con: simulador, progreso, agenda, notificaciones, IA (con límites).

---

## 5. Ventajas de una plataforma centralizada

Tener la operación y el estudio en un solo entorno facilita:

1. **Consultar** el estado de un aprendiz sin reconstruir la historia a mano.  
2. **Compartir** la misma información entre administración e instructores.  
3. **Mostrar** al estudiante qué sigue (teoría, práctica, examen).  
4. **Reducir** pérdida de información cuando cambia el personal.

Esto no garantiza por sí solo un aumento de ingresos. El resultado depende de la adopción del equipo y de la disciplina de uso.

---

## 6. Implementación y acompañamiento

### Ya existe en el producto (técnico)

- Registro de escuela y flujo de aprobación por administrador.  
- Vinculación de usuarios por invitación o solicitud.  
- Configuración de perfil de escuela y, cuando aplique, datos de facturación/membresía.  
- Contenido pedagógico de plataforma (cursos, bancos) administrable por el equipo Luz Verde.

### Debe formalizarlo el responsable comercial de Luz Verde (servicio)

| Etapa | Qué incluye (propuesta de servicio) | ¿Automatizado hoy? |
|-------|-------------------------------------|--------------------|
| Configuración inicial | Alta, revisión de datos, activación | Parcial (aprobación admin) |
| Usuarios | Capacitar el flujo de invitación | Manual / acompañamiento |
| Capacitación | Sesión remota o presencial del panel escuela + instructor + estudiante | **Servicio a ofrecer** |
| Adopción | Seguimiento 2–4 semanas | **Servicio a ofrecer** |
| Soporte | Canal y horarios | **Por definir** |

---

## 7. Modelo comercial y precios

### Precios de referencia para esta propuesta

| Plan | Precio | Notas |
|------|--------|--------|
| **Mensual** | **$500.000 COP** | Referencia comercial definida por el propietario para esta oferta |
| **Anual** | **$5.000.000 COP** | Equivale a un **ahorro de $1.000.000 COP** frente a 12 × $500.000 |

### Qué recibe la escuela (funciones verificadas)

Al suscribirse (cuando el cobro esté activo y la escuela aprobada), el personal y los aprendices vinculados pueden usar el **panel operativo**, las herramientas de **formación** (simulador, cursos, aula, etc.) y el **asistente de IA incluido**, sujeto a límites de uso (ver `03-modelo-comercial-ia.md`).

### Condiciones que usted aún debe definir (no inventadas aquí)

- Impuestos (IVA u otros) y forma de factura.  
- Costo de instalación o migración (si aplica).  
- Límite de estudiantes / instructores incluidos (en el código hay cupos históricos distintos; **no se asumen** en esta oferta hasta que usted los unifique).  
- Permanencia mínima, renovación y cancelación.  
- Política de reembolsos.  
- Proveedor de pagos y medio de recaudo (hoy: transferencia + comprobante, si se reactiva el flujo).  
- Consentimiento y tarifas de **consumo adicional de IA**.

### Diferencia importante con el código actual

En el repositorio existen planes hardcodeados (aprox. $150.000 / $800.000 / $1.500.000 con cupos). **Esta propuesta usa los precios que usted indicó ($500.000 / $5.000.000).** Antes de publicar cotizaciones, unifique precios en producto, contrato y materiales de venta.

También debe decidir cuándo desactivar el modo **acceso gratis** (`FreeForAll`) para que la membresía se exija de verdad.

---

## 8. Proceso de contratación sugerido

1. **Contacto inicial** (WhatsApp, correo o formulario Contacto).  
2. **Reunión de diagnóstico** (tamaño de la escuela, dolores operativos).  
3. **Demostración** de la plataforma (ver guion en `04-guion-ventas-y-demostracion.md`).  
4. **Entrega de esta propuesta** y aclaración de condiciones pendientes.  
5. **Definición del plan** (mensual o anual) y aceptación contractual.  
6. **Configuración y puesta en marcha** (aprobación, usuarios, capacitación).  
7. **Seguimiento de adopción** (agenda, aprendices, uso de estudio/IA).

---

## 9. Cierre

Luz Verde ya concentra, en un solo producto, piezas que muchas escuelas siguen resolviendo a pedazos: agenda, expediente, clases, examen, práctica del estudiante e instructor en vivo.

Si le interesa evaluar si encaja con su operación, **agende una demostración**. En esa sesión revisamos su flujo real y le mostramos únicamente lo que la plataforma ya hace, con claridad sobre lo que aún debe formalizarse en contrato y en política de inteligencia artificial.

**Siguiente paso:** coordine una demo con el equipo comercial de Luz Verde a través del canal de contacto publicado en la plataforma.
