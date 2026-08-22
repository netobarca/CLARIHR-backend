# 00001 — CompanyLegalProfile · Perfil legal de la empresa

| | |
|---|---|
| **ID** | 00001-CompanyLegalProfile |
| **Paso probado** | Configuración guiada (`/setup`) → **Paso 1: Perfil legal** |
| **Pantalla** | `/personnel-files/company-legal-profile` |
| **Fecha** | 2026-08-14 |
| **Ambiente** | Producción — `https://dashboard.clarihr.com` |
| **Empresa de prueba** | `End to End SAS` (perfil legal ya configurado) |
| **Usuario** | christopher canas (OWNER) |
| **Objetivo** | Verificar que la pantalla expone **todo** lo que la BD y el endpoint requieren, tanto lo obligatorio como lo opcional |

---

## 1. Resumen ejecutivo

🟢 **Cobertura de campos: 6/6. La pantalla expone el contrato completo del endpoint, sin campos faltantes ni campos de más.**

La validación en cliente cubre formato de NIT, formato de registro ISSS, obligatoriedad y longitudes máximas, con mensajes traducidos ES/EN que coinciden con los del servidor.

Quedan **9 hallazgos de frontend** y **3 de backend**. Dos de frontend son de severidad alta: un **guard de ruta con el permiso equivocado (F-01)** y una **divergencia de validación cliente/servidor con espacios en blanco (F-02)** que produce un `400` que el frontend no puede mapear a ningún campo.

**Frontend** (§5)

| Severidad | Cantidad | IDs |
|---|---|---|
| 🔴 Alta | 2 | F-01, F-02 |
| 🟡 Media | 2 | F-03, F-04 |
| 🔵 Baja | 4 | F-05, F-06, F-07, F-08 |
| ⚪ Informativo | 1 | F-09 |

**Backend** (§6) — detectados desde el frontend, se resuelven del lado del servidor

| Severidad | ID | Estado | Resumen |
|---|---|---|---|
| 🟡 Media | B-01 | 🟢 Resuelto | El recurso ya expone `allowedActions` |
| 🟡 Media | B-02 | 🟢 Resuelto | El `400` ya trae la clave de cada campo |
| 🟡 Media | B-03 | ⏸️ Bloqueado | El proxy descarta cabeceras del upstream — **otro repositorio** |

Ninguno de los tres bloqueaba al frontend. **Dos quedaron resueltos el 2026-08-16** y habilitan dos mejoras del cliente: gobernar el botón *Guardar* con `allowedActions` y resaltar el input que falló. Ver §6.

---

## 2. Contrato de referencia (backend)

### 2.1 Endpoints del recurso

```
GET   /api/v1/companies/{companyPublicId}/legal-profile
POST  /api/v1/companies/{companyPublicId}/legal-profile
PUT   /api/v1/companies/{companyPublicId}/legal-profile
```

> El frontend los consume vía proxy del mismo origen como `GET /v1/legal-profile` (la empresa se resuelve del contexto de sesión). **Confirmado en vivo.**
>
> **El parámetro de ruta es `companyPublicId`, no `companyId`.** El controller lo declara como `companyId` pero una convención de la API lo reescribe. Igual con la respuesta: el record de C# declara `Id` y se serializa como **`publicId`**. Guiarse por el swagger, no por el código.

### 2.2 Autorización (código fuente: `CompanyPreferenceAuthorizationService`)

Este controller **no** lleva `[AuthorizationPolicySet]`. La autorización vive en el handler, vía `ICompanyPreferenceAuthorizationService`:

| Operación | Método del servicio | Permisos aceptados (cualquiera) |
|---|---|---|
| `GET` | `EnsureCanReadAsync` | `CompanyPreferences.Read` · `CompanyPreferences.Admin` · `iam.administration.manage` |
| `POST` / `PUT` | `EnsureCanManageAsync` | `CompanyPreferences.Admin` · `iam.administration.manage` |

`ResourceKey` del recurso: **`COMPANY_PREFERENCES`**.

### 2.3 Respuesta observada (`200`)

```jsonc
{
  "publicId": "efbedf88-9903-4f6e-be23-f9a9b2c386dc",
  "legalName": "End to End SAS",
  "employerNitNumber": "0614-000000-111-3",
  "isssEmployerRegistrationNumber": "123456-7",
  "fiscalAddress": "San Salvador la concha de la loras",
  "economicActivityDescription": null,
  "legalRepresentativePublicId": "cf37e83b-5006-4660-9a5c-a71a606e615c",
  "concurrencyToken": "32bffe02-ad74-4521-89ee-f32b7495838b",
  "createdAtUtc": "2026-08-13T04:54:27.19938Z",
  "modifiedAtUtc": "2026-08-13T04:54:43.5583Z",

  // ✅ Añadido el 2026-08-16 (B-01). NO estaba cuando se hizo esta corrida.
  "allowedActions": { "canView": true, "canCreate": false, "canEdit": true, "canDelete": false, "reasons": [] }
}
```

> **`allowedActions` es un objeto compartido por todo el producto** y trae más campos de los que este
> recurso usa: `canView · canCreate · canEdit · canDelete · canArchive · canActivate · canInactivate ·
> canSubmit · canApprove · canReject · canCancel · canPublish · canFinalize · actionPermissions ·
> reasons`. **Para el perfil legal solo tienen sentido `canView`, `canCreate` y `canEdit`**; los demás
> vienen en `false` porque el recurso no tiene esas transiciones. No hay que interpretarlos como
> «prohibido»: simplemente no aplican.

### 2.4 Reglas de negocio que gobiernan el recurso

