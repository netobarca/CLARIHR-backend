# Preferencias (Usuario + Compañía) — Guía de consumo (frontend) · Fase 3

> **Prerequisitos:** [Autenticación](../auth/authentication.md) (Fase 1) y
> [Account Companies](../account-companies/account-companies.md) (Fase 2 — las preferencias de
> compañía solo funcionan sobre la **compañía activa** del switch).
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## Overview

Dos controladores, **7 endpoints**:

| Controlador (tag) | Base | Endpoints | Modelo de authz |
|---|---|---|---|
| **User Preferences** | `/api/v1/account/me/preferences` | `GET` · `PUT` (idioma) · `PUT /social-links` · `PATCH` | self-scoped (solo JWT — sin RBAC, sin 403/404) |
| **Company Preferences** | `/api/v1/companies/{companyPublicId}/preferences` | `GET` · `PUT` · `PATCH` | **RBAC + tenant activo** |

Qué guarda cada una:

- **Usuario**: `language` (i18n del usuario, default `"en"`, se guarda en minúsculas) y
  `socialLinks` (hasta 10 links `providerCode`+`url`).
- **Compañía**: `currencyCode` (ISO 4217, se guarda en MAYÚSCULAS) y `timeZone` (string, usar
  IANA, ej. `America/Tegucigalpa`).

### Cuándo consumirlas

- **User preferences**: justo después del login/confirm — `GET` para fijar el idioma de la UI.
  El registro la auto-provisiona con `language: "en"`; el `GET` la crea si no existe (nunca 404).
- **Company preferences**: después del `switch` — `GET` para moneda/zona horaria que formatean
  montos y fechas del dashboard. Solo funciona si `{companyPublicId}` **es la compañía activa**.

### Convenciones compartidas de esta fase

- **Toda escritura lleva `If-Match: "<concurrencyToken>"`** (el token viene en el body de cada
  respuesta y en el header `ETag`). Faltante/malformado → `400`; stale → `409
  CONCURRENCY_CONFLICT` (recargar con `GET` y reintentar). Cada escritura exitosa rota el token.
- **PATCH = JSON Patch RFC 6902**, `Content-Type: application/json-patch+json`, body = **array
  desnudo** de operaciones (máx 50 ops / 64 KB). El esquema `{operations:[...]}` del Swagger es
  engañoso (convención de toda la API).
- Sin rate limits específicos en esta familia.
- Idioma de la UI ≠ idioma de los errores del backend: los `title`/`detail` de ProblemDetails se
  localizan por el header `Accept-Language` de cada request — alineá ese header con el
  `language` guardado.

---

# A. User Preferences (`/api/v1/account/me/preferences`)

Recurso **singleton del usuario autenticado** (se resuelve del JWT — no hay id en la ruta). No
hay RBAC ni ownership que validar: cualquier usuario autenticado lee/escribe **lo suyo**. `403`
y `404` no ocurren (auto-provisión en el primer acceso; una carrera de doble-provisión se
resuelve sola del lado servidor).

## 1. Obtener mis preferencias

### Endpoint
`GET /api/v1/account/me/preferences`

### Description
Devuelve idioma y social links del usuario autenticado. Auto-provisiona el registro si no existe
(primer acceso) — siempre responde `200` para un caller autenticado.

### Authentication
Bearer requerido.

### Authorization
Solo autenticación (self-scoped).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
Ninguno.

### Query Parameters
Ninguno.

### Request Body
N/A.

### Responses
`200`:

```json
{
  "publicId": "…",
  "language": "es",
  "socialLinks": [
    { "providerCode": "linkedin", "url": "https://www.linkedin.com/in/ana-garcia" }
  ],
  "concurrencyToken": "8f3a1c2e-…",
  "createdAtUtc": "…",
  "modifiedAtUtc": null
}
```

`401` ProblemDetails.

