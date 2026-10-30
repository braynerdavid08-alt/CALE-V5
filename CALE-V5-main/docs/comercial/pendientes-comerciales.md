# Decisiones pendientes antes de enviar la propuesta a un cliente

**Fecha:** 23 de octubre de 2026

Completar estos puntos evita contradicciones entre el documento, el contrato y el producto.

## Comercial

1. **Fecha de apagar `Access:FreeForAll` en Render** (código ya default `false`; Render puede seguir con env `true` hasta otorgar cortesía/planes). Modelo: estudiante gratis · escuela paga · instructor vía escuela.
2. **Cupos** de estudiantes e instructores incluidos en cada plan (hoy el catálogo no fija un tope comercial anunciable).
3. **¿El precio incluye impuestos?** Pendiente de confirmación comercial y contable.
4. **Medio de recaudo** (transferencia + confirmación admin vs futuro proveedor de pagos).
5. **Permanencia, renovación, cancelación y reembolsos.**
6. **Canal y horario de soporte** (correo, WhatsApp, tiempos de respuesta).
7. **Horas de capacitación** incluidas en el onboarding.
8. **Datos de contacto comercial** (teléfono, correo, nombre del representante) para portada y cierre — hoy: *Contacto comercial por definir*.

## Producto / IA

9. Cifra comercial definitiva de **mensajes diarios de IA** por estudiante (default técnico: 20).
10. Si se ofrecerá **ampliación de cuota IA** y con qué tarifa (hoy no hay cobro automático de excesos).

## Legal

11. Responsable / razón social / NIT / dirección / correo legal (sistema legal aún con placeholders).
12. Textos definitivos de términos, privacidad y política de IA revisados por abogado colombiano.

> Recordatorio: el sistema legal completo sigue aplazado (`docs/legal/PENDIENTE_SISTEMA_LEGAL.md`). No afirmar cumplimiento normativo no verificado.

## Cómo actualizar esta propuesta

1. Cambiar precios en admin (`CommercialPlans`) y en `SchoolPlans.cs` si aplica.
2. Actualizar tablas en `propuesta-comercial-luz-verde.md`.
3. Regenerar el PDF:

```bat
python docs\comercial\generar_propuesta_pdf.py
```

4. Revisar capturas en `_capturas/` si la UI cambió.
