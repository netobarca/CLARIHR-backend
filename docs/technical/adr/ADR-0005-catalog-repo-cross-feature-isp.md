# ADR-0005 — Segregación de interfaz del repo de catálogos (consumo cross-feature)

- **Estado:** Aprobado
- **Fecha:** 2026-05-17
- **Autores:** Equipo Backend (asistido por agente)
- **Relacionado con:** Hallazgo P5 🟢 — PositionSlots acopla al repo/DTOs del feature de catálogos (ISP smell)
- **Reemplaza:** No aplica
- **Reemplazado por:** No aplica

---

## 1. Título

`IPositionDescriptionCatalogRepository` mezclaba lecturas ajenas al subdominio de catálogos: se
elimina el código muerto y se trackea como deuda el consumo cross-feature vivo.

---

## 2. Contexto

### Contexto resumido
La interfaz del repo de catálogos exponía 5 miembros señalados por el hallazgo. La exploración
**corrige el framing**: PositionSlots **no consume ninguno** (el método "slot" nombrado estaba
muerto).

### Situación actual (verificada)
- **Código muerto (0 callers en `src/` y `tests/`):** `GetPositionSlotContractTypeLookupAsync`
  + el DTO `PositionSlotContractTypeLookup`; `HasClassificationsUsingOrgUnitTypeAsync` (firma de
  id interno `long`, sin tenant, pero inerte por falta de callers); `ResolveSalaryClassCatalogIdByCodeAsync`.
- **Vivos cross-feature (NO Slots):** `ResolvePositionCategoryIdAsync` ← JobProfiles
  (`JobProfileAdministration.cs:2970`); `ResolveSalaryClassCodeByCatalogIdAsync` ← SalaryTabulator
  (`SalaryTabulatorAdministration.cs:419,511,1599`, `SalaryTabulatorExportHandler.cs:27`).
- Tenant-scoping de los vivos: limpio (`TenantId == tenantId`).

### Motivadores
- Eliminar código muerto y un DTO de Slots mal ubicado en el feature de catálogos.
- Acotar y trackear la deuda estructural ISP real (consumo cross-feature vivo).

---

## 3. Decisión

### Decisión adoptada
(a) **Eliminado el código muerto**: 3 miembros de `IPositionDescriptionCatalogRepository`
(`HasClassificationsUsingOrgUnitTypeAsync`, `GetPositionSlotContractTypeLookupAsync`,
`ResolveSalaryClassCatalogIdByCodeAsync`), sus implementaciones en el repo y el record
`PositionSlotContractTypeLookup`; actualizado el test double.
(b) Los 2 miembros vivos cross-feature **se conservan** y quedan como **deuda estructural
trackeada** (severidad 🟢, sin urgencia), sin refactor ahora.

### Alcance de la decisión
- [x] Un módulo específico (abstracción/impl del repo de catálogos) + guideline transversal.

### Reglas derivadas
- Un feature **no** debe añadir a la interfaz de repositorio de otro subdominio miembros propios.
- El consumo cross-feature de lecturas debe hacerse vía una abstracción **segregada y enfocada**
  (p.ej. `ISalaryClassCatalogLookup`, `IPositionCategoryCatalogLookup`) implementada por el repo
  dueño del dato.
- No introducir DTOs de un feature dentro de otro feature.

---

## 4. Alternativas evaluadas

### Alternativa 1
**Nombre:** Solo documentar (no tocar código)

**Ventajas:** Cero riesgo.

**Desventajas:** Mantiene código muerto y el DTO mal ubicado.

**Razón de descarte:** desaprovecha una limpieza barata y clara.

### Alternativa 2
**Nombre:** Eliminar muertos + trackear vivos como deuda *(elegida)*

**Ventajas:** Quita la mayor parte del smell con riesgo mínimo; deuda acotada y trazable.

**Desventajas:** El ISP de los 2 vivos no se resuelve aún.

**Razón de aceptación:** proporcional a severidad 🟢; coincide con la acción del hallazgo.

### Alternativa 3
**Nombre:** Segregar interfaces ahora (vivos incluidos)

**Ventajas:** Resuelve el ISP de raíz.

**Desventajas:** Toca JobProfiles/SalaryTabulator (fuera de alcance); mayor riesgo de regresión
para sev. 🟢.

**Razón de descarte:** desproporcionado ahora; queda como ruta definida (§3 reglas, §10).

---

## 5. Justificación

### Razones principales
- El código muerto es el grueso del smell y su remoción es verificable por compilación.
- Trackear los vivos preserva comportamiento sin churn en features ajenos.

### Factores considerados
- [x] Simplicidad
- [x] Mantenibilidad
- [x] Arquitectura / fronteras de subdominio
- [x] Riesgo de regresión

