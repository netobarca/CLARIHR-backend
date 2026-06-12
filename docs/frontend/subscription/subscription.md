# Suscripción (Plan + Add-ons) — Guía de consumo (frontend) · Fase 6

> **Prerequisitos:** [Account Companies](../account-companies/account-companies.md) (Fase 2 — misma
> autorización por **ownership**, mismo `{companyPublicId}`). El plan/add-ons que se administran acá
> definen qué módulos aparecen en el **access-context** (Fase 2 §13) y en el **role-builder-catalog**
> (Fase 5).
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## Overview

Un controlador (**Account Subscription**), **8 endpoints**, base
**`/api/v1/account/companies/{companyPublicId}/subscription`**. Es la administración self-service de
la suscripción de una compañía: ver/cambiar el plan y activar/desactivar add-ons, con **preview**
del impacto antes de aplicar.

| Método | Ruta (relativa a la base) | Para qué |
|--------|---------------------------|----------|
| `GET` | `/subscription` | overview: plan actual + add-ons activos + módulos efectivos |
| `GET` | `/subscription/plans` | planes a los que se puede cambiar (con `isCurrent`) |
| `POST` | `/subscription/preview` | **simular** cambio de plan (sin aplicar) |
| `PUT` | `/subscription` | **aplicar** cambio de plan |
| `GET` | `/subscription/addons` | add-ons actualmente activos |
| `GET` | `/subscription/addons/marketplace` | add-ons disponibles para adquirir |
| `POST` | `/subscription/addons/preview` | **simular** activar/desactivar un add-on |
| `POST` | `/subscription/addons` | **aplicar** activar/desactivar un add-on |

### Conceptos clave (leer primero)

- **Autorización por ownership** (igual que AccountCompanies, NO RBAC): solo el **dueño** de la
  compañía (`CreatedByUserPublicId == subject del JWT`) puede ver/cambiar la suscripción. Un
  no-dueño → `403 COMPANY_OWNERSHIP_FORBIDDEN`; compañía inexistente → `404 COMPANY_NOT_FOUND`.
  Esto **no** depende de permisos RBAC ni de la compañía activa: aplica a cualquier compañía propia.
- **Patrón preview → apply**: los `POST …/preview` y `POST …/addons/preview` **no** modifican nada
  (son consultas con body); calculan módulos agregados/quitados, advertencias y elegibilidad. Recién
  `PUT /subscription` (plan) y `POST /subscription/addons` (add-on) aplican el cambio. **Mostrá
  siempre el preview antes de aplicar.**
- **Concurrencia optimista**: el overview trae un `concurrencyToken` (body + header `ETag`). Los dos
  endpoints que aplican (`PUT /subscription`, `POST /subscription/addons`) requieren
  `If-Match: "<concurrencyToken>"` (faltante → `400`; stale → `409 CONCURRENCY_CONFLICT`). El preview
  y los GET no lo llevan.
- **El cambio de plan es inmediato**: cancela la suscripción actual y crea la nueva en una sola
  transacción; la respuesta es el overview resultante (no hace falta re-fetch). El cambio de add-on
  es **reversible** (Activate/Deactivate).
- **Efecto en el resto de la app**: cambiar plan o add-ons cambia `effectiveModules` → **invalidá el
  caché de gating** (access-context, role-builder-catalog, menú) después de aplicar.
- **MASTER es un plan reservado** para operadores de plataforma: no aparece en `/plans` para un
  dueño normal, y cambiarse a él da `403`.

### Flujo típico

```
GET /subscription            → overview (plan actual + concurrencyToken)
GET /subscription/plans      → opciones (isCurrent marca el actual)
POST /subscription/preview {commercialPlanPublicId}  → ver impacto (módulos ±, isEligible)
  └─ si isEligible:
       PUT /subscription  If-Match {commercialPlanPublicId, observations}  → aplicar
       → invalidar gating (access-context / role-builder-catalog)

GET /subscription/addons/marketplace   → add-ons (isOwned / canAcquire / blockedReason)
POST /subscription/addons/preview {commercialAddonPublicId, action}  → impacto
POST /subscription/addons   If-Match {commercialAddonPublicId, action, observations}  → aplicar
```

---

# Endpoints

