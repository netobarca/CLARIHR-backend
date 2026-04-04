---
name: update-live-analysis-docs
description: Usa esta skill cuando una historia de usuario o requerimiento ya implementado impacta documentos vivos de análisis, especialmente seguridad, performance o testing, y necesitas actualizar esos análisis sin duplicar documentación ni convertir el archivo de la HU en una copia del estado actual del sistema.
---

# Update Live Analysis Docs

## 1. Propósito

Esta skill existe para mantener actualizados los **documentos vivos de análisis** del proyecto cuando una historia de usuario impacta de forma real:

- seguridad,
- performance,
- testing,
- y, si aplica, otros análisis vivos relacionados.

Su objetivo es asegurar que los documentos de análisis representen el **estado actual del sistema**, mientras que la HU conserve solo el **rastro puntual del cambio**.

---

## 2. Cuándo usar esta skill

Usar esta skill cuando una HU o requerimiento:

- cambia controles de seguridad,
- cambia autenticación o autorización,
- cambia tenant isolation,
- cambia auditoría,
- cambia exposición de datos sensibles,
- cambia patrones de consulta,
- cambia paginación, filtros o índices,
- cambia comportamiento de alto volumen o procesos pesados,
- cambia estrategia de testing,
- cambia cobertura mínima esperada,
- cambia convenciones de pruebas,
- o modifica de forma relevante documentos vivos de análisis.

### Casos típicos
- “Actualiza security-analysis por esta HU”
- “Esta historia cambió paginación e índices, actualiza performance-analysis”
- “La HU cambió la estrategia de tests o cobertura, actualiza testing-analysis”
- “Refleja en los documentos vivos el impacto de esta historia”
- “No quiero duplicar análisis en la HU; actualiza los documentos correctos”

---

## 3. Cuándo NO usar esta skill

No usar esta skill para:

- implementar la HU,
- crear unit tests,
- revisar técnicamente toda la historia,
- cerrar toda la documentación de la HU,
- crear ADRs por sí sola,
- actualizar documentos vivos cuando no hubo impacto real.

