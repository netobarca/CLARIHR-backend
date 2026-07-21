# Respuesta Backend — Clarificaciones de Integración Frontend

| | |
| --- | --- |
| **De** | Equipo Backend (.NET API) |
| **Para** | Equipo Frontend |
| **Fecha** | 2026-07-20 |
| **Método** | Verificado contra el código fuente en `master` (no contra las guías ni de memoria). Se citan archivo:línea. |
| **Estado** | ✅ **CERRADO — sin ajustes pendientes.** Los 4 gaps de concurrencia detectados en el addendum de Q3 ya están corregidos, con build limpio (`dotnet build`: 0 errores/0 warnings) y suite de tests en verde (unit 2779/2779; integración de los módulos tocados con cobertura previa: 22/22). El único punto que queda deliberadamente abierto es una **decisión de ustedes**, no un pendiente nuestro: si quieren que `indebtedness-limits` (Q3 original) también reciba `If-Match` — ver el cierre de Q3 más abajo. |

---

## Q1 · ¿Dónde viaja el `code` de negocio en el ProblemDetails?

### Respuesta corta

**El `code` es una propiedad RAÍZ del JSON: `problemDetails.code`. Nunca hay un objeto anidado `extensions` en el body HTTP real.** El interceptor del FE debe leer `body.code`, punto — nunca `body.extensions.code`.

La confusión viene de que, en el **código C# del backend**, ese valor se escribe en un diccionario llamado `.Extensions[...]`. Pero ese nombre es interno del framework (`Microsoft.AspNetCore.Mvc.ProblemDetails.Extensions`) y **no sobrevive a la serialización**: ASP.NET Core marca ese diccionario con `[JsonExtensionData]`, lo que hace que `System.Text.Json` lo **aplane** a nivel raíz del documento JSON. Algunas guías nuestras describieron el nombre de la variable C# como si fuera una clave del JSON — eso es lo que hay que corregir, no el comportamiento del backend (que es único y consistente en todos los módulos).

### Evidencia en el código (dónde se escribe `code`)

Los tres puntos donde el backend arma un ProblemDetails hacen exactamente lo mismo:

- `src/CLARIHR.Api/Common/ProblemDetailsFactory.cs:62` — `problemDetails.Extensions["code"] = error.Code;` (camino normal: 400/401/403/404/409/413/422 de reglas de dominio)
- `src/CLARIHR.Api/Middleware/UnhandledExceptionMiddleware.cs:120` — `problemDetails.Extensions["code"] = code;` (camino de excepciones no controladas: concurrencia/conflicto/500)
- `src/CLARIHR.Api/Common/ProblemDetailsDefaults.cs:51` — `problemDetails.Extensions[CodeExtensionKey] = ValidationCode;` (camino de `ValidationProblemDetails` del model binding, 400)

En **ningún** punto se crea un objeto/diccionario propio llamado `"extensions"` dentro del payload; `Extensions` es solo el nombre de la propiedad C# de `ProblemDetails`. No existe ningún `JsonConverter` custom para `ProblemDetails` que cambie este comportamiento — se usa el serializador default de ASP.NET Core (`Program.cs` solo registra `JsonStringEnumConverter` y un `TypeInfoResolver`, ninguno toca `ProblemDetails`/`Extensions`).

### Evidencia empírica (el JSON que de verdad viaja por la red)

Decenas de tests de integración deserializan la respuesta HTTP real y leen el código así — **directo de la raíz, nunca anidado**:

```csharp
// tests/CLARIHR.Api.IntegrationTests/ApiIntegrationTests.IndebtednessValidation.cs:90
Assert.Equal("INDEBTEDNESS_LIMIT_EXCEEDED", root.GetProperty("code").GetString());
```

Mismo patrón en `ApiIntegrationTests.PayrollRuns.cs` (líneas 265, 467, 489, 498, 551, 588, 1147), `ApiIntegrationTests.NotWorkedTimes.cs` (113, 220, 235, 333), `InternalCatalogsIntegrationTests.cs:201`, `AuthRegistrationSecurityTests.cs:29`, `PlatformAuthenticationIntegrationTests.cs:96`, entre otros — ninguno hace `.GetProperty("extensions")` antes de `.GetProperty("code")`.

Shape real de un 422 (capturado literal en `docs/technical/guia-integracion-frontend-endeudamiento.md:33-44`):

```jsonc
{
  "type": "https://httpstatuses.com/422",
  "title": "...",
  "detail": "...",
  "status": 422,
  "code": "INDEBTEDNESS_LIMIT_EXCEEDED",
  "baseIncome": 1000.00,
  "traceId": "…"
}
```