1. **`PUT` es reemplazo total.** Omitir o mandar `null` en un campo opcional lo **borra**. No es un `PATCH`.
2. **Concurrencia obligatoria en `PUT`:** cabecera `If-Match` con el `concurrencyToken` vigente. Ausente → `400`; desactualizado → `409 COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT`. `POST` no lleva `If-Match`.
3. **El token rota en cada escritura** (`ConcurrencyToken = Guid.NewGuid()` tanto en `Create` como en `Update`). Hay que reemplazarlo en memoria después de cada guardado o el segundo `PUT` seguido falla.
4. **El dominio recorta (`Trim()`) todos los textos antes de guardar** y **rechaza cadenas en blanco** (`RequireText` lanza si `IsNullOrWhiteSpace`).
5. **Los formatos se validan sobre el valor recortado**, pero **`MaximumLength` se mide sobre el valor crudo**.
6. **Un solo perfil por empresa** (índice único por `tenant_id`). `POST` sobre una empresa que ya tiene perfil → `409 COMPANY_LEGAL_PROFILE_ALREADY_EXISTS`.
7. **El perfil NO se crea al aprovisionar la empresa.** Toda empresa nueva arranca sin perfil: el `404` es el estado inicial esperado, no un error.
8. **Representante legal:** debe existir en **la misma empresa** y estar **activo**. Las fechas de vigencia (`effectiveFrom`/`effectiveTo`) **no se validan a propósito** — un nombramiento futuro y una carga retroactiva son ambos legítimos.
9. **Gate de cumplimiento:** hoy la ausencia del perfil no bloquea nada. `CompanyPreference.PayrollComplianceGatesEnabled` está apagado por defecto y sin endpoint público. Cuando se active por tenant, `POST payroll-runs` responderá `422 PAYROLL_RUN_MISSING_LEGAL_PROFILE`.

### 2.5 Catálogo de errores

