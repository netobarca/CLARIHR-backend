# ADR-0003 — `*PatchState` no porta tokens de concurrencia

- **Estado:** Aprobado
- **Fecha:** 2026-05-17
- **Autores:** Equipo Backend (asistido por agente)
- **Relacionado con:** Hallazgo P3 🟡 — Estado muerto `ConcurrencyToken`/`ConcurrencyTokenTouched` en `*PatchState`
- **Reemplaza:** No aplica
- **Reemplazado por:** No aplica

---

## 1. Título

El estado de patch (`*PatchState`) del Position Description Catalog no transporta el token de
concurrencia; la concurrencia optimista vive exclusivamente en el command/If-Match.

---

## 2. Contexto

### Contexto resumido
Las 3 clases `*PatchState` de `PositionDescriptionCatalogPatchAdministration.cs`
(`PositionDescriptionCatalogItemPatchState`, `PositionCategoryClassificationPatchState`,
`PositionCategoryPatchState`) declaraban `ConcurrencyToken` y `ConcurrencyTokenTouched`.
`ConcurrencyToken` se asignaba en `From(...)` desde el response; `ConcurrencyTokenTouched` nunca se
asignaba. Ninguno se leía jamás.

### Situación actual
Estado muerto inerte, pero en un path sensible (concurrencia). La concurrencia optimista real ya
funciona vía command/If-Match: `command.ConcurrencyToken != entity.ConcurrencyToken` en los 3
handlers, y el applier JSON Patch rechaza explícitamente el path `/concurrencyToken`. El riesgo era
un footgun latente: una edición futura podía re-leer el token del body (`patchState`) en vez del
If-Match y debilitar silenciosamente la concurrencia. El hallazgo señaló que esta deuda se marcó
como "residual menor" pero **no se trackeó como ítem propio**.

### Motivadores
- Eliminar el footgun de concurrencia (estado muerto en path sensible).
- Cerrar formalmente la deuda no trackeada.
- Codificar la regla anti-regresión.

---

## 3. Decisión

### Decisión adoptada
Eliminar `ConcurrencyToken` y `ConcurrencyTokenTouched` de las 3 `*PatchState` y sus asignaciones en
`From(...)`. La concurrencia optimista vive **solo** en `command.ConcurrencyToken` (poblado desde el
header If-Match) verificado contra `entity.ConcurrencyToken`.

### Alcance de la decisión
- [x] Un módulo específico (Position Description Catalog) + regla anti-regresión transversal.

### Reglas derivadas
- Ningún `*PatchState` ni el JSON Patch document puede transportar o leer el token de concurrencia.
- El path `/concurrencyToken` debe permanecer **rechazado** por el applier.
- La concurrencia optimista se valida exclusivamente con el command/If-Match contra la entidad.

---

## 4. Alternativas evaluadas

### Alternativa 1
**Nombre:** Dejar el estado muerto (no hacer nada)

**Descripción:** Mantener los miembros inertes.

**Ventajas:** Cero esfuerzo.

**Desventajas:** Persiste el footgun; deuda sigue sin trackear.

**Razón de descarte:** no resuelve el hallazgo.

### Alternativa 2
**Nombre:** Eliminar el estado muerto + ADR de tracking *(elegida)*

**Descripción:** Borrar miembros + asignaciones; registrar decisión/regla en ADR.

**Ventajas:** Elimina el footgun; sin cambio de comportamiento; deuda cerrada y trazable.

**Desventajas:** Ninguna relevante (cambio inerte).

**Razón de aceptación:** proporcional y definitiva.

### Alternativa 3
**Nombre:** Reforzar con test de guard del path `/concurrencyToken`

**Descripción:** Añadir test que valide el rechazo del path.

**Ventajas:** Blindaje extra.

**Desventajas:** El applier ya lo rechaza; cobertura indirecta existente.

**Razón de aceptación parcial:** opcional; no requerido para cerrar el hallazgo.

---

## 5. Justificación

### Razones principales
- El estado muerto no aporta valor y representa un riesgo en un path de seguridad.
- Eliminarlo es inerte (0 lecturas) y verificable por compilación.
- El ADR cierra el gap exacto que el hallazgo señala (deuda no trackeada).

