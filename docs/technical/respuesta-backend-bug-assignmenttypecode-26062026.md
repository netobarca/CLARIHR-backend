# Respuesta backend — Bug `assignmentTypeCode` obligatorio vs. contrato (assigned-positions)

| | |
| --- | --- |
| **Para** | Equipo Frontend |
| **De** | Equipo Backend (.NET API) |
| **Endpoint** | `POST` / `PUT` / `PATCH` `/api/v1/personnel-files/{publicId}/assigned-positions[/{id}]` |
| **Ref. reporte** | "Bug Backend — `assignmentTypeCode` obligatorio en runtime pero `nullable` en el contrato" (2026-06-25) |
| **Fecha** | 2026-06-26 |
| **Estado** | ✅ **Resuelto en backend** (código + contrato + catálogo). Pendiente: ajustes menores en el FE (ver §6). |

---

## TL;DR

Tenían razón: **había una inconsistencia del backend**. Pero la corrección **no** fue quitar la validación — el campo es **obligatorio por diseño** (es un código de catálogo, no texto libre). Lo que estaba mal era el **contrato publicado (OpenAPI)**, que declaraba el campo `nullable`/opcional cuando el runtime **siempre** lo exigió.

**Alineamos el contrato a la realidad:** `assignmentTypeCode` y `positionSlotPublicId` ahora aparecen como `required` y **sin** `nullable` en el OpenAPI.

> Respuesta a la pregunta central — **¿`assignmentTypeCode` debe ser obligatorio? → ✅ SÍ.** Estamos en la rama "✅ Sí es obligatorio" de su árbol de decisión.

---

## 1. Causa raíz (por qué runtime y contrato no coincidían)

El runtime exigía el campo por **tres mecanismos independientes** — por eso es claramente intencional:

| Capa | Qué hace | Qué error produce |
| --- | --- | --- |
| 1. Model-binding de ASP.NET | `assignmentTypeCode` es un `string` **no-nullable** → `[Required]` **implícito** | El `400` exacto que vieron: `"The AssignmentTypeCode field es requerido."` |
| 2. FluentValidation | `NotEmpty()` sobre el campo | `400` de validación (backstop) |
| 3. Validación de catálogo | El código debe existir y estar activo en el catálogo `assignment-types` | `422 EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID` |

**¿Por qué el OpenAPI decía lo contrario?** El generador (Swashbuckle) no estaba configurado para reflejar la no-nulabilidad de C#, así que emitía todo `string` como `nullable: true` y **nunca** generaba el array `required`. El contrato describía "lo que Swashbuckle veía por defecto", no "lo que el runtime validaba". Era una inconsistencia real del backend — la corregimos.

---

## 2. Qué cambiamos en el backend (3 fases)

**Fase 1 — Contrato honesto (lo que les desbloquea).**
Marcamos `assignmentTypeCode` y `positionSlotPublicId` como obligatorios en los request models (`POST`/`PUT`/`PATCH`) y ajustamos el generador para que el OpenAPI los publique como `required` y **sin** `nullable`. **Sin cambio de comportamiento en runtime** — el runtime ya los exigía; solo el contrato se puso al día.

**Fase 0 — Catálogo garantizado en todos los entornos.**
El catálogo `assignment-types` (las 9 opciones SV) antes se sembraba **solo en dev**. Lo movimos al pipeline de migraciones para que exista en **todos** los entornos (staging/prod incluidos). Esto evita que, al volver el campo obligatorio, el `<select>` "Tipo" quede vacío fuera de dev y bloquee el alta.

**Fase 2 — Corrección sistémica (raíz del problema, toda la API).**
Activamos en Swashbuckle el reflejo de los tipos no-nullable de C#: **cualquier** campo de request no-nullable ahora se publica como `required`/no-nullable en el OpenAPI (y los `?` siguen opcionales). Esto cierra la **clase entera** de la discrepancia que reportaron, no solo este endpoint. Ver §5.

---

## 3. Respuestas directas a sus 5 preguntas

1. **¿`assignmentTypeCode` debe ser obligatorio?** → ✅ **SÍ.**