| Código | HTTP | Cuándo |
|---|---|---|
| `COMPANY_LEGAL_PROFILE_NOT_FOUND` | `404` | La empresa aún no configuró el perfil. **Estado vacío, no error** |
| `COMPANY_LEGAL_PROFILE_ALREADY_EXISTS` | `409` | `POST` sobre una empresa que ya tiene perfil |
| `COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado |
| `COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_NOT_FOUND` | `422` | El id de representante no existe en ninguna empresa |
| `COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_INACTIVE` | `422` | Existe en esta empresa pero está inactivo |
| *tenant mismatch* | `403` | El representante existe pero pertenece a **otra** empresa |
| `common.validation` | `400` | Falla de validación. **Los mensajes llegan bajo la clave `""`**, no bajo el nombre del campo |

Forma del `400` — **volcado literal del servidor, verificado el 2026-08-21** con `Accept-Language: es`,
enviando `legalName` y `fiscalAddress` con solo espacios:

```jsonc
{
  "type": "https://httpstatuses.com/400",
  "title": "Se encontraron uno o más errores de validación.",
  "status": 400,
  "detail": "Se encontraron uno o más errores de validación.",
  "errors": {
    "legalName":     ["'Legal Name' no debería estar vacío."],
    "fiscalAddress": ["'Fiscal Address' no debería estar vacío."]
  },
  "code": "common.validation",
  "traceId": "0HNNVTGPQNNV1"
}
```

> ⚠️ **Esto corrige lo que decía este documento.** La versión anterior afirmaba que todos los mensajes
> llegaban bajo la **clave vacía** `""` y que *«el frontend no puede mapear estos errores al input
> automáticamente»*. **Ya no es cierto**: desde el 2026-08-16 cada mensaje viene con **la clave de su
> campo**, en camelCase, igual que el nombre que se envía. Hay una prueba de integración que falla si
> alguna vez vuelve la clave vacía.
>
> **Consecuencia:** el resumen de errores a nivel de formulario **ya no es obligatorio**. Se puede
> resaltar el input y poner el mensaje debajo, que es lo que pide **F-04**.

**Tres detalles del volcado que conviene no pasar por alto:**

| Detalle | Qué significa para el cliente |
|---|---|
| `code` está en la **raíz** | No existe un objeto `extensions` en el JSON. Leer `code`, no `extensions.code` |
| `title` y `detail` traen **el mismo texto** | Basta con mostrar uno |
| El nombre del campo dentro del mensaje sale en **inglés** (`'Legal Name'`) | La frase está en español pero la etiqueta no. Es un defecto de servidor conocido y **el cliente no debe intentar traducirlo**: el mensaje se muestra tal cual, y las etiquetas se irán añadiendo en el servidor |

**Los espacios en blanco sí se rechazan** (`400`, no `500`): `NotEmpty()` de FluentValidation trata una
cadena de solo espacios como vacía. Es lo que sostiene **F-02**.

---

## 3. Cobertura de campos — resultado

| # | Campo API | Etiqueta ES / EN | `id` del input | Oblig. | Regla backend | Estado |
|---|---|---|---|---|---|---|
| 1 | `legalName` | Razón social / Legal name | `clp-legal-name` | **Sí** | `NotEmpty` + máx. 200 | 🟢 Presente y validado |
| 2 | `employerNitNumber` | NIT patronal / Employer NIT | `clp-nit` | **Sí** | `NotEmpty` + máx. 20 + `^\d{4}-\d{6}-\d{3}-\d$` | 🟢 Presente y validado |
| 3 | `isssEmployerRegistrationNumber` | Registro patronal ISSS / ISSS employer registration number | `clp-isss` | **Sí** | `NotEmpty` + máx. 20 + `^[0-9-]{6,20}$` | 🟢 Presente y validado |
| 4 | `fiscalAddress` | Dirección fiscal / Fiscal address | `clp-address` | **Sí** | `NotEmpty` + máx. 500 | 🟢 Presente y validado |
| 5 | `economicActivityDescription` | Actividad económica / Economic activity | `clp-activity` | No (nullable) | máx. 200 | 🟢 Presente, marcado *Opcional*, validado |
| 6 | `legalRepresentativePublicId` | Representante legal / Legal representative | `clp-legal-rep` | No (nullable) | activo + misma empresa | 🟢 Presente, con opción de limpiar (×) |

**No hay campos faltantes. No hay campos de más.** `publicId`, `concurrencyToken`, `createdAtUtc` y `modifiedAtUtc` son metadatos y correctamente no se editan.

### Extras correctos que vale la pena reconocer

- El selector de representante llama `GET /v1/legal-representatives?IsActive=true&Page=1&PageSize=100` → **filtra por activos de la empresa actual**, tal como exige la regla 8. Incluye buscador y enlace *Gestionar representantes*.
- Los *placeholders* de NIT (`0614-010180-101-2`) y de ISSS (`123456-7`) muestran el formato esperado.
- Traducción ES/EN **completa**, incluidos los mensajes de error, con la terminología del dominio (*Razón social*, *NIT patronal*, *Registro patronal ISSS*, *Dirección fiscal*).
- La pantalla ya está enlazada desde *Configuración HR* y desde el paso 1 del asistente. Cierra dos pendientes históricos de la guía de integración.
- *Guardar cambios* se deshabilita mientras el formulario es inválido.

---

## 4. Validación en cliente — batería ejecutada

Todas las pruebas se hicieron **sin guardar** (no se ejecutó ningún `POST`/`PUT`). Los valores originales quedaron restaurados.

| # | Entrada | Resultado observado | ¿Coincide con el backend? |
|---|---|---|---|
| 1 | `legalName` vacío | «Este campo es obligatorio.» · *Guardar* deshabilitado | ✅ |
| 2 | `legalName` con 250 caracteres | «Máximo 200 caracteres.» | ✅ |
| 3 | `economicActivityDescription` con 250 caracteres | «Máximo 200 caracteres.» | ✅ |
| 4 | `fiscalAddress` con 600 caracteres | «Máximo 500 caracteres.» | ✅ |
| 5 | `employerNitNumber` = `ABC` | «El NIT patronal debe seguir el formato ####-######-###-#.» | ✅ |
| 6 | `employerNitNumber` = `0614-010180-101` | Mismo mensaje de formato | ✅ |
| 7 | `isssEmployerRegistrationNumber` = `12345` | «El número de registro patronal del ISSS solo acepta dígitos y guiones (6 a 20 caracteres).» | ✅ |
| 8 | `isssEmployerRegistrationNumber` = `ABCDEF` | Mismo mensaje | ✅ |
| 9 | `isssEmployerRegistrationNumber` con 21 caracteres | «Máximo 20 caracteres.» | ✅ |
| 10 | `employerNitNumber` = `"  0614-000000-111-3  "` | «Máximo 20 caracteres.» | ⚠️ Técnicamente sí, pero engañoso → **F-06** |
| 11 | `legalName` = `"   "` (solo espacios) | **Sin error · *Guardar* HABILITADO** | ❌ **Divergencia → F-02** |
| 12 | `fiscalAddress` = `"   "` (solo espacios) | **Sin error · *Guardar* HABILITADO** | ❌ **Divergencia → F-02** |

---

## 5. Hallazgos

> Cada hallazgo lleva la **solución integrada con backend**: el endpoint y contrato válidos, y la regla de negocio que lo gobierna.

---

### 🔴 F-01 — El guard de la ruta pide el permiso equivocado

**Severidad:** Alta · **Tipo:** Autorización

#### Evidencia (frontend)

En el bundle desplegado (`main-EOCFHGRF.js`) la ruta sigue registrada así:

```js
{
  path: "company-legal-profile",
  canActivate: [t, o(n.PersonnelFiles, "Read")],
  loadComponent: () => import("./chunk-WOER74T4.js").then(...)
}
```

#### Regla de negocio que lo gobierna

El backend **no** gobierna este endpoint con el permiso de expedientes, sino con el de preferencias de empresa (§2.2). Consecuencia en dos direcciones:

1. Quien tenga `CompanyPreferences.Admin` pero **no** `PersonnelFiles.Read` **no puede llegar a la pantalla** — redirige en silencio a `/`, sin mensaje. Aunque la API le permitiría leer *y* escribir.
2. Quien tenga `PersonnelFiles.Read` pero **no** `CompanyPreferences.Read` **sí entra**, y el `GET` responde `403`.

**Por qué no se vio en esta corrida:** la cuenta usada es OWNER y tiene ambos permisos, lo que enmascara el defecto por completo.

#### Solución integrada — endpoints y contratos válidos

**Ajuste inmediato en frontend (sin esperar backend):** cambiar el guard a los permisos de §2.2:

```
Entrar a la pantalla  → CompanyPreferences.Read | CompanyPreferences.Admin | iam.administration.manage
Habilitar «Guardar»   → CompanyPreferences.Admin | iam.administration.manage
```

Hoy el botón *Guardar cambios* se muestra habilitado a **todo** el que entre, incluso a quien solo tiene `Read`; ese usuario recibirá `403` al guardar.

**⚠️ NO usar estos endpoints como fuente del guard:**

```
GET /api/v1/account/companies/{companyPublicId}/access-context
GET /api/v1/account/companies/{companyPublicId}/authorization/resource-policies/{resourceKey}
```

Aunque `resource-policies/{resourceKey}` devuelve exactamente la política del usuario para un recurso (can access/read/create/update/delete + estados por campo), **su autorización es por propiedad de la empresa**: el llamante debe ser el **creador** de la compañía. Un administrador legítimo que no la creó recibe `403`/`404`. Sirve para el dueño, no como mecanismo general.

**Cambio pedido al backend (recomendado, es la vía canónica):** exponer `allowedActions` en este recurso. Son dos cambios pequeños:

1. Decorar `CompanyLegalProfilesController` con `[ResourceActions(CompanyPreferencePermissionCodes.ResourceKey)]` → `"COMPANY_PREFERENCES"`.
2. Hacer que `CompanyLegalProfileResponse` implemente `ISupportsAllowedActions` (agregar `AllowedActionsResponse? AllowedActions = null` como último miembro posicional). La documentación de la interfaz lo describe como *«usually a one-token change»*.

Con eso, `AllowedActionsResultFilter` lo llena solo — **las respuestas de objeto único se enriquecen siempre** (el `?includeAllowedActions=true` solo aplica a listas paginadas). El `GET` pasaría a devolver:

```jsonc
{
  "...": "...",
  "allowedActions": {
    "canView": true,
    "canCreate": false,
    "canEdit": true,
    "canDelete": false,
    "reasons": []
  }
}
```

Y el frontend gobierna guard y botón con `canView` / `canEdit` / `canCreate`, sin adivinar códigos de permiso. **Este cambio está pendiente del lado del backend; el ajuste del guard (arriba) se puede hacer hoy mismo.**

---

### 🔴 F-02 — El cliente acepta espacios en blanco donde el servidor los rechaza

**Severidad:** Alta · **Tipo:** Divergencia de validación cliente/servidor

#### Evidencia (frontend)

```
legalName    = "   " (solo espacios)  →  sin error · Guardar HABILITADO
fiscalAddress = "   " (solo espacios) →  sin error · Guardar HABILITADO
```

El formulario da por válido el contenido y deja guardar.

#### Regla de negocio que lo gobierna

El servidor lo rechaza por **dos** capas independientes:

1. **Validador** — `CompanyLegalProfileCommandValidatorBase.ApplySharedRules` aplica `NotEmpty()` a `legalName`, `employerNitNumber`, `isssEmployerRegistrationNumber` y `fiscalAddress`. `NotEmpty()` de FluentValidation, sobre `string`, falla también con **solo espacios** (`IsNullOrWhiteSpace`).
2. **Dominio** — `CompanyLegalProfile.RequireText` lanza `ArgumentException` si el valor es blanco, y hace `Trim()` antes de asignar.

**Causa raíz:** `Validators.required` de Angular solo mide longitud, así que `"   "` pasa. Es la misma causa raíz de **F-06**: el cliente nunca normaliza los espacios, mientras que el servidor siempre recorta antes de guardar.

#### Por qué importa más de lo que parece

El `400` resultante llega con los mensajes bajo la clave `""` (§2.5), así que el frontend **no puede señalar qué campo falló**. El usuario ve un error genérico a nivel de formulario sobre un campo que «se ve lleno». Es la peor ruta de fallo posible.

#### Contrato para el frontend

```jsonc
POST /v1/legal-profile            PUT /v1/legal-profile   ← If-Match obligatorio
{
  "legalName": "…",                        // requerido · NotEmpty · máx. 200
  "employerNitNumber": "…",                // requerido · NotEmpty · máx. 20 · ^\d{4}-\d{6}-\d{3}-\d$
  "isssEmployerRegistrationNumber": "…",   // requerido · NotEmpty · máx. 20 · ^[0-9-]{6,20}$
  "fiscalAddress": "…",                    // requerido · NotEmpty · máx. 500
  "economicActivityDescription": null,     // opcional  · máx. 200
  "legalRepresentativePublicId": null      // opcional  · activo y de la misma empresa
}
```

| Regla | Dónde vive | Qué implica para el cliente |
|---|---|---|
| `NotEmpty()` de FluentValidation | validador | Falla también con **solo espacios** |
| `RequireText` | dominio | Lanza si es blanco, y **recorta antes de guardar** |
| `MaximumLength` | validador | Se mide sobre el valor **crudo** |

Si llega el `400`, los mensajes **ya vienen con la clave de su campo** (🟢 B-02 resuelto el 2026-08-16) — ver **F-04**.

#### Ajuste pedido al frontend

Normalizar en cliente **antes** de validar, replicando lo que hace el dominio:

- Aplicar `trim()` al valor en `blur` (o un `updateOn: 'blur'` con normalización) en los 6 campos de texto.
- Sustituir `Validators.required` por un validador que rechace también blancos (`control.value?.trim().length > 0`) en los 4 campos obligatorios.

Con esto se alinea el cliente con las dos capas del servidor y de paso desaparece **F-06**, porque el `maxLength` pasaría a medirse sobre el valor ya recortado — que es exactamente el que se persiste.

**No requiere cambio en el backend.** El contrato ya es el correcto; es el cliente el que debe replicarlo.

---

### 🟡 F-03 — Los campos obligatorios no se distinguen de los opcionales

**Severidad:** Media · **Tipo:** UX / Accesibilidad

#### Evidencia (frontend)

Ninguna etiqueta lleva marca de obligatoriedad:

```
LABEL[for=clp-legal-name] html="Legal name"
LABEL[for=clp-nit]        html="Employer NIT"
LABEL[for=clp-isss]       html="ISSS employer registration number"
LABEL[for=clp-address]    html="Fiscal address"
LABEL[for=clp-activity]   html="Economic activity"
LABEL[for=clp-legal-rep]  html="Legal representative"
```

Los 4 obligatorios y los 2 opcionales se ven **idénticos**. Ningún input declara `required` ni `aria-required="true"`. Es inconsistente con la propia app: el login sí usa asterisco (`Email *`, `Password *`).

#### Regla de negocio que lo gobierna

La obligatoriedad es la de §3, tomada de `ApplySharedRules`: obligatorios los 4 primeros (`NotEmpty`), opcionales `economicActivityDescription` (solo `MaximumLength(200)`) y `legalRepresentativePublicId` (nullable).

#### Contrato para el frontend

**No interviene ningún endpoint nuevo** — es presentación. Pero la **fuente de verdad** de la obligatoriedad es el cuerpo de escritura:

```jsonc
POST /v1/legal-profile            PUT /v1/legal-profile   ← If-Match obligatorio
{
  "legalName": "…",                        // requerido · NotEmpty · máx. 200
  "employerNitNumber": "…",                // requerido · NotEmpty · máx. 20 · ^\d{4}-\d{6}-\d{3}-\d$
  "isssEmployerRegistrationNumber": "…",   // requerido · NotEmpty · máx. 20 · ^[0-9-]{6,20}$
  "fiscalAddress": "…",                    // requerido · NotEmpty · máx. 500
  "economicActivityDescription": null,     // opcional  · máx. 200
  "legalRepresentativePublicId": null      // opcional  · activo y de la misma empresa
}
```

#### Ajuste pedido al frontend

Marcar con `*` las 4 etiquetas obligatorias y añadir `aria-required="true"` a sus inputs.

Si se implementa el `allowedActions` de **F-01**, conviene saber que existe además un catálogo de **política por campo** (`hidden` / `masked` / `readonly` / `editable`, más banderas de *required* y *sensitive*) expuesto en `resource-policies/{resourceKey}` — pero **con la limitación de propiedad ya descrita**, así que hoy no es una fuente confiable para este caso. La obligatoriedad puede quedar declarada en el cliente.

---

### 🟡 F-04 — El mensaje de error no está vinculado al input

**Severidad:** Media · **Tipo:** Accesibilidad

#### Evidencia (frontend)

Al invalidar el NIT:

```
nit.aria-invalid        = true      ✅
nit.aria-describedby    = null      ❌
nit.aria-errormessage   = null      ❌
<p class="mt-1 text-xs text-error"> sin id, sin role, sin aria-live   ❌
```

Un usuario con lector de pantalla oye que el campo es inválido **pero nunca oye por qué**, y el mensaje no se anuncia al aparecer.

#### Regla de negocio que lo gobierna

Se conecta con §2.5: como los errores del servidor llegan bajo la clave `""` y **no son mapeables a un campo**, la pantalla necesita de todas formas una **región de error a nivel de formulario**. Conviene resolver ambas cosas en el mismo pase.

#### Contrato para el frontend

Los dos endpoints que devuelven este error:

```
POST /v1/legal-profile
PUT  /v1/legal-profile     If-Match: "{concurrencyToken}"
```

La forma del error que hay que presentar:

```jsonc
{ "status": 400, "code": "common.validation",
  "detail": "Se encontraron uno o más errores de validación.",
  "errors": {
    "employerNitNumber": [ "El formato del NIT patronal no es válido." ],
    "isssEmployerRegistrationNumber": [ "…" ]
  } }
