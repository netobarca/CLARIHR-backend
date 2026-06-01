# Personnel Files — Employee Relations (parentescos entre empleados)

Las **employee relations** registran el vínculo de parentesco/relación entre la persona del archivo y **otro empleado** de la compañía (ej. cónyuge, hermano/a). El otro empleado se referencia por su `publicId`. Cuelgan de un archivo ya existente y funcionan sobre **cualquier** archivo, esté en `Draft` o `Completed` (no están sujetas a la finalización del empleado).

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/employee-relations` | Listar las relaciones del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/employee-relations` | Agregar una relación |
| `GET`    | `/api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` | Obtener una relación por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` | Reemplazar una relación |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` | Cambios parciales sobre una relación |
| `DELETE` | `/api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` | Eliminar una relación |

`{publicId}` = id del archivo de personal. `{employeeRelationPublicId}` = id de la relación (GUID; ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

---

## Campos del recurso

**Body de `POST`/`PUT`** (`AddEmployeeRelationRequest`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `relatedEmployeePublicId` | uuid | sí | `publicId` del otro empleado (archivo de personal) con quien existe el vínculo. |
| `relationship` | string | no | Tipo de parentesco/relación (ej. `Cónyuge`, `Hermano/a`). |

**Respuesta** (`EmployeeRelationResponse`):

| Campo | Tipo | Notas |
|-------|------|-------|
| `employeeRelationPublicId` | uuid | |
| `relatedEmployeePublicId` | uuid | |
| `relatedEmployeeFullName` | string (nullable) | Nombre completo resuelto del empleado relacionado (solo lectura). |
| `relationship` | string (nullable) | |
| `concurrencyToken` | uuid | |

---

## `GET` Listar relaciones

`GET /api/v1/personnel-files/{publicId}/employee-relations`

Devuelve el **array completo** de relaciones del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)).

```bash
curl "$BASE/api/v1/personnel-files/$ID/employee-relations" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
[
  {
    "employeeRelationPublicId": "d4e5f607-1a2b-4c3d-8e9f-0a1b2c3d4e5f",
    "relatedEmployeePublicId": "91cc3402-5e6f-4071-82a3-b4c5d6e7f809",
    "relatedEmployeeFullName": "Carlos Méndez",
    "relationship": "Hermano/a",
    "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
  }
]
```

**Errores:** `401`, `403`, `404` (archivo inexistente o de otra compañía).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` → `200` `EmployeeRelationResponse`. El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE` de este ítem.

```bash
curl "$BASE/api/v1/personnel-files/$ID/employee-relations/$REL" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar una relación

`POST /api/v1/personnel-files/{publicId}/employee-relations` · **sin `If-Match`** (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`): ver la [tabla de campos](#campos-del-recurso).

**Respuesta `201`** — `EmployeeRelationResponse` (+ headers `Location` y `ETag` con el `concurrencyToken` inicial).

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/employee-relations" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "relatedEmployeePublicId": "91cc3402-5e6f-4071-82a3-b4c5d6e7f809",
    "relationship": "Hermano/a"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{publicId}/employee-relations/d4e5...4e5f   ETag: "a1b2...f809"
{
  "employeeRelationPublicId": "d4e5f607-1a2b-4c3d-8e9f-0a1b2c3d4e5f",
  "relatedEmployeePublicId": "91cc3402-5e6f-4071-82a3-b4c5d6e7f809",
  "relatedEmployeeFullName": "Carlos Méndez",
  "relationship": "Hermano/a",
  "concurrencyToken": "a1b2c3d4-5e6f-7081-92a3-b4c5d6e7f809"
}
```

**Errores:** `400` (validación; ej. `relatedEmployeePublicId` faltante), `401`, `403`, `404`, `409`, `422`.

---

## `PUT` Reemplazar una relación

`PUT /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza **todos** los campos de negocio del ítem. Body = mismo shape que el `POST`.

**Respuesta `200`** — `EmployeeRelationResponse` con el `concurrencyToken` **nuevo** (también en el header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/employee-relations/$REL" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '{
    "relatedEmployeePublicId": "91cc3402-5e6f-4071-82a3-b4c5d6e7f809",
    "relationship": "Cónyuge"
  }'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (RFC 6902), **sin** envoltorio `operations` (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: `relatedEmployeePublicId`, `relationship`.

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/employee-relations/$REL" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...f809"' \
  -d '[
    { "op": "replace", "path": "/relationship", "value": "Cónyuge" }
  ]'
```

**Respuesta `200`** — `EmployeeRelationResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `401`, `403`, `404`, `409`, `422`.

---

## `DELETE` Eliminar una relación

`DELETE /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del **archivo padre** tras quitar el ítem (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)):

```jsonc
// 200 OK
{ "parentConcurrencyToken": "f0e1d2c3-b4a5-6978-8a9b-0c1d2e3f4a5b" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/employee-relations/$REL" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...f809"'
```

**Errores:** `400`, `401`, `403`, `404`, `409` (token desactualizado).
