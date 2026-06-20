# Job Profiles — Guía de prueba E2E paso a paso (con dependencias)

> **Objetivo:** probar el flujo COMPLETO de un perfil de puesto de punta a punta, creando
> **cada prerrequisito en el orden correcto** para que ningún paso falle por una dependencia faltante.
> Sirve como smoke-test manual (Swagger UI / Postman / curl) y como guion de QA.
>
> **Fuente:** contratos extraídos y **verificados contra el código** (DTOs de request reales + enums +
> el test de integración `JobProfiles_*` que corre contra la API real) el **2026-06-19**.
> Reglas transversales: ver [_conventions.md](./_conventions.md). Orden global FE: ver
> [../INTEGRATION-ORDER.md](../INTEGRATION-ORDER.md).

---

## 0. Antes de empezar (pre-flight)

Todo lo de abajo asume que ya resolviste estas 3 llaves (si no, el negocio devuelve `401`/`403`):

1. **Token Bearer** — login ([auth](../auth/authentication.md)). Header en TODAS las requests:
   `Authorization: Bearer <token>`.
2. **Compañía activa** — `POST /account/companies/{id}/switch` re-emite la sesión con el tenant
   ([account-companies](../account-companies/account-companies.md)). El `{companyPublicId}` de las rutas
   **debe ser la compañía activa** del token; cross-tenant → `404`.
3. **Módulo habilitado + permiso** — el plan del tenant debe habilitar Job Profiles
   ([subscription](../subscription/subscription.md)) y tu rol debe traer `JobProfiles.Admin` (el creador
   de la compañía es **Owner** y ya lo tiene). Sin esto → `403 JOB_PROFILES_FORBIDDEN`.

> 💡 La forma más rápida de correr esta guía es **Swagger UI** (`/swagger`) autenticado con el Bearer:
> copia/pega los bodies de abajo y encadena los `publicId` / `concurrencyToken` que cada respuesta devuelve.

### Convenciones que usa esta guía

- **Base:** `https://<host>/api/v1` (en los ejemplos se omite el prefijo `/api/v1`).
- **Concurrencia (`If-Match`):** cada respuesta de mutación trae `concurrencyToken` (en el body) **y**
  header `ETag`. `PUT`/`PATCH`/`DELETE` **requieren** el header `If-Match: "<concurrencyToken>"`
  (con comillas dobles). `POST` (crear) **no** lleva `If-Match`. Token vencido → `409 CONCURRENCY_CONFLICT`;
  faltante/malformado → `400`.
- **Regla de oro del token:** guarda el `concurrencyToken` de la última respuesta y úsalo como `If-Match`
  en la siguiente mutación **del mismo recurso**. ⚠️ Tras agregar/editar **sub‑recursos**, vuelve a hacer
  `GET` del perfil para tomar su token actual **antes** de publicar/editar el shell.
- **`PATCH`** = JSON Patch RFC 6902: `Content-Type: application/json-patch+json` y el body es un
  **array desnudo** `[ { "op": "...", "path": "...", "value": "..." } ]`.
- En esta guía marco cada paso con **📌 Captura:** lo que debes guardar para el paso siguiente.

---

## ⚡ Camino feliz mínimo (smoke test en 6 pasos)

Si solo quieres ver un perfil **publicado** lo antes posible (sin categoría ni catálogos opcionales):

1. `POST /companies/{companyId}/organization-structure-catalogs/unit-types` → crea un tipo de unidad.
2. `POST /companies/{companyId}/organization-units` → crea la Org Unit (usa el tipo del paso 1).
3. `POST /companies/{companyId}/job-profiles` con `objective` + `responsibilities` → perfil en **Draft**.
4. `POST /job-profiles/{id}/functions` → agrega **1 función**.
5. `POST /job-profiles/{id}/requirements` → agrega **1 requisito**.
6. `GET /job-profiles/{id}` (toma el token) → `PATCH /job-profiles/{id}` con `status → Published`.

El resto de la guía añade la **categoría de puestos**, los **9 sub‑recursos**, la **edición de un perfil
publicado**, el **archivado** y las **pruebas negativas**.

---

## Bloque A — Datos base (prerrequisitos), en orden de dependencia

### A1. Tipo de Unidad Organizativa  *(prerequisito de la Org Unit)*

