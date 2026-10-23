# Modelo comercial de inteligencia artificial — Luz Verde

**Fecha:** 22 de octubre de 2026  
**Prioridad:** alta  
**Base técnica:** `AssistantOptions`, `AssistantService`, `AssistantLlmClient`, `AssistantController`, widget frontend  
**Aviso legal:** la política de IA definitiva, el proveedor y la retención de conversaciones siguen con placeholders; ver `docs/legal/PENDIENTE_SISTEMA_LEGAL.md`.

---

## A. Acceso incluido en la suscripción

La contratación del plan **mensual** o **anual** de Luz Verde (cuando la membresía esté activa) habilita, para los estudiantes vinculados a esa escuela y para el rol escuela, el uso del **asistente de Luz Verde**, siempre que:

1. El asistente esté habilitado (`Assistant:Enabled`).  
2. Exista una `ApiKey` de proveedor configurada **solo en el servidor**.  
3. El usuario pertenezca a un rol soportado (**Student** o **School**). Instructores y administradores **no** tienen asistente en el código actual.

### Qué puede hacer el asistente hoy (verificado)

Según herramientas expuestas en `AssistantToolbox`:

**Estudiante (lectura / orientación):** consultar progreso, horarios de teoría, cupos de examen, clases prácticas y otras consultas conectadas a los mismos servicios de la app. Acciones sensibles requieren confirmación explícita cuando aplica el flujo de confirmación.

**Escuela:** resúmenes operativos y consultas de progreso de estudiantes **de su propia escuela** (aislamiento por `SchoolId` / usuario escuela).

El asistente **no** debe presentarse como:

- un abogado o autoridad de tránsito;
- un reemplazo del instructor;
- un sistema con memoria ilimitada de conversaciones (el historial no se guarda de forma durable en servidor).

---

## B. Límites diarios de mensajes — estado comprobado

### Hallazgos técnicos

| Parámetro | Estado actual comprobado | Propuesta comercial | Ajuste pendiente |
|-----------|--------------------------|---------------------|------------------|
| Mensajes por estudiante / día | **Sí:** default **20** (`DailyMessagesPerUser`); se expone `remainingToday` | Mantener un cupo diario **configurable** por ambiente; cifra comercial definitiva tras medir costo | Definir cifra comercial y documentarla en contrato |
| Reinicio diario | **Sí:** clave con fecha Colombia (`ColombiaTime.TodayInColombia`) | Comunicar «se reinicia cada día (hora Colombia)» | Confirmar zona y mensaje UX |
| Consumo diferenciado por escuela | **Parcial:** tope de **requests al proveedor** por escuela/día (default **60**), no un «paquete de mensajes de la escuela» facturable | Cupo incluido de la escuela = suma de uso de estudiantes + staff escuela | Definir métrica comercial (mensajes vs tokens vs $) |
| Consulta de uso por admin escuela | **No** hay panel de consumo IA | Construir panel de consumo (incluido / restante / extra) | Desarrollo + diseño |
| Límite de tokens / costo por request | **No** se registran tokens ni costo | Medir tokens de entrada/salida y modelo | Integrar respuesta del proveedor |
| Bloqueo al alcanzar límite | **Sí** (límites diarios en memoria + rate limit 20 POST/min) | Mantener bloqueo; añadir mensaje claro y opción de cupo extra **autorizado** | UX + política |
| Distinción mensaje usuario vs respuesta IA vs llamadas internas | **Parcial:** cuenta mensajes de usuario y **requests** de proveedor (un mensaje puede gastar varias rondas de tools, default hasta 4) | Explicar que un mensaje puede consumir varias «llamadas» internas | Copy comercial + telemetría |
| Registro de errores / reintentos | **Parcial:** reintento 429/503 una vez; error en español al usuario; **no** hay ledger de fallos; el contador puede haberse consumido antes del fallo | Política: no cobrar overage por fallos del proveedor; idealmente no descontar cupo si falla | Ajuste de código + política |
| Persistencia de contadores | **No durable:** `IMemoryCache` (se pierde al reiniciar; frágil con varios servidores) | Contadores en base de datos por día/usuario/escuela | Desarrollo obligatorio antes de vender overage |
| Instructor / Admin con IA | **No soportado** | Fuera de oferta inicial o fase 2 | Decisión de producto |

**Conclusión:** hay **límites antiabuso** útiles para un proveedor gratuito o barato, pero **no** un sistema de facturación de consumo listo para cobrar overage con transparencia contractual.

El número definitivo de mensajes diarios incluidos debe quedar como **parámetro configurable**, pendiente de definir según el **costo real del proveedor**.

---

## C. Consumo adicional facturable a la escuela (propuesta)

> Esta sección es una **propuesta de diseño**. **No está implementada** en el código actual. No debe presentarse al cliente como función ya activa.

### Principio comercial (obligatorio)

**No debe existir cobro adicional automático y sorpresivo.**

Antes de cualquier cargo extra, la escuela debe conocer por escrito:

1. Límite incluido.  
2. Unidad de medida (idealmente **costo real en tokens/modelo**, no solo «mensajes»).  
3. Tarifa o fórmula del consumo extra.  
4. Impuestos aplicables.  
5. Mecanismo de **autorización expresa** (contrato + aceptación en panel).  

Si no hay consentimiento, al llegar al límite el sistema **detiene o limita** nuevas solicitudes.

### Mecanismo propuesto (10 puntos)

