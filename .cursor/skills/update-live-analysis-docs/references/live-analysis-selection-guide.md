# Guía de selección de análisis vivos por tipo de impacto

## 1. Propósito

Esta guía ayuda a decidir **qué documentos vivos de análisis deben actualizarse** cuando una historia de usuario o requerimiento impacta el estado actual del sistema.

Debe usarse junto con:

- `.agents/skills/update-live-analysis-docs/SKILL.md`
- `.agents/skills/close-user-story-docs/SKILL.md`
- `.agents/skills/review-dotnet-cqrs-user-story/SKILL.md`
- `docs/technical/overview/project-foundation.md`
- `/AGENTS.md`
- `docs/AGENTS.md`

Su objetivo es evitar:

- actualizar documentos equivocados,
- dejar sin actualizar un análisis vivo importante,
- duplicar análisis dentro de la HU,
- y crear ruido documental cuando no hubo cambio real.

---

## 2. Regla principal

Antes de actualizar un documento vivo, responder internamente:

1. ¿Este cambio altera el estado actual del sistema?
2. ¿Ese cambio afecta seguridad, performance o testing?
3. ¿Ese impacto es estructural o solo puntual de la HU?
4. ¿Ya existe un documento vivo oficial para este tema?
5. ¿La HU solo necesita dejar trazabilidad o además cambiar el análisis vigente?

### Regla de decisión
- Si el cambio altera la **situación vigente del sistema**, actualizar el **documento vivo** correspondiente.
- Si el cambio solo necesita dejar **rastro histórico**, documentarlo en `HU-XXXX.md`.
- Si el cambio no altera el análisis vigente, no actualizar por obligación artificial.

---

## 3. Documentos vivos cubiertos por esta guía

Esta guía se enfoca principalmente en:

- `docs/analysis/current-state/security-analysis.md`
- `docs/analysis/current-state/performance-analysis.md`
- `docs/analysis/current-state/testing-analysis.md`

Y, como referencia secundaria, ayuda a detectar si el cambio también puede impactar:

- `docs/analysis/current-state/architecture-analysis.md`
- `docs/business/current-system-business-flows.md`

---

## 4. Cuándo actualizar `security-analysis.md`

Actualizar `docs/analysis/current-state/security-analysis.md` cuando el cambio impacte de forma real alguno de estos puntos:

- autenticación,
- autorización,
- RBAC,
- políticas de acceso,
- permisos por acción,
- permisos por campo,
- tenant isolation,
- ownership,
- auditoría,
- datos sensibles,
- protección de PII,
- exposición de recursos,
- reglas de acceso por rol o alcance.

### Preguntas guía
- ¿el endpoint o flujo ahora requiere autenticación donde antes no?
- ¿cambió un permiso, política o restricción?
- ¿se agregó o cambió una validación de tenant?
- ¿se agregó auditoría a una acción crítica?
- ¿se cambió la visibilidad o exposición de datos sensibles?
- ¿se reforzó un control de acceso importante?

### Ejemplos claros
- una HU ahora exige permiso específico para crear o editar,
- un endpoint administrativo dejó de ser accesible para ciertos roles,
- se añadió control explícito de `TenantId`,
- se introdujo field-level permission,
- se agregó auditoría para cambios sensibles,
- se cambió la forma en que se oculta información de otros tenants.

### No actualizar si
- el cambio fue solo refactor interno sin alterar el comportamiento de seguridad,
- se cambió código interno pero no reglas ni controles vigentes.

---

## 5. Cuándo actualizar `performance-analysis.md`

Actualizar `docs/analysis/current-state/performance-analysis.md` cuando el cambio impacte de forma real alguno de estos puntos:

- consultas,
- paginación,
- filtros,
- sorting,
- proyecciones,
- `AsNoTracking()`,
- índices,
- patrones de acceso a datos,
- caching,
- procesos pesados,
- exportaciones,
- listados de alto volumen,
- request path,
- background processing.

### Preguntas guía
- ¿se agregó un listado nuevo?
- ¿se cambió una query relevante?
- ¿se agregó paginación, filtros o sorting?
- ¿se agregó o modificó un índice?
- ¿se introdujo una optimización o un riesgo nuevo?
- ¿se movió procesamiento fuera del request?
- ¿se modificó un flujo crítico de alto volumen?

### Ejemplos claros
- una HU agrega un endpoint de listado paginado,
- una consulta cambió de cargar entidades completas a proyectar DTOs,
- se agregó índice por tenant y fecha,
- se ajustó un endpoint para usar `AsNoTracking()`,
- se sacó una exportación pesada a procesamiento asíncrono,
- se cambió la estrategia de filtros en una búsqueda sensible.

### No actualizar si
- el cambio no altera el análisis vigente de rendimiento,
- solo se ajustó código menor sin cambiar estrategia ni riesgo.

---

## 6. Cuándo actualizar `testing-analysis.md`

Actualizar `docs/analysis/current-state/testing-analysis.md` cuando el cambio impacte de forma real alguno de estos puntos:

- estrategia de pruebas,
- cobertura mínima esperada,
- convención de naming de tests,
- tipos de escenarios obligatorios,
- enfoque de mocks,
- criterios de calidad,
- alcance esperado por HU,
- lineamientos para tenant, permisos o ErrorCodes,
- behaviors o patrones de testeo del proyecto.

### Preguntas guía
- ¿se definió una nueva regla de testing por HU?
- ¿se cambió el estándar mínimo esperado?
- ¿se formalizó una convención nueva?
- ¿se amplió el tipo de escenarios obligatorios?
- ¿se cambió cómo deben probarse tenant, permisos o Result?

