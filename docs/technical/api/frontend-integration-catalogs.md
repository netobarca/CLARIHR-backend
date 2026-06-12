# Integración Frontend — Catálogos (`general-catalogs` y `reference-catalogs`)

> **Fecha**: 2026-06-11
> **Controlador**: `GeneralCatalogsController`
> **Cambio**: los catálogos ahora son **company-less** (sin `companyId` en la ruta). Reemplaza a la guía anterior `frontend-integration-file-document-types.md`.

---

## ⚠️ Cambio de contrato (breaking)

Las rutas con compañía **fueron eliminadas**:

| Antes (ELIMINADO) | Ahora |
|---|---|
| `GET /api/v1/companies/{companyId}/general-catalogs/{key}` | `GET /api/v1/general-catalogs/{key}` |
| `GET /api/v1/companies/{companyId}/reference-catalogs/{key}` | `GET /api/v1/reference-catalogs/{key}` |

- **Sin `companyId`.** Solo requieren estar autenticado (`Authorization: Bearer {token}`) — sin permisos de módulo ni RBAC. Esto permite cargarlos **durante el onboarding (crear empresa / representante legal), antes de que exista una compañía**.
- El país, cuando aplica, se pasa por la query `?countryCode=` (código ISO de 2–3 letras, p. ej. `SV`).

---

## Endpoints

### 1) Catálogos generales

```
GET /api/v1/general-catalogs/{catalogKey}[?countryCode=SV]
Authorization: Bearer {token}
```

- Catálogos **globales de sistema** → **NO** requieren `countryCode` (lo ignoran si se envía).
- Catálogos **de país** → **requieren** `countryCode` (sin él devuelven lista vacía `[]`).

| `catalogKey` | ¿`countryCode`? | Contenido |
|---|---|---|
| `countries` | No | Países disponibles |
| `file-document-types` | No | Tipos de documento de expediente |
| `education-statuses` | No | Estados de estudio |
| `education-study-types` | No | Tipos de titulación |
| `education-shifts` | No | Jornadas |
| `education-modalities` | No | Modalidades |
| `education-careers` | No | Carreras |
| `languages` | **Sí** | Idiomas |
| `language-levels` | **Sí** | Niveles de idioma |
| `training-types` | **Sí** | Tipos de formación |
| `duration-units` | **Sí** | Unidades de duración |
| `reference-types` | **Sí** | Tipos de referencia |
| `currencies` | **Sí** | Monedas |
| `banks` | **Sí** | Bancos |

**Respuesta `200 OK`** — array plano de `PersonnelCatalogItemResponse`:

```json
[
  {
    "publicId": "f5e6d7c8-1234-5678-9abc-def012345678",
    "category": "FileDocumentType",
    "code": "DUI",
    "name": "Documento Único de Identidad",
    "isSystem": true,
    "isActive": true,
    "sortOrder": 1,
    "normalizedCode": "DUI"
  }
]
```

| Campo | Tipo | Nota |
|---|---|---|
| `publicId` | `guid` | Identificador del item (úsalo como FK, ver "Cómo se usa") |
| `category` | `string` | Categoría interna (p. ej. `FileDocumentType`, `Bank`) |
| `code` | `string` | Código interno (se devuelve normalizado a MAYÚSCULAS) |
| `name` | `string` | Etiqueta para mostrar en el UI |
| `isSystem` | `bool` | `true` para catálogos de sistema |
| `isActive` | `bool` | El endpoint ya filtra inactivos (`true`) |
| `sortOrder` | `int` | Orden sugerido para el dropdown |
| `normalizedCode` | `string` | Versión normalizada de `code` (trim + MAYÚSCULAS); igual a `code` |

> **Convenciones del contrato público (aplican a TODA la API, no solo a catálogos):** el identificador se serializa como **`publicId`** (nunca `id`); los campos `code` se devuelven **normalizados a MAYÚSCULAS** (trim + upper); y todo objeto con `code` incluye además un campo sintético **`normalizedCode`** con ese mismo valor normalizado. No es un sobrante: lo inyecta `PublicContractJsonTypeInfoResolver` y sí viaja en el JSON.

---

