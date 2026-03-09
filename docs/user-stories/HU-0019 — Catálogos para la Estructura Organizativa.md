# HU-0019 — Catálogos para la Estructura Organizativa (Tipos de Empresa, Tipos de Unidad y Áreas Funcionales)

## 0) Validación del estado actual
Resultado del análisis del backend actual:

- Cobertura parcial:
  - Existe módulo de `OrgUnits` (HU-011) y permite crear unidades.
  - `OrgUnit` ya tiene campo `UnitType`, pero actualmente depende de enum fijo (`Direccion`, `Gerencia`, `Departamento`, `Coordinacion`, `Unidad`, `Otro`).
- Brechas frente al requerimiento fuente:
  - No existe catálogo de `Tipos de Empresa`.
  - `Tipos de Unidades` no está modelado como catálogo administrable; está hardcodeado en enum.
  - No existe catálogo de `Áreas Funcionales`.
  - No existe vínculo explícito de área funcional en unidades organizativas.
  - No existe API dedicada para administrar estos catálogos con ciclo de vida activo/inactivo.

Conclusión:
- Se requiere HU nueva para normalizar y administrar estos catálogos como datos tenant-scoped y reutilizables en creación de unidades y categorías de puesto.

---

## 1) Descripción del requerimiento (visión de negocio)
Como **Administrador de Empresa / RRHH**,
quiero **gestionar los catálogos base de Estructura Organizativa**:

1. `Tipos de Empresa`
2. `Tipos de Unidades`
3. `Áreas Funcionales`

para **estandarizar la clasificación organizacional** y reutilizarla en la creación de unidades y categorías de puesto.

---

## 2) Objetivo funcional (qué habilita)
- Gobernar la estructura organizativa mediante catálogos administrables.
- Evitar clasificaciones rígidas hardcodeadas.
- Homologar criterios de clasificación entre empresas y áreas.
- Reutilizar estas clasificaciones en:
  - creación/edición de `OrgUnits`
  - definición de categorías de puesto (integración futura)
  - reportes organizativos y analítica.

---

## 3) Alcance (backend/API)
### Incluye
- CRUD tenant-scoped de `Tipos de Empresa`.
- CRUD tenant-scoped de `Tipos de Unidades`.
- CRUD tenant-scoped de `Áreas Funcionales`.
- Activación/inactivación con validación de dependencias.
- Asociación de `Tipo de Unidad` y `Área Funcional` en `OrgUnit`.
- Filtros y paginación estándar.

### Fuera de alcance por ahora
- Rediseño completo de jerarquía organizativa.
- Sincronización automática con sistemas externos ERP/HRIS.
- Motor de recomendación automática de estructura.

---

## 4) Actores y permisos sugeridos
- **CompanyAdmin / HRAdmin**: administración completa.
- **HRRead / LeadershipRead**: consulta.

Permisos funcionales sugeridos:
- `OrgStructureCatalogs.Read`
- `OrgStructureCatalogs.Admin`
- `OrgUnits.Read`
- `OrgUnits.Admin`
- `iam.administration.manage` (override)
- `platform_admin` (override)

Regla base:
- Toda operación tenant-scoped valida coincidencia `companyId` vs claim `tid`.

---

## 5) Catálogos requeridos

### 5.1 Tipos de Empresa
Catálogo de clasificación de empresa/entidad.

Campos mínimos sugeridos:
- `Id` (GUID público)
- `CompanyId` (tenant)
- `Code` (único por tenant)
- `Name`
- `Description` (opcional)
- `SortOrder`
- `IsActive`
- `ConcurrencyToken`
- `CreatedAtUtc`, `UpdatedAtUtc`

### 5.2 Tipos de Unidades
Catálogo de niveles jerárquicos institucionales para uso en unidades y categorías de puesto.

Debe soportar ejemplos del requerimiento fuente:
- Dirección
- Gerencias
- Jefaturas
- Mandos Medios
- Personal Administrativo
- Personal Operativo

Campos mínimos sugeridos:
- `Id`
- `CompanyId`
- `Code`
- `Name`
- `HierarchyLevel` (opcional, entero)
- `Description` (opcional)
- `SortOrder`
- `IsActive`
- `ConcurrencyToken`
- `CreatedAtUtc`, `UpdatedAtUtc`

### 5.3 Áreas Funcionales
Catálogo de orientación funcional del negocio para clasificar unidades.

Ejemplos:
- Administración
- Comercial
- Operación
- Producción

Campos mínimos sugeridos:
- `Id`
- `CompanyId`
- `Code`
- `Name`
- `Description` (opcional)
- `SortOrder`
- `IsActive`
- `ConcurrencyToken`
- `CreatedAtUtc`, `UpdatedAtUtc`

---