### Business Rules
- Default al crear la cuenta: `language: "en"`, `socialLinks: []`.
- Guardá el `concurrencyToken` para la primera escritura.

### Validation Rules
N/A.

### Security Considerations
Ninguna especial — no expone datos de otros usuarios.

---

## 2. Reemplazar el idioma (PUT)

### Endpoint
`PUT /api/v1/account/me/preferences`

### Description
Reemplaza la preferencia de idioma (único campo del PUT — los social links van por su propio
endpoint).

### Authentication / Authorization
Bearer / self-scoped.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` |

### Path / Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `language` | string | Sí | `^[A-Za-z]{2,3}$` (código ISO 639, ej. `es`, `en`); el servidor lo guarda en minúsculas |

```json
{ "language": "es" }
```

### Responses
| Status | Body | Cuándo |
|--------|------|--------|
| `200` | `UserPreferenceResponse` + `ETag` nuevo | reemplazado |
| `400` | `common.validation` | idioma inválido / `If-Match` faltante |
| `401` | ProblemDetails | sin token |
| `409` | `CONCURRENCY_CONFLICT` | token stale |

### Business Rules
Aplicá el cambio de idioma en la UI inmediatamente y actualizá el `Accept-Language` que mandás
al backend.

### Validation Rules
Tabla del body.

### Security Considerations
N/A.

---

## 3. Reemplazar social links (PUT)

### Endpoint
`PUT /api/v1/account/me/preferences/social-links`

### Description
Reemplaza el **set completo** de social links (semántica replace-all: lo que no venga en `items`
se borra; `items: []` los borra todos — es la forma de "limpiar").

### Authentication / Authorization
Bearer / self-scoped.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` |

### Path / Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `items` | array | Sí (puede ser `[]`) | máx **10** ítems |
| `items[].providerCode` | string | Sí | máx 50; `^[A-Za-z0-9_.-]+$`; **únicos** entre sí (case-insensitive) |
| `items[].url` | string | Sí | máx 500; URL **absoluta https** |

```json
{
  "items": [
    { "providerCode": "linkedin", "url": "https://www.linkedin.com/in/ana-garcia" },
    { "providerCode": "github",   "url": "https://github.com/anagarcia" }
  ]
}
```

### Responses
Igual que el PUT de idioma (`200` + `ETag` / `400` / `401` / `409 CONCURRENCY_CONFLICT`).

### Business Rules
- `providerCode` es **texto libre con formato** (no hay catálogo de proveedores): el FE define
  sus códigos (`linkedin`, `github`, `x`, …) y los reutiliza de forma consistente para poder
  mapear íconos.
- Para editar/quitar uno: mandá el array completo resultante (no hay PATCH de links).

### Validation Rules
Tabla del body (la unicidad compara trim + case-insensitive).

### Security Considerations
`http://` se rechaza — solo `https://` absolutas.

---

## 4. Patch de preferencias (idioma)

### Endpoint
`PATCH /api/v1/account/me/preferences`

### Description
JSON Patch del singleton. Único path: **`/language`** (requerido, no removible). Los social
links NO se patchean (usar el PUT de §3).

### Authentication / Authorization
Bearer / self-scoped.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | **`application/json-patch+json`** |
| `If-Match` | Sí | `"<concurrencyToken>"` |

### Path / Query Parameters
Ninguno.

### Request Body
Array desnudo RFC 6902:

```json
[ { "op": "replace", "path": "/language", "value": "es" } ]
```

### Responses
Igual que el PUT (`200`/`400`/`401`/`409`).

### Business Rules
Con un solo campo patchable, `PUT` y `PATCH` son equivalentes — elegí uno y sé consistente.

### Validation Rules
`/language` mismas reglas que el PUT; paths desconocidos o `remove` de `/language` → `400`.

---

# B. Company Preferences (`/api/v1/companies/{companyPublicId}/preferences`)

