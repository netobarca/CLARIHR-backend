# ADR-0002 — Búsqueda free-text del catálogo: longitud mínima de `q` + supuesto de escala

- **Estado:** Aprobado
- **Fecha:** 2026-05-17
- **Autores:** Equipo Backend (asistido por agente)
- **Relacionado con:** Hallazgo P2 🟡 — Búsqueda free-text `LIKE '%x%'` no-sargable (Position Description Catalog)
- **Reemplaza:** No aplica
- **Reemplazado por:** No aplica

---

## 1. Título

La búsqueda free-text de los listados del Position Description Catalog impone una longitud mínima
de `q`; el patrón `LIKE '%x%'` no-sargable se mantiene bajo un supuesto de escala declarado, con
`pg_trgm` como plan de contingencia documentado.

---

## 2. Contexto

### Contexto resumido
Los 3 search del catálogo (`PositionDescriptionCatalogRepository.cs`) filtran con
`NormalizedCode.Contains(q) || NormalizedName.Contains(q)` → `LIKE '%x%'` no-sargable
(classifications: OR sobre 5 columnas, 3 en tablas joined). Los índices B-tree compuestos
`(TenantId, …, Normalized*)` no aplican a `%x%`.

### Situación actual
No hay regla de longitud mínima de `q` (solo `MaximumLength(150)`). Una `q` de 1 carácter (`%a%`)
recorre todo el catálogo del tenant. No existe `pg_trgm`/FTS en el repositorio.

### Motivadores
- Atajar el peor caso (`q` muy corta) sin migración DB.
- Declarar explícitamente el supuesto de escala y el plan de contingencia.
- Dejar registrada la deuda cross-cutting (mismo patrón en JP y 23+ repos).

---

## 3. Decisión

### Decisión adoptada
Imponer una longitud mínima de `q` (`MinSearchLength = 2`, tras `Trim()`) en los 3 validators del
catálogo vía FluentValidation (rechazo 400 antes de tocar caché/DB). `q` vacía/whitespace sigue
siendo "sin filtro". El `LIKE '%x%'` no-sargable se mantiene bajo el supuesto de catálogos
pequeños por tenant; `pg_trgm` (GIN `gin_trgm_ops`) + `EF.Functions.ILike` es el plan de
contingencia con trigger documentado.

### Alcance de la decisión
- [x] Un módulo específico (Position Description Catalog) + guideline transversal de búsqueda.

### Reglas derivadas
- Todo endpoint de búsqueda free-text impone longitud mínima de `q` y declara su supuesto de escala.
- `q` vacía/whitespace = "sin filtro" (válido), nunca un `%%` scan implícito.
- Umbral del dominio alineado con el precedente Internal Catalogs (`MinQueryLength: 2`).

---

## 4. Alternativas evaluadas

### Alternativa 1
**Nombre:** Solo documentar el supuesto de escala

**Descripción:** No cambiar código; solo documentar supuesto + contingencia.

**Ventajas:** Cero riesgo de regresión.

**Desventajas:** El peor caso (`q` de 1 char) sigue sin guardrail.

**Razón de descarte:** insuficiente como guardrail efectivo.

### Alternativa 2
**Nombre:** Guard de longitud mínima + documentación *(elegida)*

**Descripción:** Min length en validators + supuesto de escala + contingencia pg_trgm documentados.

**Ventajas:** Ataja el peor caso; sin migración; reutiliza precedente del dominio; bajo riesgo.

**Desventajas:** No hace sargable el `%x%`; clientes con `q` de 1 char reciben 400.

**Razón de aceptación:** mejor relación coste/beneficio dado que los catálogos son pequeños.

### Alternativa 3
**Nombre:** pg_trgm GIN + ILIKE (fix durable, ahora)

**Descripción:** `CREATE EXTENSION pg_trgm` + índices GIN + switch a `EF.Functions.ILike`.

**Ventajas:** Búsqueda sargable real.

**Desventajas:** Migración DB; validación de plan de query; mayor superficie de regresión;
cross-cutting (23+ repos).

**Razón de descarte:** desproporcionado para severidad 🟡 con catálogos pequeños; queda como
contingencia con trigger.

---

## 5. Justificación

### Razones principales
- El guardrail elimina el peor caso de scan con cambio mínimo y reutilizando un patrón existente.
- El supuesto de escala documentado convierte un riesgo implícito en una decisión explícita con
  trigger de escalado medible.

### Factores considerados
- [x] Simplicidad
- [x] Mantenibilidad
- [x] Rendimiento
- [x] Escalabilidad
- [x] Compatibilidad con arquitectura actual
- [x] Coste de implementación

### Resumen de justificación
Los catálogos son pequeños por tenant; un guard de longitud mínima + supuesto explícito + plan de
contingencia es proporcional a la severidad media-baja y no introduce riesgo de migración.

---

## 6. Consecuencias

