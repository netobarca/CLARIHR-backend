---
name: update-business-flows
description: Usa esta skill cuando una historia de usuario o requerimiento cambia el comportamiento funcional del sistema y necesitas actualizar el documento vivo de flujos de negocio para reflejar el estado actual, sin duplicar información en el archivo de la HU ni crear flujos paralelos por historia.
---

# Update Business Flows

## 1. Propósito

Esta skill existe para mantener actualizado el documento vivo de flujos de negocio del proyecto cuando una historia de usuario cambia el comportamiento funcional del sistema.

Su objetivo es asegurar que:

- el flujo funcional vigente del sistema quede documentado,
- el documento vivo de negocio represente el estado actual,
- la HU deje solo trazabilidad puntual,
- y no se creen versiones paralelas o duplicadas del mismo flujo.

---

## 2. Cuándo usar esta skill

Usar esta skill cuando una HU o requerimiento:

- agrega una nueva capacidad funcional,
- cambia pasos de un flujo existente,
- cambia estados o transiciones del proceso,
- cambia comportamiento visible para el usuario,
- cambia reglas funcionales del sistema,
- agrega un nuevo subflujo o variante del proceso,
- modifica decisiones funcionales relevantes del negocio.

### Casos típicos
- “Actualiza el flujo de negocio de esta HU”
- “La historia cambió el proceso funcional, actualiza business flows”
- “Refleja este nuevo comportamiento en el flujo vivo”
- “No quiero dejar el cambio solo en la HU; actualiza el flujo actual del sistema”

---

## 3. Cuándo NO usar esta skill

No usar esta skill para:

- implementar la HU,
- revisar arquitectura o code quality,
- actualizar seguridad, performance o testing,
- cerrar toda la documentación de la HU,
- documentar cambios puramente técnicos sin impacto funcional,
- crear un flujo nuevo por cada historia.

