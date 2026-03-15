# Guía de selección documental por tipo de cambio

## 1. Propósito

Esta guía ayuda a decidir qué documentos deben actualizarse cuando se cierra documentalmente una historia de usuario o requerimiento.

Debe usarse junto con:

- `.agents/skills/close-user-story-docs/SKILL.md`
- `docs/technical/overview/project-foundation.md`
- `/AGENTS.md`
- `docs/AGENTS.md`

Su objetivo es evitar:

- duplicación documental,
- actualizaciones innecesarias,
- omisiones en la trazabilidad,
- y creación de archivos que no correspondan.

---

## 2. Regla principal

Antes de actualizar o crear un documento, responder:

1. ¿Este cambio altera el estado actual del sistema?
2. ¿Ese estado actual ya tiene un documento vivo?
3. ¿Este cambio solo necesita trazabilidad puntual por HU?
4. ¿Estoy a punto de duplicar información ya existente?

### Regla de decisión
- Si representa el **estado actual del sistema**, actualizar un **documento vivo**.
- Si representa el **impacto puntual de una HU**, actualizar o crear el archivo de la **HU**.
- Si representa una **decisión técnica duradera**, evaluar un **ADR**.

---

## 3. Tipos de cambio y documento a actualizar

## 3.1 Cambio funcional de negocio

### Cuándo aplica
Cuando la HU:
- cambia un flujo del sistema,
- agrega una nueva capacidad funcional,
- modifica pasos del proceso de negocio,
- cambia comportamiento visible para el usuario.

### Documento principal
- `docs/business/current-system-business-flows.md`

### También actualizar
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### No hacer
- crear un flujo nuevo por HU,
- dejar el cambio solo documentado en el archivo de la HU.

---

## 3.2 Cambio de arquitectura

### Cuándo aplica
Cuando la HU:
- introduce un nuevo patrón,
- cambia la distribución de responsabilidades,
- modifica la relación entre capas,
- agrega un componente estructural relevante,
- cambia una decisión importante de diseño.

### Documento principal
- `docs/analysis/current-state/architecture-analysis.md`

### También actualizar
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### Evaluar adicionalmente
- `docs/decisions/ADR-XXXX.md` si hay una decisión duradera

### No hacer
- copiar toda la arquitectura dentro del archivo de la HU,
- crear otro documento arquitectónico paralelo con el mismo propósito.

---

## 3.3 Cambio de seguridad

### Cuándo aplica
Cuando la HU:
- cambia autenticación,
- cambia autorización,
- afecta roles o permisos,
- toca field permissions,
- cambia tenant isolation,
- modifica auditoría,
- toca datos sensibles,
- cambia comportamiento de acceso o exposición.

### Documento principal
- `docs/analysis/current-state/security-analysis.md`

### También actualizar
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### No hacer
- dejar el cambio solo en el código,
- documentar seguridad solo como observación menor si realmente cambió una regla.

---

## 3.4 Cambio de rendimiento

### Cuándo aplica
Cuando la HU:
- agrega listados,
- cambia paginación,
- modifica consultas,
- introduce nuevas proyecciones,
- cambia índices,
- agrega caché,
- mueve procesos a segundo plano,
- impacta rutas críticas o alto volumen.

### Documento principal
- `docs/analysis/current-state/performance-analysis.md`

### También actualizar
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### No hacer
- tocar performance-analysis si no hubo cambio real,
- registrar rendimiento solo en observaciones vagas.

---

## 3.5 Cambio de testing

### Cuándo aplica
Cuando la HU:
- introduce nueva estrategia de test,
- cambia reglas de cobertura,
- agrega una nueva convención de pruebas,
- redefine el alcance mínimo esperado de tests,
- obliga a ajustar el análisis vigente de pruebas.

### Documento principal
- `docs/analysis/current-state/testing-analysis.md`

### También actualizar
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### No hacer
- actualizar este documento si solo se agregaron tests normales sin cambiar estrategia o lineamientos.

---

## 3.6 Cambio de API

### Cuándo aplica
Cuando la HU:
- crea endpoints,
- modifica endpoints,
- cambia request/response,
- cambia filtros,
- cambia paginación,
- cambia códigos de error,
- cambia autenticación/autorización del endpoint,
- cambia comportamiento observable del contrato.

### Documento principal
- `docs/technical/api/endpoint-reference.md`

