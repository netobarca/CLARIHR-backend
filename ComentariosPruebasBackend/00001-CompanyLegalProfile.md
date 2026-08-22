# 00001 — CompanyLegalProfile · Hallazgos de backend

| | |
|---|---|
| **ID** | 00001-CompanyLegalProfile |
| **Documento espejo** | [`ComentariosPruebasFrontend/00001-CompanyLegalProfile`](../ComentariosPruebasFrontend/00001-CompanyLegalProfile.md) |
| **Paso probado** | Configuración guiada (`/setup`) → **Paso 1: Perfil legal** |
| **Pantalla que los destapó** | `/personnel-files/company-legal-profile` |
| **Fecha** | 2026-08-14 |
| **Ambiente** | Producción — `https://dashboard.clarihr.com` |

Hallazgos de **backend** detectados durante la prueba del Paso 1. El comportamiento del cliente y su ajuste están en el documento espejo; aquí va solo lo que se resuelve del lado del servidor.

---

## 1. Resumen

| ID | Sev. | Hallazgo | Componente | Origen | Alcance | Estado |
|---|---|---|---|---|---|---|
| [B-01](#2-b-01--el-recurso-de-perfil-legal-no-expone-allowedactions) | 🟡 Media | El recurso no expone `allowedActions` | API | F-01 | **51 de 90 controllers** (medido) | 🟢 **Resuelto** aquí · resto congelado |
| [B-02](#3-b-02--el-400-de-validación-agrupa-todos-los-mensajes-bajo-la-clave-vacía) | 🟡 Media | El `400` de validación agrupa todos los mensajes bajo la clave `""` | Application | §2.5, F-02, F-04 | **Un solo archivo** (medido — NO transversal) | 🟢 **Resuelto** |
| [B-03](#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location) | 🟡 Media | El proxy descarta cabeceras del upstream: `ETag` y `Location` | BFF / proxy | F-09 · 00002 §5.2 | **Transversal (confirmado)** | ⏸️ **Bloqueado** — fuera de este repo |

**Ninguno bloquea al frontend**: los tres tenían vía alterna vigente, documentada en su ficha.

> **Estado al 2026-08-16.** **B-01 y B-02 resueltos y verificados.** **B-03 pasa a ⏸️ Bloqueado**: el proxy no vive en este repositorio (`src/`, `tests/`, `docs/`) — el arreglo es una línea de configuración de Express y pertenece a quien mantiene el BFF. Dejarlo como 🔲 Propuesto lo hacía parecer trabajo pendiente del backend, y no lo es.
>
> Suites: **unit 2970/2970** · **integración dirigida 84/84** · build sin warnings con `TreatWarningsAsErrors` · `openapi.yaml` actualizado y válido.

---

## 2. B-01 — El recurso de perfil legal no expone `allowedActions`

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** en esta pantalla — 2026-08-16 (§2.8) · resto **congelado** (§2.9) |
| **Componente** | API · `CompanyLegalProfilesController` / `CompanyLegalProfileResponse` |
| **Origen** | Hallazgo **F-01** del documento espejo |
| **Alcance** | **51 de 90 controllers gobernados** — medido (§2.9) |

### 2.1 Evidencia

`GET /v1/legal-profile` devuelve 10 propiedades y **ninguna es `allowedActions`**:

```jsonc
{
  "publicId": "…", "legalName": "…", "employerNitNumber": "…",
  "isssEmployerRegistrationNumber": "…", "fiscalAddress": "…",
  "economicActivityDescription": null, "legalRepresentativePublicId": "…",
  "concurrencyToken": "…", "createdAtUtc": "…", "modifiedAtUtc": "…"
}
```

En código:

- `CompanyLegalProfilesController` **no** lleva `[ResourceActions]`.
- `CompanyLegalProfileResponse` **no** implementa `ISupportsAllowedActions`.

### 2.2 Causa

`AllowedActionsResultFilter` solo enriquece respuestas de controllers decorados con `ResourceActionsAttribute`, y solo si el DTO implementa `ISupportsAllowedActions`. Este recurso no cumple ninguna de las dos condiciones, así que el filtro lo ignora.

### 2.3 Impacto

El frontend **no tiene forma de saber si el usuario puede escribir** sin hardcodear códigos de permiso. Consecuencias medidas:

- Hoy muestra *Guardar cambios* habilitado a quien solo tiene `CompanyPreferences.Read`; ese usuario recibirá `403` al guardar.
- Obliga a que el guard de la ruta replique en el cliente la lógica de `CompanyPreferenceAuthorizationService` — que es justo donde ya se equivocó (**F-01**: el guard pide `PersonnelFiles.Read`).

La alternativa existente **no sirve** para este caso: `GET /account/companies/{id}/authorization/resource-policies/{resourceKey}` devuelve exactamente la política del usuario, pero su autorización es **por propiedad de la empresa** (el llamante debe ser el creador). Un administrador legítimo que no la creó recibe `403`/`404`.

### 2.4 Propuesta

Dos cambios pequeños, ambos aditivos:

1. Decorar el controller con `[ResourceActions(CompanyPreferencePermissionCodes.ResourceKey)]` → `"COMPANY_PREFERENCES"`.
2. Agregar `AllowedActionsResponse? AllowedActions = null` como último miembro posicional de `CompanyLegalProfileResponse` e implementar `: ISupportsAllowedActions`.

La documentación de la interfaz describe el segundo paso como *«usually a one-token change»*. `AllowedActionsResultFilter` hace el resto: **las respuestas de objeto único se enriquecen siempre** (el `?includeAllowedActions=true` solo aplica a listas paginadas).

Resultado esperado del `GET`:

```jsonc
{
  "…": "…",
  "allowedActions": { "canView": true, "canCreate": false, "canEdit": true, "canDelete": false, "reasons": [] }
}
```

### 2.5 Compatibilidad

**No rompe el contrato.** `AllowedActions` es un miembro adicional con default `null`; los clientes actuales lo ignoran. **Requiere regenerar `openapi.yaml`.**

### 2.6 Alcance a revisar

Vale la pena listar qué otros controllers carecen de `[ResourceActions]` teniendo escrituras gobernadas por RBAC. Si son varios, conviene resolverlos en un mismo pase en vez de uno por pantalla conforme se vayan probando.

### 2.7 Vía alterna vigente

El frontend gobierna guard y botón replicando los permisos a mano:

```
Entrar    → CompanyPreferences.Read | CompanyPreferences.Admin | iam.administration.manage
Guardar   → CompanyPreferences.Admin | iam.administration.manage
```

Funciona, pero es frágil: cualquier cambio en la autorización del backend hay que replicarlo en el cliente sin que nada lo detecte.

### 2.8 Lo que se hizo — y el paso que el hallazgo no vio

La propuesta decía «dos cambios pequeños». **Son tres**, y el tercero es el que se olvida:

| # | Cambio | ¿Estaba en §2.4? |
|---|---|---|
| 1 | `[ResourceActions(CompanyPreferencePermissionCodes.ResourceKey)]` en el controller | ✅ Sí |
| 2 | `CompanyLegalProfileResponse : ISupportsAllowedActions` + el miembro | ✅ Sí |
| 3 | **Registrar la clave en `AllowedActionsRegistry`** | ❌ **No** |

Sin el tercero, `AllowedActionsResultFilter` es **fail-closed**: `AllowedActionsResolver.Resolve` devuelve `null` para una clave no registrada y el campo sale `null`, aunque el atributo y la interfaz estén puestos. Se detectó porque el rojo pasó de «falta la clave» a «la clave existe pero es `null`» — un test que solo comprobara la presencia del campo lo habría dado por bueno.

Registrado como `Policy(...)` con `supportsActivate: false, supportsInactivate: false`: el perfil legal no tiene ciclo de vida, solo ver / crear / editar.

**Verificación.** `CompanyLegalProfile_Get_ShouldExposeAllowedActionsPerPermission` comprueba los **dos** perfiles: el administrador recibe `canEdit: true`, el lector `canEdit: false`. Un `canEdit` que saliera `true` para todos sería tan inútil como no tenerlo.

**No rompe el contrato** — miembro aditivo con default `null`. `openapi.yaml` actualizado.

### 2.9 Alcance medido — y por qué el resto se congela

§2.6 pedía listar qué otros controllers carecen del atributo. Medido por reflexión: **51 de los 90** controllers gobernados con escrituras.

| Familia | Controllers |
|---|---|
| Expediente de personal | 23 |
| Planilla y compensación | 17 |
| Retiro y salida | 5 |
| Solicitudes del empleado | 3 |
| Ausencias y salud | 3 |

> ⚠️ **Ese número costó tres intentos, y los dos primeros fueron míos.** Un `grep` de `AuthorizationPolicySet` dio **57**: contaba también los controllers donde la cadena aparece en un comentario que explica por qué **no** lo llevan — el patrón que el propio `00000` elogia en `AccountCompaniesController`. Leer la lista desde la salida de xUnit dio **5**: venía truncada con `···`. El número bueno sale de reflexión más un `Assert` que imprime la lista completa, que es por lo que el guardrail no usa `Assert.Empty`.

**Decisión: congelar, no drenar.** Saldar uno son los tres pasos de §2.8, y el registro necesita su terna de permisos; equivocarla le diría al frontend «puedes editar» a quien no puede — el defecto que esto existe para evitar, cometido al arreglarlo. Las pantallas de esos 51 no se han ejercitado aún en la corrida de QA, así que no habría con qué comprobarlo.

`ResourceActionsCoverageGuardrailsTests` congela el inventario: **ningún controller nuevo puede nacer sin declarar su recurso**, y un segundo test impide que la lista se quede con entradas muertas. Convierte deuda que crece en deuda que solo baja. **Se drenan por módulo cuando toque cada pantalla**, con un test que pruebe que `canEdit` distingue perfiles.

### 2.10 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-14 | 🔲 Propuesto | Detectado al probar el Paso 1 de la configuración guiada |
| 2026-08-16 | 🟢 Resuelto | Arreglado en esta pantalla (§2.8). **El hallazgo decía dos pasos y son tres**: sin registrar la clave, el filtro es fail-closed y el campo sale `null`. Alcance medido: **51 de 90**, congelados con guardrail (§2.9) |

---

## 3. B-02 — El `400` de validación agrupa todos los mensajes bajo la clave vacía

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-16 (§3.8) |
| **Componente** | Application · validadores FluentValidation |
| **Origen** | §2.5 del documento espejo, hallazgos **F-02** y **F-04** |
| **Alcance** | **Un solo archivo** — la sospecha de transversal quedó descartada (§3.6) |

### 3.1 Evidencia

Respuesta `400` del perfil legal (verificada con `Accept-Language: es`):

```jsonc
{
  "status": 400,
  "code": "common.validation",
  "detail": "Se encontraron uno o mas errores de validacion.",
  "errors": {
    "": [                                    // ← clave VACÍA
      "El NIT patronal debe seguir el formato ####-######-###-#.",
      "El número de registro patronal del ISSS solo acepta dígitos y guiones."
    ]
  }
}
```

Los mensajes son correctos y están bien traducidos. El problema es **la clave**.

### 3.2 Causa

`CompanyLegalProfileCommandValidatorBase.ApplySharedRules` recibe **accesores lambda** (`Func<TCommand, string>`) y los usa así:

```csharp
RuleFor(command => legalName(command)).NotEmpty().MaximumLength(200);
```

FluentValidation deriva el nombre de la propiedad analizando la expresión del `RuleFor`. Con una lambda *invocada* no hay propiedad que derivar, así que emite la clave vacía. El patrón se usó para compartir las reglas entre `CreateCompanyLegalProfileCommand` y `UpdateCompanyLegalProfileCommand`.

### 3.3 Impacto

El cliente **no puede señalar qué campo falló**: le llega una lista de textos sin decir a qué input pertenecen. Obliga a pintar un bloque de errores a nivel de formulario.

Se agrava al combinarse con **F-02** del documento espejo (el cliente acepta espacios en blanco donde el servidor los rechaza): el usuario recibe un error genérico sobre un campo que «se ve lleno». Es la peor ruta de fallo posible — el usuario no puede diagnosticar qué corregir.

### 3.4 Propuesta

Que `errors` venga con las claves reales: `legalName`, `employerNitNumber`, `isssEmployerRegistrationNumber`, `fiscalAddress`, `economicActivityDescription`.

Dos caminos:

| Opción | Cómo | Nota |
|---|---|---|
| **A (recomendada)** | Declarar las reglas por comando con expresiones directas: `RuleFor(c => c.LegalName)` | Pierde el método compartido, gana claridad y el nombre sale solo |
| **B** | Conservar `ApplySharedRules` y añadir `.WithName("legalName")` a cada regla | Menos código tocado, pero el nombre queda escrito a mano y puede desincronizarse |

Se recomienda **A**: son dos comandos con los mismos 5 campos; la duplicación es menor que el costo de mantener nombres literales.

### 3.5 Compatibilidad

**Cambia la forma del `400`.** Hoy nadie puede depender de la clave `""` para nada útil, así que el riesgo es bajo — pero si algún cliente lee `errors[""]` a ciegas, dejará de encontrarlo. Conviene avisar al frontend antes de desplegarlo.

No afecta `openapi.yaml` (la forma de `ProblemDetails` no cambia, solo el contenido de `errors`).

### 3.6 Alcance a revisar

**Antes de decidir, medir.** El patrón `ApplySharedRules(Func<TCommand, …>)` puede estar replicado en otros módulos que también comparten validaciones entre `Create` y `Update`. Si aparece en varios features, el arreglo y el beneficio se multiplican, y conviene resolverlo de una vez con un criterio único en vez de pantalla por pantalla.

**Medición parcial — 2026-08-14, corrida del Paso 2** ([`00002-UnitTypes`](00002-UnitTypes.md)): los validadores de `OrgStructureCatalogs` usan **expresiones directas** (`RuleFor(c => c.Code)`) y **sí devuelven la clave del campo** en camelCase:

```jsonc
"errors": { "code": ["…"], "name": ["…"], "sortOrder": ["…"] }
```

Conclusión: **el defecto no es general del backend.** Es específico del patrón de accesores lambda compartidos, no de la forma en que el producto construye sus `400`. Eso reduce el alcance estimado, pero **no lo cierra**: falta contar cuántos features usan el patrón `ApplySharedRules(Func<…>)`. Las corridas siguientes lo irán acotando.

### 3.7 Vía alterna vigente

El frontend muestra los mensajes como lista a nivel de formulario, sin resaltar el campo. Funciona, pero degrada el diagnóstico del usuario.

### 3.8 Alcance cerrado, y lo que se hizo

§3.6 pedía contar cuántos features usan el patrón. **Medido: uno.**

```
ApplySharedRules(Func<…>)                → CompanyLegalProfileAdministration.cs (único)
RuleFor con lambda invocada              → 6 reglas, todas ahí
```

La sospecha de transversalidad queda **descartada**: el defecto era del patrón de accesores compartidos, no de cómo el producto construye sus `400`.

**Se aplicó la opción A** (expresiones directas por comando). Se descartó B —`.WithName("legalName")`— porque un nombre escrito como literal se desincroniza en silencio el día que la propiedad se renombre; con la expresión directa el nombre lo da el compilador.

Las dos reglas con mensaje propio (NIT e ISSS) se conservan compartidas mediante un helper que recibe una **`Expression<Func<…>>`**, no un `Func<…>`: FluentValidation sigue viendo la expresión y derivando el nombre. Así no se duplica la regla de formato y aun así la clave sale correcta.

> **Detalle que el hallazgo no menciona:** `companyId` también estaba en las reglas compartidas, y es un **parámetro de ruta**, no un campo del cuerpo. Ahora emite su propia clave en vez de caer en `""`.

**Verificación.** `CompanyLegalProfile_WhenValidationFails_ShouldKeyErrorsByField` manda **dos** campos malformados a la vez a propósito: con uno solo, un `errors` de una sola entrada pasaría igual aunque la clave siguiera siendo la vacía. Rojo verificado antes del arreglo.

**No afecta `openapi.yaml`** — la forma de `ProblemDetails` no cambia, solo el contenido de `errors`.

### 3.9 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-14 | 🔲 Propuesto | Detectado al probar el Paso 1 de la configuración guiada |
| 2026-08-14 | 🔲 Propuesto | Alcance acotado en la corrida del Paso 2: `OrgStructureCatalogs` sí devuelve claves de campo → el defecto es del patrón de accesores compartidos, no del backend en general (§3.6) |
| 2026-08-16 | 🟢 Resuelto | **Alcance cerrado: un solo archivo.** Aplicada la opción A; las reglas con mensaje propio se comparten vía `Expression<Func<…>>`, que sí conserva el nombre (§3.8) |

---

## 4. B-03 — El proxy descarta cabeceras del upstream (`ETag` y `Location`)

| | |
|---|---|
| **Severidad** | 🟡 Media *(subió de Baja tras confirmarse el alcance en el Paso 2)* |
| **Estado** | ⏸️ **Bloqueado** — fuera de este repositorio (§4.9) |
| **Componente** | BFF / proxy del mismo origen |
| **Dueño** | **Quien mantiene el BFF** — no es deuda del backend · índice accionable en [00900 / ProxyBFF](../ComentariosPruebasFrontend/00900-ProxyBFF.md) |
| **Origen** | Hallazgo **F-09** del documento espejo |
| **Alcance** | **Transversal — confirmado en 2 módulos y 2 cabeceras.** Afecta a todo recurso con concurrencia y a todo `201` con `Location` |

### 4.1 Evidencia

Medido en vivo sobre `GET /v1/legal-profile`:

```
ETag que llega al navegador  = W/"1d3-+0jcVFtouHBHD3qEZfrvRbad0/A"   ← weak, calculado sobre el cuerpo
concurrencyToken del cuerpo  = 32bffe02-ad74-4521-89ee-f32b7495838b  ← el que exige If-Match
```

**Ampliación medida en el Paso 2** — en los `POST` que devuelven `201` al crear tipos de unidad:

```
Location : (ausente)
ETag     : W/"142-f/6gfZ4AQQkBwkEX8IFPPeN859M"
```

El backend emite ambas (`ToCreatedAtActionResult`) y el swagger las documenta. **Ninguna llega al navegador.**

### 4.2 Causa

La API emite un `ETag` **fuerte** con el token de concurrencia (`ToActionResultWithETag` / `ETagHeader.Format`). El proxy del mismo origen **recalcula la cabecera sobre el cuerpo de la respuesta y pisa la del upstream**, que es el comportamiento por defecto de Express/Node.

### 4.3 Impacto

La cabecera `ETag` queda **inservible para concurrencia en todo el producto**, no solo en esta pantalla:

- Cualquier cliente que siga la convención HTTP estándar (leer `ETag` → mandar `If-Match`) fallará con `400` (formato inválido, no es un GUID) o `409`.
- Obliga a documentar en **cada pantalla** la excepción «usar el token del cuerpo, no la cabecera». Es deuda que se paga una y otra vez.
- El trabajo del backend para emitir un ETag correcto se desperdicia.

No hay síntoma en producción hoy porque el frontend ya usa el token del cuerpo — pero es una trampa esperando a quien «optimice» leyendo la cabecera.

### 4.4 Propuesta

Configurar el proxy para **preservar la cabecera `ETag` del upstream** en lugar de generar la suya. En Express, desactivar el ETag propio en las rutas proxied (`app.set('etag', false)` o equivalente en el middleware de proxy) y reenviar la cabecera tal cual llega.

### 4.5 Compatibilidad

**No rompe nada.** El frontend hoy no usa la cabecera para concurrencia; pasaría a ser utilizable. Ojo con el caché HTTP: si algo depende del ETag débil actual para revalidación condicional, hay que verificarlo antes.

### 4.6 Alcance a revisar

Confirmar si el proxy pisa **otras** cabeceras del upstream (`Location`, `Content-Disposition` en descargas, cabeceras de rate limit). Si es un patrón general de reescritura, conviene revisarlo completo en vez de solo el `ETag`.

**Medido — 2026-08-14, corrida del Paso 2** ([`00002-UnitTypes`](../ComentariosPruebasFrontend/00002-UnitTypes.md#52-lo-que-reveló-la-escritura-dos-cabeceras-que-no-llegan)): en los nueve `POST` que devolvieron `201` al crear tipos de unidad, **la cabecera `Location` tampoco llega al navegador**, y el `ETag` volvió a ser el weak del proxy:

```
Location : (ausente)
ETag     : W/"142-f/6gfZ4AQQkBwkEX8IFPPeN859M"
```

El backend emite las dos (`ToCreatedAtActionResult`) y el swagger las documenta.

**Conclusión: no es un problema del `ETag`, es del proxy descartando cabeceras del upstream.** Confirmado en dos módulos independientes (Compliance y OrgStructureCatalogs) y en dos cabeceras distintas. Eso **sube la prioridad** de este hallazgo: ya no es una trampa aislada de una pantalla.

**Medido — 2026-08-15, corrida del Paso 3** ([`00003-OrgUnits`](00003-OrgUnits.md)): `Content-Disposition` **también se descarta**, en los tres formatos de export de unidades organizativas.

**Pero el síntoma visible no se materializa**, y eso matiza el impacto. Se interceptaron `URL.createObjectURL` y `HTMLAnchorElement.click` para capturar lo que la interfaz hace al pulsar *Export CSV*, sin descargar el archivo:

```
createObjectURL  →  mime: text/csv · 8114 bytes
a.click          →  download: "org-units.csv"
```

**El frontend fija el nombre por su cuenta.** No depende de la cabecera.

**Conclusión revisada:** tres cabeceras descartadas —`ETag`, `Location`, `Content-Disposition`— en tres módulos independientes. **Ninguna rompe al cliente actual**, porque el frontend no consume ninguna de las tres: toma el token del cuerpo, refresca la lista en vez de seguir `Location`, y nombra el archivo él mismo.

El riesgo es **latente, no actual**: cualquier otro consumidor —un integrador, un enlace directo, un cliente que siga la convención HTTP estándar— sí se rompería. Eso justifica arreglarlo, pero **no con la urgencia de un defecto que esté fallando hoy**.

### 4.7 Vía alterna vigente

Regla operativa documentada en cada pantalla: **construir el `If-Match` siempre con el `concurrencyToken` del cuerpo**, nunca con la cabecera `ETag`.

```http
PUT /v1/legal-profile
If-Match: "32bffe02-ad74-4521-89ee-f32b7495838b"
```

### 4.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-14 | 🔲 Propuesto | Detectado al probar el Paso 1 de la configuración guiada |
| 2026-08-14 | 🔲 Propuesto | **Alcance confirmado y ampliado** en la corrida del Paso 2: el proxy también descarta `Location` en el `201`. Transversal probado en dos módulos y dos cabeceras (§4.6) |
| 2026-08-15 | 🔲 Propuesto | **Alcance cerrado** en el Paso 3: `Content-Disposition` también se descarta, pero el frontend fija el nombre de archivo por su cuenta. Tres cabeceras, tres módulos, **impacto latente y no actual** (§4.6) |
| 2026-08-16 | ⏸️ Bloqueado | **No es trabajo de este repositorio** (§4.9). Se reclasifica para que no cuente como deuda pendiente del backend |

### 4.9 Por qué queda bloqueado y no propuesto

El repositorio contiene `src/`, `tests/` y `docs/` — **el proxy no está aquí**. El backend ya emite las tres cabeceras correctamente (`ToActionResultWithETag`, `ToCreatedAtActionResult`), y el swagger las documenta: no hay nada que corregir de este lado.

El arreglo —desactivar el ETag propio de Express en las rutas *proxied* y reenviar las cabeceras del upstream— pertenece a **quien mantiene el BFF**. Mantenerlo como 🔲 Propuesto lo hacía figurar como deuda del backend y distorsionaba el conteo de lo que queda por hacer aquí.

Se mantiene **abierto**, no descartado: el propio §4.6 concluyó que el impacto es **latente pero real** para cualquier consumidor que no sea el frontend actual.
