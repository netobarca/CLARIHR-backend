# Guía de Integración Frontend — Entrevista de Retiro (Exit Interview)

| | |
|---|---|
| **Módulo** | Entrevista de retiro (exit interview) — Fase 1 |
| **Estado backend** | Implementado en `feature/entrevista-retiro-fase1` (1966 tests unitarios verdes). |
| **Decisiones** | `docs/business/analisis-entrevista-retiro-empleado.md` (D-01…D-14, RQ-01…RQ-06) |
| **Plan técnico** | `docs/technical/plan-tecnico-entrevista-retiro.md` |
| **País de referencia** | El Salvador (`SV`) |
| **Errores** | Bilingües (ES/EN); el `code` viene en el `ProblemDetails`. |

> Convenciones generales del API CLARIHR:
> - Auth: `Authorization: Bearer <jwt>` en todas las llamadas.
> - Concurrencia optimista: las escrituras sobre un recurso existente exigen `If-Match: "<concurrencyToken>"`; la respuesta devuelve el nuevo token en `ETag` y en el cuerpo (`concurrencyToken`).
> - Errores: `application/problem+json` con `code` (string), `title`, `status` y, para validaciones, `errors` por campo.
> - Todos los recursos son **multi-tenant** (resueltos por el token); IDs públicos son `Guid`.

---

## 0. Modelo mental

El módulo tiene **dos mitades**:

1. **Diseño (RRHH):** construir formularios dinámicos (grupos + campos + opciones), publicarlos, asociar **cada formulario a UN motivo de retiro** (uno activo por motivo). Permiso: `PersonnelFiles.ManageExitInterviewForms`.
2. **Llenado (empleado/RRHH):** el empleado que se retira abre la entrevista aplicable a su **motivo de baja** y la responde (autoservicio); RRHH puede capturarla y **leer** las respuestas. Permisos: `PersonnelFiles.ManageExitInterviews` (llenar) y `PersonnelFiles.ViewExitInterviews` (leer — solo RRHH).

Reglas clave:
- El **motivo de retiro** es un catálogo jerárquico **categoría → motivo** (país-scoped). Un formulario se asocia a **un motivo**.
- **Anonimato a nivel de formulario:** si el formulario es anónimo, la submission **no** se vincula al empleado.
- **Score 0–100** ponderado, derivado de los pesos de campo y puntajes de opción/escala.
- La entrevista es **opcional**: si el motivo no tiene formulario activo, no hay entrevista.

---

## 1. Catálogos (lectura, autenticado)

### 1.1 Motivo de retiro (categorías y motivos)

```
GET /api/v1/reference-catalogs/retirement-categories?countryCode=SV
GET /api/v1/reference-catalogs/retirement-reasons?countryCode=SV&parentCode={CATEGORY_CODE}
```
Respuesta (cada item): `{ "id": "<guid>", "code": "RENUNCIA_VOLUNTARIA", "name": "Renuncia voluntaria", "sortOrder": 10 }`

- `retirement-reasons` se filtra por `parentCode` = código de categoría (jerarquía categoría → motivo).
- Seed SV incluido (categorías: `VOLUNTARIA`, `JUBILACION`, `INVOLUNTARIA`, `ABANDONO`, `NO_SUPERA_PERIODO_PRUEBA`, `FIN_CONTRATO`, `MUTUO_ACUERDO`, `FALLECIMIENTO`; motivos como `MEJOR_OFERTA_SALARIAL`, `BAJO_DESEMPENO`, etc.).
- Estos códigos se registran en la **baja del empleado** (PUT `…/employment-information`, campos `retirementCategoryCode` / `retirementReasonCode`), ya validados por catálogo.

### 1.2 Tipos de control de campo

```
GET /api/v1/general-catalogs/form-control-types?countryCode=SV
```
Conjunto **cerrado** de 9 tipos. Sus capacidades (para renderizar y validar) son fijas:

