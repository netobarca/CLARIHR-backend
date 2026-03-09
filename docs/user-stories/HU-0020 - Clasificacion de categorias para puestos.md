# HU-0020 - Clasificacion de categorias para puestos (Catalogo I del Manual Descriptivo de Puestos)

## 0) Validacion del estado actual
Resultado del analisis del backend/documentacion actual:

- Cobertura parcial:
  - Existe `JobProfile` (HU-0012), pero no tiene un catalogo formal para clasificar categorias de puesto por funcion, jerarquia y tipo de contrato.
  - Existe `job_catalog_items` y API `job-catalogs` para catalogos dinamicos tenant-scoped.
  - Existe `OrgStructureCatalogs` (HU-0019) con `unit-types`, util para representar jerarquia institucional.
- Brechas frente al requerimiento fuente:
  - No existe entidad/catalogo dedicado para `Clasificacion de categorias para puestos`.
  - No existen categorias explicitas en `job-catalogs` para `tipo de funcion` y `tipo de contrato` orientadas a clasificacion de categorias de puesto.
  - No existe API dedicada para administrar la combinacion de ejes:
    - funcion (Directivo, Administrativo, Operativo)
    - jerarquia institucional (Director, Gerente, Jefatura, etc.)
    - tipo de contrato (Permanente, Temporal, etc.)
  - No existe punto formal de integracion para que el catalogo ii (`Categoria de Puestos`) consuma esta clasificacion.

Conclusion:
- Se requiere HU nueva para crear un catalogo tenant-scoped de `Clasificacion de categorias para puestos`, reusable por `Categoria de Puestos`.

---

## 1) Descripcion del requerimiento (vision de negocio)
Como **Administrador de Empresa / RRHH**,
quiero **gestionar un catalogo de clasificacion de categorias para puestos**,
definiendo agrupaciones por funcion, jerarquia institucional y tipo de contrato,
para que la institucion cuente con una base estandarizada y auditable que luego se utilice en el catalogo de `Categoria de Puestos`.

Resultado esperado:
- Clasificacion consistente entre areas y puestos.
- Menor ambiguedad al definir categorias de puesto.
- Reutilizacion del catalogo en creacion de categorias y reportes.

---

## 2) Objetivo funcional (que habilita)
- Estandarizar agrupaciones de categorias de puesto por ejes organizativos clave.
- Evitar clasificaciones en texto libre y valores hardcodeados.
- Habilitar prerequisito funcional para el catalogo ii (`Categoria de Puestos`).
- Facilitar filtros/reportes por funcion, jerarquia y contrato.
- Mantener trazabilidad de cambios con auditoria y concurrencia.

---

## 3) Alcance (backend/API)
### Incluye
- CRUD tenant-scoped de `Clasificacion de categorias para puestos`.
- Activacion/inactivacion con validacion de dependencias.
- Integracion con catalogos base para ejes de clasificacion:
  - `PositionFunctionType` (via `job-catalogs`)
  - `PositionContractType` (via `job-catalogs`)
  - `OrgUnitTypeCatalogItem` (via HU-0019)
- Filtros y paginacion estandar:
  - `isActive`
  - `functionTypeId`
  - `orgUnitTypeId`
  - `contractTypeId`
  - `q`
  - `page`
  - `pageSize`

### Fuera de alcance por ahora
- Implementacion del catalogo ii `Categoria de Puestos` (se define en HU separada).
- Migracion automatica de datos historicos a clasificaciones nuevas.
- Reglas avanzadas de aprobacion multinivel para cambios del catalogo.

---

## 4) Actores y permisos sugeridos
- **CompanyAdmin / HRAdmin**: administracion completa.
- **HRRead / LeadershipRead**: solo consulta.

Permisos funcionales sugeridos:
- `PositionCategoryClassifications.Read`
- `PositionCategoryClassifications.Admin`
- `JobCatalogs.Read`
- `JobCatalogs.Admin` (para catalogos base de funcion/contrato)
- `OrgStructureCatalogs.Read` (para eje de jerarquia)
- `iam.administration.manage` (override)
- `platform_admin` (override)

Regla base:
- Toda operacion tenant-scoped valida coincidencia `companyId` de ruta vs claim `tid`.

---

## 5) Datos que debe manejar el backend (modelo minimo)

### 5.1 Catalogos base para ejes
Se propone extender `job_catalog_items` con categorias:
- `PositionFunctionType` (ej.: Directivo, Administrativo, Operativo)
- `PositionContractType` (ej.: Permanente, Temporal)

Para jerarquia institucional se reutiliza:
- `OrgUnitTypeCatalogItem` (HU-0019).

### 5.2 Entidad nueva: `PositionCategoryClassification`
Campos minimos sugeridos:
- `Id` (GUID publico)
- `CompanyId` (tenant)
- `Code` (unico por tenant)
- `Name`
- `Description` (opcional)
- `PositionFunctionCatalogItemId` (FK `job_catalog_items`, categoria `PositionFunctionType`)
- `OrgUnitTypeCatalogItemId` (FK `org_unit_type_catalog_items`)
- `PositionContractCatalogItemId` (FK `job_catalog_items`, categoria `PositionContractType`)
- `SortOrder`
- `IsActive`
- `ConcurrencyToken`
- `CreatedAtUtc`, `UpdatedAtUtc`