### 2) Catálogos de referencia

```
GET /api/v1/reference-catalogs/{catalogKey}?countryCode=SV[&parentCode=...]
Authorization: Bearer {token}
```

**Todos** son de país → **`countryCode` es obligatorio** (sin él devuelven `[]`).

| `catalogKey` | Notas |
|---|---|
| `professions` | Profesiones |
| `marital-statuses` | Estados civiles |
| `identification-types` | Tipos de identificación (DUI, pasaporte, NIT…) |
| `kinships` | Parentescos |
| `departments` | Departamentos |
| `municipalities` | Municipios — usa `&parentCode={departmentCode}` para filtrar por departamento |

**Respuesta `200 OK`** — array plano de `PersonnelReferenceCatalogItemResponse`:

```json
[
  {
    "publicId": "a1b2c3d4-5678-9abc-def0-123456789abc",
    "code": "SOLTERO_A",
    "name": "Soltero/a",
    "sortOrder": 1,
    "normalizedCode": "SOLTERO_A"
  }
]
```

| Campo | Tipo | Nota |
|---|---|---|
| `publicId` | `guid` | Identificador del item |
| `code` | `string` | Código interno, normalizado a MAYÚSCULAS (úsalo como valor a enviar, ver "Cómo se usa") |
| `name` | `string` | Etiqueta para mostrar |
| `sortOrder` | `int` | Orden sugerido |
| `normalizedCode` | `string` | Versión normalizada de `code`; igual a `code` (campo del contrato público, ver nota arriba) |

---

## Cómo se usa cada catálogo

Regla general:
- **`general-catalogs`** → al guardar, se referencia por **`publicId`**.
- **`reference-catalogs`** → al guardar, se referencia por **`code`**.

Ejemplos verificados:

```jsonc
// Tipo de documento de un documento del expediente (general-catalogs/file-document-types):
{ "documentTypeCatalogItemPublicId": "<publicId del item>" }

// Banco de una cuenta bancaria (general-catalogs/banks):
{ "bankPublicId": "<publicId del item>" }

// Datos personales del expediente (reference-catalogs → se envía el CODE):
{
  "maritalStatusCode": "SOLTERO_A",
  "professionCode": "ANALISTA_DE_DATOS",
  "birthDepartmentCode": "SAN_SALVADOR",
  "birthMunicipalityCode": "SAN_SALVADOR_CENTRO"
}
```

> **Representante legal:** su campo `documentType` es **texto libre** (máx. 40), no una FK a catálogo. Puedes poblar el dropdown con `reference-catalogs/identification-types?countryCode=SV` por UX, pero el backend acepta cualquier texto.

---

## Ejemplos de consumo

```typescript
const headers = { Authorization: `Bearer ${token}` };

// Global (onboarding) — sin país, sin compañía:
const docTypes = await api.get('/api/v1/general-catalogs/file-document-types', { headers });

// De país:
const banks = await api.get('/api/v1/general-catalogs/banks?countryCode=SV', { headers });
const idTypes = await api.get('/api/v1/reference-catalogs/identification-types?countryCode=SV', { headers });

// Jerárquico:
const municipios = await api.get(
  '/api/v1/reference-catalogs/municipalities?countryCode=SV&parentCode=SAN_SALVADOR',
  { headers });
```

---

## Errores

| HTTP | Causa |
|---|---|
| `400` | `catalogKey` no soportado, o `countryCode` con formato inválido (debe ser 2–3 letras) |
| `401` | Token de autenticación faltante o inválido |

Notas de comportamiento:
- Un `catalogKey` de país **sin** `countryCode` (o con un país inexistente) devuelve `200` con lista **vacía** `[]`, no error.
- Los `reference-catalogs` exigen `countryCode` no vacío (`400` si falta).

---

## Resumen de migración para el frontend

1. Quitar el segmento `/companies/{companyId}` de todas las llamadas a catálogos.
2. Agregar `?countryCode=` en los catálogos **de país** (todos los `reference-catalogs` + `languages`, `language-levels`, `training-types`, `duration-units`, `reference-types`, `currencies`, `banks`).
3. Leer el identificador desde **`publicId`** (no `id`).
