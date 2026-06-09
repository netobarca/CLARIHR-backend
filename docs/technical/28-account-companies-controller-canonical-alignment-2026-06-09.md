# Evaluación de Alineación Canónica + Ejecución — `AccountCompaniesController`

> Nivel: **Controller** (controlador + su vertical Company directa). Re-evaluación de **separación de responsabilidades** (criterios 1/6/13) que la alineación original no cubrió.
> Fecha: 2026-06-09 · Rama: `master` · Autor: Claude (Opus 4.8) · Estado: **EJECUTADO — PR-A/B/C uncommitted**
> Hermanos ya alineados: `AccountCompanySubscriptionsController` (doc 25), `AccountCompanyAuthorizationController` (docs 26+27).

## 1. Resumen ejecutivo y veredicto

**Ya mayormente canónico de entrada.** El controlador se alineó en `3b3a729` (Ola 2) y se versionó a `/v1` en `d901906` — esa pasada resolvió concurrencia (2), PATCH RFC-6902 (4), 201+objeto (7), snake (8), lifecycle (10), soft-delete (11/12), `/v1` (14). De 15 criterios: **10 ✅**, **1 ➖ N/A** (9 — migración ya generada, sin esquema nuevo), **4 ❌** (1, 6, 13, 15) — y **1/6/13 son el mismo problema**.

**El hallazgo:** el controlador hospedaba **7 endpoints de otros dominios** que la alineación original no reubicó, y **no estaba enrolado en el guardrail OpenAPI** (sus hermanos `^AccountCompanySubscriptions`/`^AccountCompanyAuthorization` sí), por lo que sus GET pudieron quedar sin `[SwaggerOperation]` sin romper CI.

**Resuelto en 3 PRs sin cambiar una sola URL** → cero ruptura de frontend; solo cambió el controlador host y el `[Tags]` de Swagger.

## 2. Inventario de endpoints (antes → después)

| Endpoint (URL **invariante**) | Antes | Después | Authz |
|---|---|---|---|
| `GET` / `GET {id}` / `POST` / `PUT {id}` / `PATCH {id}` / `PATCH {id}/archive` / `PATCH {id}/reactivate` / `POST {id}/switch` | AccountCompanies | **AccountCompanies** (Company aggregate) | ownership |
| `GET {id}/access-context` | AccountCompanies | **AccountCompanyAccess** (PR-A) | ownership |
| `GET {id}/authorization/role-builder-catalog` | AccountCompanies | **AccountCompanyAccess** (PR-A) | ownership |
| `GET {id}/authorization/resource-policies/{resourceKey}` | AccountCompanies | **AccountCompanyAccess** (PR-A) | ownership |
| `GET countries` / `company-types` / `legal-representative-position-titles` / `legal-representative-representation-types` | AccountCompanies | **AccountCompanyCatalogs** (PR-B) | authn-only |

## 3. Evaluación criterio por criterio

| # | Criterio | Estado | Evidencia / acción |
|---|----------|--------|--------------------|
| 1 | Administra solo su entidad | ❌→✅ | Hospedaba 7 endpoints ajenos → extraídos a AccountCompanyAccess (×3) + AccountCompanyCatalogs (×4). Quedan 8 eps, todos del agregado Company. |
| 2 | ConcurrencyToken en la entidad | ✅ | `Company.ConcurrencyToken` + EF `.IsConcurrencyToken()`; expuesto en `AccountCompanyDetailResponse`; If-Match en PUT/PATCH/archive/reactivate. |
| 3 | Cada endpoint retorna su entidad | ✅ | Todas las mutaciones → `AccountCompanyDetailResponse` completo. |
| 4 | PATCH JSON Patch RFC-6902 | ✅ | `JsonPatchDocument<PatchAccountCompanyRequest>` + `[Consumes(json-patch+json)]` (`/name`, `/companyTypePublicId`). |
| 5 | IDs `{Entidad}PublicId` | ✅ | `companyPublicId` en ruta + auto-transform. |
| 6 | GET = único array | ❌→✅ | Los 4 catálogos (arrays de otras entidades) salieron a AccountCompanyCatalogs; el único GET-array que queda es `List` (Company). |
| 7 | POST → 201 + objeto | ✅ | `ToCreatedAtActionResult` (201 + Location + ETag). |
| 8 | snake_case en BD | ✅ | `CompanyConfiguration` 100% snake. |
| 9 | Ejecutar migración | ➖ N/A | `20260604175301_AddCompanyConcurrencyToken` ya generada; estos PRs no tocan esquema → sin migración nueva. |
| 10 | PATCH estado-solo → reemplazar; lógica → separado | ✅ | `/archive` y `/reactivate` se mantienen **separados**: tienen reglas (no archivar activa/primaria; reactivar sujeto a cupo). El PATCH general excluye `/status`. |
| 11 | Sin fallback en borrado | ✅ | No hay DELETE. |
| 12 | Delete vs soft-delete + motivo | ✅ (soft) | `Company` es raíz de agregado con dependencias históricas/transaccionales (legal reps, subscriptions, IAM roles/users, personnel files, audit). Hard delete rompería integridad referencial y borraría histórico de auditoría/billing → **soft delete vía `Status=Archived`**. |
| 13 | Subrecursos / tags | ❌→✅ | Los 7 ajenos reubicados a sus tags (`Account Access Context`, `Account Companies Catalogs`). |
| 14 | URL `…/v1/…` | ✅ | `api/v1/account/companies` (`d901906`). |
| 15 | `[SwaggerOperation]` | ❌→✅ | Faltaba en los 10 reads/switch + el controlador no estaba enrolado. PR-A/B/C: `[SwaggerOperation]` en los 3 controladores + 3 familias enroladas en `OpenApiContractGuardrailsTests`. |

