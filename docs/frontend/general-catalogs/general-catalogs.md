# General Catalogs — Guía de consumo (frontend) · Fase 7

> **Prerequisitos:** [Account Companies](../account-companies/account-companies.md) (Fase 2 — opera
> sobre la **compañía activa**) e [IAM](../iam-authorization/iam-authorization.md) (Fase 5 — el
> acceso está gateado por el permiso **`PersonnelFiles.Read`**, ver abajo).
>
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`); verificado contra
> `docs/technical/api/openapi.yaml` y el código el **2026-06-10**.

---

## Overview

Un controlador (**General Catalogs**), **2 endpoints `GET` read-only**, que sirven los catálogos de
referencia que alimentan los **dropdowns de los formularios de Personnel Files** (idiomas, monedas,
bancos, países, profesiones, departamentos/municipios, etc.).

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/api/v1/companies/{companyPublicId}/general-catalogs/{catalogKey}` | catálogos de currículum / sistema / país (14 keys) |
| `GET` | `/api/v1/companies/{companyPublicId}/reference-catalogs/{catalogKey}` | catálogos de referencia por país, algunos jerárquicos (6 keys) |

### ⚠️ Dos familias de "catálogos" — no confundir

| Familia | Endpoint | Para qué | Authz |
|---------|----------|----------|-------|
| **Onboarding de compañía** (Fase 2) | `/account/companies/countries`, `/company-types`, `/legal-representative-*` | armar el form de **crear compañía** (antes de que exista) | solo autenticación |
| **General Catalogs** (este doc) | `/companies/{id}/general-catalogs/*`, `/reference-catalogs/*` | dropdowns de los **formularios de empleado / Personnel Files** | `PersonnelFiles.Read` + tenant activo |

Ojo: `countries` existe en **ambas** familias, con DTOs distintos. Para el form de crear compañía
usá el de la Fase 2; para los forms de empleado usá el de acá.

### Conceptos clave (leer primero)

- **Acceso gateado por Personnel Files** (decisión de diseño): leer cualquier catálogo requiere el
  permiso **`PersonnelFiles.Read`** y que el **módulo Personnel Files** esté habilitado en el plan.
  No hay un permiso `catalogs.read` propio. Si tu pantalla necesita un catálogo pero el usuario no
  tiene PF, hoy recibe `403` — coordiná con backend si aparece ese caso (está contemplado como
  posible reapertura).
- **`{companyPublicId}` debe ser la compañía activa**: el handler valida `companyPublicId == tenant
  del JWT`. Si no coincide → **`403 TENANT_MISMATCH`** (con `details[{resourceKey:"PERSONNEL_FILES",
  action:"Read"}]`). Hacé `switch` (Fase 2) primero.
- **`catalogKey` es una whitelist cerrada**: una key no soportada → **`400`** (`errors.catalogKey`).
  No inventes keys; usá exactamente las de las tablas de abajo.
- **Scoping transparente para el FE**: algunos catálogos son **globales/sistema** (no varían por
  país: educación, tipos de documento) y otros son **por país** (idiomas, monedas, bancos,
  profesiones, geografía) — el backend filtra por el país de la compañía autorizada. El FE siempre
  llama igual (con `companyPublicId`); solo cambian los resultados.
- **Read-only**: no hay POST/PUT/PATCH/DELETE acá. La administración de estos catálogos es de
  plataforma (backoffice), no del FE de la app.
- **Sin paginación y sin rate limit**: devuelven el array completo de ítems activos ordenado por
  `sortOrder`. Son lecturas chicas de referencia — cacheálas del lado del cliente.

---

# Endpoints

## 1. Listar un catálogo general

### Endpoint
`GET /api/v1/companies/{companyPublicId}/general-catalogs/{catalogKey}`

### Description
Ítems **activos** del catálogo identificado por `catalogKey`, ordenados por `sortOrder`. Cubre
catálogos de currículum, de sistema (globales) y por país.

### Authentication
Bearer requerido (tenant activo = `companyPublicId`).

### Authorization
`PersonnelFiles.Read` + módulo Personnel Files habilitado.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la compañía **activa** |
| `catalogKey` | string | una de las 14 keys (ver tabla) |