### También actualizar
- `docs/technical/api/openapi.yaml` si existe y es fuente contractual
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### No hacer
- mantener dos documentos manuales distintos con el mismo detalle de endpoints,
- registrar el cambio de API solo en el archivo de la HU.

---

## 3.7 Cambio de SQL o persistencia

### Cuándo aplica
Cuando la HU:
- crea o modifica tablas,
- crea o modifica columnas,
- cambia relaciones,
- cambia índices,
- cambia constraints,
- cambia scripts SQL,
- introduce migraciones relevantes,
- modifica estrategias tenant-scoped en datos.

### Documento principal
Actualizar el documento o artefacto técnico correspondiente dentro de:
- `docs/technical/data/`
- `docs/technical/sql/`
- o la ruta técnica oficial definida por el proyecto

### También actualizar
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### Evaluar adicionalmente
- `docs/analysis/current-state/performance-analysis.md` si el cambio tiene impacto en rendimiento
- `docs/analysis/current-state/security-analysis.md` si el cambio tiene impacto en exposición o aislamiento

---

## 3.8 Decisión técnica duradera

### Cuándo aplica
Cuando la HU obliga a decidir algo como:
- estrategia de IDs,
- patrón de autorización,
- estrategia multi-tenant,
- política de errores,
- patrón de integración,
- uso de caché,
- diseño de particionamiento,
- convención estructural del sistema.

### Documento principal
- `docs/decisions/ADR-XXXX.md`

### También actualizar
- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

### Puede requerir además
- `docs/technical/overview/project-foundation.md`
- `docs/analysis/current-state/architecture-analysis.md`
- otros documentos vivos impactados

### No hacer
- crear ADR por cambios menores o triviales,
- generar ADR solo porque “se hizo algo técnico”.

---

## 4. Cuándo actualizar solo el archivo de la HU

Actualizar solo:

- `docs/analysis/changes/HU-XXXX.md`
- `docs/analysis/changes/hu-index.md`

cuando el cambio:
- no modifica reglas vivas del sistema,
- no altera arquitectura,
- no cambia seguridad,
- no cambia performance,
- no cambia testing strategy,
- no cambia API de forma relevante,
- y solo requiere trazabilidad del trabajo implementado.

Ejemplos:
- ajuste menor interno,
- corrección puntual de validación,
- refactor limitado sin impacto documental estructural,
- corrección de bug sin cambio de contrato ni de reglas base.

---

## 5. Cuándo NO crear un archivo nuevo

No crear un archivo nuevo cuando:

- ya existe un documento vivo para ese tema,
- el cambio puede registrarse en la HU sin abrir otra rama documental,
- el contenido sería un duplicado,
- el archivo nuevo no tendría mantenimiento claro,
- el objetivo es “guardar contexto” que ya existe en otro documento.

---

## 6. Secuencia recomendada de decisión

Seguir esta secuencia:

1. Identificar el cambio principal de la HU.
2. Determinar si cambia estado actual del sistema o solo deja rastro histórico.
3. Actualizar documentos vivos impactados.
4. Crear o actualizar `HU-XXXX.md`.
5. Actualizar `hu-index.md`.
6. Evaluar si existe una decisión técnica formal que amerite ADR.
7. Verificar que no se haya duplicado información.

---

## 7. Checklist rápida para la skill

Antes de cerrar la documentación de una HU, validar:

- [ ] Identifiqué si hubo cambio funcional
- [ ] Identifiqué si hubo cambio de arquitectura
- [ ] Identifiqué si hubo cambio de seguridad
- [ ] Identifiqué si hubo cambio de rendimiento
- [ ] Identifiqué si hubo cambio de testing strategy
- [ ] Identifiqué si hubo cambio de API
- [ ] Identifiqué si hubo cambio de SQL o persistencia
- [ ] Actualicé solo los documentos que realmente correspondían
- [ ] Actualicé `HU-XXXX.md`
- [ ] Actualicé `hu-index.md`
- [ ] No dupliqué documentación
- [ ] Evalué correctamente si se necesitaba ADR

---

## 8. Criterio rector

Si hay duda sobre dónde documentar algo, aplicar esta regla:

**actualizar primero la fuente viva correcta, luego dejar el rastro puntual en la HU, y nunca crear documentación paralela para el mismo propósito.**