### Consecuencias positivas
- El peor caso (`q` de 1 char → scan tenant-wide) queda atajado con 400 temprano.
- Supuesto de escala y trigger de pg_trgm explícitos y auditables.

### Consecuencias negativas o trade-offs
- Clientes que envían `q` de 1 carácter pasan a recibir 400 (antes 200). Recomendación: el
  frontend debe aplicar debounce/min-length.

### Riesgos
- Falsos negativos preexistentes por normalización asimétrica de búsqueda (ver §13). Independiente
  de esta decisión.

### Impacto técnico
- Constantes/`IsValidSearchLength` en `PositionDescriptionCatalogValidationRules`; 3 validators
  actualizados. Sin cambios en repositorio ni migraciones.

### Impacto operativo o documental
- Nueva guideline transversal en `project-foundation.md §12.8` y referencia en `AGENTS.md §4.5`.

---

## 7. Impacto por capa o área

### Domain
No aplica.

### Application
`PositionDescriptionCatalogCommon.cs` (constantes + helper) y 3 search validators en
`PositionDescriptionCatalogAdministration.cs`.

### Infrastructure
No aplica (repositorio sin cambios).

### API
Contrato: `q` no vacía con < 2 chars → 400. `q` vacía/whitespace sin cambios.

### Data / SQL
No aplica (sin migración). `pg_trgm` queda como contingencia.

### Security
No aplica.

### Performance
Elimina el peor caso de scan; el resto del patrón se mantiene bajo supuesto de escala.

### Testing
Nuevos tests unitarios de los 3 validators + `IsValidSearchLength`.

### Documentation
`project-foundation.md §12.8`, `AGENTS.md §4.5`, esta ADR.

---

## 8. Plan de implementación

### Cambios requeridos
- Constantes `MinSearchLength`/`MaxSearchLength` + `IsValidSearchLength`.
- 3 validators: `MaxSearchLength` + `Must(IsValidSearchLength)`.
- Tests unitarios; documentación; ADR.

### Dependencias
- Ninguna externa.

### Orden sugerido
1. Constantes/helper. 2. Validators. 3. Tests. 4. Documentación/ADR. 5. Build + tests.

### Validaciones requeridas
- `dotnet build -warnaserror` limpio; suite Application.UnitTests en verde.

---

## 9. Impacto en documentación

### Documentos a actualizar
- `docs/technical/overview/project-foundation.md` (§12.8 añadida)
- `AGENTS.md` (§4.5, bullet de referencia)

### Observación
Complementa las reglas de rendimiento vigentes; no revierte ninguna.

---

## 10. Impacto en historias de usuario o roadmap

### HUs impactadas
- Hallazgo P2 (remediación de rendimiento de búsqueda del catálogo).

### Iniciativas impactadas
- Position Description Catalog. Deuda cross-cutting: Job Profiles + 23+ repos.

### Requerimientos futuros habilitados
- Migración a `pg_trgm`/FTS con trigger documentado cuando el volumen lo exija.

---

## 11. Criterios de aceptación de la decisión

### Se considerará aplicada correctamente cuando:
- `q` no vacía con < `MinSearchLength` → 400 en los 3 endpoints.
- `q` vacía/whitespace → 200 (sin filtro, comportamiento inalterado).
- Build limpio y tests unitarios de validators en verde.

### Evidencias esperadas
- Tests unitarios nuevos pasando.
- §12.8 y bullet en AGENTS.md presentes.

---

## 12. Estado de seguimiento

### Estado actual
Adoptada

### Próxima revisión
Al alcanzar el trigger de escalado declarado en §12.8 (volumen/p95) o ante incidente de latencia.

### Responsable de seguimiento
Equipo Backend

---

## 13. Notas adicionales

- **Deuda cross-cutting reconocida:** el patrón `.Contains()` no-sargable existe en el search de
  Job Profiles y en 23+ repositorios. No se aborda aquí (alcance acotado al catálogo por decisión
  del usuario). Los nuevos search deben aplicar el guardrail y declarar su supuesto de escala
  (§12.8).
- **Observación independiente (no abordada):** la normalización de búsqueda
  (`search.Trim().ToUpperInvariant()`) es asimétrica respecto a la de escritura
  (NFD → strip marks → NFC → upper). Puede causar falsos negativos con acentos. Bug separado,
  fuera del alcance de P2.

---

## 14. Referencias

- Foundation document: `docs/technical/overview/project-foundation.md` (§12.8)
- Reglas: `AGENTS.md` (§4.5)
- Código: `src/CLARIHR.Application/Features/PositionDescriptionCatalogs/Common/PositionDescriptionCatalogCommon.cs`, `.../PositionDescriptionCatalogAdministration.cs`
- Precedente: `src/CLARIHR.Application/Features/InternalCatalogs/Common/InternalCatalogCommon.cs` (`MinQueryLength`)
- ADR relacionada: `docs/technical/adr/ADR-0001-catalog-allowedactions-no-per-item-dependency.md`