| code | Valor | Opciones | Rango (min/máx) | Múltiple | Puntúa |
|---|---|---|---|---|---|
| `TEXTO_CORTO` | texto | no | no | no | no |
| `TEXTO_LARGO` | texto | no | no | no | no |
| `NUMERO` | número | no | sí | no | no |
| `FECHA` | fecha | no | no | no | no |
| `LISTA_DESPLEGABLE` | opciones | sí | no | no | sí |
| `OPCION_UNICA` | opciones | sí | no | no | sí |
| `SELECCION_MULTIPLE` | opciones | sí | no | sí | sí |
| `CASILLA` | booleano | no | no | no | no |
| `ESCALA` | número (Likert) | no | sí (`scaleMax`) | no | sí |

---

## 2. Diseño de formularios (RRHH — `ManageExitInterviewForms`)

### 2.1 Crear (cabecera)
```
POST /api/v1/exit-interview-forms
{ "name": "Entrevista de salida estándar", "description": "...", "isAnonymous": false }
→ 201 ExitInterviewFormResponse (Location + ETag)
```

### 2.2 Guardar la definición completa (upsert anidado)
```
PUT /api/v1/exit-interview-forms/{formId}/definition
If-Match: "<concurrencyToken>"
{
  "name": "Entrevista de salida estándar",
  "description": "...",
  "isAnonymous": false,
  "groups": [
    { "groupKey": "G1", "title": "Motivos de salida", "description": null, "displayOrder": 10 }
  ],
  "fields": [
    {
      "groupKey": "G1",
      "controlTypeCode": "LISTA_DESPLEGABLE",
      "fieldKey": "motivo_principal",
      "title": "¿Cuál fue tu motivo principal?",
      "description": null,
      "weight": 3,
      "isRequired": true,
      "displayOrder": 10,
      "minValue": null, "maxValue": null, "maxLength": null, "scaleMax": null,
      "options": [
        { "optionCode": "SALARIO", "label": "Mejor salario", "score": 0, "displayOrder": 10 },
        { "optionCode": "CRECIMIENTO", "label": "Crecimiento", "score": 50, "displayOrder": 20 }
      ]
    },
    {
      "groupKey": "G1",
      "controlTypeCode": "ESCALA",
      "fieldKey": "satisfaccion",
      "title": "Satisfacción general (1–5)",
      "weight": 2, "isRequired": true, "displayOrder": 20,
      "scaleMax": 5,
      "options": []
    }
  ]
}
→ 200 ExitInterviewFormResponse
```
Notas:
- `groupKey` es una **clave del payload** para enlazar `fields` a su grupo (no es un id de BD). Un campo sin `groupKey` queda a nivel raíz.
- Solo se puede guardar la definición en estado **Draft**.
- `weight ≥ 0`; `fieldKey` patrón `[A-Za-z0-9_]`; los campos de selección (`LISTA_DESPLEGABLE`/`OPCION_UNICA`/`SELECCION_MULTIPLE`) llevan `options`; los de rango (`NUMERO`/`ESCALA`) pueden llevar `minValue`/`maxValue`/`scaleMax`.
- El **anonimato** se fija al publicar; no se cambia luego para esa versión.

### 2.3 Ciclo de vida
```
POST /api/v1/exit-interview-forms/{formId}/publish    If-Match  → valida (≥1 campo, coherencia) y publica
POST /api/v1/exit-interview-forms/{formId}/reopen     If-Match  → vuelve a Draft (nueva versión) para editar
POST /api/v1/exit-interview-forms/{formId}/archive    If-Match  → archiva (sale de uso)
PUT  /api/v1/exit-interview-forms/{formId}/reason     If-Match  { "reasonCode": "MEJOR_OFERTA_SALARIAL" }  → asocia a 1 motivo (single-active)
DELETE /api/v1/exit-interview-forms/{formId}          If-Match  → desactiva (soft-delete)
```
- **Publicar** exige que la definición sea coherente (≥1 campo; selección con opciones; rangos válidos).
- **Asociar a motivo** requiere que el formulario esté **Published**; activa este formulario para el motivo y **desactiva** el que estuviera activo para ese mismo motivo (single-active, D-03).
- **Reabrir** desasocia el formulario de su motivo (deja de ser el activo) y permite editarlo; las submissions previas conservan su snapshot de versión.

