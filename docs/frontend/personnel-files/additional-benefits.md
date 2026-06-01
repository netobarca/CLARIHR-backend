# Additional Benefits — Sub‑recurso de compensación

Los **additional benefits** (beneficios adicionales) son las prestaciones extra‑salariales asignadas a un empleado dentro de su archivo de personal (p. ej. seguro complementario, bono de transporte, membresías), con su rango de vigencia y notas.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> **Solo sobre archivos finalizados.** Las escrituras (`POST`/`PUT`/`PATCH`/`DELETE`) requieren un archivo de personal **finalizado** (`Completed`, empleado). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

**Permisos:** `GET` → `PersonnelFiles.Read` · `POST/PUT/PATCH/DELETE` → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`    | `/api/v1/personnel-files/{publicId}/additional-benefits` | Listar los beneficios del archivo |
| `POST`   | `/api/v1/personnel-files/{publicId}/additional-benefits` | Agregar un beneficio |
| `GET`    | `/api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` | Obtener un beneficio por id |
| `PUT`    | `/api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` | Reemplazar un beneficio |
| `PATCH`  | `/api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` | Cambios parciales sobre un beneficio |
| `DELETE` | `/api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` | Eliminar un beneficio |

`{publicId}` = id del archivo de personal padre. `{additionalBenefitPublicId}` = id del beneficio.

---

## `GET` Listar beneficios

`GET /api/v1/personnel-files/{publicId}/additional-benefits`

Devuelve el **array completo** de beneficios del archivo (no paginado, ver [Convenciones §7](./_conventions.md#7-paginación-en-endpoints-de-búsqueda)). Cada ítem trae su propio `concurrencyToken`.

**Respuesta `200`** — array de `AdditionalBenefitResponse`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/additional-benefits" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `POST` Agregar un beneficio

`POST /api/v1/personnel-files/{publicId}/additional-benefits`

Crea un beneficio. **No** lleva `If-Match` (ver [Convenciones §6](./_conventions.md#6-crear-post)).

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `benefitTypeCode` | string (nullable) | no | Código de catálogo: tipo de beneficio. |
| `startDate` | string (date-time, nullable) | no | Inicio de vigencia. |
| `endDate` | string (date-time, nullable) | no | Fin de vigencia (abierto si se omite). |
| `isActive` | boolean | sí | Si el beneficio está activo. |
| `notes` | string (nullable) | no | Notas libres. |

**Respuesta `201`** — `AdditionalBenefitResponse` (+ headers `Location` y `ETag`):

| Campo | Tipo |
|-------|------|
| `additionalBenefitPublicId` | uuid |
| `benefitTypeCode` | string (nullable) |
| `startDate` | string (date-time, nullable) |
| `endDate` | string (date-time, nullable) |
| `isActive` | boolean |
| `notes` | string (nullable) |
| `concurrencyToken` | uuid |

```bash
curl -X POST "$BASE/api/v1/personnel-files/$ID/additional-benefits" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "benefitTypeCode": "SEGURO_VIDA_COMPLEMENTARIO",
    "startDate": "2026-06-01T00:00:00Z",
    "isActive": true,
    "notes": "Cobertura ampliada según política RH-2026"
  }'
```

```jsonc
// 201 Created   Location: /api/v1/personnel-files/{id}/additional-benefits/9c4d...a2   ETag: "d1e2...b3"
{
  "additionalBenefitPublicId": "9c4d...a2",
  "benefitTypeCode": "SEGURO_VIDA_COMPLEMENTARIO",
  "startDate": "2026-06-01T00:00:00Z",
  "endDate": null,
  "isActive": true,
  "notes": "Cobertura ampliada según política RH-2026",
  "concurrencyToken": "d1e2...b3"
}
```

**Errores:** `400` (validación), `409` (conflicto), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `GET` Obtener un beneficio por id

`GET /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` → `200` `AdditionalBenefitResponse` (mismos campos que el `201`). El `concurrencyToken` que devuelve es el que vas a usar en `If-Match` para `PUT`/`PATCH`/`DELETE`.

```bash
curl "$BASE/api/v1/personnel-files/$ID/additional-benefits/$ITEM" \
  -H "Authorization: Bearer $TOKEN"
```

**Errores:** `401`, `403`, `404`.

---

## `PUT` Reemplazar un beneficio

`PUT /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` · **requiere `If-Match`** con el `concurrencyToken` **del beneficio** (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

Reemplaza todos los campos de negocio. Body = mismo shape que el `POST`.

**Respuesta `200`** — `AdditionalBenefitResponse` (con el `concurrencyToken` nuevo + header `ETag`).

```bash
curl -X PUT "$BASE/api/v1/personnel-files/$ID/additional-benefits/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "d1e2...b3"' \
  -d '{
    "benefitTypeCode": "SEGURO_VIDA_COMPLEMENTARIO",
    "startDate": "2026-06-01T00:00:00Z",
    "endDate": "2026-12-31T00:00:00Z",
    "isActive": true,
    "notes": "Renovado hasta fin de año"
  }'
```

**Errores:** `400`, `409` (token desactualizado), `422` (archivo en `Draft` / regla de negocio), `404`.

---

## `PATCH` Cambios parciales

`PATCH /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` · **requiere `If-Match`** · `Content-Type: application/json-patch+json`.

Body = **array desnudo** de operaciones JSON Patch (ver [Convenciones §5](./_conventions.md#5-patch--json-patch-rfc-6902--formato-de-array-desnudo)). Campos parchables: los del body del `POST` (`benefitTypeCode`, `startDate`, `endDate`, `isActive`, `notes`).

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/additional-benefits/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "d1e2...b3"' \
  -d '[
    { "op": "replace", "path": "/isActive", "value": false },
    { "op": "replace", "path": "/endDate", "value": "2026-08-31T00:00:00Z" }
  ]'
```

**Respuesta `200`** — `AdditionalBenefitResponse` (con el `concurrencyToken` nuevo). **Errores:** `400` (patch inválido), `409`, `422`, `404`.

---

## `DELETE` Eliminar un beneficio

`DELETE /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}` · **requiere `If-Match`** con el `concurrencyToken` del beneficio.

**Respuesta `200`** — `{ "parentConcurrencyToken": "..." }` (el token del archivo padre tras quitar el ítem; ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

```bash
curl -X DELETE "$BASE/api/v1/personnel-files/$ID/additional-benefits/$ITEM" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-Match: "d1e2...b3"'
```

```jsonc
// 200 OK
{ "parentConcurrencyToken": "e3f4...c5" }
```

**Errores:** `400` (`If-Match` faltante/malformado), `409` (token desactualizado), `404`.

---

> El `concurrencyToken` cambia con cada escritura exitosa: usá siempre el último (del body o del header `ETag`) para la próxima operación.
