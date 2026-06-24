# Guía de Integración Frontend — Competencias del Puesto

| | |
|---|---|
| **Tipo de documento** | Guía de integración para Frontend |
| **Audiencia** | Equipo Frontend, QA |
| **Documentos base** | [`docs/business/analisis-competencias-puesto-empleado.md`](../business/analisis-competencias-puesto-empleado.md) (D-01…D-12) · [`docs/technical/plan-tecnico-competencias-puesto.md`](plan-tecnico-competencias-puesto.md) |
| **Rama** | `feature/competencias-puesto-fase1` |
| **Estado** | Implementado — Fase 1. Solución compila; 1911 pruebas unitarias en verde. |
| **Fecha** | 2026-06-24 |
| **Versión API** | `v1` (`/api/v1`) |

---

## 1. Resumen

La opción **"Competencias del puesto"** del expediente del empleado muestra, **agrupadas por tipo (gestión / organizacional / técnica)**, las competencias **esperadas para el puesto asignado** (derivadas de la **matriz de competencias del perfil**, por nivel jerárquico) **combinadas con las notas alcanzadas** del empleado, con la **brecha calculada** (`esperada − alcanzada`), la **fecha de evaluación** y el **historial** por competencia.

**Modelo conceptual (importante para el frontend):**

- La **matriz del perfil** (`JobProfileCompetencyExpectation`, ya existente) define, por competencia, su **tipo**, sus **conductas deseadas** y un **valor esperado** (`expectedValue`) en la **escala de la empresa**.
- El empleado tiene **resultados** (`PersonnelFilePositionCompetencyResult`): cada uno es **una nota alcanzada** para **una competencia esperada (una "expectation" de la matriz)**, en una **fecha**. Se permiten **varias** por competencia (historial).
- La **escala de calificación** es **configurable por empresa** (numérica como 0–100 / 1–5, o discreta como A–F). Las notas esperada y alcanzada se expresan en la **misma** escala y la **brecha** se calcula sobre su valor numérico/ordinal.

> **⚠️ Cambio incompatible (breaking) respecto a la versión anterior del recurso `position-competency-results`.** Antes el recurso aceptaba **texto libre** (`competencyCode`, `desiredBehaviors`, `expectedScore`, `gapScore`…). Ahora el resultado se **vincula a la matriz**: el alta/edición recibe `expectationPublicId` + `achievedScore` + `evaluationDateUtc`; la **brecha** y la **nota esperada** ya **no se envían** (se derivan). No hay datos productivos (D-11), así que no hay migración de datos; el contrato cambió.

---

## 2. Permisos (RBAC)

| Permiso | Uso |
|---|---|
| `PersonnelFiles.ViewCompetencies` | **Lectura** de competencias del puesto (consulta + resultados). `Admin`/`iam.administration.manage` son superset. **Autoservicio:** el empleado **titular** puede leer **sus propias** competencias aunque no tenga el permiso (D-09). |
| `PersonnelFiles.ManageCompetencies` | **Escritura** de resultados (registrar/editar/eliminar notas). `Admin`/`iam.administration.manage` superset. **Solo RRHH** (no hay autoservicio de escritura). |
| `CompetencyFramework.Read` / `CompetencyFramework.Admin` | Leer / administrar la **escala de calificación** y la **matriz** del perfil (módulo comercial `COMPETENCY_FRAMEWORK`). |

Respuestas de autorización: **401** (no autenticado), **403** (sin permiso / no titular), **404** (expediente de otro tenant se reporta como no encontrado).

---

## 3. Endpoints

### 3.1 Consulta principal — "Competencias del puesto"

```
GET /api/v1/personnel-files/{personnelFilePublicId}/position-competencies
```
Permiso: `ViewCompetencies` **o** titular (autoservicio). Solo expedientes **completados** (si no, `422 STATE_RULE_VIOLATION`).