### Factores considerados
- [x] Simplicidad
- [x] Mantenibilidad
- [x] Seguridad
- [x] Compatibilidad con arquitectura actual

### Resumen de justificación
Remoción de código muerto de bajo riesgo que elimina un footgun de concurrencia y deja registro
formal de la decisión y la regla anti-regresión.

---

## 6. Consecuencias

### Consecuencias positivas
- Footgun eliminado; superficie de concurrencia reducida a command/If-Match.
- Deuda cerrada y trazable.

### Consecuencias negativas o trade-offs
- Ninguna (cambio sin impacto funcional; los miembros no se leían).

### Riesgos
- Romper build por referencia oculta → mitigado por `-warnaserror` y suites.

### Impacto técnico
- 3 `*PatchState` sin los 2 miembros; 3 `From(...)` sin la asignación.

### Impacto operativo o documental
- Esta ADR. Sin cambios en `project-foundation.md`/`AGENTS.md` (no hay sección de concurrencia;
  la regla se registra aquí, consistente con AGENTS.md §4.6).

---

## 7. Impacto por capa o área

### Domain
No aplica.

### Application
`PositionDescriptionCatalogPatchAdministration.cs`: remoción de miembros muertos y asignaciones.

### Infrastructure
No aplica.

### API
Sin cambios de contrato; concurrencia sigue por If-Match.

### Data / SQL
No aplica.

### Security
Positivo: elimina footgun de concurrencia optimista.

### Performance
No aplica.

### Testing
Sin tests nuevos requeridos; regresión vía suites existentes de PATCH/If-Match.

### Documentation
Esta ADR.

---

## 8. Plan de implementación

### Cambios requeridos
- Borrar `ConcurrencyToken`/`ConcurrencyTokenTouched` de las 3 `*PatchState`.
- Borrar `ConcurrencyToken = response.ConcurrencyToken` de los 3 `From(...)`.
- ADR.

### Dependencias
- Ninguna.

### Orden sugerido
1. Remoción de código. 2. ADR. 3. Build + tests.

### Validaciones requeridas
- `dotnet build -warnaserror` limpio; Application.UnitTests y InternalCatalogs integración en verde.

---

## 9. Impacto en documentación

### Documentos a actualizar
- Esta ADR (nueva).

### Observación
Complementa el hardening del catálogo (ADR-0001/0002); no revierte reglas vigentes.

---

## 10. Impacto en historias de usuario o roadmap

### HUs impactadas
- Hallazgo P3 (higiene de seguridad de concurrencia).

### Iniciativas impactadas
- Position Description Catalog.

### Requerimientos futuros habilitados
- Ninguno; previene regresión de concurrencia.

---

## 11. Criterios de aceptación de la decisión

### Se considerará aplicada correctamente cuando:
- Los 3 `*PatchState` no declaran `ConcurrencyToken`/`ConcurrencyTokenTouched`.
- Build con `-warnaserror` limpio.
- PATCH con If-Match válido sigue → 200 + token rotado; mismatch → conflicto.

### Evidencias esperadas
- `grep ConcurrencyTokenTouched` → 0 ocurrencias en la solución.
- `PositionDescriptionCatalogs_PatchEndpoints_ShouldReturnPatchedEntity` en verde.

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

- **Alcance verificado:** tras la remoción, `ConcurrencyTokenTouched` tiene **0 ocurrencias** en
  toda la solución. Los demás `*PatchState` (JobProfiles y sub-recursos) **no** portaban este
  miembro muerto; el patrón estaba confinado al Position Description Catalog. Sin trabajo adicional.
- El applier JSON Patch ya rechaza el path `/concurrencyToken` en los 3 tipos; esa defensa se
  mantiene intacta y es parte de la regla derivada (§3).

---

## 14. Referencias

- Código: `src/CLARIHR.Application/Features/PositionDescriptionCatalogs/PositionDescriptionCatalogPatchAdministration.cs`
- ADR relacionadas: `docs/technical/adr/ADR-0001-catalog-allowedactions-no-per-item-dependency.md`, `docs/technical/adr/ADR-0002-catalog-free-text-search-min-length-and-scale-assumption.md`
- Reglas: `AGENTS.md §4.6` (salida documental ordenada)
