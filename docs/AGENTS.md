# AGENTS.md — Documentación del proyecto CLARIHR Backend

## 1. Propósito

Este archivo define **cómo debe trabajar Codex cuando modifique, cree, reorganice o mantenga documentación dentro de `docs/`**.

Su objetivo es asegurar que la documentación del proyecto:

- sea clara,
- tenga una sola fuente canónica por tema,
- no se duplique,
- evolucione junto con el sistema,
- y deje trazabilidad ordenada por historia de usuario o requerimiento.

Este archivo aplica específicamente al directorio `docs/` y tiene prioridad sobre reglas más generales cuando la tarea afecte documentación.

---

## 2. Documento rector

Toda la documentación dentro de `docs/` debe alinearse primero con:

- `docs/technical/overview/project-foundation.md`

Ese documento define la base oficial del proyecto en cuanto a:

- arquitectura,
- stack,
- multi-tenant,
- seguridad,
- rendimiento,
- pruebas,
- gobernanza documental,
- y salida requerida por historia de usuario.

Si existe contradicción entre documentos históricos y el foundation document, **no replicar la contradicción**.  
Usar el foundation document como base y documentar la transición cuando sea necesario.

---

## 3. Principio central de documentación

## Regla madre
**Para cada tipo de información debe existir una sola fuente canónica.**

Antes de crear cualquier archivo nuevo, validar:

1. si ya existe un documento vivo que deba actualizarse,
2. si el cambio corresponde solo a un registro por HU,
3. si el contenido sería duplicado respecto a otro documento existente.

### Nunca hacer
- crear un nuevo markdown para repetir información ya documentada,
- dejar dos documentos manuales con el mismo propósito,
- crear árboles paralelos por historia de usuario con análisis completos repetidos,
- mantener documentación “temporal” sin dueño ni propósito claro.

---

## 4. Modelo documental oficial

La documentación del proyecto se divide en dos grupos principales:

## 4.1 Documentación viva
Representa el **estado actual** del sistema.

Se actualiza cuando el sistema cambia.  
No debe duplicarse por historia de usuario.

Ejemplos:
- flujos actuales del negocio,
- análisis actuales de arquitectura,
- análisis actuales de seguridad,
- análisis actuales de performance,
- análisis actuales de testing,
- referencia técnica actual,
- foundation del proyecto.

## 4.2 Registro de cambio por HU o requerimiento
Representa el **impacto puntual** de una historia de usuario.

Debe resumir:
- qué cambió,
- qué documentos vivos se actualizaron,
- impactos técnicos,
- riesgos,
- validaciones ejecutadas,
- pendientes si existen.

No debe convertirse en una copia del estado completo del sistema.

---

## 5. Estructura objetivo de documentación

Tomar como estructura objetivo:

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