2. **Si SÍ es obligatorio:**
   - ✅ Corregimos el **OpenAPI** (`required` + sin `| null`).
   - ✅ Actualizamos la guía (`multi-plaza-frontend-integration.md`, §5/§7/§8).
   - ⏳ El FE agrega `required: true` + validación en el combobox "Tipo".
   - **¿Aplica al `PUT`?** → ✅ **SÍ.** Y también al **`PATCH`** — los tres comparten el mismo validador y la misma validación de catálogo.

3. **Si NO es obligatorio** → No aplica (es obligatorio por diseño).

4. **¿Otros campos con la misma discrepancia?** → ✅ **SÍ**, ver §5. El más relevante: **`positionSlotPublicId`** (mismo bug; no lo pegaban porque siempre lo enviaban). El resto del body de assigned-positions estaba correcto. A nivel de toda la API era **sistémico** → lo cerramos con la Fase 2.

5. **Workaround temporal** (elegir un "Tipo" antes de guardar) → era correcto como paliativo. Deja de ser necesario una vez que el combobox sea `required` y se llene del catálogo.

---

## 4. El contrato: antes vs. después

**Antes** (lo que motivó el reporte):

```yaml
AddEmploymentAssignmentRequest:
  type: object                 # ❌ sin array `required`
  properties:
    assignmentTypeCode:
      type: string
      nullable: true           # ❌ aparecía opcional/nullable
    positionSlotPublicId:
      type: string
      format: uuid
      nullable: true           # ❌ idem (bug latente)
```

**Después**:

```yaml
AddEmploymentAssignmentRequest:
  required:
    - assignmentTypeCode       # ✅
    - positionSlotPublicId     # ✅
  type: object
  properties:
    assignmentTypeCode:
      minLength: 1
      type: string             # ✅ ya no `nullable`
    positionSlotPublicId:
      type: string
      format: uuid             # ✅ ya no `nullable`
    contractTypeCode:
      type: string
      nullable: true           # (sigue opcional — correcto)
```

Idéntico para `UpdateEmploymentAssignmentRequest` (`PUT`).

> **El `docs/technical/api/openapi.yaml` del repo ya se regeneró por completo** desde el API en vivo: ahora refleja **exactamente** el contrato actual (**272 paths · 559 schemas**), no solo estas dos schemas. El archivo venía desfasado de varias versiones (le faltaban módulos enteros ya desplegados y conservaba rutas muertas). Hay cambios de contrato que deben absorber al regenerar su cliente — ver **§8**.

---

## 5. Otros campos (su pregunta #4) — revisados

**En este endpoint:**

| Campo | ¿Obligatorio real? | Antes en OpenAPI | Ahora |
| --- | --- | --- | --- |
| `assignmentTypeCode` | **SÍ** | nullable, sin required | ✅ required |
| `positionSlotPublicId` | **SÍ** | nullable, sin required | ✅ required |
| `startDate` | sí | (value type) | required |
| `contractTypeCode`, `workdayCode`, `payrollTypeCode`, `paymentMethodCode` | no | nullable | nullable (correcto) |
| `orgUnitPublicId`, `workCenterPublicId`, `costCenterPublicId`, `paymentBankAccountPublicId`, `endDate`, `notes` | no | nullable | nullable (correcto) |

**A nivel de toda la API (Fase 2):** la discrepancia "campo no-nullable que aparecía opcional" existía en **decenas** de DTOs (todo `string` no-nullable de un request). La Fase 2 los corrige de raíz. **Consecuencia para ustedes:** al regenerar su cliente desde el OpenAPI verán **más campos marcados `required`** en otros endpoints. Eso es **correcto** — el backend ya los exigía; antes el contrato no lo decía. Si algún `required` nuevo les sorprende, es porque el runtime ya devolvía `400`/`422` ante su ausencia.

---

## 6. Qué les toca al FE

- [ ] **Combobox "Tipo":** marcar `assignmentTypeCode` como `required: true` + validación. Poblarlo desde `GET /api/v1/general-catalogs/assignment-types?countryCode=SV` (9 opciones SV, ya seedeadas en todos los entornos — ver §7). **No** input libre.
- [ ] **`positionSlotPublicId`:** mantenerlo `required: true` (ya lo trataban así).
- [ ] **Quitar el workaround** manual (ya no hace falta).
- [ ] **Regenerar el cliente** desde el OpenAPI actualizado y absorber los nuevos `required` (ver §5).
- [ ] **Dejar de usar** la guía vieja `employment-assignments 1.md` (13-jun, previa a multi-plaza). Fuente vigente: **`docs/business/multi-plaza-frontend-integration.md`**.