### Query Parameters
Ninguno.

### Request Body
N/A.

### `catalogKey` soportadas (general-catalogs)

| `catalogKey` | Contenido | Scope |
|--------------|-----------|-------|
| `countries` | países | global |
| `currencies` | monedas | por país |
| `banks` | bancos | por país |
| `languages` | idiomas | por país |
| `language-levels` | niveles de idioma | currículum |
| `training-types` | tipos de capacitación | currículum |
| `duration-units` | unidades de duración | currículum |
| `reference-types` | tipos de referencia (laboral/personal) | currículum |
| `education-statuses` | estados de estudios (en curso/finalizado…) | sistema (global) |
| `education-study-types` | tipos de estudio | sistema (global) |
| `education-shifts` | turnos (diurno/nocturno…) | sistema (global) |
| `education-modalities` | modalidades (presencial/virtual…) | sistema (global) |
| `education-careers` | carreras | sistema (global) |
| `file-document-types` | tipos de documento de archivo | sistema (global) |

### Responses
`200` — array de `PersonnelCatalogItemResponse`:

```json
[{
  "publicId": "…",
  "category": "Currency",
  "code": "HNL",
  "name": "Lempira hondureño",
  "isSystem": true,
  "isActive": true,
  "sortOrder": 1,
  "normalizedCode": "HNL"
}]
```

| Campo | Tipo | Notas |
|-------|------|-------|
| `publicId` | uuid | id del ítem |
| `category` | string | categoría interna del catálogo (informativo) |
| `code` | string | **el valor a guardar** en el form del empleado |
| `name` | string | etiqueta a mostrar |
| `isSystem` | bool | `true` = catálogo de sistema (global) |
| `isActive` | bool | siempre `true` en esta respuesta (solo trae activos) |
| `sortOrder` | int | orden de presentación |
| `normalizedCode` | string | code normalizado (solo lectura) |

| Status | `code` | Cuándo |
|--------|--------|--------|
| `400` | `common.validation` | `catalogKey` no soportada (`errors.catalogKey`) |
| `401` | — | sin token |
| `403` | `TENANT_MISMATCH` | `companyPublicId` ≠ tenant activo |
| `403` | (PersonnelFiles forbidden) | sin `PersonnelFiles.Read` o módulo deshabilitado |

### Business Rules
- Guardá el **`code`** (no el `publicId`) en los formularios de empleado — es lo que los validadores
  de Personnel Files esperan.
- Los `education-*` y `file-document-types` son globales: podés cachearlos una vez por sesión; los
  por-país conviene recachearlos si cambia la compañía activa.

### Validation Rules
`catalogKey` ∈ whitelist; cualquier otra → `400`.

### Security Considerations
No hay enumeración sensible — son listas de referencia. El gating PF es la única barrera.

---

## 2. Listar un catálogo de referencia

### Endpoint
`GET /api/v1/companies/{companyPublicId}/reference-catalogs/{catalogKey}`

### Description
Ítems **activos** del catálogo de referencia **por país** identificado por `catalogKey`, ordenados
por `sortOrder`. Soporta catálogos **jerárquicos** vía `parentCode`.

### Authentication / Authorization
Bearer / `PersonnelFiles.Read` + módulo PF (igual que el endpoint 1).

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | Sí | `Bearer <accessToken>` |

### Path Parameters
| Param | Tipo | Descripción |
|-------|------|-------------|
| `companyPublicId` | uuid | la compañía activa |
| `catalogKey` | string | una de las 6 keys (ver tabla) |

### Query Parameters
| Param | Tipo | Req. | Validación |
|-------|------|------|------------|
| `parentCode` | string | No | máx 120; formato código válido; filtra hijos de catálogos jerárquicos |

### Request Body
N/A.

### `catalogKey` soportadas (reference-catalogs)

| `catalogKey` | Contenido | Jerárquico |
|--------------|-----------|------------|
| `professions` | profesiones | no |
| `marital-statuses` | estados civiles | no |
| `identification-types` | tipos de identificación (DNI, pasaporte…) | no |
| `kinships` | parentescos | no |
| `departments` | departamentos / estados (nivel 1 geográfico) | no (es el padre) |
| `municipalities` | municipios | **sí** — hijos de un `department` vía `parentCode` |