**Respuesta `200`** (`EmployeePositionCompetenciesResponse`):
```json
{
  "personnelFileId": "8f3c…",
  "jobProfilePublicId": "a1b2…",
  "jobProfileCode": "ANL-RH",
  "jobProfileTitle": "Analista de RRHH",
  "hasAssignedPosition": true,
  "groups": [
    {
      "competencyTypePublicId": "c0…",
      "competencyTypeCode": "GESTION",
      "competencyTypeName": "Gestión",
      "competencies": [
        {
          "expectationPublicId": "e1…",
          "competencyPublicId": "k1…",
          "competencyCode": "LIDERAZGO",
          "competencyName": "Liderazgo",
          "occupationalPyramidLevelPublicId": "n1…",
          "occupationalPyramidLevelCode": "JEFATURA",
          "occupationalPyramidLevelName": "Jefatura",
          "occupationalPyramidLevelOrder": 30,
          "behaviorLevelPublicId": "b1…",
          "behaviorLevelCode": "AVANZADO",
          "behaviorLevelName": "Avanzado",
          "expectedEvidence": "Dirige equipos multidisciplinarios.",
          "expectedScore": 4,
          "achievedScore": 3,
          "gapScore": 1,
          "evaluationDateUtc": "2026-03-01T00:00:00Z",
          "desiredBehaviors": ["Comunica la visión", "Delega con seguimiento"],
          "history": [
            { "positionCompetencyResultPublicId": "r2…", "expectedScore": 4, "achievedScore": 3, "gapScore": 1, "evaluationDateUtc": "2026-03-01T00:00:00Z" },
            { "positionCompetencyResultPublicId": "r1…", "expectedScore": 4, "achievedScore": 2, "gapScore": 2, "evaluationDateUtc": "2025-09-01T00:00:00Z" }
          ]
        }
      ]
    }
  ]
}
```

Notas de render:
- **Agrupar por `groups[]`** (tipo) y dentro listar `competencies[]`.
- Por competencia mostrar: **competencia** (`competencyName`), **conductas deseadas** (`desiredBehaviors[]`), **nota esperada** (`expectedScore`), **nota alcanzada vigente** (`achievedScore`, la más reciente), **brecha** (`gapScore`), **fecha** (`evaluationDateUtc`). El **historial** (`history[]`) viene ordenado de más reciente a más antiguo.
- **Competencia esperada aún no evaluada:** `achievedScore`, `gapScore`, `evaluationDateUtc` y `history` vendrán **nulos/vacíos** (mostrar "sin evaluar"). El `expectedScore` puede ser **null** si la matriz no definió valor esperado para esa celda.
- **Empleado sin puesto resoluble:** `hasAssignedPosition = false`, `groups = []` y `jobProfile*` nulos → mostrar estado vacío "sin puesto asignado / sin matriz".
- **Brecha:** sugerencia de semáforo — `gapScore <= 0` (cumple/supera) verde; `> 0` ámbar/rojo según magnitud.

### 3.2 Registro de notas alcanzadas — `position-competency-results`

Mismas rutas que antes (es un **split de autorización**, no cambia la URL), pero **el cuerpo cambió**.

| Verbo | Ruta | Permiso |
|---|---|---|
| `GET` | `…/personnel-files/{id}/position-competency-results` | View o titular |
| `GET` | `…/position-competency-results/{resultId}` | View o titular |
| `POST` | `…/personnel-files/{id}/position-competency-results` | Manage |
| `PUT` | `…/position-competency-results/{resultId}` | Manage |
| `PATCH` | `…/position-competency-results/{resultId}` | Manage |
| `DELETE` | `…/position-competency-results/{resultId}` | Manage |

