# Job Profiles — Catálogos (Job Catalogs + Internal Catalogs)

Dos sistemas de catálogos que alimentan los forms de los perfiles: **Job Catalogs** (editables por
compañía, por categoría) y **Internal Catalogs** (diccionario global de valores free‑text).

> Antes de consumir, leé las [Convenciones](./_conventions.md). Acá solo lo específico.

---

## A. Job Catalogs (por categoría, editables por compañía)

CRUD completo de ítems de catálogo, agrupados por **categoría**. Cada perfil/sub‑recurso referencia
estos ítems por `catalogItemPublicId`.

**Permisos:** `GET` → `JobProfiles.Read` · `POST/PUT/PATCH/DELETE` → `JobCatalogs.Admin`.

### Endpoints

Base: `/api/v1/companies/{companyPublicId}/job-catalogs/{category}`

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/.../job-catalogs/{category}` | listar ítems de una categoría (paginado, `isActive`, `q`≥2) |
| `POST` | `/.../job-catalogs/{category}` | crear ítem |
| `PUT` | `/.../job-catalogs/{category}/{jobCatalogPublicId}` | reemplazar (`If-Match`) |
| `PATCH` | `/.../job-catalogs/{category}/{jobCatalogPublicId}` | JSON Patch (`If-Match`) |
| `DELETE` | `/.../job-catalogs/{category}/{jobCatalogPublicId}` | eliminar (`If-Match`) |

### `category` — whitelist (`JobCatalogCategory`)

`EducationLevel` · `KnowledgeArea` · `Competency` · `Training` · `BenefitType` · `WorkingCondition`
· `RelationType` · `DecisionLevel` · `CompetencyType` · `BehaviorLevel` · `Behavior` · `General`

> Qué categoría alimenta qué campo lo resuelve el [`catalog-manifest`](./job-profiles.md) — no lo
> hardcodees.

### Request body — Create / Update

| Campo | Tipo | Req. | Validación |
|-------|------|------|------------|
| `code` | string | Sí | máx 50, formato código, único en la categoría (por compañía) |
| `name` | string | Sí | máx 120 |
| `isActive` | bool | No (update) | default `true`; deshabilita sin borrar |

### Responses

`JobCatalogItemResponse`: `publicId`, `category`, `code`, `name`, `isSystem`, `isActive`,
`concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`, `allowedActions?`.

### Errores específicos

| `code` | HTTP | Cuándo |
|--------|------|--------|
| `JOB_CATALOG_ITEM_CODE_CONFLICT` | 409 | código duplicado en la categoría |
| `JOB_CATALOG_ITEM_NOT_FOUND` | 404 | inexistente / otro tenant |
| `JOB_CATALOG_ITEM_SYSTEM_IMMUTABLE` | 409 | editar/borrar un ítem de **sistema** (`isSystem: true`) |
| `JOB_CATALOG_ITEM_IN_USE` | 409 | borrar un ítem referenciado por perfiles |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` stale |

### Reglas de negocio

- **Ítems de sistema** (`isSystem: true`) son read-only (no editar/borrar) — marcalos así en la UI.
- **In-use guard**: no se puede borrar un ítem referenciado por perfiles (`409 *_IN_USE`); en su
  lugar desactivalo (`isActive: false`).
- Code único por (categoría, compañía).

---

## B. Internal Catalogs (diccionario global de valores free‑text)

Diccionario **cross-tenant** de valores escritos por usuarios, con autocomplete + creación
controlada por similitud. Alimenta los **requisitos** del perfil (educación, conocimiento,
certificación) donde se permite texto libre con sugerencias.

**Permisos:** solo autenticación (no RBAC por tenant).

### Endpoints

Base: `/api/v1/job-profiles/internal-catalogs`

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/internal-catalogs?context={context}` | listar las definiciones de un contexto (ej. `job-profile.requirements`) |
| `GET` | `/internal-catalogs/{catalogKey}/values?q=&limit=` | buscar sugerencias (fuzzy) |
| `POST` | `/internal-catalogs/{catalogKey}/values` | crear (o reusar) un valor |

### `catalogKey` — whitelist

`job-profile.requirements.education` · `job-profile.requirements.knowledge` ·
`job-profile.requirements.certification` (los tres: render `Search`, permiten crear, `minQueryLength=2`).

> Los requisitos tipo `Experience` y `Other` son **free‑text sin catálogo** (no tienen `catalogKey`).

### Buscar valores (`GET .../values`)

Query: `q` (máx 200; bajo `minQueryLength`=2 devuelve vacío), `limit` (1–20, default 10). Respuesta:
array de `{ publicId, value, score }` (score = similitud 0–1), ordenado por relevancia/uso.

### Crear valor (`POST .../values`)

Body: `{ "value": "<texto>" }` (máx 200). Resultado según similitud:

| Status | Cuándo |
|--------|--------|
| `201` | valor nuevo creado |
| `200` | match exacto existente → se **reusa** ese valor |
| `409 INTERNAL_CATALOG_SIMILAR_VALUE_CONFLICT` | demasiado similar (≥90%) a uno existente → el body trae `suggestions[]` para que el usuario elija |
| `422 INTERNAL_CATALOG_CREATE_NOT_ALLOWED` | el catálogo no permite crear (ej. Experience/Other) |

### Reglas de negocio

- **Dedup por similitud**: crear algo ≥90% parecido a un valor existente se rechaza con sugerencias
  — mostralas y dejá que el usuario reuse en vez de duplicar.
- Es un diccionario **compartido entre compañías** (crowdsourced); los valores son inmutables una
  vez creados (sin `If-Match`).

### Guía FE

- En el form de **requirements**, para los tipos `Education`/`Knowledge`/`Certification` mostrá un
  autocomplete: `GET .../values?q=` mientras el usuario tipea (desde 2 caracteres); si no existe,
  `POST .../values` y manejá el `409` mostrando las `suggestions`.
- Para `Experience`/`Other`, input de texto libre (sin catálogo).
- El mapeo tipo‑de‑requisito → `catalogKey` y `renderType` lo da `GET /internal-catalogs?context=job-profile.requirements`.