### Ejemplos claros
- se estableció que toda HU sensible debe probar tenant scope,
- se formalizó que toda prueba valide `ErrorCode`,
- se definió una nueva convención de nombres,
- se cambió el enfoque estándar para mocks o builders,
- se amplió el estándar de cobertura mínima en handlers.

### No actualizar si
- solo se agregaron tests normales siguiendo la estrategia ya documentada,
- no cambió ninguna regla vigente de testing.

---

## 7. Cuándo también evaluar `architecture-analysis.md`

Aunque esta skill se centra en seguridad, performance y testing, también debes considerar si el cambio amerita revisar arquitectura cuando:

- cambió la distribución de responsabilidades,
- se introdujo un patrón nuevo,
- cambió la relación entre capas,
- se movió lógica entre Domain, Application, Infrastructure o API,
- se cambió una decisión estructural del backend.

### Señal práctica
Si el cambio no solo “usa” la arquitectura, sino que la **modifica o tensiona**, probablemente también impacta `architecture-analysis.md`.

---

## 8. Cuándo también evaluar `current-system-business-flows.md`

Evaluar actualización de `docs/business/current-system-business-flows.md` cuando:

- cambia el flujo funcional,
- cambia el comportamiento visible del usuario,
- se agrega una nueva capacidad operativa,
- cambia la secuencia del proceso de negocio,
- cambian estados o transiciones funcionales relevantes.

### Regla
Si el sistema ahora se comporta distinto desde la perspectiva del negocio, el flujo vivo debe reflejarlo.

---

## 9. Cuándo dejar el cambio solo en `HU-XXXX.md`

Dejar el cambio solo en el archivo de la HU cuando:

- el cambio fue puntual,
- no altera reglas vivas del sistema,
- no modifica estrategia de seguridad, performance o testing,
- no cambia el análisis vigente,
- solo necesitas trazabilidad del trabajo implementado.

### Ejemplos
- corrección puntual de validación,
- ajuste menor sin impacto estructural,
- refactor pequeño,
- mejora interna sin cambio de postura de seguridad,
- test agregado siguiendo la estrategia ya existente.

---

## 10. Qué no hacer nunca

No hacer nunca lo siguiente:

- copiar todo `security-analysis.md` dentro de la HU,
- copiar todo `performance-analysis.md` dentro de la HU,
- copiar todo `testing-analysis.md` dentro de la HU,
- crear un archivo nuevo de análisis por cada historia,
- actualizar documentos vivos sin cambio real,
- mezclar histórico de HU con estado actual del sistema sin separarlo.

---

## 11. Matriz rápida de decisión

| Tipo de cambio | Security Analysis | Performance Analysis | Testing Analysis | Solo HU |
|---|---|---|---|---|
| Cambio de permisos/autorización | Sí | No usualmente | A veces | No |
| Cambio de tenant isolation | Sí | A veces | A veces | No |
| Cambio de auditoría | Sí | No usualmente | A veces | No |
| Cambio de paginación/filtros/query crítica | No usualmente | Sí | A veces | No |
| Cambio de índice o patrón de acceso | No usualmente | Sí | No usualmente | No |
| Cambio de estrategia de tests | No usualmente | No | Sí | No |
| Solo se agregaron tests normales | No | No | No usualmente | Sí |
| Bug fix puntual sin impacto estructural | No | No | No | Sí |
| Cambio sensible con permisos + alto volumen | Sí | Sí | A veces | No |

---

## 12. Secuencia recomendada

1. Leer la HU o requerimiento.
2. Confirmar en el código qué cambió realmente.
3. Identificar si cambió seguridad.
4. Identificar si cambió performance.
5. Identificar si cambió testing.
6. Evaluar si también cambió arquitectura o negocio.
7. Actualizar solo los documentos vivos realmente impactados.
8. Reflejar en `HU-XXXX.md` qué documentos fueron actualizados.
9. Verificar que no hubo duplicación.

---

## 13. Checklist rápida

- [ ] Confirmé si hubo cambio real en seguridad
- [ ] Confirmé si hubo cambio real en performance
- [ ] Confirmé si hubo cambio real en testing
- [ ] Evalué si el cambio también impacta arquitectura
- [ ] Evalué si el cambio también impacta flujo de negocio
- [ ] Actualicé solo los documentos vivos correctos
- [ ] Dejé la HU como rastro puntual, no como copia del análisis
- [ ] Evité cambios artificiales o de relleno
- [ ] Mantengo consistencia con `project-foundation.md`

---

## 14. Ejemplos rápidos

### Caso A: Nuevo endpoint administrativo con permiso específico
Actualizar:
- `security-analysis.md`
- `HU-XXXX.md`

### Caso B: Nuevo listado paginado con filtros y optimización por índice
Actualizar:
- `performance-analysis.md`
- `HU-XXXX.md`

### Caso C: Nueva regla del proyecto para probar tenant scope en todas las HUs sensibles
Actualizar:
- `testing-analysis.md`
- `HU-XXXX.md`

### Caso D: Bug fix pequeño sin cambio estructural
Actualizar:
- `HU-XXXX.md` solamente

### Caso E: Cambio que agrega permisos y además cambia un query crítico
Actualizar:
- `security-analysis.md`
- `performance-analysis.md`
- `HU-XXXX.md`

---

## 15. Criterio rector

Actualiza únicamente el análisis vivo que represente una regla o situación vigente modificada por la HU; todo lo demás debe quedar como trazabilidad puntual en el archivo de cambio de la historia.