```

**La clave es el nombre del campo**, en camelCase, idéntico al que se envía. Se puede usar directamente
para localizar el control.

| Código | HTTP | ¿Se puede señalar el campo? |
|---|---|---|
| `common.validation` | `400` | **Sí** — clave por campo desde 2026-08-16 (**B-02** resuelto). Se puede resaltar el input |
| `COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_NOT_FOUND` | `422` | Sí — el selector de representante |
| `COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_INACTIVE` | `422` | Sí — el selector de representante |
| `COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT` | `409` | No — recargar el perfil |

#### Ajuste pedido al frontend

1. Dar `id` al `<p>` del error y referenciarlo con `aria-describedby` (o `aria-errormessage`) desde el input.
2. Añadir `role="alert"` o `aria-live="polite"` al contenedor del error.
3. **Mapear cada entrada de `errors` a su control por el nombre de la clave** y pintar el mensaje bajo
   el input correspondiente. La clave es el nombre del campo en camelCase: `errors.employerNitNumber` →
   el input del NIT.

> ⚠️ **Esto cambió respecto a la versión anterior de este documento**, que pedía un resumen a nivel de
> formulario porque el servidor mandaba todo bajo la clave vacía. **Ya no lo hace** (§2.5). Un resumen
> sigue siendo buena práctica de accesibilidad, pero ya no es la única forma de mostrar el error.

**No requiere cambio en el backend.** El contrato ya expone el campo.

---

### 🔵 F-05 — «Última actualización» muestra la fecha ISO cruda

**Severidad:** Baja · **Tipo:** UX

#### Evidencia (frontend)

```
Última actualización: 2026-08-13T04:54:43.5583Z
```

Valor crudo de `modifiedAtUtc`, sin formatear, sin localizar y sin convertir de zona horaria. Igual en inglés y en español.

#### Regla de negocio que lo gobierna

- `createdAtUtc` y `modifiedAtUtc` son **UTC** (sufijo `Z`).
- **`modifiedAtUtc` es nullable**: viene `null` hasta la primera edición. Un perfil recién creado por `POST` lo tendrá en `null`.
- La zona horaria de la empresa vive en las preferencias, no en este recurso:

```
GET /api/v1/companies/{companyPublicId}/preferences
→ { "currencyCode": "...", "timeZone": "America/El_Salvador", ... }
```

`CompanyPreference.TimeZone` tiene default `"UTC"` y se normaliza al guardarse.

#### Contrato para el frontend

Hacen falta **dos** peticiones: la del perfil, que ya se emite, y la de preferencias, para la zona horaria.

```
GET /v1/legal-profile              → { "createdAtUtc": "…", "modifiedAtUtc": null }   // nullable
GET /v1/companies/{companyPublicId}/preferences
                                   → { "currencyCode": "USD", "timeZone": "America/El_Salvador", … }
