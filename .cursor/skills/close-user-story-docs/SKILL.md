---
name: close-user-story-docs
description: Usa esta skill cuando una historia de usuario o requerimiento ya fue implementado o está por cerrarse y necesitas actualizar la documentación viva impactada, crear o actualizar el archivo de cierre de la HU y mantener el índice de cambios sin duplicar documentación. No usar para implementar código fuente ni para reorganizaciones documentales masivas no relacionadas a una HU específica.
---

# Close User Story Docs

## 1. Propósito

Esta skill existe para cerrar documentalmente una historia de usuario o requerimiento de forma ordenada, consistente y sin generar caos documental.

Su objetivo es asegurar que, al terminar una HU:

- se actualicen los documentos vivos correctos,
- se cree o actualice el archivo de cierre de la HU,
- se actualice el índice maestro de cambios,
- y no se creen documentos duplicados o paralelos.

Esta skill trabaja sobre la estructura documental del proyecto CLARIHR Backend.

---

## 2. Cuándo usar esta skill

Usar esta skill cuando el trabajo principal ya está implementado o suficientemente definido y necesitas dejar la salida documental correcta para una HU o requerimiento.

### Casos típicos
- “Cierra la documentación de esta HU”
- “Actualiza los documentos afectados por esta historia”
- “Genera el archivo HU-XXXX de cierre”
- “Actualiza el hu-index con esta implementación”
- “Documenta qué cambió en arquitectura / seguridad / performance / testing por esta HU”
- “Deja la trazabilidad documental de este requerimiento”

---

## 3. Cuándo NO usar esta skill

No usar esta skill para:

- implementar código fuente de la feature,
- diseñar toda la arquitectura del proyecto desde cero,
- crear documentación general no asociada a una HU concreta,
- hacer migraciones documentales masivas del repositorio completo,
- generar documentación de usuario final,
- crear o modificar ADRs si no existe una decisión técnica duradera.

Si la tarea principal es implementar backend, usa primero la skill o flujo de implementación correspondiente.  
Esta skill entra cuando la HU ya necesita dejar su rastro documental correcto.

---

## 4. Fuentes de verdad obligatorias

Antes de modificar documentación, revisar siempre estas fuentes en este orden:

1. `docs/technical/overview/project-foundation.md`
2. `/AGENTS.md`
3. `docs/AGENTS.md`
4. `docs/templates/hu-closeout-template.md`
5. `docs/analysis/changes/hu-index.md`
6. `docs/templates/adr-template.md` (solo si surge una decisión técnica formal)

Si encuentras contradicciones entre documentos históricos y el foundation document, no replique la contradicción.  
Usa como base el foundation document y deja trazabilidad ordenada.

---

## 5. Reglas no negociables

### 5.1 Fuente canónica única
Para cada tipo de información debe existir una sola fuente oficial.

Antes de crear un archivo nuevo, siempre validar:

1. ¿Ya existe un documento canónico para este contenido?
2. ¿Debo actualizar un documento vivo existente en lugar de crear otro?
3. ¿Esto es estado actual del sistema o solo rastro de cambio de una HU?

### 5.2 No duplicación
Nunca:

- crear carpetas por HU con copias completas de análisis ya existentes,
- duplicar arquitectura, seguridad, performance o testing por cada historia,
- mantener dos documentos manuales con la misma información de API,
- crear documentación “temporal” sin propósito claro.

### 5.3 Documentación viva vs cambio puntual
- Los documentos vivos representan el estado actual del sistema y se actualizan.
- El archivo de la HU resume el impacto puntual de la historia.
- El archivo de la HU no sustituye los documentos vivos.

---

## 6. Entradas mínimas esperadas

Para ejecutar bien esta skill, debes identificar o inferir:

- código de la HU, por ejemplo `HU-0014`,
- título corto de la HU,
- módulo o contexto funcional,
- alcance implementado,
- capas afectadas,
- documentos vivos impactados,
- cambios de API, seguridad, performance, testing o SQL si aplican,
- pruebas agregadas o actualizadas,
- estado actual de la HU.

Si alguno de estos datos no está explícito pero puede inferirse razonablemente del requerimiento y de los cambios, infiérelo con criterio técnico y mantén consistencia.

---

## 7. Flujo de trabajo

## Paso 1. Leer el contexto
Leer el requerimiento o HU y entender:

- qué capacidad funcional se agregó o cambió,
- qué flujo de negocio toca,
- qué capas toca,
- si hubo cambios en API,
- si hubo cambios en SQL o persistencia,
- si hubo impacto en seguridad,
- si hubo impacto en rendimiento,
- si hubo cambios en testing.

## Paso 2. Determinar el impacto documental
Responder internamente:

- ¿Debe actualizarse `business/current-system-business-flows.md`?
- ¿Debe actualizarse `analysis/current-state/architecture-analysis.md`?
- ¿Debe actualizarse `analysis/current-state/security-analysis.md`?
- ¿Debe actualizarse `analysis/current-state/performance-analysis.md`?
- ¿Debe actualizarse `analysis/current-state/testing-analysis.md`?
- ¿Debe actualizarse `technical/api/endpoint-reference.md`?
- ¿Debe actualizarse `technical/api/openapi.yaml`?
- ¿Debe documentarse SQL o data?
- ¿Debe existir ADR?

## Paso 3. Actualizar documentos vivos
Actualizar solamente los documentos vivos realmente impactados.

No hagas cambios artificiales ni “de relleno”.  
Si una HU no cambia arquitectura, no fuerces un update en `architecture-analysis.md`.

## Paso 4. Crear o actualizar el archivo de cierre de la HU
Usar como base:

- `docs/templates/hu-closeout-template.md`

Ubicación esperada:

- `docs/analysis/changes/HU-XXXX.md`

Si el archivo ya existe, actualizarlo.
Si no existe, crearlo siguiendo la plantilla.

## Paso 5. Actualizar el índice maestro
Actualizar:

- `docs/analysis/changes/hu-index.md`

Debes mantener al menos:
- código HU,
- título,
- módulo,
- estado,
- fecha última actualización,
- archivo de cambio,
- documentos vivos actualizados,
- observaciones cortas.

## Paso 6. Evaluar necesidad de ADR
Solo si la HU introduce una decisión técnica estructural o duradera, considerar:

- `docs/decisions/ADR-XXXX.md`

Si no existe decisión arquitectónica duradera, no crear ADR.

## Paso 7. Validar consistencia
Antes de cerrar, revisar:

- que no se haya duplicado documentación,
- que el archivo de la HU no replique documentos vivos completos,
- que el índice apunte al archivo correcto,
- que los documentos vivos reflejen el nuevo estado del sistema,
- que la trazabilidad quede clara.

---

## 8. Estructura objetivo que debes respetar

Trabaja alineado con esta estructura objetivo:

```text
docs/
  business/
    current-system-business-flows.md

  analysis/
    current-state/
      architecture-analysis.md
      security-analysis.md
      performance-analysis.md
      testing-analysis.md
      remediation-plan.md
      validation-checklist.md
    changes/
      hu-index.md
      HU-XXXX.md

  technical/
    overview/
      project-foundation.md
    api/
      endpoint-reference.md
      openapi.yaml
    security/
    performance/
    operations/
    data/

  decisions/
    ADR-XXXX.md

  templates/
    hu-closeout-template.md
    adr-template.md