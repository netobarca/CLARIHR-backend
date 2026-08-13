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
| [H-02](#h-02) | ~~`POST /company/users` siempre devuelve `500`~~ — **✅ RESUELTO 2026-08-09** | Bug | ✅ Corregido |
| [H-03](#h-03) | ~~Con `Email:Provider=Logging` no se puede activar ningún usuario invitado~~ — **cerrado: fuera de alcance** | Hueco de ambiente | ⬜ Cerrado |
| [H-04](#h-04) | ~~`switch` de empresa exige propiedad, el contrato dice membresía~~ — **✅ RESUELTO 2026-08-09** | Contrato ≠ implementación | ✅ Corregido |
| [H-05](#h-05) | ~~El tabulador no valida el salario mínimo legal~~ — **descartado: la empresa es dueña de su configuración** | Premisa incorrecta | ❌ No aplica |
| [H-06](#h-06) | ~~Regla no documentada de la matriz de competencias~~ — **✅ RESUELTO 2026-08-09** | Contrato incompleto | ✅ Corregido |
| [H-07](#h-07) | ~~`requirements` acepta cualquier catálogo sin validar la categoría~~ — **✅ RESUELTO 2026-08-09** | Validación ausente | ✅ Corregido |
| [H-08](#h-08) | ~~`salary-tabulator/lines` no expone el código de la clase salarial~~ — **✅ RESUELTO 2026-08-09** | Contrato incompleto | ✅ Corregido |
| [H-09](#h-09) | ~~`version` del perfil cuenta escrituras, no revisiones~~ — **✅ RESUELTO 2026-08-09** | Semántica engañosa | ✅ Corregido |
| [H-10](#h-10) | ~~El manifiesto de catálogos apunta al catálogo equivocado~~ — **✅ RESUELTO 2026-08-09** | Bug de contrato | ✅ Corregido |
| [H-11](#h-11) | ~~Convenciones inconsistentes entre catálogos de la misma sección~~ — **✅ RESUELTO 2026-08-09** | Deuda de contrato | ✅ Corregido |
| [H-12](#h-12) | ~~`inactivate` de tipo de centro de trabajo no valida uso~~ — **retirado: el guard ya existía** | Hallazgo incorrecto | ❌ No aplica |
| [H-14](#h-14) | ~~La plaza acepta cualquier salario, dentro o fuera de su banda~~ — **✅ RESUELTO 2026-08-09** | Regla de negocio ausente | ✅ Corregido |
| [H-15](#h-15) | ~~No hay forma de eliminar ni desactivar una plaza~~ — **✅ RESUELTO 2026-08-10** | Ciclo de vida incompleto | ✅ Corregido |
| [H-16](#h-16) | ~~El listado de plazas no expone la dependencia jerárquica~~ — **✅ RESUELTO 2026-08-10** | Contrato incompleto | ✅ Corregido |
| [H-17](#h-17) | ~~`requiresGeo` acepta la coordenada (0,0)~~ — **✅ RESUELTO 2026-08-10** | Validación superficial | ✅ Corregido |
| [H-18](#h-18) | ~~La jornada sembrada viola el invariante que la API exige~~ (**falso positivo**) **+ error mal atribuido — ✅ RESUELTO 2026-08-10** | Bug real + hallazgo incorrecto | ✅ Corregido / ❌ (a) no aplica |
| [H-19](#h-19) | ~~Falta la capa que deriva horas extra desde la jornada, y tres de sus insumos~~ — **✅ FASE 1 RESUELTA 2026-08-10**; fase 2 espera el marcador | Capa por construir | ✅ Fase 1 / ⏸️ Fase 2 |
| [H-20](#h-20) | ~~El tramo horario de la hora extra no se valida contra nada: doble pago silencioso~~ — **✅ RESUELTO 2026-08-10** | Bug | ✅ Corregido |
| [H-21](#h-21) | ~~Los catálogos de conceptos devuelven `[]` sin `countryCode`: falsa alarma de "no sembrado"~~ — **✅ RESUELTO 2026-08-11** | Bug | ✅ Corregido |
| [H-22](#h-22) | ~~`document_type_catalog_items` está vacío y **no tiene endpoint**~~ — **el endpoint SÍ existía (Backoffice); el catálogo vacío ✅ RESUELTO 2026-08-11** | Semilla faltante + hallazgo parcialmente incorrecto | ✅ Corregido / ❌ (endpoint) no aplica |
| [H-23](#h-23) | ~~`occupiedEmployees` de la plaza es un contador que nadie mantiene ni lee~~ — **✅ RESUELTO 2026-08-11**; el tablero SÍ lo leía | Dato desincronizable + indicador afectado | ✅ Corregido |
| [H-24](#h-24) | ~~`finalize` significa "retiro definitivo"~~ — **retirado, era un error mío** | — | ❌ Inválido |
| [H-25](#h-25) | ~~`finalize` es un paso obligatorio del alta que no se descubre hasta que falla~~ — **✅ RESUELTO 2026-08-11**; un código para cuatro cosas | Contrato no evidente | ✅ Corregido |
| [H-26](#h-26) | ~~Una fecha sin zona (`"2026-08-01"`) produce `500` en la asignación~~ — **✅ RESUELTO 2026-08-11**; eran DOS vías de entrada y la causa no era la escritura | Bug | ✅ Corregido |
| [H-27](#h-27) | ~~Cuentas bancarias: duplicados exactos y N primarias simultáneas~~ — **✅ RESUELTO 2026-08-12**; incluye las identificaciones, que tenían el mismo hueco | Validación ausente | ✅ Corregido |
| [H-28](#h-28) | ~~La antigüedad para vacaciones se toma de la plaza, no del ingreso~~ — **✅ RESUELTO 2026-08-12**; eran DOS resoluciones del ancla y el mismo defecto vivía en el finiquito | Regla de negocio incorrecta | ✅ Corregido |
| [H-29](#h-29) | ~~No existe el reporte de planilla por empleado~~ — **✅ RESUELTO 2026-08-12**; eran TRES bloqueos, no dos: el tercero era un error de retención | Superficie faltante | ✅ Corregido |
| [H-30](#h-30) | ~~La clase del descuento no se persiste en la línea~~ — **✅ RESUELTO 2026-08-12** con H-29; el join en lectura tenía además un segundo defecto de multi-país | Modelo incompleto | ✅ Alta |
| [H-31](#h-31) | ~~Los días de incapacidad y de tiempo no trabajado no llegan a la planilla~~ — **✅ RESUELTO 2026-08-12** con H-29 | Contrato incompleto | ✅ Corregido |
| [H-32](#h-32) | ~~El tope patronal de incapacidad de 9 días no es legal~~ — **retirado: es configuración de la empresa** | Referencia legal | ❌ No aplica |
| [H-33](#h-33) | ~~**Transversal:** cuatro 🔴/🟠 tenían cobertura que nunca probó la condición que importaba~~ — **✅ CERRADO 2026-08-12**; el mecanismo 3 seguía vivo (tres instancias nuevas esta sesión) y resultó parcialmente automatizable | Práctica de pruebas | ✅ Corregido |
| [H-34](#h-34) | ~~El `parentConcurrencyToken` que devuelve el DELETE de un hijo **nunca cambia**~~ — **✅ RESUELTO 2026-08-11**; cambiaba en 29 de 53, que es peor | Contrato ambiguo | ✅ Corregido |
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
## H-02 · ✅ RESUELTO — `POST /api/v1/company/users` siempre devuelve `500`

> **✅ Corregido el 2026-08-09.** El fix es una palabra; lo que importa es por qué pasó y por qué nadie lo vio.
>
> **La trampa:** `PublicContractNaming` tiene **dos** funciones de renombrado y para la misma entrada dan
> resultados distintos —`GetExternalIdentifierName("userId")` → `userPublicId` (JSON) vs
> `GetExternalRouteIdentifierName("userId")` → `publicId` (rutas, la que aplica la convención). El
> `Location` se construyó con la de JSON. Compila, pasa todo test unitario de handler, y da 500.
>
> **Arreglado:** la clave a `publicId`, y el parámetro de ruta renombrado `userId` → `publicId` en los 6
> endpoints del controller — cero cambio de contrato (la convención ya lo reescribía) pero deja de depender
> de un renombrado implícito.
>
> ### Barrido: 1 de 91, no una epidemia
>
> Recorrí los 91 `ToCreatedAtActionResult*` de los 82 controllers. **Un primer chequeo dio 88 desalineados y
> estaba mal**: ASP.NET rellena los valores de ruta faltantes con los *ambient values* del request, que es
> por qué las rutas anidadas solo pasan la clave del hijo. Con esa regla: **1**. `CompanyUsers` es el único
> POST cuya ruta no tiene ningún parámetro, así que no hay ambient value que rescate la clave faltante.
>
> ### Guardrail nuevo
>
> `tests/CLARIHR.Application.UnitTests/CreatedAtActionRouteKeyGovernanceTests.cs` valida los 91 call sites
> llamando **`PublicContractNaming.GetExternalRouteIdentifierName` directo** —no reimplementa la regla, así
> que no puede desincronizarse de la convención que protege— y descontando los ambient values. Se escribió
> **antes** del fix y reportó exactamente 1 fallo con el diagnóstico completo; un guardrail que nace verde no
> prueba nada.
>
> ### La lección reutilizable: el test que lo tapaba
>
> La **única** cobertura de ese POST era `CompanyUsers_Invite_ShouldRateLimit`: 11 llamadas, corta al recibir
> `429` y **solo asserta el 429**. Las 10 previas devolvían `500` y el loop las ignoraba. El endpoint se
> ejercitaba 11 veces por corrida sin verificarse nunca.
>
> Los **9** loops de rate limit del repo tenían la misma ceguera. Ahora los 9 assertan que ninguna respuesta
> previa al `429` sea **5xx** — no "que sea exitosa", porque algunos mandan input inválido a propósito (un
> `If-Match` obsoleto, p. ej.) y ahí un `4xx` es legítimo; un `5xx` nunca lo es.
>
> **Y algo que el barrido destapó:** `[EnableRateLimiting]` corre como middleware **antes** del authorization
> del endpoint, así que esos loops tampoco probaban autorización. Al escribir el happy-path real apareció que
> el rol del actor sembrado **no puede invitar** —el seeder le da `email` y `firstName` como no editables y el
> Create exige permisos de campo sobre ambos—, un `403` legítimo que nadie había visto. El test happy-path se
> autentica con el otro usuario del escenario.
>
> **Test de regresión:** `CompanyUsers_Invite_ShouldReturn201WithResolvableLocation` — asserta `201`, `ETag`,
> y **sigue el header `Location` con un `GET`**. Un assert de `201` a secas no habría cazado este bug, porque
> el problema no era el status sino la URL.
>
> **Verificado:** build limpio · **2867/2867** unitarios · **24/24** integración de company users y los 9
> loops de rate limit.
>
> **Sin impacto de contrato para el frontend:** la ruta pública ya era `/api/v1/company/users/{publicId}`. Lo
> que cambia es que el `POST` pasa de `500` a `201` con `Location` — si el FE tenía un workaround para el
> `500`, conviene retirarlo.

---

### El hallazgo original

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
## H-03 · ⬜ CERRADO, fuera de alcance — con `Email:Provider=Logging` no se puede activar ningún usuario invitado

> **Cerrado por decisión del usuario (2026-08-09). No se implementa nada.**
>
> **No es un defecto de producto: es un hueco de ambiente.** Con el frontend integrado y un proveedor real
> configurado, el flujo funciona completo — el invitado recibe el correo, hace clic y se activa. El código de
> Brevo **ya está completo**; solo falta configuración manual (API key, remitente verificado y las 3 URLs del
> frontend, todo en `brevo-email-setup.md`). En los logs de julio quedó la evidencia de que estaba cableado y
> solo faltaba la llave: `Brevo rejected the message with 401 Unauthorized: Key not found`.
>
> **Decisión de alcance:** las pruebas de correo se harán **en el servidor con el frontend integrado**. Para
> trabajo local basta con scripts — la receta de abajo (`auth/register` + un `UPDATE` de una columna) es
> suficiente y no requiere construir nada.
>
> **La severidad original 🟠 estaba inflada:** se documentó como bloqueo cuando en realidad describe un
> ambiente sin proveedor configurado.
>
> ### Dos correcciones al hallazgo original, que sí conviene conservar
>
> **1 · Los tests de integración NO están bloqueados para los correos de auth.** El hallazgo dice que las
> tres vías quedan cerradas; eso vale para el trabajo manual, no para los tests. Existe
> `tests/CLARIHR.Api.IntegrationTests/CapturingAuthEmailService.cs`, que reemplaza `IAuthEmailService` en el
> host de pruebas (`IntegrationTestWebApplicationFactory.cs:60-61`) y expone
> `LatestVerificationTokenFor(email)`. El flujo registro → verificar ya se prueba end-to-end con el token
> real de un solo uso.
>
> **2 · El único hueco genuino que quedaría es la invitación de empresa en tests.** Va por
> `IPendingEmailDispatcher`, y el host de pruebas **no** lo reemplaza — solo reemplaza `IAuthEmailService`.
> Si algún día hace falta probar en CI un flujo que exija dos usuarios reales creados por invitación
> (separación de funciones, anti-self), lo que se necesita es **un doble espejo del de auth** —
> `CapturingPendingEmailDispatcher` con `LatestInvitationTokenFor(email)`— y no un transporte nuevo. Queda
> anotado como pista, no como pendiente: hoy esos casos se cubren sembrando el segundo usuario en
> `IntegrationTestSeeder` (`TargetUserId`).

---

### El hallazgo original

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
## H-04 · ✅ RESUELTO — `switch` de empresa exige propiedad, el contrato dice membresía

> **✅ Corregido el 2026-08-09.** El contrato tenía razón: **la membresía es la regla**, y el handler era
> más restrictivo de lo previsto.
>
> **Modelo ratificado por el usuario:** un usuario puede ser invitado a varias empresas, pero solo **una**
> está activa a la vez; para entrar a otra **debe hacer `switch`**, y no puede operar en dos sin él. El
> handler ya implementaba esa mecánica bien —mueve la primaria y emite un token acotado al tenant nuevo—;
> lo que estaba mal era la puerta. Servía solo a dueños y rechazaba justo el caso que existe para servir.
>
> **La pista que lo delataba:** el chequeo de membresía de la línea 930 era **inalcanzable** para un
> no-propietario, y sería código muerto si la propiedad fuera la regla intencional (un dueño siempre tiene
> membresía). Que alguien lo escribiera prueba que la intención era membresía; la resolución por propiedad
> se coló por reusar el único helper disponible, `ResolveOwnedCompanyAsync`, cuyos **otros 6 llamadores** sí
> administran la empresa y siguen igual.
>
> **Arreglado:** `AccountCompanyActorResolver` gana `ResolveCompanyAsync` (carga sin exigir propiedad) y
> `switch` lo usa. `ResolveOwnedCompanyAsync` no cambió de comportamiento — ahora se apoya en el hermano.
>
> ### Un segundo defecto en el mismo camino
>
> Con el fix, el chequeo de membresía pasó a ser la puerta **alcanzable** — y devolvía
> `ACTIVE_COMPANY_SWITCH_FORBIDDEN` con `ErrorType.Conflict`, o sea **409**, cuando el Swagger promete `403`
> y semánticamente "no sos miembro" es autorización. Separados:
>
> | Situación | Antes | Ahora |
> |---|---|---|
> | Miembro no propietario | `403 COMPANY_OWNERSHIP_FORBIDDEN` | **`200`** |
> | Sin membresía | `409 ACTIVE_COMPANY_SWITCH_FORBIDDEN` | **`403 COMPANY_MEMBERSHIP_FORBIDDEN`** (nuevo) |
> | Empresa inactiva | `409 ACTIVE_COMPANY_SWITCH_FORBIDDEN` | igual — ahí sí es conflicto de estado |
>
> **Cambio de contrato para el frontend**, neto una mejora. De paso se corrigió una traducción a medias que
> estaba en el resx español: *"No tienes permiso para administrar **this company**"*.
>
> ### Por qué pasó verde: la cobertura nunca probó el caso real
>
> `AccountCompanies_Switch_ShouldReturnTokenWithSelectedTenant` **ya** hacía switch a una segunda empresa con
> membresía no primaria y esperaba éxito. Pasaba porque el seeder crea **las dos** empresas con el mismo
> dueño (`IntegrationTestSeeder.cs:33-34`, `actorUserId` en ambas), así que el camino de no-propietario nunca
> se ejercitaba.
>
> **Tests nuevos**, los dos corridos en rojo antes del fix:
> `AccountCompanies_Switch_AsNonOwnerMember_ShouldSucceed` (monta una empresa creada por otro usuario) y
> **`AccountCompanies_Switch_WithoutMembership_ShouldReturn403`** — el guardrail del lado opuesto, que no es
> opcional: sin él, relajar la propiedad podría degenerar en "cualquiera entra a cualquier empresa", un
> agujero de aislamiento multi-tenant peor que el bug original.
>
> **Verificado:** build limpio · **2867/2867** unitarios · **62/62** integración de cuentas de empresa,
> acceso efectivo, autenticación de plataforma y guardrails de contrato público.
>
> > **Trampa al correr la integración:** fijar `CLARIHR_INTEGRATION_TEST_CONNECTION_STRING` con un nombre de
> > base único hace que las **tres** factories (`IntegrationTest`, `CoreJwt`, `BackofficeJwt`) compartan la
> > misma base, y la primera en hacer dispose la borra debajo de las otras — 5 fallos en 12 ms que parecen
> > del código y no lo son. Sin la variable, cada factory crea su base efímera
> > (`IntegrationTestConnectionStrings.cs:23`), que es el modo correcto cuando el Postgres de Docker está
> > arriba.

---

### El hallazgo original

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
redactado, o el handler es más restrictivo de lo previsto.

**Es un defecto de backend, no un cambio de frontend** — el FE no tiene forma de sortearlo: no existe otro
endpoint para cambiar de empresa y la API rechaza a un miembro legítimo. Quien lo *sufre* es el frontend;
quien lo arregla es el backend. (La redacción original de esta línea decía "Impacto en el frontend" y se
prestaba a leerse al revés.)

**Consecuencia:** un usuario invitado a una empresa no puede "entrar" a ella por esta vía.

Mitigante: el token del `login` ya trae el claim `tid` cuando la membresía es primaria, así que el
`switch` no fue necesario en la corrida. Pero un usuario con varias empresas dependería de él.

---

<a id="h-05"></a>
## H-05 · ❌ NO APLICA — el tabulador no valida el salario mínimo legal

> **Descartado por el usuario (2026-08-09). La premisa del hallazgo era incorrecta.**
>
> El hallazgo ofrecía dos salidas —advertir o rechazar— y la respuesta fue una tercera: **no le corresponde
> al sistema**. *"El tabulador lo configura cada empresa; ellos tienen la autoridad y la garantía de
> configurar sus datos de la manera que crean conveniente para cada plaza."* Mismo criterio que
> [H-32](#h-32) para el tope patronal de incapacidad.
>
> **El control del tabulador es la aprobación, y ya funciona:** anti-self verificado en vivo
> (`allowSelfApproval = false`, `SalaryTabulatorAdministration.cs:1319`).
>
> ### Un dato que confirma que el hallazgo estaba mal encaminado
>
> **No existe fuente del mínimo legal por encima del empleado individual.** Solo está en
> `personnel_file_employee_profiles.minimum_monthly_wage`, tecleado por expediente; no hay nivel país,
> tenant ni sector, y `company_legal_profiles.economic_activity_description` es **texto libre**. Y el mínimo
> salvadoreño es **por sector** —`$408.80` comercio/industria · `$402.26` maquila · `$272.72`
> agropecuario— así que el número único que el hallazgo daba por obvio habría falseado la alarma en dos de
> los tres.
>
> **Arquitectura que queda explícita:** el mínimo legal vincula **donde se paga** (planilla y liquidación,
> por empleado, que es donde ya vive y se usa). El tabulador y la plaza son **configuración de la empresa**.
>
> Lo que sí resultó real fue [H-14](#h-14) —que la banda aprobada no gobernaba la plaza— y se arregló.

---

### El hallazgo original

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
## H-06 · ✅ RESUELTO — regla no documentada de la matriz de competencias

> **✅ Corregido el 2026-08-09.**
>
> **Código propio para la causa real.** El `409` genérico `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT` cubría
> varias causas con un mensaje que no distinguía ninguna. Ahora las conductas que no comparten terna devuelven
> `409 JOB_PROFILE_COMPETENCY_MATRIX_CONDUCT_TRIPLE_MISMATCH`, con un mensaje que dice qué hacer: *"Separe las
> conductas en un ítem por terna."* **Mismo status a propósito** — el delta para el frontend es un código más,
> no un status nuevo que manejar.
>
> **El DTO dice la verdad.** `conductPublicIds` pasó de `IReadOnlyCollection<Guid>?` a no-nullable, y se
> quitaron las **dos** coalescencias `?? []` del controlador que disimulaban la diferencia. El comportamiento
> no cambia —vacío o nulo ya se rechazaba— pero el OpenAPI dejaba de prometer un campo opcional que en runtime
> siempre fallaba.
>
> **Documentado en el Swagger de `POST` y `PUT`:** que un ítem **es** una celda de la terna, que la terna se
> **deriva** de las conductas y no viaja en el cuerpo, la consecuencia (todas las conductas comparten terna),
> y los límites numéricos: **50 conductas por ítem, 200 ítems por perfil**.
>
> ### Una parte del hallazgo estaba desactualizada
>
> Decía *"nada en el Swagger lo dice"*. Falso a la fecha de la corrección: la descripción del `POST` ya
> mencionaba que *"conducts of differing competency/type/behavior-level"* devuelven `409`. Lo que faltaba de
> verdad era el **nombre del código** —imposible de manejar en el cliente si el mensaje no lo distingue— y los
> **límites numéricos**, que solo aparecían como *"the per-profile item cap"* sin decir cuánto.
>
> **Verificado:** build limpio · **2871/2871** unitarios · **16/16** integración del framework de competencias.
> El test `CompetencyFramework_MatrixUpdate_ItemWithMismatchedConducts_ShouldReturn409` se corrió **en rojo
> primero** con el código nuevo, y al pasar dejó de poder aprobarse por la razón equivocada — que es justo el
> riesgo que se había anotado al planear H-01.

---

### El hallazgo original

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
## H-07 · ✅ RESUELTO — `requirements` acepta cualquier catálogo sin validar la categoría

> **✅ Corregido el 2026-08-09.**
>
> **La regla que se fijó.** `catalogItemPublicId` resuelve contra los `job-catalogs`, y solo dos tipos de
> requisito tienen categoría equivalente:
>
> | `requirementType` | `catalogItemPublicId` |
> | --- | --- |
> | `Education` | debe ser `EducationLevel` |
> | `Knowledge` | debe ser `KnowledgeArea` |
> | `Certification`, `Experience`, `Other` | **no se acepta** — el contenido va en `description` |
>
> Dos códigos nuevos, ambos **422**: `JOB_PROFILE_REQUIREMENT_CATALOG_CATEGORY_MISMATCH` (categoría distinta a
> la que exige el tipo) y `JOB_PROFILE_REQUIREMENT_CATALOG_ITEM_NOT_APPLICABLE` (tipo sin categoría equivalente
> y llegó un ítem). Sin ítem no se valida nada: el campo sigue siendo opcional para los cinco tipos.
>
> Aplicado en los **tres** puntos de escritura de `ResolveCatalogItemInternalIdAsync` — `POST` (add), `PUT`
> (update) y `PATCH`. El del `PATCH` lee `patchState.RequirementType`, no el tipo persistido: un patch que
> cambia el tipo **y** el ítem a la vez se valida contra el tipo resultante, no contra el que se está dejando.
>
> ### El manifiesto ofrecía tres listas que el campo rechaza
>
> Hallazgo que apareció al arreglar esto. `JobProfileCatalogBindingMap` tenía **cinco** bindings sobre
> `requirement.catalogItemPublicId`: los dos correctos (`EducationLevel`, `KnowledgeArea`) más
> `RequirementsEducation`, `RequirementsKnowledge` y `RequirementsCertification` — catálogos **internos**, que
> alimentan `description`, no `catalogItemPublicId`. Un frontend que leyera el manifiesto literal habría
> propuesto al usuario tres listas cuyos ítems el campo ahora rechaza con `422`. Los tres se movieron al campo
> `description`, que es el que de verdad consumen.
>
> ### Comportamiento que sí existía y ahora queda documentado
>
> Para ciertos `requirementType` el `description` **auto-resuelve o crea** el valor en los catálogos internos
> (`ResolveDescriptionInternalCatalogUsageAsync` + `InternalCatalogRegistry`): o sea que
> `job-profile.requirements.{education,knowledge,certification}` se pueblan solos, no hay que sembrarlos. Eso
> es exactamente por qué los tres tipos sin categoría pueden rechazar el ítem sin perder capacidad: su
> contenido ya tenía otro canal.
>
> ### Verificación
>
> Cuatro tests de integración escritos **antes** del fix, con el reparto esperado por la regla de
> [H-33](#h-33): **2 en rojo** (los dos que exigen `422`) y **2 en verde** (el par correcto
> `Education`+`EducationLevel` y el caso sin ítem, que no debía romperse). Tras el fix, 4/4. Regresión:
> `JobProfile` en integración **76/76**, unitarios **2871/2871**.
>
> Trampa del camino: los cuatro tests arrancaron dando `403`. `CreateJobProfileAdminContext` no incluye
> `JobCatalogs.Admin`, que es lo que hace falta para crear el ítem de catálogo del escenario; se migraron a
> `CreateCompetencyFrameworkAdminContext`.

### Hallazgo original

`JobProfileRequirementCommandSupport.ResolveCatalogItemInternalIdAsync` busca el ítem **solo por
publicId**, sin comparar su `JobCatalogCategory` contra el `requirementType` del requisito.

Consecuencia: se puede colgar un ítem de `EducationLevel` en un requisito de tipo `Certification`, o
un `Training` en uno de tipo `Knowledge`, y el `POST` responde `201`. El descriptor de puesto queda
internamente incoherente sin que nada proteste.

---

<a id="h-08"></a>
## H-08 · ✅ RESUELTO — `salary-tabulator/lines` no expone el código de la clase salarial

> **✅ Corregido el 2026-08-09.** El hallazgo lo clasificó como conveniencia para el frontend. Al leer el
> código la causa era otra, y peor.
>
> ### La línea guarda un código, no una FK
>
> `SalaryTabulatorLine` tiene `SalaryClassCode` + `NormalizedSalaryClassCode` y **ningún `SalaryClassId`**. El
> índice único es `(TenantId, NormalizedSalaryClassCode, NormalizedSalaryScaleCode, EffectiveFromUtc)`: **el
> código ES la clave del dominio.** El `salaryClassPublicId` que devolvía la API no estaba guardado — se
> derivaba en cada lectura con una subconsulta correlacionada que exigía `item.IsActive`.
>
> Y como el código guardado nunca se exponía, esa derivación era **la única** forma de saber a qué clase
> pertenecía una línea. Dos operaciones permitidas la rompían:
>
> | Operación | Efecto |
> | --- | --- |
> | Inactivar la clase (`/isActive=false`) | el join exige `IsActive` → `salaryClassPublicId` = `null` en **todas** sus líneas → **inatribuibles** |
> | Renombrar el código (`/code`) | el join es por código → deja de coincidir → mismo resultado |
>
> No era solo visualización: `GET .../lines?salaryClassPublicId=X` resuelve X al código **actual** y filtra
> por él, así que tras un renombre el filtro devolvía **cero líneas** de una clase con varias; el export hacía
> lo mismo (Excel vacío), y cuando traía filas llevaba un GUID crudo en el único documento que lee un contador.
>
> ### Lo que se construyó
>
> **`salaryClassCode` + `salaryClassName` en cinco DTOs** —no cuatro como decía el plan: el `impact` del change
> request tenía el mismo problema y se habría quedado como el único endpoint con la identificación rota—.
> `salaryClassCode` sale de la línea, siempre presente; `salaryClassName` del catálogo. **`salaryClassPublicId`
> conserva su semántica exacta** (`null` si la clase está inactiva): cambiarla habría sido tocar un campo
> existente. En el export las dos columnas van antes de `salaryScaleCode`.
>
> **Un solo mecanismo.** Había dos: subconsulta correlacionada con filtro `IsActive` para las líneas y
> diccionario en memoria para los items. Quedó el diccionario, **sin** el filtro `IsActive` —para que una clase
> inactiva siga resolviendo su nombre— con `IsActive` como campo del entry para preservar el `publicId`. Las
> subconsultas correlacionadas, que eran la parte frágil, desaparecieron.
>
> **El código no se renombra con líneas.** `409 POSITION_DESCRIPTION_CATALOG_CODE_IN_USE` (código propio: el
> mensaje del genérico habla de inactivación, que es falso para un renombre). Bloquean **todas** las líneas,
> activas o cerradas — una línea cerrada también tiene que seguir diciendo a qué clase perteneció. `/name`,
> `/description` y `/sortOrder` siguen libres: la etiqueta no es la clave.
>
> **La clase no se inactiva con líneas activas.** `409 POSITION_DESCRIPTION_CATALOG_IN_USE`. Solo las líneas
> **en vigencia** bloquean: si contara cualquier línea, ninguna clase sería retirable nunca.
>
> ### El guard de inactivación ya existía y preguntaba lo que no importaba
>
> `IsCatalogItemInUseAsync` mandaba `SalaryClass` al branch por defecto,
> `HasJobProfilesUsingCatalogItemAsync`, que solo mira `StrategicObjectiveCatalogItemId`,
> `AssignedWorkEquipmentCatalogItemId` y `ResponsibilityCatalogItemId` — ninguno es la clase salarial. Para
> `SalaryClass` respondía **siempre `false`**. No faltaba el guard: estaba y contestaba otra pregunta.
>
> ### Dos trampas
>
> **El guard tenía que colgarse del código MOVIÉNDOSE, no de `HasScalarMutation`.** El handler llama
> `entity.Update(patchState.Code, patchState.Name, …)` con todo el set escalar en cualquier mutación, y el
> patch state arranca sembrado con los valores actuales: **un patch que solo toca `/name` reescribe el mismo
> código**. Colgado de `HasScalarMutation`, el guard habría prohibido editar la etiqueta de cualquier clase con
> líneas. `SalaryClass_ChangeNameOnly_WithTabulatorLines_ShouldSucceed` fija que no.
>
> **El doble de test.** `TestPositionDescriptionCatalogRepository` devuelve `Task.FromResult(false)` en los diez
> probes de uso. Para el método nuevo eso habría dejado pasar cualquier test futuro del renombre sin probar
> nada — el patrón exacto que ya convirtió un guardrail en decoración en
> [este repo](auth-anonymous-endpoints-audit-tenant-bug) dos veces. Se implementó como
> `throw new NotImplementedException()`.
>
> ### Verificación
>
> Los 9 tests escritos antes del fix: **6 rojos, 3 verdes**. El plan había previsto 5 rojos — clasificó mal
> `..._WhenClassInactive` como no-regresión cuando también afirma los campos nuevos. Los rojos fallaron por la
> razón correcta (`KeyNotFoundException` en la propiedad ausente; `OK` donde debía haber `Conflict`). Después
> del fix, 9/9. Regresión: `SalaryTabulator|PositionDescriptionCatalog|PositionSlot` **54/54**, unitarios
> **2871/2871**. `openapi.yaml` parcheado a mano en las 4 schemas y revalidado con el parser.

### Hallazgo original

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
## H-09 · ✅ RESUELTO — `version` del perfil cuenta escrituras, no revisiones

> **✅ Corregido el 2026-08-09.** De las dos salidas que planteaba el hallazgo —renombrar el campo o versionar
> de verdad— se eligió la segunda: *"versionemos por estado"*.
>
> **`version` = cuántas veces se aprobó el descriptor.** `0` = borrador nunca publicado; `2` = publicado dos
> veces.
>
> | Operación | Antes | Ahora |
> | --- | --- | --- |
> | `Publish()` | `Version++` | `Version++` ← **la única** |
> | `Reopen()` | `Version++` | no mueve — se trabaja *hacia* la revisión siguiente |
> | `Archive()` | `Version++` | no mueve — no cambia contenido |
> | `Create` | `Version = 1` | `Version = 0` |
> | `UpdateCore` (PUT del núcleo) | `Version++` | no mueve |
> | 16 altas/bajas de colecciones + sus ediciones | `Version++` | no mueve |
> | matriz de competencias | `Version++` | no mueve |
>
> ### Tres cosas que aparecieron al hacerlo
>
> **El `Version++` y el `RefreshConcurrencyToken()` del PUT vivían en el MISMO `if (bumpVersion)`.** Borrar el
> bloque —el movimiento natural— habría dejado el PUT del perfil sin rotar el token, matando la concurrencia
> optimista del núcleo **con la suite entera en verde**. El test
> `JobProfiles_Update_ShouldRotateConcurrencyToken_WithoutMovingVersion` existe solo para eso, y verifica
> además que el token viejo pase a dar `409`.
>
> **`BumpDescriptorVersion()` era redundante dentro del agregado pero NO fuera.** Los 16 mutadores de
> colección ya abren con `EnsureEditable()`, así que ahí la llamada no aportaba nada más que el bump. Pero la
> capa de aplicación lo invocaba **directo en 20 sitios** para los `PUT`/`PATCH`/`DELETE` en sitio de una fila
> hija, donde el hijo se muta sin pasar por `Add*`/`Remove*` y **no hay ningún otro chequeo de estado**. Ahí
> era el único guard. Mi lectura inicial dijo "borrarlo"; lo encontró el compilador. Sobrevive como
> `EnsureDescriptorEditable()`, guard puro.
>
> `BumpVersion()` (matriz) siguió el mismo camino → `EnsureMatrixWritable()`, con el invariante **débil**
> (solo not-archived), que es lo que mantiene la matriz escribible sobre un perfil publicado. Confundir los dos
> compila limpio y rompe la excepción de H-01; el guardián
> `CompetencyFramework_MatrixAdd_OnPublishedProfile_ShouldSucceed` vigila justo eso y se le actualizó el
> comentario.
>
> **Los nombres ahora dicen la verdad.** `BumpX()` describía el efecto secundario que se fue; lo que quedó
> —y siempre fue la mitad que sostenía el peso— es el guard.
>
> ### Efecto colateral aceptado
>
> El `Version++` era **lo único que ensuciaba la fila del perfil** en una escritura de colección hija, y eso
> disparaba `MarkModified` (`ApplicationDbContext:566`). Sin él, **el `modifiedAtUtc` del perfil ya no se mueve
> al editar un requisito o una función**. Decidido a propósito: la fila del perfil no cambió, así que su fecha
> no miente, y cada hijo trae su propio `modifiedAtUtc`. Las alternativas (rotar el token del padre, o marcar
> la fila desde infraestructura) se descartaron: la primera produce `409` sorpresa y rompe la concurrencia
> granular que el módulo eligió a propósito.
>
> ### Verificación
>
> 9 tests escritos antes del fix, **9 rojos** (el noveno falla en la mitad de la versión y pasa en la del
> token, que es la que debe seguir pasando). Los valores exactos son deliberados: un `after > before` habría
> nacido verde sin probar nada, porque el número también se movía antes. Después del fix 9/9.
>
> **Cuatro aserciones preexistentes dadas vuelta, no borradas.** `ApiIntegrationTests.cs:8814` afirmaba que el
> `version` subía tras un alta en la matriz; dos unitarios afirmaban `Version == 2` y token intacto; y
> `JobProfiles_Reopen_ThenEdit_ThenRepublish_ShouldSucceed` afirmaba que la reapertura subía el número — esa
> última la escribió H-01 y **solo apareció en la corrida de regresión**, no en la lista que el plan había
> previsto. Cada una quedó afirmando lo contrario con el porqué al lado.
>
> Unitarios **2871/2871** · integración `JobProfile|CompetencyFramework|PositionSlot` **123/123**.
>
> **Trampa del proceso, no del código:** lancé dos corridas de integración en paralelo y se pisaron la base
> (cada una hace `ResetDatabaseAsync()`); la segunda murió sin escribir resultado y por un momento pareció que
> no había evidencia. Misma familia que la trampa de `CLARIHR_INTEGRATION_TEST_CONNECTION_STRING`: **una sola
> corrida de integración a la vez**.
>
> Los 33 perfiles del ambiente quedan con su `version` inflado; se corrige al borrarlos y recrearlos, que ya
> estaba decidido en [H-01](#h-01).

### Hallazgo original

Tras cargar las 9 colecciones hijas, los `version` de los 33 perfiles quedaron entre **30 y 39**,
correlacionados con la cantidad de filas hijas (`P-DG` llegó a 39 por sus muchos
`dependent-positions`) — no con revisiones del documento.

Si el frontend rotula ese campo como "versión del descriptor", muestra un número que no significa
lo que el usuario va a entender. Conviene o renombrarlo en el contrato, o versionar el descriptor de
verdad en las transiciones de estado (lo que se conecta con H-01).

---

<a id="h-10"></a>
## H-10 · ✅ RESUELTO — el manifiesto de catálogos apunta al catálogo equivocado

> **✅ Corregido el 2026-08-09.** No era "confirmar si es deliberado": el binding **no podía funcionar nunca**.
>
> ### Con los datos reales de Avianca
>
> El manifiesto decía que `jobProfile.positionCategoryPublicId` se llena de
> `/position-description-catalogs/position-function-types/items`, que devuelve cinco **ejes de función**:
> `ADMINISTRATIVA` · `COMERCIAL` · `DIRECTIVA` · `OPERATIVA` · `TECNICA`.
>
> El campo acepta un publicId de `position_categories`, servido en **otro** endpoint
> (`/companies/{id}/position-categories`), que en Avianca son: `ADMINISTRATIVO` · `COMERCIAL` ·
> `OPERATIVO_AEREO` · `OPERATIVO_TIERRA` · `TECNICO_AERONAUTICO`.
>
> | Categoría (lo que el campo acepta) | Clasificación | Eje de función |
> | --- | --- | --- |
> | `ADMINISTRATIVO` | CLAS-ADMIN | ADMINISTRATIVA |
> | `COMERCIAL` | CLAS-COMERCIAL | COMERCIAL |
> | `OPERATIVO_AEREO` (tripulaciones) | CLAS-AEREO | **OPERATIVA** |
> | `OPERATIVO_TIERRA` | CLAS-TIERRA | **OPERATIVA** |
> | `TECNICO_AERONAUTICO` | CLAS-TECNICO | TECNICA |
>
> `OPERATIVA` se abre en **dos** categorías y `DIRECTIVA` no tiene ninguna: la relación es 1→N. Y como los
> publicId viven en tablas distintas, uno de `PositionFunctionType` **jamás** coincide con uno de
> `position_categories`. **No guardaba un valor equivocado: ninguna opción de la lista ofrecida podía funcionar.**
> Verificado: el test end-to-end contra el código sin arreglar responde `NotFound`.
>
> ### La causa no era un typo
>
> El manifiesto solo sabía hablar de tres familias, y `position-categories` no es un catálogo de items: es un
> recurso con su propio CRUD cuyas filas cuelgan de una *clasificación* que combina tres ejes. **Era un hueco
> en el vocabulario tapado con el vecino más cercano** — el primer eje de la clasificación.
>
> ### Lo que se construyó
>
> Cuarta familia **`PositionStructure`**, con `PositionCategory` y `PositionCategoryClassification`. Su
> `apiEndpointTemplate` es la colección tenant-scoped directa (`/companies/{companyId}/{slug}`), no una ruta
> `/items`. `jobProfile.positionCategoryPublicId` pasa a `position-categories`, y `subResources` gana
> `positionCategory` con el campo `classificationPublicId` — **al final del arreglo**, así que ningún índice
> existente se movió.
>
> Los dos tipos los siembra el servicio idempotente de arranque, no una migración: **un ambiente levantado
> necesita reiniciar la API**.
>
> ### El test que hace irrepetible la clase de defecto
>
> Dos de los cuatro no comparan strings: **siguen la URL que el manifiesto publica y meten el id que devuelve en
> el campo que el manifiesto dice que alimenta.** Un string se puede mantener sincronizado por casualidad; esto
> no pasa si el binding no es realmente correcto. 4/4 rojos antes (el end-to-end con `NotFound`), 4/4 después.
> Unitarios **2871/2871**, con el guardrail `CanonicalTypes_ShouldBeTheExactExpectedSet` extendido a 30 entradas.
>
> ### Segunda vuelta: la cadena cerrada de punta a punta
>
> El primer paso dejó los tres ejes de la clasificación fuera, y al revisarlo el argumento se dio vuelta:
> publicar solo `classificationPublicId` dejaba al frontend **eligiendo** clasificaciones pero sin poder
> **crearlas**, porque los tres campos con que se arma son obligatorios. **Cubrir un formulario a medias es peor
> que no cubrirlo**: el cliente igual necesita el caso especial y encima puede no notar que le falta un campo.
>
> Costo real, medido en vez de supuesto: **un solo tipo canónico nuevo**, no tres —`PositionFunctionType` y
> `PositionContractType` ya existían—, más la quinta familia `OrgStructure` para `OrgUnitType`
> (`/organization-structure-catalogs/unit-types`). Y el acoplamiento con ese módulo es **conceptual, no de
> compilación**: el binding map son strings, así que no hay referencia de proyecto ni tipo importado.
>
> Recorrido completo: `unit-types` / `position-function-types` / `position-contract-types` →
> `position-category-classifications` → `position-categories` → `jobProfile.positionCategoryPublicId`.
> `functional-areas` queda afuera sin dilema: ningún campo de la cadena la referencia.
>
> **El riesgo que sí introduce, y cómo queda cubierto.** Los slugs de `OrgStructure` son literales en los
> atributos del controller, sin route map al que anclarlos —`PositionDescription` sí lo tiene—, así que un
> renombre de ruta haría mentir al manifiesto otra vez. Lo impiden los tests end-to-end: 8 tests de manifiesto,
> 4 rojos por vuelta antes del fix (el mensaje literal *"The manifest publishes no
> 'positionCategoryClassification' sub-resource"*), 8/8 después.

### Hallazgo original

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

> **Nota (2026-08-09):** [H-07](#h-07) corrigió **otros** bindings del mismo archivo —los tres catálogos
> internos que colgaban de `requirement.catalogItemPublicId`— y este se cerró aparte, el mismo día, con la
> resolución de arriba.

---

<a id="h-11"></a>
## H-11 · ✅ RESUELTO — convenciones inconsistentes entre catálogos de la misma sección

> **✅ Corregido el 2026-08-09.** De los cuatro ejes de inconsistencia se cerraron los que eran **hueco
> funcional**, se armonizó lo que no podía romper nada, y se dejó explícitamente fuera lo único que habría roto
> el contrato del frontend sin habilitar nada.
>
> ### Lo que se cerró
>
> **`job-catalogs` gana `sortOrder` y `description`.** No era cosmética: `Competency` tiene 12 ítems y la tabla
> **no tenía ninguna de las dos columnas**, así que un selector de competencias solo podía ir alfabético —no se
> podía poner `LIDERAZGO` primero en un perfil directivo— y un diccionario de competencias no tenía dónde decir
> qué significa cada término. **El listado ahora ordena por `sortOrder`**; antes ordenaba por nombre, algo que
> ningún cliente podía influir.
>
> **`name` armonizado a 150.** Cubrí más de lo que decía el hallazgo: además de la pirámide y `job-catalogs`, la
> **escala de calificación y sus niveles** también estaban en 120. Dejarlos afuera habría recreado la
> inconsistencia que se estaba cerrando. Siete columnas ensanchadas; el `Up` de la migración **solo ensancha y
> agrega**, lo destructivo vive únicamente en el `Down`.
>
> ### Dónde el hallazgo se equivocaba
>
> **El `levelOrder` único de la pirámide no es una inconsistencia a borrar: es un invariante.** Una pirámide
> ocupacional es un ranking estricto — dos niveles en la misma posición no significa nada. Queda documentado como
> deliberado.
>
> Lo que sí dolía era **reordenar**, y eso se resolvió con tres endpoints de reordenamiento en lote
> (`PATCH .../order`) con un mismo cuerpo: la lista completa de ids en el orden deseado, y **el servidor asigna
> 10, 20, 30…**. No viajan números en el request, así que un cliente **no puede construir** una colisión con el
> rango único. Sin `If-Match`, decidido y documentado: no hay agregado único que sostenga un token y el request
> trae el orden completo, así que last-writer-wins es la semántica honesta de un guardado de arrastrar-y-soltar.
>
> ### La trampa, demostrada de dos formas
>
> Con el índice único `uq_occupational_pyramid_levels__tenant_level_order` **no diferible**, Postgres lo verifica
> por sentencia, así que escribir los rangos finales de una pasada rompe en el primer choque — un intercambio
> simple es el caso mínimo. Quité la fase 1 del handler y el test del intercambio dio `InternalServerError`; como
> un 500 no dice la causa, lo demostré directo en SQL:
>
> ```
> UPDATE occupational_pyramid_levels SET level_order = 20 WHERE code = 'DIRECTIVO';
> UPDATE occupational_pyramid_levels SET level_order = 10 WHERE code = 'GERENCIAL';
> → duplicate key value violates unique constraint "uq_occupational_pyramid_levels__tenant_level_order"
>   DETAIL: Key (tenant_id, level_order)=(9031b0db-…, 20) already exists.
> ```
>
> De ahí las **dos fases**: parquear el conjunto en una banda estrictamente por encima del máximo actual **y**
> del rango más alto por asignar, y después escribir los finales. Los otros dos recursos usan una sola fase.
>
> ### Dos errores míos que vale registrar
>
> **Rompí la regla de [H-33](#h-33) en la etapa B+C**: implementé antes de ver los tests rojos. Al revisar si
> *habrían* fallado encontré que **uno no probaba nada**: el test de orden usaba nombres "Primero"/"Segundo" y el
> orden viejo era **por nombre**, así que habría pasado igual sin el fix. Lo cambié a `Zeta`/`Alfa` —donde el
> alfabético contradice al deseado— y **verifiqué que muerde** revirtiendo el `OrderBy`.
>
> **Y el cambio de límite destapó un test que llevaba tiempo sin probar nada:** `Validate_NameTooLong_Fails`
> fijaba `121` literal; con el techo en 150 dejó de ser "demasiado largo". Ahora deriva de
> `OccupationalPyramidLevel.MaxNameLength + 1`.
>
> ### Un guardrail del repo me atrapó, y estuvo bien
>
> `BackendMessageLocalizationTests` exige que todo código de error viva en **ambos** resx: los tres nuevos
> faltaban. Y delató algo peor — mi validador tenía un `.WithMessage("Unsupported catalog type…")` que acuñaba
> una **segunda forma sin traducir** de algo que ya tenía código catalogado y localizado
> (`POSITION_DESCRIPTION_CATALOG_INVALID_TYPE`). Ese chequeo se movió al handler.
>
> Los dobles de test de los dos repositorios se implementaron como `NotImplementedException`, no como lista
> vacía: una lista vacía habría hecho pasar cualquier test futuro del reordenamiento sin probar nada.
>
> ### Verificación
>
> 10 tests de integración nuevos (4 de B+C, 6 de reordenamiento) — los 6 del reordenamiento **rojos antes**, y el
> del intercambio verificado quitando la fase 1. Regresión
> `JobCatalog|PositionDescriptionCatalog|OccupationalPyramid|CompetencyFramework|JobProfile|Reorder`
> **124/124**; unitarios **2871/2871**.
>
> **Queda fuera a propósito:** renombrar `levelOrder` → `sortOrder` y unificar los sets de verbos. Rompe el
> contrato del frontend, toca cinco familias de endpoints y no habilita ninguna funcionalidad.

### Hallazgo original

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
## H-12 · ❌ RETIRADO — el guard ya existía; el hallazgo estaba mal

> **❌ Retirado el 2026-08-09. No había nada que arreglar, y no escribí un guard duplicado.**
>
> El hallazgo afirmaba que `InactivateWorkCenterTypeCommandHandler` iba de la verificación de concurrencia
> directo a inactivar. **Es falso**: la línea inmediatamente siguiente al chequeo de concurrencia es
>
> ```csharp
> var dependencyResult = await dependencyPolicy.CanInactivateWorkCenterTypeAsync(
>     workCenterType.PublicId, cancellationToken);
> if (dependencyResult.IsFailure) { return Result<...>.Failure(dependencyResult.Error); }
> ```
>
> y `LocationDependencyPolicy.CanInactivateWorkCenterTypeAsync` devuelve
> `WORK_CENTER_TYPE_IN_USE` (`409`) cuando `HasActiveWorkCentersAsync` es verdadero. O sea que `WorkCenterType`
> **ya cumplía** el criterio de sus tres hermanos, y el que no lo cumplía era `SalaryClass` — cerrado en
> [H-08](#h-08).
>
> ### Verificado, no leído
>
> Leer el código no alcanza (es la lección de [H-33](#h-33)), así que comprobé que la cobertura **muerde**:
> existen `WorkCenterTypes_Inactivate_WhenTypeIsInUse_ShouldReturn409` (integración, crea el tipo, crea un
> centro que lo usa, intenta inactivar) y `LocationDependencyPolicy_WhenTypeIsInUse_ShouldReturnConflict`
> (unitario). Desactivé el chequeo de uso **dentro de la política** y los dos se pusieron rojos —
> `Expected: Conflict / Actual: OK`—; después restauré, y `git status` quedó vacío, o sea byte-idéntico al
> original. Verde otra vez: 6/6 `WorkCenterType` en integración, 2871/2871 unitarios.
>
> Un intento previo de desactivar el guard **en el handler** no compiló (`CS9113: parámetro sin leer`, con
> warnings como errores) y encima un unitario construye el handler pasándole la política — dos señales más de
> que está cableado de verdad. Ojo con esa trampa: la primera corrida con el build roto usó el DLL viejo y dio
> un verde que no significaba nada.
>
> ### Por qué el hallazgo salió mal
>
> Se anotó desde el playbook §3.4.2 sin releer el handler. El criterio que sí quedó decidido acá —*"bloquear con
> `*_IN_USE`"*— se aplicó donde de verdad faltaba, y esa parte sigue en pie.

### Hallazgo original

Ya estaba anotado en el playbook §3.4.2 y se confirma leyendo el handler:
`InactivateWorkCenterTypeCommandHandler` va de la verificación de concurrencia directo a inactivar,
**sin comprobar si hay centros de trabajo usando el tipo**. Sus dos hermanos (tipo de unidad
organizativa, área funcional, centro de costo) sí bloquean con `*_IN_USE`.

Puede ser deliberado, pero la asimetría conviene confirmarla.

---

<a id="h-14"></a>
## H-14 · ✅ RESUELTO — la plaza acepta cualquier salario, dentro o fuera de su banda

> **✅ Corregido el 2026-08-09.** Con [H-05](#h-05) descartado, este era el hallazgo real de los dos.
>
> **El diagnóstico afinado:** el problema no era el mínimo legal, era que **la aprobación de dos firmas del
> tabulador no gobernaba nada**. La banda que produce solo validaba el salario del *empleado*; la plaza —la
> fuente de verdad de cuánto paga el puesto— se configuraba fuera de su propia banda aprobada, así que un
> error de captura se propagaba a todos los ocupantes y la doble firma quedaba decorativa.
>
> **Arreglado:** `PositionSlotJobProfileLookup` lleva ahora la banda (`BandMinAmount`, `BandMaxAmount`,
> `BandCurrencyCode`), resuelta con un **LEFT JOIN** en el lookup que el handler de crear ya invocaba — cero
> llamadas extra. `EnsureConfiguredSalaryWithinBand` valida en `POST` y `PUT`, con cotas **inclusivas**.
>
> **No se reusó `GetSalaryRangeAsync`**: recibe el publicId de la *plaza*, que al crear todavía no existe.
>
> ### Un segundo defecto que apareció de paso
>
> **Nadie comparaba las monedas.** La banda tiene `CurrencyCode` y la plaza `ConfiguredBaseSalaryCurrencyCode`,
> y comparar montos entre monedas distintas no significa nada. Error propio:
> `POSITION_SLOT_CONFIGURED_SALARY_CURRENCY_MISMATCH` (422) — se rechaza en vez de comparar o de saltar la
> validación en silencio.
>
> ### La brecha, cerrada en el mismo paso
>
> El bypass era: una plaza creada antes de que su perfil tuviera compensación no se validaba nunca, y agregar
> la línea después no la revalidaba. **Decisión del usuario: si no hay parámetro contra el que comparar, no se
> puede configurar el salario.** Nuevo error `POSITION_SLOT_JOB_PROFILE_HAS_NO_SALARY_BAND` (422), con el
> mensaje diciendo qué hacer — configurar primero la compensación del perfil.
>
> **Y su contrapeso, que es lo que evita que se vuelva una dependencia dura:** la plaza **sí** se crea sin
> banda mientras no se le configure salario. Exigir banda para *existir* habría convertido al tabulador en
> requisito para armar el organigrama. Dos tests fijan las dos mitades:
> `WithSalaryWhenProfileHasNoBand_ShouldReturn422` y `WithoutSalaryWhenProfileHasNoBand_ShouldSucceed`.
>
> ### Y el cambio de banda: se reporta, no se bloquea
>
> Si una línea se aprueba con cotas nuevas, las plazas ya configuradas pueden quedar fuera.
> **Decisión del usuario: no bloquear, reportar.** Bloquear habría significado que el estado de las plazas
> vete una decisión de política salarial ya firmada por dos personas — y el caso normal es una empresa que
> sube su banda, que quedaría atascada por plazas viejas.
>
> La respuesta de `approve` trae ahora `outOfBandPositionSlots[]` con lo necesario para actuar sin una segunda
> consulta: plaza, perfil, salario configurado y las cotas que ya no respeta. Está **acotado a los pares
> (clase, escala) que la aprobación tocó**, así que no arrastra desajustes previos ajenos al cambio.
>
> **El reporte va también a la auditoría.** Si solo viviera en la respuesta HTTP se perdería en cuanto el
> aprobador cerrara la pantalla, y quedaría un dato irrecuperable — el mismo problema de H-23 y la lección de
> [H-33](#h-33).
>
> Dos tests, con su contrapeso: `WhenNewBandLeavesSlotsOutside_ShouldSucceedAndReportThem` y
> `WhenNewBandStillContainsSlots_ShouldReportNone` — sin el segundo, un reporte que devolviera siempre todas
> las plazas pasaría por bueno.
>
> **Verificado:** build limpio · **2870/2870** unitarios · **55/55** integración de plazas, tabulador,
> conceptos de compensación y asignaciones. Los 4 tests que exigen `422` se corrieron **en rojo antes del
> fix** (devolvían `201`); los 3 que esperan `201` pasaban desde el principio, lo que probó que el escenario
> sí armaba banda.
>
> **Sin cambio de contrato para el frontend** más allá de los dos códigos nuevos: `configuredBaseSalary`
> sigue siendo opcional y su forma no cambió.
>
> > Las 33 plazas del ambiente se cargaron sin control y varias quedan fuera de banda: fallarán al
> > **actualizarse**, no al existir. Consistente con la decisión de H-01 de borrarlas y recrearlas.

---

### El hallazgo original

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
## H-15 · ✅ RESUELTO — no hay forma de eliminar ni desactivar una plaza

> **✅ Corregido el 2026-08-10.**
>
> ### Dos correcciones al hallazgo
>
> **`is_active` sí se escribe.** Es **derivada** de `Status` en el dominio (`PositionSlot.cs:63,237`):
> `IsActive = Status != Suspended`. `PATCH /status` la escribe en cada llamada.
>
> **Y suspender no era decorativo.** `Suspended` ya bloqueaba asignar un empleado
> (`EmploymentAssignments.Rules.cs:134` → `POSITION_SLOT_NOT_ASSIGNABLE`) y actualizar la ocupación. Lo cierto
> del hallazgo: no había `DELETE`, y una plaza suspendida **seguía apareciendo en el listado**.
>
> ### El dato que definió el diseño
>
> **Ninguna tabla tiene FK hacia `position_slots` salvo sus autorreferencias.** Cuatro la referencian por
> `public_id` **sin FK**: `personnel_file_employment_assignments` (60 filas / 33 plazas en el ambiente),
> `personnel_file_contract_histories`, `personnel_file_authorization_substitutions` y
> `exit_interview_submissions`. O sea que **un `DELETE` crudo no falla: orfana el historial en silencio.** El
> guard es la funcionalidad; el verbo es la mitad fácil.
>
> Al revés, las cinco FK que salen de la plaza son todas `RESTRICT` y **32 de 33 plazas cuelgan de un padre**,
> así que borrar un padre sin guard daba un `500` crudo de Postgres.
>
> ### Lo construido
>
> `DELETE /position-slots/{id}` con `If-Match`, que responde `409 POSITION_SLOT_IN_USE` si la referencia
> **cualquiera** de esas cuatro tablas —activa **o histórica**, porque una fila histórica se orfana igual— o si
> es padre de otra plaza. Devuelve `200` con el snapshot final.
>
> `422 POSITION_SLOT_SUSPEND_WITH_OCCUPANTS` al suspender con asignaciones activas: antes se podía, y dejaba el
> agregado incoherente (`isActive=false` con ocupantes vivos), porque `EnsureStatusConsistency` solo normaliza
> `Vacant`/`Occupied`.
>
> Filtro `isActive` en el listado **y en el export**, aditivo y sin cambiar el default.
>
> ### Tres decisiones tomadas sobre la marcha
>
> **Los guards cuentan asignaciones reales, no `occupiedEmployees`.** Verifiqué que `UpdateOccupancy` solo lo
> invoca el endpoint `/occupancy`: **crear una asignación no toca el contador**. Un guard que confiara en él
> habría pasado justo en el caso que existe para bloquear. (Es la misma raíz que [H-23](#h-23).)
>
> **El `DELETE` devuelve `200` con cuerpo, no `204`.** Mi test asumía `204`; la convención del repo
> (`job-catalogs`, sub-recursos del perfil) es `200` con cuerpo. Corregí **el test**, no la implementación —
> inventar una tercera forma para el mismo verbo es lo que cerramos en [H-11](#h-11).
>
> **`isActive` se agregó también al export**, fuera del plan: el export ya replicaba todos los demás filtros del
> listado y dejarlo afuera los habría hecho divergir en cuanto alguien filtrara la pantalla y exportara.
>
> ### Sobre los dobles de test
>
> El doble rico se modeló con una propiedad `Usage` configurable —por defecto "nada la referencia", para no
> mover los tests existentes, pero un test puede declarar lo contrario—; los dos simples van con
> `NotSupportedException`. Un all-zero fijo habría dejado ambos guards intesteables a ese nivel.
>
> ### Verificación
>
> 7 tests: **6 rojos antes** (`DELETE` → `405`; suspender con gente → `200`; el filtro ignorado) y 1 verde de
> no-regresión. 7/7 después. Unitarios **2871/2871**; integración
> `PositionSlot|EmploymentAssignment|PersonnelFile` **88/88**.
>
> **No se agregó un cuarto estado `Archived` ni rutas `/activate` `/inactivate`**, decidido explícitamente:
> `Suspended` ya significa "no asignable" y ya apaga `isActive`, y un segundo eje de desactivación recrearía la
> inconsistencia cerrada en H-11.

### Hallazgo original

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
## H-16 · ✅ RESUELTO — el listado de plazas no expone la dependencia jerárquica

> **✅ Corregido el 2026-08-10.** Validado end-to-end contra la API real antes y después, a pedido explícito
> para descartar un falso positivo como el de [H-12](#h-12).
>
> ### La validación en vivo, antes del arreglo
>
> ```
> GET /companies/{c}/position-slots  →  33 plazas · 32 campos · campos de dependencia: NINGUNO
>
> script del playbook §4.4, tal cual está escrito:
>   plazas: 33                → esperado 33   OK
>   posiciones ocupables: 60  → esperado 60   OK
>   plazas raíz: 33           → esperado 1    REVISAR     ← falso
>
> /graph:   33 nodos · 32 aristas (todas Direct) · raíces reales: 1
> detalle:  PL-ABOG-001 → directDependencyPositionSlotCode = 'PL-DG-001'
> ```
>
> El árbol estaba perfecto —33 plazas, 32 con padre, 1 raíz `PL-DG-001`, profundidad 5
> (`PL-DG-001 > PL-VPOPS-001 > PL-GERAER-001 > PL-SUPRAMPA-001 > PL-AGRAMPA-001`)— y el listado era **la única
> superficie que no lo decía**. El daño real no es cosmético: un cliente no podía distinguir **"no tiene
> padre"** de **"el listado no me lo dice"**, y por eso una verificación acusó un problema inexistente.
>
> ### Lo que la validación corrigió del plan
>
> Mi plan suponía que habría que **agregar dos self-joins** y medir su costo. Falso: `BuildJoinedQuery` ya
> traía `directDependency` y `functionalDependency` como `LEFT JOIN` desde siempre —el export ya los consumía—
> y `SlotJoinedRow` ya los exponía. **La consulta ya estaba pagada; solo faltaban cuatro líneas en el `Select`.**
> Esa parte del plan simplemente no existía.
>
> ### Después del arreglo, mismo script, mismos datos
>
> ```
> GET /companies/{c}/position-slots  →  33 plazas · 36 campos
>   directDependencyPositionSlotPublicId · directDependencyPositionSlotCode
>   functionalDependencyPositionSlotPublicId · functionalDependencyPositionSlotCode
>
>   plazas raíz: 1  → esperado 1  OK
> ```
>
> Y el filtro nuevo `?directDependencyPositionSlotPublicId=` contrastado contra la verdad del grafo: **9 hijos
> directos de `PL-DG-001`**, iguales a sus 9 aristas directas, y coincidente con el nivel 2 medido en SQL.
>
> ### Detalles de la decisión
>
> Los cuatro campos usan **los mismos nombres que el detalle**: un tercer vocabulario para la misma relación es
> la divergencia que cerró [H-11](#h-11). La dependencia **funcional** se publica aunque tenga **0 filas** en los
> datos reales, por simetría con el detalle y el export — publicar la mitad es el problema, no la solución.
>
> **No se agregó `rootsOnly`**, decidido explícitamente: con el campo expuesto, contar raíces en cliente es
> exactamente lo que hace el script del playbook, y funciona.
>
> ### Verificación
>
> 4 tests, **4 rojos antes** (`KeyNotFoundException` en el campo ausente; el filtro ignorado devolvía padre e
> hija) y 6/6 después contando los de no-regresión. Unitarios **2871/2871**. Uno de los tests **reproduce el
> falso negativo del playbook**: cuenta raíces sobre el listado y las contrasta con `nodos − aristas directas`
> del grafo, así que si el campo vuelve a desaparecer el test lo dice.
>
> El script del playbook §4.4 quedó anotado con el porqué del falso negativo, no reescrito: desde que el listado
> publica el campo, funciona tal cual estaba.

### Hallazgo original

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
## H-17 · ✅ RESUELTO — `requiresGeo` acepta la coordenada (0,0)

> **✅ Corregido el 2026-08-10.**
>
> ### Una corrección al hallazgo: la validación de rango ya existía
>
> El hallazgo recomienda *"validar rango (`lat ∈ [-90,90]`, `long ∈ [-180,180]`) y, si se quiere ser estricto,
> rechazar `(0,0)`"*. **La primera mitad ya estaba hecha** y es compartida por los tres caminos de escritura
> (`WorkCenterRules.ValidateAssignmentAsync`), devolviendo `WORK_CENTER_INVALID_COORDINATES`. Lo que faltaba era
> solo el `(0,0)`.
>
> ### Y un hueco que el hallazgo no menciona, peor que el que sí menciona
>
> La regla de "las dos coordenadas" **solo corría cuando el tipo exigía geo**:
>
> ```csharp
> if (workCenterType.RequiresGeo && (!geoLat.HasValue || !geoLong.HasValue))
> ```
>
> Con `requiresGeo = false` no había ninguna: se podía guardar **latitud sin longitud**. `(0,0)` al menos es un
> punto; media coordenada no es una ubicación en absoluto. Confirmado en rojo: los dos casos devolvían `201`.
>
> ### Lo construido
>
> - `422 WORK_CENTER_COORDINATES_INCOMPLETE` — las dos o ninguna, **sin importar `requiresGeo`**.
> - `422 WORK_CENTER_COORDINATES_PLACEHOLDER` — el par exacto `(0,0)`, verificado **después** del rango para que
>   un par fuera de rango siga reportando el error de rango.
>
> ### El dato real
>
> `SAL-EST` —estación de aeropuerto, tipo con `requires_geo = true`— estaba en `0.000000, 0.000000`. Con el
> guard activo esa fila quedaba bloqueada para cualquier edición, así que se corrigió con las coordenadas
> **confirmadas por el usuario**: `13.4445, -89.0558`. Auditoría posterior de los cinco centros: ninguno con isla
> nula ni con media coordenada.
>
> ### Por qué sigue siendo 🟢
>
> **Nadie consume las coordenadas hoy**: no alimentan geocerca, marcación ni asistencia — se guardan y se
> devuelven. El costo de no arreglarlo era diferido: el día que algo las lea, `SAL-EST` habría sido un dato
> **confiadamente incorrecto**, que es peor que uno ausente.
>
> ### Verificación
>
> 3 rojos antes (los dos de media coordenada y el de la isla nula, todos devolviendo `201`) y 3 verdes de
> no-regresión; 6/6 después. Unitarios **2871/2871**.

### Hallazgo original

Un tipo de centro de trabajo con `requiresGeo: true` rechaza la creación sin coordenadas
(`400 WORK_CENTER_GEO_REQUIRED`, reproducido al crear `SAL-HGR` sin geo). Pero la validación es de
**presencia**, no de validez: `SAL-EST` quedó guardado con `geo_lat = 0.000000, geo_long = 0.000000`
—la "isla nula" en el Golfo de Guinea— y pasó sin objeción.

Conviene validar rango (`lat ∈ [-90,90]`, `long ∈ [-180,180]`) y, si se quiere ser estricto, rechazar
el par exacto `(0,0)`, que en la práctica siempre es un placeholder.

---

<a id="h-18"></a>
## H-18 · ✅ (b) RESUELTO · ❌ (a) FALSO POSITIVO

> **2026-08-10.** El hallazgo traía dos partes. **(a) es falsa**: la semilla nunca estuvo sin días. **(b) es
> real** y quedó corregida.
>
> ### (a) ❌ La jornada sembrada SIEMPRE tuvo sus 6 días
>
> Tres pruebas, en orden de fuerza:
>
> 1. **El sembrador trae el template completo.** `WorkScheduleTemplateSeeder` define L-V 08:00-17:00 con comida
>    12:00-13:00 + sábado 08:00-12:00, y pasa `totalWeeklyHours: null` para derivar las 44 h de esos días.
>    Sin cambios desde el **2026-07-16**, anterior a la corrida.
> 2. **El dominio rechaza cero días.** `ReplaceDaysCore` lanza si `days.Count == 0`, del mismo commit. Ninguna
>    ruta —sembrador o `POST`— puede crear una jornada sin días.
> 3. **La auditoría lo dice.** El `WORK_SCHEDULE_UPDATED` del `PUT` del 8-ago registra **`días en before: 6`**.
>
> Y el aprovisionamiento calza: la empresa se creó a las `19:11:34.179` y la jornada a las `19:11:34.943`,
> 0.76 s después, en la misma transacción — fue el sembrador, y funcionó.
>
> **Cómo casi me equivoco yo también.** Primero inferí por timestamps que los días habían llegado dos días
> tarde, porque su `created_utc` es del 8-ago. Esa inferencia es inválida: el `PUT` **reemplaza** los días
> —`ReplaceDaysCore` borra e inserta— así que ese timestamp aparece igual con la semilla perfecta. La auditoría
> es lo único que distingue los dos casos. Queda como recordatorio de que un timestamp de fila hija no prueba
> cuándo existió la relación.
>
> ### (b) ✅ El error señalaba el campo equivocado
>
> Un `scheduleClass: "NOCTURNA"` o un `attendanceDateAnchor: "MEDIO"` salían de sus setters como
> `ArgumentException` y caían en el `catch` amplio alrededor de `WorkSchedule.Create`, devolviendo
> `422 WORK_SCHEDULE_DAY_INVALID` — *"the days are not valid"*. Los días estaban bien. Confirmado en rojo:
> `Expected: BadRequest / Actual: UnprocessableEntity`.
>
> **Se corrigió en el validador, no con códigos 422 nuevos** (opción B de las tres planteadas). Razón: los otros
> tres casos de valor inválido del módulo —`dayOfWeek: 7`, `totalWeeklyHours: 200`, `days: []`— ya responden
> `400` **nombrando el campo**, y un enum inválido es precisamente lo que un validador puede ver. Ponerlo ahí lo
> alinea con sus tres hermanos en lugar de crear una cuarta forma de decir lo mismo — el criterio de
> [H-11](#h-11).
>
> **El predicado quedó en el dominio, junto a los valores permitidos** (`WorkScheduleAnchors.IsKnown`,
> `WorkScheduleClasses.IsKnown`), y **los dos lados lo comparten**: el validador delega y los setters del
> agregado lo usan. Así no pueden discrepar sobre qué es válido, ni siquiera en el manejo de mayúsculas. La
> alternativa era exponer `PayrollNormalization` (que es `internal` al dominio) o duplicar la normalización en la
> capa de aplicación; las dos habrían dejado dos definiciones capaces de derivar.
>
> **El `catch` se conserva** para lo que solo el agregado puede juzgar —día duplicado, comida en turno nocturno,
> turno de cero horas— y su comentario, que **admitía** cubrir `anchor/class out of range`, quedó corregido con
> la advertencia de no volver a ensancharlo.
>
> ### Verificación
>
> 2 rojos antes (los dos enums, con `422` en vez de `400`) y 2 verdes de no-regresión, uno de ellos clave: **el
> día duplicado debe seguir devolviendo `422 WORK_SCHEDULE_DAY_INVALID`**, para que acotar el alcance del `catch`
> no lo convierta en un `500`. 4/4 después. Unitarios **2871/2871** — el guardrail de localización volvió a
> atrapar las dos claves de mensaje nuevas.

### Hallazgo original

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
## H-19 · ✅ FASE 1 RESUELTA · ⏸️ FASE 2 ESPERA EL MARCADOR

> **2026-08-10.** La fase 1 quedó construida junto con [H-20](#h-20) — son la misma pieza: el módulo de horas
> extra ahora **resuelve la jornada del empleado**, que era lo que faltaba para que cualquier regla pudiera
> existir. Se cerró con **una diferencia de diseño respecto de la propuesta original**, decidida por el usuario.
>
> **La exención vive en la PLAZA, no en la persona.** La propuesta 1 pedía un `overtimeExempt` en el empleado; se
> construyó como `position_slots.generates_overtime boolean NOT NULL DEFAULT true`. El motivo es que la
> elegibilidad a horas extra es una propiedad **del puesto**: una dirección general no genera horas extra sea
> quien sea quien la ocupe, y la regla sobrevive al cambio de titular —una bandera en la persona habría que
> volver a ponerla en cada relevo—. Y el **multi-plaza es real** en el ambiente (hay un empleado con
> `PL-ANANOM-001` y `PL-ANACONT-001`), así que una bandera por persona sería incorrecta para él: podría ocupar
> una plaza exenta y otra no. El código del error quedó `OVERTIME_POSITION_EXEMPT` en vez de
> `OVERTIME_EMPLOYEE_EXEMPT`, por lo mismo.
>
> **Encaje técnico:** el registro ya llevaba `assignedPositionPublicId`, que es el id de la **asignación**
> (empleado–plaza). De esa única fila salen **la plaza** (`position_slot_public_id`) **y la jornada**
> (`workday_code`), así que las dos cosas que hacían falta se resuelven en un solo salto, sin consultas extra.
>
> **Propuestas 1-4 de la fase 1, todas cerradas:** exención declarada (en la plaza) · bloqueo en el **alta**, no
> en la autorización · **advertencia** sin bloquear cuando no hay jornada
> (`OVERTIME_WARNING_MISSING_WORK_SCHEDULE` en `warnings[]` de la respuesta) · tramo validado (ver H-20).
> Con la 1 + la 3, `workdayCode = null` deja de ser ambiguo: la exención se **declara** en la plaza y la ausencia
> de jornada es **siempre** un aviso.
>
> **Día sin fila en la jornada = día libre**, decisión del usuario: toda hora trabajada ahí es extra. Eso cubre
> las jornadas custom sin modelar nada nuevo (06:00-18:00 de lunes a jueves deja viernes, sábado y domingo
> ausentes), que era el ejemplo que motivó la aclaración.
>
> **Sigue pendiente la fase 2 (propuestas 5-8), y no por falta de código sino de insumo:** no hay marcadores.
> Lo que la fase 1 le deja listo: la jornada ya se resuelve por fecha, el tramo ya es la fuente de verdad de la
> duración, la banda legal ya se clasifica y la máquina `EN_REVISION → AUTORIZADA` ya es el flujo
> "proponer y validar". La propuesta 8 (tope diario derivado de la jornada en vez de una preferencia única de
> empresa) se volvió **más** urgente con el ejemplo del usuario: con jornadas de 11 h y de 8 h en la misma
> empresa, un solo `preferences.MaxDailyMinutes` no puede servir a las dos.
>
> **Lo que quedó fuera a propósito:** *derivar el tipo* (`HED`/`HEN`/`HEDF`/`HENF`) del tramo + el calendario de
> asuetos —propuesta 4 de [H-20](#h-20)— **no se construyó**. El catálogo `OvertimeType` no tiene banda ni marca
> de asueto: solo `Code`, `Name` y `DefaultFactor`, y el código lo edita cada empresa. Derivar exigiría o reservar
> los cuatro códigos de ley, o adivinar por texto contra un catálogo libre; ambas cosas son decisiones de negocio,
> no de implementación. La regla pura ya está escrita y probada (`OvertimeScheduleRules.DeriveTypeCode`, cuatro
> casos), así que el día que se decida es conectarla.

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
## H-20 · ✅ RESUELTO 2026-08-10

> **El tramo pasó de decorativo a fuente de verdad, y las cuatro vías de doble pago quedaron cerradas.**
>
> **Propuesta 1 — el tramo manda.** `startTime`/`endTime` son **obligatorios** y la duración se **deriva** de
> ellos; `durationHours`/`durationMinutes` **dejaron de aceptarse en la entrada** (siguen en la respuesta, que es
> lo que consume el motor). Se eligió eliminar una de las dos fuentes en vez de "validar coherencia": el motor
> paga `Σ(durationDecimalHours × factor)` y el tramo no entraba en la cuenta, así que un registro podía declarar
> 8 h con tramo de 10:00 a 11:00 y cobrar ocho mientras el autorizador aprobaba una. Derivándola, **lo que el
> autorizador lee es exactamente lo que se paga**. `endTime < startTime` no es error: es cruce de medianoche,
> legítimo (22:00-02:00 = 4 h). Un tramo de cero minutos —que antes se aceptaba— es
> `422 OVERTIME_RANGE_EMPTY`.
>
> **Propuesta 2 — solape con la jornada** → `422 OVERTIME_WITHIN_SCHEDULED_SHIFT`. Se mide contra **todo el
> turno**, sin descontar la comida: su duración varía por empresa (1 h, 2 h) y el empleado la toma a la hora que
> acuerde con su jefatura, así que la ventana almacenada es **nominal** y no se puede reclamar como "fuera de
> jornada". Un solape parcial **rechaza el registro completo** en vez de recortarlo (decisión del usuario):
> recortar en silencio cambiaría lo que la persona pidió.
>
> **Propuesta 3 — solape entre registros** del mismo empleado y fecha → `422 OVERTIME_RECORDS_OVERLAP`. En la
> edición el propio registro se excluye de la comparación, o ningún `PUT` pasaría.
>
> **Un guard que el hallazgo no pedía y que salió del análisis:** un tramo que cruza el corte legal 06:00 / 19:00
> (art. 161 CT) es `422 OVERTIME_CROSSES_LEGAL_BOUNDARY`. Un registro lleva **un solo factor**, y 18:00-21:00 es
> 1 h diurna (×2.00) + 2 h nocturna (×2.50) = 7.00 h-factor: ni todo-`HED` (6.00, corto) ni todo-`HEN` (7.50, de
> más) lo expresan. Se rechaza para que el usuario lo divida en vez de partirlo solo (decisión del usuario), y
> así una solicitud sigue siendo un registro en la bandeja y en la auditoría.
>
> **Propuesta 4 — derivar el tipo: NO se construyó.** El motivo está en el cierre de [H-19](#h-19): el catálogo
> `OvertimeType` no tiene banda ni marca de asueto y su código lo edita cada empresa.
>
> **Verificación.** 24 unitarios nuevos de la regla pura + 9 de integración; los 5 guards y la advertencia se
> comprobaron **neutralizando la implementación**: cayeron exactamente los 6 tests esperados y ninguno de los de
> éxito (regla de [H-33](#h-33)). El test de "65 minutos" se volvió inexpresable —la duración ya no se teclea— y
> se reemplazó por el hueco que ese chequeo dejaba abierto: el tramo vacío.

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
## H-21 · ✅ RESUELTO 2026-08-11

> **El hallazgo se quedó corto: no eran 2 endpoints, eran ~88 lecturas de catálogo.** El mismo corte
> (`if (countryCatalogItemId is null) return [];`) se repetía en **6 métodos** del repositorio y cubría **53
> categorías** país-scoped detrás de `general-catalogs/{catalogKey}`, las **12** de `reference-catalogs` y los
> **4 controladores dedicados** (`compensation-concept-types`, `settlement-concepts`, `contract-types`, `afps`).
>
> **Y la familia se contradecía a sí misma:** `reference-catalogs` declaraba el país **requerido** y respondía
> `400`; `general-catalogs` lo declaraba opcional y respondía `200 []`. Mismo controlador, contratos opuestos. El
> motivo real es que la query de `general-catalogs` sirve **también** catálogos de sistema (education-*,
> `file-document-types`) que legítimamente ignoran el país, así que se hizo opcional para todos.
>
> **Un segundo agujero que el hallazgo no menciona y que también se cerró:** un país **desconocido**
> (`countryCode=XX`) devolvía `200 []` en las **dos** familias, incluida la estricta. Ni el endpoint que validaba
> distinguía "país que no existe" de "catálogo vacío".
>
> ### Lo construido
>
> Una sola regla, en un solo lugar (`CatalogCountryResolution`, capa de aplicación): **el código explícito manda,
> el país del tenant es el respaldo, y si no hay ninguno de los dos se responde `400`**. La compañía **es** el
> tenant (`companies.public_id == tenant_id`), así que el país siempre estuvo a un salto de distancia.
>
> | Caso | Antes | Ahora |
> |---|---|---|
> | con `?countryCode=SV` | 200 con items | **igual** — el FE que ya lo manda no nota nada |
> | sin parámetro, con tenant | **`200 []`** | 200 con los items del país del tenant |
> | sin parámetro y sin tenant | `200 []` | **`400 CATALOG_COUNTRY_REQUIRED`** |
> | `countryCode=XX` | **`200 []`** | **`400 CATALOG_COUNTRY_UNKNOWN`** |
> | país válido sin filas | `200 []` | `200 []`, y **ahora eso solo significa "vacío"** |
> | sistema (`education-*`, `file-document-types`, `countries`) | ignoran el país | **sin cambio** |
>
> Decisiones del usuario: el país desconocido responde **`400`** (no `200 []`), y `reference-catalogs` **recibe el
> mismo respaldo de tenant** para que la familia tenga un solo comportamiento — es compatible hacia atrás y deja
> el `400` solo para cuando no hay tenant.
>
> **Detalles que importan:**
> - En `general-catalogs` el brazo de **sistema se resuelve PRIMERO**, sin país: al revés, `education-*` y
>   `file-document-types` empezarían a dar `400` para el llamante sin empresa, que es justo el flujo para el que
>   existe esa superficie.
> - La clasificación sistema/país vive en **un solo conjunto** (`GeneralCatalogScopes`) que leen el handler **y**
>   el repositorio; dos listas independientes habrían derivado en silencio, y esa deriva es invisible (una
>   categoría por país mal clasificada devuelve lista vacía para siempre). El brazo de sistema **lanza** si
>   aparece una categoría global sin rama, en vez de caer al de país.
> - **`education-careers` es por país** pese a vivir entre los catálogos de educación. Está fijado en un test.
> - No hizo falta un método nuevo: `CountryCodeIsActiveAsync` **ya existía** en el repositorio. Se extrajo
>   `ICatalogCountryLookup` (dos métodos) del que `IPersonnelFileRepository` ahora hereda, para poder probar la
>   regla sin levantar el repositorio entero.
>
> **Verificación.** 16 tests de integración nacieron **rojos** (10 del respaldo + 6 del país desconocido) y
> quedaron verdes; 5 más fijan que los de sistema siguen respondiendo sin país. 12 unitarios de la regla, incluido
> el caso sin tenant, que por HTTP no se puede provocar. Los tests existentes **todos** mandaban `?countryCode=SV`
> —por eso el defecto sobrevivió— y ninguno se rompió.
>
> **`file-document-types` sigue devolviendo `[]` con o sin país: eso es [H-22](#h-22)**, no este contrato. Queda
> fijado en un test para que nadie lo lea como una regresión de este arreglo.

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
## H-22 · ✅ RESUELTO 2026-08-11 · ❌ la mitad del diagnóstico era incorrecta

> **El hallazgo traía dos afirmaciones. Una es falsa y la otra es real.**
>
> ### ❌ «No existe ninguna ruta HTTP para crear los tipos» — FALSO
>
> El controlador existe: `src/CLARIHR.Backoffice.Api/Controllers/Catalogs/DocumentTypeCatalogsController.cs`,
> ruta `api/platform/document-type-catalogs`, política `PlatformOperator`, con **siete verbos** (GET paginado,
> GET por id, POST, PUT, PATCH JSON-Patch, activate, inactivate) despachando exactamente los cuatro comandos que
> el hallazgo daba por huérfanos — **y con sus propios tests** (`BackofficeDocumentTypeCatalogsIntegrationTests`).
>
> La búsqueda del hallazgo cubrió «las 591 rutas indexadas», que son las del **Core**. Este repositorio tiene
> **dos APIs**: `src/CLARIHR.Api` (tenant) y `src/CLARIHR.Backoffice.Api` (plataforma, `:5100`). Los catálogos de
> educación y los tipos de catálogo de perfiles viven ahí también, y el dominio lo dice explícitamente:
> *«Managed globally by platform operators via Backoffice»*.
>
> **Lección:** «no existe endpoint» exige buscar en **todos** los hosts del repositorio, no solo en el que uno
> tiene abierto.
>
> ### ✅ «La tabla está vacía» — CIERTO, y era el bloqueo real
>
> 0 filas. Y el bloqueo iba más hondo de lo que el hallazgo vio: **`platform_operators` también estaba en 0**, así
> que ni siquiera se podía usar el endpoint. El diagnóstico correcto no era «falta el endpoint» sino **«el
> catálogo nace vacío y llenarlo exige un camino operativo que nadie había recorrido ni documentado»**.
>
> ### ⚠️ «Campo obligatorio en 16 controladores» — impreciso
>
> La base lo zanja: solo **2** tablas tienen la FK `NOT NULL` (`personnel_file_documents`,
> `medical_claim_documents`) → esas dos familias sí estaban bloqueadas. Las otras **6** (incapacidades, tiempo
> compensatorio, amonestaciones, reconocimientos, ayuda económica, transacciones fuera de nómina) la tienen
> nullable y el handler **salta el lookup si viene `null`**: esos adjuntos **siempre funcionaron**, solo quedaban
> sin clasificar. El playbook §8.1 repetía las dos afirmaciones incorrectas y quedó corregido.
>
> ### Lo construido
>
> **12 tipos de plataforma sembrados por `HasData`** (ids `-9978…-9989`, banda verificada libre), el mismo
> mecanismo de los catálogos de educación: `CONSTANCIA_MEDICA`, `INCAPACIDAD`, `RECETA`, `FACTURA`, `RECIBO`,
> `CONTRATO`, `CARTA`, `TITULO`, `CURRICULUM`, `IDENTIFICACION`, `RESPALDO`, `OTRO`. El operador de plataforma
> sigue pudiendo agregar, renombrar y desactivar encima.
>
> **Un bug que solo apareció al probar de punta a punta:** la semilla usa **ids negativos** (convención de
> `HasData` en todo el repo, para no chocar con la secuencia de identidad) y los agregados de adjuntos guardaban
> la FK con `<= 0`, así que **rechazaban exactamente las filas que se siembran**. Un tipo creado por el Backoffice
> (id positivo) funcionaba; uno sembrado, no. El guard pasó a rechazar solo el **`0`** (el default sin asignar) en
> los **12 sitios** de 5 agregados. El test de dominio que afirmaba lo contrario se reescribió a la invariante
> nueva. Sin esto, la semilla habría quedado decorativa.
>
> ### El camino del operador, recorrido de verdad
>
> No se documentó de memoria: se ejecutó. `cp appsettings.Development.json{.example,}` →
> `bootstrap-platform-operator <correo> Admin` (exige un usuario local **activo y con contraseña** ya existente) →
> `:5100` → `POST /api/platform/auth/login` **200** → `GET /api/platform/document-type-catalogs` **12 tipos** →
> `POST` uno nuevo **201** → `PATCH /{id}/inactivate` con `If-Match` **200**. El tipo de prueba se borró; el
> ambiente quedó con los 12 activos.
>
> ### Decisiones del usuario (2026-08-11), no pendientes
>
> **El catálogo se queda GLOBAL.** Una empresa no puede tener sus propios tipos: si un cliente pide «Carta de
> renuncia interna», la agrega el operador de plataforma y queda visible para todas las empresas. Es la decisión,
> no un límite por resolver — no construir tipos por empresa sin pedirlo de nuevo. Tampoco se vuelve país-scoped
> (el hallazgo lo proponía): un «recibo» no cambia de naturaleza entre países, y costaría dominio + migración +
> reclasificación en `GeneralCatalogScopes` sin ganar nada.
>
> **El operador de plataforma de dev se queda.** El bootstrap de la verificación dejó a
> `solicitante.tabulador@clarihr.test` como operador `Admin`; el usuario decidió conservarlo para poder curar
> tipos desde el Backoffice.

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
## H-23 · ✅ RESUELTO 2026-08-11 · el hallazgo se quedaba corto en la gravedad

> **«Ninguna regla lo consume» era falso, y eso lo empeora.** El **tablero de RRHH** lo leía:
> `PersonnelFileDashboardRepository.GetPositionOccupancyAsync` acumulaba `occupied += slot.OccupiedEmployees` y lo
> publicaba como `positionOccupancy` en `GET .../personnel-files/dashboard/overview`. O sea, no era un campo
> decorativo: era un **KPI que se le muestra a RRHH construido sobre una nota manual**. Y no tenía **ni un test**.
>
> Lo demás del hallazgo se confirmó: ningún escritor lo mantenía (la asignación nunca lo tocaba) y las reglas
> duras —cupo al asignar, suspensión con ocupantes— ya contaban asignaciones reales.
>
> **Y había un tercer camino que el hallazgo no vio: `PATCH /status` INVENTABA el número.**
> `EnsureStatusConsistency()` ponía `0` al pasar a `Vacant` y **`1`** al pasar a `Occupied`. Dos llamadas de API
> bastaban para que el tablero reportara un ocupante que no existía.
>
> ### Lo construido (opción A + estado derivado + endpoint eliminado, decisión del usuario)
>
> - **`occupiedEmployees` se deriva** contando las asignaciones activas de la plaza, en las cuatro proyecciones
>   (listado, detalle, grafo, export). El campo **sigue en la respuesta con el mismo nombre y tipo**: el FE no
>   cambia de forma, solo empieza a recibir la verdad.
> - **El estado también se deriva**: `Occupied` si tiene gente, `Vacant` si no, `Suspended` si está retirada.
>   `IsActive` (persistido) es lo único que se decide, porque retirar una plaza sí es una decisión. El filtro
>   `?status=` del listado evalúa el hecho derivado (traduce a `EXISTS`).
> - **El tablero cuenta asignaciones reales**, respetando sus filtros de dimensión.
> - **Se eliminaron**: las columnas `occupied_employees` y `status`, el índice `(tenant, status)`,
>   `UpdateOccupancy`, `ValidateStatusOccupancyConsistency`, `EnsureStatusConsistency`, los cuatro códigos de
>   error de ocupación y el endpoint **`PATCH /position-slots/{id}/occupancy`**.
> - **Índice nuevo** `(tenant_id, position_slot_public_id, is_active)` en las asignaciones: la derivación cuenta
>   por esa columna, que ya usaban sin índice la regla de cupo y el guard de H-15.
> - **El playbook pierde su §7.7**, que documentaba el paso manual «porque la asignación no lo hace».
>
> ### Verificación
>
> 6 tests de integración nacieron rojos y quedaron verdes, incluido el del `1` fabricado (marcar «Ocupada» una
> plaza vacía ya no mueve el tablero) y el del indicador, que **no tenía cobertura alguna**. Los tests de dominio
> que fijaban las coerciones se reemplazaron por el que fija la regla nueva. Se retiró
> `PositionSlots_UpdateOccupancy_WhenOverCapacity_ShouldReturn422`: probaba la guarda del endpoint eliminado, y el
> cupo real ya está cubierto donde se aplica (`EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED`, por vigencia solapada).
>
> ### Un tropiezo del ejecutor, anotado
>
> Al limpiar un bloque quedó `PositionSlotAdministration.cs` sintácticamente roto y se restauró con
> `git checkout`, lo que **borró también el trabajo sin commitear de H-14, H-15, H-16 y H-19 en ese archivo**. Se
> reconstruyó todo (guard de banda salarial, guard de suspensión con ocupantes, campos de jerarquía + filtros del
> listado, `generatesOvertime`) usando como especificación los tests de esos hallazgos, que sí sobrevivieron.
> **Regla para adelante: nunca `git checkout` sobre un archivo con trabajo sin commitear.**

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
## H-25 · ✅ RESUELTO 2026-08-11

> **El hallazgo acertaba y se quedaba corto: no era un mensaje pobre, era un código usado 154 veces para cuatro
> cosas distintas.** Clasificados por la condición que los produce:
>
> | Sitios | Significaba | Ahora responde |
> |---|---|---|
> | **143** | «falta llamar `finalize`» | **`PERSONNEL_FILE_NOT_FINALIZED`** con `lifecycleStatus`, `requiredTransition` y `readiness` en el cuerpo |
> | **4** | «el expediente no es de empleado» | **`PERSONNEL_FILE_NOT_EMPLOYEE`** |
> | **1** | «ya estaba finalizado» (el opuesto exacto del primero) | **`PERSONNEL_FILE_ALREADY_FINALIZED`** |
> | **1** | «no hay ancla de antigüedad» | **`PERSONNEL_FILE_VACATION_ANCHOR_MISSING`** (conecta con [H-28](#h-28)) |
> | **5** | una excepción de dominio atrapada | **`404 PERSONNEL_FILE_CHILD_NOT_FOUND`** o una **validación que nombra el campo** |
>
> `PERSONNEL_FILE_STATE_RULE_VIOLATION` **ya no existe**. Se borró primero, a propósito, para que **el compilador
> enumerara los 154 sitios** (308 errores) y ninguno quedara olvidado — un script no habría visto 28 de ellos.
>
> Los 5 `catch (InvalidOperationException)` eran los peores: lo que el dominio lanza ahí es *"Language with public
> id X not found"* (un **404**) o *"At least one language skill must be true"* (una **validación**), y las dos
> volvían como «el estado del expediente no permite esto», que es falso en ambos casos.
>
> **El mensaje en español estaba a medio traducir:** *"La operacion solicitada no esta permitida para el personnel
> file state."* Los cinco códigos nuevos quedaron en los dos `.resx`.
>
> ### La frontera se queda donde está (decisión del usuario)
>
> Información de empleo y compensación siguen exigiendo expediente finalizado; identificaciones, cuenta bancaria y
> **asignación de plaza** siguen funcionando en `Draft`. Es coherente: **`finalize` EXIGE una plaza asignada** más
> el correo institucional, así que el orden lo impone el código —
> `crear → asignar plaza → finalize → empleo · compensación` — y está fijado en un test de no-regresión que recorre
> el camino completo.
>
> ### Tres trampas del camino que el hallazgo no mencionaba, verificadas al ejecutar
>
> 1. **Asignar la plaza mueve el `concurrencyToken` del expediente** (la asignación *toca* al padre), así que hay
>    que releerlo antes del `finalize` o responde `409`.
> 2. **El `If-Match` del `PUT employment-information` es obligatorio incluso la primera vez**, cuando la sección
>    todavía no existe y su valor no se compara contra nada. El swagger afirmaba lo contrario; corregido.
> 3. El `finalize/preview` —que **ya existía** y lista lo que falta con código, sección y campo— nunca estaba
>    referenciado desde el muro. Ahora el error apunta ahí.
>
> ### Verificación
>
> 6 tests de integración nacieron rojos y quedaron verdes · unitarios 2908/2908 · el unitario que fijaba el código
> viejo se reescribió al nuevo, incluyendo las tres claves del payload · swagger de `finalize`,
> `employment-information` y `compensation-concepts` · playbook §7.0, guía de integración del FE y
> `endpoint-reference.md` con la tabla de los cinco códigos.
>
> **De paso**: la nota de §7.0 que decía «la ocupación de la plaza no se actualiza sola» quedó falsa desde
> [H-23](#h-23) y se corrigió aquí; y un test de asignaciones que seguía esperando `200` en el `DELETE` de un hijo
> ([H-34](#h-34)) se actualizó — mi filtro de esa regresión no lo alcanzaba.

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
## H-26 · ✅ RESUELTO 2026-08-11

> **La causa no era la que decía el hallazgo, y eso cambiaba el arreglo.** Decía que la fecha «llega intacta hasta
> Npgsql y revienta ahí — **después** de pasar validación, ya dentro de la escritura». La escritura nunca fue el
> problema: el agregado normaliza al construirse (`StartDate = NormalizeDate(startDate)` → `SpecifyKind(Utc)`). Lo
> que reventaba era **la consulta previa**: `CountOverlappingActiveAssignmentsForSlotAsync` compara
> `item.StartDate <= endDate` con la fecha **cruda del request**, antes de que el dominio la toque. Normalizar en el
> dominio no habría arreglado nada.
>
> **🆕 Y había una segunda vía de entrada que el hallazgo no vio.** Reproducido en vivo:
> `GET /companies/{c}/personnel-actions/export?fromUtc=2026-08-01` → **`500`**; con `Z` → `200`. Son **34
> parámetros `[FromQuery] DateTime`** en los controladores de reportes, y esos **no pasan por el serializador JSON**
> — los liga el model binder de MVC. La opción 1 del hallazgo («configurar el serializador») era correcta pero
> **insuficiente**: habría dejado vivos esos 34.
>
> **El defecto era intermitente por endpoint**, que es lo peor para diagnosticarlo:
> `incapacities/export?startFromUtc=2026-08-01` devolvía `200` porque ese valor no termina en una comparación SQL.
>
> ### Lo construido (A + B, decisión del usuario)
>
> **A · Normalización en la frontera, en las dos vías.** `UtcDateTimeJsonConverter` (+ su gemelo nullable) para el
> cuerpo y `UtcDateTimeModelBinderProvider` para query/ruta/formulario, registrados en `Program.cs`. La regla, con
> sus tres casos —y el del medio es la trampa—:
>
> | `Kind` que llega | Qué se hace | Por qué |
> |---|---|---|
> | `Unspecified` (`"2026-08-01"`) | `SpecifyKind(Utc)` | es lo que ya hacía el dominio |
> | `Local` (`"…T00:00:00-06:00"`) | **`ToUniversalTime()`** | reetiquetar correría el instante 6 horas |
> | `Utc` | tal cual | — |
>
> Hay un test dedicado al caso `Local`: crea una plaza con `effectiveFromUtc = 2026-08-01T00:00:00-06:00` y exige
> que quede guardada como `2026-08-01T06:00:00Z`. Sin él, «arreglar» reetiquetando pasaría desapercibido.
>
> **B · `DateOnly` en los campos que son un día.** `startDate`/`endDate` de la asignación y `hireDate` de la
> información de empleo pasan a `DateOnly` en el contrato. Con un `LenientDateOnlyJsonConverter` que acepta
> **también** la forma de instante (`"2026-08-01T00:00:00Z"`), porque era la que el playbook venía documentando como
> obligatoria: cambiar el tipo no puede romper a quien ya funcionaba — y los 22 tests existentes que la usan lo
> demuestran.
>
> ### B llegó hasta el fondo: el tipo se decidió y bajó al dominio y a la base
>
> Al principio el borde convertía el día a UTC-medianoche y el dominio seguía guardando un `DateTime`. Eso dejaba la
> mentira intacta un nivel más abajo, y era justo la que produjo el `500`: la consulta de capacidad comparaba en SQL
> antes de que el agregado normalizara. **La regla que quedó fijada:**
>
> | Lo que dice el negocio | Tipo | Columna |
> |---|---|---|
> | «el **día** en que pasó» (`startDate`, `endDate`) | `DateOnly` | `date` |
> | «el **momento** en que pasó» (`effectiveFromUtc`, `transactionDateUtc`, sellos de auditoría) | `DateTime` | `timestamptz` |
>
> `PersonnelFileEmploymentAssignment.StartDate`/`EndDate`, sus reglas, su repositorio y las dos columnas pasaron a
> días (migración `H26AssignmentDatesAsDate`). Efectos que lo justifican solos: `PersonnelTransactionRepository`
> **dejó de inventar medianoches UTC** para comparar contra una ventana que ya venía en días, y
> `PersonnelFileVacationRepository` —el ancla de [H-28](#h-28)— dejó de hacer `DateOnly.FromDateTime` sobre el
> `startDate` que acababa de leer. Los bordes que siguen en instantes (historial de contrato, conceptos de
> compensación, motor de finiquitos certificado, snapshot de la baja) convierten explícitamente, cada uno con su
> comentario.
>
> ⚠️ **La trampa de la migración**, verificada contra la base de desarrollo: un `timestamptz::date` a secas se
> resuelve con el `TimeZone` de la **sesión**, así que desde una conexión en `America/El_Salvador` convertiría
> `2026-12-01 00:00+00` en **`2026-11-30`**. Medido: `('2026-12-01 00:00:00+00'::timestamptz)::date` → `2026-11-30`
> contra `(… AT TIME ZONE 'UTC')::date` → `2026-12-01`. La migración ancla el `USING … AT TIME ZONE 'UTC'`; las 60
> filas del ambiente conservaron su día. Sin eso, cada asignación habría retrocedido un día y con ella la antigüedad
> de vacaciones de H-28.
>
> **Y el `500` dejó de filtrar el motor.** Respondía textualmente *"Cannot write DateTime with Kind=Unspecified to
> PostgreSQL type 'timestamp with time zone'… (Parameter 'value')"*. Ahora el `detail` es genérico y el mensaje real
> queda solo en el log, localizable por el `traceId` que ya viaja en la respuesta.
>
> ### Verificación
>
> 7 tests de integración nacieron rojos (6 de 7) y quedaron verdes: los tres formatos de día en la asignación, las
> dos formas por query string, el del offset explícito y el de que no se filtre el motor. Y uno más fija **el tipo**,
> no solo la ausencia del `500`: `Assignments_StartDate_ShouldRoundTripAsAPlainDay` exige `"2026-12-01"` **exacto**,
> sin parte de hora. Comprobado que muerde: con el serializador escribiendo la forma de instante falla 3/3.
>
> Unitarios 2908/2908 · asignaciones y fechas 22/22 · vacaciones 8/8 ·
> retiros/liquidaciones/recontratación 46/46 · transacciones y tablero 18/18 · build limpio · migración aplicada al
> ambiente sin corrimiento de día.
>
> ### Lo que queda de B, anotado
>
> `birthDate`, `issuedDate`/`expiryDate` de identificaciones y el resto de los ~166 `DateTime` de contratos de
> entrada siguen siendo `DateTime`. **Ya no fallan** —la frontera los normaliza—, así que convertirlos es una mejora
> de expresividad, no un arreglo; cuando se haga, la regla de la tabla de arriba decide cada uno. El `startDate` de
> la asignación, que era el bloqueante de [H-28](#h-28), **ya está decidido y bajado a `date`**.

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
## H-27 · ✅ RESUELTO 2026-08-12

> **La medición del hallazgo había quedado vieja, así que lo reproduje.** Las 382 filas se fueron con un reseteo de
> la base (hoy 59 cuentas / 59 empleados / 59 primarias), pero el defecto seguía intacto: dos `POST` idénticos
> —mismo banco, misma moneda, mismo número, mismo tipo— devolvieron **`201` los dos** y el empleado quedó con
> **3 cuentas, las 3 primarias y un solo número**.
>
> ### El daño real, medido: dos problemas de tamaño distinto
>
> El único consumidor de la primaria es la conciliación bancaria, y elegía así
> (`PayrollRunRepository.cs:843`):
>
> ```csharp
> account ??= fileAccounts.FirstOrDefault(item => item.IsPrimary) ?? fileAccounts.FirstOrDefault();
> ```
>
> …sobre una consulta **sin `OrderBy`**, o sea el orden físico de las filas, que cambia con los updates y el
> vacuum. **Pero la cuenta designada en la plaza gana sobre la primaria**, y las **60** asignaciones activas del
> ambiente la tienen puesta: lo comprobé con tres primarias distintas y la conciliación devolvió la misma cuenta
> las tres veces. Así que el problema de la primaria es **latente** —vive esperando a un tenant que no designe— y
> el de los duplicados es **concreto**: basura de auditoría en un módulo de nómina, y N llamadas para borrarla.
>
> ### 🆕 El punto 3 del hallazgo se confirma, pero las dos colecciones no son el mismo caso
>
> Hay **seis** banderas de primaria en el esquema. Tres ya tenían unicidad (representantes legales y empresas del
> usuario con índice único parcial; las asignaciones de plaza con auto-degradación en código). De las que faltaban:
>
> | | Duplicados | Primaria única | ¿Alguien lee la primaria? |
> |---|---|---|---|
> | `personnel_file_bank_accounts` | ❌ nada | ❌ nada | ✅ **sí** (dónde cae el sueldo) |
> | `personnel_file_identifications` | ✅ **ya bloqueados** | ❌ nada | ❌ nadie |
>
> Las identificaciones ya tenían `uq_..._tenant_type_number`, **y por tenant, no por expediente**: el mismo DUI no
> se puede registrar dos veces en la empresa. Así que ahí solo faltaba la primaria única, y se incluyó por decisión
> del usuario (mismo índice, mismo patrón) aunque hoy nadie la consuma. `location_groups.is_default` es de otro
> módulo y queda fuera.
>
> ### La trampa ya estaba documentada, y era la que me habría mordido
>
> `LegalRepresentativeAdministration.cs:1060` lo explica: el índice único **parcial** se evalúa **por sentencia** y
> no es diferible, así que si la degradación y la promoción van en el mismo lote EF puede ordenar la promoción
> primero, dejar dos filas primarias por un instante y **Postgres rechaza el lote — el cliente recibe un 500**. Los
> cinco caminos nuevos (alta/`PUT`/`PATCH` de cuentas y alta/edición de identificaciones) descargan la degradación
> **antes** de promover, dentro de la misma transacción.
>
> ### Lo construido
>
> `PersonnelFileCompensationController` · `POST/PUT/PATCH /api/v1/personnel-files/{publicId}/bank-accounts`
>
> - **Índice único** `(personnel_file_id, bank_catalog_item_id, normalized_account_number, currency_code)` +
>   guarda en el handler con código propio `PERSONNEL_FILE_BANK_ACCOUNT_DUPLICATE` (422, en ambos `.resx`). El
>   índice cierra la carrera; la guarda da el mensaje.
> - **Índices únicos parciales** `(personnel_file_id) WHERE is_primary` en cuentas y en identificaciones, el patrón
>   de `uq_user_companies__primary_user`.
> - **Auto-degradación, no rechazo**: marcar una cuenta como primaria desmarca la anterior. Es el patrón de la casa
>   y es lo que el usuario espera — no querés un error por marcar tu cuenta nueva como principal.
> - **La primera cuenta es primaria por definición**, aunque el cuerpo diga `false`: si no, el expediente queda con
>   cuentas y ninguna principal y el consumidor cae en un `FirstOrDefault()` sin criterio.
> - **La moneda entra en la clave, el tipo de cuenta no**: la misma cuenta en dólares y en colones es un caso real;
>   el mismo número no puede ser de dos tipos.
> - **`OrderBy` explícito en la conciliación**, para que la elección sea estable incluso sobre datos escritos antes
>   del arreglo y deje de depender de un detalle del motor.
> - **Limpieza destructiva en la migración**, antes de crear los índices: deduplica conservando la fila más
>   antigua, degrada las primarias sobrantes y **repone la primaria del expediente que quedara sin ninguna**. Sin
>   eso la migración falla en cualquier base que ya cargue el desorden que el defecto permitía.
>
> ### 🆕 Y el número se normaliza sin separadores
>
> El primer arreglo dejaba pasar `2000-0021-3813` contra `200000213813`: el duplicado disfrazado. El repo **ya
> había decidido esto** para los documentos de representantes legales, con el comentario *«`01234567-8` y
> `012345678` son una persona, no dos»*. Se aplicó el mismo criterio con
> `PersonnelFileNormalization.NormalizeAccountNumber`. Verificado que las 59 filas del ambiente no quedaron
> desalineadas (ninguna traía separadores).
>
> ### Verificación
>
> **6 de los 8 tests nacieron rojos por la razón exacta** —`Created` donde debía decir `422`, y *«the collection
> contained 2 matching items»* para la primaria—; los otros dos pasaban desde el principio a propósito: el de la
> misma cuenta en **otra moneda** (que debe permitirse) fija el límite deliberado para que nadie «endurezca» la
> regla y rompa el caso legítimo.
>
> Unitarios **2922/2922** · expedientes, cuentas e identificaciones **48/48** · nómina **40/40** · build limpio.
>
> **En vivo**, repitiendo el sondeo que había reproducido el defecto: el duplicado exacto → **`422`** las dos
> veces, el mismo número con separadores → **`422`**, y una cuenta distinta marcada primaria → `201` con la
> anterior **degradada automáticamente**. El ambiente quedó en 59/59 y sin expedientes sin primaria.
>
> ### La nota de contrato del hallazgo sigue en pie
>
> El `DELETE` de una cuenta exige en `If-Match` el `concurrencyToken` **de la cuenta**, no el del expediente.
> Desde [H-34](#h-34) el token del padre ya no viaja en ninguna respuesta de borrado, así que nada lo sugiere; el
> token correcto viene en cada ítem del listado. No se cambió.

---

<a id="h-27-original"></a>
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
modificó" cuando en realidad es "mandaste el token equivocado".

> **Actualización 2026-08-11 ([H-34](#h-34)):** el token del padre **ya no viaja en la respuesta** de ningún
> `DELETE` de hijo (los 53 responden `204`), así que la tentación de mandarlo en el `If-Match` desaparece. La
> confusión del `409` sigue siendo posible si alguien reusa el token del expediente que trae el `GET`, pero ya no
> hay nada en la respuesta del borrado que la sugiera. El token correcto viene en cada ítem de
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
## H-28 · ✅ RESUELTO 2026-08-12

> **Eran DOS resoluciones del ancla, no una.** El hallazgo señalaba `GetAnchorDateAsync` (el alta individual).
> Faltaba la de la **generación masiva** —`GetGenerationCandidatesAsync` armaba su propio diccionario con la misma
> precedencia por plaza—, que es justo el endpoint del escenario medido con 0 de 59. Arreglar solo una dejaba roto
> el camino del hallazgo.
>
> **Y el ancla gobernaba dos cosas.** No solo la elegibilidad: `PeriodBounds` también fija la **ventana** del
> periodo, y con el aniversario activado (el default) corría sobre la fecha de registro de la plaza. Mover una sin
> la otra dejaba la elegibilidad y la ventana midiéndose contra fechas distintas.
>
> ### El ancla ya estaba decidida en el resto del sistema
>
> `HireDate` es el ancla de antigüedad en **los otros seis consumidores**: `EmployeeSeniority.Between` (perfil),
> rangos del tablero, meses mínimos de ayuda económica, constancias, el guard del retiro… y la **recontratación**,
> que reescribe `HireDate` justo «para reiniciar antigüedad» (D-03). O sea: `HireDate` ya significaba «inicio de la
> relación laboral **vigente**», y vacaciones era el único módulo fuera de línea.
>
> Lo delator estaba escrito en el código: el parámetro de la regla se llamaba **`hireOrPlazaStart`** —«el ingreso
> *o* el inicio de plaza», como si fuera «el que aplique»— pero la implementación siempre prefería la plaza y solo
> caía al ingreso cuando el empleado no tenía **ninguna** asignación, que es justo el caso donde no hay nada que
> calcular.
>
> ### 🆕 El mismo defecto vivía en el finiquito, y estaba RATIFICADO
>
> `SettlementCalculation.Rules.cs` medía `RetirementDate − PlazaStartDate` («P-01 ratified: from the assignment
> StartDate»). Eso alimenta **indemnización**, el tramo de **aguinaldo** (15/19/21) y el mínimo de 2 años de la
> prestación por renuncia. Con los datos del ambiente, alguien ingresado 2024-02-01 con plaza desde 2026-08-01 se
> liquidaba por **11 días** en vez de 2.5 años. **Decisión del usuario: también pasa al `hireDate`.**
>
> Se guarda como **snapshot** (`personnel_file_settlements.seniority_start_date`), igual que `plaza_start_date`:
> editar después la fecha de ingreso no debe reescribir un finiquito ya valorado. Sigue siendo **por plaza** —cada
> plaza aporta su propio salario—, así que dos plazas con la misma antigüedad **reparten** el monto en vez de
> duplicarlo. La `DaysSinceAnniversary` de la vacación proporcional (G-04) también pasa al aniversario de ingreso,
> para que no discrepe del fondo.
>
> ⚠️ **La trampa de la migración**: EF generaba la columna con `defaultValue: 0001-01-01` —una antigüedad de dos
> mil años esperando a que existiera una fila—. Se reescribió a mano: se agrega *nullable*, se rellena con
> `plaza_start_date` (la semántica exacta de antes, así ningún finiquito ya valorado se mueve) y solo entonces se
> vuelve obligatoria.
>
> ### 🆕 Y el modo año calendario llegaba un año tarde
>
> La elegibilidad se medía contra el **inicio** del periodo. En modo aniversario (el default) da igual —la ventana
> arranca en el aniversario—, pero en modo **año calendario** quien ingresó el 2026-01-16 cumple su año el
> 2027-01-16, o sea **dentro** del calendario 2027, y se le negaba: su primer fondo salía en 2028. Ahora la
> decisión vive en un solo lugar, `VacationRules.IsEligibleForPeriod`, que mide contra el **fin** del periodo.
>
> ### Los años pasados quedan abiertos (decisión del usuario)
>
> `generate` acepta cualquier año de `[2000, 2100]`, incluido el pasado: es la vía para cargar el fondo que la
> empresa ya debía al arrancar. Un periodo generado siempre otorga los días completos, así que para un año cuya
> vacación ya se gozó la empresa **baja los días** con el `PUT` que ya existía. Queda fijado con un test, no como
> nota.
>
> ### Verificación
>
> **Los 5 tests de integración nacieron rojos** (4 de 5 — el quinto fija que el corte de <1 año siga rechazando, y
> ya pasaba): `created: 0` donde debía decir 2 y 1, y `422 VACATION_ELIGIBILITY_NOT_MET` en los individuales. Los
> tests de vacaciones que ya existían **no podían ver el defecto**: sembraban el ingreso y el inicio de plaza con
> la misma fecha (instancia de [H-33](#h-33) — la regla tampoco tenía **ninguna** prueba unitaria; ahora tiene 12).
>
> El guardarraíl del finiquito se comprobó **mutando el cableado**: falla con
> `SERVICIO_MINIMO_NO_CUMPLIDO · «antigüedad: 0 años»`. En el primer intento el test moría antes en
> `SETTLEMENT_REQUESTER_NOT_HR`, así que la primera corrida «roja» no probaba nada — hubo que arreglar la siembra y
> repetirla.
>
> Unitarios **2922/2922** · vacaciones 13/13 · finiquitos 29/29 · retiros/recontratación/tablero 34/34 · build
> limpio.
>
> **En vivo contra el ambiente** (59 candidatos, 0 periodos): `generate { year: 2026 }` → **52 creados / 7
> inelegibles** (antes `created: 0` con 59 errores). Los 7 son los ingresados en 2026. El caso nominal del
> hallazgo, José Antonio Hernández Ramos (ingreso 2024-02-01, plaza 2026-08-01), quedó con periodo
> **2026-02-01 → 2027-01-31** —su aniversario de ingreso— y fondo de 15 días con provisión de $6,630 donde antes
> había `422` y ceros. Los 52 periodos de la prueba se borraron después.
>
> ### Lo que NO se hizo, a propósito
>
> **Sin fallback a la plaza.** Un fallback que solo dispara cuando falta un dato es exactamente la trampa que
> produjo este defecto. `HireDate` es no-nullable y el perfil es obligatorio para un expediente finalizado, así que
> el ancla siempre resuelve. Si algún día hace falta «recargo de funciones que no reinicia antigüedad», es un campo
> explícito.
>
> **Sin preferencia de empresa para elegir el ancla.** El Art. 177 no es configurable; una preferencia ahí
> permitiría configurar un incumplimiento.

---

<a id="h-28-original"></a>
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
## H-29 · ✅ RESUELTO 2026-08-12 (con H-30 y H-31)

> Los tres se entregaron juntos por decisión del usuario: dos de las 17 columnas dependían de H-30 y una de H-31,
> así que hacerlos por separado habría publicado un reporte fiscal incompleto y cambiado el contrato tres veces.
>
> ### 🆕 Eran TRES bloqueos, y el tercero es un error de retención
>
> El hallazgo pedía «verificar que `VIATICOS` y `OTRO_INGRESO` vengan con `Affects*` en `false`». **No había nada
> que verificar**: los dos mapeos de ingresos fijaban `AffectsIsss: true, AffectsAfp: true, AffectsRenta: true` a
> mano (`PayrollRuns.Handlers.cs:112` y `:136`) y el catálogo de conceptos de planilla **no tenía** columnas
> `affects_*` — mientras el de finiquitos **sí**. Misma asimetría que denuncia H-30: un motor modela el eje y el
> otro no.
>
> **Los viáticos y reembolsos se venían cotizando y gravando.** Medido con un viático de $150 sobre un salario de
> $600: ISSS de **`13.50`** cuando debía ser **`9.00`** — el 3 % cobrado sobre 450 en vez de 300, o sea **$4.50 de
> retención de más por empleado**. El test nació rojo con ese número exacto.
>
> El motor **siempre** supo honrar el eje (`PayrollCalculation.Rules.cs:290` arma las bases con las banderas por
> línea), así que se arregló llevando el dato: no se tocó una sola línea del cálculo.
>
> ### El eje de ingresos no existía
>
> El lado de descuentos ya tenía `default_deduction_class` con los tres valores que el reporte necesita. El lado de
> ingresos era **todo** `(sin clase)`: `SALARIO_BASE, BONO, COMISION, HORAS_EXTRA, VIATICOS, AGUINALDO,
> OTRO_INGRESO` compartían fila. Así que el «no hay que modelar nada nuevo» del hallazgo valía para los descuentos,
> no para los ingresos.
>
> Se agregó `default_income_class`, simétrico, y **sus valores son las columnas del reporte** (`Salario` · `Bono` ·
> `Comision` · `HorasExtra` · `NoDeducible` · `Aguinaldo` · `Otro`). Hoy hay 7 conceptos y 7 clases, pero el punto
> es hacia adelante: un `BONO_PRODUCTIVIDAD` que cree la empresa se marca `Bono` una vez y cae en la columna
> correcta, en vez de irse en silencio a «otros» o de agregarse a una lista de códigos dentro de la consulta.
> `AGUINALDO` quedó en **columna propia**: fiscalmente se trata distinto y no es un «ingreso adicional».
>
> ### La trampa que solo apareció midiendo: el salario caía en «otros»
>
> El motor emite su línea de salario con el código **`SALARIO`** y el catálogo la llama **`SALARIO_BASE`**. Sin un
> alias explícito, **la línea más grande de la planilla** —el 100 % del ingreso en la corrida del ambiente— caía en
> el bucket `otrosIngresos`. Se resolvió en un solo lugar (`PayrollConceptClassification.CatalogCodeFor`) que usan
> los dos consumidores.
>
> ### Y la otra: los días se duplicaban en multi-plaza
>
> El pivote dio **900 días** para 59 empleados (= 60 × 15) porque un empleado con dos plazas aporta dos líneas de
> salario de 15 días. Como «días del periodo» eso es falso, así que se toman **una vez** (`max`, no `Σ`). Medido
> después del arreglo: **885** = 59 × 15. Nota aparte: el reporte patronal que ya existía agrupa por
> `nombre + código + centro de costo`, así que partiría a un multi-plaza con centros distintos en dos filas —
> latente, no se manifiesta con estos datos porque las dos plazas comparten centro. El pivote de H-29 agrupa por
> `EmployeePublicId`.
>
> ### H-30 tenía una segunda razón, independiente de la mutabilidad
>
> El argumento del hallazgo era que el catálogo es mutable y un periodo cerrado se reclasificaría. Cierto, y hay
> más: `compensation_concept_type_catalog_items` está indexado por **`(country, code)`**, así que un join solo por
> código **multiplica filas** en cuanto se siembre un segundo país. Hoy solo hay `SV` y el código resulta único por
> accidente. Dos razones independientes para persistir la clase en la línea, que es lo que se hizo
> (`deduction_class` + `income_class`, con un `CHECK` que impide la combinación incoherente).
>
> ### H-31 — el hueco existía y se dejaba vacío
>
> `PayrollDeductionItem.Units` ya estaba declarado y los descuentos de pool sí lo pasaban; los dos mapeos de
> registro lo omitían. Ahora `PayrollRegistroRow` lleva el desglose por pagador (`UnpaidDays`, `EmployerPaidDays`,
> `SubsidizedDays`), se persiste en la línea y el reporte se arma **solo desde la corrida**, así que editar el
> registro origen después del cierre no mueve un periodo ya pagado.
>
> ⚠️ **Los días equivalentes NO se recomponen con un porcentaje.** Los tramos por riesgo pueden tener porcentajes
> distintos dentro de la misma incapacidad, así que el aporte de los días patronales se deriva del **monto que el
> motor realmente pagó** sobre la diaria del empleado.
>
> ### Lo construido
>
> `PayrollRunsReportingController`
> · `POST /api/v1/companies/{companyId}/payroll-runs/{payrollRunId}/employees/query` → JSON paginado
> · `GET  /api/v1/companies/{companyId}/payroll-runs/{payrollRunId}/employees/export` → xlsx/csv
>
> Un solo query handler y un solo pivote para las dos superficies —es un reporte fiscal, tienen que dar el mismo
> número—, gate `ViewPayrollRuns`, rate-limit de export. Respeta `is_included` y usa `override_amount ?? calculated`.
> La fila de totales viaja en el JSON (calculada sobre TODA la corrida, no sobre la página) y como última fila del
> archivo. Los buckets `otrosIngresos`/`otrosDescuentos` garantizan que
> `ingresoTotal − totalDescuentos = liquidoAPagar` cuadre aunque entre un concepto sin clasificar.
>
> Los años pasados de `generate` y el resto de decisiones quedaron como se acordó. **`generate` de vacaciones no
> tiene nada que ver acá** — eso fue H-28.
>
> ### Verificación
>
> Los tests de taxabilidad **nacieron rojos con el número exacto** (`13.50` esperando `9.00`), y el espejo del bono
> —que sí debe cotizar— pasó desde el principio, así que «arreglarlo» poniendo todo en `false` no habría pasado.
>
> Unitarios **2922/2922** · nómina **40/40** · incapacidades, TNT y tiempo compensatorio **31/31** · ingresos y
> descuentos recurrentes y eventuales **113/113** · build limpio · tres migraciones aplicadas al ambiente.
>
> **En vivo contra la corrida real** (`GENERADA`, 59 empleados, 406 líneas): la matriz devuelve **59 filas** con
> `ingresoTotal 94,061.16`, `totalDescuentos 25,532.98` y `liquidoAPagar 68,528.18` — **idénticos a la cabecera de
> la corrida**. `otrosIngresos` en **0** (el alias funciona), `otrosDescuentos` en **0**, `diasPeriodo` **885** y no
> 900. El CSV dio 60 filas (59 + `TOTAL`), 26 columnas y **los mismos tres totales**, celda por celda.
>
> Un detalle que confirma la semántica de snapshot: **antes** de regenerar, la misma corrida devolvía el salario en
> `otrosIngresos` y 0 días, porque sus 406 líneas se habían generado antes de la migración y no llevaban clase. El
> reporte **no reclasifica hacia atrás** — que es exactamente el punto de H-30.
>
> ### ⚠️ Corrección 2026-08-12 — la verificación de arriba omitía lo que NO se ejercitó
>
> Lo listado es cierto pero incompleto, y por omisión daba a entender que el reporte estaba verificado entero. No
> lo estaba: **de los seis grupos de tests que este plan prometía se entregaron dos.** Medido después, a pedido del
> usuario:
>
> | Columna | Tests que la ejercitaban |
> |---|---|
> | `descuentosExternos` / `descuentosInternos` — las dos de H-30 | **0** |
> | `diasSinGoce`, `diasIncapacidadEmpresa`, `unpaidDays`, `employerPaidDays` — H-31 completo | **0** |
> | `comisiones`, `aguinaldo` | **0** |
>
> **Y el sondeo en vivo no podía cazarlo**: la corrida del ambiente no tiene descuentos externos ni internos ni
> incapacidades, así que esas columnas devolvían 0 — y ese 0 se reportó como señal buena. Es el mecanismo 3 de
> [H-33](#h-33) aplicado a este mismo cierre: una verificación que **no podía fallar**.
>
> #### Lo que se agregó al cerrar el hueco
>
> - `PayrollMatrix_DayBreakdown_SquaresTheEquivalentPaidDays` — siembra un permiso sin goce y una incapacidad con
>   tramo patronal al 75 %, y cuadra las seis columnas de días. **Y corrige el número del hallazgo**: el `13.5`
>   ilustrativo suponía 1 día descontado, pero un día sin goce hace perder el SÉPTIMO (regla ratificada de
>   REQ-011), así que el escenario real da **12.5** = `15 − 2 − 2 + 1.5`. La fórmula es la misma; el ejemplo del
>   hallazgo no contemplaba el séptimo día.
> - `PayrollMatrix_EveryColumn_IsFedByItsOwnConcept` — un ingreso de cada clase y un descuento **Interno** y otro
>   **Externo**. La AFP en **41.33** exacto (7.25 % de 570) prueba al centavo que el aguinaldo y los viáticos
>   quedaron fuera de la base previsional.
> - `PayrollMatrix_ReclassifyingTheConceptAfterGenerating_DoesNotMoveTheReport` — **la razón de existir de H-30**:
>   se reclasifica `PRESTAMO_BANCARIO` en el catálogo después de generar y los $90 no saltan de columna.
> - **Trampa encontrada al escribirlos**: el cuerpo de descuento eventual de los tests venía con
>   `payrollTypeCode = "MENSUAL"` y la corrida es QUINCENAL, así que el pending los filtraba. Era exactamente lo que
>   hacía que las dos columnas de H-30 salieran en cero sin que nada fallara.
> - **`AGUINALDO` corregido**: pasa a `affects_isss = false, affects_afp = false`, la regla que el catálogo de
>   FINIQUITOS ya tenía ratificada para `AGUINALDO_PROPORCIONAL`. Migración `H29AguinaldoTaxability`.
>   ⚠️ Lo que **no** arregla: la exención parcial de Renta (`HastaLimitePorMinimo × 2.00` en el catálogo de
>   finiquitos). El motor de planilla solo tiene un booleano, así que el aguinaldo pagado por planilla tributa
>   completo — sobre-retención, no bajo-retención.
>
>   **Pendiente con diseño ya decidido (usuario, 2026-08-12): opción b, tope ANUAL acumulado.** El tope de 2
>   salarios mínimos es anual, no por periodo: aplicarlo en cada periodo eximiría el doble si el aguinaldo se paga
>   en cuotas, y eso sí es bajo-retención. La forma acordada es que el data provider aporte lo ya exento en el año
>   (corridas `CERRADA`/`AUTORIZADA`) y el motor exima solo el remanente — **la misma maquinaria del arrastre** que
>   el módulo ya tiene para TNT. Piezas: los dos campos en el catálogo de conceptos, el transporte por
>   `PayrollConceptClassification` (ya construido), la consulta del acumulado, el corte por línea en
>   `rentaIncomeBase` (portando `SettlementCalculation.Rules.cs:557` casi literal) y persistir
>   `exempt_amount`/`taxable_amount` en la línea. **El motor ya conoce el salario mínimo del empleado**
>   (`PayrollPopulationRow.MinimumMonthlyWage`), que era la pieza que se temía faltara.
>
> **`horasExtras` — cubierto el 2026-08-12, y mi diagnóstico estaba mal.** Dije que «requiere sembrar la cadena de
> horario y jornada». **El horario es OPCIONAL**: el propio motor documenta que «un día de la semana ausente del
> horario es un día LIBRE, no un dato faltante», así que sin turno toda hora es extra. Lo obligatorio era una
> **plaza real** —la elegibilidad vive en `position_slots.generates_overtime` (H-19), no en la persona— y el
> candidato de planilla sembraba la asignación **sin plaza**. Ése era todo el bloqueo, y se resolvió con un
> parámetro opt-in.
>
> Dos tests: `PayrollMatrix_OvertimeColumn_IsFedByTheAuthorizedRecord` afirma **12.50** exacto (salario 600 →
> diaria 20 → hora 2.50; `2.5 h × factor 2.00 = 5` horas-factor × 2.50), que es el mismo aritmético del golden 2
> del motor; y `Overtime_OnAnExemptPosition_IsRejectedWhoeverHoldsIt` fija la regla de H-19 —una plaza exenta no
> acumula horas extras, sea quien sea el que la ocupe—. Sin el segundo, «horasExtras funciona» no diría nada sobre
> la exención por plaza.
>
> Con esto **las 17 columnas de la matriz tienen cobertura**.
>
> #### Verificación de la corrección
>
> Los cinco tests nuevos nacieron rojos, y dos de ellos por razones que valían la pena: el de días señaló que el
> `13.5` del hallazgo omitía el séptimo, y el de columnas destapó el `payrollTypeCode` desalineado.
>
> Unitarios **2924/2924** · nómina + ingresos/descuentos + incapacidades/TNT + finiquitos/retiros/recontratación
> **194/194** en 19 m 42 s · build limpio · migración `H29AguinaldoTaxability` aplicada.
>
> ### Lo que quedó fuera, a propósito
>
> `endpoint-reference.md` **no se tocó**: el módulo de nómina no está documentado ahí (ni una mención a
> `payroll-runs`), así que agregar solo estos dos endpoints quedaría descolgado. El contrato vive en el swagger y en
> `openapi.yaml`, que sí se actualizaron (2 paths, 3 schemas y los 5 campos nuevos de `PayrollRunLineResponse`;
> diff puramente aditivo).
>
> **`AGUINALDO` quedó con `affects_* = true`**, que es el comportamiento actual. En El Salvador el aguinaldo tiene
> tratamiento fiscal propio (exención parcial de Renta, y el catálogo de finiquitos ya modela
> `exemption_rule`/`exemption_multiplier` para eso). Cambiarlo es una decisión aparte, no un efecto colateral de
> este trabajo — queda anotado.

---

<a id="h-29-original"></a>
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
## H-30 · ✅ RESUELTO 2026-08-12 — ver el cierre de [H-29](#h-29)

> Se entregó junto con H-29, que dependía de él. El detalle de lo construido, las trampas que aparecieron y la
> verificación están en esa nota.

---

<a id="h-30-original"></a>
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
## H-31 · ✅ RESUELTO 2026-08-12 — ver el cierre de [H-29](#h-29)

> Se entregó junto con H-29, que dependía de él. El detalle de lo construido, las trampas que aparecieron y la
> verificación están en esa nota.

---

<a id="h-31-original"></a>
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

<a id="h-33"></a>
## H-33 · ✅ CERRADO 2026-08-12 — el mecanismo 3, que era el que seguía vivo

> El ajuste de práctica del 2026-08-09 cubrió tres de los cuatro mecanismos. **Verifiqué las cuatro instancias
> una por una** antes de tocar nada:
>
> | # | Mecanismo | Estado |
> |---|---|---|
> | 1 | El loop descarta los intermedios | ✅ arreglada — el loop asserta `< 500` en las 10 previas |
> | 2 | Una capa del middleware oculta otra | ✅ arreglada — existe `CompanyUsers_Invite_ShouldReturn201WithResolvableLocation` |
> | 4 | El test fija el defecto como correcto | ✅ desaparecida al arreglar H-01 |
> | 3 | **El fixture vuelve constante la condición bajo prueba** | ❌ **viva, y produciendo instancias nuevas** |
>
> ### La evidencia de que el mecanismo 3 seguía activo son tres hallazgos de esta misma sesión
>
> No los busqué: aparecieron arreglando otras cosas.
>
> - **[H-28](#h-28)** — los tests de vacaciones sembraban `hireDate` y el `startDate` de la plaza con **la misma
>   fecha**, y el defecto era precisamente cuál de las dos manda. Ningún test podía verlo.
> - **[H-28](#h-28)** — los de finiquito, igual.
> - **[H-27](#h-27)** — los de cuentas bancarias nunca sembraban dos cuentas idénticas, así que el duplicado no
>   tenía cómo aparecer.
>
> En los tres el assert existía y estaba bien escrito. Lo que fallaba era el fixture.
>
> ### 🆕 El mecanismo 3 SÍ es parcialmente automatizable — contra lo que decía este hallazgo
>
> El texto original afirmaba que «no lo detecta ningún análisis estático». Es cierto del mecanismo en general,
> **pero la familia de fechas tiene firma mecánica**: un helper de siembra que alimenta dos campos de fecha con la
> misma constante. Y es justo la familia que escondió H-28.
>
> **G5** (`SeedHelpers_MustNotFeedTwoDateFieldsFromOneConstant`) nació rojo señalando **10 helpers** compartidos,
> con más de cien tests colgando de los más usados. El desacople es **opt-in**: parámetros opcionales cuyo default
> es el valor acoplado, así que ningún test existente cambió de comportamiento.
>
> **Y la ironía se repitió por tercera vez.** Al aplicarle al propio G5 la regla del hallazgo:
>
> - su **primera versión veía 7** helpers, no 10: recogía las constantes solo del archivo propio, y la clase de
>   tests es `partial` — los tres helpers de finiquito toman `RetirementHireDate` de otro archivo. Corregido, pasó
>   de 7 a 11.
> - de esos 11, **uno era falso positivo**: `SeedNotWorkedTimeRecordAsync` usa la constante para el inicio del
>   permiso y su `AddDays(4)` para el fin — una fecha corrida, no dos campos que una regla distinga. Afinado para
>   ignorar las derivaciones aritméticas pero **sí** contar las conversiones de tipo
>   (`DateOnly.FromDateTime(X)`), que es exactamente el acoplamiento de H-28. Quedaron **10** reales.
> - verificado en las dos direcciones: reacoplar un helper a propósito lo pone rojo.
>
> ### La instancia 3, y lo que destapó
>
> El seeder creaba **las dos** empresas con el mismo dueño. Ahora `acme-two` es de otro dueño y hay una tercera,
> `acme-three`, que el actor sí posee — porque los tests que **mutan** una empresa propia no primaria (renombrar,
> archivar, reactivar) necesitaban una, y estaban usando `acme-two` sin que nadie notara que pasaban por el camino
> de la propiedad.
>
> **Se pusieron rojos 10 de 99 tests**, todos en `AccountCompanies_*`. Y el más revelador:
>
> > **`AccountCompanies_List_ShouldReturnOwnedCompaniesOnly`** afirmaba `TotalCount == 2` con las dos empresas
> > presentes — porque el actor era dueño de ambas. Un test llamado «solo las propias» que **habría pasado igual si
> > el endpoint devolviera todas las empresas de la base**. Ahora lo que prueba el filtro es la AUSENCIA:
> > `Assert.DoesNotContain(... "Acme Two")`.
>
> Los otros nueve eran de dos familias: los que mutan (ahora apuntan a `acme-three`) y los del **límite de
> empresas**, que archivaban `acme-two` para bajar la cuenta del actor — con la empresa en otras manos, archivarla
> ya no bajaba nada. Ahora archivan la propia. Los 34 quedaron verdes.
>
> ### Lo construido
>
> - **G5** en `IntegrationTestQualityGovernanceTests.cs`, limitado a la familia de fechas por decisión del usuario:
>   abrirlo a cualquier constante repetida (montos, códigos) trae ruido legítimo que habría que excepcionar una por
>   una, y un guardrail con lista larga de excepciones deja de leerse.
> - **Los 10 helpers desacoplados**, con `hireDate`/`plazaStartDate` opcionales.
> - **`AGENTS.md` §5: la QUINTA pregunta** — *«¿el fixture hace que los dos campos que la regla distingue tengan el
>   mismo valor?»*—, con el caso medido de H-28. Las cuatro anteriores cubrían el actor, el assert, las capas y el
>   «¿se pondría rojo?»; ninguna cubría el acoplamiento de datos. Y la pregunta 1 se actualizó: ya no dice que el
>   seeder da al actor las dos empresas, porque dejó de ser cierto.
>
> ### Verificación
>
> **G5 nació rojo** con 10 helpers y se verificó en las dos direcciones (reacoplar uno a propósito lo pone rojo).
> El cambio de dueño puso **10 de 99 tests en rojo** y los 34 de `AccountCompanies` quedaron verdes.
>
> Unitarios **2923/2923** · **regresión de integración completa 910/910 en 1 h 35 m** — la excepción a la regla de
> cortes que este cambio justificaba, porque tocaba el fixture de toda la suite.
>
> ### 🆕 Un rojo preexistente que la regresión destapó, ajeno a H-33
>
> `OptedInControllers_ReturnMarkerTypeOnEveryPutPatchAction` llevaba **rojo desde [H-11](#h-11)**: los tres
> endpoints `Reorder*` de catálogos (`JobCatalogs`, `OccupationalPyramidLevels`,
> `PositionDescriptionCatalogItems`) devolvían un tipo sin `ISupportsAllowedActions`. Corregido acá porque una
> suite verde tiene que significar algo.
>
> Y es H-33 por el otro lado: no un test que pasa sin probar, sino **un guardrail que falla sin que nadie lo lea**.
> Llevaba semanas rojo porque la práctica de correr solo la sección tocada nunca ejecutaba la suite completa. La
> lección operativa: la regresión completa no es solo pre-despliegue, es también lo único que lee los guardarraíles
> transversales.
>
> ### Y un error propio, para que quede escrito
>
> A mitad de la primera corrida completa **compilé con la corrida viva** —lo que mi propia nota advierte que da
> fallos irreproducibles— y encima esa corrida usaba el binario de antes del arreglo, así que su resultado no
> validaba nada. Hubo que matarla, limpiar 7 bases de test huérfanas acumuladas y reiniciar de cero. Los 1 h 35 m
> de la corrida buena son con el código final.
>
> ### 🆕 Ampliación 2026-08-12 — el mecanismo 2 TAMBIÉN era automatizable
>
> El usuario leyó entre líneas que este cierre dejaba problemas repetibles, y tenía razón: de los dos mecanismos
> que quedaban como práctica, **uno no debía quedarse ahí**.
>
> **G6 (`NoEndpoint_MayHaveOnlyErrorAssertionsAsItsCoverage`)** — un `(ruta, verbo)` no puede tener como única
> cobertura aserciones de error. Es exactamente el caso del invite: su único test afirmaba el `429` del rate
> limiter, y el `500` vivió semanas detrás de esa cobertura. G2 cubría el caso particular del loop; G6 generaliza.
>
> Nació señalando **12 endpoints**, y la lista se ingresó **separando diseño de deuda**, que no es lo mismo:
> `PATCH /position-slots/{id}/occupancy` es solo-error **por diseño** —H-23 eliminó la superficie y su test afirma
> que está ida—, y los otros once son **deuda heredada** con la que G6 entra en vigor, para que ninguno nuevo se
> sume. Decirlo así importa: una lista de excepciones que no distingue las dos cosas convierte la deuda en
> «comportamiento esperado».
>
> **Y G6 nació defectuoso tres veces antes de servir** —el patrón ya conocido de este hallazgo—:
> 1. escaneando solo cuerpos de test acusaba 26, la mayoría falsos positivos porque la aserción de éxito vive en un
>    helper (mismo tropiezo que G2 documentó);
> 2. al reemplazar el extractor por uno más ancho saltó de 16 a **73**, porque perdió las rutas alcanzadas vía
>    `HttpRequestMessage` —casi todos los PATCH—: había que **unir** los dos extractores, no sustituirlos;
> 3. y no veía los envoltorios genéricos (`PostLeaveMasterAsync<T>(`) por los argumentos de tipo.
>
> ### Lo que queda como práctica, a propósito
>
> El mecanismo 4 —un assert sobre el comportamiento actual en vez del correcto— **no tiene firma mecánica**: exige
> saber cuál *es* el comportamiento correcto, y eso ninguna regla estática lo sabe. Se queda en las preguntas, y ahí
> el cierre es honesto. Este hallazgo se cierra porque el mecanismo que faltaba ya tiene
> guardrail, no porque el problema sea imposible de repetir: se repite el día que alguien escriba un fixture que
> no puede fallar. Para eso están las cinco preguntas.

---

<a id="h-33-original"></a>
## H-33 · 🔴 Transversal — la cobertura de integración ejercita el endpoint sin probar la condición que importa

Levantado el 2026-08-09, después de arreglar H-01, H-02 y H-04. **No es un defecto de código: es una
propiedad de cómo están escritos algunos tests**, y es la razón por la que tres 🔴/🟠 —incluido un `500`
permanente— convivieron con una suite verde.

### Las cuatro instancias, y sus cuatro mecanismos distintos

| # | Test | Mecanismo |
|---|---|---|
| 1 | `CompanyUsers_Invite_ShouldRateLimit` | **el loop descarta los intermedios.** 11 llamadas, corta al `429` y solo asserta el `429`. Las 10 previas devolvían `500` y nadie las miraba. El endpoint se ejercitaba 11 veces por corrida sin verificarse jamás. |
| 2 | el mismo test | **el orden del middleware oculta una capa.** `[EnableRateLimiting]` corre *antes* del authorization del endpoint, así que llegar al limitador no prueba nada sobre permisos. Se descubrió al escribir el happy-path real: el rol del actor sembrado **no puede invitar** y nadie lo había visto. |
| 3 | `AccountCompanies_Switch_ShouldReturnTokenWithSelectedTenant` | **el fixture vuelve constante la condición bajo prueba.** Hace switch a una segunda empresa con membresía no primaria y espera éxito — pero el seeder crea **las dos** empresas con el mismo dueño (`IntegrationTestSeeder.cs:33-34`), así que el camino de no-propietario nunca corre. |
| 4 | `JobProfiles_UpdatePublishedProfile_ShouldAllowEditingPositionCategory` | **el test fija el defecto como comportamiento correcto.** Afirmaba que un perfil publicado *es* editable, que era exactamente el defecto de H-01. Un test así no es un hueco: es un candado. |

Un quinto caso casi ocurrió: al planear el gate de la matriz (H-01) reusar el código de error existente
habría puesto en verde `CompetencyFramework_MatrixUpdate_ItemWithMismatchedConducts_ShouldReturn409`
**por la razón equivocada** — el gate nuevo habría disparado primero y el test habría dejado de probar el
desajuste de conductas. Se evitó usando un código propio.

### Lo que un barrido mecánico NO encuentra — verificado

Barrí los **753** métodos de test de integración buscando los olores automatizables:

| Olor | Encontrados |
|---|---|
| Tests sin ninguna aserción | **0 reales** (7 candidatos, todos falsos positivos: `InitializeAsync` de las factories, y aserciones que viven en helpers como `AssertSingleActivePrimaryAsync`) |
| Llamadas cuya respuesta se descarta | **1**, introducida por mí en `JobProfiles_Publish_WithAdminButWithoutPublishPermission_ShouldReturn403` |

**La suite es disciplinada en tener aserciones.** Ninguno de los cuatro casos es "falta un assert" — en los
cuatro el assert existe y está **sobre la cosa equivocada**, o sobre un escenario donde la condición bajo
prueba no puede variar. Eso no lo detecta ningún análisis estático: requiere un cambio de práctica.

### Por qué es 🔴

Porque degrada la señal de la suite entera. Un `500` permanente en `POST /company/users` sobrevivió
semanas con **cobertura de integración que golpeaba ese endpoint 11 veces por corrida**. Mientras el patrón
siga, "la suite está verde" no es evidencia de que algo funcione — y eso vale más que cualquiera de los
ítems individuales que quedan abiertos.

### La práctica que sí los habría cazado a todos

**Correr el test nuevo contra el código sin arreglar y confirmar que falla.** Se aplicó en H-01, H-02 y
H-04 y encontró algo real cada vez:

- en H-02 el guardrail nació en rojo señalando exactamente 1 de 91 call sites;
- en H-04 los dos tests nuevos fallaron con el `403` de propiedad, probando que el escenario sí montaba un
  no-propietario;
- y en H-02 la primera versión de mi propio barrido reportaba **88 falsos positivos** por no modelar los
  *ambient values* de ASP.NET — un guardrail que habría sido desactivado el primer día.

Un guardrail que nace verde no prueba nada. Es la misma trampa que este hallazgo describe, aplicada a los
guardarraíles.

> **✅ Ajuste ejecutado el 2026-08-09** — `plan-ajuste-practica-pruebas.md`.
>
> `AGENTS.md` §5 Paso 5 lleva la regla del rojo-antes-de-verde y las 4 preguntas. Los tres guardarraíles
> automatizables viven en `IntegrationTestQualityGovernanceTests.cs`. Unitarios **2870/2870**.
>
> **Y la ironía que confirma el hallazgo:** al aplicarles la propia regla, dos de los tres guardarraíles
> resultaron defectuosos. **G2 nacía en verde y no habría cazado H-02** — su clave era la ruta sin el verbo,
> así que `GET /company/users` (el listado) "cubría" el `POST` del invite. Y daba un falso positivo por leer
> solo cuerpos de `public async Task`, ignorando los helpers privados. Ambos corregidos, y los tres
> verificados rompiendo a propósito lo que deben detectar.

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

---

<a id="h-34"></a>
## H-34 · ✅ RESUELTO 2026-08-11 · el diagnóstico era medio cierto, y eso lo empeoraba

> **«Nunca cambia» vale para 24 de los 53 endpoints. En los otros 29 sí cambia — y ese es el problema.**
>
> - **Perfiles de puesto (10 `DELETE`)**: `JobProfile` solo rota su token en `UpdateCore`, `Publish`, `Reopen` y
>   `Archive`. Ninguna escritura de colección hija lo toca. Aquí el hallazgo acertaba.
> - **Expediente (43 `DELETE`)**: **29 lo rotan** —llaman `TouchPersonnelFile`, que pasa por
>   `UpdatePersonalInfo` → `RefreshConcurrencyToken()`— y **14 no** (educación, idiomas, direcciones,
>   identificaciones, documentos, contactos de emergencia, referencias, capacitaciones, empleos anteriores…).
>
> O sea: **el mismo campo, en la misma forma de respuesta, significaba dos cosas según qué módulo contestara**. Un
> valor en el que se puede confiar la mitad de las veces, sin forma de saber cuál mitad. Peor que uno inútil.
>
> **Y el swagger lo prometía con todas las letras:** *«Returns the parent job profile's **updated** concurrency
> token so the caller can continue mutating the profile without an extra round-trip»* — falso en esos 10.
> El token viajaba además en el **`ETag`** de la respuesta del `DELETE`, no solo en el cuerpo.
>
> **Nadie lo verificaba.** Los 18 usos en tests solo comprobaban `NotEqual(Guid.Empty)`; y **10 unitarios
> afirmaban explícitamente que NO cambiaba** (`Assert.Equal(profile.ConcurrencyToken, …ParentConcurrencyToken)`).
> Por eso sobrevivió.
>
> ### Lo construido (opción A, decisión del usuario)
>
> Los **53 `DELETE` responden `204 No Content`**, sin cuerpo y sin `ETag`. Los dos records de resultado
> (`JobProfileParentConcurrencyResult`, `PersonnelFileParentConcurrencyResult`) se reemplazaron por un
> `ChildDeletionResult` vacío —existe solo porque el despachador es genérico sobre un tipo de respuesta— y los
> controladores usan un helper nuevo, `ToNoContentResult`.
>
> La concurrencia queda declarada donde ya vivía: **por hijo**. El `If-Match` del `DELETE` lleva el token **del
> hijo**, nunca el del padre — que es justo el malentendido que [H-27](#h-27) documenta en cuentas bancarias
> («mandar el token del padre da `409 CONCURRENCY_CONFLICT`, que suena a "alguien más lo modificó"»). Sin el campo
> en la respuesta, esa confusión es difícil de cometer.
>
> **`TouchPersonnelFile` NO se tocó**: mueve el `modifiedAtUtc` del expediente, que alimenta el indicador de
> **frescura** del tablero (D-08). Un test de no-regresión lo fija, porque era la pieza fácil de arrastrar por
> error al quitar el campo.
>
> ### Verificación
>
> 3 tests de integración nacieron rojos y quedaron verdes (un `DELETE` de cada familia + la frescura); los 10
> unitarios que fijaban el token constante se reemplazaron; openapi pasó **53 respuestas `200`→`204`** y perdió
> los dos esquemas (557 paths y YAML válido tras el cambio).

## H-34 · 🟡 El `parentConcurrencyToken` que devuelve el DELETE de un hijo nunca cambia

Detectado el 2026-08-09 al analizar [H-09](#h-09), **no cerrado**.

`DELETE /api/v1/job-profiles/{jobProfilePublicId}/requirements/{requirementPublicId}` responde `200 OK` con
`{ parentConcurrencyToken }`, y sus ocho hermanos hacen lo mismo. Ese valor es el `ConcurrencyToken` del
perfil — que **ninguna escritura hija rota**: los `Add*`/`Remove*` del agregado no llaman
`RefreshConcurrencyToken()`, y tras H-09 tampoco tocan el `Version`.

O sea que el cliente recibe, en cada borrado, el mismo token que ya tenía. Devolver un token constante es el
vestigio de una intención que no se implementó: si el token del padre nunca se mueve, el campo no informa nada
y el nombre promete un refresco que no ocurre.

Las dos salidas coherentes son opuestas y hay que elegir:

- **quitar el campo** de las nueve respuestas de `DELETE`, asumiendo que la concurrencia del módulo es
  granular por hijo (que es como está construida) y el padre no participa
- **rotar el token del padre** en cada escritura hija, con lo que el campo empieza a significar algo — al
  precio de que quien tenga el token del padre para un `PUT` posterior reciba `409` cuando alguien toque un
  hijo

Se dejó abierto a propósito: es una decisión de contrato con el frontend, no un bug con arreglo obvio.
