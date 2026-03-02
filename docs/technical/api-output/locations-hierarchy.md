# Locations Hierarchy

## Scope

HU-010 introduce administracion tenant-scoped de:

- configuracion general de jerarquia
- niveles de ubicacion
- validacion del nivel que permite centros de trabajo
- seed inicial `General`

## Endpoints

- `GET /api/v1/companies/{companyId}/location-hierarchy`
- `PUT /api/v1/companies/{companyId}/location-hierarchy`
- `GET /api/v1/companies/{companyId}/location-levels`
- `POST /api/v1/companies/{companyId}/location-levels`
- `PUT /api/v1/location-levels/{id}`
- `PATCH /api/v1/location-levels/{id}/activate`
- `PATCH /api/v1/location-levels/{id}/inactivate`

## Security

- Requiere JWT.
- `companyId` debe coincidir con el `tid` actual del token.
- Lectura:
  - `Locations.Read`
  - `Locations.Admin`
  - `iam.administration.manage`
  - `platform_admin`
- Escritura:
  - `Locations.Admin`
  - `iam.administration.manage`
  - `platform_admin`

## Seed por defecto

Al provisionar una empresa nueva el backend crea automaticamente:

1. `LocationHierarchyConfig`
   - `isMultiLevel = false`
   - `defaultGroupCode = "GENERAL"`
   - `defaultGroupName = "General"`
2. `LocationLevel`
   - `levelOrder = 1`
   - `displayName = "General"`
   - `isActive = true`
   - `isRequired = true`
   - `allowsWorkCenters = true`
3. `LocationGroup`
   - `code = "GENERAL"`
   - `name = "General"`
   - `isDefault = true`

## Reglas importantes

- Siempre debe existir al menos un nivel activo.
- Un nivel requerido no puede quedar inactivo.
- Solo el ultimo nivel activo puede permitir centros de trabajo.
- La concurrencia se valida con `concurrencyToken` y devuelve `409 CONCURRENCY_CONFLICT`.
