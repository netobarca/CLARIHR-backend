# Finalize — Finalizar el archivo de personal (Draft → Completed)

Operación sobre el **archivo de personal** (no es un sub‑recurso con ítems): transiciona el archivo de `Draft` a `Completed` y, opcionalmente, **provisiona una cuenta de usuario** para la persona (invitación). Incluye además un **preview** (dry‑run) que valida la elegibilidad sin mutar nada.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de esta operación.

**Permisos:** `GET` (preview) → `PersonnelFiles.Read` · `PATCH` (finalizar) → `PersonnelFiles.Manage`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`   | `/api/v1/personnel-files/{publicId}/finalize/preview` | Dry‑run: validar elegibilidad e issues, sin mutar |
| `PATCH` | `/api/v1/personnel-files/{publicId}/finalize` | Finalizar (Draft → Completed) y opcionalmente crear el usuario |

**Path params:** `publicId` (uuid) = archivo de personal (ver [Convenciones §3](./_conventions.md#3-identificadores-publicid)).

> Solo se finalizan archivos de tipo **empleado** en estado `Draft`. Requisitos: `institutionalEmail` presente y una plaza asignada (`assignedPositionSlotPublicId`); si además se va a crear la cuenta, la plaza debe tener rol y el email no puede estar ya enlazado a otro archivo. El preview lista exactamente qué falta antes de intentar el `PATCH`.

---

## `GET` Preview (dry‑run)

`GET /api/v1/personnel-files/{publicId}/finalize/preview`

Devuelve, **sin mutar**, si el archivo es elegible para finalizar y la lista de issues (bloqueantes o no). Útil para habilitar/deshabilitar el botón "Finalizar" y mostrar qué corregir.

**Query params:**

| Param | Tipo | Notas |
|-------|------|-------|
| `createUserAccount` | boolean | Si la validación debe contemplar la creación de la cuenta de usuario. Default `true`. |

**Respuesta `200`** — `FinalizePersonnelFilePreviewResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `personnelFilePublicId` | uuid | Id del archivo evaluado. |
| `createUserAccount` | boolean | Eco del parámetro evaluado. |
| `isEligible` | boolean | `true` si no hay issues bloqueantes (se puede finalizar). |
| `issues` | array de `issue` | Problemas detectados (vacío si todo OK). |

Cada `issue`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `code` | string | Código estable del problema (p. ej. `PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL`). |
| `message` | string | Mensaje legible. |
| `section` | string | Sección lógica del archivo (p. ej. `personnel-file`, `employment`). |
| `fieldKey` | string | Campo afectado (p. ej. `institutionalEmail`, `assignedPositionSlotPublicId`). |
| `navigationKey` | string | Clave de navegación para llevar al usuario a corregirlo (p. ej. `personnel-files`, `personal-info`, `employee-profile`). |
| `isBlocking` | boolean | Si impide finalizar. |

```bash
curl "$BASE/api/v1/personnel-files/$ID/finalize/preview?createUserAccount=true" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK — no elegible: falta el email institucional
{
  "personnelFilePublicId": "3d9e...05",
  "createUserAccount": true,
  "isEligible": false,
  "issues": [
    {
      "code": "PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL",
      "message": "An institutional email is required to finalize the personnel file.",
      "section": "personnel-file",
      "fieldKey": "institutionalEmail",
      "navigationKey": "personnel-files",
      "isBlocking": true
    }
  ]
}
```

**Errores:** `400`, `401`, `403`, `404`.

---

## `PATCH` Finalizar

`PATCH /api/v1/personnel-files/{publicId}/finalize` · **requiere `If-Match`**.

⚠️ A diferencia de los sub‑recursos, el `If-Match` acá lleva el `concurrencyToken` **del propio archivo de personal** (el que devuelve `GET /api/v1/personnel-files/{publicId}`), **no** un token de body ni de un ítem. Es el mismo caso que la mutación del shell (ver [Convenciones §4](./_conventions.md#4-concurrencia-optimista--if-match-importante)).

> Aunque el verbo es `PATCH`, **no** es JSON Patch: el `Content-Type` es `application/json` y el body es un objeto con un único campo opcional. No mandes el formato de array de operaciones.

**Body** (`application/json`):

| Campo | Tipo | Req. | Notas |
|-------|------|------|-------|
| `createUserAccount` | boolean (nullable) | no | Si se debe provisionar la cuenta de usuario al finalizar. **Default `true`** si se omite. Poné `false` para finalizar sin crear usuario. |

**Respuesta `200`** — `FinalizePersonnelFileResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `personnelFile` | object `PersonnelFileResponse` | El archivo ya finalizado (incluye el `concurrencyToken` nuevo y `lifecycleStatus: "Completed"`). |
| `user` | object (nullable) | La cuenta provisionada (`null` si `createUserAccount=false`). Ver tabla abajo. |
| `invitationExpiresUtc` | string (date-time, nullable) | Vencimiento de la invitación cuando se creó la cuenta. |

`user` (`CompanyUserResponse`, cuando aplica):

| Campo | Tipo |
|-------|------|
| `publicId` | uuid |
| `email` | string (nullable) |
| `firstName` / `lastName` | string (nullable) |
| `roles` | array de roles (`publicId` + nombre) |
| `status` | enum string (nullable) |

```bash
curl -X PATCH "$BASE/api/v1/personnel-files/$ID/finalize" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H 'If-Match: "a1b2...c3"' \
  -d '{ "createUserAccount": true }'
```

```jsonc
// 200 OK
{
  "personnelFile": {
    "publicId": "3d9e...05",
    "lifecycleStatus": "Completed",
    "fullName": "Lucía Méndez",
    "linkedUserPublicId": "91cc...34",
    "concurrencyToken": "b2c3...d4"
  },
  "user": {
    "publicId": "91cc...34",
    "email": "lucia.mendez@acme.com",
    "firstName": "Lucía",
    "lastName": "Méndez",
    "roles": [ { "publicId": "7f0a...22", "name": "Analista" } ],
    "status": "Invited"
  },
  "invitationExpiresUtc": "2026-06-07T15:20:00Z"
}
```

**Errores:**
- `400` — `If-Match` faltante/malformado o validación.
- `409` — token desactualizado (`CONCURRENCY_CONFLICT`): recargá el archivo y reintentá.
- `422` — regla de estado/negocio: no es empleado, ya está `Completed`/enlazado, falta `institutionalEmail` o plaza, la plaza no tiene rol, o el email ya está enlazado a otro archivo. Usá el **preview** para conocer el issue exacto antes de reintentar.
- `404` — el archivo no existe en esta compañía.
