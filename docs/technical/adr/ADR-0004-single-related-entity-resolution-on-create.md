# ADR-0004 — Resolución única de entidad relacionada en handlers de create

- **Estado:** Aprobado
- **Fecha:** 2026-05-17
- **Autores:** Equipo Backend (asistido por agente)
- **Relacionado con:** Hallazgo P4 🟡 — Doble resolución de classification en `CreatePositionCategoryCommandHandler`
- **Reemplaza:** No aplica
- **Reemplazado por:** No aplica

---

## 1. Título

Al crear una entidad, una relación que solo requiere validar existencia y obtener su FK se
resuelve **una sola vez** (fetch de entidad + null-check); el response se arma post-insert.

---

## 2. Contexto

### Contexto resumido
`CreatePositionCategoryCommandHandler.Handle` resolvía la misma classification dos veces:
`GetClassificationResponseByIdAsync` (DTO, solo para el null-check de existencia, descartado) +
`GetClassificationByIdAsync` (entidad, solo para el FK `Id`, con
`?? throw InvalidOperationException`).

### Situación actual
2 round-trips para una validación+FK; patrón inconsistente con el handler hermano
`CreatePositionCategoryClassificationCommandHandler`, que resuelve cada relación una sola vez vía
`GetActiveCatalogReferenceAsync`/`GetActiveOrgUnitTypeReferenceAsync`. El response final ya se
construye post-insert con `GetCategoryResponseByIdAsync`, por lo que el DTO era redundante.
Riesgo menor de lecturas divergentes bajo concurrencia. No es perf-crítico (path de create).

### Motivadores
- Eliminar el round-trip redundante y la ventana de doble lectura.
- Unificar el patrón de resolución entre handlers hermanos.

---

## 3. Decisión

### Decisión adoptada
Resolver la classification con un único `GetClassificationByIdAsync` (entidad) y validar
existencia por null-check → `ClassificationNotFound`. Se preserva la semántica actual: **no** se
exige classification activa (decisión explícita del usuario). El FK sale de `entity.Id`; el
response se sigue construyendo post-insert con `GetCategoryResponseByIdAsync`.

### Alcance de la decisión
- [x] Un handler específico (`CreatePositionCategoryCommandHandler`) + regla anti-regresión
  transversal a handlers de create.

### Reglas derivadas
- En handlers de create, no duplicar lookups DTO+entidad para una misma validación+FK.
- Cuando solo se requiere existencia + FK, preferir un único fetch de entidad y null-check.
- El response se arma post-insert (lectura proyectada), no a partir del DTO de validación.

---

## 4. Alternativas evaluadas

### Alternativa 1
**Nombre:** No hacer nada

**Descripción:** Mantener la doble resolución.

**Ventajas:** Cero esfuerzo.

**Desventajas:** Persiste el round-trip redundante y la inconsistencia.

**Razón de descarte:** no resuelve el hallazgo.

### Alternativa 2
**Nombre:** Resolución única preservando semántica *(elegida)*

**Descripción:** Un `GetClassificationByIdAsync` + null-check; sin filtro de activo.

**Ventajas:** −1 query; sin cambio de comportamiento; patrón consistente; bajo riesgo.

**Desventajas:** No unifica la semántica de "activo" con el handler hermano.

**Razón de aceptación:** coincide literal con la acción del hallazgo y la decisión del usuario.

### Alternativa 3
**Nombre:** Espejar `GetActive*Reference` (exigir activa)

**Descripción:** Resolver con lookup que filtra `IsActive == true`.

**Ventajas:** Consistencia total con `CreatePositionCategoryClassificationCommandHandler`.

**Desventajas:** **Cambia el comportamiento** (hoy se permite classification inactiva al crear
categoría).

**Razón de descarte:** el usuario decidió preservar la semántica actual; se documenta como
diferencia conocida (§13).

---

## 5. Justificación

### Razones principales
- Elimina trabajo redundante sin alterar el contrato.
- Reduce superficie de error (un solo origen de verdad de la classification).
- Alinea el patrón de resolución entre handlers hermanos.

### Factores considerados
- [x] Simplicidad
- [x] Mantenibilidad
- [x] Rendimiento
- [x] Consistencia / arquitectura