### Responses
`200` — array de `PersonnelReferenceCatalogItemResponse`:

```json
[{
  "publicId": "…",
  "code": "FM",
  "name": "Francisco Morazán",
  "sortOrder": 1,
  "normalizedCode": "FM"
}]
```

| Campo | Tipo | Notas |
|-------|------|-------|
| `publicId` | uuid | id del ítem |
| `code` | string | **el valor a guardar** |
| `name` | string | etiqueta |
| `sortOrder` | int | orden |
| `normalizedCode` | string | solo lectura |

> Más liviano que `PersonnelCatalogItemResponse`: sin `category`/`isSystem`/`isActive`.

| Status | `code` | Cuándo |
|--------|--------|--------|
| `400` | `common.validation` | `catalogKey` no soportada, o `parentCode` inválido (>120 / formato) |
| `401` | — | sin token |
| `403` | `TENANT_MISMATCH` | `companyPublicId` ≠ tenant activo |
| `403` | (PersonnelFiles forbidden) | sin permiso / módulo |

### Business Rules
- **Catálogos jerárquicos (geografía)**: pedí `departments` sin `parentCode`, y al elegir uno pedí
  `municipalities?parentCode={code del department}` para sus municipios. Sin `parentCode`,
  `municipalities` devuelve todos (puede ser grande) — usá la cascada.
- Todo es **por país** (el de la compañía activa): cambiar de compañía puede cambiar la geografía,
  profesiones, etc. — recacheá tras `switch`.
- Guardá el **`code`** en el form del empleado.

### Validation Rules
`catalogKey` ∈ whitelist; `parentCode` opcional, máx 120, formato código válido.

### Security Considerations
Igual que el endpoint 1.

---

# Referencia compartida

## Catálogo de códigos de error de la fase

| `code` | HTTP | Cuándo | Acción FE |
|--------|------|--------|-----------|
| `common.validation` | 400 | `catalogKey` no soportada / `parentCode` inválido | revisar la key contra la whitelist |
| `TENANT_MISMATCH` | 403 | `companyPublicId` ≠ tenant activo | corregir routing / `switch` |
| (PersonnelFiles forbidden) | 403 | sin `PersonnelFiles.Read` o módulo PF deshabilitado | ocultar la pantalla / revisar plan |
| (401 sin code) | 401 | sin token | re-login |

## Reglas / límites

| Cosa | Valor |
|------|-------|
| `catalogKey` | whitelist cerrada (14 general + 6 reference) |
| `parentCode` | opcional, máx 120, formato código |
| Paginación | ninguna (array completo de activos) |
| Rate limit | ninguno |
| Mutaciones | ninguna (read-only; admin es de backoffice/plataforma) |

## Guía de implementación del cliente

1. **Capa de catálogos cacheada**: un servicio que, dado un `catalogKey`, hace el `GET` una vez y
   cachea el resultado por (compañía activa, key). Invalidá el caché al hacer `switch` de compañía.
2. **Globales vs. por-país**: los `education-*` y `file-document-types` son globales (caché larga);
   el resto conviene atarlos a la compañía activa.
3. **Guardá `code`, mostrá `name`**: en los dropdowns el value es `code` (lo que Personnel Files
   valida), el label es `name`. No persistas `publicId`.
4. **Cascada geográfica**: `departments` → al seleccionar, `municipalities?parentCode={code}`. No
   cargues todos los municipios de una.
5. **Gating**: estos catálogos viven detrás de `PersonnelFiles.Read` — si tu pantalla no es de
   Personnel Files pero necesita un catálogo, validá que el usuario igual tenga ese permiso (o
   pedí a backend abrir el acceso).

## Próximas fases (orden sugerido)

1. **Organización** — Org Units, Work Centers, Cost Centers, Location Groups (la estructura
   organizativa sobre la que cuelgan los empleados).
2. **Personnel Files** — los expedientes de empleado (ya hay docs de sus sub-recursos en
   `docs/frontend/personnel-files/`); estos catálogos alimentan sus formularios.
3. **Job Profiles** y el resto de los módulos de negocio.
