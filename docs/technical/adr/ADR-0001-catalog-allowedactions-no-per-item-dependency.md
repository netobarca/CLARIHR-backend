# ADR-0001 — `AllowedActions` en listados de catálogo sin dependencia por ítem

- **Estado:** Aprobado
- **Fecha:** 2026-05-17
- **Autores:** Equipo Backend (asistido por agente)
- **Relacionado con:** Hallazgo P1 🟠 — N+1 en `includeAllowedActions=true` (Position Description Catalog)
- **Reemplaza:** No aplica
- **Reemplazado por:** No aplica

---

## 1. Título

`AllowedActions` en listados de catálogo se deriva solo del permiso (`canManage`); el estado de
dependencia (`hasDependents`) no se calcula por ítem en listados.

---

## 2. Contexto

### Contexto resumido
Con `includeAllowedActions=true`, los 3 handlers de búsqueda del Position Description Catalog
(items, classifications, categories) recorrían la página (hasta 100 ítems) y, por cada ítem,
ejecutaban `GetXByIdAsync` + un `Has*UsingY` (`AnyAsync`): ~200 round-trips secuenciales por
request, ejecutados incluso en cache-hit (el enriquecimiento es post-caché y el payload cacheado
no incluye `AllowedActions`).

### Situación actual
Patrón N+1 documentado en path caliente. Viola `project-foundation.md §12` y `AGENTS.md §4.5`
("Evitar N+1"). El tope `pageSize=100` acota pero no elimina el riesgo de tail-latency y presión
sobre el pool de conexiones bajo concurrencia.

### Motivadores
- Eliminar el N+1 en su origen.
- Alinear el contrato del listado con el precedente de Job Profiles.
- Mantener intacta la enforcement de borrado/inactivación server-side.

---

## 3. Decisión

### Decisión adoptada
Eliminar por completo el cálculo de dependencias por ítem en los listados de catálogo. En listados,
`AllowedActions` se deriva únicamente de `canManage` (paridad exacta con `JobProfilePolicyAdapter`).
La proyección SQL `EXISTS` queda como patrón canónico para superficies de **detalle** que requieran
`hasDependents`; no se aplica al listado porque el listado deja de necesitarlo.

### Alcance de la decisión
- [x] Un módulo específico (Position Description Catalog) + regla transversal de listados.

### Reglas derivadas
- Listados/búsquedas no calculan dependencias por ítem (sin N+1, ni siquiera post-caché).
- `hasDependents` en detalle: proyección SQL `EXISTS` en la query de lectura.
- El bloqueo real de inactivación/borrado se enforcea server-side en command/PATCH handlers.

---

## 4. Alternativas evaluadas

### Alternativa 1
**Nombre:** Query batch post-caché (`WHERE id IN (@ids)` + `EXISTS`)

**Descripción:** Mantener el listado igual y reemplazar el `foreach` por 1 query batch por página.

**Ventajas:**
- Reduce ~200 round-trips a 1.
- No cambia el contrato del listado.

**Desventajas:**
- Sigue ejecutándose en cada request aun con cache-hit.
- Mantiene complejidad de dependencias en el path de listado.

**Razón de descarte:** mejora real pero conserva trabajo evitable en cache-hit y diverge del
precedente de Job Profiles.

### Alternativa 2
**Nombre:** Proyectar `hasDependents` en el SQL del search (cacheable)

**Descripción:** Columna calculada `EXISTS` en la proyección del search, cacheada con la página.

**Ventajas:**
- Cero queries extra en cache-hit.

**Desventajas:**
- Mantiene gating por dependencia en el listado, diverge de Job Profiles.
- Aumenta el coste del search base (join/EXISTS) para todas las peticiones.

**Razón de descarte:** adoptada como patrón canónico para **detalle**, no para el listado.

### Alternativa 3
**Nombre:** Alinear con Job Profiles — quitar dependencia del listado *(elegida)*

**Descripción:** El listado no calcula dependencias; `AllowedActions` solo refleja `canManage`.

**Ventajas:**
- Elimina el N+1 en su origen; cero coste extra.
- Paridad con el precedente existente; menor superficie de mantenimiento.

**Desventajas:**
- `canInactivate` en el listado deja de reflejar dependencias (hint advisory).

**Razón de aceptación:** decisión confirmada por el equipo; enforcement real intacta server-side.

---

## 5. Justificación

### Razones principales
- Coste cero y máxima simplicidad: el N+1 desaparece sin añadir queries.
- Consistencia con `JobProfilePolicyAdapter` (mismo shape de adapter de listado).
- Sin regresión funcional: la enforcement no dependía del flag del listado.

### Factores considerados
- [x] Simplicidad
- [x] Mantenibilidad
- [x] Rendimiento
- [x] Escalabilidad
- [x] Compatibilidad con arquitectura actual
- [x] Multi-tenant

