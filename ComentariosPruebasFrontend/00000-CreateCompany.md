# 00000 — CreateCompany · Hallazgos de frontend

| | |
|---|---|
| **ID** | 00000-CreateCompany |
| **Documento espejo** | [`ComentariosPruebasBackend/00000-CreateCompany`](../ComentariosPruebasBackend/00000-CreateCompany.md) |
| **Paso probado** | **Ninguno del asistente** — es la pantalla *anterior* al Paso 1 |
| **Pantalla** | `/companies/create` · asistente de 3 pasos |
| **Fecha** | 2026-08-15 |
| **Ambiente** | `https://dashboard.clarihr.com` |

> **Por qué lleva el número `00000`.** No es un paso de la configuración guiada: es **la puerta de entrada**, la pantalla que crea la empresa sobre la que después corren los ocho pasos. Se numeró con ceros para que quede ordenada delante de `00001` sin desplazar la numeración ya publicada.
>
> **Se probó porque se necesitaba una empresa desechable**, no por plan. Dos mediciones del Paso 3 —el límite de profundidad y el manejo de `403`— exigían una empresa que se pudiera ensuciar sin dañar `End to End SAS`. Al crearla, la pantalla destapó seis hallazgos.

---

## 1. Resumen ejecutivo

🔴 **La mitad del asistente muestra las claves de traducción en crudo.** El segundo paso completo y parte del tercero renderizan `legalReps.firstName`, `legalReps.identity`, `legalReps.selectDocType`… **26 en total**, y en **los dos idiomas** (**F-01**). Es la primera pantalla que ve un cliente nuevo.

🔴 **El campo de país parece lleno y está vacío.** Muestra `US` en inglés y `MX` en español, con el mismo aspecto que un valor elegido. Al pulsar *Siguiente* responde «El código de país es obligatorio» (**F-02**).

🟡 **La fecha de vigencia viene con el día equivocado durante seis horas de cada día.** Se precarga con la fecha **UTC** y no con la local: a las 18:07 del 15 de agosto en El Salvador, el campo decía **16 de agosto** (**F-03**).

🟡 **El teléfono asume 🇺🇸 +1** aunque la empresa se esté registrando en El Salvador (**F-04**).

🟢 **El catálogo de tipos de empresa se resuelve bien por país**: está deshabilitado hasta elegir país y entonces carga los tipos salvadoreños (`Sociedad Anónima de Capital Variable`, `Cooperativa`…). Es el comportamiento correcto para un catálogo por país.

🟢 **La empresa quedó creada y operativa**, activa y seleccionada como empresa actual, y con su expediente de estructura vacío — sin datos sembrados de conveniencia, como corresponde.

**Frontend** (§4)

| Severidad | Cantidad | IDs |
|---|---|---|
| 🔴 Alta | 2 | F-01, F-02 |
| 🟡 Media | 2 | F-03, F-04 |
| 🔵 Baja | 1 | F-05 |
| 🟢 Cerrado | 1 | F-06 — resuelto por B-04 |

