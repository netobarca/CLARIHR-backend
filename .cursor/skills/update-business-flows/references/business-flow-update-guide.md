# Guía de actualización de flujos de negocio vivos

## 1. Propósito

Esta guía ayuda a decidir **cuándo y cómo actualizar** el documento vivo de flujos de negocio cuando una historia de usuario o requerimiento cambia el comportamiento funcional del sistema.

Debe usarse junto con:

- `.agents/skills/update-business-flows/SKILL.md`
- `.agents/skills/close-user-story-docs/SKILL.md`
- `.agents/skills/review-dotnet-cqrs-user-story/SKILL.md`
- `docs/technical/overview/project-foundation.md`
- `/AGENTS.md`
- `docs/AGENTS.md`

Su objetivo es evitar:

- dejar desactualizado el flujo funcional vigente,
- duplicar el flujo dentro del archivo de la HU,
- crear flujos paralelos por historia,
- mezclar negocio con implementación técnica,
- y perder trazabilidad de qué cambió realmente.

---

## 2. Regla principal

Antes de actualizar el flujo vivo, responder internamente:

1. ¿La HU cambió el comportamiento funcional del sistema?
2. ¿Ese cambio afecta un flujo completo, un subflujo o una regla visible del negocio?
3. ¿Ese cambio modifica el estado actual del sistema o solo deja rastro histórico?
4. ¿El documento vivo actual ya cubre este flujo?
5. ¿Estoy por duplicar en la HU algo que debe vivir solo en el documento de flujos?

### Regla de decisión
- Si el cambio modifica el **comportamiento funcional vigente**, actualizar el **documento vivo**.
- Si el cambio solo necesita **trazabilidad histórica**, documentarlo en `HU-XXXX.md`.
- Si el cambio es puramente técnico y no funcional, no actualizar business flows.

---

## 3. Documento vivo cubierto por esta guía

Documento principal:

- `docs/business/current-system-business-flows.md`

Documento secundario de trazabilidad:

- `docs/analysis/changes/HU-XXXX.md`

### Regla
El archivo de negocio representa **cómo funciona hoy el sistema**.  
El archivo de la HU representa **qué cambió en esa historia**.

---

## 4. Cuándo actualizar el flujo vivo

Actualizar `docs/business/current-system-business-flows.md` cuando la HU cambie cualquiera de estos puntos:

- actor principal del proceso,
- inicio del flujo,
- secuencia de pasos,
- decisiones del proceso,
- transiciones de estado,
- validaciones visibles para negocio,
- resultados visibles,
- condiciones para continuar o bloquearse,
- aprobaciones o rechazos,
- excepciones o variantes funcionales,
- nuevos subflujos,
- reglas operativas del proceso.

### Ejemplos claros
- se agregó una aprobación antes de completar la acción,
- se cambió el orden de los pasos del proceso,
- se agregó una nueva acción que el usuario puede ejecutar,
- cambió el criterio para pasar de un estado a otro,
- ahora existe una ruta alterna del flujo,
- se agregó una validación funcional visible al usuario o al negocio.

---

## 5. Cuándo NO actualizar el flujo vivo

No actualizar `current-system-business-flows.md` cuando:

- el cambio es solo técnico,
- el cambio es solo de arquitectura,
- el cambio es solo de performance,
- el cambio es solo de seguridad interna sin impacto funcional visible,
- el cambio es una refactorización,
- el cambio es una validación puramente técnica,
- el cambio no altera cómo funciona el proceso desde negocio.

### Ejemplos
- cambio de nombre de clase,
- mejora interna de query,
- ajuste de repositorio,
- cambio de DTO sin alterar comportamiento,
- mejora de logging,
- optimización de índice sin cambio en el proceso funcional.

---

## 6. Qué tipo de cambio funcional suele requerir actualización

## 6.1 Nuevo flujo
Cuando se introduce una capacidad nueva.

### Ejemplos
- crear organización,
- aprobar solicitud,
- reasignar responsable,
- anular operación,
- generar acción que antes no existía.

## 6.2 Cambio de flujo existente
Cuando el sistema ya hacía algo, pero ahora lo hace distinto.

### Ejemplos
- cambia el orden de pasos,
- cambia un estado intermedio,
- cambia una validación funcional,
- cambia el resultado del proceso.

## 6.3 Subflujo o excepción
Cuando se agrega o cambia una ruta alterna.

### Ejemplos
- qué pasa si el recurso no cumple condición,
- qué pasa si requiere aprobación,
- qué pasa si el usuario no puede continuar,
- qué pasa en cancelación o rechazo.

---

## 7. Preguntas guía antes de editar

Hazte estas preguntas:

- ¿Qué hacía el usuario antes?
- ¿Qué hará ahora?
- ¿Qué parte del proceso cambió realmente?
- ¿Cambió la secuencia?
- ¿Cambió una decisión funcional?
- ¿Cambió el estado final o los estados intermedios?
- ¿Hay una nueva variante del flujo?
- ¿Hay una nueva validación visible para negocio?
- ¿Se agregó un actor nuevo al proceso?
- ¿Este cambio debe quedar visible para alguien no técnico?

Si la mayoría responde “sí”, probablemente debes actualizar el flujo vivo.

---

## 8. Qué debe contener un flujo bien actualizado

Cuando actualices el documento vivo, asegúrate de dejar claro:

- nombre del flujo,
- objetivo del flujo,
- actor principal,
- precondiciones si aplican,
- pasos principales,
- decisiones o bifurcaciones,
- excepciones relevantes,
- resultado esperado,
- estados involucrados si son importantes para negocio,
- reglas visibles del proceso.