```
POST /companies/{companyId}/organization-structure-catalogs/unit-types
```
```json
{ "code": "DIR", "name": "Direccion", "description": null, "sortOrder": 10 }
```
📌 **Captura:** `publicId` → es el `orgUnitTypePublicId`.

### A2. Unidad Organizativa  *(el perfil cuelga de aquí — `orgUnitPublicId` es OBLIGATORIO)*

```
POST /companies/{companyId}/organization-units
```
```json
{
  "code": "DIR-FIN",
  "name": "Direccion de Finanzas",
  "orgUnitTypePublicId": "{A1.publicId}",
  "functionalAreaPublicId": null,
  "parentPublicId": null,
  "sortOrder": 1,
  "description": null,
  "costCenterCode": null,
  "managerEmployeePublicId": null
}
```
📌 **Captura:** `publicId` → es el `orgUnitPublicId` del perfil.

### A3 + A4. Tipo de Función y Tipo de Contrato  *(ejes de la clasificación)*

```
POST /companies/{companyId}/position-description-catalogs/position-function-types/items
```
```json
{ "code": "FUNC-OPE", "name": "Operativa", "description": null, "sortOrder": 10 }
```
```
POST /companies/{companyId}/position-description-catalogs/position-contract-types/items
```
```json
{ "code": "CON-FULL", "name": "Tiempo Completo", "description": null, "sortOrder": 10 }
```
📌 **Captura:** ambos `publicId` (`positionFunctionTypePublicId`, `positionContractTypePublicId`).

### A5. Clasificación de Categoría de Puesto

```
POST /companies/{companyId}/position-category-classifications
```
```json
{
  "code": "CLASS-OPE",
  "name": "Clasificacion Operativa",
  "description": null,
  "positionFunctionTypePublicId": "{A3.publicId}",
  "positionContractTypePublicId": "{A4.publicId}",
  "orgUnitTypePublicId": "{A1.publicId}",
  "sortOrder": 10
}
```
📌 **Captura:** `publicId` → `classificationPublicId`.
⚠️ La terna (función, contrato, tipo-de-unidad) es **única**; repetirla → `409` por ejes duplicados.

### A6. Categoría de Puesto  *(el `positionCategoryPublicId` del perfil)*

```
POST /companies/{companyId}/position-categories
```
```json
{ "code": "CAT-ANALISTA", "name": "Analista", "description": null, "classificationPublicId": "{A5.publicId}", "sortOrder": 10 }
```
📌 **Captura:** `publicId` → `positionCategoryPublicId`.

### A7. (Opcional) Catálogos del shell — objetivo estratégico / equipo / responsabilidad

Solo si quieres llenar esos campos opcionales del perfil. Mismo patrón, distinto slug:

```
POST /companies/{companyId}/position-description-catalogs/strategic-objectives/items
POST /companies/{companyId}/position-description-catalogs/work-equipments/items
POST /companies/{companyId}/position-description-catalogs/responsibilities-catalog/items
```
```json
{ "code": "OBJ-01", "name": "Mejorar rentabilidad", "description": null, "sortOrder": 10 }
```
📌 Alimentan `strategicObjectiveCatalogItemPublicId`, `assignedWorkEquipmentCatalogItemPublicId`,
`responsibilityCatalogItemPublicId` del shell.

### A8. (Opcional) Catálogos de los sub‑recursos — `frequencies`, `requirement-types`, Job Catalogs

Solo si vas a usar las **versiones con catálogo** de los sub‑recursos (las mínimas no lo necesitan):

```
POST /companies/{companyId}/position-description-catalogs/frequencies/items        # function.frequencyCatalogItemPublicId
POST /companies/{companyId}/position-description-catalogs/requirement-types/items   # requirement.requirementTypeCatalogItemPublicId
```
**Job Catalogs** (alimentan `catalogItemPublicId` de requirements/competencies/relations/trainings/benefits):
```
POST /companies/{companyId}/job-catalogs/{category}
```
```json
{ "code": "UNIV", "name": "Educacion Universitaria" }
```
Categorías válidas de `{category}`: `EducationLevel`, `KnowledgeArea`, `Competency`, `Training`,
`BenefitType`, `WorkingCondition`, `RelationType`, `DecisionLevel`, `CompetencyType`, `BehaviorLevel`, `Behavior`.