```

| Campo | Tipo | Nota |
|---|---|---|
| `createdAtUtc` | `DateTime` UTC | siempre presente · hoy **no se usa** en la pantalla |
| `modifiedAtUtc` | `DateTime?` UTC | `null` hasta la primera edición |
| `timeZone` | `string` IANA | default `"UTC"`, normalizado al guardarse |

#### Ajuste pedido al frontend

1. Formatear con el pipe de fecha/hora, convirtiendo de UTC a la `timeZone` de las preferencias de la empresa.
2. **Manejar el `null`:** cuando `modifiedAtUtc` sea `null`, caer a `createdAtUtc` con la etiqueta «Creado el …». Hoy `createdAtUtc` no se usa en ninguna parte de la pantalla y este caso no se pudo verificar porque el perfil probado ya tenía edición.

---

### 🔵 F-06 — Un NIT pegado con espacios produce un error engañoso

**Severidad:** Baja · **Tipo:** UX · **Misma causa raíz que F-02**

#### Evidencia (frontend)

Pegar `"  0614-000000-111-3  "` (21 caracteres con los espacios) responde **«Máximo 20 caracteres.»** en lugar de aceptarlo.

#### Regla de negocio que lo gobierna

El cliente está replicando fielmente al servidor: el backend también mide `MaximumLength(20)` sobre el valor **crudo** y solo hace `Trim()` antes de validar el **formato** (regla 5 de §2.4). No es divergencia de contrato — es un mensaje que el usuario no puede diagnosticar, porque el valor «se ve» de 19 caracteres.

#### Contrato para el frontend

```
POST /v1/legal-profile            PUT /v1/legal-profile
```

Mismo endpoint que **F-02**. Lo que importa es el **orden en que se aplican las reglas** sobre `employerNitNumber` e `isssEmployerRegistrationNumber`:

| Orden | Regla | Se mide sobre |
|---|---|---|
| 1 | `MaximumLength(20)` | el valor **crudo**, con espacios |
| 2 | `Must(IsEmployerNit)` | el valor **recortado** |

Por eso `"  0614-000000-111-3  "` (21 caracteres) falla por **longitud**, no por formato, y el mensaje resulta indiagnosticable.

#### Ajuste pedido al frontend

El `trim()` en `blur` propuesto en **F-02** resuelve este hallazgo también. El servidor persiste el valor recortado de todos modos, así que recortarlo antes no cambia el resultado: solo elimina un error indiagnosticable.

---

### 🔵 F-07 — El selector de representante legal no es un combobox accesible

**Severidad:** Baja · **Tipo:** Accesibilidad

#### Evidencia (frontend)

El control es un `<button id="clp-legal-rep">` que despliega un panel con buscador y lista. No declara `role="combobox"`, ni `aria-haspopup="listbox"`, ni `aria-expanded`.

#### Regla de negocio que lo gobierna

`legalRepresentativePublicId` es opcional y nullable (regla 8 de §2.4). El control debe permitir dejarlo vacío — hoy lo hace bien con la «×».

#### Contrato para el frontend

**No interviene ningún endpoint** — es el patrón ARIA del control, no su origen de datos. El endpoint que lo alimenta y su defecto de paginación son asunto de **F-08**.

#### Ajuste pedido al frontend

Aplicar el patrón ARIA de combobox, o como mínimo `aria-haspopup="listbox"` + `aria-expanded` sincronizado con el estado del panel. **No requiere cambio en el backend.**

---

### 🔵 F-08 — El selector de representantes puede truncar en silencio

**Severidad:** Baja · **Tipo:** Riesgo funcional

#### Evidencia (frontend)

Una sola llamada, sin paginación, y el filtrado del buscador se hace en cliente:

```
GET /v1/legal-representatives?IsActive=true&Page=1&PageSize=100
```

#### Regla de negocio que lo gobierna

`LegalRepresentativeValidationRules`:

```
DefaultPageSize = 20
MaxPageSize     = 100
```

**El frontend ya está pidiendo el máximo permitido**: `pageSize` está validado con `[Range(1, 100)]` y `InclusiveBetween(1, 100)`. Subirlo no es opción — devolvería `400`. Una empresa con más de 100 representantes activos vería la lista recortada **sin ningún aviso**.

#### Solución integrada — endpoint y contrato válidos

El endpoint **ya soporta búsqueda del lado del servidor**, que es lo que el frontend debería usar:

```
GET /api/v1/companies/{companyId}/legal-representatives
    ?isActive=true
    &q={texto libre}          ← búsqueda server-side (NO se está usando)
    &isPrimary={bool}
    &representationType={enum}
    &page=1
    &pageSize=20              ← default 20, máx. 100
    &includeAllowedActions={bool}
