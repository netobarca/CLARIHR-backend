# Respuesta backend — Preguntas de integración FE · Entrevista de retiro (Exit Interview)

| | |
| --- | --- |
| **Para** | Equipo Frontend |
| **De** | Equipo Backend (.NET API) |
| **Contexto** | Aclaraciones a las 5 preguntas que el OpenAPI no expresa: catalogKeys + seed, mapeo `code` de error → HTTP status, valores de `separationType`, concurrencia de la submission, y permission-codes. Todo verificado contra el código real ya mergeado. |
| **Rama** | `feature/entrevista-retiro-fase1` (mergeada a `master`, PR #51) |
| **Fecha** | 2026-06-26 |
| **Estado** | ✅ Respondido y verificado en código. Las 5 áreas confirmadas; ver §0 para las 5 correcciones a las suposiciones del FE. |

---

## 0. TL;DR — correcciones a las suposiciones del FE

La mayoría de sus suposiciones eran correctas. Estas 5 **no** lo eran y conviene ajustarlas antes de codificar:

1. **`countryCode` es obligatorio también en `form-control-types`** (no solo en los dos `reference-catalogs`).
2. **`EXIT_INTERVIEW_FIELD_KEY_DUPLICATE` y `EXIT_INTERVIEW_OPTION_CODE_DUPLICATE` son `409`** (conflicto de unicidad), no `400`/`422`.
3. **Los pares `*_OPTIONS_NOT_ALLOWED` y `*_RANGE_*` son códigos DISTINTOS, no alias** — los cuatro existen y los cuatro son `422`.
4. **El `code` de error llega en `body.code` a nivel RAÍZ** (es una extensión de `ProblemDetails` que .NET serializa aplanada; **no** hay objeto `extensions` anidado en el wire). Su suposición `body.code` es la correcta.
5. **La submission NO usa optimistic concurrency** — no exige `If-Match` ni `concurrencyToken` en el body.

---

## 1. Catálogos — catalogKeys, endpoint genérico y seed

> **Aviso transversal:** los **tres** catálogos exigen `countryCode` (ej. `SV`). No es opcional en ninguno.

### 1.1 `retirement-categories`

- ✅ catalogKey exacta = **`retirement-categories`**, vía **`reference-catalogs`**
  → `GET /api/v1/reference-catalogs/retirement-categories?countryCode=SV`
- ✅ Requiere `countryCode` (**obligatorio**).
- ✅ Seed SV confirmado; los 8 códigos exactos coinciden con su lista:

| code | name | sortOrder | `separationType` (ver §3) |
| --- | --- | --- | --- |
| `VOLUNTARIA` | Renuncia voluntaria | 10 | `VOLUNTARIA` |
| `JUBILACION` | Jubilación | 20 | `VOLUNTARIA` |
| `INVOLUNTARIA` | Despido / involuntaria | 30 | `INVOLUNTARIA` |
| `ABANDONO` | Abandono de trabajo | 40 | `INVOLUNTARIA` |
| `NO_SUPERA_PERIODO_PRUEBA` | No supera período de prueba | 50 | `INVOLUNTARIA` |
| `FIN_CONTRATO` | Fin de contrato | 60 | `OTRA` |
| `MUTUO_ACUERDO` | Mutuo acuerdo | 70 | `OTRA` |
| `FALLECIMIENTO` | Fallecimiento | 80 | `OTRA` |

### 1.2 `retirement-reasons`

- ✅ catalogKey exacta = **`retirement-reasons`**, vía **`reference-catalogs`**.
- ✅ `parentCode` = **código de la categoría padre**, y es **OPCIONAL**: si se omite, devuelve **todos** los motivos (igual que `insurance-ranges`); si se envía, filtra por esa categoría.
  → `GET /api/v1/reference-catalogs/retirement-reasons?countryCode=SV&parentCode=VOLUNTARIA`
- ✅ Requiere `countryCode` (**obligatorio**).
- Seed SV (motivo → `parentCode`):

| parentCode | reasons (code) |
| --- | --- |
| `VOLUNTARIA` | `MEJOR_OFERTA_SALARIAL`, `CRECIMIENTO_PROFESIONAL`, `AMBIENTE_LABORAL`, `RELACION_JEFATURA`, `MOTIVOS_PERSONALES`, `SALUD`, `ESTUDIOS`, `REUBICACION_GEOGRAFICA`, `DISTANCIA_TRANSPORTE`, `INSATISFACCION_FUNCIONES` |
| `JUBILACION` | `JUBILACION_EDAD` |
| `INVOLUNTARIA` | `BAJO_DESEMPENO`, `REESTRUCTURACION`, `FALTA_DISCIPLINARIA`, `AUSENTISMO`, `INCUMPLIMIENTO_POLITICAS`, `RECORTE_PRESUPUESTARIO` |
| `ABANDONO` | `ABANDONO_TRABAJO` |
| `NO_SUPERA_PERIODO_PRUEBA` | `NO_SUPERA_PRUEBA` |
| `FIN_CONTRATO` | `FIN_CONTRATO_TEMPORAL`, `FIN_OBRA_PROYECTO` |
| `MUTUO_ACUERDO` | `MUTUO_ACUERDO` |
| `FALLECIMIENTO` | `FALLECIMIENTO` |

### 1.3 `form-control-types`

- ✅ catalogKey exacta = **`form-control-types`**, vía **`general-catalogs`** (no `reference-catalogs`)
  → `GET /api/v1/general-catalogs/form-control-types?countryCode=SV`
- ⚠️ Requiere `countryCode` (**obligatorio** — se trata como catálogo country-scoped). *(Corrección a su suposición de que no lo requería.)*
- ✅ Los **9 códigos cerrados** coinciden exactamente con su lista: `TEXTO_CORTO`, `TEXTO_LARGO`, `NUMERO`, `FECHA`, `LISTA_DESPLEGABLE`, `OPCION_UNICA`, `SELECCION_MULTIPLE`, `CASILLA`, `ESCALA`.
- ✅ **Confirmado: el item del catálogo NO trae metadata de capacidades.** El endpoint genérico devuelve solo `{ id/publicId, code, name, sortOrder, isActive }`. Por tanto **tratar la tabla §1.2 de la guía como constantes fijas del FE es lo correcto y necesario.**

Para que sus constantes queden 100% alineadas con el modelo real del backend, estas son las **4 dimensiones de capacidad autoritativas** del seed (lo que el catálogo conoce internamente aunque no lo exponga en el wire):

| code | valueKind | options | range (min/max) | multiple |
| --- | --- | --- | --- | --- |
| `TEXTO_CORTO` | Text | no | no | no |
| `TEXTO_LARGO` | Text | no | no | no |
| `NUMERO` | Number | no | **sí** | no |
| `FECHA` | Date | no | no | no |
| `LISTA_DESPLEGABLE` | Options | **sí** | no | no |
| `OPCION_UNICA` | Options | **sí** | no | no |
| `SELECCION_MULTIPLE` | Options | **sí** | no | **sí** |
| `CASILLA` | Boolean | no | no | no |
| `ESCALA` | Number | no | **sí** (es el `scaleMax`) | no |

> El backend **no** modela una capacidad "puntúa/score" a nivel de tipo de control. El puntaje (índice ponderado 0–100) se configura por **opción/campo**, no por tipo. Las 4 dimensiones de arriba son las únicas que el catálogo conoce.

### Resumen de endpoints de catálogo

| Catálogo | Endpoint | countryCode | parentCode |
| --- | --- | --- | --- |
| `retirement-categories` | `GET /api/v1/reference-catalogs/retirement-categories` | obligatorio | n/a |
| `retirement-reasons` | `GET /api/v1/reference-catalogs/retirement-reasons` | obligatorio | opcional (filtro) |
| `form-control-types` | `GET /api/v1/general-catalogs/form-control-types` | obligatorio | n/a |

---

## 2. Códigos de error `EXIT_INTERVIEW_*` → HTTP status

**Dónde llega el `code`:** ✅ en **`body.code`** a nivel **raíz** del JSON. Es una extensión de `ProblemDetails` y .NET la serializa **aplanada** — **no** hay un objeto `extensions` anidado en el wire. Está cubierto por tests de integración (`RootElement.GetProperty("code")`), así que es estable. `body.traceId` también va en la raíz.

| code | HTTP | Tipo interno | ¿coincide con su suposición? |
| --- | --- | --- | --- |
| `EXIT_INTERVIEW_FORM_NAME_DUPLICATE` | **409** | Conflict | ✓ |
| `EXIT_INTERVIEW_FORM_NOT_DRAFT` | **409** | Conflict | ✓ |
| `EXIT_INTERVIEW_FORM_NOT_PUBLISHED` | **422** | UnprocessableEntity | ✓ |
| `EXIT_INTERVIEW_FORM_NOT_PUBLISHABLE` | **422** | UnprocessableEntity | ✓ |
| `EXIT_INTERVIEW_FIELD_KEY_DUPLICATE` | **409** | Conflict | ⚠️ esperaban 400/422 |
| `EXIT_INTERVIEW_OPTION_CODE_DUPLICATE` | **409** | Conflict | ⚠️ esperaban 400/422 |
| `EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED` | **422** | UnprocessableEntity | ✓ |
| `EXIT_INTERVIEW_FIELD_OPTIONS_NOT_ALLOWED` | **422** | UnprocessableEntity | ver nota ↓ |
| `EXIT_INTERVIEW_OPTIONS_NOT_ALLOWED_ON_FIELD` | **422** | UnprocessableEntity | ver nota ↓ |
| `EXIT_INTERVIEW_FIELD_RANGE_INVALID` | **422** | UnprocessableEntity | ver nota ↓ |
| `EXIT_INTERVIEW_FIELD_RANGE_NOT_ALLOWED` | **422** | UnprocessableEntity | ver nota ↓ |
| `EXIT_INTERVIEW_CONTROL_TYPE_INVALID` | **422** | UnprocessableEntity | ✓ |
| `EXIT_INTERVIEW_FORM_NOT_FOUND` | **404** | NotFound | ✓ |
| `EXIT_INTERVIEW_SUBMISSION_NOT_FOUND` | **404** | NotFound | ✓ |
| `EXIT_INTERVIEW_FORM_CONCURRENCY_CONFLICT` | **409** | Conflict | ✓ |
| `EXIT_INTERVIEW_SUBMISSION_ALREADY_SUBMITTED` | **409** | Conflict | ✓ |

> El mapeo tipo→status es global: `Validation→400`, `UnprocessableEntity→422`, `Unauthorized→401`, `Forbidden→403`, `NotFound→404`, `Conflict→409`.

⚠️ **Corrección clave:** `FIELD_KEY_DUPLICATE` y `OPTION_CODE_DUPLICATE` son **409** (conflictos de unicidad), no 400/422.

**Nota — los pares NO son alias; son códigos distintos (los cuatro existen, los cuatro son `422`):**

| code | significado |
| --- | --- |
| `EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED` | un campo de selección debe definir al menos una opción |
| `EXIT_INTERVIEW_FIELD_OPTIONS_NOT_ALLOWED` | este tipo de control no admite opciones |
| `EXIT_INTERVIEW_OPTIONS_NOT_ALLOWED_ON_FIELD` | solo se pueden agregar opciones a un control de selección |
| `EXIT_INTERVIEW_FIELD_RANGE_INVALID` | el mínimo no puede ser mayor que el máximo |
| `EXIT_INTERVIEW_FIELD_RANGE_NOT_ALLOWED` | este tipo de control no admite rango numérico |

### 2.1 Ejemplo de respuesta de error (no-validación)

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "An exit-interview form with the same name already exists.",
  "detail": "An exit-interview form with the same name already exists.",
  "status": 409,
  "code": "EXIT_INTERVIEW_FORM_NAME_DUPLICATE",
  "traceId": "0HMVGK7G1BVLS:00000001"
}
```

`title`/`detail` llegan ya localizados (es/en) según el `Accept-Language`. Para i18n propio, mapeen por `code`.

### 2.2 `common.validation` (400) — forma de `errors`

- `body.code` = **`common.validation`** (raíz). El diccionario llega en **`body.errors`** (propiedad raíz estándar de `ValidationProblemDetails`), keyed por campo.
- **Para las validaciones del llenado (lo que les interesa), la key es el `fieldKey` literal o un bucket fijo — NO un path indexado:**

| caso | key en `errors` |
| --- | --- |
| opción inválida para un campo | el **`fieldKey`** del campo |
| valor numérico fuera de rango | el **`fieldKey`** del campo |
| respuesta a un campo inexistente | `"answers"` |
| campos obligatorios faltantes | `"answers"` |
| empleado sin motivo de retiro registrado | `"personnelFile"` |
| no hay formulario configurado | `"form"` |
| anónimo intentando guardar borrador (`submit=false`) | `"submit"` |

Ejemplo:

```json
{
  "status": 400,
  "code": "common.validation",
  "title": "One or more validation errors occurred.",
  "errors": {
    "PREGUNTA_3": ["Option 'XYZ' is not valid for this field."],
    "answers": ["Field 'PREGUNTA_5' is required."]
  },
  "traceId": "0HMVGK7G1BVLS:00000002"
}
```

> Los paths indexados tipo `answers[0].selectedOptionCodes` **solo** aparecen para errores crudos de deserialización/model-binding (payload malformado), no para estas validaciones de negocio. Regla práctica: mapeen la key directo al control por `fieldKey`; los buckets fijos (`answers`/`personnelFile`/`form`/`submit`) van a un banner o error general del formulario.

---

## 3. `separationType` en la submission — origen y valores

1. **Origen:** se **deriva** de la **categoría de retiro** del empleado. Es el enum `RetirementSeparationType` de `RetirementCategoryCatalogItem`; el backend lo captura del snapshot al guardar la submission (mapeo categoría → tipo en la tabla §1.1).
2. **Valores posibles** (enum string, serializado en MAYÚSCULAS): **`VOLUNTARIA`**, **`INVOLUNTARIA`**, **`OTRA`**, o **`null`** (si el empleado no tiene categoría de retiro registrada).
3. ✅ **Es de solo lectura.** `SaveExitInterviewSubmissionRequest` = `{ answers[], submit }` — el FE no envía ni puede setear `separationType`. Úsenlo solo para **display/filtros** en la pantalla de análisis RRHH.

---

## 4. Concurrencia de la submission (`PUT …/exit-interview/submission`)

1. ✅ **Confirmado: NO usa optimistic concurrency.** El endpoint **no** declara `If-Match` y `SaveExitInterviewSubmissionRequest` **no** lleva `concurrencyToken`. La unicidad es **single-active-submission por expediente + estado**:
   - Existe un borrador → re-guardar lo **actualiza** (reemplaza respuestas).
   - Ya fue enviada → **`409 EXIT_INTERVIEW_SUBMISSION_ALREADY_SUBMITTED`**.
   - Formulario anónimo → siempre crea submission nueva (no reanudable).
2. **No necesitan mandar `If-Match`.** La respuesta **sí** incluye `concurrencyToken` (y `ETag`), pero es **informativo**: el servidor no lo exige ni lo valida en el `PUT`. Pueden ignorarlo para este endpoint.

> El `If-Match` real **solo** aplica al **diseño de formularios** (`PUT …/definition`, `PUT …/reason`, y `publish`/`reopen`/`archive`), no a la submission.

---

## 5. Permission-codes, recurso y reglas de autoservicio

### 5.1 Strings exactos — ✅ los tres tal cual los asumieron

| Acción | code exacto |
| --- | --- |
| Diseño de formularios | ✅ `PersonnelFiles.ManageExitInterviewForms` |
| Llenar entrevista | ✅ `PersonnelFiles.ManageExitInterviews` |
| Leer submissions (RRHH) | ✅ `PersonnelFiles.ViewExitInterviews` |

`Admin` y `ManageAdministration` son superset de los tres.

### 5.2 Recurso

- ✅ **Los tres cuelgan del recurso `PersonnelFiles`** (`RESOURCE_KEYS` = `PERSONNEL_FILES`), igual que `Manage/ViewMedicalClaims`. **El diseño de formularios NO es un recurso/screen propio.** Internamente son 2 controllers dedicados (`ExitInterviewFormsController`, `ExitInterviewsController`) bajo el mismo recurso.

### 5.3 Autoservicio y anonimato

- **a)** ✅ El **propio empleado** puede `GET …/exit-interview` y `PUT …/submission` sobre **su** expediente **sin** `ManageExitInterviews` (autoservicio resuelto en backend vía `IsSelf` — compara el usuario logueado con el `LinkedUser` del expediente). **Un tercero sin permiso → `403`.**
- **b)** ✅ La **lectura de submissions** (`GET /api/v1/exit-interviews` y `GET /api/v1/exit-interviews/{submissionId}`) es **solo RRHH** con `ViewExitInterviews` (o Admin). Jefatura/área → **`403`** (D-14). Nota: incluso el autor de una submission **no** la lee por estas rutas; el empleado consulta la suya por `GET …/personnel-files/{id}/exit-interview`.
- **c)** ✅ **Anonimato:** en submissions anónimas `personnelFilePublicId = null` (se anula explícitamente; no hay vínculo al empleado ni al autor) y **el flujo anónimo exige `submit=true`** — si `submit=false` con formulario anónimo → `400 common.validation` con key `"submit"`.

---

## Apéndice — Rutas exactas confirmadas (para el whitelist del BFF)

```
GET    /api/v1/personnel-files/{publicId}/exit-interview             (self o RRHH)
PUT    /api/v1/personnel-files/{publicId}/exit-interview/submission  (self o ManageExitInterviews)
GET    /api/v1/exit-interviews                                       (ViewExitInterviews)
GET    /api/v1/exit-interviews/{submissionId}                        (ViewExitInterviews)