Singleton **por compañía**, se aprovisiona junto con la compañía. A diferencia de todo lo
anterior, acá hay **RBAC real + tenant**:

| Operación | Requiere (cualquiera de) |
|-----------|--------------------------|
| `GET` | `CompanyPreferences.Read` · `CompanyPreferences.Admin` · `iam.administration.manage` |
| `PUT` / `PATCH` | `CompanyPreferences.Admin` · `iam.administration.manage` |

Además, `{companyPublicId}` **debe ser la compañía activa de la sesión** (la del último
`switch`); si no coincide → **`403 TENANT_MISMATCH`**. Verificá los permisos del usuario contra
`currentUserAccess.permissions` del access-context para mostrar/ocultar la pantalla.

## 5. Obtener preferencias de la compañía

### Endpoint
`GET /api/v1/companies/{companyPublicId}/preferences`

### Description
Moneda y zona horaria de la compañía + `concurrencyToken` para editar.

### Authentication
Bearer requerido (con el tenant de esta compañía en el JWT).

### Authorization
`CompanyPreferences.Read` (o `Admin` / `iam.administration.manage`) + tenant activo = la compañía.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la compañía **activa** |

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200`:

```json
{
  "publicId": "…",
  "currencyCode": "HNL",
  "timeZone": "America/Tegucigalpa",
  "concurrencyToken": "8f3a1c2e-…",
  "createdAtUtc": "…",
  "modifiedAtUtc": null
}
```

| Status | `code` | Cuándo |
|--------|--------|--------|
| `401` | — | sin token |
| `403` | `TENANT_MISMATCH` | la compañía de la URL no es el tenant activo (trae `details[{resourceKey:"COMPANY_PREFERENCES", action}]`) |
| `403` | `COMPANY_PREFERENCES_FORBIDDEN` | tenant correcto pero sin permiso |
| `404` | `COMPANY_PREFERENCE_NOT_FOUND` | registro faltante (anómalo — se crea con la compañía) |

### Business Rules
Usá `currencyCode`/`timeZone` para formatear montos y fechas de toda la app de esa compañía;
recargalas tras cada `switch`.

### Validation Rules / Security Considerations
Distinguí en UX el `403 TENANT_MISMATCH` (bug de routing del FE — pedís una compañía que no es
la activa) del `403 COMPANY_PREFERENCES_FORBIDDEN` (falta de permiso real).

---

## 6. Reemplazar preferencias de la compañía (PUT)

### Endpoint
`PUT /api/v1/companies/{companyPublicId}/preferences`

### Description
Reemplaza los dos campos editables: `currencyCode` y `timeZone`.

### Authentication / Authorization
Bearer / `CompanyPreferences.Admin` o `iam.administration.manage` + tenant activo.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` |

### Path Parameters
`companyPublicId` (uuid, la activa).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `currencyCode` | string | Sí | **exactamente 3 caracteres** (tras trim); el servidor guarda MAYÚSCULAS — mandá ISO 4217 (`HNL`, `USD`) |
| `timeZone` | string | Sí | máx 100; texto libre — usá identificadores IANA |

```json
{ "currencyCode": "USD", "timeZone": "America/Tegucigalpa" }
```

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `CompanyPreferenceResponse` + `ETag` nuevo | reemplazado |
| `400` | `common.validation` | validación / If-Match faltante |
| `401` / `403` / `404` | (ver GET) | token / tenant·permiso / registro |
| `409` | `CONCURRENCY_CONFLICT` | token stale |

### Business Rules
- El backend **no** valida que la moneda exista en ISO 4217 ni que la zona sea IANA válida —
  restringí el form a dropdowns curados (catálogo de monedas/zonas del FE).
- Cambiar la moneda **no** convierte montos existentes; es solo formato/etiqueta. Confirmá con
  el usuario antes de guardar.

### Validation Rules
Tabla del body.

### Security Considerations
Pantalla solo para admins (gateá por el permiso en access-context).