No hay clave `"extensions"` en ningún lugar del documento.

### Por qué las guías se contradicen

| Guía | Qué dice | Veredicto |
| --- | --- | --- |
| `guia-integracion-frontend-horas-extras.md:14,232` | código en `problemDetails.extensions.code` | ⚠️ Desactualizada — confunde el nombre C# con la ruta JSON |
| `guia-integracion-frontend-tiempo-compensatorio.md:14,184` | ídem | ⚠️ Desactualizada |
| `guia-integracion-frontend-tablero-acciones-personal.md:14,308` | ídem | ⚠️ Desactualizada |
| `guia-integracion-frontend-planilla-descuentos-ciclicos.md:9,218` | ídem | ⚠️ Desactualizada |
| `guia-integracion-frontend-tiempos-no-trabajados.md:155` | `code` en raíz, "no existe un objeto extensions" | ✅ Correcta |
| `guia-integracion-frontend-planilla-descuentos-eventuales.md:241-242` | ídem, con explicación del aplanado | ✅ Correcta |
| `guia-integracion-frontend-endeudamiento.md:21-24,239` | ídem, con explicación del aplanado | ✅ Correcta |
| `aclaraciones.md:47-63` (2026-06-25) | dice "`extensions.code`" en el título pero el JSON de ejemplo (línea 57) ya muestra `code` como propiedad raíz | ⚠️ Título ambiguo, contenido correcto |

✅ **Corregido.** Las 4 guías marcadas ⚠️ (horas extras, tiempo compensatorio, tablero de acciones de personal, descuentos cíclicos) ya se editaron para dejar de decir `extensions.code` y usar la misma redacción que las guías correctas ("`code`, miembro RAÍZ — no existe un objeto `extensions`").

### OpenAPI — ✅ ya tipado

El schema `ProblemDetails` (`docs/technical/api/openapi.yaml:79084-79103`) antes solo declaraba `type`, `title`, `status`, `detail`, `instance` y cerraba con `additionalProperties: {}`. **Ya se extendió** para declarar explícitamente `code` y `traceId`:

```yaml
ProblemDetails:
  type: object
  properties:
    type: { type: string, nullable: true }
    title: { type: string, nullable: true }
    status: { type: integer, format: int32, nullable: true }
    detail: { type: string, nullable: true }
    instance: { type: string, nullable: true }
    code: { type: string, nullable: true }        # código de negocio estable
    traceId: { type: string, nullable: true }
  additionalProperties: { }   # queda abierto para campos específicos de cada 422 (p.ej. baseIncome)
```

Cambio puramente de documentación de contrato — el backend ya emitía estos campos, ahora el spec los tipa.

### Acción concreta para el interceptor de errores del FE

```ts
const code = (problemDetails as any).code; // string | undefined — SIEMPRE raíz, nunca .extensions.code
```

No existe ningún caso, en ningún módulo, donde el código viva anidado. Un solo camino de lectura basta para todos los módulos.

---

## Q2 · ¿Cuál es el `code` exacto del 422 de endeudamiento excedido, y cómo distinguirlo de forma estable de los demás 422?

### Respuesta corta

Sí, es literalmente **`INDEBTEDNESS_LIMIT_EXCEEDED`**. Es el único `code` de todo el flujo de `recurring-deductions` que es forzable, y es forzable en los tres endpoints (POST, PUT, PATCH `/resolution`) con el flag `acknowledgeIndebtednessExceeded: true` en el body. Estructuralmente es imposible que ese flag bypasee ningún otro error de validación — lo explicamos abajo — así que su detección defensiva actual (buscar `INDEBTEDNESS` en el `code`) es correcta y no necesita ampliarse a texto del mensaje. **No existe hoy** ningún campo genérico tipo `isForcible`/`canOverride` en la respuesta — lo señalamos como gap al final.

### El código, en la fuente

`src/CLARIHR.Application/Features/PersonnelFiles/Compensation/Indebtedness.Rules.cs:13-16`:

```csharp
public static readonly Error LimitExceeded = new(
    "INDEBTEDNESS_LIMIT_EXCEEDED",
    "The deduction would push the employee past the applicable indebtedness limit.",
    ErrorType.UnprocessableEntity);   // → 422
```

El propio comentario XML de esa clase (líneas 8-12) ya documenta la intención: *"warn, never block — re-sending the same request with `acknowledgeIndebtednessExceeded = true` proceeds and stamps the override footprint."* Es el único código del módulo pensado para forzarse.