**Aclaración de errores** (para el mapeo de UI):
- `assignmentTypeCode` / `positionSlotPublicId` **ausente o `null`** → **`400`** de validación (campo requerido).
- `assignmentTypeCode` **con un valor que no está en el catálogo** → **`422 EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID`**.

---

## 7. Catálogo de tipos de asignación

`GET /api/v1/general-catalogs/assignment-types?countryCode=SV` →

```jsonc
[
  { "code": "LEY_SALARIOS",            "name": "Ley de Salarios",        "isActive": true, "sortOrder": 10 },
  { "code": "CONTRATO",                "name": "Contrato",               "isActive": true, "sortOrder": 20 },
  { "code": "INDEFINIDO",              "name": "Tiempo indefinido",      "isActive": true, "sortOrder": 30 },
  { "code": "PLAZO_FIJO",              "name": "Plazo fijo",             "isActive": true, "sortOrder": 40 },
  { "code": "INTERINO",                "name": "Interinato",             "isActive": true, "sortOrder": 50 },
  { "code": "POR_OBRA",                "name": "Por obra o servicio",    "isActive": true, "sortOrder": 60 },
  { "code": "AD_HONOREM",              "name": "Ad honorem",             "isActive": true, "sortOrder": 70 },
  { "code": "SERVICIOS_PROFESIONALES", "name": "Servicios profesionales","isActive": true, "sortOrder": 80 },
  { "code": "RECARGO_FUNCIONES",       "name": "Recargo de funciones",   "isActive": true, "sortOrder": 90 }
]
```

Antes este catálogo solo se sembraba en dev (riesgo de quedar vacío fuera de dev); ahora se siembra vía migración en **todos** los entornos. El `<select>` siempre tendrá estas opciones.

---

## 8. Contrato OpenAPI regenerado por completo — qué absorber al regenerar el cliente

Aprovechando este fix, **regeneramos `openapi.yaml` de punta a punta** desde el API en vivo, porque venía desfasado varias versiones. El nuevo contrato es 100 % fiel al runtime. Al regenerar su cliente, esto es lo que cambia:

### 8.1 Higiene de nombres de schema (causa de fondo del desfase)
El generador estaba configurado para nombrar los schemas con el **namespace interno completo** (p. ej. `CLARIHR.Api.Contracts.PersonnelFiles.AddEmploymentAssignmentRequest`), lo que ensuciaba el contrato en vivo y lo alejaba del `.yaml` documentado (que usaba nombres cortos). Lo corregimos: **ahora el contrato usa nombres cortos y limpios** (`AddEmploymentAssignmentRequest`). Con esto, el `.yaml` del repo y el OpenAPI en vivo del API **coinciden exactamente** — ya no hay "doc vs runtime".

### 8.2 Renombrado sistemático de tipos genéricos (acción requerida en el FE)
Los tipos genéricos cambian de convención de nombre `XOfY` → `YX`. **El payload/forma NO cambia; solo el nombre de la clase generada.** Ejemplos:

| Antes (cliente actual) | Ahora |
| --- | --- |
| `PagedResponseOfPersonnelFileListItemResponse` | `PersonnelFileListItemResponsePagedResponse` |
| `PagedResponseOfJobProfileListItemResponse` | `JobProfileListItemResponsePagedResponse` |
| `JsonPatchDocumentOfPatchPersonnelFileRequest` | `PatchPersonnelFileRequestJsonPatchDocument` |
| `OperationOfPatchPersonnelFileRequest` | `PatchPersonnelFileRequestOperation` |

Afecta a **todas** las respuestas paginadas (`PagedResponseOf…`, ~35 tipos) y a **todos** los cuerpos de `PATCH` JSON Patch. Al regenerar el cliente, las clases se renombran solas; solo deben actualizar las referencias en su código.