### Resumen de justificación
Refactor de bajo riesgo, sin cambio de comportamiento, que quita un round-trip y una ventana de
carrera menor, y deja registrada la regla para evitar reincidencia.

---

## 6. Consecuencias

### Consecuencias positivas
- −1 round-trip en el create de categoría; cierre de la ventana de doble lectura.
- Patrón de resolución consistente entre handlers.

### Consecuencias negativas o trade-offs
- Ninguna funcional. La divergencia de semántica "activo" entre create-categoría y
  create-classification permanece (documentada, §13).

### Riesgos
- Referencia residual al DTO eliminado → mitigado por `-warnaserror` y suites.

### Impacto técnico
- `CreatePositionCategoryCommandHandler.Handle`: una resolución en vez de dos; se elimina el
  `InvalidOperationException` (null ahora = `ClassificationNotFound`, semánticamente correcto).

### Impacto operativo o documental
- Esta ADR. Sin cambios en `project-foundation.md`/`AGENTS.md`.

---

## 7. Impacto por capa o área

### Domain
No aplica.

### Application
`PositionDescriptionCatalogAdministration.cs` (`CreatePositionCategoryCommandHandler`).

### Infrastructure
No aplica (repositorio sin cambios).

### API
Sin cambios de contrato (mismo error, mismo orden de validaciones).

### Data / SQL
−1 SELECT en el path de create de categoría.

### Security
No aplica.

### Performance
Mejora menor (path de create, no perf-crítico).

### Testing
Test de integración negativo recomendado (classification inexistente → 404).

### Documentation
Esta ADR.

---

## 8. Plan de implementación

### Cambios requeridos
- Reemplazar la doble resolución por una sola (entidad + null-check).
- ADR.
- Test de integración negativo (recomendado).

### Dependencias
- Ninguna.

### Orden sugerido
1. Refactor del handler. 2. ADR. 3. Test negativo. 4. Build + tests.

### Validaciones requeridas
- `dotnet build -warnaserror` limpio; Application.UnitTests e InternalCatalogs integración en verde.

---

## 9. Impacto en documentación

### Documentos a actualizar
- Esta ADR (nueva).

### Observación
Complementa el hardening del catálogo (ADR-0001/0002/0003); no revierte reglas vigentes.

---

## 10. Impacto en historias de usuario o roadmap

### HUs impactadas
- Hallazgo P4 (consistencia/perf del create de categoría).

### Iniciativas impactadas
- Position Description Catalog.

### Requerimientos futuros habilitados
- Ninguno; previene reincidencia del patrón de doble resolución.

---

## 11. Criterios de aceptación de la decisión

### Se considerará aplicada correctamente cuando:
- `CreatePositionCategoryCommandHandler` hace un único lookup de classification.
- Build con `-warnaserror` limpio.
- classification inexistente → `ClassificationNotFound`; happy path de creación intacto.

### Evidencias esperadas
- `GetClassificationResponseByIdAsync` ya no se invoca en ese handler.
- Suites de integración del catálogo en verde + test negativo nuevo.

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

- **Diferencia conocida (preexistente, fuera de alcance de P4):** `CreatePositionCategory` no
  exige que la classification esté **activa**, mientras que
  `CreatePositionCategoryClassificationCommandHandler` sí exige sus relaciones activas
  (`GetActive*Reference`, `RelatedCatalogItemNotFound`). Por decisión explícita del usuario, P4
  **preserva** la semántica actual y no unifica este comportamiento. Queda registrado aquí como
  diferencia conocida para una eventual decisión futura.

---

## 14. Referencias

- Código: `src/CLARIHR.Application/Features/PositionDescriptionCatalogs/PositionDescriptionCatalogAdministration.cs` (`CreatePositionCategoryCommandHandler`)
- Patrón de referencia: `CreatePositionCategoryClassificationCommandHandler` (mismo archivo)
- ADR relacionadas: `ADR-0001-catalog-allowedactions-no-per-item-dependency.md`, `ADR-0002-catalog-free-text-search-min-length-and-scale-assumption.md`, `ADR-0003-patchstate-no-concurrency-token.md`
- Reglas: `AGENTS.md §4.6` (salida documental ordenada)