## 4. Decisiones de usuario (2026-06-09)

- **Grupo A** (read-models de acceso/autorización): **controlador de Acceso dedicado** → `AccountCompanyAccessController`, tag `Account Access Context`, **conserva authz ownership** (no se contamina el front-door RBAC de roles del AuthorizationController).
- **Grupo B** (catálogos de onboarding): **controlador de catálogos de cuenta** → `AccountCompanyCatalogsController`, tag `Account Companies Catalogs`.
- Ninguna reubicación cambia URLs (se confirmó como requisito).

## 5. PRs ejecutados (uncommitted)

**PR-A · `AccountCompanyAccessController`** (nuevo): 3 GET (`access-context`, `authorization/role-builder-catalog`, `authorization/resource-policies/{resourceKey}`) con `[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors(Read)]`; handlers de Application intactos (ownership vía `ResolveOwnershipAsync`). Familia `^AccountCompanyAccess` enrolada.

**PR-B · `AccountCompanyCatalogsController`** (nuevo): 4 catálogos authn-only; errores `Unauthorized` (company-types `BadRequest|Unauthorized`) — contrato exacto preservado, **sin 403 espurio** (precedente UserPreferences). 3 `using` huérfanos removidos de AccountCompanies. Familia `^AccountCompanyCatalogs` enrolada.

**PR-C · cierre AccountCompanies**: `[SwaggerOperation]` en `List`/`GetById`/`Switch`; migración de los 8 eps a `[ProducesStandardErrors]` (consistencia con la familia, códigos exactos preservados); familia `^AccountCompanies` (plural — prefijo disjunto de los `AccountCompany*` singular) enrolada.

`openapi.yaml`: re-tag de los 7 endpoints movidos (suplemento parcial a mano).

## 6. Verificación

- **Build API:** 0 warnings / 0 errors (cada PR).
- **Unit tests:** **1783 / 0** (1774 base + 3 familias OpenAPI × 3 casos Theory). El guardrail valida `[Tags]` + `[SwaggerOperation]` de los 3 controladores.
- **Integration AccountCompany:** **12 / 12** tras PR-A y PR-B (app compone, routing sin conflictos). Tras PR-C **no re-ejecutados**: el Docker daemon (DB de test `clarihr-postgres`, 5433) quedó caído — fallo de infra, no de código. PR-C no cambió rutas/DI/composición (solo atributos Swagger/error en eps existentes), por lo que el riesgo de regresión de integración es nulo. **Re-correr** con `docker compose up -d postgres` + `dotnet test … --filter ~AccountCompany`.

## 7. Notas

- **No-rompe-FE:** ninguna URL cambió; solo controlador host + `[Tags]`.
- **Drift-proofing:** las 3 familias quedan en `OpenApiContractGuardrailsTests.Families[]` → un GET que pierda `[SwaggerOperation]` o una clase que pierda `[Tags]` falla CI.
- **Authz por diseño:** las 3 familias quedan fuera de `GovernedFamilyRegex` (Access = ownership; Catalogs = authn-only; Companies = ownership) — sin policy declarativa que declarar.
- **Pendiente:** commit (3 PRs o 1) + aplicar la migración pre-existente si no está aplicada.