### El campo que fuerza el paso — mismo nombre en los 3 endpoints

`src/CLARIHR.Api/Contracts/PersonnelFiles/RecurringDeductionContracts.cs`:
- `AddRecurringDeductionRequest.AcknowledgeIndebtednessExceeded` (línea 49) — POST
- `UpdateRecurringDeductionRequest.AcknowledgeIndebtednessExceeded` (línea 77) — PUT
- `ResolveRecurringDeductionRequest.AcknowledgeIndebtednessExceeded` (línea 94) — PATCH `/resolution`

Los tres son `bool`, default `false`, serializan camelCase estándar: **`acknowledgeIndebtednessExceeded`**.

### Por qué el flag NUNCA puede tapar otro error (no es una convención, es estructural)

En `RecurringDeductionWriteSupport.ResolveAndValidateAsync` (`RecurringDeductions.Handlers.cs:132-243`), cada validación previa (tipo, concepto, institución financiera, tipo de planilla, frecuencias, plaza asignada, forma del plan/segmentos, acción de liquidación) hace `return Result.Failure(...)` de inmediato si falla. El chequeo de endeudamiento es literalmente el **último paso** de la cadena (línea ~224-235):

```csharp
// solo se llega aquí si TODAS las validaciones anteriores pasaron
if (indebtedness.IsExceeded && !input.AcknowledgeIndebtednessExceeded)
{
    return Result<RecurringDeductionResolved>.Failure(IndebtednessErrors.LimitExceeded with { ... });
}
```

Mismo patrón en la resolución (`ResolvePersonnelFileRecurringDeductionCommandHandler.Handle`, línea ~907-918): el flag solo se consulta después de `StatusInvalid`, `DecisionNoteRequired`, `ItemNotFound` (404), `ConcurrencyConflict` (409) y `SelfApprovalForbidden` (403), todos con su propio `return`. Si cualquiera de esos falla antes, la ejecución nunca alcanza el `if` del flag — no hay ninguna rama de código donde `acknowledgeIndebtednessExceeded=true` pueda silenciar un error distinto a `INDEBTEDNESS_LIMIT_EXCEEDED`.

### El resto de 422 del mismo flujo — todos NO forzables (confirmar contra esta lista, no contra texto)

Catálogo cruzado en `RecurringDeductions.Rules.cs`, todos `ErrorType.UnprocessableEntity` salvo donde se indica:

| `code` | Significado |
| --- | --- |
| `RECURRING_DEDUCTION_TYPE_INVALID` | Catálogo tipo de descuento inválido |
| `RECURRING_DEDUCTION_CONCEPT_INVALID` | Concepto de compensación inválido |
| `RECURRING_DEDUCTION_FINANCIAL_INSTITUTION_REQUIRED` | Falta institución financiera |
| `RECURRING_DEDUCTION_PAYROLL_TYPE_INVALID` | Tipo de planilla inválido |
| `RECURRING_DEDUCTION_FREQUENCY_INVALID` | Frecuencia inválida |
| `RECURRING_DEDUCTION_ASSIGNED_POSITION_INVALID` | Plaza asignada inválida |
| `RECURRING_DEDUCTION_STATUS_INVALID` | Estado destino inválido en resolución |
| `RECURRING_DEDUCTION_DECISION_NOTE_REQUIRED` | Falta nota de decisión |
| `RECURRING_DEDUCTION_STATE_RULE_VIOLATION` | Transición de estado no permitida (no está `EN_REVISION`, etc.) |
| `RECURRING_DEDUCTION_PAYROLL_INPUT_RANGE_REQUIRED` | Falta rango de aplicación en planilla |
| `RECURRING_DEDUCTION_SEGMENTS_REQUIRED` / `_SEGMENT_VALUE_INVALID` / `_SEGMENT_RANGE_INVALID` | Errores de forma de los segmentos |
| **`RECURRING_DEDUCTION_SEGMENTS_NOT_CONTIGUOUS`** | Segmentos no contiguos (el que preguntaron) |
| `RECURRING_DEDUCTION_SEGMENTS_WITH_INTEREST` / `_SEGMENTS_INDEFINITE_SHAPE` | Combinaciones inválidas de segmentos |
| **`RECURRING_DEDUCTION_INTEREST_INDEFINITE`** | Interés compuesto + plan indefinido (el que preguntaron) |
| `RECURRING_DEDUCTION_INTEREST_PRINCIPAL_INVALID` / `_RATE_INVALID` / `_COUNT_INVALID` / `_NOT_AMORTIZABLE` | Otros errores de interés compuesto |
| **`RECURRING_DEDUCTION_INSTALLMENT_NOT_DUE_YET`** | Cuota aún no exigible (el que preguntaron) |
| `RECURRING_DEDUCTION_INSTALLMENT_SEQUENCE_INVALID` / `_EXCEEDS_PLAN` / `_NOT_APPLICABLE` | Otros errores de cuotas/liquidación |
| `RECURRING_DEDUCTION_SETTLEMENT_ACTION_INDEFINITE`, `_EXTRAORDINARY_*`, `_APPLICATION_FREQUENCY_INVALID` | Errores de acciones de liquidación/extraordinarias |
| `PERSONNEL_FILE_STATE_RULE_VIOLATION` / `EMPLOYEE_PROFILE_RETIRED_LOCKED` | Errores transversales del expediente (empleado no completo / retirado) |
| `RECURRING_DEDUCTION_SELF_APPROVAL_FORBIDDEN` | ⚠️ es **403**, no 422 |
| `CONCURRENCY_CONFLICT` | ⚠️ es **409**, no 422 |
| `PERSONNEL_FILE_ITEM_NOT_FOUND` | ⚠️ es **404**, no 422 |