| # | Componente | Descripción |
|---|------------|-------------|
| 1 | Límite incluido | Cupo mensual (o diario agregado) por escuela, derivado de la suscripción |
| 2 | Medición | Contadores durables por escuela y por estudiante; requests y tokens |
| 3 | Costo atribuible | Guardar modelo, tokens in/out, costo estimado del proveedor por request |
| 4 | Panel escuela | Vista «Consumo de IA»: incluido, usado, restante, extra autorizado |
| 5 | Alertas | Notificación in-app (y correo si se define) al **80 %** y **100 %** |
| 6 | Regla al tope | Configurable: **bloquear** · **pedir autorización** · **permitir con presupuesto preaprobado** |
| 7 | Reporte mensual | Incluido vs adicional, por estudiante top consumers |
| 8 | Historial auditable | Tabla tipo ledger (análoga a `MembershipEvents`) con cargos y quién autorizó |
| 9 | Fallos / reintentos | No facturar requests fallidos del proveedor; registrar incidencia |
| 10 | Abuso | Mantener rate limit; detectar patrones automatizados; opción de suspender IA por escuela |

### Distinción de precios (no inventar recargo)

```
Costo real proveedor  →  + margen comercial (por definir)  →  precio facturable a la escuela
```

- **Costo real:** lo que Luz Verde paga al proveedor (por tokens/modelo).  
- **Margen:** decisión comercial del propietario (**pendiente**; no fijado aquí).  
- **Precio final:** lo que se cotiza a la escuela tras autorización.

---

## D. Cómo definir el límite incluido (método, sin inventar consumo)

### Variables del método

1. Precio de suscripción (ej. $500.000 / mes o $5.000.000 / año).  
2. Estudiantes habilitados (vinculados activos).  
3. % estimado que usa IA.  
4. Mensajes promedio / usuario activo / día.  
5. Tokens entrada y salida promedio.  
6. Modelo y tarifa del proveedor.  
7. Margen objetivo.  
8. Colchón de variabilidad (picos de temporada).

### Fórmula ilustrativa (simulación — no son costos reales)

```
Costo_IA_mensual_estimado ≈
  estudiantes × %_activos × msgs/día × 30
  × (tokens_in + tokens_out)_promedio
  × precio_por_token_del_modelo
  × (1 + factor_reintentos_tools)
```

Luego:

```
Cupo_incluido_máximo_sostenible ≈
  (porción_del_plan_asignada_a_IA) / (1 + margen)
```

### Escenarios etiquetados como SIMULACIÓN

| Escenario | Hipótesis (inventadas solo para método) | Lectura |
|-----------|------------------------------------------|---------|
| Conservador | Pocos usuarios activos, modelo barato | Cupo diario alto relativo al plan |
| Base | Uso moderado | Cupo medio + overage opcional |
| Pico | Muchos mensajes cerca del examen | Necesita alerta 80 % y tope duro |

**Recomendación:** lanzar con **límite configurable**, medir 4–8 semanas de uso real y **después** fijar la oferta comercial de incluidos + extra.

---

## E. Seguridad y aislamiento entre escuelas

### Comprobado

| Control | Estado |
|---------|--------|
| API key solo en backend | **Sí** |
| Tools de escuela filtrados por escuela del usuario | **Sí** (revisión de `SchoolId`) |
| Rate limiting HTTP del chat | **Sí** (20/min/usuario) |
| Historial de chat en BD compartida | **No hay store** → no hay fuga por tabla de chats; tampoco hay auditoría de contenido |

### Riesgos / mejoras propuestas

1. Contadores en memoria: en despliegue multi-instancia los límites se diluyen → **persistir en BD**.  
2. Sin panel de consumo: una escuela no ve su uso (tampoco el de otra), pero Luz Verde tampoco factura con evidencia → **ledger por tenant**.  
3. No exponer nunca la clave en frontend, apps móviles embebidas ni repositorios públicos.  
4. Revisar que reportes futuros de IA filtren siempre por `SchoolId` del caller.

---

## F. Texto comercial sugerido (adaptado a la realidad)

Versión usable en propuestas, con condiciones pendientes marcadas:

> «La suscripción de Luz Verde habilita el acceso de los estudiantes y del personal de la escuela a las herramientas de inteligencia artificial incluidas en la plataforma, con **límites diarios de uso** definidos de forma transparente.  
>  
> Hoy el sistema aplica cupos diarios por usuario y topes de solicitudes por escuela y por la plataforma; la **cifra comercial definitiva**, el **cupo mensual incluido** y las **tarifas de consumo adicional** se informarán en las condiciones del servicio una vez medido el costo real del proveedor.  
>  
> Si la escuela requiere un consumo superior al incluido, podrá **contratar consumo adicional** solo con **autorización previa** y conforme a tarifas acordadas. Sin esa autorización, el uso se limita al alcanzar el tope.  
>  
> Luz Verde trabajará para que la escuela pueda consultar su consumo; el panel detallado de overage es una capacidad **en definición/implementación**, no una promesa de función ya desplegada.»

Texto corto (cuando el overage ya esté construido):

> «La suscripción incluye IA con límites diarios transparentes. El consumo extra se contrata solo con autorización previa; la escuela puede ver su uso y controlar el gasto.»

---

## G. Checklist técnico antes de vender overage

- [ ] Contadores durables por usuario/escuela/día y mes  
- [ ] Tokens y modelo por request  
- [ ] Panel escuela + alertas 80/100 %  
- [ ] Flag de autorización de presupuesto extra  
- [ ] Ledger auditable  
- [ ] Política de fallos (no cobrar / no descontar)  
- [ ] Textos en contrato + política de IA revisada por abogado  
- [ ] Pruebas de aislamiento multi-tenant en reportes  

Hasta completar lo anterior, la oferta comercial debe hablar de **IA incluida con límites diarios**, no de **factura automática por exceso**.