```

Respuesta: `PagedResponse<LegalRepresentativeListItemResponse>` (trae `totalCount`, lo que permite detectar el truncamiento).

**⚠️ Regla operativa importante:** el endpoint está *rate-limited* con la política `LegalRepresentatives:Search`, particionada **por usuario + tenant**, con límite por defecto de **120** peticiones (`RateLimiting:LegalRepresentatives:Search:PermitLimit` en configuración). Una búsqueda con *type-ahead* sin control agotaría la cuota y empezaría a recibir `429`.

**Ajuste pedido al frontend:**

1. Usar `q=` para buscar en servidor conforme se teclea, en lugar de traer 100 y filtrar en cliente.
   ✅ **Desde el 2026-08-21 esa búsqueda ignora los acentos**: `q=jose` encuentra «José» y `q=canas`
   encuentra «Cañas». Un filtrado en cliente por comparación de cadenas **no** hace eso, así que la
   búsqueda del servidor es además más exacta que la actual.
2. **Debounce de al menos 300 ms** y cancelación de la petición anterior, por el rate limit.
3. Volver a `pageSize=20` (el default) para la carga inicial, y usar `totalCount` para avisar o paginar si hay más resultados.

---

### ⚪ F-09 — Nota / trampa: el `ETag` de la respuesta **no** es el `concurrencyToken`

**Severidad:** Informativo · **Tipo:** Trampa de integración

#### Evidencia (medida en vivo sobre `GET /v1/legal-profile`)

```
ETag                    = W/"1d3-+0jcVFtouHBHD3qEZfrvRbad0/A"     ← weak ETag del proxy Node
body.concurrencyToken   = 32bffe02-ad74-4521-89ee-f32b7495838b    ← el token real del backend
```

#### Regla de negocio que lo gobierna

El backend sí emite un `ETag` fuerte con el token (`ToActionResultWithETag`), pero **el proxy del mismo origen lo sustituye por su propio weak ETag calculado sobre el cuerpo**. Lo que llega al navegador ya no es el token.

#### Contrato válido

El `If-Match` del `PUT` debe construirse **siempre con `concurrencyToken` del cuerpo**, nunca con la cabecera `ETag`:

```http
PUT /v1/legal-profile
If-Match: "32bffe02-ad74-4521-89ee-f32b7495838b"
```

Usar la cabecera produciría `400` (formato inválido) o `409`. Y tras cada guardado exitoso hay que **reemplazar el token en memoria** con el que devuelve la respuesta (regla 3 de §2.4).

#### Contrato para el frontend

```
GET  /v1/legal-profile                       → 200 · el cuerpo trae concurrencyToken
POST /v1/legal-profile                       → 201 · sin If-Match
PUT  /v1/legal-profile   If-Match: "{token}" → 200 · el cuerpo trae el token ROTADO
```

| Situación | Respuesta |
|---|---|
| `If-Match` ausente | `400` |
| `If-Match` con un token desactualizado | `409 COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT` |
| `If-Match` con el `ETag` del proxy (`W/"…"`) | `400` — no es un GUID |

No se detectó ningún síntoma, así que probablemente ya se está haciendo bien — se documenta para que no se «optimice» después leyendo la cabecera.

---

## 6. Hallazgos de backend

Detectados **desde el frontend** durante esta corrida, pero cuya causa y solución están del lado del servidor.

> El **detalle completo vive en el documento espejo** [`ComentariosPruebasBackend/00001-CompanyLegalProfile`](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md), con su estado y bitácora. Aquí solo queda el puntero para no duplicar contenido.

| ID | Sev. | Estado | Hallazgo | Origen |
|---|---|---|---|---|
| [**B-01**](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#2-b-01--el-recurso-de-perfil-legal-no-expone-allowedactions) | 🟡 Media | 🟢 **Resuelto** | El recurso ya expone `allowedActions`; el frontend deja de adivinar permisos | F-01 |
| [**B-02**](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#3-b-02--el-400-de-validación-agrupa-todos-los-mensajes-bajo-la-clave-vacía) | 🟡 Media | 🟢 **Resuelto** | El `400` ya trae la clave de cada campo en vez de agrupar todo bajo `""` | §2.5, F-02, F-04 |
| [**B-03**](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location) | 🟡 Media | ⏸️ **Bloqueado** | El proxy descarta cabeceras del upstream. **No es del backend**: el proxy vive en otro repositorio | F-09 |

### ✅ Lo que el frontend ya puede hacer (2026-08-16)

**1. Dejar de replicar los permisos a mano.** `GET /v1/legal-profile` ahora devuelve:

```jsonc
"allowedActions": { "canView": true, "canCreate": false, "canEdit": true, "canDelete": false, "reasons": [] }
```

Se puede gobernar el guard de la ruta y el botón *Guardar* con `canView` / `canEdit` en vez de con la lista de códigos. Es la vía que **cierra F-01 de raíz**: el guard pedía `PersonnelFiles.Read`, un permiso de otro módulo, y ese tipo de error deja de ser posible.

**2. Señalar el campo que falló.** El `400` ya viene con claves reales:

```jsonc
"errors": {
  "employerNitNumber": ["El NIT patronal debe seguir el formato ####-######-###-#."],
  "isssEmployerRegistrationNumber": ["El número de registro patronal del ISSS solo acepta dígitos y guiones."]
}
```

Se pueden resaltar los inputs en vez de pintar una lista a nivel de formulario. **Mejora F-04** y quita la peor ruta de fallo que documentaba F-02: un error genérico sobre un campo que «se ve lleno».

> ⚠️ Si algún código lee `errors[""]` a ciegas, dejará de encontrarlo. Es el único cambio de forma.

**B-03 sigue vigente**: seguir construyendo el `If-Match` con el `concurrencyToken` **del cuerpo**, nunca con la cabecera `ETag`.

---

## 7. Qué NO se probó en esta corrida

| Escenario | Motivo |
|---|---|
| `POST` (crear) y `PUT` (actualizar) | No se guardó nada: la empresa de prueba tiene datos reales y no se pidió modificarlos |
| Estado vacío (`404 COMPANY_LEGAL_PROFILE_NOT_FOUND`) | La empresa probada ya tiene perfil. Es el estado inicial de toda empresa nueva (regla 7) y debe pintarse como formulario vacío que guarda con `POST` |
| Manejo de `403` (sin permiso) | El usuario es OWNER — es justo lo que enmascara **F-01** |
| Conflicto de concurrencia (`409`) | Requiere dos guardados |
| Errores `422` de representante legal | Requiere guardar |
| Presentación del `400` en la pantalla | Requiere guardar — se conecta con **F-04** y **F-02**. ⚠️ **La forma del error ya no es la que documentaba esta corrida**: ver §2.5 |
| Comportamiento con `modifiedAtUtc = null` | Ver **F-05** |
| Truncamiento con >100 representantes activos | Requiere datos de volumen |

Para cubrirlos hace falta una empresa desechable o autorización para escribir en la actual.

---

## 8. Prioridad sugerida

### Frontend

| # | Hallazgo | Depende de backend | Esfuerzo |
|---|---|---|---|
| 1 | **F-01** — guard de ruta | 🟢 **B-01 resuelto**: usar `allowedActions.canView`/`canEdit` en vez de replicar códigos | Bajo |
| 2 | **F-02** — trim y required contra blancos | No | Bajo |
| 3 | **F-03** — marcar obligatorios | No | Muy bajo |
| 4 | **F-05** — formatear fecha + fallback a `createdAtUtc` | No | Bajo |
| 5 | **F-04**, **F-07** — accesibilidad (agrupables) | 🟢 **B-02 resuelto**: el `400` ya trae la clave del campo, se puede resaltar el input | Medio |
| 6 | **F-08** — búsqueda server-side con debounce | No | Medio |
| 7 | **F-06** | Se resuelve solo al aplicar **F-02** | — |
| 8 | **F-09** | Solo verificar que no se regresione. **B-03 bloqueado en otro repositorio**: seguir usando el token del cuerpo | — |

### Backend

| # | Hallazgo | Cambio | Estado |
|---|---|---|---|
| 1 | **B-01** | `[ResourceActions]` + `ISupportsAllowedActions` + **registro en `AllowedActionsRegistry`** (eran tres pasos, no dos) | 🟢 **Resuelto** |
| 2 | **B-02** | Validadores con expresiones directas, para que el `400` deje de agrupar todo bajo `""` | 🟢 **Resuelto** |
| 3 | **B-03** | Que el proxy preserve las cabeceras del upstream | ⏸️ **Bloqueado** — otro repositorio |

Los dos primeros están resueltos y verificados. **B-03 no es trabajo del backend**: el proxy no vive en este repositorio, y el arreglo pertenece a quien mantiene el BFF.

---

## 9. Para reintegrar y publicar — revisión de esta pantalla

> **Cómo leer esta lista.** No sabemos qué tiene hoy el cliente, así que **no está escrita como «cambios»
> sino como comprobaciones**: cada punto es algo que el servidor hace de una forma concreta y que la
> pantalla tiene que estar haciendo igual. Si ya coincide, no hay nada que tocar.
>
> **Esta pantalla no está rota.** El contrato es el mismo que se probó el 2026-08-13 salvo por **dos
> añadidos del servidor** que la mejoran, y por **una forma de error que cambió**. Los nueve hallazgos
> (§5) son trabajo aparte.

### 9.1 El único cambio de forma — si algo lee `errors[""]`, dejará de encontrarlo

| Antes (lo que documentaba esta corrida) | Ahora — verificado el 2026-08-21 |
|---|---|
| `"errors": { "": [ "mensaje", "mensaje" ] }` | `"errors": { "legalName": [ "…" ], "fiscalAddress": [ "…" ] }` |

**Es el único punto que puede romper algo.** Un cliente que lea la clave vacía a ciegas se queda sin
mensajes que mostrar — sin error de JavaScript, simplemente una lista vacía.

Si el formulario hoy pinta un resumen a nivel de formulario, **sigue funcionando** siempre que recorra las
claves de `errors` en vez de acceder a `errors[""]`.

### 9.2 Dos añadidos que se pueden aprovechar, y ninguno obliga a nada

| # | Qué hay ahora | Qué permite |
|---|---|---|
| 1 | `allowedActions` en la respuesta del `GET` (§2.3) | Gobernar el guard de la ruta y el botón *Guardar* con `canView`/`canEdit` en vez de replicar códigos de permiso. **Es lo que cierra F-01 de raíz** |
| 2 | La búsqueda `q=` del selector de representantes **ignora acentos** | `q=jose` encuentra «José». Hace que el arreglo de **F-08** sea además más exacto que el filtrado en cliente de hoy |

**Ninguno de los dos rompe nada si se ignora.** La pantalla sigue funcionando exactamente igual.

### 9.3 Lo que NO cambió y no hay que tocar

- Las tres rutas, sus verbos y su autorización: `GET`/`POST`/`PUT` sobre `legal-profile`.
- **`PUT` sigue siendo reemplazo total**, no un `PATCH`: omitir un opcional lo borra.
- **`If-Match` se construye con `concurrencyToken` del cuerpo, nunca con la cabecera `ETag`** (F-09). El
  proxy sigue sustituyendo el `ETag` por el suyo — `00001 / B-03` está bloqueado en otro repositorio.
- El token **rota en cada escritura**: hay que reemplazarlo en memoria tras cada guardado.
- Los seis campos del formulario, sus reglas y sus longitudes máximas.
- El `404` sigue siendo **el estado inicial de toda empresa nueva**, no un error.

### 9.4 Un detalle de presentación que conviene decidir antes de publicar

Los mensajes de validación llegan en español pero **con el nombre del campo en inglés**:

```
'Legal Name' no debería estar vacío.
```

Es un defecto del servidor, en corrección progresiva. **La recomendación es mostrar el mensaje tal cual y
no intentar traducirlo en cliente**: cualquier sustitución por texto propio dejará de coincidir cuando el
servidor empiece a mandar la etiqueta correcta.

Si el formulario ya tiene su propio mensaje para «campo obligatorio» —que es lo que hoy muestra en
cliente—, lo razonable es **seguir usándolo para la validación local** y reservar el texto del servidor
para lo que solo el servidor sabe (formato de NIT, ISSS, representante inactivo).

### 9.5 Orden sugerido para volver a probar de cero

1. **Comprobar §9.1.** Es lo único que puede dejar la pantalla sin mostrar errores del servidor.
2. **F-02 primero entre los hallazgos** (recortar y rechazar blancos): es de esfuerzo bajo, no depende de
   nadie, y **arrastra F-06** sin trabajo extra.
3. **F-01 con `allowedActions`** — cierra el hallazgo más grave y elimina la clase entera de error.
4. El resto (F-03, F-04, F-05, F-07, F-08) puede ir después: ninguno impide usar la pantalla.
5. Al reabrir la corrida se validará la pantalla completa otra vez, incluidos los escenarios de §7 que
   nunca se pudieron ejecutar por no tener permiso de escritura.

> ⚠️ **Este documento no se ha vuelto a probar contra el ambiente.** Lo revalidado el 2026-08-21 es **el
> contrato del servidor**: rutas, esquemas, reglas de validación y la forma literal del `400`, leídos del
> código y del contrato publicado, con un volcado real del error. Los hallazgos F-01…F-09 siguen tal como
> se observaron el 2026-08-13: no se han añadido ni retirado, porque no se ha repetido la corrida.