### Resumen de justificación
La validación de "en uso" ya se ejecuta server-side en los PATCH handlers
(`PositionDescriptionCatalogPatchAdministration.cs:173-177, 381-385, 553-557`), independiente del
flag `includeAllowedActions`. El `hasDependencies` del listado era solo un hint advisory; removerlo
elimina el N+1 sin debilitar la seguridad.

---

## 6. Consecuencias

### Consecuencias positivas
- Eliminación total del N+1 en los 3 listados de catálogo.
- Path de listado más simple y barato; payload cacheado sigue siendo correcto.

### Consecuencias negativas o trade-offs
- `AllowedActions.canInactivate` en el listado pasa a `true` para ítems en uso (antes `false`).
  El control de UI basado en ese hint se verá habilitado; el PATCH responde 409.

### Riesgos
- Consumidores de UI que asumían el gating por fila deben confiar en la respuesta del PATCH.

### Impacto técnico
- Se eliminan `HasSimpleDependenciesAsync` / `HasClassificationDependenciesAsync` /
  `HasCategoryDependenciesAsync` (código muerto) y los overloads de 4 args de `ApplyAllowedActions`.

### Impacto operativo o documental
- Nueva regla transversal en `project-foundation.md §12.7` y referencia en `AGENTS.md §4.5`.

---

## 7. Impacto por capa o área

### Domain
No aplica.

### Application
`PositionDescriptionCatalogAdministration.cs`: 3 handlers de búsqueda refactorizados;
`PositionDescriptionCatalogPolicyAdapter` simplificado.

### Infrastructure
No aplica (interfaz y repositorio sin cambios).

### API
Contrato estable: `AllowedActions` sigue presente; cambia el valor semántico de `canInactivate`
en listados.

### Data / SQL
No aplica (se eliminan queries por ítem).

### Security
Sin cambios en enforcement; bloqueo server-side intacto.

### Performance
Eliminación del N+1; hasta ~2×pageSize queries menos por request.

### Testing
Guardia de regresión: `Policy_IncludeAllowedActions_ShouldReturnActionsInCoreLists`.

### Documentation
`project-foundation.md §12.7`, `AGENTS.md §4.5`, esta ADR.

---

## 8. Plan de implementación

### Cambios requeridos
- Refactor de los 3 handlers de búsqueda (early-return + proyección `canManage`).
- Limpieza de `PositionDescriptionCatalogPolicyAdapter` (overloads 3 args, helpers muertos).
- Documentación (§12.7, §4.5, ADR).

### Dependencias
- Ninguna externa.

### Orden sugerido
1. Refactor handlers.
2. Limpieza adapter.
3. Documentación.
4. Build + tests.

### Validaciones requeridas
- `dotnet build -warnaserror` sin warnings de símbolo no usado.
- Tests unitarios e integración (ver §11).

---

## 9. Impacto en documentación

### Documentos a actualizar
- `docs/technical/overview/project-foundation.md` (§12.7 añadida)
- `AGENTS.md` (§4.5, bullet de referencia)

### Observación
La decisión complementa las reglas vigentes de rendimiento; no revierte ninguna.

---

## 10. Impacto en historias de usuario o roadmap

### HUs impactadas
- Hallazgo P1 (remediación de rendimiento del catálogo).

### Iniciativas impactadas
- Position Description Catalog.

### Requerimientos futuros habilitados
- Patrón canónico `EXISTS` para `hasDependents` en superficies de detalle.

---

## 11. Criterios de aceptación de la decisión

### Se considerará aplicada correctamente cuando:
- No exista cálculo de dependencia por ítem en los listados de catálogo.
- `Policy_IncludeAllowedActions_ShouldReturnActionsInCoreLists` pase (campo poblado).
- Los PATCH de inactivación de ítems en uso sigan devolviendo 409.

### Evidencias esperadas
- Build con `-warnaserror` limpio.
- Suite de integración del catálogo en verde.

---

## 12. Estado de seguimiento

### Estado actual
Adoptada

### Próxima revisión
No aplica

### Responsable de seguimiento
Equipo Backend

---

## 13. Notas adicionales

Comunicar a los equipos de frontend el cambio semántico de `canInactivate` en listados: el control
puede aparecer habilitado; la fuente de verdad del bloqueo es la respuesta del PATCH (409).

---

## 14. Referencias

- Foundation document: `docs/technical/overview/project-foundation.md` (§12.7)
- Reglas: `AGENTS.md` (§4.5)
- Código: `src/CLARIHR.Application/Features/PositionDescriptionCatalogs/PositionDescriptionCatalogAdministration.cs`
- Enforcement: `src/CLARIHR.Application/Features/PositionDescriptionCatalogs/PositionDescriptionCatalogPatchAdministration.cs`
- Precedente: `src/CLARIHR.Application/Features/JobProfiles/JobProfileAdministration.cs`