### 8.3 Rutas eliminadas del contrato (ya no existían en el API)
Estas rutas ya **no** estaban en el backend (refactor multi-plaza + simplificación de perfil); el `.yaml` viejo las conservaba por error. **Migración FE:**

| Ruta eliminada | Reemplazo |
| --- | --- |
| `…/employment-assignments[/{id}]` | **`…/assigned-positions[/{id}]`** (este mismo endpoint del bug) |
| `…/payment-methods[/{id}]` | Campo **`paymentMethodCode`** en la asignación + catálogo `GET /api/v1/general-catalogs/payment-methods` |
| `…/salary-items[/{id}]` | Eliminado (la compensación vive en `…/compensation-concepts`) |
| `…/employee-profile` | Plegado en el shell del expediente (`GET/PUT …/personnel-files/{publicId}`) |

### 8.4 Módulos nuevos ahora documentados (antes faltaban)
El contrato viejo **no** incluía módulos ya desplegados. Ahora sí (36 rutas nuevas), entre ellos: **entrevista de retiro** (`/exit-interview-forms`, `/exit-interviews`), **transacciones fuera de nómina** (`…/off-payroll-transactions`), **conceptos de compensación** (`…/compensation-concepts`), **escalas de calificación de competencias** (`…/competency-rating-scale`), **competencias del puesto del empleado** (`…/position-competencies`), **tipos de centro de costo** (`/cost-center-types`), **tramos de renta** (`/income-tax-brackets`) y **documentos de reclamos médicos**.

> Recomendación: regeneren el cliente desde el `openapi.yaml` actualizado en un branch, revisen el renombrado de §8.2 y los reemplazos de §8.3, y prueben los flujos afectados.

---

## Resumen de cambios en el backend (para trazabilidad)

| Archivo | Cambio |
| --- | --- |
| `GlobalCatalogSeedData.cs` + `GeneralCatalogItemConfiguration.cs` + migración `…SeedAssignmentTypeCatalogForElSalvador` | Fase 0: seed de `assignment-types` vía `HasData` (todos los entornos); removido de `DevSeedService`. |
| `PersonnelFileRequests.cs` | Fase 1: `[Required]` (en el **parámetro** del record, no `[property:]`) en `assignmentTypeCode` + `positionSlotPublicId` (`Add`/`Update`/`Patch`/`Rehire`). |
| `PublicContractSchemaFilter.cs` | Fase 1: publica como `required`/no-nullable los campos con `[Required]` en la propiedad **o en el parámetro del constructor** (records posicionales). |
| `Program.cs` (`AddSwaggerGen`) | Fase 2: `SupportNonNullableReferenceTypes()` (campos no-nullable → `required`/no-nullable). **Además** se quitó el override `CustomSchemaIds(type.FullName)` que filtraba el namespace interno en cada nombre de schema → contrato con nombres cortos y limpios, y `.yaml` == API en vivo (§8.1). |
| `docs/technical/api/openapi.yaml` | **Regenerado por completo** desde el API en vivo (272 paths · 559 schemas): +36 rutas de módulos ya desplegados, −7 rutas muertas, nombres de schema normalizados (§8). |
| `docs/business/multi-plaza-frontend-integration.md` | Guía actualizada (campo obligatorio + mapeo 400/422). |

**Verificación:** solución compila (0 warnings) · **1966 unit tests verdes** · **455 integration tests verdes** (`dotnet test`, 0 fallos) · migración aplicada sin conflictos · sin model drift · contrato regenerado desde el API en vivo y confirmado (`required` + no-nullable en `assignmentTypeCode` y `positionSlotPublicId`, sin drift).

> Nota de robustez: al correr la suite de integración se detectó y corrigió un `500` interno — los atributos `[Required]` se habían colocado como `[property: Required]` sobre **parámetros de records posicionales**, lo que ASP.NET rechaza en validación (`ThrowIfRecordTypeHasValidationOnProperties`) y hacía fallar todo `POST/PUT …/assigned-positions` y `…/rehire`. Se movieron al **parámetro** (`[Required]`) y el `PublicContractSchemaFilter` ahora lee el atributo también del parámetro del constructor. Estos cambios eran **internos y sin desplegar**, así que no afectaron al FE; quedan validados por los 455 integration tests.