**Cuerpo de alta (`POST`) / reemplazo (`PUT`)** (`AddPositionCompetencyResultRequest` / `UpdatePositionCompetencyResultRequest`):
```json
{
  "expectationPublicId": "e1…",          // requerido — la competencia esperada de la matriz del puesto
  "achievedScore": 3,                     // requerido — en la escala activa de la empresa
  "evaluationDateUtc": "2026-03-01T00:00:00Z", // requerido, no futura
  "sourceSystem": null,                   // opcional (procedencia, si se importó)
  "sourceReference": null,                // opcional
  "sourceSyncedUtc": null                 // opcional
}
```
- **`expectedScore` y `gapScore` NO se envían** — el backend toma el esperado (snapshot) de la matriz y **calcula la brecha**.
- **`PUT`/`PATCH`/`DELETE` requieren `If-Match`** con el `concurrencyToken` del resultado (faltante → `400`, desfasado → `409`).
- **`POST`** devuelve `201` con `Location` + `ETag` (token inicial).

**Respuesta de un resultado** (`PersonnelFilePositionCompetencyResultResponse`):
```json
{
  "positionCompetencyResultPublicId": "r2…",
  "expectationPublicId": "e1…",
  "competencyPublicId": "k1…",
  "competencyCode": "LIDERAZGO",
  "competencyName": "Liderazgo",
  "competencyTypePublicId": "c0…",
  "competencyTypeCode": "GESTION",
  "competencyTypeName": "Gestión",
  "expectedScore": 4,
  "achievedScore": 3,
  "gapScore": 1,
  "evaluationDateUtc": "2026-03-01T00:00:00Z",
  "sourceSystem": null, "sourceReference": null, "sourceSyncedUtc": null,
  "concurrencyToken": "d4…"
}
```

**`PATCH`** (`application/json-patch+json`, RFC 6902) — campos parchables: `/expectationPublicId`, `/achievedScore`, `/evaluationDateUtc`, `/sourceSystem`, `/sourceReference`, `/sourceSyncedUtc`. (`/expectedScore` y `/gapScore` son **derivados**, no parchables.)
```json
[ { "op": "replace", "path": "/achievedScore", "value": 4 },
  { "op": "replace", "path": "/evaluationDateUtc", "value": "2026-06-01T00:00:00Z" } ]
```

**`DELETE`** devuelve `200` con el token de concurrencia **refrescado del expediente padre** (`{ "parentConcurrencyToken": "…" }`).

### 3.3 Escala de calificación (configurable por empresa)

```
GET /api/v1/companies/{companyId}/competency-rating-scale     (CompetencyFramework.Read)
PUT /api/v1/companies/{companyId}/competency-rating-scale     (CompetencyFramework.Admin)
```

**`GET` → `200`** (`ActiveCompetencyRatingScaleResponse`):
```json
{
  "isConfigured": true,
  "scale": {
    "id": "s1…", "companyId": "co…",
    "code": "ESCALA_1_5", "name": "Escala 1 a 5",
    "scaleType": "Discrete",            // "Numeric" | "Discrete"
    "minValue": null, "maxValue": null, "decimals": 0,
    "isActive": true, "concurrencyToken": "t…",
    "levels": [
      { "id": "l1…", "code": "1", "label": "Deficiente", "value": 1, "sortOrder": 10 },
      { "id": "l5…", "code": "5", "label": "Excelente", "value": 5, "sortOrder": 50 }
    ]
  }
}
```
Toda empresa se aprovisiona con una escala por defecto **1–5 (discreta)**; el frontend puede asumir `isConfigured = true` salvo configuraciones especiales.

**`PUT`** (`SetCompetencyRatingScaleRequest`) — crea o **redefine en sitio** la escala activa:
- **Numérica:** `scaleType: "Numeric"`, `minValue` < `maxValue`, `decimals ≥ 0`, `levels: []`.
  ```json
  { "code": "PORC", "name": "0 a 100", "scaleType": "Numeric", "minValue": 0, "maxValue": 100, "decimals": 0, "levels": [] }
  ```
- **Discreta:** `scaleType: "Discrete"`, **≥ 2** `levels` con `value` **distintos** (el `value` es el ordinal para la brecha).
  ```json
  { "code": "AF", "name": "A a F", "scaleType": "Discrete",
    "levels": [ {"code":"F","label":"Deficiente","value":0,"sortOrder":10},
                {"code":"A","label":"Excelente","value":5,"sortOrder":50} ] }
  ```