---

## 7. Patch de preferencias de la compañía

### Endpoint
`PATCH /api/v1/companies/{companyPublicId}/preferences`

### Description
JSON Patch del singleton. Paths: **`/currencyCode`** (exactamente 3 caracteres) y
**`/timeZone`** (máx 100). Ambos requeridos — `remove` no está permitido.

### Authentication / Authorization / Headers
Igual que el PUT, pero `Content-Type: application/json-patch+json`.

### Path Parameters
`companyPublicId` (uuid, la activa).

### Query Parameters
Ninguno.

### Request Body
Array desnudo RFC 6902 (máx 50 ops / 64 KB):

```json
[ { "op": "replace", "path": "/timeZone", "value": "America/Mexico_City" } ]
```

### Responses
Igual que el PUT (`200`/`400`/`401`/`403`/`404`/`409`).

### Business Rules
Útil para cambiar un solo campo sin re-enviar el otro.

### Validation Rules
Mismas del PUT por campo; paths desconocidos / `remove` → `400`.

---

# Referencia compartida

## Catálogo de códigos de error de la fase

| `code` | HTTP | Endpoint(s) | Acción FE |
|--------|------|-------------|-----------|
| `common.validation` | 400 | todos los writes | errores por campo (keys camelCase, anidadas tipo `items[0].url`) |
| `CONCURRENCY_CONFLICT` | 409 | todos los writes | `GET` fresco + reintentar con el token nuevo |
| `TENANT_MISMATCH` | 403 | company preferences | la URL no apunta a la compañía activa — corregir routing / hacer switch |
| `COMPANY_PREFERENCES_FORBIDDEN` | 403 | company preferences | ocultar la pantalla (sin permiso) |
| `COMPANY_PREFERENCE_NOT_FOUND` | 404 | company preferences | estado anómalo — soporte |
| `USER_PREFERENCE_NOT_FOUND` | 404 | user preferences | teóricamente inalcanzable (auto-provisión) |
| `INVALID_CURRENT_USER` | 401 | user preferences | re-login |

## Resumen de reglas por campo

| Campo | Regla |
|-------|-------|
| `language` | 2–3 letras (`^[A-Za-z]{2,3}$`), guardado en minúsculas, default `en` |
| `socialLinks` | máx 10; `providerCode` máx 50 `^[A-Za-z0-9_.-]+$` únicos; `url` https absoluta máx 500; replace-all (`[]` limpia) |
| `currencyCode` | exactamente 3 caracteres, guardado en MAYÚSCULAS (usar ISO 4217) |
| `timeZone` | máx 100, texto libre (usar IANA) |

## Guía de implementación del cliente

1. **Post-login**: `GET /account/me/preferences` → setear i18n de la UI con `language` y mandar
   `Accept-Language` acorde en requests siguientes.
2. **Post-switch**: `GET /companies/{activa}/preferences` → registrar `currencyCode`/`timeZone`
   en el estado tenant-scoped (e invalidarlo al cambiar de compañía).
3. **Escrituras**: patrón único de la API — guardar `concurrencyToken` → `If-Match` → reemplazar
   el token con el de la respuesta; ante `409` recargar y reintentar.
4. **Gating**: la pantalla de preferencias de compañía se muestra solo si el access-context trae
   `CompanyPreferences.Read`+; el botón guardar solo con `CompanyPreferences.Admin`+.

## Próximas fases (orden sugerido)

1. **Company Users** — invitar/administrar usuarios (`CompanyUsersController`; el invitado
   cierra el ciclo con `auth/company-user-invitations/accept` de la Fase 1).
2. **Roles y permisos (IAM)** — `AccountCompanyAuthorizationController` + role-builder-catalog.
3. **Suscripción** — `AccountCompanySubscriptionsController`.
4. **General Catalogs** y módulos de negocio (Org, Personnel Files, …).