Ninguno de estos admite `acknowledgeIndebtednessExceeded` — todos son validaciones reales, tal como ya asumía su detección defensiva.

**Nota aparte para no confundir:** existe un código hermano `INDEBTEDNESS_LIMIT_TYPE_DUPLICATED`, pero pertenece a la **administración de parámetros** de endeudamiento (configurar los límites por tipo), no al flujo de creación/resolución de un descuento. No es forzable y no debería tratarse como el mismo caso solo por compartir el prefijo `INDEBTEDNESS`.

### Evidencia de test end-to-end (mismo body, con y sin el flag)

`tests/CLARIHR.Api.IntegrationTests/ApiIntegrationTests.IndebtednessValidation.cs`:
- Líneas 76-88: POST sin flag → `422` + `code == "INDEBTEDNESS_LIMIT_EXCEEDED"` + desglose (`baseIncome`, `currentLoad`, `newInstallment`, `projectedPercent`, `limitPercent`, `limitSource`) como propiedades raíz adicionales.
- Líneas 90-99: mismo body + `acknowledgeIndebtednessExceeded: true` → `201 Created`, con `indebtednessOverrides[0].stage == "CREACION"` en la respuesta (footprint de que se forzó).
- Líneas 218-247: PATCH `.../resolution` sin flag → `422` mismo `code`; con el flag en el body del PATCH → `200 OK`, `indebtednessOverrides[0].stage == "AUTORIZACION"`.

Esto último es útil para el FE: **cada vez que se fuerza, queda un registro en `indebtednessOverrides[]` con `stage`** (`CREACION` o `AUTORIZACION`), así que además de detectar el 422 forzable, pueden mostrar en la UI si un descuento tiene overrides históricos.

### Gap real detectado (no hay campo estable de "esto es forzable" en la respuesta)

Confirmado por búsqueda exhaustiva: no existe ningún `isForcible`/`canOverride`/`requiresAcknowledgement` en `ProblemDetailsFactory.cs` ni en ningún `Extensions` del catálogo de errores. Hoy la única señal es comparar `code === "INDEBTEDNESS_LIMIT_EXCEEDED"` (string exacto — no hace falta ni recomendamos parsear el `detail`, que es localizado y puede cambiar de redacción). Su approach defensivo actual (buscar `INDEBTEDNESS` en el código) es más amplio de lo necesario pero no incorrecto, dado que el único código hermano (`INDEBTEDNESS_LIMIT_TYPE_DUPLICATED`) vive en un endpoint completamente distinto (administración de parámetros) al que la UI de creación/resolución de un descuento nunca llama — en la práctica no hay colisión. Aun así, **recomendamos comparar el string exacto `code === "INDEBTEDNESS_LIMIT_EXCEEDED"`** en vez de un substring, para no depender de que ningún código futuro empiece igual.

Si más adelante quieren un contrato más explícito (p.ej. un campo `forcible: true` en el ProblemDetails de este caso específico), es un cambio pequeño y lo podemos agendar — avísenme si lo prefieren sobre la comparación por código.

---

## Q3 · `PUT /companies/{id}/indebtedness-limits` sin `If-Match` — ¿intencional o descuido?

### Respuesta corta