Devuelve la escala resultante (`CompetencyRatingScaleResponse`).

### 3.4 Matriz del perfil — campo nuevo `expectedValue`

El editor de matriz existente (`…/job-profiles/{jobProfilePublicId}/competency-matrix/items`) ahora acepta/devuelve **`expectedValue`** (decimal, opcional) en cada celda — la **nota esperada** en la escala de la empresa. El frontend del **editor de matriz** debe agregar este campo (numérico, validado contra la escala activa). Es lo que alimenta el `expectedScore` de la consulta del empleado.

---

## 4. Validaciones y errores

| Situación | HTTP | Código |
|---|---|---|
| `expectationPublicId`/`achievedScore`/`evaluationDateUtc` faltantes; fecha **futura** | **400** | `common.validation` |
| Escala mal definida (numérica min≥max, discreta <2 niveles o valores repetidos) | **400** | `common.validation` |
| La expectativa no existe en la empresa | **422** | `POSITION_COMPETENCY_EXPECTATION_INVALID` |
| La competencia **no pertenece** a la matriz del **puesto asignado** del empleado | **422** | `POSITION_COMPETENCY_NOT_IN_PROFILE` |
| No hay **escala activa** configurada | **422** | `POSITION_COMPETENCY_SCALE_NOT_CONFIGURED` |
| `achievedScore` **fuera de la escala** activa | **422** | `POSITION_COMPETENCY_SCORE_OUT_OF_RANGE` |
| Expediente **no completado** | **422** | `STATE_RULE_VIOLATION` |
| `If-Match` ausente / desfasado | **400 / 409** | — / `CONCURRENCY_CONFLICT` |
| Sin permiso (y no titular) | **403** | `PERSONNEL_FILES_FORBIDDEN` |

Mensajes **bilingües** (ES/EN) por `Accept-Language`.

---

## 5. Flujos sugeridos para el Frontend

**A) Pantalla de consulta (lectura):**
1. `GET …/position-competencies`.
2. Si `hasAssignedPosition=false` → estado vacío.
3. Render por **tipo** → competencias con esperada/alcanzada/brecha/fecha + conductas + historial.

**B) Registrar una evaluación (RRHH):**
1. La consulta (A) ya entrega las competencias esperadas con su `expectationPublicId`.
2. El usuario elige una competencia y captura **nota alcanzada** (en la escala — usar `GET competency-rating-scale` para construir el control: rango numérico o select de `levels`) y **fecha** (no futura).
3. `POST …/position-competency-results` con `{ expectationPublicId, achievedScore, evaluationDateUtc }`.
4. La brecha aparece calculada al releer (A) o en la respuesta del recurso.

**C) Editar / eliminar:** usar el `concurrencyToken` del resultado en `If-Match` (`PUT`/`PATCH`/`DELETE`).

**D) Configurar la escala (Admin de competencias):** `GET` para precargar, `PUT` para guardar (numérica o discreta).

---

## 6. Notas de despliegue

- **Migración EF** `20260624051135_CompetencyRatingScaleAndPositionCompetencyResultRestructure` (crea `competency_rating_scales`/`…_levels`, agrega `expected_value` a la matriz, reestructura `personnel_file_position_competency_results`). **D-11: sin datos productivos → drop & recreate**; vaciar la tabla de resultados en entornos no productivos antes de aplicar.
- **Seed por-tenant:** al aprovisionar una empresa se siembran los **tipos de competencia** (`GESTION`/`ORGANIZACIONAL`/`TECNICA`) y una **escala 1–5** activa. Tenants existentes (si los hubiera) requieren backfill de estos defaults.
- Para que la consulta muestre competencias, el perfil del puesto debe tener **matriz** cargada (con `expectedValue`) y el empleado una **plaza/asignación vigente** (activa y principal).