> 🔎 **No memorices qué catálogo alimenta cada campo** — pídeselo a la API:
> `GET /job-profiles/catalog-manifest` devuelve, por sub‑recurso y campo, el `slug`/`family` y el
> `apiEndpointTemplate` exacto. (Responde con `Cache-Control: no-store`.)

### A9. (Opcional) Línea de Tabulador Salarial — para `compensations`

`compensations` referencia una línea **activa** de tabulador. El tabulador es **maker‑checker**
(crear change‑request → submit → approve, con permisos distintos). Sigue
[salary-tabulator.md](../salary-tabulator/salary-tabulator.md) y guarda el `publicId` de la **línea**
resultante → `salaryTabulatorLinePublicId`.

---

## Bloque B — Crear y poblar el perfil (estado `Draft`)

### B1. Crear el shell del perfil → nace en `Draft`

Requeridos mínimos: `code`, `title`, `orgUnitPublicId`. Incluyo `objective` + `responsibilities` desde ya
para que quede listo para publicar. (`positionCategoryPublicId` es opcional aquí; lo asignaremos/editaremos
luego para probar la edición de un publicado.)

```
POST /companies/{companyId}/job-profiles
```
```json
{
  "code": "JP-ANALISTA-001",
  "title": "Analista de Datos",
  "objective": "Analizar la informacion operativa de la compania.",
  "orgUnitPublicId": "{A2.publicId}",
  "reportsToJobProfilePublicId": null,
  "positionCategoryPublicId": null,
  "strategicObjectiveCatalogItemPublicId": null,
  "assignedWorkEquipmentCatalogItemPublicId": null,
  "responsibilityCatalogItemPublicId": null,
  "decisionScope": "Aprobacion hasta $10,000",
  "assignedResources": "Computadora, telefono",
  "responsibilities": "Construir reportes y tableros.",
  "marketSalaryReference": "Referencia de mercado",
  "valuationNotes": "Notas de valuacion",
  "effectiveFromUtc": null,
  "effectiveToUtc": null,
  "allowInlineCatalogCreate": false
}
```
✅ Espera `201` + header `Location` + `status: "Draft"`.
📌 **Captura:** `publicId` (→ `{profileId}`) y `concurrencyToken`.

### B2. (Recomendado) Descubrir el mapeo campo → catálogo

```
GET /job-profiles/catalog-manifest
```
Úsalo para saber, sin hardcodear, qué endpoint llena cada `*CatalogItemPublicId`.

### B3. Agregar ≥1 **Función**  *(prerrequisito de publicación)*

```
POST /job-profiles/{profileId}/functions
```
```json
{ "functionType": "General", "frequencyCatalogItemPublicId": null, "description": "Construir reportes mensuales", "sortOrder": 1 }
```
- `functionType`: **`General`** | **`Specific`**.
- `frequencyCatalogItemPublicId`: opcional (de A8).
📌 **Captura:** `functionPublicId` (por si lo editas/borras después).

### B4. Agregar ≥1 **Requisito**  *(prerrequisito de publicación)*

```
POST /job-profiles/{profileId}/requirements
```
```json
{ "requirementType": "Experience", "requirementTypeCatalogItemPublicId": null, "catalogItemPublicId": null, "description": "3 anios de experiencia", "sortOrder": 1 }
```
- `requirementType`: **`Education`** | **`Experience`** | **`Knowledge`** | **`Certification`** | **`Other`**.
- `catalogItemPublicId`: opcional; para `Education`/`Knowledge`/`Certification` se suele ligar a un Job Catalog (A8).
📌 **Captura:** `requirementPublicId`.

### B5. (Opcional) Los demás sub‑recursos

Todos cuelgan de `/job-profiles/{profileId}/<recurso>` y devuelven `<recurso>PublicId` + `concurrencyToken`.
**No** afectan la publicación, pero completan el descriptor:

