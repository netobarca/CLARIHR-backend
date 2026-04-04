---
name: update-api-reference-openapi
description: Usa esta skill cuando una historia de usuario o cambio backend modifica endpoints, contratos request/response, códigos de error, autenticación/autorización, filtros, paginación o comportamiento observable de la API y necesitas actualizar la referencia técnica de endpoints y el contrato OpenAPI sin duplicar documentación.
---

# Update API Reference and OpenAPI

## 1. Propósito

Esta skill existe para mantener alineada la documentación técnica de la API cuando una historia de usuario o requerimiento cambia el comportamiento observable de endpoints del sistema.

Su objetivo es asegurar que, cuando una HU cambie la API:

- se actualice la referencia humana de endpoints,
- se actualice el contrato OpenAPI cuando aplique,
- se evite duplicar documentación,
- y quede trazabilidad clara del cambio para implementación, pruebas e integraciones futuras.

---

## 2. Cuándo usar esta skill

Usar esta skill cuando un cambio backend:

- agrega un endpoint nuevo,
- modifica un endpoint existente,
- cambia request o response,
- cambia códigos de error,
- cambia autenticación o autorización del endpoint,
- cambia filtros, paginación o sorting,
- cambia comportamiento observable del contrato,
- o cambia el shape de los datos expuestos.

### Casos típicos
- “Actualiza endpoint-reference y openapi por esta HU”
- “Se agregó un endpoint nuevo, documenta el contrato”
- “Cambió la respuesta del endpoint, actualiza la referencia”
- “Agrega al OpenAPI estos filtros y códigos de error”
- “La HU cambió paginación y permisos del endpoint”

---

## 3. Cuándo NO usar esta skill

No usar esta skill para:

- implementar el endpoint desde cero,
- revisar arquitectura general de la HU,
- cerrar toda la documentación de la historia,
- documentar cambios puramente internos sin impacto observable en API,
- mantener dos documentos manuales con la misma información.

Si la tarea principal es implementar backend, usar:
- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`

Si la tarea principal es cierre documental integral de la HU, usar:
- `.agents/skills/close-user-story-docs/SKILL.md`

Si la tarea principal es revisión técnica de la HU, usar:
- `.agents/skills/review-dotnet-cqrs-user-story/SKILL.md`

---

## 4. Fuentes de verdad obligatorias

Antes de actualizar la documentación de API, revisar en este orden:

1. `docs/technical/overview/project-foundation.md`
2. `/AGENTS.md`
3. `docs/AGENTS.md`
4. la HU o requerimiento fuente
5. el código real implementado
6. `docs/technical/api/endpoint-reference.md`
7. `docs/technical/api/openapi.yaml` (si existe y aplica)
8. `docs/analysis/changes/HU-XXXX.md` si ya existe

---

## 5. Regla madre

Para la documentación de API debe existir una sola fuente canónica por propósito.

### Regla de decisión
- `openapi.yaml` representa el **contrato técnico estructurado**.
- `endpoint-reference.md` representa la **referencia humana resumida**.
- `HU-XXXX.md` representa el **rastro puntual del cambio**.

### Nunca hacer
- mantener dos documentos manuales distintos con el mismo detalle de endpoints,
- dejar el cambio solo documentado en el archivo de la HU si cambió el contrato,
- actualizar un documento y dejar el otro contradiciéndolo,
- crear documentación paralela “temporal” para la misma API.

---

## 6. Entradas mínimas esperadas

Para usar correctamente esta skill, identificar o inferir:

- código HU o requerimiento,
- módulo afectado,
- endpoint o endpoints impactados,
- método HTTP,
- ruta,
- propósito del endpoint,
- request esperado,
- response esperado,
- códigos de error,
- autenticación/autorización requerida,
- tenant scope si aplica,
- filtros, paginación y sorting si aplican,
- cambios respecto al estado anterior.

---

## 7. Flujo de trabajo

## Paso 1. Entender el cambio observable
Antes de editar docs, identificar exactamente qué cambió en la API:

- endpoint nuevo,
- endpoint modificado,
- request,
- response,
- errores,
- auth,
- filtros,
- paginación,
- comportamiento observable.

## Paso 2. Confirmar la fuente canónica actual
Verificar si el proyecto ya usa:

- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

Si ambos existen, mantenerlos consistentes y con propósitos diferenciados.

## Paso 3. Actualizar OpenAPI si aplica
Actualizar `openapi.yaml` cuando el contrato técnico cambie o cuando sea la fuente estructurada principal.

## Paso 4. Actualizar la referencia humana
Actualizar `endpoint-reference.md` para reflejar de forma clara y resumida:

- endpoint,
- propósito,
- request,
- response,
- auth,
- errores,
- filtros/paginación.

## Paso 5. Reflejar el cambio en la HU
Si existe archivo de cierre de la HU, dejar referencia a que la API fue actualizada:

- `docs/analysis/changes/HU-XXXX.md`

## Paso 6. Validar consistencia
Antes de cerrar, revisar que:

- OpenAPI y endpoint reference no se contradigan,
- no haya endpoints duplicados en la documentación,
- la auth documentada coincida con el código,
- request/response coincidan con el comportamiento real,
- los errores principales estén reflejados.

---

## 8. Qué debe actualizarse según el tipo de cambio

## 8.1 Endpoint nuevo
Actualizar:
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml` si existe
- `docs/analysis/changes/HU-XXXX.md` si aplica

