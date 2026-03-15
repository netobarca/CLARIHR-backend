# API Endpoint Reference

## Locations

### `POST /api/v1/companies/{companyId}/locations/bootstrap-tree`

Bootstrap inicial de locations a partir de un arbol enviado por frontend. Este endpoint transforma el seed inicial del tenant en una configuracion multi-nivel fija `Pais -> Departamento -> Municipio` y devuelve el estado completo resultante para hidratar la UI.

**Authorization**
- Requiere autenticacion.
- Requiere permiso de administracion de locations para el `companyId` activo.
- Es tenant-scoped; si el tenant del token no coincide, responde `403`.

**Disponibilidad**
- Solo funciona mientras el tenant siga en el estado seed inicial:
  - `LocationHierarchy` en single-level
  - un solo `LocationLevel` general
  - un solo `LocationGroup` default `GENERAL`
  - sin work centers activos
- Si el tenant ya fue configurado o alterado, responde `409 LOCATION_TREE_BOOTSTRAP_NOT_ALLOWED`.

**Request**

```json
{
  "root": {
    "code": "SV",
    "name": "El Salvador",
    "description": "country-root",
    "children": [
      {
        "code": "SS",
        "name": "San Salvador",
        "description": "dept-ss-id",
        "children": [
          {
            "code": "SS-CENTRO",
            "name": "San Salvador Centro",
            "description": null,
            "children": []
          }
        ]
      }
    ]
  }
}
```

**Request rules**
- `root` es obligatorio.
- Profundidad maxima: 3 niveles.
- `code` debe seguir las reglas existentes de locations y ser unico dentro del payload.
- El backend deriva `levelOrder`, `parentId`, `isActive`, `isDefault` y `concurrencyToken`.
- El backend crea o ajusta exactamente estos niveles:
  - `Pais` nivel 1
  - `Departamento` nivel 2
  - `Municipio` nivel 3

**Response `201 Created`**

```json
{
  "hierarchy": {
    "id": "guid",
    "isMultiLevel": true,
    "defaultGroupCode": "SV",
    "defaultGroupName": "El Salvador",
    "concurrencyToken": "guid"
  },
  "levels": [
    {
      "id": "guid",
      "levelOrder": 1,
      "displayName": "Pais",
      "isActive": true,
      "isRequired": true,
      "allowsWorkCenters": false,
      "concurrencyToken": "guid"
    },
    {
      "id": "guid",
      "levelOrder": 2,
      "displayName": "Departamento",
      "isActive": true,
      "isRequired": false,
      "allowsWorkCenters": false,
      "concurrencyToken": "guid"
    },
    {
      "id": "guid",
      "levelOrder": 3,
      "displayName": "Municipio",
      "isActive": true,
      "isRequired": false,
      "allowsWorkCenters": true,
      "concurrencyToken": "guid"
    }
  ],
  "locations": [
    {
      "id": "guid",
      "levelOrder": 1,
      "code": "SV",
      "name": "El Salvador",
      "parentId": null,
      "description": "country-root",
      "isActive": true,
      "isDefault": true,
      "concurrencyToken": "guid",
      "children": []
    }
  ]
}
```

**Observable behavior**
- Reemplaza el `default group` seed por el root enviado en el arbol.
- Mantiene los endpoints actuales de `location-hierarchy`, `location-levels` y `location-groups` para mantenimiento posterior.
- Solo los grupos de nivel 3 pueden alojar work centers indirectamente, porque solo el nivel `Municipio` queda con `allowsWorkCenters = true`.

**Main errors**
- `400 common.validation`
- `401 UNAUTHENTICATED`
- `403 LOCATIONS_FORBIDDEN`
- `403 TENANT_MISMATCH`
- `409 LOCATION_TREE_BOOTSTRAP_NOT_ALLOWED`
- `409 LOCATION_GROUP_CODE_CONFLICT`

**OpenAPI note**
- El contrato estructurado del API sigue generado por Swagger en runtime desde la metadata de controllers y DTOs.
- En este repo no existia un `docs/technical/api/openapi.yaml` versionado para actualizar sin introducir una copia parcial o inconsistente.