### 2.4 Consulta y resolución
```
GET /api/v1/exit-interview-forms?status=Published&reasonCode=&search=   → lista (ExitInterviewFormListItemResponse[])
GET /api/v1/exit-interview-forms/{formId}                               → definición completa
GET /api/v1/exit-interview-forms/applicable?reasonCode={CODE}           → { "hasForm": true|false, "form": {…}|null }
```

---

## 3. Llenado de la entrevista

### 3.1 Abrir la entrevista del empleado (autoservicio o RRHH)
```
GET /api/v1/personnel-files/{personnelFilePublicId}/exit-interview
→ {
    "hasForm": true,
    "form": ExitInterviewFormResponse | null,
    "currentSubmission": ExitInterviewSubmissionResponse | null
  }
```
- Resuelve el formulario aplicable por el **motivo de baja** del empleado. Si el empleado no tiene motivo o el motivo no tiene formulario activo → `hasForm=false`.
- `currentSubmission` trae el borrador/entrevista actual (solo para formularios **no anónimos**; los anónimos no se pueden vincular ni reanudar).
- Acceso: el **propio empleado** (su expediente) o RRHH.

### 3.2 Guardar / enviar
```
PUT /api/v1/personnel-files/{personnelFilePublicId}/exit-interview/submission
{
  "answers": [
    { "fieldKey": "motivo_principal", "selectedOptionCodes": ["CRECIMIENTO"] },
    { "fieldKey": "satisfaccion", "valueNumber": 4 },
    { "fieldKey": "comentarios", "valueText": "…" }
  ],
  "submit": true
}
→ 200 ExitInterviewSubmissionResponse (ETag)
```
- `submit=false` → guarda **borrador** (reanudable; solo en formularios no anónimos). `submit=true` → valida obligatorios y calcula el **score 0–100**.
- Por respuesta, según el tipo de control: `valueText` / `valueNumber` / `valueDate` / `valueBool` / `selectedOptionCodes[]`.
- Una sola submission activa por empleado+baja: re-guardar actualiza el borrador; una vez **enviada** no se puede modificar (error `EXIT_INTERVIEW_SUBMISSION_ALREADY_SUBMITTED`).
- **Anónimo:** si el formulario es anónimo, la submission se guarda **sin** vínculo al empleado y **debe** enviarse directo (`submit=true`; no admite borrador).
- Acceso: el **propio empleado** (autoservicio) o RRHH.

### 3.3 Lectura de respuestas (solo RRHH — `ViewExitInterviews`)
```
GET /api/v1/exit-interviews?reasonCode=&period=YYYY-MM     → lista (ExitInterviewSubmissionListItemResponse[])
GET /api/v1/exit-interviews/{submissionId}                 → detalle con respuestas
```
- **Solo RRHH** (D-14); jefatura/área no accede (403). Las submissions anónimas se listan/leen **sin** vínculo al empleado (`personnelFileId = null`).

---

## 4. Contratos (formas)

**ExitInterviewFormResponse**
```
{ id, name, description, isAnonymous, status: "Draft"|"Published"|"Archived", version,
  retirementReasonCode, isActiveForReason, isActive, concurrencyToken,
  groups: [{ id, title, description, displayOrder }],
  fields: [{ id, groupId, controlTypeCode, fieldKey, title, description, weight, isRequired,
             displayOrder, minValue, maxValue, maxLength, scaleMax, isActive,
             options: [{ id, optionCode, label, score, displayOrder, isActive }] }] }
```

