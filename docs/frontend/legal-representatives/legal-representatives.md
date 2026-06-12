# Legal Representatives — Guía de consumo (frontend) · Fase 19

> **Prerequisitos:** [onboarding 1–6](../README.md). Complementa
> [Account Companies](../account-companies/account-companies.md) (Fase 2): al **crear** una compañía
> se carga su *representante legal inicial*; este módulo administra el set completo de representantes
> de una compañía. Convenciones globales en el [índice maestro](../README.md).

---

## Overview

Los **representantes legales** de una compañía (apoderados, representantes principales/alternos). Un
controlador, **10 endpoints** con el patrón CRUD canónico + acciones de estado y `set-primary`.

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/companies/{companyPublicId}/legal-representatives` | listar (paginado, filtros) |
| `GET` | `/legal-representatives/{publicId}` | detalle (+ `ETag`) |
| `GET` | `/legal-representatives/{publicId}/usage` | si puede inactivarse |
| `GET` | `/companies/{companyPublicId}/legal-representatives/export` | exportar (xlsx/…) |
| `POST` | `/companies/{companyPublicId}/legal-representatives` | crear |
| `PUT` | `/legal-representatives/{publicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/legal-representatives/{publicId}` | JSON Patch — solo campos descriptivos (`If-Match`) |
| `PATCH` | `/legal-representatives/{publicId}/activate` | reactivar (`If-Match`) |
| `PATCH` | `/legal-representatives/{publicId}/inactivate` | inactivar (`If-Match`) |
| `PATCH` | `/legal-representatives/{publicId}/set-primary` | marcar como principal (`If-Match`) |

**Permisos:** `GET` → `LegalRepresentatives.Read` · escrituras → `LegalRepresentatives.Manage`.
**Compañía activa:** los endpoints `/companies/{companyPublicId}/...` exigen que sea el tenant activo
(cross-tenant → `404`). **Concurrencia:** token fuerte GUID; `GET {id}` emite `ETag`; las mutaciones
exigen `If-Match` (faltante → `400`, stale → `409 CONCURRENCY_CONFLICT`). **Rate limits:** búsqueda
120/min, export 10/min.

### Conceptos clave

- **Exactamente un primario activo**: marcar/crear uno como primario **demota** al primario actual
  (el backend lo gestiona). Solo un representante **activo** puede ser primario.
- **Mínimo un representante activo**: no se puede inactivar el último activo (la compañía debe
  conservar al menos uno).
- **Mutaciones segmentadas**: el `PATCH` (JSON Patch) toca solo campos **descriptivos/de contacto**;
  la **identidad legal** (documento) y el **rango de fechas** se editan por `PUT` (validados como
  unidad); `isPrimary` por `/set-primary`; el estado por `/activate`–`/inactivate`.

---

## Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `firstName` | string | Sí | máx 100; letras unicode/espacios/`'`/`.`/`-` |
| `lastName` | string | Sí | ídem `firstName` |
| `documentType` | string | Sí | máx **40** |
| `documentNumber` | string | Sí | máx 80; formato documento (`^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$`) |
| `positionTitle` | string | Sí | máx 150; formato cargo |
| `representationType` | enum string | Sí | `PrimaryLegalRepresentative` \| `AlternateLegalRepresentative` \| `AttorneyInFact` |
| `authorityDescription` | string | No | máx 500 |
| `appointmentInstrument` | string | No | máx 500 (instrumento de nombramiento) |
| `appointmentDateUtc` | date-time | No | fecha de nombramiento |
| `effectiveFromUtc` | date-time | Sí | vigencia desde |
| `effectiveToUtc` | date-time | No | vigencia hasta (≥ `from`) |
| `email` | string | No | formato email, máx 320 |
| `phone` | string | No | máx 40 |
| `isPrimary` | bool | No (create, default `false`) / Sí (update) | marca/quita primario |

```json
{
  "firstName": "Ana", "lastName": "García",
  "documentType": "DNI", "documentNumber": "0801-1990-12345",
  "positionTitle": "Gerente General",
  "representationType": "PrimaryLegalRepresentative",
  "effectiveFromUtc": "2026-06-10T00:00:00Z",
  "isPrimary": true
}
```

> ⚠️ `representationType` (el rol declarado) es independiente de `isPrimary` (cuál es el primario de
> la compañía): podés tener varios `PrimaryLegalRepresentative` como tipo, pero un solo `isPrimary:
> true` activo.

### Patch (`PATCH`)

Patchables: `/firstName`, `/lastName`, `/positionTitle`, `/representationType`,
`/authorityDescription`, `/appointmentInstrument`, `/appointmentDateUtc`, `/email`, `/phone`.

**No** patchables (van por otra vía): `/documentType`–`/documentNumber` y
`/effectiveFromUtc`–`/effectiveToUtc` → por `PUT` (se validan como unidad); `/isPrimary` →
`/set-primary`; `/isActive` → `/activate`–`/inactivate`.

---

## Acciones especiales

### `GET /usage`
```json
{ "legalRepresentativePublicId": "…", "canInactivate": true }
```
`canInactivate: false` si es el último representante activo. Consultalo antes de ofrecer inactivar.

### `PATCH /set-primary`
Sin body. Designa este representante como el primario de la compañía y **demota** al actual. Solo si
está **activo** (si no → `422 LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION`).

### `GET /export`
Descarga con los mismos filtros del listado. `format` (default `xlsx`; desconocido → `400`). `413` si
excede el límite síncrono.

## Buscar — filtros

`isActive`, `isPrimary`, `representationType`, `q` (búsqueda libre, **mínimo 2 caracteres**), `page`,
`pageSize` (máx 100), `includeAllowedActions`.

## Responses

`LegalRepresentativeResponse`: `publicId`, `companyPublicId`, los campos del body resueltos,
`fullName`, `isActive`, `isPrimary`, `concurrencyToken`, timestamps, `allowedActions?`.

## Enum `LegalRepresentativeRepresentationType` (wire, string)

`PrimaryLegalRepresentative` · `AlternateLegalRepresentative` · `AttorneyInFact`.

## Catálogo de errores

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `LEGAL_REPRESENTATIVE_DOCUMENT_CONFLICT` | 409 | documento (tipo+número) duplicado en la compañía |
| `LEGAL_REPRESENTATIVE_NOT_FOUND` | 404 | inexistente / otro tenant |
| `LEGAL_REPRESENTATIVE_ACTIVE_MIN_REQUIRED` | 409 | inactivar el último representante activo |
| `LEGAL_REPRESENTATIVE_EFFECTIVE_DATES_INVALID` | 422 | `effectiveToUtc` < `effectiveFromUtc` |
| `LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION` | 422 | marcar primario uno inactivo (u otra regla de estado) |
| `LEGAL_REPRESENTATIVE_EXPORT_FORMAT_INVALID` | 400 | formato de export desconocido |
| `LEGAL_REPRESENTATIVES_FORBIDDEN` | 403 | sin permiso |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

## Guía de implementación del cliente

1. **Pantalla de representantes**: tabla con `GET list` (filtros por estado/tipo/primario), columna
   "Principal" según `isPrimary`, y la acción "Marcar como principal" (`/set-primary`) en las filas no
   primarias y activas.
2. **Edición segmentada**: form general con `PUT` (incluye documento y fechas); cambios rápidos de
   datos descriptivos con `PATCH`; estado y primario con sus acciones dedicadas.
3. **Antes de inactivar**: `GET /usage` → respetá `canInactivate` (no permitas inactivar el último
   activo); manejá el `409 ACTIVE_MIN_REQUIRED` por si la condición cambió.
4. **Relación con el alta de compañía**: el representante inicial se crea en el flujo de
   [crear compañía](../account-companies/account-companies.md) (Fase 2); este módulo es para
   administrarlos después (agregar alternos, cambiar el principal, etc.).

## Estado de la documentación

Con Legal Representatives, **toda la superficie de API del lado tenant queda documentada**. Ver el
[índice maestro](../README.md).
