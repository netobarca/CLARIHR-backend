# API Endpoint Reference

## Account Companies

### `POST /api/account/companies`

Crea una nueva company para el usuario autenticado. En el MVP, `countryCode` es obligatorio porque el backend usa ese valor para sembrar automáticamente la estructura inicial de locations del tenant.

**Authorization**
- Requiere autenticación.

**Request**

```json
{
  "name": "Acme El Salvador",
  "countryCode": "SV",
  "companyTypeId": null,
  "initialLegalRepresentative": {
    "firstName": "Ana",
    "lastName": "Mendoza",
    "documentType": "TaxId",
    "documentNumber": "0614-290190-102-3",
    "positionTitle": "Representante Legal",
    "representationType": "PrimaryLegalRepresentative",
    "authorityDescription": "Representación general judicial y administrativa",
    "appointmentInstrument": "Acta de nombramiento",
    "appointmentDateUtc": "2026-01-10T00:00:00Z",
    "effectiveFromUtc": "2026-01-10T00:00:00Z",
    "effectiveToUtc": null,
    "email": "ana@acme.test",
    "phone": "+50370000000",
    "isPrimary": true
  }
}
```

**Observable behavior**
- `countryCode` es inmutable en el MVP.
- Si el país tiene plantilla soportada, provisioning siembra automáticamente:
  - `LocationHierarchy` multi-level
  - `LocationLevels` fijos `Pais -> Departamento -> Municipio`
  - `LocationGroups` predefinidos por país
- Si el país no está soportado por el seed backend, responde error de validación `provisioning.country_not_supported`.

## Locations

En el MVP, el frontend no debe construir la jerarquía de locations. El sistema la genera automáticamente al crear la company y luego se consume por lectura.

### `GET /api/v1/companies/{companyId}/location-hierarchy`

Devuelve la configuración de jerarquía de locations ya sembrada para el tenant.

**Authorization**
- Requiere autenticación.
- Requiere permiso de lectura de locations para el `companyId` activo.
- Es tenant-scoped; si el tenant del token no coincide, responde `403`.

**Response `200 OK`**

```json
{
  "id": "guid",
  "isMultiLevel": true,
  "defaultGroupCode": "GENERAL",
  "defaultGroupName": "General",
  "concurrencyToken": "guid"
}
```

**Observable behavior**
- `GENERAL` se mantiene como metadato de config.
- Ya no existe un endpoint de bootstrap por árbol enviado por frontend.

### `GET /api/v1/companies/{companyId}/location-levels`

Devuelve los niveles de locations sembrados por el sistema.

**Response `200 OK`**

```json
[
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
]
```

### `GET /api/v1/companies/{companyId}/location-groups/tree`

Devuelve el árbol de locations ya sembrado para el país de la company.

**Response `200 OK`**

```json
[
  {
    "id": "guid",
    "levelOrder": 1,
    "code": "SV",
    "name": "El Salvador",
    "parentId": null,
    "description": "Pais",
    "isActive": true,
    "isDefault": false,
    "concurrencyToken": "guid",
    "children": [
      {
        "id": "guid",
        "levelOrder": 2,
        "code": "SS",
        "name": "San Salvador",
        "parentId": "guid",
        "description": "Departamento",
        "isActive": true,
        "isDefault": false,
        "concurrencyToken": "guid",
        "children": [
          {
            "id": "guid",
            "levelOrder": 3,
            "code": "APOPA",
            "name": "Apopa",
            "parentId": "guid",
            "description": "Municipio",
            "isActive": true,
            "isDefault": false,
            "concurrencyToken": "guid",
            "children": []
          }
        ]
      }
    ]
  }
]
```

**Observable behavior**
- Los `LocationGroups` son tenant-scoped, pero nacen desde una plantilla backend por país.
- Ningún grupo real se marca como `default` en este seed.
- Solo los grupos de nivel 3 pueden alojar work centers, porque solo `Municipio` tiene `allowsWorkCenters = true`.

## MVP flow note

Los endpoints write de `location-hierarchy`, `location-levels` y `location-groups` siguen existiendo para administración técnica y compatibilidad, pero salen del flujo principal del frontend para onboarding. El flujo recomendado es:

1. `POST /api/account/companies` con `countryCode`
2. `POST /api/account/companies/{companyId}/switch`
3. `GET /location-hierarchy`
4. `GET /location-levels`
5. `GET /location-groups/tree`

## OpenAPI note

El contrato estructurado de la API sigue generado por Swagger en runtime desde la metadata de controllers y DTOs. En este repositorio no existe hoy un `docs/technical/api/openapi.yaml` versionado que pueda actualizarse sin introducir una copia parcial o inconsistente.