**Backend** (§5) — 4 hallazgos, encabezados por [`00000 / B-02`](../ComentariosPruebasBackend/00000-CreateCompany.md#3-b-02--tres-fechas-de-calendario-viajan-como-datetime-contra-la-convención-dateonly-del-propio-producto), que es el espejo de F-03.

> **El hallazgo de fechas se renumeró `B-01`→`B-02`** el 2026-08-15: la revisión del documento de backend levantó un hallazgo de severidad Alta ([`B-01`](../ComentariosPruebasBackend/00000-CreateCompany.md#2-b-01--el-camino-patch-se-salta-la-frontera-de-fechas-diez-lectores-leen-el-jsonelement-en-crudo), el camino `PATCH` se salta la frontera de fechas) y el README obliga a numerar por severidad.

---

## 2. Contrato de la pantalla

> **Revalidado contra el backend el 2026-08-21**, campo por campo, contra el contrato publicado y el
> código. Lo que sigue es lo que el servidor acepta y devuelve hoy — no una descripción de lo que cambió.

### 2.1 Endpoints que intervienen

**El que crea la empresa:**

```
POST /v1/account/companies          → 201   (ruta real: api/v1/account/companies)
```

> **Autorización distinta al resto del producto.** Esta familia **no usa RBAC**: la propiedad se comprueba contra el `sub` del JWT (`CreatedByUserPublicId`), resuelto en los handlers. Está excluida a propósito de `[AuthorizationPolicySet]`. **El frontend no debe buscar aquí un permiso que declarar.**

**Los cuatro catálogos que alimentan los controles.** Todos devuelven un array plano, sin paginar:

| Control del formulario | Endpoint | Parámetros | Campos del elemento |
|---|---|---|---|
| Selector de **país** (paso 1) | `GET /v1/account/companies/countries` | ninguno | `publicId · code · name · sortOrder · defaultLocale · normalizedCode` |
| Selector de **tipo de empresa** (paso 1) | `GET /v1/account/companies/company-types` | `countryCode` *(opcional; sin él devuelve todos)* | `publicId · code · name · description · sortOrder · isActive · concurrencyToken · createdAtUtc · modifiedAtUtc · normalizedCode` |
| Selector de **cargo** (paso 2) | `GET /v1/account/companies/legal-representative-position-titles` | ninguno | `publicId · code · name · sortOrder · normalizedCode` |
| Selector de **tipo de representación** (paso 2) | `GET /v1/account/companies/legal-representative-representation-types` | ninguno | `publicId · code · name · sortOrder · normalizedCode` |

⚠️ **No existe un catálogo de tipos de documento.** `documentType` es texto libre validado por la pareja
(país, tipo) — ver §2.3. La lista que hoy pinta el formulario (`Other · DUI · NIT · Pasaporte · Carne de
residente`) **la mantiene el cliente**, no el servidor.

### 2.2 Cuerpo de la petición

```jsonc
{
  "name": "…",                       // requerido · string
  "countryCode": "SV",               // requerido · string  ← el formulario lo deja vacío por defecto (F-02)
  "companyTypePublicId": "…",        // OPCIONAL · uuid|null — el catálogo se resuelve por país
  "initialLegalRepresentative": {
    "firstName": "…",                // requerido · string
    "lastName": "…",                 // requerido · string
    "documentType": "DUI",           // requerido · string libre — ver §2.3
    "documentNumber": "…",           // requerido · string — formato según (país, tipo), ver §2.3
    "positionTitle": "CEO",          // requerido · string libre — el catálogo es sugerencia, no restricción
    "representationType": "…",       // requerido · enum string — ver abajo
    "authorityDescription": null,    // opcional · string|null
    "appointmentInstrument": null,   // opcional · string|null
    "appointmentDate": null,         // opcional · date|null   "2026-08-15"
    "effectiveFrom": "2026-08-15",   // requerido · date       "2026-08-15"
    "effectiveTo": null,             // opcional · date|null
    "email": null,                   // opcional · string|null — verificado: enviarlo vacío no bloquea
    "phone": null                    // opcional · string|null — verificado: enviarlo vacío no bloquea
  }
}
```

**`representationType` — los tres valores que acepta el servidor:**

```
PrimaryLegalRepresentative   ·   AlternateLegalRepresentative   ·   AttorneyInFact
```

✅ **Se puede enviar el `code` del catálogo tal cual.** El catálogo devuelve
`PRIMARYLEGALREPRESENTATIVE` (sin separadores) y el servidor lo acepta: la comparación **ignora
mayúsculas y minúsculas**. Verificado con las cuatro variantes.

❌ **Lo que NO acepta es transformarlo.** `PRIMARY_LEGAL_REPRESENTATIVE` —con guiones bajos— se rechaza
con `400`. Si el cliente normaliza códigos a snake_case en algún punto, este campo se rompe.

> **`isPrimary` no existe en el contrato.** Lo decide el servidor: marca como principal al primer
> representante de la empresa. Un cliente que lo siga mandando no se rompe; el campo se ignora. (F-06)

### 2.3 Reglas de validación que el cliente puede replicar

Todas están en el servidor y devuelven `400`. Se documentan para que el formulario pueda avisar **antes**
de enviar, no para sustituirlas.

| Campo | Regla | Nota para el formulario |
|---|---|---|
| `documentNumber` con `countryCode=SV` y `documentType=DUI` | **9 dígitos** una vez quitados los separadores | `01234567-8` y `012345678` son equivalentes |
| `documentNumber` con `countryCode=SV` y `documentType=NIT` | **14 dígitos** una vez quitados los separadores | `####-######-###-#` |
| `documentNumber`, cualquier otra pareja | `^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$` sobre el valor **sin** quitar separadores | Pasaportes, carnés de residente y países sin regla propia |
| `firstName` · `lastName` | `^[\p{L}][\p{L}\p{N} '.-]{0,99}$` | Admite acentos y eñes; empieza por letra |
| `positionTitle` | `^[\p{L}\p{N}][\p{L}\p{N} '&().,/-]{0,149}$` | **Texto libre**: el catálogo de cargos es una lista de sugerencias, no una restricción |
| `documentType` | máximo 40 caracteres | Sin catálogo en el servidor |

> **Los separadores se descartan antes de validar y antes de comparar duplicados.** `01234567-8` y
> `012345678` son **el mismo documento** para el servidor: enviar uno u otro no cambia el resultado ni
> esquiva la comprobación de unicidad.

### 2.4 Respuesta

`201` con `AccountCompanyDetailResponse`, que **incluye `concurrencyToken` en el cuerpo** (y en `ETag`, que el proxy descarta — ver [00001 / B-03](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location)). Ese token es el que hace falta para el `PUT` posterior.

⚠️ **Las tres fechas vuelven como día, sin hora**: `"2026-08-15"`, no `"2026-08-15T00:00:00Z"`. Cualquier
código del cliente que haga `new Date(...)` sobre ese valor y derive el día **debe revisarse** — es
exactamente el patrón que causó F-03.

### 2.5 Errores — el catálogo real de esta pantalla

El código de negocio viaja en el miembro **`code` de la raíz** del ProblemDetails. **No existe un objeto
`extensions` en el JSON**: el servidor lo escribe en `ProblemDetails.Extensions` y el serializador lo
aplana.

| `code` | HTTP | Cuándo lo dispara esta pantalla |
|---|---|---|
| *(validación de campos)* | `400` | Falta un requerido o el formato no cuadra. El detalle va en `errors`, con **una clave por campo** |
| — | `401` | Sesión vencida o ausente |
| `COMPANY_LIMIT_REACHED` | `409` | **Éste es el `409` del `POST`**: la cuenta alcanzó su límite de empresas activas |
| `COMPANY_NOT_FOUND` | `404` | Solo en las operaciones sobre una empresa existente |
| `COMPANY_OWNERSHIP_FORBIDDEN` | `403` | La empresa no pertenece al usuario del token |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado — aplica al `PUT`, no al `POST` |

> El país o el tipo de empresa inexistentes se responden como **`400` de validación**, no como `404`.

### 2.6 Idioma de los mensajes — **cómo pedir español**

El servidor tiene **1 051 mensajes traducidos** y los sirve, pero la forma de pedirlos tiene una
precedencia que el cliente debe conocer:

```
claim `language` del JWT   →   cabecera Accept-Language   →   inglés
```

⚠️ **Si el usuario tiene un idioma guardado en sus preferencias, la cabecera se ignora.** El claim solo
se emite cuando existe esa preferencia; si no existe, manda `Accept-Language`.

**Consecuencia para el frontend:** un selector de idioma que solo cambie la cabecera **no funcionará**
para un usuario que ya tenga preferencia guardada. Hay que escribirla:

```
PUT  /v1/account/me/preferences      (o PATCH)
GET  /v1/account/me/preferences
```

El token se re-emite con el claim nuevo en el siguiente inicio de sesión o refresco.

## 3. Cobertura de campos

| # | Campo API | ¿En el formulario? | Estado |
|---|---|---|---|
| 1 | `name` | ✅ paso 1 | 🟢 Presente |
| 2 | `countryCode` | ✅ paso 1 | 🔴 **F-02** — parece lleno y está vacío |
| 3 | `companyTypePublicId` | ✅ paso 1 | 🟢 Presente · correctamente opcional y resuelto por país |
| 4–7 | `firstName`, `lastName`, `documentType`, `documentNumber` | ✅ paso 2 | 🟢 Presentes |
| 8–9 | `positionTitle`, `representationType` | ✅ paso 2 | 🟢 Presentes |
| 10–11 | `authorityDescription`, `appointmentInstrument` | ✅ paso 2 | 🟢 Presentes |
| 12 | `appointmentDate` | ✅ paso 2 | 🟢 Presente · ⚠️ renombrado por B-02 |
| 13 | `effectiveFrom` | ✅ paso 2 | 🟡 **F-03** — valor por defecto en día equivocado · ⚠️ renombrado por B-02 |
| 14 | `effectiveTo` | ✅ paso 2 | 🟢 Presente · ⚠️ renombrado por B-02 |
| 15–16 | `email`, `phone` | ✅ paso 2 | 🟡 **F-04** en el teléfono |
| 17 | ~~`isPrimary`~~ | ❌ **AUSENTE** | 🟢 **F-06 cerrado** — el campo salió del contrato; lo decide el servidor (B-04) |

**Cobertura: 16 de 16.** El campo 17 dejó de existir en el contrato: B-04 lo movió al servidor.

---

## 4. Hallazgos de frontend

### 🔴 F-01 — El segundo paso del asistente muestra 26 claves de traducción en crudo

**Severidad:** Alta · **Tipo:** Internacionalización

#### Evidencia

El paso *Legal representative* renderiza literalmente los identificadores del diccionario, no su texto:

```
legalReps.identity          legalReps.firstName        legalReps.lastName
legalReps.documentType      legalReps.selectDocType    legalReps.documentNumber
legalReps.representation    legalReps.positionTitle    legalReps.selectPositionTitle
legalReps.representationType  legalReps.selectRepType  legalReps.authority
legalReps.authorityDescription  legalReps.appointmentInstrument
legalReps.appointmentDate   legalReps.dates            legalReps.effectiveFrom
legalReps.effectiveTo       legalReps.email            legalReps.phone
```

**20 etiquetas visibles + 5 placeholders** (`legalReps.firstNamePlaceholder`, `…lastNamePlaceholder`, `…documentNumberPlaceholder`, `…emailPlaceholder`, `…phonePlaceholder`) **+ el título de sección**. El paso 3 (*Confirmación*) arrastra las mismas.

#### Medición del alcance — los dos idiomas

Se repitió el asistente con `clarihr-ui-lang = es`, **sin enviarlo**:

| Idioma | Marco del asistente | Campos del representante |
|---|---|---|
| `en` | ✅ «Legal representative», «Register the initial legal representative…» | ❌ **21 claves + 5 placeholders** |
| `es` | ✅ «Representante legal», «Registra al representante legal inicial…» | ❌ **21 claves + 5 placeholders** |

**Idéntico en ambos.** No es un hueco de un idioma: **falta el namespace `legalReps.*` entero en los dos diccionarios**. Que el marco sí traduzca en los dos confirma que el mecanismo de i18n funciona y que lo que falta son las entradas.

#### Impacto

Es **la primera pantalla que ve un cliente nuevo**, y la mitad de ella parece rota. Pide un documento de identidad y un cargo legal sin decir con qué palabras — un usuario no técnico no puede saber qué es `legalReps.appointmentInstrument`.

#### Contrato para el frontend

**No interviene ningún endpoint.** Es puramente de presentación: faltan las entradas del namespace `legalReps.*` en los diccionarios de `en` y `es`. Los nombres de los campos del API (§2.2) sirven de referencia uno a uno para redactarlas.

#### Ajuste pedido al frontend

Añadir el namespace completo a los dos diccionarios. **Y añadir una comprobación que falle la compilación si una clave se renderiza sin traducir** — este fallo es invisible en revisión de código y evidente en pantalla, que es la peor combinación.

---

### 🔴 F-02 — El campo de país parece tener un valor elegido y está vacío

**Severidad:** Alta · **Tipo:** Validación / afordancia

#### Evidencia

El desplegable *Country code* muestra un código de país plausible antes de que el usuario toque nada:

| Idioma de la interfaz | Lo que muestra el campo |
|---|---|
| `en` | **`US`** |
| `es` | **`MX`** |

No es un valor: al pulsar *Siguiente* sin tocarlo, el campo se marca en rojo y aparece **«El código de país es obligatorio»**.

**El contraste está en el mismo formulario.** El otro desplegable del paso 1, *Tipo de empresa*, usa un placeholder que se lee como tal: **«Selecciona un tipo»**. El de país usa un código que se lee como una elección.

#### ¿Debería estar? — el placeholder sí; ese texto, no

El campo es requerido y debe estar. Lo que no debe es **disfrazar el vacío de valor**. Que el texto cambie con el idioma delata su origen: se deriva de la configuración regional del navegador, que no tiene por qué coincidir con el país donde se constituye la empresa.

#### Impacto

**El usuario no ve un campo pendiente.** Recorre el formulario, lee «US» o «MX» como algo ya resuelto y se topa con un error que no esperaba. En el mejor caso pierde un intento; en el peor —si algún día el campo dejara de ser requerido— registraría la empresa en el país equivocado, y **el país gobierna todos los catálogos** del producto: tipos de empresa, tipos documentales, rangos de seguro, tramos de renta.

#### Contrato para el frontend

```jsonc
POST /v1/account/companies
{ "countryCode": "SV", … }     // requerido · string
```

Sin él, `400` con la clave `countryCode`.

**La lista sale de `GET /v1/account/companies/countries`** — sin parámetros, array plano, con
`code · name · sortOrder`. Hay campo de orden: **respetarlo** en vez de ordenar en cliente.

⚠️ **Ese catálogo NO trae prefijo telefónico.** Sus campos son `publicId · code · name · sortOrder ·
defaultLocale · normalizedCode`. La correspondencia país → prefijo que pide **F-04** la tiene que
mantener el cliente; el servidor no la publica.

#### Ajuste pedido al frontend

Que el placeholder se lea como placeholder —«Selecciona un país», igual que el campo de al lado— y **no como un código de país**. Si además se quiere una preselección real, que sea un valor de verdad elegido y no un texto que solo lo aparenta.

---

### 🟡 F-03 — La fecha de vigencia se precarga con el día UTC, no con el día local

**Severidad:** Media · **Tipo:** Fechas

#### Evidencia

Medido en el navegador, en el momento de abrir el paso 2:

| | |
|---|---|
| Zona horaria del navegador | `America/El_Salvador` (UTC−6) |
| Hora local | **sáb 15 ago 2026, 18:07** |
| Día local | **2026-08-15** |
| Día UTC | **2026-08-16** |
| **Valor precargado en `effectiveFrom`** | **Aug 16, 2026** ❌ |

El campo trae **el día UTC**. En El Salvador eso significa que **de las 18:00 a las 23:59 hora local —seis horas de cada día, el 25 %— el valor por defecto es mañana.**

#### Causa probable

El día se calcula sobre el instante en UTC (la forma habitual es `new Date().toISOString().slice(0,10)`) en vez de sobre el calendario local. **Es la misma trampa que ya está documentada como regla del proyecto**: lo que es «el día en que pasó» no se deriva de un instante.

#### Impacto

La vigencia de un nombramiento legal es **un dato con efecto jurídico**. Un representante que aparece vigente desde el 16 cuando se nombró el 15 es un error de registro, y **nadie lo va a notar**: el campo viene relleno con algo verosímil y el usuario acepta el valor por defecto, que es justo para lo que sirve un valor por defecto.

**Y sesga en una sola dirección**: siempre hacia adelante, nunca hacia atrás. En un huso al oeste de Greenwich el error solo puede adelantar la fecha.

#### Contrato para el frontend

**Antes** (era la raíz del defecto):

```jsonc
"effectiveFromUtc": "2026-08-15T00:00:00Z"   // DateTime · REQUERIDO
```

⚠️ **El contrato pedía un instante (`DateTime`) para algo que es un día.** Eso obligaba al frontend a inventarse una hora, y era la raíz del problema — levantado como [**00000 / B-02**](../ComentariosPruebasBackend/00000-CreateCompany.md#3-b-02--tres-fechas-de-calendario-viajan-como-datetime-contra-la-convención-dateonly-del-propio-producto).

**Ahora** (🟢 B-02 resuelto el 2026-08-16 — ver §5 para el cambio completo):

```jsonc
"effectiveFrom": "2026-08-15"                // date · REQUERIDO · campo renombrado
```

#### Ajuste pedido al frontend

**Calcular el día por defecto sobre el calendario local, no sobre el instante UTC.** Eso sigue siendo necesario: es un defecto del cliente y B-02 no lo arregla solo.

**Lo que ya NO hace falta** es la precaución de enviar «medianoche UTC del día local»: el campo es un día y el servidor lo lee como está escrito, sin desplazarlo. Basta con mandar `"2026-08-15"`.

> **Precisión medida en el backend** (`00000 / B-02` §3.4): mientras el campo fue un instante, la ventana de falla eran **las últimas seis horas del día local**, no el día entero. Desde El Salvador (−06:00), cualquier hora entre `00:00` y `17:59` producía el día correcto; solo de `18:00` en adelante se corría. Medianoche local **no** se corría — la primera redacción del hallazgo de backend decía lo contrario y quedó corregida.

> **Aplica a los tres campos de fecha de esta pantalla**, no solo a este: `appointmentDate` y `effectiveTo` comparten tipo y tratamiento.

---

### 🟡 F-04 — El teléfono asume 🇺🇸 +1 aunque la empresa se registre en El Salvador

**Severidad:** Media · **Tipo:** Coherencia de datos

#### Evidencia

El campo de teléfono del paso 2 muestra el selector de prefijo en **🇺🇸 +1**, con el país de la empresa ya fijado en **El Salvador** dos campos antes. El prefijo salvadoreño es **+503**.

#### ¿Debería estar el selector? Sí — con otro valor por defecto

Un selector de prefijo es correcto: un representante legal puede tener un teléfono extranjero. Lo que no es correcto es **ignorar el dato que el propio formulario acaba de capturar**. El país de la empresa es la mejor conjetura disponible, y está a dos campos de distancia.

#### Impacto

Un número salvadoreño de 8 dígitos guardado como `+1 ########` es un teléfono estadounidense que no existe. **No hay ninguna señal de error**: es un valor por defecto plausible sobre un campo opcional.

#### Contrato para el frontend

```jsonc
"phone": "+50322000000"    // opcional · string
```

No hay validación de prefijo en el servidor: **el frontend es el único punto donde esto se puede acertar.**

#### Ajuste pedido al frontend

Que el prefijo por defecto se derive del `countryCode` ya elegido, dejándolo cambiable.

---

### 🔵 F-05 — Dos catálogos de la pantalla se presentan mal ordenados o sin traducir

**Severidad:** Baja · **Tipo:** Presentación de catálogos

#### Evidencia

**(a) Un código crudo entre etiquetas.** En *Position title*:

```
OWNER  ·  CEO  ·  Executive Management  ·  Human Resources  ·  Finance  ·  Accounting
```

`OWNER` está en mayúsculas de código mientras el resto son etiquetas legibles. `CEO` también va en mayúsculas, pero por ser una sigla real; `OWNER` no lo es — debería leerse «Owner» / «Propietario».

**(b) «Otro» encabeza la lista.** En *Document type*:

```
Other  ·  DUI  ·  NIT  ·  Pasaporte  ·  Carne de residente
```

La opción residual aparece **antes** que la que va a elegir prácticamente todo el mundo en El Salvador.

#### Impacto

Menor pero acumulativo: son las dos primeras listas que abre un cliente nuevo, y las dos transmiten descuido.

#### Contrato para el frontend — **medición hecha el 2026-08-21**

El montaje que este hallazgo dejó planificado ya se ejecutó. **Las dos mitades resultaron tener dueños
distintos**, y conviene separarlas porque solo una es del frontend.

**(a) `OWNER` — es dato del servidor, no del cliente.** El catálogo de cargos devuelve orden y etiqueta,
y el frontend los está pintando bien. El problema es lo que trae:

| `code` | `name` | `sortOrder` |
|---|---|---|
| `OWNER` | **`OWNER`** ← la etiqueta es literalmente el código | 1 |
| `CEO` | `CEO` | 2 |
| `EXECUTIVE_MANAGEMENT` | `Executive Management` | 3 |
| `HUMAN_RESOURCES` | `Human Resources` | 4 |

**El frontend no tiene nada que arreglar aquí**: está mostrando el `name` que recibe. La corrección es de
datos sembrados y **cruza al backend** — hay que levantarla allí; no se levanta desde este documento
porque no corresponde a una prueba de frontend.

> Y hay un segundo hecho en la misma tabla: **todas las etiquetas están en inglés**
> («Executive Management», «Human Resources»), igual que las de tipo de representación
> («Primary Legal Representative»). Es el mismo asunto de datos.

**(b) «Other» primero — esto sí es del frontend, entero.** **No existe un catálogo de tipos de documento
en el servidor** (§2.1): la lista `Other · DUI · NIT · Pasaporte · Carne de residente` la mantiene el
cliente. El orden es una decisión del cliente y se arregla en el cliente.

#### Ajuste pedido al frontend

**Solo la mitad (b):** reordenar la lista de tipos de documento que el propio cliente mantiene, poniendo
la opción residual al final. **Y respetar `sortOrder`** en los catálogos que sí vienen del servidor, en
vez de reordenarlos.

Para la mitad (a) no hay ajuste de cliente: seguir pintando `name` es lo correcto.

---

### 🟢 F-06 — `isPrimary` no está en el formulario: **cerrado, y el servidor se hizo cargo**

**Severidad:** Informativo · **Tipo:** Cobertura de campos · **Estado:** 🟢 Resuelto en el backend (2026-08-16)

#### Evidencia

El contrato aceptaba `isPrimary` en el representante inicial. El formulario no lo ofrecía.

#### El veredicto de la corrida — y en qué se quedó corto

**El argumento a favor de omitirlo era bueno:** es el representante *inicial*, el primero y único de una empresa recién creada. Preguntar «¿es el principal?» cuando no hay otro es ruido.

**El argumento en contra era de contrato:** `isPrimary` era `bool?` con valor por defecto `null` en un `record` posicional, y **omitirlo no aplicaba el valor por defecto: llegaba `null`**. La empresa quedaba con un representante sin marcar.

La conclusión de entonces fue *«el frontend tiene que enviarlo»*. **Resultó ser la solución equivocada, aunque el diagnóstico fuera correcto**: trasladaba al cliente una regla que es del servidor, y la pregunta que quedó anotada para el backend —si el servidor debería marcarlo él— era la buena.

#### Resolución

Decisión de negocio tomada el 2026-08-16: **el servidor marca como principal al primer representante**. El campo **salió del contrato** — ver [`00000 / B-04`](../ComentariosPruebasBackend/00000-CreateCompany.md#5-b-04--el-representante-inicial-puede-quedar-sin-marcar-como-principal).

#### Ajuste pedido al frontend

**Ninguno.** No hay control que agregar ni campo que enviar. Si el cliente sigue mandando `isPrimary`, se ignora sin romper nada.

> **Lo que el backend encontró al ejecutarlo:** no era un agujero sino **tres**. Crear el primer representante con `isPrimary: false` tampoco lo promovía, y dar de baja al principal dejaba la empresa **sin ninguno**. Los tres están cerrados y cubiertos por un test que recorre el ciclo de vida completo.

---

## 5. Hallazgos de backend

> El detalle completo vive en [`ComentariosPruebasBackend/00000-CreateCompany`](../ComentariosPruebasBackend/00000-CreateCompany.md).

| ID | Sev. | Estado | Hallazgo | Origen |
|---|---|---|---|---|
| [**B-01**](../ComentariosPruebasBackend/00000-CreateCompany.md#2-b-01--el-camino-patch-se-salta-la-frontera-de-fechas-diez-lectores-leen-el-jsonelement-en-crudo) | 🔴 Alta | 🟢 **Resuelto** | **El camino `PATCH` se salta la frontera de fechas**: diez lectores leían el `JsonElement` en crudo. `PATCH /appointmentDateUtc` con `"2026-08-15"` daba `500` | Revisión de código |
| [**B-02**](../ComentariosPruebasBackend/00000-CreateCompany.md#3-b-02--tres-fechas-de-calendario-viajan-como-datetime-contra-la-convención-dateonly-del-propio-producto) | 🟡 Media | 🟢 **Resuelto** ⚠️ rompe contrato | **Tres fechas de calendario viajaban como `DateTime`** contra la convención `DateOnly` del propio producto (228 usos en el dominio) | F-03 |
| [**B-03**](../ComentariosPruebasBackend/00000-CreateCompany.md#4-b-03--la-regla-de-tipos-de-fecha-no-está-escrita-en-las-definiciones-técnicas) | 🟡 Media | 🟢 **Resuelto** | **La regla día↔instante no estaba escrita** ni tenía guardrail: por eso la entidad derivó | Causa raíz de B-01 y B-02 |
| [**B-04**](../ComentariosPruebasBackend/00000-CreateCompany.md#5-b-04--el-representante-inicial-puede-quedar-sin-marcar-como-principal) | 🔵 Baja | 🟢 **Resuelto** ⚠️ cambia contrato | **El representante inicial podía quedar con `is_primary = NULL`**. Resuelto: el servidor lo marca como principal. Eran **tres** caminos, no uno | F-06 |

### ⚠️ Cambio de contrato de B-02 — acción requerida del frontend

**Los tres campos de fecha del representante legal cambiaron de nombre y de forma** (2026-08-16). Afecta a esta pantalla y a la de representantes legales del Paso 1.

| Antes | Ahora | Forma |
|---|---|---|
| `appointmentDateUtc` | **`appointmentDate`** | `"2026-08-15"` en vez de `"2026-08-15T00:00:00Z"` |
| `effectiveFromUtc` | **`effectiveFrom`** | idem |
| `effectiveToUtc` | **`effectiveTo`** | idem |

Aplica a `POST /account/companies` (dentro de `initialLegalRepresentative`), a `POST`/`PUT` de `legal-representatives`, a las respuestas, y a la ruta del `PATCH` (`/appointmentDate`).

**En la entrada el cambio es tolerante:** el servidor sigue aceptando `"2026-08-15T00:00:00Z"` y hasta la forma con offset, y se queda con el día que nombra el texto **sin desplazarlo**. Lo que sí es obligatorio es **usar los nombres nuevos**: con los viejos, `effectiveFrom` llega vacío y la respuesta es `400`.

**En la salida el cambio no es tolerante:** las respuestas ahora traen `"2026-08-15"`, sin parte de hora. Cualquier código del cliente que haga `new Date(...)` sobre ese valor y luego derive el día **debe revisarse** — es exactamente el patrón que causó F-03.

> ✅ **F-03 queda cerrado de raíz.** Ya no hay que enviar «medianoche UTC del día local»: el campo es un día y se lee como está escrito. El ajuste que se pidió en F-03 sigue siendo correcto, pero ya no es una precaución necesaria.

**B-01 y B-03 no tocan el contrato**: mismas peticiones, sin el `500`.

> **F-06 queda respondida.** La pregunta que dejó abierta —si el servidor debería marcar como principal al primer representante— se comprobó contra el handler: **no lo hace**. Está levantada como `B-04`.

---

## 6. Ciclo cubierto

| Operación | Vía | Resultado |
|---|---|---|
| **Crear** | Interfaz — asistente completo | 🟢 `201` · empresa activa y seleccionada |
| **Leer** | Interfaz — tarjeta en *Your companies* | 🟢 Correcto |
| **Validación de requeridos** | Interfaz — *Siguiente* con el país vacío | 🟢 Bloquea, con mensaje en español (**F-02** es cómo se ve el campo, no que falle) |
| **Opcionales vacíos** | Interfaz — email y teléfono sin llenar | 🟢 `201` · lo opcional es opcional |
| **Editar / desactivar / eliminar** | — | ⬜ **No probado** — ver §7 |

---

## 7. Qué NO se probó, y cómo se va a medir

| Pendiente | Montaje | Cuándo |
|---|---|---|
| **Editar y desactivar la empresa** | `PUT /v1/account/companies/{id}` con `If-Match`, y el ciclo de estado. Se hará **sobre la empresa desechable**, que existe justo para esto | Antes de cerrar la corrida |
| **Manejo de `403`** | Segundo usuario con rol restringido en la empresa desechable. **Ya no está bloqueado**: la empresa existe | Próxima sesión |

**Nada queda sin plan.** Ningún pendiente depende de un insumo externo.

> **Dos de los cuatro pendientes se cerraron el 2026-08-21 leyendo el servidor, sin volver a probar la
> pantalla:** qué dispara el `409` (es `COMPANY_LIMIT_REACHED` — §2.5) y el orden y las etiquetas de los
> catálogos (F-05). Los dos eran preguntas de contrato, no de comportamiento, y por eso se podían
> responder sin ambiente.

---

## 8. Estado en que quedó la empresa desechable

| | |
|---|---|
| **Nombre** | `ZZZ DESECHABLE - Medicion de limites` |
| **País** | El Salvador · Sociedad Anónima de Capital Variable |
| **Representante** | Prueba Desechable · DUI `01234567-8` · CEO · Primary |
| **Contenido** | 1 tipo de unidad (`NIVEL`) · **18 unidades organizativas** |
| **Estado del árbol** | ⚠️ **Profundidad máxima 16, por encima del límite declarado de 15** — dejado a propósito como caso de regresión de [00003 / B-02](../ComentariosPruebasBackend/00003-OrgUnits.md#3-b-02--move-no-valida-la-altura-del-subárbol-el-árbol-supera-el-límite-que-el-propio-servidor-declara) |

> **No borrar sin avisar.** El árbol inválido es la evidencia viva del hallazgo más grave del Paso 3.

---

## 9. Para reintegrar y publicar — revisión de esta pantalla

> **Cómo leer esta lista.** No sabemos qué tiene hoy el cliente, así que **no está escrita como «cambios»
> sino como comprobaciones**: cada punto es algo que el servidor hace de una forma concreta y que el
> formulario tiene que estar haciendo igual. Si ya coincide, no hay nada que tocar.
>
> Cubre **solo el contrato de esta pantalla**. Los seis hallazgos (§4) son trabajo aparte: esto es lo que
> hace falta para que la pantalla **funcione**; aquéllos son lo que hace falta para que esté **bien**.

### 9.1 Comprobaciones de contrato — si alguna no coincide, la pantalla falla

| # | Qué comprobar | Cómo se ve si está mal |
|---|---|---|
| 1 | Los tres campos de fecha se llaman **`appointmentDate`, `effectiveFrom`, `effectiveTo`** *(sin el sufijo `Utc`)* | `400` con `effectiveFrom` marcado como requerido, aunque el formulario esté lleno |
| 2 | Se envían como **día**: `"2026-08-15"` | La entrada tolera el instante antiguo; **no es urgente**, pero el día es lo correcto |
| 3 | Al **leer** la respuesta, las fechas vienen **sin hora** | Un `new Date("2026-08-15")` seguido de `getDate()` puede devolver el día anterior según el huso |
| 4 | **`isPrimary` ya no existe** en `initialLegalRepresentative` | No rompe nada: si se sigue enviando, se ignora |
| 5 | `representationType` viaja como **el `code` del catálogo, sin transformar** | `PRIMARY_LEGAL_REPRESENTATIVE` → `400`. `PRIMARYLEGALREPRESENTATIVE` → correcto |
| 6 | El código de error se lee de **`code` en la raíz**, no de `extensions.code` | El cliente no encuentra el código y muestra un mensaje genérico |

### 9.2 Comprobaciones de datos — silenciosas, y por eso peores

| # | Qué comprobar | Por qué importa ahora |
|---|---|---|
| 7 | **Ningún emparejamiento por `name` de catálogo** — usar siempre `code` | Los nombres de catálogo **ganaron tildes y eñes** el 2026-08-21: `Sociedad Anónima de Capital Variable`, `Banco Agrícola`, `Cabañas`. Un `if (name === "Sociedad Anonima…")` dejó de casar **sin dar error** |
| 8 | El **selector de idioma escribe la preferencia**, no solo la cabecera | Con preferencia guardada, `Accept-Language` **se ignora** (§2.6). El usuario ve el idioma que no eligió |
| 9 | Se respeta **`sortOrder`** de los catálogos en vez de reordenar en cliente | Los cuatro catálogos lo traen (§2.1) |

### 9.3 Lo que NO cambió y no hay que tocar

- La ruta, el método y la autorización: `POST /v1/account/companies`, **sin permiso RBAC que declarar**.
- La forma del cuerpo salvo los nombres de fecha y la salida de `isPrimary`.
- Los campos opcionales siguen siendo opcionales: `email` y `phone` vacíos devuelven `201`.
- `companyTypePublicId` sigue siendo opcional.

### 9.4 Orden sugerido para volver a probar de cero

1. **Publicar con las comprobaciones de §9.1 resueltas.** Sin ellas la pantalla no completa el alta y no
   se puede llegar al Paso 1.
2. **§9.2 en la misma versión si es barato**; si no, anotarlo: son fallos silenciosos, no bloqueantes.
3. **Los hallazgos F-01…F-05 pueden ir después.** Ninguno impide crear la empresa — F-02 cuesta un
   intento al usuario, F-01 hace la pantalla ilegible, pero **el alta funciona**.
4. Al reabrir la corrida se validará la pantalla completa otra vez: los seis hallazgos se vuelven a
   comprobar de cero, no se dan por cerrados sin verlos.

> ⚠️ **Este documento no se ha vuelto a probar contra el ambiente.** Lo revalidado el 2026-08-21 es **el
> contrato del servidor**, leído del código y del contrato publicado. Los hallazgos F-01…F-06 siguen tal
> como se observaron el 2026-08-15: no se han añadido ni retirado, porque no se ha repetido la corrida.
