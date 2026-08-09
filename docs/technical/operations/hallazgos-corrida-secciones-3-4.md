# Hallazgos de la corrida — secciones 3 y 4

Registro de defectos, inconsistencias de contrato y mejoras encontradas al ejecutar
`playbook-pruebas-api.md` §3 (estructura organizativa) y §4 (puestos, plazas y tabulador)
contra el ambiente local, el 2026-08-06 / 2026-08-08.

Empresa de prueba: `Avianca El Salvador` · `9031b0db-4c8c-4e96-8a49-ee40c87e15d8`
API: `http://127.0.0.1:5000` · BD: `clarihr_dev` @ `localhost:5433`

Todo lo listado está **verificado leyendo el código** y, salvo donde se indique, **reproducido
contra la API en vivo**. Las referencias a archivo:línea apuntan al árbol local de esa fecha.

---

## Resumen

| # | Hallazgo | Tipo | Severidad |
|---|---|---|---|
| [H-01](#h-01) | ~~El estado del perfil de puesto es puramente documental~~ — **✅ RESUELTO 2026-08-08** | Diseño / control interno | ✅ Corregido |
| [H-02](#h-02) | `POST /company/users` siempre devuelve `500` | Bug | 🔴 Grave |
| [H-03](#h-03) | Con `Email:Provider=Logging` no se puede activar ningún usuario invitado | Bloqueo de ambiente | 🟠 Alta |
| [H-04](#h-04) | `switch` de empresa exige propiedad, el contrato dice membresía | Contrato ≠ implementación | 🟠 Alta |
| [H-05](#h-05) | El tabulador no valida el salario mínimo legal | Regla de negocio ausente | 🟠 Alta |
| [H-06](#h-06) | Regla no documentada de la matriz de competencias | Contrato incompleto | 🟡 Media |
| [H-07](#h-07) | `requirements` acepta cualquier catálogo sin validar la categoría | Validación ausente | 🟡 Media |
| [H-08](#h-08) | `salary-tabulator/lines` no expone el código de la clase salarial | Contrato incompleto | 🟡 Media |
| [H-09](#h-09) | `version` del perfil cuenta escrituras, no revisiones | Semántica engañosa | 🟡 Media |
| [H-10](#h-10) | El manifiesto de catálogos apunta al catálogo equivocado | Posible bug | 🟡 Media |
| [H-11](#h-11) | Convenciones inconsistentes entre catálogos de la misma sección | Deuda de contrato | 🟢 Baja |
| [H-12](#h-12) | `inactivate` de tipo de centro de trabajo no valida uso | Asimetría | 🟢 Baja |
| [H-14](#h-14) | La plaza acepta cualquier salario, dentro o fuera de su banda | Regla de negocio ausente | 🟠 Alta |
| [H-15](#h-15) | No hay forma de eliminar ni desactivar una plaza | Ciclo de vida incompleto | 🟠 Alta |
| [H-16](#h-16) | El listado de plazas no expone la dependencia jerárquica | Contrato incompleto | 🟡 Media |
| [H-17](#h-17) | `requiresGeo` acepta la coordenada (0,0) | Validación superficial | 🟢 Baja |
| [H-18](#h-18) | La jornada sembrada viola el invariante que la API exige, y un error mal atribuido | Bug + semilla inconsistente | 🟡 Media |
| [H-19](#h-19) | Falta la capa que deriva horas extra desde la jornada, y tres de sus insumos | Capa por construir | 🟠 Alta |
| [H-20](#h-20) | El tramo horario de la hora extra no se valida contra nada: doble pago silencioso | Bug | 🔴 Grave |
| [H-21](#h-21) | Los catálogos de conceptos devuelven `[]` sin `countryCode`: falsa alarma de "no sembrado" | Bug | 🟠 Alta |
| [H-22](#h-22) | `document_type_catalog_items` está vacío y **no tiene endpoint**: bloquea todo adjunto | Superficie faltante | 🔴 Grave |
| [H-23](#h-23) | `occupiedEmployees` de la plaza es un contador que nadie mantiene ni lee | Dato decorativo | 🟡 Media |
| [H-24](#h-24) | ~~`finalize` significa "retiro definitivo"~~ — **retirado, era un error mío** | — | ❌ Inválido |
| [H-25](#h-25) | `finalize` es un paso obligatorio del alta que no se descubre hasta que falla | Contrato no evidente | 🟠 Alta |
| [H-26](#h-26) | Una fecha sin zona (`"2026-08-01"`) produce `500` en la asignación | Bug | 🔴 Grave |
| [H-27](#h-27) | Cuentas bancarias: duplicados exactos y N primarias simultáneas, sin validación | Validación ausente | 🟠 Alta |
| [H-28](#h-28) | La antigüedad para vacaciones se toma de la plaza, no del ingreso: **nadie tiene vacaciones el primer año tras el arranque** | Regla de negocio incorrecta | 🔴 Grave |
| [H-29](#h-29) | No existe el reporte de planilla por empleado: **ni el Excel ni el JSON** para la matriz del website | Superficie faltante | 🔴 Grave |
| [H-30](#h-30) | La clase del descuento (Ley/Interno/Externo) no se persiste en la línea: un periodo cerrado se reclasifica retroactivamente | Modelo incompleto | 🟠 Alta |
| [H-31](#h-31) | Los días de incapacidad y de tiempo no trabajado **no llegan a la planilla**: solo el monto | Contrato incompleto | 🟠 Alta |
| [H-32](#h-32) | ~~El tope patronal de incapacidad de 9 días no es legal~~ — **retirado: es configuración de la empresa** | Referencia legal | ❌ No aplica |
| [H-13](#h-13) | Huecos del playbook en §4.1, §4.2, §4.4 y §6.6 | Documentación | ✅ Corregido / ⚠️ pendiente |

---

<a id="h-01"></a>
## H-01 · ✅ RESUELTO — el estado del perfil de puesto es puramente documental

> **✅ Corregido el 2026-08-08.** Lo que se construyó, y las dos trampas que aparecieron al hacerlo:
>
> **Permiso propio `JobProfiles.Publish`** que `JobProfiles.Admin` **no implica** — quien redacta no
> aprueba. Vive en `JobProfileResolutionController`, un controlador dedicado **obligatoriamente**: el
> `[AuthorizationPolicySet]` es de clase, así que no se puede tener otra policy para tres acciones dentro
> de `JobProfilesController`.
>
> **Tres transiciones nuevas**, todas con `If-Match`:
> `PATCH /job-profiles/{publicId}/publication` · `/reopening` (**motivo obligatorio**) · `/archival`.
> `/status` en el JSON Patch **deja de existir** y devuelve `400` — era la puerta por la que cualquier
> administrador de perfiles podía publicar. **Rompe contrato con el frontend** (F1–F6 del plan).
>
> **Publicar congela** el perfil y sus 9 colecciones (`422 JOB_PROFILE_STATE_RULE_VIOLATION`); reabrir con
> motivo es la única vuelta atrás, e incrementa `Version`. **Crear o re-apuntar una plaza exige
> `Published`** (`422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED`).
>
> ### Trampa 1 · el congelamiento no puede ser un solo invariante
>
> `EnsureEditable()` lo llamaban **dos familias con requisitos opuestos**: los mutadores del descriptor
> (deben congelarse) y `BumpVersion()`, que usan los updates de las 9 colecciones (deben congelarse)
> **y las 4 escrituras de la matriz de competencias** (no deben cambiar — la matriz es una capa operativa
> sobre un descriptor aprobado y queda **fuera** del congelamiento por decisión del stakeholder).
> Endurecer `EnsureEditable()` sin más **rompía la matriz en perfiles publicados**: compila limpio, se ve
> solo en tests. Se resolvió con dos invariantes —`EnsureEditable()` estricto y `EnsureNotArchived()`
> permisivo, más `BumpDescriptorVersion()`— y un test guardián con nombre autoexplicativo,
> `CompetencyFramework_MatrixAdd_OnPublishedProfile_ShouldSucceed`.
>
> ### Trampa 2 · la compensación saltaba el invariante por completo
>
> Add/Update/Patch de `JobProfileCompensationAdministration` **nunca cargaban el agregado** (usaban
> `ResolveJobProfileInternalIdAsync`), así que un perfil **archivado** tenía compensación mutable y el
> congelamiento tampoco la habría alcanzado. Ahora cargan el agregado como las otras 8.
>
> **Arreglado de paso:** `Publish()` no tenía guarda de estado (`Archived → Published` reportaba
> `PUBLISH_REQUIREMENTS_MISSING`, engañoso) · 36 sitios filtraban el mensaje crudo del dominio al cliente
> con el código ad-hoc `JobProfile.Conflict` · el DELETE de colecciones hijas ya no puede dejar un
> publicado por debajo de su propia barra de publicación (lo resuelve el congelamiento).
>
> **Verificado:** build limpio · **2866/2866 unitarios** · **45/45** integración de perfiles y matriz ·
> **38/38** plazas, asignaciones y recontratación · **91/91** áreas consumidoras (competencias, centros de
> costo, tabulador, finalize, catálogos, exports). Los 27 tests que crean plazas se arreglaron con **una
> sola edición**: el helper `EnsureJobProfilePublishedAsync` enganchado en `CreatePositionSlotAsync`.
>
> **Pendiente del alcance recortado** (decisión del stakeholder, no omisión): no se gatean las referencias
> `reportsTo`/puestos dependientes, ni la matriz, ni el PDF del borrador (que sigue imprimiendo
> `Estado: Draft` en inglés). Las plazas existentes no se consideraron: se eliminan y se vuelven a crear.

---

### El hallazgo original

**Reportado por el usuario durante la corrida.** El perfil de puesto nace en `Draft` y se puede
publicar con `PATCH /api/v1/job-profiles/{publicId}`, pero **el estado no habilita ni bloquea nada
aguas abajo**.

Verificado: la creación de la plaza resuelve el perfil con

```csharp
// PositionSlotRepository.cs:320
where profile.TenantId == tenantId && profile.PublicId == jobProfileId
```

Sin filtro por `Status` ni por `IsActive`. Un perfil en `Draft` —sin revisar, sin aprobar— crea
plazas igual que uno publicado.

Barrido de todos los usos de `JobProfileStatus` fuera del módulo `JobProfiles`:

| Consumidor | Regla |
|---|---|
| Matriz de competencias (`JobProfileCompetencyMatrixAdministration.cs:794`) | bloquea solo si `Archived` |
| `AllowedActionsRegistry.cs:309-312` | `NonEditableStates: [Archived]` → `Draft` y `Published` son igualmente editables |
| `AllowedActionsRegistry.cs:247` | `publishableStates: [Draft]` |
| Plazas · expedientes · nómina | **ninguna regla de estado** |

### Por qué es grave

1. **No hay control de cambios sobre el descriptor de puesto.** Un perfil publicado —que ya tiene
   plazas y empleados colgando— se puede editar con el mismo permiso y sin ninguna transición de
   estado. El descriptor de puesto es un documento con valor laboral y de auditoría; que sea
   libremente mutable después de publicado es un problema de control interno, no de comodidad.
2. **El estado no discrimina permisos.** Hoy `JobProfiles.Admin` habilita editar en cualquier
   estado. No existe la distinción entre "quien redacta el borrador" y "quien autoriza la
   publicación", que es justamente la separación de funciones que el resto del sistema sí modela
   (ver el anti-self del tabulador en H-05, o los dos pasos autorizar→cerrar de planilla).

### Propuesta

- Exigir `Published` para crear plaza (`POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED`).
- Agregar `Published` a `NonEditableStates` del perfil y de sus 9 colecciones hijas, o bien exigir
  una transición explícita `Published → Draft` (con permiso propio) para reabrir la edición.
- Separar el permiso de publicar del de editar, con anti-self si el equipo lo considera —
  el chasis ya existe (`SalaryTabulatorChangeRequest.Approve(..., allowSelfApproval)`).

> Decisión del usuario: **se ataca al terminar todas las pruebas**, no durante la corrida.

---

<a id="h-02"></a>
## H-02 · 🔴 `POST /api/v1/company/users` siempre devuelve `500`

Reproducido dos veces, con dos correos distintos:

```
HTTP 500 · {"code":"common.unexpected","detail":"No route matches the supplied values."}
```

**Escribe correctamente y falla al generar la respuesta.** Verificado en BD tras el `500`:
`user_companies.status = Active`, `iam_users.is_active = true`, rol asignado. Y
`GET /api/v1/company/users/{publicId}` devuelve `200` con el usuario completo.

### Causa raíz

`PublicContractRouteConvention` reescribe **todo** parámetro de ruta guid-like al nombre genérico
`publicId` (`PublicContractNaming.cs:37-39`), así que la ruta efectiva del `GET` es
`/api/v1/company/users/{publicId}`. Pero `Create` construye el `Location` con otra clave:

```csharp
// CompanyUsersController.cs:118
value => new { userPublicId = value.User.Id }   // ← debe ser: publicId = value.User.Id
```

`CreatedAtAction` no encuentra ruta que calce → excepción → `500`.

Es la misma trampa que `OrganizationUnitsController` sí documenta en un comentario
(*"the Location route value MUST be keyed `publicId` (not `id`)"*). El arreglo es esa única clave.

### Recomendación adicional

Un guardrail de test que recorra todos los `CreatedAtAction*` y verifique que la clave de ruta
existe en la plantilla reescrita. Este bug es invisible en pruebas unitarias de handler y solo
aparece en integración.

---

<a id="h-03"></a>
## H-03 · 🟠 Con `Email:Provider=Logging` no se puede activar ningún usuario invitado

Las **tres** vías de activación quedan cerradas simultáneamente:

| Vía | Por qué no sirve |
|---|---|
| Invitación (`POST /company/users` → `accept`) | el token se guarda hasheado (`company_invitation_tokens.token_hash`) y la respuesta solo trae `user` + `invitationExpiresUtc` |
| Reseteo de contraseña | ídem |
| Verificación de correo | ídem |

`LoggingEmailSender.cs` enmascara el token del enlace (`abcd...wxyz`, vía `SecretPreview.cs`) y su
comentario dice que es deliberado. No hay nivel de log que lo revele; `TemplatedAuthEmailService.cs`
tampoco lo escribe.

**Consecuencia práctica:** en un ambiente local o de CI **no se puede crear un segundo usuario** por
API. Eso bloquea de raíz cualquier prueba de separación de funciones — el anti-self del tabulador
(H-05), los dos pasos de planilla, las autorizaciones anti-self de expedientes.

### Workaround usado en esta corrida

`POST /api/v1/auth/register` (anónimo) + un `UPDATE` de una columna, réplica exacta de
`User.ConfirmEmail()`:

```sql
update auth_users set status='Active', modified_utc=now()
where normalized_email in (...) and status='PendingEmailVerification';
```

Después `POST /company/users` con el mismo correo reusa el usuario local existente y crea la
membresía `Active` (`CreateCompanyUser.cs:99-137`). No hace falta forjar hashes.

### Propuesta

Un transporte de desarrollo que escriba el correo completo a disco o a un endpoint de buzón local
—no al log—, activable solo fuera de producción. Alternativa mínima: que `reset-invitation`
devuelva el token cuando el proveedor configurado es `Logging`.

> Nota de esquema para quien repita el workaround: `normalized_email` se guarda en **minúsculas**.

---

<a id="h-04"></a>
## H-04 · 🟠 `switch` de empresa exige propiedad, el contrato dice membresía

```
POST /api/v1/account/companies/{c}/switch
→ 403 · COMPANY_OWNERSHIP_FORBIDDEN
```

Reproducido con un usuario que tiene **membresía activa, primaria, con rol Admin de Empresa** en esa
misma empresa (verificado en BD: `user_status=Active`, `membership=Active`, `is_primary=t`).

El Swagger del endpoint afirma:

> *"The company must be active and **the caller must have an active membership**, otherwise `403`."*

Pero el handler resuelve primero por **propiedad**:

```csharp
// AccountCompanyAdministration.cs:913
AccountCompanyActorResolver.ResolveOwnedCompanyAsync(..., currentUser.PublicId, ...)
→ AccountCompanyErrors.OwnershipForbidden
```

El chequeo de membresía (línea 930) nunca se alcanza para un no-propietario. O el contrato está mal
redactado, o el handler es más restrictivo de lo previsto. **Impacto en el frontend:** un usuario
invitado a una empresa no puede "entrar" a ella por esta vía.

Mitigante: el token del `login` ya trae el claim `tid` cuando la membresía es primaria, así que el
`switch` no fue necesario en la corrida. Pero un usuario con varias empresas dependería de él.

---

<a id="h-05"></a>
## H-05 · 🟠 El tabulador no valida el salario mínimo legal

`SalaryTabulatorChangeRequestItemInputValidator` (`SalaryTabulatorAdministration.cs:371-403`) valida:

- `proposedBaseAmount` obligatorio salvo `Inactivate`, y `> 0`
- `min ≤ base ≤ max` (las tres comparaciones)
- `effectiveToUtc ≥ effectiveFromUtc`

**No existe ninguna referencia al salario mínimo** en todo el módulo (grep de `minimumwage`,
`minimum_wage`, `408` → 0 resultados). Una banda de `$100` entra sin advertencia.

El playbook §4.3 pregunta *"banda bajo `$408.80` → ¿rechaza o advierte?"*. La respuesta es
**ninguna de las dos: la acepta**.

Contraste: el resto del sistema sí conoce el mínimo (`$408.80` está fijado en el motor de planillas).
Conviene decidir si el tabulador debe **advertir** —siguiendo el criterio "advertir, nunca bloquear"
que ya se usó en endeudamiento— o **rechazar**.

### Lo que sí funciona bien en este módulo

Vale registrarlo porque se probó explícitamente:

- **Anti-self de aprobación confirmado en vivo.** `allowSelfApproval` está fijo en `false`
  (`SalaryTabulatorAdministration.cs:1319`); el dominio lanza *"Requester cannot approve their own
  salary tabulator request"* y `MapDomainValidation` lo traduce a
  `422 SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION`. Probado: el mismo usuario que creó y sometió
  recibió `422`; un segundo usuario aprobó con `200`. **El token de concurrencia no rotó en el
  intento fallido**, así que el reintento con otro usuario funcionó con el mismo `If-Match`.
- **Cobertura de perfiles.** Tras cargar las 33 compensaciones, `approve` valida que no quede ningún
  perfil sin línea (`HasUncoveredJobProfileCompensationReferenceAsync` →
  `SALARY_TABULATOR_JOB_PROFILE_COVERAGE_CONFLICT`) y hace rollback. Antes de haber compensaciones
  ese chequeo era no-op.

---

<a id="h-06"></a>
## H-06 · 🟡 Regla no documentada de la matriz de competencias

`POST /api/v1/job-profiles/{id}/competency-matrix/items` exige que **todas las conductas de un ítem
compartan la terna (competencia, tipo de competencia, nivel de conducta)**; si difieren devuelve
`JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT` (`JobProfileCompetencyMatrixAdministration.cs:871-882`).

Nada en el Swagger lo dice. Un consumidor que arme un ítem con conductas de competencias distintas
—lo natural si se lee el contrato como "lista de conductas"— recibe un `409` cuyo mensaje
(*"The requested competency matrix change is not valid for the current state"*) no orienta.

Además: **`conductPublicIds` es obligatorio** (`NotEmpty` en el validador) aunque el DTO lo declare
`IReadOnlyCollection<Guid>?`. Y los campos `competencyCatalogItemId`, `competencyTypeCatalogItemId`
y `behaviorLevelCatalogItemId` de la fila **se derivan de las conductas**, no viajan en el cuerpo.

Límites no documentados: 50 conductas por ítem, 200 ítems por perfil
(`CompetencyFrameworkCommon.cs:17-18`).

---

<a id="h-07"></a>
## H-07 · 🟡 `requirements` acepta cualquier catálogo sin validar la categoría

`JobProfileRequirementCommandSupport.ResolveCatalogItemInternalIdAsync` busca el ítem **solo por
publicId**, sin comparar su `JobCatalogCategory` contra el `requirementType` del requisito.

Consecuencia: se puede colgar un ítem de `EducationLevel` en un requisito de tipo `Certification`, o
un `Training` en uno de tipo `Knowledge`, y el `POST` responde `201`. El descriptor de puesto queda
internamente incoherente sin que nada proteste.

Comportamiento útil que sí existe y conviene documentar: para ciertos `requirementType`, el
`description` **auto-resuelve o crea** el valor en los catálogos internos
(`ResolveDescriptionInternalCatalogUsageAsync` + `InternalCatalogRegistry`). O sea que
`job-profile.requirements.{education,knowledge,certification}` se pueblan solos — no hay que
sembrarlos.

---

<a id="h-08"></a>
## H-08 · 🟡 `salary-tabulator/lines` no expone el código de la clase salarial

Campos del ítem de la respuesta:

```
publicId · salaryClassPublicId · salaryScaleCode · currencyCode · baseAmount · minAmount ·
maxAmount · effectiveFromUtc · effectiveToUtc · isActive · version · concurrencyToken ·
createdAtUtc · modifiedAtUtc · allowedActions
```

Falta `salaryClassCode` / `salaryClassName`. Un tabulador se lee **agrupado por clase**, así que el
frontend necesita una segunda llamada a `position-description-catalogs/salary-classes/items` y un
join en cliente para mostrar la pantalla más obvia del módulo.

El resto de las respuestas del sistema sí desnormalizan la referencia (`orgUnitName` en el perfil de
puesto, `salaryClassCode` en la propia tabla de BD). Es una inconsistencia, no una decisión visible.

---

<a id="h-09"></a>
## H-09 · 🟡 `version` del perfil cuenta escrituras, no revisiones

Tras cargar las 9 colecciones hijas, los `version` de los 33 perfiles quedaron entre **30 y 39**,
correlacionados con la cantidad de filas hijas (`P-DG` llegó a 39 por sus muchos
`dependent-positions`) — no con revisiones del documento.

Si el frontend rotula ese campo como "versión del descriptor", muestra un número que no significa
lo que el usuario va a entender. Conviene o renombrarlo en el contrato, o versionar el descriptor de
verdad en las transiciones de estado (lo que se conecta con H-01).

---

<a id="h-10"></a>
## H-10 · 🟡 El manifiesto de catálogos apunta al catálogo equivocado

`GET /api/v1/job-profiles/catalog-manifest` —que el playbook llama "el mapa de la sección"— declara:

```csharp
// JobProfileCatalogBindingMap.cs:109
new("jobProfile", "positionCategoryPublicId", "PositionFunctionType"),
```

El campo `positionCategoryPublicId` del perfil se alimenta de `position-categories`, no de
`position-description-catalogs/position-function-types`. Un frontend que consuma el manifiesto como
binding literal ofrecería la lista equivocada en ese campo.

Puede ser deliberado —la categoría se alcanza a través de la clasificación, cuyo primer eje **es** un
tipo de función— pero no está explicado en ningún comentario. **Confirmar con el equipo.**

---

<a id="h-11"></a>
## H-11 · 🟢 Convenciones inconsistentes entre catálogos de la misma sección

Todo dentro de §4, en catálogos que el usuario percibe como equivalentes:

| Recurso | Campo de orden | Cuerpo del POST | Verbos | Inactivación |
|---|---|---|---|---|
| `occupational-pyramid-levels` | **`levelOrder`**, y es **único** | code, name, levelOrder, description | `GET POST PUT PATCH` | rutas `/activate` `/inactivate` |
| `position-description-catalogs/*/items` | `sortOrder`, libre | code, name, description, sortOrder | `GET POST PATCH` | `PATCH /isActive` |
| `position-category-classifications` | `sortOrder`, libre | + 3 ejes | `GET POST PATCH` | `PATCH /isActive` |
| `position-categories` | `sortOrder`, libre | + clasificación | `GET POST PATCH` | `PATCH /isActive` |
| `job-catalogs/{category}` | **no tiene** | **solo code, name** | `GET POST PUT PATCH DELETE` | `DELETE` real |

Cuatro ejes de inconsistencia en una sola sección: nombre del campo de orden, unicidad de ese campo,
riqueza del cuerpo, y si existe borrado real. `PUT` y `DELETE` en los tres del medio devuelven `405`
a propósito, pero `job-catalogs` sí los tiene.

Otros límites que difieren sin motivo aparente: `name` es ≤120 en pirámide ocupacional y ≤150 en el
resto; `title` del perfil es ≤180.

Esto no rompe nada, pero cada diferencia es una consulta del frontend y un caso de prueba extra.

---

<a id="h-12"></a>
## H-12 · 🟢 `inactivate` de tipo de centro de trabajo no valida uso

Ya estaba anotado en el playbook §3.4.2 y se confirma leyendo el handler:
`InactivateWorkCenterTypeCommandHandler` va de la verificación de concurrencia directo a inactivar,
**sin comprobar si hay centros de trabajo usando el tipo**. Sus dos hermanos (tipo de unidad
organizativa, área funcional, centro de costo) sí bloquean con `*_IN_USE`.

Puede ser deliberado, pero la asimetría conviene confirmarla.

---

<a id="h-14"></a>
## H-14 · 🟠 La plaza acepta cualquier salario, dentro o fuera de su banda

Probado en vivo sobre `P-RECEP`, cuya línea del tabulador es **408.80 – 550.00**:

| `configuredBaseSalary` | Resultado |
|---|---|
| `250` (bajo el mínimo legal **y** bajo la banda) | **`201`**, guardado `250.00 USD` |
| `99999` (182× el techo de la banda) | **`201`**, guardado `99999.00 USD` |

Ni rechazo ni advertencia. El playbook §4.4 pregunta exactamente esto (*"Salario fuera de la banda
del tabulador → ¿rechaza, advierte o acepta?"*): **acepta**.

### Por qué importa

Toda la cadena que construimos —`salary-classes` → solicitud de cambio → aprobación con anti-self →
línea → compensación del perfil— existe para producir una banda. Pero esa banda **solo se usa para
validar el salario negociado del empleado**:

```csharp
/// PositionSlotSalaryRange.cs
/// Salary band that governs a position slot, resolved from its job profile's active salary tabulator
/// line (PositionSlot → JobProfileCompensation → SalaryTabulatorLine). Used to validate (block) an
/// employee's negotiated base salary against the plaza's range (R-3).
```

O sea: el sistema bloquea al **empleado** por salirse de la banda, pero deja configurar la **plaza**
—que es la fuente de verdad de cuánto paga el puesto— fuera de su propia banda. Un error de captura
en la plaza se propaga a todos sus ocupantes sin que ningún control lo detenga, y encima el
tabulador aprobado con doble firma queda sin efecto práctico sobre la configuración.

Se combina con [H-05](#h-05): ni el tabulador ni la plaza conocen el mínimo legal, así que
`$250` de salario base pasa los dos filtros.

### Propuesta

Validar `configuredBaseSalary` contra la banda de la plaza en `POST`/`PUT`, con el mismo criterio que
se elija para H-05 (advertir o bloquear). El resolutor de la banda ya existe y ya se usa aguas abajo.

---

<a id="h-15"></a>
## H-15 · 🟠 No hay forma de eliminar ni desactivar una plaza

Verbos de `PositionSlotsController`:

```
GET    companies/{c}/position-slots · position-slots/{id} · /graph · /export · /diagram-export
POST   companies/{c}/position-slots
PUT    position-slots/{id}
PATCH  position-slots/{id}/status · /dependencies · /occupancy
```

**No hay `DELETE`, ni ruta `/activate` `/inactivate`, ni `isActive` entre los campos mutables.** La
tabla `position_slots` **sí** tiene columna `is_active`, pero ningún endpoint la escribe.

`PATCH /status` solo mueve entre `Vacant` / `Occupied` / `Suspended`, y una plaza suspendida sigue
contando en el listado y en los cupos.

Consecuencia: **una plaza creada por error es permanente.** En esta corrida creé dos plazas de prueba
(`PL-TEST-FUERA`, `PL-TEST-ALTO`) para el caso de H-14 y tuve que borrarlas con `DELETE` en la base de
datos porque la API no ofrece ninguna vía. Verifiqué antes que no tuvieran ocupantes ni dependientes.

Contrasta con el resto del sistema, donde cada recurso tiene o `DELETE` (colecciones del perfil,
`job-catalogs`) o `activate`/`inactivate` (centros de trabajo, centros de costo, unidades
organizativas, pirámide ocupacional) o `PATCH /isActive` (catálogos de puesto).

---

<a id="h-16"></a>
## H-16 · 🟡 El listado de plazas no expone la dependencia jerárquica

`GET /api/v1/companies/{c}/position-slots` devuelve 32 campos por ítem —incluidos los desnormalizados
`jobProfileCode`, `orgUnitName`, `workCenterCode`, `positionCategoryName`— pero **ninguno de los dos
de dependencia**: falta `directDependencyPositionSlotPublicId` y `functionalDependencyPositionSlotPublicId`.

La jerarquía sí se persiste correctamente (verificado en BD: 32 de 33 con
`direct_dependency_position_slot_id`, la raíz sin él) y sí se puede leer por
`GET .../position-slots/graph` (33 nodos, 32 aristas, 1 raíz, profundidad 5) o por el detalle.

**Efecto colateral: el script de verificación del playbook §4.4 da un falso negativo.** Cuenta las
raíces como los ítems sin `directDependencyPositionSlotPublicId` sobre el **listado**, y como el campo
no existe en esa proyección reporta `plazas raíz: 33 → REVISAR` cuando el árbol está perfecto.
Hay que reescribirlo contra `/graph`.

---

<a id="h-17"></a>
## H-17 · 🟢 `requiresGeo` acepta la coordenada (0,0)

Un tipo de centro de trabajo con `requiresGeo: true` rechaza la creación sin coordenadas
(`400 WORK_CENTER_GEO_REQUIRED`, reproducido al crear `SAL-HGR` sin geo). Pero la validación es de
**presencia**, no de validez: `SAL-EST` quedó guardado con `geo_lat = 0.000000, geo_long = 0.000000`
—la "isla nula" en el Golfo de Guinea— y pasó sin objeción.

Conviene validar rango (`lat ∈ [-90,90]`, `long ∈ [-180,180]`) y, si se quiere ser estricto, rechazar
el par exacto `(0,0)`, que en la práctica siempre es un placeholder.

---

<a id="h-18"></a>
## H-18 · 🟡 La jornada sembrada viola el invariante que la API exige, y un error mal atribuido

Dos cosas de §5.3, encontradas al configurar las jornadas.

### a) `JORNADA_ORDINARIA` llega sembrada sin días

El aprovisionamiento crea la jornada con `totalWeeklyHours = 44` y **cero días configurados**. Pero el
validador del `POST` exige lo contrario:

```csharp
RuleFor(command => command.Days).NotEmpty();
```

La semilla se construye por dentro del dominio y se salta la regla que la API impone a cualquier otro
consumidor. Una jornada sin días no puede sustentar el cálculo del séptimo día, los límites de hora
extra ni la nocturnidad — declara 44 horas que no están repartidas en ningún lado.

Se completó por `PUT` (lun-vie 08:00–17:00 con comida 12:00–13:00, sábado 08:00–12:00) y el motor
derivó **44.0 h exactas** descontando la comida, así que la derivación funciona bien. El problema es
que la semilla deja el registro a medias y nada lo señala.

### b) `WORK_SCHEDULE_DAY_INVALID` reporta mal la causa

Solo existen cuatro códigos de error en el módulo (`CODE_TAKEN`, `DAY_INVALID`, `IN_USE`, `NOT_FOUND`)
y ninguno cubre "valor de enum inválido". El handler agrupa todo en un `catch`:

```csharp
// WorkSchedules.Handlers.cs:132
catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
{
    // Day-set violations (duplicated weekday, bad meal break, night shift with meal, zero shift,
    // anchor/class out of range) → clean 422 instead of a 500 (REQ-012 §5).
    return Result<WorkScheduleResponse>.Failure(WorkScheduleErrors.DayInvalid);
}
```

El comentario admite que `anchor/class out of range` cae ahí. Resultado, probado en vivo:

| Caso enviado | Respuesta | ¿Bien atribuida? |
|---|---|---|
| `startTime == endTime` (0 h) | `422 WORK_SCHEDULE_DAY_INVALID` | ✅ |
| `totalWeeklyHours: 200` | `400`, señala `totalWeeklyHours` | ✅ |
| `days: []` | `400`, señala `days` | ✅ |
| `dayOfWeek: 7` | `400`, señala `days[0].DayOfWeek` | ✅ |
| **`scheduleClass: "NOCTURNA"`** | `422 WORK_SCHEDULE_DAY_INVALID` — *"the days are not valid (weekday, shift times or meal break)"* | ❌ los días eran válidos |
| **`attendanceDateAnchor: "MEDIO"`** | idem | ❌ los días eran válidos |

Evitar el `500` es correcto; atribuirlo a los días no. Quien reciba ese error va a revisar los
horarios cuando el problema está en otro campo. Basta con dos códigos más
(`WORK_SCHEDULE_CLASS_INVALID`, `WORK_SCHEDULE_ANCHOR_INVALID`) validados antes del `Create`.

---

<a id="h-19"></a>
## H-19 · 🟠 Falta la capa que deriva horas extra desde la jornada, y tres de sus insumos

**Flujo esperado** (según el equipo funcional): la jornada se configura **por empleado**. Puede ser
diurna, nocturna, o una jornada *custom* acordada individualmente. Y hay empleados que
**deliberadamente no tienen horario** —los directores, por ejemplo— que por eso mismo **pueden
trabajar más de 44 horas sin generar horas extra**.

> **Contexto, no defecto: que las horas extra se registren a mano es diseño deliberado.** Hoy no hay
> marcadores. El registro manual es —y seguirá siendo— el camino de excepción para horas adicionales
> que haya que capturar. Cuando existan marcadores, la intención es que el marcador aporte las
> marcaciones, que de ahí se **propongan** registros de horas extra automáticamente, y que un humano
> los valide. Este hallazgo NO reclama un módulo de asistencia: describe la capa de derivación que ese
> flujo va a necesitar y los insumos que hoy le faltan.

Verifiqué la implementación pieza por pieza. La mitad del flujo existe; la regla que le da sentido, no.

### Lo que sí está construido

| Pieza | Estado | Evidencia |
|---|---|---|
| Jornada por empleado | ✅ | `workdayCode` en la asignación de empleo |
| Jornada **opcional** (empleado sin horario) | ✅ | `RuleFor(input => input.WorkdayCode).MaximumLength(80).When(input => !string.IsNullOrWhiteSpace(input.WorkdayCode))` — y el resolutor devuelve `null` "when valid / not supplied" |
| Validación del código cuando sí viene | ✅ | debe resolver a una jornada **activa** del maestro, comparando en mayúsculas → `422 WORK_SCHEDULE_INVALID` |
| Jornada nocturna | ✅ | un día que cruza medianoche (`end < start`) se reconoce como nocturno y **no puede llevar tiempo de comida** (`WorkSchedule.cs:149,199-210`) |
| Jornada custom por acuerdo | ✅ | se crean N jornadas a nivel de empresa y se asignan por código |

### Lo que NO existe

| Pieza faltante | Comprobación |
|---|---|
| **La derivación misma**: algo que compare lo trabajado contra la jornada del empleado | El módulo `Features/PersonnelFiles/Overtime/` **no referencia** `workdayCode` ni `WorkSchedule` en ningún archivo. Lo único que se parece es `FechaJornada`, que es la fecha del día trabajado, no la jornada. Nada suma la semana ni la compara con `totalWeeklyHours` |
| Concepto de **exención de horas extra** | `grep -riE "exemptFromOvertime\|overtimeExempt\|isExempt"` sobre todo `src` → **0 resultados** |
| Que el **canal de origen se pueda declarar** | `origin_channel` existe en la tabla, con constantes `MANUAL`/`RRHH`/`PORTAL`, y la bandeja ya filtra por él. Pero **no está en el input de creación**: se infiere del llamante — `var originChannel = isManager ? Rrhh : Portal` (`OvertimeRecords.Handlers.cs:182`). Una integración de marcador no puede estampar su propio canal; sus registros quedarían como `RRHH`, indistinguibles de los capturados a mano |
| **Carga por lote** | El único alta es `POST /personnel-files/{publicId}/overtime-records`, un registro por empleado por llamada. Una sincronización nocturna sobre las 60 posiciones ocupables serían 60+ llamadas, cada una con su transacción y su auditoría |
| Que el tope diario dependa del empleado o de su jornada | El cap viene de `preferences.MaxDailyMinutes` — preferencia **de toda la empresa** (`OvertimeRecords.Handlers.cs:217`). Un solo valor no puede servir a una jornada de 8 h y a otra de 11 h en el mismo tenant |
| Clasificación diurna/nocturna **de la jornada** | `scheduleClass` solo admite `ORDINARIA`/`EXTRAORDINARIA`. Ni `work_schedules` ni `work_schedule_days` tienen columna de nocturnidad: se infiere por día desde las horas (`end < start` ⇒ nocturno) |

### Lo que ya está listo para el flujo del marcador

Vale registrarlo porque el chasis existe en parte y conviene no reconstruirlo:

| Pieza | Estado |
|---|---|
| Columna `origin_channel` + constantes `MANUAL`/`RRHH`/`PORTAL` | ✅ modelada y persistida |
| La bandeja filtra y reporta por canal | ✅ `OriginChannel` está en las proyecciones |
| Máquina de estados `EN_REVISION → AUTORIZADA` | ✅ **es exactamente el flujo "proponer y validar"**: el generador crea en `EN_REVISION`, un humano autoriza |
| Precedente de "generado por el motor" | ✅ `OvertimeApplicationOrigins` ya distingue `MANUAL`/`MOTOR`/`LIQUIDACION` en la capa de aplicación |
| Tipos de HE con factor + calendario de asuetos cargado | ✅ los insumos para clasificar `HED`/`HEN`/`HEDF`/`HENF` existen |

Lo que `OvertimeRecords.Rules.cs` sí valida: periodo de nómina y ventana de captura, horas/minutos > 0,
factor contra el snapshot del tipo, tope diario de empresa, máquina de estados, y que la fecha no sea
futura. **Nunca si el empleado tiene jornada.**

### Consecuencia

**Un director sin jornada puede tener horas extra registradas, autorizadas y pagadas.** Nada lo impide.
La regla "sin horario no se pagan horas extra" hoy vive solo en la cabeza de quien configura: si alguien
captura horas extra a un director dentro de la ventana del periodo, el sistema las acepta, el
autorizador las aprueba y el motor las paga como línea calculada.

### El agravante: `null` es ambiguo

Como `workdayCode` es un vínculo laxo por código (sin FK ni snapshot) y no hay bandera de exención, el
motor **no puede distinguir** dos situaciones opuestas:

| Situación | `workdayCode` |
|---|---|
| Director exento de horario, a propósito | `null` |
| Empleado al que **falta** configurarle la jornada | `null` |

Son indistinguibles. La primera debería bloquear horas extra; la segunda debería levantar una alerta de
configuración incompleta. Hoy las dos pasan sin decir nada, que es el peor de los dos comportamientos
para ambas.

### Propuesta, en dos fases

**Fase 1 — sirve al camino manual, que ya está en uso hoy:**

1. **Hacer explícita la exención**, no inferirla de la ausencia. Un campo en la asignación
   (`overtimeExempt`, o un `scheduleExemptionReason`) distingue "director sin horario" de "falta
   configurar". Es el mismo patrón que ya se usó en otros módulos para no confundir ausencia con
   decisión. **Este punto es el único que falla hoy y va a fallar sistemáticamente con el marcador**:
   sin él, el generador automático le crearía horas extra a los directores.
2. **Bloquear el registro de horas extra** cuando el empleado está exento →
   `422 OVERTIME_EMPLOYEE_EXEMPT`. En el registro, no en la autorización, para que el error aparezca
   donde se comete.
3. **Advertir** —no bloquear— cuando el empleado no está exento y tampoco tiene jornada: es un hueco de
   configuración, y el criterio "advertir, nunca bloquear" ya se usó en endeudamiento.
4. Validar el tramo horario (ver [H-20](#h-20)), que es prerrequisito de cualquier derivación posterior.

**Fase 2 — cuando exista el marcador:**

5. Construir la **derivación**: marcaciones + jornada del empleado + topes (diario y semanal) + asuetos
   ⇒ registros propuestos en `EN_REVISION`, con tipo y factor **derivados** de las horas reales y del
   calendario, no capturados.
6. Permitir **declarar el canal** en el alta (`MARCADOR` o similar) para que la bandeja distinga lo
   propuesto por máquina de lo capturado a mano — hoy el canal se infiere del llamante.
7. Agregar **alta por lote** para la sincronización periódica.
8. Evaluar si el tope diario debe derivarse de la jornada del empleado en vez de una preferencia única
   de empresa.

> Pendiente de cierre funcional. Se conecta con [H-01](#h-01): en los dos casos el sistema modela el
> dato pero no la regla que lo gobierna. Y con [H-20](#h-20), que es el defecto concreto que hoy
> permite pagar dos veces las mismas horas.

---

<a id="h-20"></a>
## H-20 · 🔴 El tramo horario de la hora extra no se valida contra nada: doble pago silencioso

**Regla esperada** (según el equipo funcional): una hora extra **no puede caer dentro de la jornada
asignada al empleado**. Si cae dentro, es un error — esa franja ya está cubierta por el acuerdo de
jornada y se paga como salario ordinario.

**La regla no existe. Y el problema es más amplio: `StartTime` y `EndTime` son campos decorativos.**

Verificado sobre `Features/PersonnelFiles/Overtime/` y `PersonnelFileOvertimeRecord.cs`:

| Validación que uno esperaría | Existe |
|---|---|
| El tramo no solapa con la jornada del empleado ese día | ❌ el módulo no referencia la jornada en ningún archivo |
| `EndTime > StartTime` | ❌ ni eso. Se puede registrar 17:00 → 09:00 |
| El tramo es coherente con `DurationHours`/`DurationMinutes` | ❌ nunca se cruzan |
| Dos registros del mismo día no solapan entre sí | ❌ `grep -iE "overlap\|solape\|traslape\|intersect"` → **0 resultados** |
| El tipo elegido (`HED`/`HEN`/`HEDF`/`HENF`) concuerda con el tramo y con el calendario de asuetos | ❌ el tipo es captura manual; el factor se sobrescribe con solo una nota |

Los dos campos se almacenan y se devuelven en las proyecciones, y **nada más**: no hay una sola
comparación, umbral ni excepción que los consuma.

### El escenario concreto

Empleado con jornada lun-vie 08:00–17:00. Alguien registra una hora extra el martes de **09:00 a
11:00**, 2 horas, tipo `HED` (×2).

| Qué debería pasar | Qué pasa |
|---|---|
| Rechazo: esas 2 h ya están dentro de la jornada acordada | `201`. Se autoriza, se aplica y el motor la valora en `Σ(horas × factor)` |

Resultado: **esas 2 horas se pagan dos veces** — una como salario ordinario (van dentro del sueldo del
periodo) y otra como hora extra al doble. Sobrepago de 2 h × factor 2 sin que nada lo señale.

### Lo que lo hace peor: el tramo no puede servir ni de rastro de auditoría

Lo que se paga sale de `DurationHours`/`DurationMinutes`, **no del tramo**. Como los dos no se cruzan:

- se puede registrar duración **8 h** con tramo **10:00–11:00** (1 h) → **se pagan 8 h**;
- el revisor que abra la bandeja para autorizar ve un tramo que no tiene por qué coincidir con lo que
  va a pagar.

Es decir: el control manual que hoy sustituye a la validación automática **tampoco puede detectar el
error**, porque el dato que mostraría la inconsistencia no está obligado a ser consistente.

### Y con dos registros el mismo día

`ValidateDailyCap` suma **minutos** (`existingActiveMinutes + newMinutes` contra
`preferences.MaxDailyMinutes`). Dos registros de 14:00–18:00 y 16:00–20:00 suman 8 h contra el tope,
pero **las 2 h de solape son invisibles**: el sistema no sabe que se está pagando dos veces la misma
franja de reloj.

### Propuesta

1. **Validar el tramo en sí**, primero: `EndTime > StartTime` (o cruce de medianoche explícito, como ya
   hace la jornada), y coherencia con la duración declarada — o eliminar uno de los dos para que no haya
   dos fuentes de verdad.
2. **Rechazar el solape con la jornada** del empleado para esa fecha →
   `422 OVERTIME_WITHIN_SCHEDULED_SHIFT`. Es la regla que motivó este hallazgo y la que evita el doble
   pago. Requiere que el módulo de horas extra resuelva la jornada del empleado, que es la misma pieza
   que pide [H-19](#h-19).
3. **Rechazar el solape entre registros** del mismo empleado y fecha →
   `422 OVERTIME_RECORDS_OVERLAP`.
4. **Derivar el tipo** del tramo y del calendario de asuetos (`HED`/`HEN` por la hora, `HEDF`/`HENF`
   por el asueto), dejando el override manual con nota como excepción — ya existe ese mecanismo para el
   factor, se puede reusar el patrón.

> A diferencia de [H-19](#h-19), **este defecto no depende del marcador**: la vía de doble pago está
> abierta hoy, en el camino manual, que es el que está en uso.

---

<a id="h-21"></a>
## H-21 · 🟠 Los catálogos de conceptos devuelven `[]` sin `countryCode`

`§5.6` manda verificar dos catálogos de sistema y dice, con razón: *"si están vacíos, el motor de planilla
y el de finiquitos no tienen con qué calcular"*. Ejecutado tal como está escrito:

```
GET /api/v1/compensation-concept-types  → 200 []
GET /api/v1/settlement-concepts         → 200 []
```

**Pero los catálogos están completos.** En la base de datos hay **19** tipos de concepto de compensación
y **22** conceptos de liquidación, todos con `country_code = 'SV'` y todos activos — y la empresa de
prueba es `SV`. Con el parámetro explícito aparecen:

```
GET /api/v1/compensation-concept-types?countryCode=SV  → 200, 19 items
GET /api/v1/settlement-concepts?countryCode=SV         → 200, 22 items
```

### Causa

`countryCode` es un query param **opcional** en el contrato, pero el repositorio lo trata como
obligatorio y devuelve lista vacía cuando no resuelve:

```csharp
// PersonnelFileRepository.cs:1405-1412
string? countryCode, ...
var countryCatalogItemId = await ResolveCountryCatalogItemIdAsync(countryCode, cancellationToken);
if (countryCatalogItemId is null)
{
    return [];
}
```

Con `countryCode` nulo, `ResolveCountryCatalogItemIdAsync` no resuelve nada y el método corta con `[]`.

### Por qué importa más de lo que parece

1. **Es un generador de falsas alarmas, y del peor tipo.** Un `200 []` es indistinguible de "el catálogo
   está vacío". Quien siga §5.6 al pie de la letra va a concluir que el ambiente está sin sembrar y que
   el motor de planilla no puede calcular — cuando el problema es que le falta un parámetro a la URL.
   Es exactamente el diagnóstico equivocado que el paso pretendía prevenir.
2. **El país del tenant es conocido.** El JWT trae `tid` y la empresa tiene `country_code`. El endpoint
   podría resolverlo solo, como hacen otros endpoints del sistema.
3. **El contrato miente.** Si el parámetro es obligatorio, debería declararse obligatorio y responder
   `400` cuando falta, no `200 []`.

### Propuesta

- Resolver el país del tenant cuando `countryCode` viene nulo (comportamiento esperado y el que menos
  rompe al frontend), y reservar el parámetro para consultas cross-country del backoffice.
- Si se prefiere mantenerlo explícito, marcarlo requerido y devolver `400` — cualquier cosa menos
  `200 []`.
- Documentar el parámetro en §5.6 del playbook, que hoy lista el endpoint sin él.

---

<a id="h-22"></a>
## H-22 · 🔴 `document_type_catalog_items` está vacío y no tiene endpoint que lo llene

Encontrado al preparar §7. La tabla tiene **0 filas** y es referenciada por **16 controladores** en un
campo **obligatorio**:

```csharp
// PersonnelFileRequests.cs:521
public sealed record AddMedicalClaimDocumentRequest(
    Guid FilePublicId,
    Guid DocumentTypeCatalogItemPublicId,   // ← Guid no anulable
    string? Observations);
```

Mismo patrón en los adjuntos de reconocimientos, amonestaciones, incapacidades, ayuda económica,
transacciones fuera de nómina, créditos de tiempo compensatorio y documentos del expediente.

**Y no existe ninguna ruta HTTP para crear los tipos.** Los comandos están escritos —
`CreateDocumentTypeCatalogItemCommand`, `UpdateDocumentTypeCatalogItemCommand`,
`ActivateDocumentTypeCatalogItemCommand`, `InactivateDocumentTypeCatalogItemCommand` en
`Features/DocumentTypeCatalogs/` — pero **ningún controlador los despacha**. Búsqueda de
`document-type` sobre las 591 rutas indexadas: **0 resultados**.

### Consecuencia

**Toda la superficie de adjuntos del módulo de expedientes es inalcanzable por API.** No se puede
adjuntar el documento de una incapacidad (que además es obligatorio si
`incapacityRequiresDocument: true`, como quedó configurado en las preferencias), ni el respaldo de un
crédito de tiempo compensatorio (que el playbook indica obligatorio en el `POST`), ni el soporte de una
amonestación o de un reclamo médico.

No es "falta sembrar un catálogo": es que **no hay forma de sembrarlo** salvo escribiendo en la base de
datos. Comandos huérfanos sin controlador es un caso distinto de los demás hallazgos — la lógica existe
y está probada, lo que falta es exponerla.

### Ojo con la confusión de nombres

Son **dos catálogos distintos**, y conviene no mezclarlos:

| Tabla | Filas | Para qué |
|---|---|---|
| `identification_type_catalog_items` | **4** ✅ | identidad de la **persona**: `DUI`, `NIT`, `PASSPORT`, `RESIDENT_CARD`, cada uno con su regex de formato por país |
| `document_type_catalog_items` | **0** ❌ | tipo del **archivo adjunto** a una transacción |

No hay duplicación funcional entre ellos. El riesgo es de diagnóstico: al preparar §7 asumí que las
identificaciones salían del segundo y habría escrito la sección contra una tabla vacía.

### Propuesta

Exponer un controlador para `document-type-catalogs` (los cuatro comandos ya existen), y decidir si los
tipos se siembran por país como los de identificación — probablemente sí, porque un tipo de documento
de respaldo no depende de cómo se organiza cada empresa.

---

<a id="h-23"></a>
## H-23 · 🟡 `occupiedEmployees` de la plaza es un contador que nadie mantiene ni lee

La plaza expone `maxEmployees` y `occupiedEmployees`, y la tabla persiste los dos. Pero:

- **La asignación no lo actualiza.** Verificado en `EmploymentAssignments.Handlers.cs`: crear, editar o
  borrar una asignación no toca `occupiedEmployees`. Solo se mueve con
  `PATCH /position-slots/{id}/occupancy`, a mano.
- **El control de cupo no lo usa.** Y esto es la buena noticia: la capacidad **sí se valida**, pero
  contra la realidad, no contra el contador. `EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED` cuenta las
  asignaciones activas **cuya vigencia se solapa** con la ventana del candidato y las compara con
  `slot.MaxEmployees` (capacity-by-vigencia, RF-005). Correcto y más fino que un contador.

Entonces `occupiedEmployees` es un campo **denormalizado que ningún escritor mantiene y ninguna regla
consume**. Está garantizado que se desincronice, y un consumidor del `GET` lo va a leer como si fuera
autoritativo — es el nombre más natural para "cuántos hay adentro".

### Propuesta

Dos salidas limpias, cualquiera sirve:

- **Derivarlo** en la proyección de lectura desde las asignaciones activas y dejar de persistirlo, o
- mantenerlo automáticamente en el mismo commit de la asignación y quitar el `PATCH /occupancy` como
  escritura pública.

Lo que no conviene es dejarlo como está: un número que parece un hecho y es una nota manual.

---

<a id="h-24"></a>
## H-24 · ❌ RETIRADO — era un error de lectura mío

**Lo que afirmé:** que `PATCH /personnel-files/{id}/finalize` significaba "retiro definitivo" y que su
nombre invitaba a un error peligroso.

**Es falso.** Verificado contra el controlador:

> *"Finalize a personnel file — Transitions the specified personnel file out of `Draft` (and optionally
> provisions a user account). Requires the `If-Match` header with the current `concurrencyToken`."*

`finalize` hace exactamente lo que su nombre dice: **finaliza el alta del expediente**, sacándolo de
`Draft` y opcionalmente provisionando la cuenta de usuario. El endpoint está bien nombrado.

**El retiro vive en otro controlador**: `RetirementRequestsController`, en
`/personnel-files/{publicId}/retirement-requests`.

Cómo me equivoqué: vi `finalize` junto a `finalize/preview` y lo asocié con la feature de retiro
definitivo que conocía de otro contexto, sin leer la descripción del endpoint. La corrección salió de
ejecutar §7: los pasos de información de empleo y de salario fallaban con
`422 PERSONNEL_FILE_STATE_RULE_VIOLATION`, y rastrear ese error llevó a `IsCompletedEmployee` y de ahí a
`finalize` como la transición que faltaba.

El hallazgo real que salió de eso es [H-25](#h-25).

---

<a id="h-25"></a>
## H-25 · 🟠 `finalize` es un paso obligatorio del alta que no se descubre hasta que algo falla

El expediente nace en `LifecycleStatus = Draft`. Y en `Draft` **no se puede escribir la información de
empleo ni los conceptos de compensación**:

```csharp
// EmployeeProfiles.cs:188-191
if (!personnelFile!.IsCompletedEmployee)
{
    return Result<PersonnelFileEmployeeProfileResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
}

// PersonnelFile.cs:146-148
public bool IsCompletedEmployee =>
    RecordType == PersonnelFileRecordType.Employee &&
    LifecycleStatus == PersonnelFileLifecycleStatus.Completed;
```

Para salir de `Draft` hay que llamar `PATCH /personnel-files/{id}/finalize` con `If-Match`.

### Por qué es un problema de contrato, no de implementación

La regla es razonable —no cargar sueldo a un expediente a medio llenar— pero **el consumidor no tiene
forma de anticiparla**:

1. **El error no dice qué hacer.** `PERSONNEL_FILE_STATE_RULE_VIOLATION` con *"The requested operation is
   not allowed for the current personnel file state"* no menciona `Draft`, ni `Completed`, ni `finalize`.
   Hay que leer el código para descubrir el paso que falta.
2. **El mismo código de error tapa causas distintas.** `StateRuleViolation` se usa también como destino
   de `catch (InvalidOperationException)` en otros handlers (idiomas, referencias), donde significa
   "invariante de dominio violada", no "estado incorrecto". Al depurar, el código no distingue si falta
   finalizar o si el dato es inválido.
3. **La asignación de plaza sí funciona en `Draft`.** Ese es el detalle que desorienta: de los seis pasos
   del alta, identificaciones, cuenta bancaria **y asignación de plaza** pasan en `Draft`; solo
   información de empleo y salario exigen `Completed`. No hay una frontera visible.
4. **Y `finalize` recibe un `positionSlotPublicId`**, lo que sugiere que la intención del diseño es
   finalizar *con* la plaza — o sea que el orden previsto probablemente sea otro del que uno deduce
   leyendo los endpoints por separado.

### Propuesta

- Que el error nombre la transición faltante: `PERSONNEL_FILE_NOT_FINALIZED` con un mensaje que diga
  "llamar `PATCH /finalize` antes de escribir información de empleo o compensación".
- Documentar el orden en el Swagger del `create`, que hoy solo advierte que los sub-recursos van aparte.
- Separar `StateRuleViolation` de los `catch (InvalidOperationException)` que lo reutilizan, para que el
  código de error signifique una sola cosa.

> Quedó documentado en §7.0 del playbook como paso explícito del camino crítico.

---

<a id="h-26"></a>
## H-26 · 🔴 Una fecha sin zona produce `500` en la asignación de plaza

Reproducido de forma aislada sobre el mismo expediente y la misma plaza, cambiando **solo** el formato
de `startDate`:

| `startDate` enviado | Respuesta |
|---|---|
| `"2026-08-01"` | **`500 common.unexpected`** — *"Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported"* |
| `"2026-08-01T00:00:00Z"` | `422` de negocio, correcto |

El campo del DTO es `DateTime`. Una fecha sin zona deserializa con `Kind=Unspecified`, llega intacta
hasta Npgsql y revienta ahí — **después** de pasar validación, ya dentro de la escritura.

### Por qué importa

- **`"2026-08-01"` es la forma natural de mandar una fecha.** El campo se llama `startDate`, es
  semánticamente un día, no un instante. Cualquier cliente lo va a mandar así.
- El `500` **filtra el detalle interno** de la base de datos en el `detail` de la respuesta.
- Afecta a los 60 intentos de asignación de la corrida: el 100 % falló con `500` hasta cambiar el
  formato.
- No es exclusivo de este endpoint: **todo DTO con `DateTime` sobre columna `timestamptz`** tiene el
  mismo agujero. En §7 lo vi en `startDate` de la asignación; conviene barrer el resto
  (`birthDate`, `issuedDate`, `hireDate`, `effectiveFromUtc`, …).

### Propuesta

Tres opciones, de menos a más invasiva:

1. Configurar el serializador para normalizar `Kind=Unspecified` a `Utc` en el binding de entrada — un
   solo punto, arregla todos los endpoints a la vez.
2. Usar `DateOnly` en los DTO cuyo dominio es un día (`startDate`, `hireDate`, `birthDate`), que es lo
   que ya se hace en otros módulos: `VacationPlanLineRequest` usa `DateOnly`.
3. Validar el `Kind` y devolver `400` con un mensaje claro, en vez de dejar que falle en la capa de
   datos.

La opción 2 es la correcta a largo plazo y la 1 la que cierra el `500` hoy.

---

<a id="h-27"></a>
## H-27 · 🟠 Cuentas bancarias: duplicados exactos y N primarias simultáneas, sin validación

Descubierto por accidente al ejecutar §7. Un script mío repitió el `POST` de cuenta bancaria en varias
pasadas y el sistema lo aceptó todas las veces. Estado que quedó en la base:

```
382 cuentas · 59 empleados · 59 números de cuenta distintos · 382 con isPrimary = true
```

Es decir: **hasta 7 copias exactas de la misma cuenta** (mismo banco, misma moneda, mismo número, mismo
tipo) para el mismo empleado, **y las 382 marcadas como primaria**.

### Verificado en las tres capas

**Dominio** — solo agrega, sin mirar lo que ya hay:

```csharp
// PersonnelFile.cs:577
public void AddBankAccount(PersonnelFileBankAccount item)
{
    item.SetTenantId(TenantId);
    _bankAccounts.Add(item);
    RefreshConcurrencyToken();
}
```

**Handler** — `BankAccounts.Handlers.cs` no tiene ninguna comprobación de duplicado ni de primaria
única, y el módulo no declara **ningún** error propio para esos casos.

**Base de datos** — los índices de `personnel_file_bank_accounts` son solo la PK, el `public_id` único y
tres índices de búsqueda. **No hay índice único** sobre `(personnel_file_id, account_number)` ni sobre
"una sola primaria por expediente".

### Por qué importa

1. **La cuenta primaria es la que decide dónde se deposita el sueldo.** Con N cuentas primarias, el
   criterio de selección queda en manos de lo que devuelva la consulta — orden no determinista. La
   asignación tiene `paymentBankAccountPublicId` para fijarla explícitamente, pero nada obliga a usarlo,
   y si está en `null` el pago depende de cuál "primaria" gane.
2. **Los duplicados exactos no tienen lectura de negocio.** Una segunda cuenta con el mismo número en el
   mismo banco no es un caso de uso; es un doble clic o un reintento. Y en un módulo de nómina, una fila
   de más en las cuentas del empleado es exactamente el tipo de dato que después se audita.
3. **Contrasta con el resto del sistema.** Los códigos de catálogo tienen índice único por tenant, los
   centros de costo tienen `uq_..._tenant_code`, las unidades organizativas también. Acá no hay nada.

### Nota de contrato del mismo módulo

El `DELETE` de una cuenta exige en `If-Match` el **`concurrencyToken` de la cuenta**, no el del
expediente. Mandar el del padre devuelve `409 CONCURRENCY_CONFLICT`, que sugiere "alguien más lo
modificó" cuando en realidad es "mandaste el token equivocado". El token correcto viene en cada ítem de
`GET /personnel-files/{id}/bank-accounts` (el listado es un array plano, sin ETag de colección).

### Propuesta

- Índice único sobre `(personnel_file_id, normalized_account_number, bank_catalog_item_id)` y rechazo
  con un código propio (`BANK_ACCOUNT_DUPLICATE`).
- Al marcar una cuenta como primaria, desmarcar las demás en el mismo commit — el patrón que ya usa
  `set-primary` de representantes legales.
- Revisar si las otras colecciones del expediente con `isPrimary` (identificaciones, entre otras) tienen
  el mismo hueco. En §7.2 quedó anotado como caso a probar y no se verificó.

---

<a id="h-28"></a>
## H-28 · 🔴 La antigüedad para vacaciones se toma de la plaza, no del ingreso

**Escenario planteado por el equipo funcional:** una empresa arranca hoy y registra 100 empleados que ya
llevan años trabajando. El sistema debe otorgarles sus días de vacaciones **atados a su fecha de
ingreso**, para concederlos en la fecha que corresponde según la ley salvadoreña.

**No funciona así.** El resultado medido: **0 de 59 empleados elegibles**, incluidos los que tienen 2.5
años de antigüedad.

### Lo que sí está bien construido

Conviene decirlo primero, porque el mecanismo existe y es bueno:

| Pieza | Estado |
|---|---|
| Generación masiva del fondo anual | ✅ `POST /companies/{c}/vacation-periods/generate` |
| Idempotencia por empleado-año | ✅ una re-corrida no duplica |
| Regla del Art. 177 codificada | ✅ `IsEligible(ancla, asOf) => asOf >= ancla.AddYears(1)` |
| Días por defecto desde la preferencia de empresa | ✅ toma los 15 configurados |
| Aniversario configurable | ✅ `useAnniversary`, con manejo del 29 de febrero |
| Reporte por fila de los no elegibles | ✅ `errors[]` con nombre y motivo |
| Alta manual de un periodo | ✅ `POST /personnel-files/{id}/vacation-periods` |

El problema es **de qué fecha se mide la antigüedad**.

### La causa raíz

```csharp
// PersonnelFileVacationRepository.cs:78-94  (GetAnchorDateAsync)
.Where(assignment => assignment.PersonnelFileId == personnelFileId && assignment.IsActive)
.OrderByDescending(assignment => assignment.IsPrimary)
.ThenBy(assignment => assignment.StartDate)
.Select(assignment => (DateTime?)assignment.StartDate)
...
if (start is not null) return DateOnly.FromDateTime(start);

// solo si NO hay asignación activa cae al ingreso:
var hireDate = await dbContext.PersonnelFileEmployeeProfiles
    .Where(profile => profile.PersonnelFileId == personnelFileId)
    .Select(profile => (DateTime?)profile.HireDate)...
```

El ancla es el **`startDate` de la asignación de plaza**. El `hireDate` es solo un *fallback* para cuando
el empleado **no tiene ninguna asignación** — o sea, justo el caso en el que tampoco se puede calcular
nada. El propio parámetro de la regla se llama `hireOrPlazaStart`, lo que sugiere que la intención era
"el que aplique", pero la implementación siempre prefiere la plaza.

### Por qué eso rompe el arranque, con medición

Se combina con otra regla que **por sí sola es correcta**: la asignación no puede empezar antes de la
vigencia de la plaza (`SlotIsEffectiveFor` → `EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE`). Y las
plazas se crean el día del arranque.

Resultado inevitable: **toda asignación de una empresa que arranca hoy empieza hoy**, así que el ancla de
todos los empleados es hoy, y nadie cumple el año hasta dentro de 12 meses.

Medido en la corrida, con antigüedades reales de 7 meses a 2.5 años:

```
POST /companies/{c}/vacation-periods/generate  { "year": 2026 }
→ 200 · { totalEmployees: 59, created: 0, skipped: 0 }
   errors: 59 × VACATION_ELIGIBILITY_NOT_MET
```

Y en el caso individual más claro:

| | |
|---|---|
| Empleado | José Antonio Hernández Ramos |
| `hireDate` | **2024-02-01** → 2.5 años de antigüedad |
| `startDate` de su asignación | **2026-08-01** → una semana |
| `POST /vacation-periods` año 2026 | **`422 VACATION_ELIGIBILITY_NOT_MET`** — *"has not yet completed one year of service (Art. 177) at the start of the period"* |
| `GET /vacation-fund` | `totalGrantedDays: 0` · `totalPendingDays: 0` · `totalProvisionAmount: 0` |

### El impacto

1. **Ningún empleado tiene vacaciones durante el primer año de uso del sistema**, sin importar cuántos
   años lleve en la empresa. Para una empresa que migra 100 empleados, son 100 personas sin fondo.
2. **No hay salida por API.** `AddVacationPeriodRequest` permite fijar `legalDaysGranted`,
   `benefitDaysGranted`, `useAnniversary` y `generatesEnjoymentDays`, pero **no** el ancla ni saltar la
   elegibilidad. El `422` corta antes de crear.
3. **Contamina la provisión contable.** El fondo alimenta la reserva financiera
   (`provisión = pendientes × diaria × 1.30`, D-25). Con 0 días otorgados, la provisión de vacaciones de
   toda la empresa es 0 — un pasivo laboral real que no queda registrado.
4. **Es un incumplimiento, no una molestia.** El derecho a vacación remunerada nace de la antigüedad en
   la relación laboral, no de cuándo se cargó el dato en un sistema.
5. **Se veía venir en §6.5 y lo leí como normal.** El plan de vacaciones 2027 se creó con las 59
   advertencias `VACATION_PLAN_WARNING_INSUFFICIENT_FUND`. Las interpreté como correctas —"nadie tiene
   fondo devengado todavía"— cuando en realidad eran el síntoma de este defecto.

### Propuesta

1. **El ancla debe ser el `hireDate`** para la elegibilidad del Art. 177: es la antigüedad en la
   **empresa**, no en la plaza. El `startDate` de la plaza sirve para otras cosas (imputación de costo,
   historial de puesto), no para el derecho a vacación. Invertir la precedencia de
   `GetAnchorDateAsync`: `hireDate` primero, plaza como fallback.
2. Si se quiere conservar el caso de la plaza —por ejemplo para un recargo de funciones que no reinicia
   antigüedad— hacerlo explícito con un campo, no por precedencia implícita.
3. **Verificar el resto de los consumidores del ancla.** El mismo `GetAnchorDateAsync` alimenta el
   cálculo del fondo y probablemente el aniversario del periodo; si el ancla cambia, hay que revisar que
   la ventana del periodo también quede sobre el aniversario de ingreso.
4. Para la migración inicial, evaluar si hace falta además una vía de carga del fondo histórico (días ya
   gozados y pendientes de años anteriores) — hoy `source_code` admite `MANUAL` y `GENERACION_MASIVA`,
   pero el `422` bloquea las dos.

> **Sobre la segunda parte de la pregunta funcional** —"cuando un empleado ya gozó su vacación se puede
> automatizar con las solicitudes"— eso **sí está construido**:
> `/personnel-files/{id}/vacation-requests` con `/decision`, `/cancellation` y `/returns`, más la bandeja
> corporativa `/companies/{c}/vacation-requests/query` y `/vacations/calendar`. Lo que falta es el fondo
> del cual descontar.

---

<a id="h-29"></a>
## H-29 · 🔴 No existe el reporte de planilla por empleado: ni el Excel ni el JSON para la matriz

**Requerimiento planteado por el usuario durante §8.** Se necesita un reporte con **una fila por
empleado** y estas 17 columnas:

> Código de Empleado · Nombre del Empleado · Días Trabajados · Salario base · Salario quincenal ·
> Bonos · Comisiones · Horas extras · Ingresos adicionales (reembolsos, viáticos, etc.) · **Ingreso
> total** · ISSS · AFP · Renta · Descuentos Externos · Descuentos Internos · **Total descuentos** ·
> **Líquido a Pagar**

Y el mismo contenido en JSON, para pintarlo como matriz en el website.

**Ninguna de las dos superficies existe.** Lo que hay hoy son cinco exports y ninguno tiene esa forma:

| Endpoint existente | Grano | Por qué no sirve |
|---|---|---|
| `…/payroll-runs/{run}/lines/export` | **una fila por línea** (406 filas para 59 empleados) | es el detalle largo, no la matriz; los totales vienen como filas `TipoFila=TOTAL_POR_CONCEPTO`, no como columnas |
| `…/payroll-runs/{run}/bank-reconciliation/export` | una por empleado | solo el neto y los datos bancarios — no desglosa nada |
| `…/payroll-runs/{run}/employer-cost-report/export` | una por empleado | solo carga patronal; no lleva descuentos del empleado ni líquido a pagar |
| `…/compliance-reports/income-tax-withholding/export` (F-14) | una por empleado, **mensual** | solo renta; consolida el mes, no la corrida |
| `…/compliance-reports/social-security-contributions/export` (Planilla Única) | una por empleado, **mensual** | solo ISSS + AFP; ídem |

### En JSON tampoco hay nada equivalente

Verificado endpoint por endpoint en `PayrollRunsController` y `PayrollRunsReportingController`:

- `GET …/payroll-runs/{run}` → **cabecera de la corrida**: 4 totales agregados, sin desglose por empleado.
- `GET …/payroll-runs/{run}/employees/{personnelFilePublicId}` → **un solo empleado**, y devuelve sus
  líneas sin pivotear. Para armar la matriz de 59 habría que hacer 59 llamadas y pivotear en el cliente.
- `POST …/payroll-runs/employee-history/query` → **exige el empleado**, no acepta "todos":

  ```csharp
  // PayrollRunsReporting.cs:311
  RuleFor(query => query.PersonnelFilePublicId).NotEmpty();
  ```

  Reproducido en vivo: omitirlo devuelve
  `400 common.validation · {"personnelFilePublicId": ["'Personnel File Public Id' must not be empty."]}`.
  Además su grano es una fila **por corrida**, no por concepto.

No existe ningún `PayrollRunEmployee…Response` cuyo grano sea "todos los empleados de una corrida, una
fila cada uno". El único listado paginado de la corrida es `POST …/payroll-runs/query`, y su fila es
la corrida completa.

### De dónde saldría cada columna — 14 de 17 están listas

| Columna pedida | Origen | Estado |
|---|---|---|
| Código de Empleado | `employee_code` | ✅ |
| Nombre del Empleado | `employee_name` | ✅ |
| Días del periodo | `units` de la línea `SALARIO` | ✅ |
| **Días Trabajados / Días Pagados** | derivados de TNT + incapacidades | ⚠️ **[H-31](#h-31)** |
| Salario base | `base_amount` de `SALARIO` (mensual) | ✅ |
| Salario quincenal | `calculated_amount` de `SALARIO` | ✅ |
| Bonos | concepto `BONO` | ✅ |
| Comisiones | concepto `COMISION` | ✅ |
| Horas extras | concepto `HORAS_EXTRA` | ✅ |
| **Ingresos Adicionales** | `VIATICOS` + `OTRO_INGRESO` (no deducibles) | ✅ ver definición abajo |
| Ingreso total | Σ `line_class='Ingreso'` | ✅ |
| ISSS / AFP / Renta | conceptos `ISSS` / `AFP` / `RENTA` | ✅ |
| **Descuentos Externos** | `deduction_class='Externo'` | ⚠️ **[H-30](#h-30)** |
| **Descuentos Internos** | `deduction_class='Interno'` | ⚠️ **[H-30](#h-30)** |
| Total descuentos | Σ `line_class='Descuento'` | ✅ |
| Líquido a Pagar | Σ Ingreso − Σ Descuento | ✅ |

La buena noticia: **la clasificación de conceptos que el reporte necesita ya existe** en
`compensation_concept_type_catalog_items`, y coincide casi exactamente con las columnas pedidas:

```
Ingreso  ·  (sin clase)  →  SALARIO_BASE, BONO, COMISION, HORAS_EXTRA, VIATICOS, AGUINALDO, OTRO_INGRESO
Egreso   ·  Ley          →  ISSS, AFP, RENTA
Egreso   ·  Externo      →  COOPERATIVA, CUOTA_ALIMENTICIA, EMBARGO, PRESTAMO_BANCARIO, PROCURADURIA, OTRO_EXTERNO
Egreso   ·  Interno      →  ANTICIPO, DANO_EQUIPO, PRESTAMO_INTERNO
```

O sea: **no hay que modelar nada nuevo, hay que construir la superficie que lo agregue.**

### Decisiones tomadas (usuario, 2026-08-08)

**Días — se separan en dos columnas.** `Días del periodo` es lo que hoy existe (los días comerciales,
`15` en quincenal, incondicional). `Días Trabajados / Días Pagados` es un dato **nuevo que hay que
derivar**, y su insumo no llega a la corrida: ver [H-31](#h-31). Los dos casos que lo motivan:

1. **Permiso personal sin goce aprobado** — 1 día de permiso en la quincena ⇒ se pagan **14 días, no 15**.
2. **Incapacidad cubierta por la empresa** — 2 días de incapacidad a cargo del patrono ⇒ esos 2 días se
   pagan al **75 %**, no al 100 %.

Es decir que "días pagados" no es un entero: 15 días con 1 sin goce y 2 de incapacidad al 75 % dan
`12 + 2×0.75 = 13.5` días equivalentes. La columna debe poder expresar el decimal, o acompañarse de
`díasIncapacidad75` y `díasSinGoce` como columnas propias para que el número sea auditable.

**Ingresos Adicionales — se agrega como columna definida.** Contiene los ingresos **no deducibles** que
se reintegran al empleado y que están **fuera de su salario**: viáticos, reembolsos y cualquier otro
ingreso de esa naturaleza. Implicación de contrato, no cosmética: **estos conceptos no deben afectar
las bases de ISSS, AFP ni Renta.** El motor ya soporta el eje —
`AffectsIsss` / `AffectsAfp` / `AffectsRenta` por línea (`PayrollCalculation.Rules.cs:288`) — así que al
sembrar/configurar `VIATICOS` y `OTRO_INGRESO` hay que verificar que los tres vengan en `false`. Si
vinieran en `true` se estarían cotizando y gravando reembolsos, que es un error de retención.

> Pendiente menor de catálogo: no existe concepto `REEMBOLSO` (caería en `OTRO_INGRESO`), y `AGUINALDO`
> existe aparte — fiscalmente se trata distinto, así que probablemente merece columna propia y **no**
> entra en Ingresos Adicionales.

### Propuesta — **aceptada por el usuario (2026-08-08)**

Un solo query handler con dos superficies sobre el mismo resultado — el patrón que el módulo ya usa
en `query` + `export`:

```
POST /api/v1/companies/{companyPublicId}/payroll-runs/{payrollRunPublicId}/employees/query   → JSON paginado
GET  /api/v1/companies/{companyPublicId}/payroll-runs/{payrollRunPublicId}/employees/export  → xlsx
```

- Pivoteo **en el servidor**, no en el cliente: es un reporte fiscal y las dos superficies tienen que
  dar exactamente el mismo número.
- Fila de totales incluida en la respuesta JSON (no calculada en el front, por la misma razón).
- Buckets `otrosIngresos` / `otrosDescuentos` para cualquier concepto no previsto, de modo que
  `ingresoTotal − totalDescuentos = liquidoAPagar` **siempre** cuadre aunque entre un concepto nuevo.
- Respetar `is_included`: la línea excluida en revisión no se paga y no debe sumar.
- Multi-plaza consolida en una fila (así calcula el motor y así manda la ley); si se necesita el
  desglose por plaza para contabilidad, notar que **los descuentos de ley no se pueden repartir** —
  viven a nivel de empleado y no tienen plaza asignada.

> Mientras no exista, la corrida se puede auditar con la consulta que quedó en
> `scratchpad/reporte-planilla.sql` (59 filas + totales, verificada contra el motor al centavo). Es un
> apoyo de pruebas, **no** un sustituto del endpoint.

---

<a id="h-30"></a>
## H-30 · 🟠 La clase del descuento (Ley/Interno/Externo) no se persiste en la línea de planilla

Bloquea dos columnas de [H-29](#h-29) y tiene un problema propio, más serio, de periodo cerrado.

La clasificación existe en el catálogo:

```sql
select nature, default_deduction_class, string_agg(code, ', ')
from compensation_concept_type_catalog_items where is_active group by 1,2;
-- Egreso · Externo → COOPERATIVA, CUOTA_ALIMENTICIA, EMBARGO, OTRO_EXTERNO, PRESTAMO_BANCARIO, PROCURADURIA
-- Egreso · Interno → ANTICIPO, DANO_EQUIPO, PRESTAMO_INTERNO
-- Egreso · Ley     → AFP, ISSS, RENTA
```

Pero **no viaja a la línea de planilla**. Verificado:

- `payroll_run_lines` tiene una sola columna de clasificación, `line_class`, y sus valores son
  `Ingreso` / `Descuento` / `PagoPatronal` — el eje del motor, no el del reporte.
- `grep -rn "DeductionClass" src/CLARIHR.Application/Features/Payroll/ src/CLARIHR.Domain/Payroll/`
  → **cero resultados**. El concepto no existe en el motor.
- El DTO público de la línea (`GET …/employees/{personnelFilePublicId}`) tampoco lo expone: trae
  `conceptCode`, `conceptName`, `lineClass`, `sourceModule` — nada de clase de descuento.

Hoy se puede derivar con un join `payroll_run_lines.concept_code` →
`compensation_concept_type_catalog_items.default_deduction_class`, y el `concept_code` sí es el del
catálogo (`PayrollCalculationDataProvider.cs:360`, toma `deduction.ConceptTypeCode`). Pero ese join es
**a un catálogo mutable**, y ahí está el defecto real.

### Por qué importa más allá del reporte

**Un periodo cerrado se reclasificaría retroactivamente.** Si alguien edita
`default_deduction_class` de un concepto —o lo reclasifica de `Interno` a `Externo`— **todos los
reportes históricos cambian**, incluidos los de corridas `CERRADA`. Un reporte de un periodo pagado y
declarado no puede cambiar porque se editó un catálogo seis meses después.

**La asimetría delata que fue un olvido, no una decisión.** El mismo registro origen **sí** snapshotea
el nombre:

```
personnel_file_recurring_deductions:  concept_type_code · concept_name_snapshot
personnel_file_one_time_deductions:   concept_type_code · concept_name_snapshot
```

Se congeló el nombre para que el histórico no se corrompiera, y la clase —que es la que decide en qué
columna del reporte cae la plata— quedó viva. Si el nombre merecía snapshot, la clase también.

### Propuesta — **aceptada por el usuario (2026-08-08)**

Confirmado como requerimiento: **el descuento debe llevar su categoría (interno / externo) para poder
clasificarlo**, tanto en el reporte como en la línea.

- Agregar `deduction_class` a `payroll_run_lines`, poblada por el motor al generar, desde el catálogo
  vigente **en ese momento**. Es el mismo criterio de snapshot que ya se aplica al nombre.
- Exponerla como `deductionClass` en el DTO de la línea y usarla en H-29 para `Descuentos Externos` /
  `Descuentos Internos`.
- Valores: `Ley` · `Interno` · `Externo` — los tres que ya usa el catálogo. `Ley` es lo que alimenta las
  columnas ISSS/AFP/Renta, así que las cinco columnas de descuento del reporte quedan cubiertas por un
  solo eje, sin listas de códigos embebidas en la consulta.
- Considerar snapshotearla también en el registro origen (`personnel_file_*_deductions`), por
  coherencia con `concept_name_snapshot`.
- No hay dato productivo que migrar (no hay producción a esta fecha), así que la columna puede nacer
  `NOT NULL` sin ruta de compatibilidad.

---

<a id="h-31"></a>
## H-31 · 🟠 Los días de incapacidad y de tiempo no trabajado no llegan a la planilla: solo el monto

Bloquea la columna `Días Trabajados / Días Pagados` de [H-29](#h-29).

El motor de incapacidades calcula un desglose de días **completo y auditado**:

```csharp
// IncapacityCalculation.Rules.cs:69-83
internal sealed record IncapacityCalculationResult(
    int CalendarDays, int ComputableDays,
    int SubsidizedDays,      // los que subsidia el ISSS
    int DiscountDays,        // SIN_PAGO — se descuenta el día completo
    int EmployerDays,        // los que paga la empresa al % del tramo
    …
    IReadOnlyList<IncapacityTrancheDetail> TrancheDetails,  // día×día: rango, %, pagador
    …);
```

Y el motor de tiempo no trabajado calcula `DiscountedDays` + `DiscountPercent`
(`NotWorkedTime.Rules.cs:71-83`), incluyendo el caso de horas (una llegada tarde vale un cuarto de día,
no un día).

**Nada de eso cruza la frontera hacia la planilla.** El contrato que la corrida recibe lleva solo dinero:

```csharp
// IPayrollCalculationDataProvider.cs:51-58
public sealed record PayrollRegistroRow(
    Guid RecordPublicId, Guid PersonnelFilePublicId,
    string ConceptCode, string ConceptName,
    decimal Amount, decimal EmployerAmount, bool IsCarryover);
    //     ↑ sin días, sin porcentaje, sin pagador
```

Y lo llamativo: **el hueco para los días ya existe y se deja vacío a propósito**. `PayrollDeductionItem`
declara `Units`:

```csharp
// PayrollCalculation.Rules.cs:113-122
public sealed record PayrollDeductionItem(
    string ConceptCode, string ConceptName, decimal Amount,
    string SourceModule, Guid? SourceReferencePublicId,
    bool IsDeferrable = false, int DeferralOrder = 0, bool IsCarryover = false,
    decimal? Units = null);          // ← existe
```

pero los dos mapeos de registro lo **omiten**, así que queda `null`:

```csharp
// PayrollRuns.Handlers.cs:228-234  (tiempo no trabajado)
Bucket(…).Add(new PayrollDeductionItem(
    row.ConceptCode, row.ConceptName, row.Amount,
    PayrollSourceModules.NotWorkedTime, row.RecordPublicId,
    IsCarryover: row.IsCarryover));          // sin Units

// PayrollRuns.Handlers.cs:269-274  (incapacidad)
Bucket(…).Add(new PayrollDeductionItem(
    row.ConceptCode, row.ConceptName, row.Amount,
    PayrollSourceModules.Incapacity, row.RecordPublicId));   // sin Units
```

Contrasta con los descuentos de pool, que **sí** pasan `Units` (`PayrollCalculation.Rules.cs:320`).

### Consecuencia

1. **No se puede calcular "días pagados" desde la corrida.** Habría que consultar los módulos origen
   (incapacidades, TNT) por el rango del periodo y recomponer. Para una corrida **`CERRADA`** eso es un
   defecto de la misma familia que [H-30](#h-30): si el registro origen se edita después del cierre, el
   reporte de un periodo ya pagado cambia.
2. **Se pierde la distinción de pagador.** En la línea de incapacidad solo queda un monto; no se
   distingue qué parte fue `EMPRESA` al 75 %, qué parte subsidió el `ISSS` y qué parte quedó `SIN_PAGO`.
   Esa distinción es precisamente la que el reporte necesita para justificar por qué a un empleado se le
   pagaron 13.5 días y no 15.

### Propuesta

- Agregar los días al contrato: `PayrollRegistroRow` y `PayrollDeductionItem` ya tienen dónde
  (`Units`) — falta llevar `DiscountedDays` / `EmployerDays` / `SubsidizedDays` y el porcentaje efectivo.
- Persistir en `payroll_run_lines` los días y el pagador de la línea de incapacidad, para que el reporte
  se arme **solo desde la corrida** y sea inmutable tras el cierre.
- Derivar `díasPagados` en el servidor, no en el front, por la misma razón que el resto de H-29.

---

<a id="h-32"></a>
## H-32 · ❌ NO APLICA — el tope patronal es configuración de la empresa

**Descartado por el usuario (2026-08-08):** el tope de días de incapacidad a cargo del patrono, los
tramos por riesgo y el porcentaje de cada uno son **información de la empresa** — ella es dueña de esos
parámetros y de la política que aplique. El sistema los expone como configuración por tenant
(`company_preferences.employer_covered_incapacity_days_per_year` y `incapacity_risk_parameters`, ambos
con `tenant_id`), lo cual es el comportamiento correcto. **No hay defecto que arreglar.**

Se conserva únicamente como **referencia** para quien configure esos parámetros en un tenant nuevo.

### Lo que el sistema tiene hoy

```sql
select employer_covered_incapacity_days_per_year from company_preferences;  -- 9
```

Es una **preferencia de empresa configurable**, no una constante legal. Y los tramos sembrados por
riesgo son:

| Riesgo | Días | % | Pagador |
|---|---|---|---|
| `ENFERMEDAD_COMUN` | 1–3 | 75 % | **EMPRESA** |
| `ENFERMEDAD_COMUN` | 4+ | 75 % | ISSS |
| `ACCIDENTE_COMUN` | 1–3 | 75 % | **EMPRESA** |
| `ACCIDENTE_COMUN` | 4+ | 75 % | ISSS |
| `ACCIDENTE_TRABAJO` | 1+ | 100 % | ISSS |
| `ENFERMEDAD_PROFESIONAL` | 1+ | 100 % | ISSS |
| `MATERNIDAD` | 1–112 | 100 % | ISSS |

### Referencia legal, para configurar con criterio

**El 75 % es correcto** — Art. 307 del Código de Trabajo: el patrono paga el 75 % del salario básico
mientras dure la enfermedad. Los topes anuales que fija el Art. 307 son **por antigüedad**, y conviene
tenerlos a la vista al decidir el valor de la preferencia:

| Antigüedad | Días anuales al 75 % |
|---|---|
| 1 año o más | **60** |
| 5 meses a menos de 1 año | **40** |
| 1 a menos de 5 meses | **20** |

El `9` de esta empresa se explica como política interna (3 días × 3 episodios al año). Vale anotar que
ese número **no proviene del Art. 307** — es decisión de la empresa, que es exactamente el punto por el
que este hallazgo se descartó.

El reparto de los primeros 3 días es **jurídicamente discutido** para un trabajador inscrito en
el ISSS: el Art. 24 del Reglamento para la Aplicación del Régimen del Seguro Social hace que el ISSS
reconozca subsidio **a partir del cuarto día**, y hay criterio legal que sostiene que el patrono no
queda obligado por los primeros tres precisamente porque el ISSS cubre la prestación. El MTPS, en
cambio, ha comunicado públicamente que los primeros 3 días los paga el empleador. La práctica dominante
—y lo que el sistema implementa— es que los paga la empresa.

### Comportamiento al agotarse el tope — a tener presente al configurarlo

Cuando el tope se agota, el día **no baja de porcentaje: se descuenta completo.** El motor lo
reclasifica a `SIN_PAGO`, emite el warning `INCAPACITY_WARNING_CAP_EXHAUSTED` y descuenta el día íntegro:

> *"a SIN_PAGO day means NOBODY subsidizes it, so the payroll discounts the employee the FULL day value"*
> — `IncapacityCalculation.Rules.cs`, decisión D-21

Es la consecuencia aritmética correcta: agotado el tope patronal, y sin subsidio del ISSS en los
primeros 3 días, ese día no tiene pagador. Quien defina el tope debe saber que ese es el efecto —
con `9`, el cuarto episodio de enfermedad común del año deja los días 1–3 sin ingreso.

---

<a id="h-13"></a>
## H-13 · ✅ Huecos del playbook — corregidos durante la corrida

Todos aplicados en `playbook-pruebas-api.md`:

| Sección | Problema | Corrección |
|---|---|---|
| §4.1 | La tabla de categorías omitía `classificationPublicId`, que es **obligatorio** (`Guid` no anulable + `NotEmpty`). Siguiéndola al pie de la letra el `POST` da `400` | columna `Clasificación` + cuerpo JSON de ejemplo |
| §4.1 | Solo había conjunto de datos para 2 de los 4 catálogos de la sección; faltaban los dos ejes y las clasificaciones | 3 conjuntos nuevos (5 tipos de función, 3 de contrato, 5 clasificaciones) |
| §4.1 | El bloque de endpoints estaba en orden de listado: `position-categories` antes de `position-category-classifications` | reordenado en 5 escalones de dependencia |
| §4.1 | La advertencia de clasificaciones mencionaba 1 de los **3** ejes obligatorios | los tres, con su código de error |
| §4.1 | La tabla de pirámide ocupacional decía `sortOrder`; el campo real es **`levelOrder`** | corregido + nota de unicidad y límites |
| §4.2 | Decía "ocho colecciones hijas" y listaba **nueve** | corregido a nueve, con tabla endpoint→controlador |
| §6.6 | Listaba `GET,POST /companies/{c}/economic-aid-requests`, que **no existe**: todo cuelga de `personnel-files/{publicId}` (sección 7) | reemplazado por `preferences` + nota del traslado |
| Todo el doc | No se sabía qué controlador implementa cada endpoint | índice maestro paso→controlador + una línea `**Controlador:**` bajo cada uno de los 28 bloques (130 endpoints, 0 sin resolver, 59 controladores verificados como archivos existentes) |

**Pendientes de corregir en el playbook** (encontrados en §4.4, aún no aplicados):

| Sección | Problema |
|---|---|
| §3.3 | El conjunto de datos de centros de trabajo no menciona coordenadas, y el cuerpo de ejemplo trae `geoLat: null, geoLong: null`. Pero `ESTACION_AEROPUERTO` y `HANGAR` se definen en §3.2 con `requiresGeo: true`, así que **`SAL-EST` y `SAL-HGR` no se pueden crear como está escrito** (`400 WORK_CENTER_GEO_REQUIRED`). Hay que agregar geo a esas dos filas |
| §4.4 | El script de verificación cuenta plazas raíz sobre el listado, que no expone la dependencia ([H-16](#h-16)) → falso negativo garantizado. Reescribirlo contra `/graph` |
| §4.4 | El conjunto de 33 plazas no dice qué `status` inicial usar; el campo es obligatorio (`PositionSlotStatus`: `Vacant`/`Occupied`/`Suspended`). Se cargó todo como `Vacant` con `occupiedEmployees: 0` |

---

## Anexo · Qué se cargó en la corrida

| Recurso | Cantidad |
|---|---|
Unidades organizativas | 27 (árbol de 4 niveles, 1 raíz)
Centros de costo | 14
Catálogos de estructura | 8 tipos de unidad + 11 áreas funcionales
Pirámide ocupacional | 7 niveles
Catálogos de puesto (position-description) | 51 ítems en 10 slugs
Catálogos de puesto (job-catalogs) | 50 ítems en 9 categorías
Clasificaciones / categorías | 5 / 5
Perfiles de puesto | 33, con los 3 catálogos opcionales de la raíz
Colecciones hijas del perfil | 9 de 9 en los 33 · 857 filas
Matriz de competencias | escala `ESC-1-4` + 32 conductas + 132 ítems
Tabulador salarial | 29 líneas activas, 100 % en uso por las 33 compensaciones
Centros de trabajo | 5 sedes (`SAL-EST`, `SAL-HGR`, `SAL-CRG`, `SS-CORP`, `SS-CAP`) en 3 distritos
Plazas | 33, **60 posiciones ocupables**, árbol de 5 niveles con 1 raíz (`PL-DG-001`)
**Total de llamadas** | **~1100, 0 fallos** (fuera de H-02)

Usuarios creados para probar separación de funciones:
`solicitante.tabulador@clarihr.test` y `aprobador.tabulador@clarihr.test`, ambos Admin de Empresa.
