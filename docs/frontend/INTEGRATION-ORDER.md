# CLARIHR — Orden de integración del frontend (paso a paso, sin bloqueos)

Esta guía es la **ruta de implementación incremental** para el frontend: en qué orden integrar los
documentos de `docs/frontend/` para que **cada paso sea probable sin que falte un flujo prerequisito**.

> El [README](./README.md) lista las áreas como **fases 1–19** (mapa temático). **Esa numeración NO es
> un orden topológico estricto**: hay 3 puntos donde un módulo "posterior" es en realidad prerequisito
> de uno "anterior" (ver [§ Las 3 trampas](#las-3-trampas-de-flujo-faltante)). Este documento reordena
> las áreas por **dependencia real de datos/FK** y por **compuertas de estado**, en 23 pasos / 8 bloques.
>
> Fuente: los docs de integración (verificados contra `docs/technical/api/openapi.yaml` y el código el
> **2026-06-10**) + los contratos Swagger. Orden compilado el **2026-06-11**.

---

## Cómo está gateado todo (qué hace que algo sea "probable")

Cada módulo de negocio se desbloquea con **dos llaves a la vez** (ver
[access-context](./account-companies/account-companies.md), Fase 2 §13):

1. **Módulo habilitado por el plan** → `effectiveModules` (lo da [Subscription](./subscription/subscription.md)).
2. **Permiso del usuario** → `currentUserAccess.permissions` (lo da el rol de [IAM](./iam-authorization/iam-authorization.md)).

Tener el permiso sin la capability no alcanza, y viceversa. El gating del FE es UX; el backend
re-verifica en cada endpoint (`403` si no).

> ✅ **Para testear:** el **creador de la compañía recibe el rol de sistema `Owner`** (`{ "name":
> "Owner", "isSystemRole": true }` en `access-context`), que trae los permisos completos. **Como Owner
> podés probar cualquier módulo de negocio sin construir primero la UI de IAM** — siempre que el **plan
> del tenant de prueba habilite ese módulo** (esa es la llave que se olvida; ver
> [trampa 3](#las-3-trampas-de-flujo-faltante)).

---

## El orden recomendado (23 pasos / 8 bloques)

Cada bloque es **probable de punta a punta** dado el anterior. Al final de cada uno hay un criterio de
validación (✅).

### Bloque 0 — Columna vertebral (sin esto, nada arranca)

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 1 | [`auth/authentication.md`](./auth/authentication.md) | login + **refresh single-flight** + logout. Token Bearer = prerequisito universal. |
| 2 | [`account-companies/account-companies.md`](./account-companies/account-companies.md) | Orden interno: **(a)** `GET list` + `POST {id}/switch` + `GET access-context`; **(b)** catálogos + `POST create` (onboarding); **(c)** edit/archive/reactivate. El `switch` re-emite la sesión con el tenant; el `access-context` gatea toda la UI. |
| — | [`system/system.md`](./system/system.md) | Trivial, en paralelo. Smoke-test de liveness. |

✅ **Validación:** login → crear/entrar a una compañía (`switch`) → leer `effectiveModules` +
`permissions`. Ya sos `Owner` con permisos completos.

### Bloque 1 — Administración del tenant (gating + cuenta)

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 3 | [`subscription/subscription.md`](./subscription/subscription.md) | ⚠️ **Crítico para testabilidad:** `effectiveModules` decide qué módulos podés siquiera probar. `preview`→`apply` para habilitar lo que vas a integrar después. |
| 4 | [`preferences/preferences.md`](./preferences/preferences.md) | Idioma/moneda/zona horaria. Independiente, bajo riesgo. |
| 5 | [`iam-authorization/iam-authorization.md`](./iam-authorization/iam-authorization.md) | Roles/grants/roles-por-usuario. No bloquea probar negocio como Owner, pero sí para probar usuarios no-owner / least-privilege. ⚠️ El `If-Match` en roles depende de la migración `AddConcurrencyTokenToIamRoles` — confirmá que el ambiente esté migrado. |
| 6 | [`company-users/company-users.md`](./company-users/company-users.md) | Invitar/administrar usuarios. ⚠️ El envío de email es **stub de logging**; invitar→aceptar **no es e2e** sin proveedor real (usá el workaround del doc). |

✅ **Validación:** habilitar/cambiar plan y ver el cambio en `effectiveModules`; guardar preferencias;
crear un rol; (con workaround) invitar un usuario.

### Bloque 2 — Datos de referencia (alimentan los forms)

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 7 | [`general-catalogs/general-catalogs.md`](./general-catalogs/general-catalogs.md) | Países, bancos, profesiones, geografía, parentescos, tipos de documento… Read-only. Gateados por el `*.Read` del módulo que los consume (ej. `PersonnelFiles.Read`). Guardá el `code`, no el `publicId`. |

✅ **Validación:** los dropdowns base resuelven contra catálogos reales.

### Bloque 3 — Estructura organizativa (base de todo el negocio) · orden interno por FK

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 8 | [`organization/cost-center-types.md`](./organization/cost-center-types.md) → [`cost-centers.md`](./organization/cost-centers.md) | Catálogo de tipos → centros de costo (cada centro exige un tipo activo). Los referencian unidades y slots. |
| 9 | [`organization/organization-structure-catalogs.md`](./organization/organization-structure-catalogs.md) | `unit-types` (obligatorio) + `functional-areas` — antes de las unidades. |
| 10 | [`organization/organization-units.md`](./organization/organization-units.md) | El organigrama. **Desbloquea Job Profiles** (`orgUnitPublicId` es obligatorio ahí). |
| 11 | [`organization/location-hierarchy-and-levels.md`](./organization/location-hierarchy-and-levels.md) | Config singleton (ya existe) → define los niveles. |
| 12 | [`organization/location-groups.md`](./organization/location-groups.md) | Nodos del árbol de ubicaciones. |
| 13 | [`organization/work-center-types.md`](./organization/work-center-types.md) → [`work-centers.md`](./organization/work-centers.md) | Tipos → centros de trabajo (cuelgan de groups). |

✅ **Validación:** organigrama + ubicaciones + centros de costo navegables (`/tree`). Esto desbloquea
Job Profiles y las asignaciones de Personnel Files.

### Bloque 4 — Catálogos de descriptor + tabulador ⚠️ adelantados vs. README (13/14)

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 14 | [`position-description-catalogs/`](./position-description-catalogs/README.md) | `catalog-items.md` (13 catálogos) + `categories-and-classifications.md`. **Alimentan los forms de Job Profile** (categoría de posición, `salary-classes`, etc.). |
| 15 | [`salary-tabulator/salary-tabulator.md`](./salary-tabulator/salary-tabulator.md) | Bandas salariales (flujo maker-checker). Alimenta `compensations` del Job Profile y las `salary-classes`. |

✅ **Validación:** existen las categorías/clasificaciones y al menos una línea de tabulador para
referenciar desde un perfil.

### Bloque 5 — Catálogo de puesto + competencias

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 16 | [`job-profiles/`](./job-profiles/README.md) | Orden: `job-catalogs.md` → `job-profiles.md` (shell, nace **Draft**) → los 9 sub-recursos → **publicar**. Requeridos al crear: solo `code`, `title`, `orgUnitPublicId`. |
| 17 | [`competency-framework/`](./competency-framework/README.md) | Orden: (Job Catalogs `Competency`/`CompetencyType`/`BehaviorLevel`/`Behavior`, ya del paso 16) → `occupational-pyramid-levels.md` → `competency-conducts.md` → `competency-matrix.md` (por job profile). |

✅ **Validación:** crear un perfil en Draft, agregar ≥1 function + ≥1 requirement + objective +
responsibilities, **publicarlo** sin `422`; armar la matriz de competencias de ese perfil.

### Bloque 6 — Posiciones ocupables

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 18 | [`position-slots/position-slots.md`](./position-slots/position-slots.md) | Requiere `jobProfilePublicId` (obligatorio). Hereda org unit/categoría/contrato/cost center del perfil. |

✅ **Validación:** instanciar un slot sobre un perfil existente; cambiar estado/ocupación/dependencias.

### Bloque 7 — Expedientes de personal (el integrador)

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 19 | [`files/files.md`](./files/files.md) | ⚠️ **Feeder** del sub-recurso `documents` de PF y reusado para todo upload (`upload-session`→`PUT`→`complete`). Antes de PF documents. |
| 20 | [`personnel-files/`](./personnel-files/README.md) | Orden interno abajo. Cierra el círculo: `employee-profile`/`employment-assignments` ya referencian org units / work centers / cost centers / job profiles / position slots reales. |

✅ **Validación:** crear el shell (Draft), cargar datos personales, **finalizar** (Draft→Completed),
recién ahí escribir Empleo/Talento/Compensación, subir un documento (vía Files).

### Bloque 8 — Transversales/utilidades (cualquier momento tras el Bloque 3; al final tienen más sentido)

| # | Documento | Por qué acá / qué desbloquea |
|---|-----------|------------------------------|
| 21 | [`legal-representatives/legal-representatives.md`](./legal-representatives/legal-representatives.md) | CRUD + set-primary + usage/export. (El representante inicial ya se creó en el onboarding del paso 2.) |
| 22 | [`audit/audit-logs.md`](./audit/audit-logs.md) | Trail read-only. Tiene sentido cuando ya hay actividad que auditar. |
| 23 | [`report-export-jobs/report-export-jobs.md`](./report-export-jobs/report-export-jobs.md) | Exportación asíncrona (`crear`→`poll`→`descargar`); necesita recursos que exportar (PF, tabulador…). |

---

## Las 3 trampas de "flujo faltante"

Estos son los puntos donde, si seguís la numeración literal del README, integrás un módulo y al ir a
probarlo **te falta un prerequisito**:

1. **Position Description Catalogs (README 13) y Salary Tabulator (14) alimentan Job Profiles (10).**
   Sin ellos, el form del perfil no tiene categoría de posición, `salary-classes` ni línea de tabulador
   para `compensations`. → Por eso el **Bloque 4 va antes del Bloque 5**.
   *(El shell del perfil sí crea con esos FKs opcionales, así que el shell solo no se rompe — pero el
   form completo sí.)*

2. **Files (16) alimenta Personnel Files → `documents` (9).** El sub-recurso de documentos reusa el
   upload SAS de Files. → **Files antes del sub-recurso documents** (paso 19 antes de 20).

3. **Subscription (6) gobierna `effectiveModules`.** Aunque seas `Owner` con el permiso, un módulo de
   negocio **no se puede probar si el plan del tenant de prueba no lo habilita**. → Confirmá/habilitá el
   plan del módulo antes de ir a probarlo.

---

## Sub-órdenes dentro de los agregados grandes (compuertas de estado)

- **Personnel Files** — compuerta de estado: `shell (Draft)` → sub-recursos de
  **identidad/personal/formación** (editables en Draft) → **finalize (Draft→Completed)** → recién ahí
  los sub-recursos de **Empleo/Talento/Compensación** (sobre un Draft devuelven `422`). *Atajo de
  paralelización: la "mitad personal" solo necesita General Catalogs, así que podés empezarla justo tras
  el Bloque 2, en paralelo con la estructura organizativa; la "mitad empleado" cierra al final.*
- **Job Profiles** — publicar exige `objective` + `responsibilities` + **≥1 requirement** + **≥1
  function**, si no → `422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`. Avisá en la UI antes de intentar.
- **Organization** — el orden por FK ya está en el Bloque 3 (tipos de centro de costo → Cost
  Centers → catálogos → unidades; hierarchy → levels → groups; types → work centers).
- **Competency Framework** — Job Catalogs (categorías) → Pyramid Levels → Conducts → Matrix (por perfil).

---

## Forma corta del grafo de dependencias

```
auth → account/switch/access-context ──┬─→ subscription (habilita módulos)
                                       ├─→ iam · company-users · preferences
                                       └─→ general-catalogs ──→ organization
                                                                (cc-types → cost-centers →
                                                                 catálogos → units;
                                                                 locations; work-centers)
                                                                    │
              position-description-catalogs ─┐                      │
              salary-tabulator ──────────────┴──→ job-profiles ──┬──→ competency-framework
                                                                 └──→ position-slots
                                              files ──→ personnel-files
                                                        (shell → finalize → empleado/documents)
              legal-representatives · audit · report-export-jobs   (transversales)
```

---

## Notas de estado (verificar con backend antes de integrar)

- **IAM (paso 5):** la concurrencia `If-Match` en roles depende de la migración
  `AddConcurrencyTokenToIamRoles` — confirmá que el ambiente esté migrado.
- **Invitaciones (paso 6):** el envío de email es hoy un stub de logging; invitar→aceptar no se puede
  probar end-to-end sin un proveedor real (workaround en el doc de Company Users).
- **Owner = permisos completos:** podés recorrer todos los bloques de negocio como el dueño sin
  construir primero la UI de IAM; IAM solo lo necesitás para probar roles no-owner / least-privilege.

---

### Índice

Volvé al [README](./README.md) para el mapa temático por fases y las convenciones globales.