Si la tarea principal es implementar backend, usar:
- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`

Si la tarea principal es revisar la HU, usar:
- `.agents/skills/review-dotnet-cqrs-user-story/SKILL.md`

Si la tarea principal es cerrar la documentación de la HU, usar:
- `.agents/skills/close-user-story-docs/SKILL.md`

---

## 4. Fuentes de verdad obligatorias

Antes de editar cualquier análisis vivo, revisar en este orden:

1. `docs/technical/overview/project-foundation.md`
2. `/AGENTS.md`
3. `docs/AGENTS.md`
4. la HU o requerimiento fuente
5. el código real implementado
6. `docs/analysis/current-state/security-analysis.md`
7. `docs/analysis/current-state/performance-analysis.md`
8. `docs/analysis/current-state/testing-analysis.md`
9. `docs/analysis/changes/HU-XXXX.md` si ya existe

---

## 5. Regla madre

Los documentos en `docs/analysis/current-state/` representan el **estado actual** del sistema.

### Regla de decisión
- Si la HU cambió una regla o situación vigente del sistema, actualizar el documento vivo correspondiente.
- Si la HU solo deja rastro histórico, documentarlo en `HU-XXXX.md`.
- Si no hubo impacto real en seguridad, performance o testing, no forzar cambios artificiales.

### Nunca hacer
- copiar todo el análisis vivo dentro de `HU-XXXX.md`,
- crear un archivo nuevo de análisis por cada historia,
- duplicar seguridad, performance o testing en múltiples rutas,
- actualizar por “cumplir” sin cambio real.

---

## 6. Entradas mínimas esperadas

Para usar correctamente esta skill, identificar o inferir:

- código HU o requerimiento,
- módulo o flujo afectado,
- qué cambió realmente,
- si el cambio impacta seguridad,
- si impacta performance,
- si impacta testing,
- qué documentos vivos deben actualizarse,
- qué debe quedar solo en la HU.

---

## 7. Flujo de trabajo

## Paso 1. Entender el impacto real
Antes de editar documentos, responder:

- ¿cambió algo en seguridad?
- ¿cambió algo en performance?
- ¿cambió algo en testing?
- ¿el cambio es estructural o solo puntual?
- ¿el estado actual del sistema quedó distinto después de la HU?

## Paso 2. Determinar qué análisis vivo corresponde
Evaluar si debes actualizar:

- `security-analysis.md`
- `performance-analysis.md`
- `testing-analysis.md`

Actualizar solo los realmente impactados.

## Paso 3. Actualizar el documento vivo
Editar el documento vivo correspondiente para reflejar el nuevo estado del sistema.

La actualización debe:
- integrar el cambio en el análisis actual,
- mantener claridad,
- evitar texto duplicado,
- dejar visible la nueva regla o situación vigente.

## Paso 4. Mantener trazabilidad en la HU
Si existe `HU-XXXX.md`, dejar referencia breve a qué documentos vivos se actualizaron.

La HU no debe convertirse en una copia del análisis vivo.

## Paso 5. Validar consistencia
Antes de cerrar, revisar:

- que el cambio en el análisis refleje el código real,
- que no se haya duplicado contenido,
- que el documento vivo siga representando el estado vigente,
- que la HU conserve solo el rastro puntual.

---

## 8. Cuándo actualizar `security-analysis.md`

Actualizar `docs/analysis/current-state/security-analysis.md` cuando la HU cambie algo como:

- autenticación,
- autorización,
- RBAC,
- permisos por acción,
- permisos por campo,
- tenant isolation,
- auditoría,
- protección de datos sensibles,
- exposición de PII,
- errores de acceso relevantes,
- políticas de acceso,
- validaciones de ownership o alcance.

### Señales claras de impacto
- un endpoint ahora requiere permiso nuevo,
- se agregó o cambió una política de autorización,
- se reforzó o cambió tenant scope,
- se agregó auditoría a una acción crítica,
- se modificó cómo se protege información sensible.

### No actualizar si
- el cambio fue puramente interno y no alteró la postura de seguridad del sistema.

---

## 9. Cuándo actualizar `performance-analysis.md`

Actualizar `docs/analysis/current-state/performance-analysis.md` cuando la HU cambie algo como:

- paginación,
- filtros,
- sorting,
- patrones de consulta,
- proyecciones a DTO,
- uso de `AsNoTracking()`,
- índices,
- caching,
- procesos pesados,
- request path,
- rutas críticas de alto volumen,
- lecturas o escrituras de alto impacto.

### Señales claras de impacto
- se agregó un listado,
- se cambió una query importante,
- se optimizó o degradó un patrón de acceso,
- se requirió un índice nuevo,
- se movió un proceso pesado fuera del request,
- se modificó un endpoint crítico de alto tráfico.

### No actualizar si
- el cambio no altera la estrategia ni el análisis vigente de rendimiento.

---

## 10. Cuándo actualizar `testing-analysis.md`

Actualizar `docs/analysis/current-state/testing-analysis.md` cuando la HU cambie algo como:

- estrategia de unit testing,
- cobertura mínima esperada,
- convenciones de nombres de tests,
- tipos de escenarios obligatorios,
- criterios de calidad de pruebas,
- enfoque de mocks,
- reglas de validación de pruebas,
- alcance oficial esperado de testing en el proyecto.

### Señales claras de impacto
- se definió nueva regla de pruebas por HU,
- se agregó una convención importante,
- se amplió el alcance mínimo esperado de tests,
- se formalizó cómo probar tenant, permisos o errores.

### No actualizar si
- solo se agregaron tests normales siguiendo la estrategia ya vigente.

---

## 11. Qué debe contener cada actualización

Cuando actualices un documento vivo de análisis, asegúrate de dejar claro:

- qué parte del sistema cambió,
- cuál es la regla o situación vigente ahora,
- qué riesgo o consideración importante existe,
- cómo queda el sistema después del cambio,
- y, cuando aplique, qué relación tiene con módulos o flujos sensibles.

### Evitar
- textos vagos,
- listas genéricas sin relación con el cambio,
- duplicar lo que ya dice la HU,
- dejar párrafos enteros repetidos de otros documentos.

---

## 12. Reglas de escritura

La actualización del análisis debe ser:

- clara,
- técnica,
- concreta,
- orientada al estado actual,
- sin relleno,
- sin copiar texto innecesario,
- consistente con `project-foundation.md`.

### Preferir
- secciones precisas,
- lenguaje directo,
- cambios integrados al análisis existente,
- referencias breves al impacto real.

### Evitar
- tono narrativo excesivo,
- repetir la historia completa,
- mezclar histórico y estado actual sin separación.

---

## 13. Relación con `HU-XXXX.md`

Cuando una HU impacta documentos vivos:

- el documento vivo se actualiza como estado actual,
- `HU-XXXX.md` solo debe dejar trazabilidad del cambio.

### En la HU, incluir de forma breve
- qué análisis vivos fueron actualizados,
- por qué fueron actualizados,
- observaciones puntuales del impacto.

### No incluir en la HU
- copia completa de `security-analysis.md`,
- copia completa de `performance-analysis.md`,
- copia completa de `testing-analysis.md`.

---

## 14. Salida esperada

Cuando termines, debes dejar:

1. `security-analysis.md` actualizado si aplicaba,
2. `performance-analysis.md` actualizado si aplicaba,
3. `testing-analysis.md` actualizado si aplicaba,
4. referencia del cambio en `HU-XXXX.md` si existe,
5. consistencia entre código, análisis vivo y trazabilidad de HU.

---

## 15. Checklist final

- [ ] Identifiqué si hubo impacto real en seguridad
- [ ] Identifiqué si hubo impacto real en performance
- [ ] Identifiqué si hubo impacto real en testing
- [ ] Actualicé solo los documentos vivos que realmente correspondían
- [ ] No dupliqué análisis en la HU
- [ ] El análisis vivo refleja el estado actual del sistema
- [ ] La HU mantiene solo el rastro puntual
- [ ] El contenido es consistente con `project-foundation.md`
- [ ] No hice cambios artificiales ni de relleno

---

## 16. Criterio rector

Actualiza los análisis vivos solo cuando el estado actual del sistema cambie de verdad, mantén la HU como trazabilidad puntual y evita siempre duplicar seguridad, performance o testing en múltiples lugares.