### Regla
El flujo debe explicar **cómo funciona el proceso**, no cómo está programado.

---

## 9. Qué no debe contener el flujo vivo

No incluir:

- nombres de clases,
- handlers,
- commands,
- queries,
- DTOs,
- tablas,
- columnas,
- índices,
- detalles de API,
- detalles de infraestructura,
- términos demasiado técnicos que no aportan al flujo de negocio.

### Regla
Si el dato solo le sirve al developer para implementar, probablemente no pertenece al flujo de negocio.

---

## 10. Estilo recomendado para documentar flujos

Preferir:

- pasos numerados,
- actores claros,
- decisiones visibles,
- lenguaje funcional,
- secuencias fáciles de seguir,
- excepciones separadas de la ruta principal.

### Evitar
- párrafos largos ambiguos,
- mezclar varias historias en una misma explicación sin orden,
- detalle técnico excesivo,
- descripciones históricas largas,
- copiar el texto completo de la HU.

---

## 11. Relación con `HU-XXXX.md`

Cuando una HU cambia un flujo funcional:

### En `current-system-business-flows.md`
Debe quedar:
- el flujo vigente,
- ya integrado como parte del sistema actual.

### En `HU-XXXX.md`
Debe quedar:
- qué flujo fue afectado,
- qué cambió de forma breve,
- qué documento vivo fue actualizado.

### No hacer en `HU-XXXX.md`
- copiar todo el flujo actualizado,
- mantener la versión vieja y la nueva completas,
- usar la HU como “nuevo hogar” del proceso funcional.

---

## 12. Cómo decidir si reescribir o solo ajustar una sección

## Ajustar una sección cuando:
- cambió solo un paso,
- cambió una validación funcional,
- cambió una transición puntual,
- cambió una excepción pequeña.

## Reescribir el flujo cuando:
- cambió la secuencia central,
- cambió el actor o el propósito,
- cambió la lógica del proceso completo,
- se volvió más claro reestructurarlo que parcharlo.

### Regla
Prioriza claridad del documento final sobre conservar redacciones antiguas confusas.

---

## 13. Cuándo también actualizar otros documentos

Además del flujo vivo, evaluar actualización de otros documentos si el cambio también impacta:

### Seguridad
Actualizar:
- `docs/analysis/current-state/security-analysis.md`

Cuando:
- cambian permisos,
- cambia tenant scope,
- cambia autenticación/autorización,
- cambia auditoría funcional relevante.

### Performance
Actualizar:
- `docs/analysis/current-state/performance-analysis.md`

Cuando:
- el flujo implica listados grandes,
- cambia paginación,
- cambia un patrón crítico de consulta.

### API
Actualizar:
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

Cuando:
- cambian endpoints o contratos visibles.

### Architecture
Actualizar:
- `docs/analysis/current-state/architecture-analysis.md`

Cuando:
- el cambio funcional vino acompañado de un cambio estructural relevante.

---

## 14. Matriz rápida de decisión

| Tipo de cambio | Actualizar business flows | Solo HU |
|---|---|---|
| Nueva acción funcional visible | Sí | No |
| Cambio en secuencia del proceso | Sí | No |
| Nueva aprobación o rechazo | Sí | No |
| Nueva transición de estado | Sí | No |
| Nueva excepción funcional | Sí | No |
| Refactor técnico interno | No | Sí |
| Mejora de performance sin cambio funcional | No | Sí |
| Cambio de seguridad interna sin impacto funcional visible | No usualmente | Sí |
| Bug fix pequeño sin alterar proceso | No usualmente | Sí |

---

## 15. Secuencia recomendada

1. Leer la HU o requerimiento.
2. Confirmar qué cambió funcionalmente.
3. Comparar con el flujo vivo actual.
4. Identificar si cambió ruta principal, subflujo, estado o regla visible.
5. Actualizar `current-system-business-flows.md`.
6. Reflejar en `HU-XXXX.md` qué parte del flujo se actualizó.
7. Verificar que no se haya duplicado el flujo.
8. Revisar si también deben tocarse otros documentos vivos.

---

## 16. Checklist rápida

- [ ] Confirmé que hubo cambio funcional real
- [ ] Identifiqué el flujo o subflujo impactado
- [ ] Diferencié estado actual vs rastro histórico
- [ ] Actualicé el documento vivo correcto
- [ ] No mezclé negocio con implementación técnica
- [ ] No copié el flujo completo dentro de la HU
- [ ] El flujo actualizado representa cómo funciona hoy el sistema
- [ ] Revisé si además debía actualizar seguridad, performance o API

---

## 17. Ejemplos rápidos

### Caso A: Crear organización
Actualizar:
- `docs/business/current-system-business-flows.md`
- `HU-XXXX.md`

### Caso B: Ahora una organización requiere aprobación antes de quedar activa
Actualizar:
- `docs/business/current-system-business-flows.md`
- `HU-XXXX.md`
- posiblemente `security-analysis.md` si cambian permisos de aprobación

### Caso C: Se optimizó una query sin cambiar el proceso
Actualizar:
- `HU-XXXX.md` solamente
- posiblemente `performance-analysis.md`
- no business flows

### Caso D: Se agregó una ruta alterna cuando faltan datos obligatorios visibles
Actualizar:
- `docs/business/current-system-business-flows.md`
- `HU-XXXX.md`

---

## 18. Criterio rector

Actualiza el flujo vivo cuando cambie de verdad cómo funciona el sistema desde negocio; deja la HU como trazabilidad puntual y evita siempre duplicar o fragmentar el proceso funcional en múltiples archivos.