## 1. Overview de la suscripción

### Endpoint
`GET …/subscription`

### Description
Plan actual, add-ons activos y los módulos efectivos (plan + add-ons). Trae el `concurrencyToken`
para los cambios.

### Authentication
Bearer requerido.

### Authorization
Ownership de la compañía.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la compañía (propia) |

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200` (+ header `ETag: "<guid>"`):

```json
{
  "companyPublicId": "…",
  "companyName": "Mi Empresa S.A.",
  "companySlug": "mi-empresa-sa",
  "planCode": "FREE",
  "currentPlan": {
    "commercialPlanPublicId": "…", "code": "FREE", "name": "Free",
    "baseMonthlyFee": 0, "pricePerActiveEmployee": 0, "currencyCode": "USD",
    "moduleCount": 3, "moduleKeys": ["ORG", "PERSONNEL", "RBAC"], "isCurrent": true
  },
  "activeAddons": [],
  "effectiveModules": [{ "moduleKey": "ORG", "displayName": "Organización", "grantedByPlan": true, "grantedByAddon": false }],
  "concurrencyToken": "8f3a1c2e-…"
}
```

| Status | `code` | Cuándo |
|--------|--------|--------|
| `401` / `403` | — / `COMPANY_OWNERSHIP_FORBIDDEN` | token / no es tu compañía |
| `404` | `COMPANY_NOT_FOUND` | compañía inexistente |
| `404` | `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND` | sin suscripción activa |

### Business Rules
`effectiveModules` es la fuente de qué módulos están habilitados — la misma señal que usás para el
gating del menú (junto a los permisos del access-context).

### Validation Rules / Security Considerations
Guardá el `concurrencyToken` para `PUT /subscription` o `POST /subscription/addons`.

---

## 2. Planes disponibles

### Endpoint
`GET …/subscription/plans`

### Description
Planes comerciales a los que el dueño puede cambiarse, marcando el actual (`isCurrent`).

### Authentication / Authorization
Bearer / ownership.

### Request Headers
`Authorization` (req).

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200` — array de `AccountCompanySubscriptionPlanResponse`:

```json
[{
  "commercialPlanPublicId": "…", "code": "PRO", "name": "Pro",
  "description": "Todos los módulos core + nómina avanzada",
  "baseMonthlyFee": 49.0, "pricePerActiveEmployee": 2.5,
  "currentVersionNumber": 3, "currencyCode": "USD",
  "moduleCount": 8, "moduleKeys": ["ORG","PERSONNEL","RBAC","PAYROLL","…"],
  "isCurrent": false
}]
```

`401` / `403` / `404` ProblemDetails.

### Business Rules
- El plan **MASTER** no aparece para un dueño normal (reservado a plataforma).
- `baseMonthlyFee` + `pricePerActiveEmployee` son la estructura de precio — mostrala junto a
  `moduleKeys` para comparar planes.

### Validation Rules / Security Considerations
El `commercialPlanPublicId` elegido va en el preview/cambio de plan.

---

## 3. Preview de cambio de plan

### Endpoint
`POST …/subscription/preview`

### Description
Calcula el impacto de cambiar al plan objetivo (módulos agregados/quitados, advertencias de
add-ons, elegibilidad) **sin aplicar**. Es una consulta expuesta sobre POST porque lleva body.

### Authentication / Authorization
Bearer / ownership.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `commercialPlanPublicId` | uuid | Sí | plan objetivo (de `/plans`) |

```json
{ "commercialPlanPublicId": "a1b2c3d4-…" }
```

### Responses
`200` `AccountCompanySubscriptionPlanPreviewResponse`:

```json
{
  "companyPublicId": "…",
  "currentPlan": { "code": "FREE", "…": "…" },
  "targetPlan": { "code": "PRO", "…": "…" },
  "addedModuleKeys": ["PAYROLL", "REPORTS"],
  "removedModuleKeys": [],
  "addonDeactivationWarnings": [],
  "isEligible": true,
  "ineligibilityReasons": []
}
```

| Status | `code` | Cuándo |
|--------|--------|--------|
| `400` | `common.validation` | body inválido |
| `401` / `403` | — / `COMPANY_OWNERSHIP_FORBIDDEN` / `ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN` | token / ownership / MASTER reservado |
| `404` | `COMPANY_NOT_FOUND` / plan inexistente | compañía o plan no encontrado |