**ExitInterviewSubmissionResponse**
```
{ id, formId, formVersion, isAnonymous, personnelFileId, status: "Draft"|"Submitted"|"Archived",
  retirementReasonCode, retirementCategoryCode, separationType, period: "YYYY-MM",
  submittedUtc, totalScore, concurrencyToken,
  answers: [{ id, fieldKey, title, controlTypeCode, valueText, valueNumber, valueDate, valueBool,
              selectedOptionCodes: [..], normalizedScore }] }
```

---

## 5. Score (cómo se calcula)

- Solo puntúan los campos de **selección** (con `score` por opción) y **escala** (`ESCALA`).
- Normalización a 0–100: escala → `(valor − 1)/(scaleMax − 1)×100`; selección → promedio de los `score` de las opciones elegidas.
- Índice de la submission = **promedio ponderado** `Σ(peso × scoreNorm) / Σ(peso)` sobre los campos puntuables respondidos (mayor = experiencia más favorable). Si no hay campos puntuables → `totalScore = null`.

---

## 6. Errores específicos (code)

| code | Cuándo |
|---|---|
| `EXIT_INTERVIEW_FORM_NAME_DUPLICATE` | nombre de formulario repetido |
| `EXIT_INTERVIEW_FORM_NOT_DRAFT` | editar definición de un formulario no-Draft |
| `EXIT_INTERVIEW_FORM_NOT_PUBLISHED` | asociar motivo / reabrir un formulario no publicado |
| `EXIT_INTERVIEW_FORM_NOT_PUBLISHABLE` | publicar sin campos |
| `EXIT_INTERVIEW_FIELD_KEY_DUPLICATE` | dos campos con la misma clave |
| `EXIT_INTERVIEW_OPTION_CODE_DUPLICATE` | dos opciones con el mismo código en un campo |
| `EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED` | publicar un campo de selección sin opciones |
| `EXIT_INTERVIEW_FIELD_OPTIONS_NOT_ALLOWED` / `EXIT_INTERVIEW_OPTIONS_NOT_ALLOWED_ON_FIELD` | opciones en un tipo que no las admite |
| `EXIT_INTERVIEW_FIELD_RANGE_INVALID` / `EXIT_INTERVIEW_FIELD_RANGE_NOT_ALLOWED` | rango incoherente / no admitido |
| `EXIT_INTERVIEW_CONTROL_TYPE_INVALID` | tipo de control fuera de catálogo |
| `EXIT_INTERVIEW_FORM_NOT_FOUND` / `EXIT_INTERVIEW_SUBMISSION_NOT_FOUND` | recurso inexistente |
| `EXIT_INTERVIEW_FORM_CONCURRENCY_CONFLICT` | `If-Match` desactualizado en el formulario |
| `EXIT_INTERVIEW_SUBMISSION_ALREADY_SUBMITTED` | re-guardar una entrevista ya enviada |
| `common.validation` | validaciones de campo (motivo sin formulario, opción inválida, obligatorio faltante, rango, etc.) |

---

## 7. Flujos de ejemplo

**RRHH — preparar el instrumento**
1. `POST /exit-interview-forms` (cabecera, `isAnonymous`).
2. `PUT /exit-interview-forms/{id}/definition` (grupos/campos/opciones) — repetir hasta estar conforme.
3. `POST /exit-interview-forms/{id}/publish`.
4. `PUT /exit-interview-forms/{id}/reason` con el `reasonCode` (queda activo para ese motivo).

**Empleado — llenar (autoservicio)**
1. `GET /personnel-files/{me}/exit-interview` → si `hasForm`, renderizar `form`.
2. (opcional) `PUT …/exit-interview/submission` con `submit=false` para guardar borrador.
3. `PUT …/exit-interview/submission` con `submit=true` para enviar.

**RRHH — analizar**
1. `GET /exit-interviews?period=2026-06` → lista.
2. `GET /exit-interviews/{id}` → detalle (respeta anonimato).

> **Fase 2 (no incluida):** tabulación/analítica de causas de rotación (por motivo/categoría/área=plaza/periodo/score, con exportación) y corrección/anulación de submissions por RRHH.
```