Es **un patrón heredado deliberadamente de otro endpoint preexistente** (`income-tax-brackets`), no un descuido puntual de este módulo — pero esa herencia nunca vino acompañada de una decisión explícita sobre concurrencia en sí; se copió la forma completa, incluyendo la ausencia de `If-Match`, sin que nadie razonara el punto por escrito. Dado el riesgo real que describen (lost update silencioso entre dos administradores), **recomendamos agregarlo**, y detallamos abajo el porqué es viable y el tamaño del cambio.

### Por qué no lo tiene hoy (la regla implícita del proyecto)

`If-Match` aparece en todo endpoint que reemplaza/muta una **fila identificable con su propio id+token propio** en la URL — sea un recurso singular (`recurring-deductions/{id}`, `RecurringDeductionsController.cs:118-124`, con `[FromIfMatch] Guid concurrencyToken`) o una sub-colección colgada de un padre con id (`incapacity-risks/{id}/parameters`, `IncapacityRisksController.cs:160-179` — ahí el token es del riesgo PADRE, no de cada tramo individual).

`indebtedness-limits` no encaja en ese molde: la URL solo trae `companyId` como **filtro de tenant**, no el id de una fila versionada — es un *replace-all* de una colección completa. `IndebtednessParametersController.cs:59-65`:

```csharp
public async Task<ActionResult<IReadOnlyCollection<IndebtednessLimitResponse>>> ReplaceLimits(
    Guid companyId,
    [FromBody] ReplaceIndebtednessLimitsRequest request,
    CancellationToken cancellationToken = default)
```

Sin `[FromIfMatch]`. El repositorio hace literalmente delete físico + insert de todo el set del tenant (`IndebtednessRepository.cs:25-40`):

```csharp
// Hard delete, not a logical one: without production data there is nothing to preserve...
var existing = await dbContext.IndebtednessLimits.Where(item => item.TenantId == tenantId).ToArrayAsync(...);
dbContext.IndebtednessLimits.RemoveRange(existing);
foreach (var limit in limits) dbContext.IndebtednessLimits.Add(limit);
```

La entidad `IndebtednessLimit` **sí tiene** un `ConcurrencyToken` (`IndebtednessLimit.cs:39`, configurado como `IsConcurrencyToken()` en EF), pero es **puramente interno**: protege el `SaveChanges` de EF dentro de la misma transacción, nunca se expone en `IndebtednessLimitResponse` ni lo selecciona el repositorio de lectura. El cliente no podría usarlo en un `If-Match` aunque quisiera — no es que "se les olvidó pasarlo", es que no hay ningún token de fila individual que tenga sentido en un reemplazo de colección completa.

### La prueba de que es un patrón copiado, no un diseño propio de este endpoint

`docs/technical/plan-tecnico-endeudamiento.md:100` — encabezado literal: *"Tabla `IndebtednessLimit` (**molde `IncomeTaxWithholdingBracket`**)"*, y línea 122: *"(delete+add sin `SaveChanges` — lo commitea el handler, **como el de brackets**)."* El plan técnico documenta explícitamente que este endpoint se modeló copiando la forma de `PUT .../income-tax-brackets` (`IncomeTaxBracketsController.cs:53-56`), que **tampoco** lleva `If-Match` desde su creación (commit `7a249ff`), y que tiene el mismo shape: colección scoped por una categoría no-versionada (ahí `payPeriodCode` en vez de `companyId`), delete+insert.

Mismo patrón también en `PUT .../competency-rating-scale` (`CompetencyRatingScalesController.cs:44-53`) — otro "singleton de configuración por tenant sin id propio en la URL" que tampoco tiene `If-Match`. Es decir: **no es un caso aislado de endeudamiento**, es una convención (no escrita como tal, pero consistente) para endpoints "replace-all de configuración de tenant".

Confirmado en tests: `ApiIntegrationTests.IndebtednessParameters.cs` — el helper que ejercita este PUT nunca setea `If-Match` y espera `200` limpio (el comportamiento está probado y es el esperado hoy), mientras que el PUT de la preferencia global `MaxIndebtednessPercent` (que sí vive en una entidad singular versionada, `CompanyPreference`) sí usa `If-Match` en el mismo archivo de test.

### ¿Es un riesgo real? Sí — coincidimos con el diagnóstico del FE

El lost-update que describen es real: dos admins editando los límites de endeudamiento a la vez, el segundo `PUT` gana sin aviso ni conflicto. La única razón por la que el resto del módulo (recurring-deductions, límites por deducción individual) sí está protegido es que ahí sí hay una fila con id propio; aquí no la hay porque el diseño la modeló como configuración de tenant, no como colección de entidades independientes con ciclo de vida propio.