| Sub‑recurso | `POST` body (campos clave) |
|---|---|
| **competencies** | `{ "catalogItemPublicId": null, "name": "Pensamiento analitico", "expectedLevel": "Avanzado", "notes": null, "sortOrder": 1 }` — requiere `catalogItemPublicId` **o** `name`. |
| **relations** | `{ "relationType": "Internal", "catalogItemPublicId": null, "counterpart": "Gerencia de TI", "notes": null, "sortOrder": 1 }` — `relationType`: `Internal`\|`External`; `counterpart` requerido. |
| **trainings** | `{ "catalogItemPublicId": null, "name": "Power BI", "notes": null, "sortOrder": 1 }` — `catalogItemPublicId` **o** `name`. |
| **benefits** | `{ "catalogItemPublicId": null, "name": "Seguro medico", "notes": null, "sortOrder": 1 }` — idéntico a trainings. |
| **working-conditions** | `{ "workConditionTypeCatalogItemPublicId": null, "catalogItemPublicId": null, "name": "Oficina", "notes": null, "sortOrder": 1 }` — `catalogItemPublicId` **o** `name`. |
| **dependent-positions** | `{ "dependentJobProfilePublicId": "{otroPerfilId}", "quantity": 2, "notes": null }` — apunta a **otro** perfil; detecta ciclos (`409 JOB_PROFILE_DEPENDENCY_CYCLE`). |

### B6. (Opcional) **Compensación**  *(1 por perfil)*

```
POST /job-profiles/{profileId}/compensations
```
```json
{ "salaryTabulatorLinePublicId": "{A9.lineId}", "notes": "Banda salarial nivel 4" }
```
- **Una sola por perfil**: un segundo `POST` → `409 JOB_PROFILE_COMPENSATION_ALREADY_EXISTS` (usa `PUT`/`PATCH`).
- La línea debe estar **activa** → si no, `409 ...SALARY_TABULATOR_LINE_INACTIVE`.
- Su `GET` lista devuelve **array completo** (no paginado).

---

## Bloque C — Publicar

### C1. Refrescar el token del perfil

Agregaste sub‑recursos, así que toma el `concurrencyToken` **actual** del perfil:
```
GET /job-profiles/{profileId}
```
📌 **Captura:** `concurrencyToken` (→ `{etag}`).

### C2. Publicar (cambio de estado por `PATCH`)

```
PATCH /job-profiles/{profileId}
Content-Type: application/json-patch+json
If-Match: "{etag}"
```
```json
[ { "op": "replace", "path": "/status", "value": "Published" } ]
```
✅ Espera `200` + `status: "Published"`.
❌ Si falta `objective`, `responsibilities`, ≥1 function o ≥1 requirement →
`422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`.
📌 **Captura:** el **nuevo** `concurrencyToken` (la publicación lo rota).

---

## Bloque D — Editar un perfil **publicado** (caso que estaba fallando ✅ corregido)

Un perfil `Published` **es editable** por `PUT` (el estado NO se toca por `PUT`, solo por `PATCH`). Aquí
asignamos/cambiamos la **categoría de puestos** sobre el perfil ya publicado:

```
PUT /job-profiles/{profileId}
If-Match: "{token de C2}"
```
```json
{
  "code": "JP-ANALISTA-001",
  "title": "Analista de Datos Senior",
  "objective": "Analizar la informacion operativa de la compania.",
  "orgUnitPublicId": "{A2.publicId}",
  "reportsToJobProfilePublicId": null,
  "positionCategoryPublicId": "{A6.publicId}",
  "strategicObjectiveCatalogItemPublicId": null,
  "assignedWorkEquipmentCatalogItemPublicId": null,
  "responsibilityCatalogItemPublicId": null,
  "decisionScope": "Aprobacion hasta $50,000",
  "assignedResources": "Computadora, telefono, vehiculo",
  "responsibilities": "Construir reportes y tableros.",
  "marketSalaryReference": "Referencia de mercado",
  "valuationNotes": "Notas de valuacion",
  "effectiveFromUtc": null,
  "effectiveToUtc": null,
  "allowInlineCatalogCreate": false
}
```
✅ Espera `200`, `status` sigue `Published`, y el `GET` posterior muestra `positionCategoryId = {A6.publicId}`.
> `PUT` es **reemplazo total**: manda TODOS los campos (los que omitas se interpretan como `null`).
📌 **Captura:** el nuevo `concurrencyToken`.

---

## Bloque E — Archivar (estado terminal)

```
PATCH /job-profiles/{profileId}
Content-Type: application/json-patch+json
If-Match: "{token de D}"
```
```json
[ { "op": "replace", "path": "/status", "value": "Archived" } ]
```
✅ Espera `200` + `status: "Archived"`.
A partir de aquí **cualquier** escritura (perfil o sub‑recurso) falla por estado, y **no se puede revertir**
(`Archived` es terminal: intentar volver a `Draft`/`Published` → `409`).

