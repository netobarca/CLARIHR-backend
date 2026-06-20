# CLARIHR — Guía de integración de la API (frontend)

Punto de entrada de la documentación de consumo de la API para el frontend. Cada carpeta documenta
un área, a nivel de integración (endpoints, request/response, reglas, ejemplos), con el mismo
formato por endpoint: Overview, Endpoint, Description, Authentication, Authorization, Request
Headers, Path/Query Parameters, Request Body, Responses, Business Rules, Validation Rules, Security
Considerations, enums.

> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); estos docs fueron
> verificados contra `docs/technical/api/openapi.yaml` y el código.

---

## Las fases, en orden de integración

La integración va de **onboarding/cuenta** (1–6) a **datos del negocio** (7+). Cada fase asume las
anteriores.

> 📋 **¿Buscás el orden exacto para integrar sin bloqueos?** Ver
> [INTEGRATION-ORDER.md](./INTEGRATION-ORDER.md) — el paso a paso topológico (23 pasos en 8 bloques),
> cómo validar cada bloque y las **3 trampas de "flujo faltante"** donde un módulo "posterior" es en
> realidad prerequisito de uno "anterior". Esta tabla de fases es el **mapa temático**; ese doc es la
> **ruta de implementación**.

> 🧪 **¿Querés probar el sistema de punta a punta paso a paso?** Ver
> [e2e-testing-walkthrough.md](./e2e-testing-walkthrough.md) — runbook ejecutable de **toda la API**:
> por cada módulo, los requests del camino crítico + qué capturar + cómo validar + negativos, en orden
> de dependencias. (Job Profiles tiene además su
> [walkthrough dedicado](./job-profiles/e2e-testing-walkthrough.md).)

| # | Fase | Carpeta | Qué cubre |
|---|------|---------|-----------|
| 1 | **Autenticación** | [auth/](./auth/authentication.md) | login, refresh, logout, registro + verificación de email, Google, password reset, aceptar invitación |
| 2 | **Account Companies** | [account-companies/](./account-companies/account-companies.md) | compañías del usuario, crear/`switch` (compañía activa), access-context, resource-policies, catálogos de onboarding |
| 3 | **Preferencias** | [preferences/](./preferences/preferences.md) | preferencias de usuario (idioma, social links) y de compañía (moneda, zona horaria) |
| 4 | **Company Users** | [company-users/](./company-users/company-users.md) | invitar y administrar usuarios de la compañía (cierra con `accept-invitation` de Fase 1) |
| 5 | **IAM (Roles y Permisos)** | [iam-authorization/](./iam-authorization/iam-authorization.md) | CRUD de roles, grants, roles por usuario, role-builder-catalog |
| 6 | **Suscripción** | [subscription/](./subscription/subscription.md) | plan, add-ons, preview→apply; define qué módulos existen |
| 7 | **General Catalogs** | [general-catalogs/](./general-catalogs/general-catalogs.md) | catálogos de referencia (países, bancos, profesiones, geografía…) para los forms de negocio |
| 8 | **Organización** | [organization/](./organization/README.md) | estructura organizativa (org units + catálogos), ubicaciones (hierarchy/levels/groups/work centers), cost centers |
| 9 | **Personnel Files** | [personnel-files/](./personnel-files/README.md) | expedientes de empleado y sus ~35 sub‑recursos |
| 10 | **Job Profiles** | [job-profiles/](./job-profiles/README.md) | perfiles de puesto (Draft/Published/Archived), 9 sub‑recursos y 2 catálogos |
| 11 | **Competency Framework** | [competency-framework/](./competency-framework/README.md) | niveles de pirámide ocupacional, conductas (+behaviors) y la matriz de competencias por perfil |
| 12 | **Position Slots** | [position-slots/](./position-slots/position-slots.md) | posiciones ocupables que instancian un job profile (capacidad/ocupación/estado, dependencias, grafo) |
| 13 | **Position Description Catalogs** | [position-description-catalogs/](./position-description-catalogs/README.md) | 13 catálogos de ítems + categorías/clasificaciones que tipifican los descriptores de puesto |
| 14 | **Salary Tabulator** | [salary-tabulator/](./salary-tabulator/salary-tabulator.md) | bandas salariales (PII) con flujo maker-checker de change requests (Draft→Submitted→Approved/Rejected) |
| 15 | **Audit Logs** | [audit/](./audit/audit-logs.md) | trail de auditoría append-only/read-only (listar + detalle before/after/diff) |
| 16 | **Files** | [files/](./files/files.md) | subida directa a storage (upload-session→PUT→complete), descarga y borrado (owner-only) |
| 17 | **Report Export Jobs** | [report-export-jobs/](./report-export-jobs/report-export-jobs.md) | cola de exportación asíncrona (crear→poll→descargar) para reportes grandes, por recurso |
| 18 | **System** | [system/](./system/system.md) | endpoint anónimo de status/liveness (+ contexto de sesión si se manda token) |
| 19 | **Legal Representatives** | [legal-representatives/](./legal-representatives/legal-representatives.md) | representantes legales de la compañía (CRUD + set-primary + usage/export) |