### Si deciden que sí debe llevarlo — tamaño del cambio

No requiere rediseñar el endpoint, pero sí decidir **qué token exponer como el "padre" versionado**, ya que hoy no hay ninguno pensado para esto:

- **Opción barata (recomendada):** usar el `ConcurrencyToken` de la entidad `Company` como token de "el recurso configuración de la empresa" — análogo a como `incapacity-risks/{id}/parameters` usa el token del riesgo padre. Cambios: exponerlo en el `GET` de `indebtedness-limits`, agregar `[FromIfMatch] Guid concurrencyToken` al controller, comparar contra `Company.ConcurrencyToken` en el handler antes de reemplazar. **Sin migración de esquema** (asumiendo que `Company` ya tiene su propio token, que debería tenerlo dado el patrón general del proyecto).
- **Opción más grande:** una columna/entidad dedicada tipo "versión del set de límites" por tenant — sí requiere migración EF nueva. No la recomendamos si la opción barata cumple el propósito.
- **Consistencia:** si corrigen este endpoint, `income-tax-brackets` tiene exactamente el mismo gap por el mismo motivo — conviene decidir si se corrige junto o se documenta por qué solo uno de los dos cambia, para no dejar la misma inconsistencia a medias.

Esto es una decisión de diseño (no una corrección de bug), así que la dejamos abierta: si prefieren mantenerlo así asumiendo baja contención (un par de admins por tenant, cambios infrecuentes), es defendible dado el precedente ya establecido en el repo; si prefieren cerrarlo, el esfuerzo es bajo con la opción del token de `Company`. Avísennos cuál prefieren y lo agendamos.

---

## Addendum a Q3 · Auditoría completa de `If-Match` en TODOS los endpoints de escritura

Tras Q3 hicimos una auditoría de los **375** `PUT`/`PATCH`/`DELETE` de todo el backend (API principal + Backoffice) — no una muestra, el 100% — para verificar si `indebtedness-limits` era un caso aislado o si el lineamiento canónico ("toda escritura sobre una fila individual versionada lleva `If-Match`; falta→400, stale→409") se está aplicando de forma consistente. Confirmamos personalmente contra el código fuente (no solo el resultado del análisis) los 4 hallazgos más importantes de abajo antes de reportarlos.

### Aviso previo: el conteo del FE (538 escrituras / 178 sin `If-Match`) no coincide con el código fuente

Al parsear `openapi.yaml` obtenemos solo 383 operaciones de escritura, y varias de las 38 que ese spec marca como "sin `If-Match`" **sí lo tienen en el código real** (ej. `OneTimeIncomesController.cs:116,140,165,192`, `RecurringIncomesController.cs:113,137,161,186,212,284,316` — todas con `[FromIfMatch] Guid concurrencyToken` en la firma real). Esto es consistente con lo que ya sabemos: la regeneración de `openapi.yaml` de este repo tiene una herramienta pendiente/no documentada, así que el spec tiene drift respecto al código. **Recomendamos no usar el spec como fuente de verdad para este tipo de auditoría — el código sí lo es**, y por eso hicimos el conteo directo sobre los controllers.

### Totales reales (código fuente, 100% de cobertura)

| | API principal | Backoffice | Total |
|---|---|---|---|
| Total PUT/PATCH/DELETE | 358 | 17 | **375** |
| Con `[FromIfMatch]` | ~351 | ~5 | **356** |
| Sin `[FromIfMatch]` | 17 | 2 | **19** |

De esos 19, los clasificamos así:

- **4 son Categoría B** (igual que `indebtedness-limits`): `competency-rating-scale`, `income-tax-brackets`, `indebtedness-limits`, y el `PUT .../subscription` de Backoffice — todos singletons de configuración de tenant sin fila individual, mismo patrón ya validado en Q3.
- **5 son variantes legítimas con "ETag débil"**: `AccountCompanyAuthorizationController.SyncUserRoles` y los 4 métodos de `CompanyUsersController` (Update/Patch/Deactivate/Reactivate) — estos SÍ hacen falta→400/stale→409, pero vía un ETag calculado (`TryGetWeakIfMatch`) en vez del atributo `[FromIfMatch]`, porque el recurso es una proyección sobre 3 agregados sin un único token persistido. Cumplen el espíritu del lineamiento; solo no son detectables por un grep de `[FromIfMatch]`.
- **6 protegen concurrencia por un canal distinto (campo en el body, no header `If-Match`)**: `FilesController.CompleteUpload`, y en Backoffice `PlanChangeCancel`/`AddonChangeCancel`/`SystemCatalogsController` (Update/Activate/Inactivate). Funcionalmente sí evitan el lost-update, pero violan la convención de "el token va en el header", que es justo lo que Q1 estableció como parte del contrato — vale la pena migrarlos a `If-Match` en algún momento, no es urgente porque no hay riesgo de datos.
- **4 son gaps reales (Categoría C)** — verificados por mí directamente en el código, no solo por el agente:

### Los 4 gaps — ✅ TODOS CORREGIDOS

**1. `ExitInterviewsController.SaveSubmission` — el más serio. ✅ Corregido.**
`src/CLARIHR.Api/Controllers/ExitInterviewsController.cs:43` (`PUT api/v1/personnel-files/{publicId}/exit-interview/submission`). Este endpoint es un **upsert** (crea la submission la primera vez, la actualiza después), así que **no** usa el header `If-Match` como el resto — usa un campo **`concurrencyToken` en el body**, `SaveExitInterviewSubmissionRequest` (opcional, `Guid?`):
- **Primer guardado** (no existe submission previa): se omite el campo, no se exige.
- **Guardados siguientes**: `concurrencyToken` es obligatorio — falta → `400` (validación); no coincide con el token actual → `409 CONCURRENCY_CONFLICT`.
La respuesta ya traía `concurrencyToken` (no cambió); ahora también se exige de vuelta.

**2. `AccountCompanyAuthorizationController.DeleteRole`. ✅ Corregido.**
`DELETE .../roles/{rolePublicId}` ahora exige `[FromIfMatch] Guid concurrencyToken` en el header — igual que su hermano `PatchRole` en el mismo controller. Falta → `400`; token viejo → `409 CONCURRENCY_CONFLICT` (mismo `code` que ya usan en el resto del módulo de roles).

**3. `FilesController.Delete`. ✅ Corregido.**
`DELETE api/v1/files/{filePublicId}` ahora exige `If-Match` con el `concurrencyToken` que ya reciben en las respuestas de archivo (p. ej. `CreateUploadSessionResponse.concurrencyToken`). Falta → `400`; stale → `409 CONCURRENCY_CONFLICT`.

**4. `PlatformCompanySubscriptionsController.ChangeStatus` (Backoffice). ✅ Corregido — con un hallazgo adicional que vale la pena que sepan.**
`PATCH .../subscriptions/{subscriptionPublicId}/status` ahora exige `If-Match`. **Al implementarlo encontramos que `PlatformCompanySubscriptionResponse` nunca exponía `concurrencyToken`** — es decir, exigir el header sin arreglar esto habría dejado el endpoint literalmente inusable (nadie podría obtener el token para mandarlo). Lo corregimos también: `concurrencyToken` ahora es un campo más de `PlatformCompanySubscriptionResponse` (lo reciben en el `GET .../subscription`, en el listado, y en la respuesta de cada `PATCH .../status` exitoso, para encadenar el siguiente cambio sin round-trip extra). Falta el header → `400`; token viejo → `409` con `code = "PLATFORM_COMPANY_SUBSCRIPTION_CONCURRENCY_CONFLICT"` (nota: este módulo usa un `code` de conflicto específico del dominio, no el genérico `CONCURRENCY_CONFLICT` que usan roles/archivos — verificar contra ese string exacto si cablean un manejo especial).

### Verificación hecha

- `dotnet build` sobre toda la solución: **0 errores, 0 warnings**.
- Suite de unit tests: **2779/2779** (incluye 3 tests nuevos para `DeleteFileCommandHandler` cubriendo éxito/stale-token/precedencia-sobre-ownership).
- Tests de integración de los dos módulos que ya tenían cobertura previa (`AccountCompanyAuthorizationIntegrationTests`, `BackofficeCompanySubscriptionsIntegrationTests`): **22/22**, incluyendo 4 tests nuevos (400 sin header/campo, 409 con token viejo, para roles y para subscripciones).
- `ExitInterviewsController`/`FilesController` no tenían ninguna prueba de integración previa a esta sesión (gap de testing preexistente del proyecto, no introducido por este cambio) — la lógica se verificó por revisión de código línea por línea y, en el caso de Files, con los 3 unit tests nuevos. Si quieren, podemos agendar aparte la construcción de fixtures de integración para el módulo de entrevistas de retiro (requiere armar un escenario de retiro autorizado + formulario configurado, no es trivial).
- `docs/technical/api/openapi.yaml` y `openapi-backoffice.yaml` actualizados a mano para los 4 endpoints (parámetro `If-Match`, campo `concurrencyToken` en request/response según corresponda, respuestas 400/409 documentadas).