GET    /api/v1/exit-interview-forms                                  (ManageExitInterviewForms)
GET    /api/v1/exit-interview-forms/{formId}                         (ManageExitInterviewForms)
GET    /api/v1/exit-interview-forms/applicable                       (ManageExitInterviewForms)
POST   /api/v1/exit-interview-forms                                  (ManageExitInterviewForms)
PUT    /api/v1/exit-interview-forms/{formId}/definition              (ManageExitInterviewForms, If-Match)
PUT    /api/v1/exit-interview-forms/{formId}/reason                  (ManageExitInterviewForms, If-Match)
POST   /api/v1/exit-interview-forms/{formId}/publish                 (ManageExitInterviewForms, If-Match)
POST   /api/v1/exit-interview-forms/{formId}/reopen                  (ManageExitInterviewForms, If-Match)
POST   /api/v1/exit-interview-forms/{formId}/archive                 (ManageExitInterviewForms, If-Match)
DELETE /api/v1/exit-interview-forms/{formId}                         (ManageExitInterviewForms, If-Match)
```

Catalog keys a agregar al whitelist del BFF:
- `VALID_REFERENCE_CATALOG_KEYS`: `retirement-categories`, `retirement-reasons`
- `VALID_GENERAL_CATALOG_KEYS`: `form-control-types`