## 8.2 Cambio de request/response
Actualizar:
- `endpoint-reference.md`
- `openapi.yaml`
- archivo HU si aplica

## 8.3 Cambio de errores
Actualizar:
- `endpoint-reference.md`
- `openapi.yaml`
- archivo HU si aplica

## 8.4 Cambio de auth/autorización
Actualizar:
- `endpoint-reference.md`
- `openapi.yaml`
- posiblemente `docs/analysis/current-state/security-analysis.md` si el impacto es real y estructural
- archivo HU

## 8.5 Cambio de filtros/paginación/sorting
Actualizar:
- `endpoint-reference.md`
- `openapi.yaml`
- posiblemente `docs/analysis/current-state/performance-analysis.md` si el impacto es relevante
- archivo HU

---

## 9. Qué debe contener la referencia humana

En `docs/technical/api/endpoint-reference.md`, cada endpoint debe documentarse de forma breve y útil.

### Incluir como mínimo
- método HTTP
- ruta
- propósito funcional
- autenticación / autorización requerida
- tenant scope si aplica
- request principal
- response principal
- errores relevantes
- filtros, paginación y sorting si aplican
- observaciones importantes del comportamiento

### Evitar
- copiar todo el OpenAPI en markdown,
- repetir definiciones gigantes de schemas si ya están en OpenAPI,
- documentar implementación interna,
- dejar comportamiento ambiguo.

---

## 10. Qué debe contener OpenAPI

Cuando actualices `docs/technical/api/openapi.yaml`, asegurar que el contrato refleje correctamente:

- paths
- methods
- tags si el proyecto las usa
- summary / description
- parameters
- requestBody
- responses
- security
- schemas principales
- paginación o filtros si aplican

### Regla
No dejar OpenAPI parcialmente actualizado si el contrato visible cambió.

---

## 11. Reglas de seguridad para documentación de API

Si el endpoint toca datos sensibles, permisos, usuarios, salarios, aprobaciones o tenant isolation, verificar que la documentación refleje explícitamente:

- autenticación requerida,
- autorización requerida,
- visibilidad limitada por tenant si aplica,
- errores de acceso relevantes,
- ausencia de exposición innecesaria de datos.

### Señales de alerta
- endpoint sensible documentado como público,
- falta de auth en docs cuando el código sí la exige,
- request/response que sugieren más acceso del permitido,
- documentación que omite restricciones de tenant.

---

## 12. Reglas de rendimiento para documentación de API

Si el endpoint es de listado, búsqueda o alto volumen, la documentación debe dejar claro:

- paginación,
- filtros,
- sorting si aplica,
- límites relevantes,
- comportamiento esperado de consulta.

### Señales de alerta
- listado sin paginación documentada,
- filtros reales no reflejados,
- respuestas de gran volumen sin aclaraciones.

---

## 13. Reglas de consistencia

Antes de cerrar la actualización de API, validar:

- que el endpoint documentado exista realmente,
- que la ruta coincida exactamente,
- que el método HTTP coincida,
- que request/response coincidan con el código,
- que los errores importantes estén alineados,
- que OpenAPI y endpoint reference no se contradigan,
- que la documentación nueva no duplique una sección ya existente.

---

## 14. Salida esperada

Cuando termines, debes dejar:

1. `docs/technical/api/endpoint-reference.md` actualizado si aplica,
2. `docs/technical/api/openapi.yaml` actualizado si aplica,
3. referencia del cambio en `HU-XXXX.md` si aplica,
4. consistencia entre contrato estructurado y referencia humana,
5. trazabilidad suficiente del cambio de API.

---

## 15. Checklist final

- [ ] Identifiqué qué cambió realmente en la API
- [ ] Revisé el código real implementado
- [ ] Confirmé la fuente canónica de documentación de API
- [ ] Actualicé `endpoint-reference.md` si correspondía
- [ ] Actualicé `openapi.yaml` si correspondía
- [ ] No dupliqué documentación
- [ ] La auth documentada coincide con el código
- [ ] Los errores documentados coinciden con el comportamiento esperado
- [ ] Los filtros y la paginación quedaron claros si aplican
- [ ] El cambio quedó trazable desde la HU

---

## 16. Criterio rector

Actualiza siempre la documentación de API de forma que el contrato técnico y la referencia humana queden alineados con el código real, sin duplicación y con claridad suficiente para desarrollo, pruebas e integraciones.