### Resumen de justificación
Limpieza de bajo riesgo + deuda formalmente acotada, con regla anti-regresión y ruta de
segregación definida para cuando se priorice.

---

## 6. Consecuencias

### Consecuencias positivas
- −3 miembros muertos, −1 DTO mal ubicado; interfaz de catálogos más cohesiva.
- Deuda ISP de los 2 vivos explícita y trazable.

### Consecuencias negativas o trade-offs
- El acoplamiento cross-feature de `ResolvePositionCategoryIdAsync` (JobProfiles) y
  `ResolveSalaryClassCodeByCatalogIdAsync` (SalaryTabulator) persiste hasta priorizar la
  segregación.

### Riesgos
- Referencia oculta / `using` huérfano tras el borrado → mitigado por `-warnaserror` (Application
  e Infrastructure) y suites.

### Impacto técnico
- Interfaz, repo impl, DTO y test double actualizados; sin cambios en features consumidores.

### Impacto operativo o documental
- Esta ADR. Sin cambios en `project-foundation.md`/`AGENTS.md`.

---

## 7. Impacto por capa o área

### Domain
No aplica.

### Application
`IPositionDescriptionCatalogRepository` (−3 miembros); record `PositionSlotContractTypeLookup`
eliminado.

### Infrastructure
`PositionDescriptionCatalogRepository` (−3 implementaciones).

### API
Sin cambios de contrato.

### Data / SQL
Sin cambios (los miembros muertos no se ejecutaban).

### Security
`HasClassificationsUsingOrgUnitTypeAsync` (sin tenant, inerte) eliminado → preocupación anulada.

### Performance
No aplica.

### Testing
Test double actualizado; suites de regresión en verde.

### Documentation
Esta ADR.

---

## 8. Plan de implementación

### Cambios requeridos
- Quitar 3 miembros de la interfaz + 3 impls + DTO + 3 impls del test double.
- ADR.

### Dependencias
- Ninguna.

### Orden sugerido
1. Interfaz. 2. Impl. 3. DTO. 4. Test double. 5. ADR. 6. Build + tests + grep.

### Validaciones requeridas
- Build `-warnaserror` (Application + Infrastructure); Application.UnitTests e integración en
  verde; grep de muerte total = 0.

---

## 9. Impacto en documentación

### Documentos a actualizar
- Esta ADR (nueva).

### Observación
Complementa el hardening del catálogo (ADR-0001..0004); no revierte reglas vigentes.

---

## 10. Impacto en historias de usuario o roadmap

### HUs impactadas
- Hallazgo P5 (estructural).

### Iniciativas impactadas
- Position Description Catalog; deuda futura: segregar lookups cross-feature
  (JobProfiles/SalaryTabulator).

### Requerimientos futuros habilitados
- Extracción de `ISalaryClassCatalogLookup` / `IPositionCategoryCatalogLookup` cuando se priorice.

---

## 11. Criterios de aceptación de la decisión

### Se considerará aplicada correctamente cuando:
- Los 3 miembros muertos y el DTO no existen en `src/` ni `tests/`.
- Build con `-warnaserror` limpio (Application + Infrastructure).
- Features vivos (JobProfiles/SalaryTabulator/Slots) sin cambios y suites en verde.

### Evidencias esperadas
- `grep` de los 3 símbolos + DTO → 0 ocurrencias.
- Suites de regresión en verde.

---

## 12. Estado de seguimiento

### Estado actual
Adoptada (parte muerta); deuda abierta (segregación de los 2 vivos).

### Próxima revisión
Cuando se priorice la segregación de lookups cross-feature.

### Responsable de seguimiento
Equipo Backend

---

## 13. Notas adicionales

- **Framing corregido:** el hallazgo asumía consumo por PositionSlots; verificado que Slots **no**
  consume ninguno de estos miembros. El acoplamiento vivo real es JobProfiles y SalaryTabulator.
- `IPositionSlotRepository` ya existe como abstracción propia de Slots; cualquier necesidad futura
  de Slots sobre catálogos debe resolverse con una abstracción segregada, no ampliando la interfaz
  de catálogos.

---

## 14. Referencias

- Interfaz: `src/CLARIHR.Application/Abstractions/PositionDescriptionCatalogs/IPositionDescriptionCatalogRepository.cs`
- Impl: `src/CLARIHR.Infrastructure/PositionDescriptionCatalogs/PositionDescriptionCatalogRepository.cs`
- Consumidores vivos: `JobProfileAdministration.cs`, `SalaryTabulatorAdministration.cs`, `SalaryTabulatorExportHandler.cs`
- ADR relacionadas: `ADR-0001`…`ADR-0004` (familia de hardening del catálogo)
- Reglas: `AGENTS.md §4.5` (no código muerto), `§4.6` (salida documental ordenada)
