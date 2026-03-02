# Location Groups

## Scope

Los grupos de ubicacion modelan el arbol jerarquico padre-hijo por tenant.

## Endpoints

- `GET /api/v1/companies/{companyId}/location-groups/tree`
- `GET /api/v1/companies/{companyId}/location-groups`
- `POST /api/v1/companies/{companyId}/location-groups`
- `PUT /api/v1/location-groups/{id}`
- `PATCH /api/v1/location-groups/{id}/move`
- `PATCH /api/v1/location-groups/{id}/activate`
- `PATCH /api/v1/location-groups/{id}/inactivate`

## Reglas

- `code` es unico por tenant.
- `levelOrder = 1` no puede tener padre.
- `levelOrder > 1` exige padre.
- El padre debe ser del nivel inmediatamente superior.
- El backend bloquea padres inactivos.
- El grupo default `GENERAL` es protegido:
  - no se renombra
  - no se recodifica
  - no se inactiva
- La inactivacion falla si existen:
  - hijos activos
  - centros activos asignados

## Contrato de busqueda

`GET /api/v1/companies/{companyId}/location-groups`

Query params:

- `levelOrder?`
- `isActive?`
- `q?`
- `page`
- `pageSize`

Respuesta:

- `PagedResponse<LocationGroupResponse>`