**No necesitan cambiar nada de su lado salvo empezar a enviar el header/campo donde antes no era necesario** — el shape de éxito no cambió en ninguno de los 4 endpoints, solo se activó la validación de concurrencia que ya esperaban ver (consistente con el contrato que establece Q1).

---

## Q4 · Los dos strings que el OpenAPI no expone

### a) Permiso para autorizar planillas

El string es literalmente **`PersonnelFiles.AuthorizePayrollRuns`** (con el prefijo de módulo `PersonnelFiles.` — igual que el resto de permisos del proyecto, no `AuthorizePayrollRuns` a secas).

Confirmado en dos lugares que deben coincidir exactamente entre sí (y coinciden):
- `src/CLARIHR.Application/Features/PersonnelFiles/Common/PersonnelFileCommon.cs:496` — `public const string AuthorizePayrollRuns = "PersonnelFiles.AuthorizePayrollRuns";` (el código que efectivamente llega en `currentUserAccess.permissions[].code`)
- `src/CLARIHR.Application/Features/PersonnelFiles/Common/PersonnelFilePolicies.cs:442` — mismo string, usado para registrar la policy de autorización (`Program.cs:819`) y en `[AuthorizationPolicySet(...)]` de `PayrollRunResolutionController.cs:27`.

Confirman su propia nota: **`AuthorizePayrollRuns` deliberadamente NO está implicado por `Manage`/`Admin`** — es un comentario explícito en el propio código (`PayrollRunResolutionController.cs:39`: *"must map its writes to `AuthorizePayrollRuns`"*; `Program.cs:806`: *"AuthorizePayrollRuns deliberately EXCLUDES"* el HR-admin genérico) — es decir, la separación de deberes que ya asumieron en su lógica es exactamente el diseño real del backend. Pueden cablear el check con este string ya con confianza.

### b) Keys de catálogos de estado de los módulos nuevos

Las 4 que asumieron **son exactamente correctas, sin excepción**. Confirmado en `src/CLARIHR.Application/Features/PersonnelFiles/Catalogs/GeneralCatalogKeyMap.cs:80-87`:

| Key asumida | ¿Correcta? | Línea |
| --- | --- | --- |
| `recurring-deduction-statuses` | ✅ | 82 |
| `one-time-deduction-statuses` | ✅ | 80 |
| `not-worked-time-statuses` | ✅ | 81 |
| `payroll-run-statuses` | ✅ | 87 |

Todas se consultan igual que el resto del proyecto: `GET /api/v1/general-catalogs/{catalogKey}`. Pueden cablearlas tal cual las tenían.

---

## Estado final del documento

Sin ajustes adicionales derivados de Q4 — ambos strings se confirmaron exactamente como el FE los había asumido/nombrado.

**Resumen de qué cambió de código/contrato en esta ronda (Q1 + addendum de Q3), todo ya mergeado en esta rama:**

| Cambio | Endpoint(s) | Efecto para el FE |
| --- | --- | --- |
| Schema `ProblemDetails` tipa `code`/`traceId` | Todos (400/401/403/404/409/413/422) | Solo documentación — sin cambio de comportamiento |
| `If-Match` header ahora obligatorio | `DELETE roles/{rolePublicId}`, `DELETE files/{filePublicId}`, `PATCH .../subscriptions/{id}/status` (Backoffice) | Deben empezar a enviarlo — antes no era necesario |
| Campo `concurrencyToken` (body, opcional) ahora exigido desde el 2º guardado | `PUT .../exit-interview/submission` | Enviar el `concurrencyToken` recibido a partir del segundo `PUT` |
| `concurrencyToken` agregado a la respuesta | `PlatformCompanySubscriptionResponse` (Backoffice: GET/list/PATCH status) | Campo nuevo disponible — necesario para el punto anterior |

**Lo único que sigue abierto es una decisión suya, no nuestra:** si quieren que `PUT .../indebtedness-limits` (el caso original de Q3) también lleve `If-Match`, avísennos y lo agendamos — lo dejamos así deliberadamente porque es coherente con un patrón de diseño ya existente en el repo (`income-tax-brackets`, `competency-rating-scale`), no porque falte trabajo de nuestro lado.

Todo lo demás en este documento (Q1, Q2, Q4, y los 4 gaps del addendum de Q3) está **cerrado y verificado contra código real, build limpio y tests en verde**.
