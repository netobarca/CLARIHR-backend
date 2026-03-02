# Performance Analysis

## Context

- Delivery baseline: current state up to HU-008
- Date: 2026-03-01
- Focus: query shape, indexing, caching, payload handling and known hot paths

## Performance Compliance

### 1. Read query behavior

Estado: compliant for current scope

Evidencia:

- Los repositorios de lectura principales usan `AsNoTracking()`, por ejemplo:
  - `src/CLARIHR.Infrastructure/Companies/UserCompanyRepository.cs`
  - `src/CLARIHR.Infrastructure/IdentityAccess/IamAdministrationRepository.cs`
  - `src/CLARIHR.Infrastructure/Auditing/AuditLogRepository.cs`
  - `src/CLARIHR.Infrastructure/IdentityAccess/RbacAuthorizationService.cs`
  - `src/CLARIHR.Infrastructure/IdentityAccess/FieldAccessProfileService.cs`

Impacto:

- Se reduce overhead del change tracker en listados y consultas de solo lectura.

### 2. Pagination in primary lists

Estado: compliant

Evidencia:

- Los listados de company users, IAM users, roles, permissions, audit logs y auditoria RBAC usan `Skip/Take` con `PagedResponse`.
- Esto controla tamano de payload y evita cargar listas completas en endpoints operativos principales.

### 3. Indexing

Estado: compliant

Evidencia:

- Existen indices utiles y unicos en entidades criticas:
  - usuarios auth por email/provider
  - memberships por usuario/empresa/rol/status
  - roles y permisos IAM por tenant y claves normalizadas
  - field catalog por field key y resource
  - role field permissions por `(TenantId, RoleId, NormalizedFieldKey)`
  - audit logs por tenant, actor, entity y event type
  - tokens por hash/family

Impacto:

- El modelo persistente esta preparado para accesos de lookup y filtros comunes del sistema actual.

### 4. Caching

Estado: compliant with defined single-node and multi-node strategy

Evidencia:

- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionOverrideCache.cs` centraliza el cache de overrides.
- `src/CLARIHR.Infrastructure/Configuration/FieldPermissionCacheOptions.cs` define modos `MemoryOnly` y `Distributed`.
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldAccessProfileService.cs` y la familia `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService*.cs` dependen de la abstraccion, no de `IMemoryCache` directo.
- La cache se invalida cuando cambian permisos por campo.
- La clave esta segmentada por tenant, role y resource.

Impacto:

- Reduce carga repetitiva al resolver overrides por campo y perfiles de acceso y deja preparado el salto a cache distribuida sin reescribir la logica de negocio.

Tradeoff:

- El modo `Distributed` requiere registrar un `IDistributedCache` concreto en el entorno objetivo.

### 5. Payload and serialization control

Estado: compliant

Evidencia:

- Los listados proyectan DTOs especificos.
- Los permisos por campo ya permiten ocultar o limitar visibilidad en backend.
- Los responses de auditoria y administracion no devuelven entidades completas de EF.

Impacto:

- Se evita exponer grafo completo y se controla mejor el tamano de los payloads.

## Performance Risks

### 1. Search strategy with `Contains`

Riesgo:

- Varias busquedas usan `Contains` sobre columnas normalizadas y valores `ToUpper()`.

Impacto:

- A medida que crezca el volumen, estas consultas pueden perder eficiencia y no aprovechar indices de forma optima.

Recomendacion:

- La decision ya esta tomada: mantener substring match y endurecer con `pg_trgm` e indices GIN cuando el volumen lo justifique.
- El artefacto tecnico preparado es `docs/technical/sql/p2_search_growth_hardening.sql`.

### 2. Authorization and field-profile hot path

Riesgo:

- `FieldAccessProfileService` puede cargar usuario, roles, permisos y overrides por recurso para construir el perfil efectivo.

Impacto:

- Con usuarios con muchos roles o recursos complejos, este camino puede crecer en costo.

Mitigacion actual:

- Cache de overrides por tenant/role/resource.

Recomendacion:

- Medir este flujo con telemetria y considerar cache del perfil efectivo completo si el caso de uso se vuelve muy frecuente.

### 3. Distributed cache activation depends on deployment

Riesgo:

- La estrategia ya soporta `Distributed`, pero el entorno debe proveer una implementacion real de `IDistributedCache`.

Impacto:

- Si se despliega en varias instancias sin activar/proveer el cache distribuido, puede haber inconsistencias temporales despues de cambios de permisos.

Recomendacion:

- Activar `Distributed` solo junto con un backend distribuido real compartido por todos los nodos.

## Search Strategy Decision

Decision:

- no cambiar la semantica actual de substring match
- no degradar recall pasando a `StartsWith`
- usar `pg_trgm` como paso de escalamiento natural para PostgreSQL
- activar el hardening solo cuando se alcancen umbrales operativos medibles

Umbrales definidos:

- `p95` de busqueda mayor a `200ms`
- crecimiento relevante en usuarios o logs por tenant
- degradacion perceptible reportada por QA/soporte

Artefactos:

- `docs/technical/api-output/search-growth-strategy.md`
- `docs/technical/sql/p2_search_growth_hardening.sql`

## Final Assessment

Veredicto:

- La solucion esta bien encaminada para el tamano actual del producto y demuestra decisiones correctas de base: indices, `AsNoTracking`, paginacion y cache.
- Los riesgos reales no son de performance inmediata, sino de escalamiento progresivo.
- La estrategia de búsqueda para crecimiento ya está definida; el siguiente paso será aplicarla cuando el volumen o la latencia lo justifiquen.