### Business Rules
- **Mostrá `addedModuleKeys`/`removedModuleKeys`** para que el usuario vea qué gana/pierde.
- `addonDeactivationWarnings`: bajar de plan puede forzar desactivar add-ons — avisalo.
- Si `isEligible: false`, mostrá `ineligibilityReasons` y **deshabilitá el botón aplicar**.

### Validation Rules / Security Considerations
El preview no muta nada; es seguro llamarlo en cada selección del usuario.

---

## 4. Aplicar cambio de plan (PUT)

### Endpoint
`PUT …/subscription`

### Description
Aplica el cambio de plan de forma inmediata: cancela la suscripción actual y crea la nueva en una
transacción, devolviendo el overview resultante. Bajar a FREE desactiva los add-ons activos.

### Authentication / Authorization
Bearer / ownership.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` (del overview) |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `commercialPlanPublicId` | uuid | Sí | plan objetivo |
| `observations` | string | No | máx 2000 |

```json
{ "commercialPlanPublicId": "a1b2c3d4-…", "observations": "Upgrade para habilitar nómina" }
```

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `AccountCompanySubscriptionOverviewResponse` + `ETag` nuevo | aplicado |
| `400` | `common.validation` | validación / If-Match faltante |
| `403` | `COMPANY_OWNERSHIP_FORBIDDEN` / `ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN` | ownership / MASTER |
| `404` | `COMPANY_NOT_FOUND` / `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND` / plan inexistente | |
| `409` | (motivo de inelegibilidad) | el cambio no es elegible (lo que el preview marcó con `ineligibilityReasons`) |
| `409` | `CONCURRENCY_CONFLICT` | token stale → re-`GET` overview y reintentar |

### Business Rules
- **Idealmente solo se llega acá si el preview dio `isEligible: true`**; si igual no es elegible, el
  backend lo rechaza con el motivo.
- Devuelve el overview nuevo: actualizá el estado local con él (no re-fetchees) y **rota el
  `concurrencyToken`**.
- Tras aplicar, **invalidá el gating** (access-context / role-builder-catalog / menú) porque los
  módulos efectivos cambiaron.

### Validation Rules
Tabla del body.

### Security Considerations
Confirmación explícita al bajar de plan (puede desactivar add-ons y quitar módulos en uso).

---

## 5. Add-ons activos

### Endpoint
`GET …/subscription/addons`

### Description
Add-ons actualmente activos en la suscripción, enriquecidos con sus módulos.

### Authentication / Authorization
Bearer / ownership.

### Request Headers
`Authorization` (req).

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200` — array de `AccountCompanySubscriptionAddonResponse`:

```json
[{
  "companyAddonPublicId": "…", "commercialAddonPublicId": "…",
  "code": "ATS", "name": "Reclutamiento", "description": "…",
  "type": "Specialized", "billingModel": "PerSeat", "measurementUnit": "asiento",
  "unitPrice": 5.0, "minimumQuantity": 1, "minimumMonthlyFee": 20.0, "periodicity": "Monthly",
  "status": "Active", "moduleCount": 2, "moduleKeys": ["ATS","ONBOARDING"]
}]
```

`401` / `403` / `404` ProblemDetails.

### Business Rules
- `companyAddonPublicId` = la instancia del add-on en esta compañía; `commercialAddonPublicId` = el
  add-on del catálogo (es el que va en preview/apply).
- `status` (`CompanyAddonStatus`) puede ser `Active`, `PendingActivation`, `PendingDeactivation` o
  `Inactive` — mostrá los estados transitorios.

### Validation Rules / Security Considerations
N/A.

---

## 6. Marketplace de add-ons

### Endpoint
`GET …/subscription/addons/marketplace`

### Description
Catálogo de add-ons adquiribles, marcando los ya poseídos (`isOwned`) y los adquiribles
(`canAcquire`, con `blockedReason` si no).

### Authentication / Authorization
Bearer / ownership.

### Request Headers
`Authorization` (req).

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters / Request Body
Ninguno / N/A.

### Responses
`200` — array de `AccountCompanyMarketplaceAddonResponse`:

```json
[{
  "commercialAddonPublicId": "…", "code": "ATS", "name": "Reclutamiento", "description": "…",
  "type": "Specialized", "billingModel": "PerSeat", "measurementUnit": "asiento",
  "unitPrice": 5.0, "minimumQuantity": 1, "minimumMonthlyFee": 20.0, "periodicity": "Monthly",
  "moduleCount": 2, "moduleKeys": ["ATS","ONBOARDING"],
  "isOwned": false, "canAcquire": false,
  "blockedReason": "Upgrade desde el plan FREE para adquirir add-ons"
}]
```

`401` / `403` / `404` ProblemDetails.

### Business Rules
- `isOwned: true` → ya activo (no ofrecer "adquirir").
- `canAcquire: false` + `blockedReason` → típicamente una suscripción FREE debe **subir de plan
  primero**; mostrá el `blockedReason` y un CTA a cambiar de plan.
- Solo los `canAcquire: true` habilitan el flujo de activar.

### Validation Rules / Security Considerations
N/A.

---

## 7. Preview de cambio de add-on

### Endpoint
`POST …/subscription/addons/preview`

### Description
Calcula el impacto de activar o desactivar un add-on (módulos agregados/quitados, elegibilidad,
advertencias) **sin aplicar**.

### Authentication / Authorization
Bearer / ownership.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `commercialAddonPublicId` | uuid | Sí | add-on del marketplace |
| `action` | enum string | Sí | `Activate` \| `Deactivate` |

```json
{ "commercialAddonPublicId": "e5f6a7b8-…", "action": "Activate" }
```

### Responses
`200` `AccountCompanyAddonChangePreviewResponse`:

```json
{
  "companyPublicId": "…",
  "commercialAddonPublicId": "…",
  "addonCode": "ATS", "addonName": "Reclutamiento",
  "action": "Activate",
  "addedModuleKeys": ["ATS","ONBOARDING"],
  "removedModuleKeys": [],
  "isEligible": true,
  "ineligibilityReasons": [],
  "warnings": []
}
```

`400` / `401` / `403` / `404` ProblemDetails.

### Business Rules
Mismo patrón que el preview de plan: mostrá módulos ±, `warnings`, y si `isEligible: false`
deshabilitá aplicar mostrando `ineligibilityReasons`.

### Validation Rules / Security Considerations
No muta nada.

---

## 8. Aplicar cambio de add-on

### Endpoint
`POST …/subscription/addons`

### Description
Aplica un cambio **reversible** de add-on (Activate/Deactivate). Devuelve el overview resultante
(HTTP `200`, no `201` — es un comando, no creación de recurso).

### Authentication / Authorization
Bearer / ownership.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |
| `Content-Type` | Sí | `application/json` |
| `If-Match` | Sí | `"<concurrencyToken>"` (del overview) |

### Path Parameters
`companyPublicId` (uuid).

### Query Parameters
Ninguno.

### Request Body
| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `commercialAddonPublicId` | uuid | Sí | add-on del catálogo |
| `action` | enum string | Sí | `Activate` \| `Deactivate` |
| `observations` | string | No | máx 2000 |

```json
{ "commercialAddonPublicId": "e5f6a7b8-…", "action": "Activate", "observations": "Equipo de reclutamiento" }
```

### Responses
| Status | Body / `code` | Cuándo |
|--------|---------------|--------|
| `200` | `AccountCompanySubscriptionOverviewResponse` + `ETag` nuevo | aplicado |
| `400` | `common.validation` | validación / If-Match faltante |
| `403` | `COMPANY_OWNERSHIP_FORBIDDEN` | no es tu compañía |
| `404` | `COMPANY_NOT_FOUND` / `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND` / add-on inexistente | |
| `409` | (motivo de inelegibilidad) | cambio no elegible (p. ej. activar add-on en plan FREE) |
| `409` | `CONCURRENCY_CONFLICT` | token stale, **o** submit concurrente del mismo add-on (choca con el índice único) |

### Business Rules
- Reversible: `Deactivate` revierte un `Activate`.
- Devuelve el overview nuevo (actualizá el estado local y rotá el `concurrencyToken`).
- Tras aplicar, **invalidá el gating** (cambian `effectiveModules`).
- Doble submit del mismo add-on → `409` (índice único compañía-add-on); deshabilitá el botón
  mientras está en vuelo.