### 5.3 Vista de respuesta recomendada
Para simplificar consumo frontend/reportes:
- ids + codigos + nombres de cada eje
- metadatos de estado y concurrencia
- bandera `isInUse` (si ya esta referenciado por catalogos/entidades consumidoras)

---

## 6) Reglas de negocio
- `Code` unico por tenant.
- No permitir duplicado de combinacion activa:
  - `(PositionFunctionCatalogItemId, OrgUnitTypeCatalogItemId, PositionContractCatalogItemId)`.
- Todos los ejes referenciados deben:
  - existir
  - pertenecer al mismo tenant
  - estar activos
  - cumplir categoria esperada (cuando aplique `job_catalog_items`).
- Inactivacion bloqueada si el registro esta en uso por catalogos consumidores (catalogo ii y/o otros modulos).
- Escrituras por id validan `ConcurrencyToken`.
- Eliminacion logica (`activate/inactivate`) como estrategia por defecto.
- Diferenciar `NotFound` vs `TenantMismatch` en resolucion por id.

---

## 7) Impacto de modelo y dominio (propuesto)

### 7.1 Entidad nueva
- `PositionCategoryClassification` (dominio + persistencia + repositorio).

### 7.2 Ajuste de catalogos base
- Extender `JobCatalogCategory` con:
  - `PositionFunctionType`
  - `PositionContractType`

### 7.3 Integraciones
- Reutilizar `OrgUnitTypeCatalogItem` como fuente de jerarquia institucional.
- Preparar FK/relacion para que `Categoria de Puestos` (HU siguiente) dependa de esta entidad.

---

## 8) API propuesta (v1)

### 8.1 Clasificacion de categorias para puestos
- `GET    /api/v1/companies/{companyId}/position-category-classifications`
- `POST   /api/v1/companies/{companyId}/position-category-classifications`
- `GET    /api/v1/position-category-classifications/{id}`
- `PUT    /api/v1/position-category-classifications/{id}`
- `PATCH  /api/v1/position-category-classifications/{id}/activate`
- `PATCH  /api/v1/position-category-classifications/{id}/inactivate`

### 8.2 Catalogos base reutilizados
- `GET/POST /api/v1/companies/{companyId}/job-catalogs/PositionFunctionType`
- `GET/POST /api/v1/companies/{companyId}/job-catalogs/PositionContractType`
- Jerarquia:
  - `GET /api/v1/companies/{companyId}/org-structure-catalogs/unit-types`

---

## 9) Contratos de error sugeridos
- `POSITION_CATEGORY_CLASSIFICATION_FORBIDDEN`
- `POSITION_CATEGORY_CLASSIFICATION_NOT_FOUND`
- `POSITION_CATEGORY_CLASSIFICATION_CODE_CONFLICT`
- `POSITION_CATEGORY_CLASSIFICATION_DUPLICATE_AXES`
- `POSITION_CATEGORY_CLASSIFICATION_IN_USE`
- `JOB_CATALOG_ITEM_INVALID_CATEGORY`
- `JOB_CATALOG_ITEM_NOT_FOUND`
- `ORG_STRUCTURE_CATALOG_NOT_FOUND`
- `CONCURRENCY_CONFLICT`
- `TENANT_MISMATCH`

---

## 10) Base de datos y migraciones (propuesto)
- Crear script HU:
  - `docs/technical/sql/hu020_position_category_classifications.sql`
- Tabla sugerida:
  - `position_category_classifications`
- FKs sugeridas:
  - `position_function_catalog_item_id -> job_catalog_items(id)`
  - `org_unit_type_catalog_item_id -> org_unit_type_catalog_items(id)`
  - `position_contract_catalog_item_id -> job_catalog_items(id)`
- Indices recomendados:
  - unicidad `(tenant_id, normalized_code)`
  - unicidad de combinacion de ejes por tenant
  - busqueda `(tenant_id, is_active)`
  - busqueda `(tenant_id, normalized_name)`
- Seed:
  - valores base de `PositionFunctionType` y `PositionContractType` en `seed_api_test_data.sql`.

---

## 11) Pruebas y aceptacion

### Unit tests
- Normalizacion y unicidad por `Code`.
- Validacion de combinacion unica de ejes.
- Reglas de activacion/inactivacion con dependencias.
- Concurrencia por `ConcurrencyToken`.

### Integration tests HTTP
- CRUD completo con autorizacion valida.
- `403` por permiso insuficiente.
- `403` `TENANT_MISMATCH`.
- `404` no encontrado.
- `409` conflicto de codigo, concurrencia o duplicado de ejes.
- Validacion de filtros/paginacion en listados.

### Regression
- Verificar que HU-0012 (`JobProfiles`) y HU-0019 (`OrgStructureCatalogs`) no rompen compatibilidad.
- Confirmar uso correcto de `job-catalogs` para nuevas categorias de ejes.

---

## 12) Supuestos iniciales
- Este catalogo se implementa como prerequisito inmediato del catalogo ii (`Categoria de Puestos`).
- La jerarquia institucional reutiliza catalogo de `unit-types` para evitar duplicidad semantica.
- Funcion y tipo de contrato se administran como catalogos tenant-scoped via `job-catalogs`.
- Backend mantiene la validacion critica de negocio/tenant/seguridad como fuente de verdad.