---

## Bloque F — Pruebas negativas (guardas que deben dispararse)

| Caso | Cómo provocarlo | Resultado esperado |
|---|---|---|
| Publicar sin prerrequisitos | Crear perfil sin function/requirement (o sin objective/responsibilities) y `PATCH status→Published` | `422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` |
| Editar publicado dejándolo inválido | Sobre un publicado, `PUT` con `objective: null` | `422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` |
| Token vencido | `PUT`/`PATCH` con un `If-Match` viejo | `409 CONCURRENCY_CONFLICT` |
| `If-Match` faltante | `PUT`/`PATCH`/`DELETE` sin el header | `400` |
| Código duplicado | Crear otro perfil con un `code` ya usado | `409 JOB_PROFILE_CODE_CONFLICT` |
| Ciclo de dependencia | `reportsTo` o `dependent-positions` que cierre un ciclo | `409 JOB_PROFILE_DEPENDENCY_CYCLE` |
| Catálogo inactivo | Referenciar un `catalogItemPublicId` desactivado | `409 JOB_CATALOG_ITEM_INACTIVE` |
| Escribir un archivado | Cualquier `PUT`/`PATCH`/sub‑recurso sobre `Archived` | `409` (state conflict) |
| Sin permiso / módulo off | Llamar sin `JobProfiles.Admin` o con el módulo deshabilitado | `403 JOB_PROFILES_FORBIDDEN` |

---

## Checklist de orden de dependencias (referencia rápida)

```
A1 unit-type ─► A2 org-unit ───────────────────────────────┐ (orgUnitPublicId, OBLIGATORIO)
A3 function-type ┐                                          │
A4 contract-type ┴► A5 classification ─► A6 category ───────┤ (positionCategoryPublicId, opcional)
A7 catálogos shell (opc) ──────────────────────────────────┤
A8 frequencies/requirement-types/job-catalogs (opc) ───────┤
A9 salary-tabulator line (opc) ────────────────────────────┘ (compensations)
                                   │
                                   ▼
        B1 crear perfil (Draft) ─► B3 +función ─► B4 +requisito ─► [B5 sub-recursos] ─► [B6 compensación]
                                   │
                                   ▼
        C1 GET token ─► C2 PATCH status=Published ─► D PUT editar (categoría) ─► E PATCH status=Archived
```

| # | Crear | Endpoint | Alimenta |
|---|-------|----------|----------|
| A1 | Tipo de unidad | `POST /companies/{c}/organization-structure-catalogs/unit-types` | A2, A5 |
| A2 | Org Unit | `POST /companies/{c}/organization-units` | perfil `orgUnitPublicId` ✅ obligatorio |
| A3 | Tipo de función | `POST /companies/{c}/position-description-catalogs/position-function-types/items` | A5 |
| A4 | Tipo de contrato | `POST /companies/{c}/position-description-catalogs/position-contract-types/items` | A5 |
| A5 | Clasificación | `POST /companies/{c}/position-category-classifications` | A6 |
| A6 | Categoría | `POST /companies/{c}/position-categories` | perfil `positionCategoryPublicId` |
| A7 | Catálogos shell (opc) | `POST /companies/{c}/position-description-catalogs/{slug}/items` | campos opcionales del shell |
| A8 | Catálogos sub‑rec. (opc) | `…/frequencies/items`, `…/requirement-types/items`, `POST /companies/{c}/job-catalogs/{cat}` | sub‑recursos |
| A9 | Línea de tabulador (opc) | ver salary-tabulator.md (maker‑checker) | compensations |
| B1 | Perfil (Draft) | `POST /companies/{c}/job-profiles` | — |
| B3 | Función | `POST /job-profiles/{p}/functions` | publicación (≥1) |
| B4 | Requisito | `POST /job-profiles/{p}/requirements` | publicación (≥1) |
| C2 | Publicar | `PATCH /job-profiles/{p}` `status→Published` | — |
| D | Editar publicado | `PUT /job-profiles/{p}` | — |
| E | Archivar | `PATCH /job-profiles/{p}` `status→Archived` | — |

---

### Índice

Volvé al [README](./README.md) (modelo de datos y lista de recursos) o a las
[Convenciones](./_conventions.md) (auth, estados, `If-Match`, JSON Patch, patrón de sub‑recursos).