### Validation Rules
Tabla del body.

### Security Considerations
Mismo `concurrencyToken` del overview que el cambio de plan — si hacés ambos, re-`GET` el overview
entre uno y otro.

---

# Referencia compartida

## Enums (wire, serializan como string)

| Enum | Valores | Dónde |
|------|---------|-------|
| `SubscriptionAddonChangeAction` | `Activate` · `Deactivate` | `action` en preview/apply de add-on |
| `CompanyAddonStatus` | `Inactive` · `PendingActivation` · `Active` · `PendingDeactivation` | `status` de add-ons activos |
| `CommercialAddonType` | `Massive` · `Specialized` | `type` de add-on |
| `CommercialAddonBillingModel` | `PerActiveEmployee` · `PerSeat` · `PerVolume` | `billingModel` de add-on |
| `CommercialAddonPeriodicity` | `Monthly` · `Annual` | `periodicity` de add-on |

## Nombres de campo en el wire

Los request bodies usan `commercialPlanPublicId` y `commercialAddonPublicId` (auto-transform
`*Id`→`*PublicId` de la API), aunque en C# se llamen `CommercialPlanId`/`CommercialAddonId`.

## Catálogo de códigos de error de la fase

| `code` | HTTP | Cuándo | Acción FE |
|--------|------|--------|-----------|
| `common.validation` | 400 | validación / If-Match faltante | errores por campo |
| `COMPANY_OWNERSHIP_FORBIDDEN` | 403 | no sos el dueño | ocultar la sección |
| `ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN` | 403 | cambiar a MASTER | no ofrecer MASTER |
| `COMPANY_NOT_FOUND` | 404 | compañía inexistente | volver a la lista |
| `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND` | 404 | sin suscripción activa | estado anómalo / soporte |
| (motivo de inelegibilidad) | 409 | aplicar un cambio no elegible | usar el preview antes; mostrar `ineligibilityReasons` |
| `CONCURRENCY_CONFLICT` | 409 | token stale o submit concurrente | re-`GET` overview + reintentar |

## Límites

| Cosa | Valor |
|------|-------|
| `observations` (plan / add-on) | máx 2000 |

Sin rate limits específicos (superficie owner-gated).

## Guía de implementación del cliente

1. **Pantalla de suscripción**: `GET /subscription` (overview + token) + `GET /subscription/plans`
   + `GET /subscription/addons` + `…/addons/marketplace` para armar la vista completa.
2. **Preview obligatorio antes de aplicar**: en cada selección del usuario llamá el `…/preview`
   correspondiente, mostrá módulos ±/`warnings`, y solo habilitá "aplicar" si `isEligible: true`.
3. **Aplicar con If-Match**: usá el `concurrencyToken` del overview; la respuesta del apply es el
   overview nuevo — reemplazá el estado y el token con él. Ante `409 CONCURRENCY_CONFLICT` re-`GET`
   el overview y rehacé.
4. **Invalidar gating tras aplicar**: plan y add-ons cambian `effectiveModules` → recargá
   access-context (Fase 2 §13) y role-builder-catalog (Fase 5), y refrescá el menú/feature flags.
5. **Marketplace bloqueado**: respetá `canAcquire`/`blockedReason` — un FREE debe subir de plan
   antes de adquirir add-ons; ofrecé el CTA de cambio de plan.
6. **Estados transitorios**: mostrá `PendingActivation`/`PendingDeactivation` de los add-ons como
   "en proceso" si aparecen.

## Próximas fases (orden sugerido)

Con la cuenta, compañía, usuarios, roles y suscripción cubiertos, el onboarding queda completo. Lo
que sigue son los **datos** de la app:

1. **General Catalogs** — catálogos de referencia transversales (países, bancos, tipos de documento,
   monedas, etc.) que alimentan los dropdowns de los formularios de negocio.
2. **Organización** — Org Units, Work Centers, Cost Centers, Location Groups (la estructura
   organizativa sobre la que cuelga todo lo demás).
3. **Personnel Files, Job Profiles** y el resto de los módulos de negocio.