## 6) Reglas de negocio
- `Code` único por tenant dentro de cada catálogo.
- No permitir duplicado semántico por nombre normalizado activo.
- Inactivación bloqueada cuando el elemento esté en uso por `OrgUnits` activas.
- No permitir referencias entre tenants.
- Escrituras por id validan `ConcurrencyToken`.
- Eliminación lógica (`activate/inactivate`) como estrategia por defecto.

---

## 7) Impacto de modelo y dominio (propuesto)

### 7.1 Entidades nuevas
- `CompanyTypeCatalogItem`
- `OrgUnitTypeCatalogItem`
- `FunctionalAreaCatalogItem`

### 7.2 Ajuste en `OrgUnit`
- Reemplazar o complementar `OrgUnitType` enum con referencia a catálogo:
  - `OrgUnitTypeCatalogItemId`
- Agregar referencia de área:
  - `FunctionalAreaCatalogItemId` (nullable según política de negocio)

Nota técnica:
- Para compatibilidad v1, puede mantenerse temporalmente `OrgUnitType` enum y coexistir con referencia catalogada hasta completar migración de consumidores.

---

## 8) API propuesta (v1)

### Tipos de Empresa
- `GET    /api/v1/companies/{companyId}/org-structure-catalogs/company-types`
- `POST   /api/v1/companies/{companyId}/org-structure-catalogs/company-types`
- `GET    /api/v1/org-structure-catalogs/company-types/{id}`
- `PUT    /api/v1/org-structure-catalogs/company-types/{id}`
- `PATCH  /api/v1/org-structure-catalogs/company-types/{id}/activate`
- `PATCH  /api/v1/org-structure-catalogs/company-types/{id}/inactivate`

### Tipos de Unidades
- `GET    /api/v1/companies/{companyId}/org-structure-catalogs/unit-types`
- `POST   /api/v1/companies/{companyId}/org-structure-catalogs/unit-types`
- `GET    /api/v1/org-structure-catalogs/unit-types/{id}`
- `PUT    /api/v1/org-structure-catalogs/unit-types/{id}`
- `PATCH  /api/v1/org-structure-catalogs/unit-types/{id}/activate`
- `PATCH  /api/v1/org-structure-catalogs/unit-types/{id}/inactivate`

### Áreas Funcionales
- `GET    /api/v1/companies/{companyId}/org-structure-catalogs/functional-areas`
- `POST   /api/v1/companies/{companyId}/org-structure-catalogs/functional-areas`
- `GET    /api/v1/org-structure-catalogs/functional-areas/{id}`
- `PUT    /api/v1/org-structure-catalogs/functional-areas/{id}`
- `PATCH  /api/v1/org-structure-catalogs/functional-areas/{id}/activate`
- `PATCH  /api/v1/org-structure-catalogs/functional-areas/{id}/inactivate`

### Ajuste de OrgUnits
- `POST/PUT /api/v1/companies/{companyId}/org-units`
  - agregar payload:
    - `unitTypeCatalogItemId`
    - `functionalAreaCatalogItemId` (si aplica)

---

## 9) Contratos de error sugeridos
- `ORG_STRUCTURE_CATALOG_FORBIDDEN`
- `ORG_STRUCTURE_CATALOG_NOT_FOUND`
- `ORG_STRUCTURE_CATALOG_CODE_CONFLICT`
- `ORG_STRUCTURE_CATALOG_IN_USE`
- `CONCURRENCY_CONFLICT`
- `TENANT_MISMATCH`
- `RESOURCE_IN_USE`

---

## 10) Base de datos y migraciones (propuesto)
- Crear script HU:
  - `docs/technical/sql/hu019_org_structure_catalogs.sql`
- Tablas sugeridas:
  - `company_type_catalog_items`
  - `org_unit_type_catalog_items`
  - `functional_area_catalog_items`
- Alter table de `org_units` para nuevas FK:
  - `org_unit_type_catalog_item_id`
  - `functional_area_catalog_item_id`
- Índices:
  - unicidad `(tenant_id, normalized_code)`
  - búsqueda `(tenant_id, normalized_name, is_active)`
- Seed:
  - catálogos base para tenant A/B en `seed_api_test_data.sql`.

---

## 11) Pruebas y aceptación

### Unit tests
- Normalización y unicidad por catálogo.
- Reglas de activación/inactivación en uso.
- Concurrencia por `ConcurrencyToken`.

### Integration tests HTTP
- CRUD completo para cada catálogo.
- `403` por permiso insuficiente.
- `403` `TENANT_MISMATCH`.
- `404` no encontrado.
- `409` conflicto de código/concurrencia/recurso en uso.

### Regression
- Confirmar que HU-011 (`OrgUnits`) mantiene compatibilidad en rutas existentes.

---

## 12) Supuestos iniciales
- Los catálogos son tenant-scoped y administrables por empresa.
- Se prioriza coexistencia de modelo durante transición (enum -> catálogo) para evitar ruptura abrupta.
- Toda validación crítica (negocio + seguridad + tenant) vive en backend como fuente de verdad.