**Cobertura:** toda la superficie de API del **lado tenant** está documentada (51/51 controladores).
Lo único no cubierto es el **Backoffice/Plataforma** (`CLARIHR.Backoffice.Api`) — para operadores de
CLARI, no para el FE de la app.

---

## El hilo conductor (cómo se conectan)

```
1. Login  ───────────────►  accessToken (JWT, 15 min) + refreshToken (14 d, rotación)
                            el JWT ya trae la compañía primaria como tenant
2. ¿Sin compañía?          → crear (account-companies) → POST {id}/switch  → re-emite sesión con tenant
   ¿Con compañía?          → la activa es isActiveContext=true
                            access-context → qué MÓDULOS (plan) y qué PERMISOS (rol) tiene el usuario
3-6. Preferencias / usuarios / roles / suscripción     administran la cuenta y el tenant
7-9. Catálogos → Organización → Personnel Files         los DATOS, gateados por módulo + permiso
```

- **Tenant**: no hay "header de compañía" por request — el tenant viaja **dentro del JWT**. Cambiar
  de compañía = `POST /account/companies/{id}/switch`, que **re-emite la sesión**. Tras un switch,
  invalidá todo caché tenant-dependiente.
- **Gating de UI en dos capas**: un módulo/pantalla se muestra si (a) el módulo está habilitado por
  el **plan** (`effectiveModules` del access-context / suscripción) **y** (b) el usuario tiene el
  **permiso** (`currentUserAccess.permissions`). Se necesitan ambas.

---

## Convenciones globales (válidas para toda la API)

Estas reglas aplican en todas las fases; cada área las refina en su propio `_conventions` o sección.

- **Auth**: `Authorization: Bearer <accessToken>` en toda request autenticada. El `accessToken`
  expira a los 15 min; refrescá con rotación single-use (ver [Fase 1](./auth/authentication.md) —
  **single-flight obligatorio**: dos refresh en paralelo matan la sesión).
- **Identificadores**: en el wire siempre GUIDs `publicId`; los ids internos nunca se exponen. Las
  FKs en los bodies se nombran `<recurso>PublicId` (auto-transform `*Id`→`*PublicId`).
- **Enums**: viajan como **strings** (`"Active"`, nunca `1`).
- **Concurrencia optimista**: los recursos versionados devuelven `concurrencyToken` (body + header
  `ETag`) y las mutaciones exigen `If-Match`. Token **fuerte** = GUID citado (`"<guid>"`); token
  **débil** = `W/"<hash>"` (Company Users, user-roles de IAM). Stale → `409 CONCURRENCY_CONFLICT`;
  faltante → `400`.
- **JSON Patch (RFC 6902)**: `Content-Type: application/json-patch+json`, body = **array desnudo**
  de operaciones (el esquema `{operations:[...]}` del Swagger es engañoso).
- **Paginación**: `page` (1, ≥1) + `pageSize` (20, máx 100); `q` de búsqueda mínimo 2 caracteres.
  Forma: `{ items, pageNumber, pageSize, totalCount }`.
- **Errores (RFC 7807 ProblemDetails)**: `application/problem+json` con `code` estable + `traceId`.
  **La lógica del FE siempre sobre `code`**, nunca sobre el mensaje (se localiza en/es por
  `Accept-Language`). El `400` de validación trae `errors` con keys en **camelCase**.
- **Rate limits**: por usuario+tenant (o por IP en auth), solo en lecturas costosas
  (search/tree/export) y endpoints sensibles (login/invite); `429` con header `Retry-After`.

---

## Notas de estado (verificar con backend antes de integrar)

- **IAM (Fase 5)**: la concurrencia `If-Match` en roles depende de la migración
  `AddConcurrencyTokenToIamRoles` — confirmá que el ambiente esté migrado.
- **Invitaciones (Fase 4)**: el envío de email es hoy un stub de logging; el flujo invitar→aceptar
  no se puede probar end-to-end sin un proveedor real (workaround en el doc de Company Users).
