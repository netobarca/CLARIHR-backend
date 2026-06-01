# Personnel Files — Family Members

Miembros del grupo familiar asociados a un archivo de personal (cónyuge, hijos, dependientes, beneficiarios, etc.). Cada archivo puede tener varios miembros.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/family-members` | Listar los miembros familiares del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/family-members` | Agregar un miembro familiar |
| `GET`    | `/api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}` | Obtener un miembro por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}` | Reemplazar un miembro |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}` | Cambios parciales |
| `DELETE` | `/api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}` | Quitar un miembro |

**Path params:** `publicId` (uuid) = archivo de personal · `familyMemberPublicId` (uuid) = ítem de miembro familiar.

---

## `GET` Listar

`GET /api/v1/personnel-files/{publicId}/family-members`

Devuelve el array completo (no paginado) de miembros familiares del archivo. Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `FamilyMemberResponse` (ver tabla de campos en el `GET` por id).

```bash
curl "$BASE/api/v1/personnel-files/$ID/family-members" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar

`POST /api/v1/personnel-files/{publicId}/family-members`

No lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)). Responde `201` + headers `Location` y `ETag`.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `firstName` | string | no | Nombres. |
| `lastName` | string | no | Apellidos. |
| `kinshipCode` | string | no | Código de catálogo de parentesco. |
| `nationality` | string | no | |
| `birthDate` | string (date-time) | no | Fecha de nacimiento. |
| `sex` | enum string | no | Sexo del miembro (`PersonnelFamilyMemberSex`). |
| `maritalStatus` | string | no | |
| `occupation` | string | no | Ocupación. |
| `documentType` | string | no | Tipo de documento de identidad. |
| `documentNumber` | string | no | Número de documento. |
| `phone` | string | no | Teléfono. |
| `isStudying` | boolean | no | Indica si estudia. |
| `studyPlace` | string | no | Centro de estudios. |
| `academicLevel` | string | no | Nivel académico. |
| `isBeneficiary` | boolean | no | Marca al miembro como beneficiario. |
| `isWorking` | boolean | no | Indica si trabaja. |
| `workplace` | string | no | Lugar de trabajo. |
| `jobTitle` | string | no | Cargo. |
| `workPhone` | string | no | Teléfono laboral. |
| `salary` | number (double) | no | Salario. |
| `isDeceased` | boolean | no | Indica si falleció. |
| `deceasedDate` | string (date-time) | no | Fecha de fallecimiento. |

**Respuesta `201`** — `FamilyMemberResponse` (ver tabla en el `GET` por id), con header `ETag` = `concurrencyToken` inicial.

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/family-members" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Diego",
    "lastName": "Méndez",
    "kinshipCode": "HIJO_A",
    "nationality": "SV",
    "birthDate": "2015-09-01T00:00:00Z",
    "sex": "Male",
    "isStudying": true,
    "studyPlace": "Colegio San José",
    "academicLevel": "Primaria",
    "isBeneficiary": true,
    "isWorking": false,
    "isDeceased": false
  }'
```

```jsonc
// 201 Created   Location: .../family-members/3f6a...77   ETag: "a1b2...c3"
{
  "familyMemberPublicId": "3f6a...77",
  "firstName": "Diego",
  "lastName": "Méndez",
  "fullName": "Diego Méndez",
  "kinshipCode": "HIJO_A",
  "nationality": "SV",
  "birthDate": "2015-09-01T00:00:00Z",
  "sex": "Male",
  "maritalStatus": null,
  "occupation": null,
  "documentType": null,
  "documentNumber": null,
  "phone": null,
  "isStudying": true,
  "studyPlace": "Colegio San José",
  "academicLevel": "Primaria",
  "isBeneficiary": true,
  "isWorking": false,
  "workplace": null,
  "jobTitle": null,
  "workPhone": null,
  "salary": null,
  "isDeceased": false,
  "deceasedDate": null,
  "concurrencyToken": "a1b2...c3"
}
```

**Errores:** `400` (validación), `404`, `409`, `422` (regla de negocio).

---

## `GET` Obtener por id

`GET /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`

**Respuesta `200`** — `FamilyMemberResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `familyMemberPublicId` | uuid | Id del ítem. |
| `firstName` | string (nullable) | |
| `lastName` | string (nullable) | |
| `fullName` | string (nullable) | Nombre completo compuesto (solo lectura). |
| `kinshipCode` | string (nullable) | Código de catálogo de parentesco. |
| `nationality` | string (nullable) | |
| `birthDate` | string (date-time, nullable) | |
| `sex` | enum string | `PersonnelFamilyMemberSex`. |
| `maritalStatus` | string (nullable) | |
| `occupation` | string (nullable) | |
| `documentType` | string (nullable) | |
| `documentNumber` | string (nullable) | |
| `phone` | string (nullable) | |
| `isStudying` | boolean | |
| `studyPlace` | string (nullable) | |
| `academicLevel` | string (nullable) | |
| `isBeneficiary` | boolean | |
| `isWorking` | boolean | |
| `workplace` | string (nullable) | |
| `jobTitle` | string (nullable) | |
| `workPhone` | string (nullable) | |
| `salary` | number (double, nullable) | |
| `isDeceased` | boolean | |
| `deceasedDate` | string (date-time, nullable) | |
| `concurrencyToken` | uuid | Token para `If-Match` en `PUT`/`PATCH`/`DELETE`. |

```bash
curl "$BASE/api/v1/personnel-files/$ID/family-members/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar

`PUT /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

Reemplaza todos los campos de negocio del ítem. Body = mismo shape que el `POST`. Nota: `fullName` es solo lectura (se compone de `firstName` + `lastName`); no se envía en el body.

**Respuesta `200`** — `FamilyMemberResponse` con el `concurrencyToken` nuevo (también en `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/family-members/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{
    "firstName": "Diego Andrés",
    "lastName": "Méndez",
    "kinshipCode": "HIJO_A",
    "nationality": "SV",
    "birthDate": "2015-09-01T00:00:00Z",
    "sex": "Male",
    "isStudying": true,
    "studyPlace": "Colegio San José",
    "academicLevel": "Primaria",
    "isBeneficiary": true,
    "isWorking": false,
    "isDeceased": false
  }'
```

**Errores:** `400`, `404`, `409` (token desactualizado), `422`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Paths parchables = los campos del body del `POST` (`/firstName`, `/lastName`, `/kinshipCode`, `/nationality`, `/birthDate`, `/sex`, `/maritalStatus`, `/occupation`, `/documentType`, `/documentNumber`, `/phone`, `/isStudying`, `/studyPlace`, `/academicLevel`, `/isBeneficiary`, `/isWorking`, `/workplace`, `/jobTitle`, `/workPhone`, `/salary`, `/isDeceased`, `/deceasedDate`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/family-members/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '[
    { "op": "replace", "path": "/isBeneficiary", "value": false },
    { "op": "replace", "path": "/academicLevel", "value": "Secundaria" }
  ]'
```

**Respuesta `200`** — `FamilyMemberResponse` con el `concurrencyToken` nuevo. **Errores:** `400` (patch inválido), `404`, `409`, `422`.

---

## `DELETE` Quitar

`DELETE /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del ítem.

**Respuesta `200`** — devuelve el token del archivo padre tras quitar el ítem:

```jsonc
{ "parentConcurrencyToken": "f4e5...d6" }
```

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/family-members/$ITEM_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "a1b2...c3"'
```

**Errores:** `400`, `404`, `409` (token desactualizado).