Si la tarea principal es implementación backend, usar:
- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`

Si la tarea principal es cierre documental integral, usar:
- `.agents/skills/close-user-story-docs/SKILL.md`

Si la tarea principal es actualizar análisis vivos, usar:
- `.agents/skills/update-live-analysis-docs/SKILL.md`

---

## 4. Fuentes de verdad obligatorias

Antes de editar flujos de negocio, revisar en este orden:

1. `docs/technical/overview/project-foundation.md`
2. `/AGENTS.md`
3. `docs/AGENTS.md`
4. la HU o requerimiento fuente
5. el comportamiento funcional real implementado o acordado
6. `business/current-system-business-flows.md` (externo)
7. `analysis/changes/HU-XXXX.md` (externo, si ya existe)

NOTA: La documentación de flujos de negocio se maneja por fuera del proyecto. Pide la ruta externa al usuario antes de modificar o crear.

---

## 5. Regla madre

`business/current-system-business-flows.md` (externo) representa el **estado funcional actual** del sistema.

### Regla de decisión
- Si la HU cambia el comportamiento funcional vigente, actualizar el documento vivo.
- Si la HU solo deja un rastro histórico o una nota puntual, registrarlo en `HU-XXXX.md`.
- Si no cambió el comportamiento funcional real, no hacer cambios artificiales en business flows.

### Nunca hacer
- crear un archivo de flujo por HU,
- duplicar el flujo vigente dentro de `HU-XXXX.md`,
- mantener varias versiones manuales del mismo flujo sin una fuente oficial,
- usar el documento vivo como histórico por iteración.

---

## 6. Entradas mínimas esperadas

Para usar correctamente esta skill, identificar o inferir:

- código HU o requerimiento,
- módulo funcional afectado,
- actor o actores involucrados,
- flujo actual,
- flujo nuevo o modificado,
- pasos que cambian,
- decisiones o reglas funcionales que cambian,
- estados o resultados visibles que cambian,
- documento vivo que debe actualizarse.

---

## 7. Flujo de trabajo

## Paso 1. Entender el cambio funcional
Antes de editar documentos, responder:

- ¿qué hacía el sistema antes?
- ¿qué hará ahora?
- ¿qué actor ejecuta el flujo?
- ¿qué paso cambió?
- ¿hay una nueva variante o excepción?
- ¿hay nuevos estados, decisiones o resultados visibles?

## Paso 2. Identificar el flujo impactado
Determinar si el cambio afecta:

- un flujo completo,
- un subflujo,
- una transición de estado,
- una regla funcional,
- una validación visible para negocio,
- una condición o excepción del proceso.

## Paso 3. Actualizar el documento vivo
Editar:

- `business/current-system-business-flows.md` (externo)

La actualización debe reflejar el nuevo comportamiento vigente del sistema.

## Paso 4. Mantener trazabilidad en la HU
Si existe `HU-XXXX.md`, registrar brevemente:

- qué flujo fue actualizado,
- qué cambió,
- qué parte del comportamiento funcional se modificó.

## Paso 5. Validar consistencia
Antes de cerrar, revisar:

- que el flujo documentado coincida con la funcionalidad real,
- que no se haya dejado el flujo viejo como si siguiera vigente,
- que no se haya duplicado el mismo flujo en varios lugares,
- que la HU conserve solo rastro puntual.

---

## 8. Cuándo actualizar `current-system-business-flows.md`

Actualizar `business/current-system-business-flows.md` (externo) cuando la HU cambie algo como:

- inicio del flujo,
- secuencia principal,
- actores que intervienen,
- decisiones del proceso,
- estados del negocio,
- resultados visibles,
- pasos obligatorios,
- pasos opcionales,
- validaciones funcionales visibles,
- excepciones del flujo,
- rutas alternativas del proceso.

### Señales claras de impacto
- se agregó una nueva acción del usuario,
- cambió el orden de un proceso,
- se agregó una aprobación,
- cambió el criterio funcional para avanzar,
- se agregó una nueva transición de estado,
- cambió el resultado funcional visible del flujo.

### No actualizar si
- el cambio es puramente técnico,
- el cambio no modifica el comportamiento funcional del sistema,
- el cambio solo afecta detalles internos de implementación.

---

## 9. Qué debe contener la actualización del flujo

Cuando actualices el documento vivo, asegúrate de dejar claro:

- nombre del flujo o proceso,
- actor principal,
- objetivo del flujo,
- precondiciones si son necesarias,
- secuencia funcional actual,
- decisiones o variantes relevantes,
- resultado esperado,
- reglas funcionales visibles,
- excepciones importantes si aplican.

### Evitar
- detalles técnicos de implementación,
- nombres de clases, handlers o tablas,
- exceso de detalle interno,
- copiar la historia de usuario completa,
- mezclar análisis técnico con flujo funcional.

---

## 10. Reglas de escritura

El documento de negocio debe quedar:

- claro,
- funcional,
- entendible para negocio y equipo,
- centrado en comportamiento,
- libre de ruido técnico,
- consistente con el sistema actual.

### Preferir
- pasos secuenciales,
- actores claros,
- decisiones explícitas,
- reglas visibles,
- lenguaje funcional.

### Evitar
- detalles de arquitectura,
- detalles de SQL,
- detalles de DTOs o endpoints,
- narración excesiva,
- texto histórico mezclado con el flujo actual.

---

## 11. Relación con `HU-XXXX.md`

Cuando una HU cambia un flujo funcional:

- `current-system-business-flows.md` se actualiza como estado actual,
- `HU-XXXX.md` solo deja trazabilidad puntual.

### En la HU, incluir brevemente
- qué flujo funcional fue impactado,
- si se agregó, ajustó o corrigió,
- qué documento vivo se actualizó.

### No incluir en la HU
- una copia completa del flujo actualizado,
- varias versiones del flujo,
- duplicación del contenido de negocio vigente.

---

## 12. Cuándo también evaluar otros documentos

Además del flujo vivo, evaluar si el cambio funcional también impacta:

### API
Si cambia endpoints o contratos visibles:
- `docs/technical/api/endpoint-reference.md` (local)
- `docs/technical/api/openapi.yaml` (local)

### Seguridad
Si cambia autenticación, autorización, permisos o tenant:
- `analysis/current-state/security-analysis.md` (externo)

### Performance
Si cambia listados, filtros, consultas críticas o alto volumen:
- `analysis/current-state/performance-analysis.md` (externo)

### Testing
Si cambia la estrategia o los criterios de testing:
- `analysis/current-state/testing-analysis.md` (externo)

### Architecture
Si el cambio funcional forzó un ajuste estructural relevante:
- `analysis/current-state/architecture-analysis.md` (externo)

---

## 13. Salida esperada

Cuando termines, debes dejar:

1. `business/current-system-business-flows.md` (externo) actualizado si aplicaba,
2. referencia del cambio en `HU-XXXX.md` si existe,
3. consistencia entre comportamiento funcional real y flujo documentado,
4. ausencia de duplicación o versiones paralelas del flujo.

---

## 14. Checklist final

- [ ] Confirmé que la HU cambió el comportamiento funcional
- [ ] Identifiqué el flujo o subflujo impactado
- [ ] Actualicé el documento vivo correcto
- [ ] No dupliqué el flujo en la HU
- [ ] El flujo actualizado representa el estado actual del sistema
- [ ] Evité detalles técnicos innecesarios
- [ ] El contenido es consistente con `project-foundation.md`
- [ ] La HU conserva solo trazabilidad puntual

---

## 15. Criterio rector

Actualiza el flujo vivo solo cuando el comportamiento funcional del sistema cambie de verdad, mantén la HU como rastro puntual y evita siempre crear versiones paralelas del mismo proceso de negocio.