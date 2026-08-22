# 00002 — UnitTypes · Hallazgos de backend

| | |
|---|---|
| **ID** | 00002-UnitTypes |
| **Documento espejo** | [`ComentariosPruebasFrontend/00002-UnitTypes`](../ComentariosPruebasFrontend/00002-UnitTypes.md) |
| **Paso probado** | Configuración guiada (`/setup`) → **Paso 2: Tipos de unidad** |
| **Pantalla que los destapó** | `/org-units?tab=unit-types` |
| **Fecha** | 2026-08-14 |
| **Ambiente** | Producción — `https://dashboard.clarihr.com` |

Hallazgos de **backend** detectados durante la prueba del Paso 2. El comportamiento del cliente y su ajuste están en el documento espejo; aquí va solo lo que se resuelve del lado del servidor.

---

## 1. Resumen

| ID | Sev. | Hallazgo | Componente | Origen | Alcance | Estado |
|---|---|---|---|---|---|---|
| [B-01](#2-b-01--el-español-que-se-sirve-estaba-roto-199-mensajes-en-dos-idiomas-y-320-sin-tildes) | 🟡 Media | Mensajes sin localizar — **el diagnóstico era falso; el defecto real era otro** | Application · Infrastructure | §2.7, §5.4, F-02 | 519 mensajes | 🟢 **Resuelto** — 2026-08-18 (§2.9) |
| [B-02](#3-b-02--la-clave-del-error-de-búsqueda-es-search-pero-el-parámetro-público-es-q) | 🔵 Baja | La clave del error de búsqueda es `search`, pero el parámetro público es `q` | API · varios controllers | F-01, F-03 · 00003 | **50 sitios** (medido) | ⛔ **Descartado** — se resuelve en [00005 / B-02](00005-WorkCenters.md#3-b-02--la-clave-del-error-nombra-el-campo-interno-y-no-el-público-cuarto-caso-y-el-primero-en-el-cuerpo) |
| [B-03](#4-b-03--el-mensaje-de-formato-no-dice-cuál-es-el-formato-y-filtra-nombres-de-propiedad) | 🔵 Baja | El mensaje de formato no dice cuál es el formato | Application · validadores | Residual de B-01 | **46 sitios** + 39 mensajes interpolados | 🟢 **Resuelto** |
| [B-04](#5-b-04--los-nombres-de-propiedad-siguen-en-inglés-dentro-de-mensajes-en-español) | 🔵 Baja | Los nombres de propiedad siguen en inglés dentro de mensajes en español | Application · FluentValidation | Punto 4 de B-01 §2.4 | Mecanismo + 45 de 431 etiquetas | 🟢 **Resuelto** |

**Ninguno bloquea al frontend.**

> **Cierre del documento — actualizado 2026-08-18.** B-02 sigue ⛔ **Descartado** porque se resuelve en otro documento, no porque se abandone. **B-01 ya NO lo está**: se descartó, se reabrió con una medición equivocada, y al medirlo de verdad resultó tener un defecto distinto y peor del que decía su título — 🟢 Resuelto en §2.9.
>
> ⚠️ **Pero la sustitución de B-01 se llevaba trabajo real por delante.** `00003 / B-03` es **solo el canal de idioma**; no cubre que `"Code format is invalid."` no diga cuál es el formato ni que `'Sort Order'` filtre el nombre interno de la propiedad. Arreglar el idioma no arregla eso —el mensaje **ni siquiera tiene clave en el `.resx`**, así que seguiría en inglés—. Ese residual se levanta aquí como **B-03**.

### Nota de contraste — lo que este módulo hace bien

Vale registrarlo porque acota un hallazgo anterior:

- **Los `400` sí vienen con la clave del campo** (`code`, `name`, `sortOrder`), porque los validadores usan expresiones directas (`RuleFor(c => c.Code)`). El problema de la clave vacía `""` de [**00001 / B-02**](00001-CompanyLegalProfile.md#3-b-02--el-400-de-validación-agrupa-todos-los-mensajes-bajo-la-clave-vacía) **no afecta a este módulo**.
- **El recurso expone `allowedActions`**: el controller lleva `[ResourceActions(ORG_STRUCTURE_CATALOGS)]` y los DTO implementan `ISupportsAllowedActions`. Es exactamente el cambio que se propone en [**00001 / B-01**](00001-CompanyLegalProfile.md#2-b-01--el-recurso-de-perfil-legal-no-expone-allowedactions) para el perfil legal, ya funcionando aquí — y el frontend ya lo consume (`IncludeAllowedActions=true`).
- **Las carreras de código duplicado se mapean a `409` limpio**, no a `500`, mediante el guard `OrgStructureCatalogConstraintViolations` sobre el índice único.

---

## 2. B-01 — El español que se sirve estaba roto: 199 mensajes en dos idiomas y 320 sin tildes

> ✅ **RECTIFICADO Y RESUELTO — 2026-08-18.** Este hallazgo se descartó, se reabrió, y **la reabertura estaba mal medida**. La rectificación importa más que el arreglo, porque explica cómo se llegó a un número falso.
>
> | Lo que afirmaba §2.0 al reabrirse | Lo medido el 2026-08-18 |
> |---|---|
> | «hay **0 claves** `validation.message.*` en el `.resx`» | ❌ Había **161** ya commiteadas en `HEAD` |
> | «cobertura del **5 %**: `NotEmpty`, `MaximumLength`, `GreaterThan`… no casan ningún patrón» | ❌ FluentValidation 11.11 **traduce al español de fábrica**: 12 de 12 validadores probados |
> | «la familia de validación sigue en inglés» | ❌ Sale en español por el cable, verificado con `Accept-Language: es` |
>
> **La causa del error de medición:** §2.0 contó *usos de validador* contra los patrones del traductor manual y dedujo la cobertura. Nunca hizo la petición. Cuando se hizo —después de que [00003 / B-03](00003-OrgUnits.md#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar) destapara el claim `language` que tapaba la cabecera— la respuesta ya venía en español. **Contar código no es medir comportamiento.**
>
> **Pero al medir de verdad apareció un defecto peor**, que la ficha no nombraba: la traducción existía y estaba **rota**. Ver §2.0.

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-18, verificado (§2.9) |
| **Componente** | Infrastructure · `BackendMessages.es.resx` · `ResourceBackendMessageLocalizer` |
| **Origen** | §2.7, §5.4 y hallazgo **F-02** del documento espejo |
| **Alcance** | **519 mensajes corregidos** de 1 054: 199 mezclaban idiomas · 320 perdían tildes o eñes |

### 2.0 Lo que estaba mal de verdad — medido el 2026-08-18

El canal funciona y sirve español. Lo que nadie había mirado es **qué dice** ese español.

#### a) 199 mensajes mezclaban los dos idiomas

Huella de una pasada de traducción automática que tradujo los verbos y dejó los sustantivos del dominio:

| Antes | Ahora |
|---|---|
| `Otro cost center ya usa code solicitado.` | `Otro centro de costo ya usa el código solicitado.` |
| `No tienes permiso para acceder a commercial addon administration.` | `No tienes permiso para acceder a la administración de complementos comerciales.` |
| `La cuenta actual no puede reactivate this company because the active company limit has been reached.` | `La cuenta actual no puede reactivar esta empresa porque se alcanzó el límite de empresas activas.` |
| `current user context no es válido.` | `El contexto del usuario actual no es válido.` |

El tercero es el peor: la frase queda **ininteligible en los dos idiomas**.

#### b) 320 mensajes perdían tildes o eñes

Y uno cambiaba de significado:

> `El filtro de mes requiere un ano explicito.`

En español eso no dice «año». También `contrasena` por «contraseña» y `compania` (18 veces) por «compañía».

#### c) `BackendMessages.es-SV.resx` es inalcanzable

`RequestLanguageResolver.TryNormalizeLanguage` **corta la región** —`es-SV` → `es`— y `ResolveCulture` devuelve siempre la cultura neutra. `CurrentUICulture` nunca vale `es-SV`, y la reserva de recursos del `ResourceManager` va de específico a neutro, **nunca al revés**: ese archivo no se consulta jamás.

Sus 23 claves eran, precisamente, **las versiones bien acentuadas** de mensajes que el `es` servido tenía sin acentos. El trabajo correcto estaba escrito en el único archivo que nadie lee.

⚠️ **Y unos 20 planes técnicos del repositorio instruyen añadir cada error a los tres `.resx`**, varios afirmando que olvidar el `es-SV` «rompe el build». **Es falso**: `BackendMessageLocalizationTests` solo compara inglés y `es`. Esa convención fantasma explica por qué el archivo tiene 23 claves sueltas y no 1 054.

#### d) Nada vigilaba la calidad

`BackendMessageLocalizationTests` comprueba que cada código **tenga** entrada, no qué dice. Una clave presente con texto roto pasaba por traducida — que es exactamente cómo 519 mensajes malos convivieron con un guardrail en verde.

### 2.1 Evidencia

Afecta a **dos familias de mensaje**, las dos con `Accept-Language: es`.

**a) Mensajes de validación** — `POST /v1/unit-types` con cuerpo inválido:

```jsonc
{
  "status": 400,
  "code": "common.validation",
  "errors": {
    "code":      ["'Code' must not be empty.", "Code format is invalid."],
    "name":      ["'Name' must not be empty."],
    "sortOrder": ["'Sort Order' must be greater than or equal to '0'."]
  }
}
```

Las claves están bien. **Los mensajes están en inglés**, y exponen los nombres de propiedad en inglés (`'Code'`, `'Sort Order'`).

**b) Títulos de conflicto** — `POST` con un código ya existente:

```jsonc
{
  "status": 409,
  "code": "ORG_STRUCTURE_CATALOG_CODE_CONFLICT",
  "title": "Another catalog item already uses the requested code."
}
```

Ese texto es el que el frontend pinta tal cual en su banner de error. **Verificado en la interfaz** (§5.4 del documento espejo). Los `Error` estáticos de `OrgStructureCatalogErrors` están todos escritos en inglés.

**Contraste directo:** el perfil legal (Paso 1), con la misma cabecera, sí responde en español:

```jsonc
"errors": { "": ["El NIT patronal debe seguir el formato ####-######-###-#."] }
```

Así que la localización existe en el producto; **este módulo no la usa**.

### 2.2 Causa

**Los mensajes de validación:** los validadores de `OrgStructureCatalogsAdministration` no aplican `.WithMessage(...)` con mensajes localizados, así que FluentValidation cae en sus plantillas por defecto, que están en inglés y que sustituyen el nombre de la propiedad tal cual (`'Code'`, `'Sort Order'`).

**Los títulos de conflicto:** `OrgStructureCatalogErrors` declara los `Error` con su descripción en inglés, escrita a mano en el propio archivo:

```csharp
public static readonly Error CatalogCodeConflict = new(
    "ORG_STRUCTURE_CATALOG_CODE_CONFLICT",
    "Another catalog item already uses the requested code.",
    ErrorType.Conflict);
```

El caso del formato es aparte: `Code format is invalid.` **sí** es un mensaje escrito a mano, pero está en inglés y además **no dice cuál es el formato**.

### 2.3 Impacto

1. **El usuario ve texto en inglés dentro de una interfaz en español.** El frontend no puede corregirlo: reenvía el texto del servidor tal cual, tanto en el banner de validación como en el de conflicto.
2. **Los mensajes filtran nombres internos** (`'Sort Order'` en vez de la etiqueta del negocio).
3. **`Code format is invalid.` no es accionable.** El usuario no sabe qué corregir. Compárese con el perfil legal, que sí indica el patrón esperado (`####-######-###-#`). El frontend termina obligado a escribir su propio mensaje en cliente para que el usuario entienda — es lo que se pide en **F-02** del documento espejo.

### 2.4 Propuesta — **qué de esto resultó necesario**

La propuesta original tenía cuatro puntos. Al medir, dos ya estaban hechos y dos siguen vivos en otra ficha:

| Punto | Veredicto |
|---|---|
| 1. Localizar los mensajes de validación | ✅ **Ya funcionaba.** FluentValidation traduce de fábrica y el `.resx` tenía 161 claves `validation.message.*` |
| 2. Localizar las descripciones de `OrgStructureCatalogErrors` | ✅ **Ya tenían entrada**… con el texto roto. Ése era el defecto (§2.0) |
| 3. Explicitar cuál es el formato en el mensaje del código | 🔲 **Sigue abierto** → [B-03](#4-b-03--el-mensaje-de-formato-no-dice-cuál-es-el-formato-y-filtra-nombres-de-propiedad) |
| 4. Usar etiquetas de negocio en vez de `'Sort Order'` | 🔲 **Sigue abierto** → [B-03](#4-b-03--el-mensaje-de-formato-no-dice-cuál-es-el-formato-y-filtra-nombres-de-propiedad) |

**Lo que de verdad hacía falta no estaba en la lista**: reparar 519 mensajes y poner algo que vigile el texto, no solo la presencia de la clave.

### 2.5 Compatibilidad

**No rompe el contrato.** Cambian los textos, no la forma ni las claves. Un cliente que hoy reconociera los mensajes **por su texto en inglés** se rompería — pero eso sería una mala práctica que conviene erradicar, y el frontend no lo está haciendo (los pinta tal cual).

No afecta `openapi.yaml`.

### 2.6 Alcance a revisar

**Antes de decidir, medir.** Conviene listar qué otros módulos devuelven mensajes de validación sin localizar. La corrida del Paso 1 demostró que el perfil legal **sí** los localiza, así que la carencia no es general: hay módulos de los dos tipos.

**Medido — 2026-08-15, corrida del Paso 3** ([`00003-OrgUnits`](00003-OrgUnits.md#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar)): `OrgUnits` **tampoco localiza**, ni la validación ni los títulos de conflicto.

| Módulo | ¿Localiza? | Corrida |
|---|---|---|
| Compliance (perfil legal) | 🟢 Sí | Paso 1 |
| OrgStructureCatalogs | 🔴 No | Paso 2 |
| OrgUnits | 🔴 No | Paso 3 |

Van **dos módulos confirmados sin localizar**, y son los dos últimos probados. En vez de seguir levantando el mismo hallazgo en cada corrida, conviene **contar de una vez cuántos módulos usan `BackendMessages` y cuántos no** — una búsqueda responde el alcance completo y permite decidir con el costo real sobre la mesa.

### 2.7 Vía alterna vigente

Ninguna del lado del servidor. El frontend puede mitigar validando en cliente antes de enviar (**F-02**), pero cualquier error que llegue del servidor se mostrará en inglés.

### 2.9 Lo que se hizo

| Pieza | Dónde |
|---|---|
| 199 mensajes retraducidos desde el inglés original | `Localization/BackendMessages.es.resx` |
| 320 correcciones de tilde y eñe | `Localization/BackendMessages.es.resx` |
| Las 2 versiones acentuadas que vivían en el archivo inalcanzable, fusionadas | `.es-SV.resx` → `.es.resx` |
| Guardrail de calidad del español (3 pruebas) | `tests/CLARIHR.Application.UnitTests/SpanishMessageQualityTests.cs` |
| Pruebas de localización por el cable (3 pruebas) | `tests/CLARIHR.Api.IntegrationTests/ApiIntegrationTests.ValidationLocalization.cs` |
| La verdad sobre `es-SV` y la convención fantasma de «3 resx» | `definiciones-tecnicas-backend.md` §10 |

#### Rojo antes de verde, con números

El guardrail se corrió **contra el `.resx` sin arreglar**:

| Prueba | Sin el arreglo | Con el arreglo |
|---|---|---|
| `SpanishMessages_ShouldNotContainEnglishFragments` | ❌ **196 mensajes** | ✅ |
| `SpanishMessages_ShouldNotDropDiacritics` | ❌ **204 mensajes** | ✅ |
| `SpanishMessages_ShouldNotBeVerbatimEnglish` | ✅ (ninguno era copia literal) | ✅ |

La tercera pasa en rojo **a propósito y se conserva**: mide una forma de fallo que hoy no ocurre —copiar el inglés tal cual— y que es la más fácil de introducir al añadir una clave con prisa.

#### Dos trampas de la corrección masiva

**El detector de inglés hay que calibrarlo hacia el falso negativo.** La primera versión marcó 222 mensajes, muchos buenos: `plan`, `rol`, `legal`, `actual` y `total` son palabras españolas, y `endpoint` es un préstamo aceptado. Un guardrail que grita de más se desactiva, y entonces no protege nada. La lista final excluye a propósito toda palabra que exista en ambos idiomas.

**Los homógrafos no se pueden arreglar con una regla.** `esta`/`está`, `este`/`esté`, `registro`/`registró`, `cambio`/`cambió`, `si`/`sí` dependen del contexto. Se clasificaron **los 38 contextos** de `esta`: 32 eran demostrativo («esta operación») y 71 verbo («está inactivo»). `este`, `registro` y `cambio` resultaron sustantivos en el 100 % de los casos y **no se tocaron**. El guardrail tampoco los vigila: prohibir `esta` haría fallar el español correcto.

> Y una trampa del propio ensayo: la primera regla de `esta` **no disparaba nunca** porque `.strip('.,;:')` no quita espacios y la palabra siguiente llegaba como `' reservada'`. Se detectó al revisar el diff en seco antes de escribir. Sin esa revisión, el arreglo habría corregido las tildes y dejado los 71 verbos intactos, con el guardrail en verde por casualidad.

**Verificado:** build 0 errores / 0 advertencias · unitarias **2982/2982** · integración `~Localization` **6/6**.

#### Lo que sigue abierto, y por qué no se cerró aquí

**Los nombres de propiedad siguen en inglés** dentro de mensajes en español: `'Code' no debería estar vacío.`, `'Sort Order' debe ser mayor o igual que '0'.` Eso ya es [B-03](#4-b-03--el-mensaje-de-formato-no-dice-cuál-es-el-formato-y-filtra-nombres-de-propiedad) y se deja ahí: no se cierra un hallazgo desde otro.

**El archivo `es-SV` se conservó.** Es inalcanzable y ya no aporta nada, pero lo referencian ~20 planes técnicos y hay `DefaultLocale = 'es-SV'` sembrado en migraciones. Borrarlo contradice esos documentos sin avisar; **la decisión de retirarlo, y de corregir la convención de «3 resx» en los planes, es aparte de este hallazgo.**

### 2.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-14 | 🔲 Propuesto | Detectado al probar el Paso 2 de la configuración guiada |
| 2026-08-14 | 🔲 Propuesto | Ampliado tras el ciclo CRUD completo: no son solo los mensajes de validación — el `title` del `409` también está en inglés y es el texto que el cliente pinta en su banner (§2.1 b) |
| 2026-08-16 | 🔄 Reabierto | Se reabre con una medición **de código, no de comportamiento**: «0 claves `validation.message.*`, 5 % de cobertura» |
| 2026-08-18 | 🟢 Resuelto | **La reabertura estaba mal medida** (había 161 claves; FluentValidation traduce de fábrica 12/12). Al medir por el cable apareció el defecto real: **199 mensajes mezclaban idiomas y 320 perdían tildes**, incluido `ano` por «año». 519 corregidos + guardrail de calidad + 3 pruebas de integración. `es-SV` resultó **inalcanzable** y sus 23 claves eran las versiones bien acentuadas |

---

## 3. B-02 — La clave del error de búsqueda es `search`, pero el parámetro público es `q`

> 🔎 **REENCUADRADO — 2026-08-15.** El Paso 5 encontró el mismo desajuste sobre un **campo del cuerpo** (`locationGroupPublicId` → clave `locationGroupId`). Eso cambia el diagnóstico: no es un problema del buscador, es **la convención `XxxId → xxxPublicId` filtrándose a los mensajes de error** en cualquier comando. El alcance real y la propuesta que sí escala están en [**00005 / B-02**](00005-WorkCenters.md#3-b-02--la-clave-del-error-nombra-el-campo-interno-y-no-el-público-cuarto-caso-y-el-primero-en-el-cuerpo). Este hallazgo sigue vigente como uno de los cuatro casos; **la solución se decide allí.**


| | |
|---|---|
| **Severidad** | 🔵 Baja |
| **Estado** | ⛔ **Descartado** — se resuelve en [00005 / B-02](00005-WorkCenters.md#3-b-02--la-clave-del-error-nombra-el-campo-interno-y-no-el-público-cuarto-caso-y-el-primero-en-el-cuerpo), que reencuadró la causa |
| **Componente** | API · `OrganizationStructureCatalogsController` |
| **Origen** | Hallazgos **F-01** y **F-03** del documento espejo |
| **Alcance** | **Patrón confirmado en 2 módulos** (`OrgStructureCatalogs`, `OrgUnits`) · sospecha en representantes legales |

### 3.1 Evidencia

```
GET /v1/unit-types?q=a  →  400
{"errors":{"search":["Search must be at least 2 characters when provided."]}}
```

El cliente envía **`q`**. El error vuelve bajo la clave **`search`**.

### 3.2 Causa

El controller renombra el parámetro en el enlace:

```csharp
[FromQuery(Name = "q")] string? search
```

El nombre público es `q`, pero la validación se hace sobre la propiedad `Search` de la query, así que la clave del error sale con el nombre interno.

### 3.3 Impacto

Menor pero real: un cliente que mapee `errors[<clave>]` a su control correspondiente —que es justo lo que se pide en **F-03**— no puede casar `search` con el parámetro `q` que él mandó. Tiene que codificar una excepción a mano.

Es la única clave del módulo que no coincide con lo que el cliente envía; todas las demás (`code`, `name`, `description`, `sortOrder`, `pageSize`) sí coinciden. Esa inconsistencia puntual es lo que la vuelve fácil de pasar por alto.

### 3.4 Propuesta

Dos caminos:

| Opción | Cómo | Nota |
|---|---|---|
| **A (recomendada)** | Que el error se emita bajo la clave `q`, con `.OverridePropertyName("q")` en la regla del validador | Corrige el síntoma sin tocar la URL pública |
| **B** | Renombrar el parámetro público a `search` y eliminar el `[FromQuery(Name = "q")]` | Rompe a los clientes que ya usan `q` |

Se recomienda **A**: el nombre público `q` ya está en el swagger y es la convención del resto de los buscadores del producto.

### 3.5 Compatibilidad

**Con la opción A no se rompe nada:** cambia la clave de un mensaje de error, no la URL ni la forma de la respuesta. No afecta `openapi.yaml`.

### 3.6 Alcance a revisar

Buscar otros `[FromQuery(Name = …)]` donde el nombre público difiera del de la propiedad validada. El buscador de representantes legales usa el mismo patrón (`[FromQuery(Name = "q")] string? search`), así que muy probablemente arrastra el mismo desajuste — conviene confirmarlo y resolver ambos juntos.

**Medido — 2026-08-15, corrida del Paso 3** ([`00003-OrgUnits`](00003-OrgUnits.md)): `OrgUnits` **repite el desajuste idéntico**.

```
GET /v1/org-units?q=a  →  400
{"errors":{"search":["Search must be at least 2 characters when provided."]}}
```

**Deja de ser «un endpoint»: es un patrón.** Confirmado en dos módulos independientes, y el de representantes legales usa la misma construcción. Conviene resolverlo con un criterio único —`.OverridePropertyName("q")` en la regla del validador— en vez de endpoint por endpoint.

### 3.7 Vía alterna vigente

El frontend puede mapear `search` → campo de búsqueda con una excepción escrita a mano. Funciona, pero es una regla especial que hay que recordar en cada pantalla con buscador.

### 3.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-14 | 🔲 Propuesto | Detectado al probar el Paso 2 de la configuración guiada |
| 2026-08-15 | 🔲 Propuesto | Alcance ampliado en la corrida del Paso 3: `OrgUnits` repite el desajuste. Es un patrón, no un endpoint (§3.6) |

---

## 4. B-03 — El mensaje de formato no dice cuál es el formato, y filtra nombres de propiedad

| | |
|---|---|
| **Severidad** | 🔵 Baja |
| **Estado** | 🟢 **Resuelto** — 2026-08-20 (§4.9). La mitad de los nombres de propiedad se separa a [B-04](#5-b-04--los-nombres-de-propiedad-siguen-en-inglés-dentro-de-mensajes-en-español) |
| **Componente** | Application · validadores FluentValidation · `*ValidationRules` |
| **Origen** | **Residual de B-01** — lo que su sustitución por `00003 / B-03` dejaba fuera |
| **Alcance** | **46 sitios** para el mensaje de formato (no 3) · **39 mensajes interpolados** invisibles para el guardrail |

> ⚠️ **Dos premisas de esta ficha eran falsas, y conviene leerlas antes que el arreglo.**
>
> | Afirmaba | Medido el 2026-08-20 |
> |---|---|
> | «no tiene clave en `BackendMessages.es.resx` (verificado: 0 coincidencias)» | ❌ **Sí la tenía**, ya commiteada: `validation.message.code_format_is_invalid` → «El formato de código no es válido.» |
> | «3 sitios medidos» | ❌ **46**: 34 en validadores y **12 más en las rutas PATCH**, que arman el diccionario de errores a mano |
>
> **Por qué falló la búsqueda:** se buscó la frase inglesa dentro del `.resx`, pero la clave está
> **normalizada** (`code_format_is_invalid`), no es la frase. Es el mismo error de medición que tenía
> [B-01](#2-b-01--el-español-que-se-sirve-estaba-roto-199-mensajes-en-dos-idiomas-y-320-sin-tildes).
>
> **Lo que sí se sostenía —y era lo importante— es la queja de fondo:** el mensaje estaba traducido y
> seguía sin decir cuál es el formato.

### 4.1 Por qué existe este hallazgo

B-01 se marcó ⛔ sustituido por `00003 / B-03`, con la instrucción «no trabajar este hallazgo: trabajar aquel». Correcto para la parte del idioma. **Pero B-01 §2.4 pedía cuatro cosas y `00003 / B-03` solo cubre dos** (localizar validación y títulos). Los puntos 3 y 4 —explicitar el formato y usar etiquetas de negocio— **no son de localización**: son de contenido del mensaje, y sobreviven intactos.

### 4.2 Evidencia

```
POST /v1/unit-types   { "code": "AB CD" }
→ 400   errors: { "code": ["Code format is invalid."] }
```

El mensaje **no dice cuál es el formato**. El usuario no sabe qué corregir.

```csharp
// 3 sitios, escrito a mano
.WithMessage("Code format is invalid.");
```

- `OrgStructureCatalogsAdministration.cs:193` y `:209`
- `JobProfileCatalogTypeCommands.cs:50`

Y **no tiene clave en `BackendMessages.es.resx`** (verificado: 0 coincidencias). Es decir: **aunque se arregle el canal de idioma de `00003 / B-03`, este mensaje seguirá saliendo en inglés**, porque no hay nada que buscar en el catálogo.

El segundo síntoma es distinto: `'Sort Order' must be greater than or equal to '0'.` viene de la plantilla por defecto de FluentValidation, que sustituye el **nombre de la propiedad C#** partido en palabras. No es un mensaje escrito por nadie.

### 4.3 Contraste

El perfil legal sí lo hace bien, y es el molde a copiar:

```
"El NIT patronal debe seguir el formato ####-######-###-#."
```

Dice el patrón. El usuario puede corregir sin adivinar.

### 4.4 Propuesta

1. **Explicitar el formato** en los 3 sitios y darles clave en el `.resx`:
   «El código debe empezar con letra o número y solo admite letras, números, guion y guion bajo (máximo 50 caracteres)» — que es lo que expresa `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$`.
2. **Etiquetas de negocio** en los campos cuyo nombre C# no sea presentable, vía `.WithName(...)`.

> ⚠️ **Depende de `00003 / B-03`, no al revés.** Añadir la clave al `.resx` no sirve de nada mientras el canal de idioma no funcione. Hacer este hallazgo **después**, o el trabajo no se ve.

### 4.5 Compatibilidad

**No rompe el contrato.** Cambia el texto de los mensajes, no las claves de `errors` ni la forma. No afecta `openapi.yaml`.

### 4.6 Alcance a revisar

Contar cuántos `.WithMessage(...)` del backend están escritos a mano en inglés y sin clave en el `.resx`: son los que quedarán sin traducir aunque el canal funcione. **Es una búsqueda, no una corrida.**

### 4.7 Vía alterna vigente

El frontend valida en cliente y escribe su propio mensaje (**F-02** del espejo). Funciona, pero duplica la regla: si el backend cambia el patrón, el cliente miente.

### 4.9 Lo que se hizo

#### El hallazgo cambia de forma al medirlo: las reglas NO son iguales entre sí

Ésta es la razón de que nadie lo hubiera arreglado con un texto compartido — y de que la corrección
propuesta en §4.4 («máximo 50 caracteres») **habría sido falsa en 22 de los 46 sitios**:

| Regla | Caracteres | Máximo | Sitios |
|---|---|---:|---:|
| `[A-Za-z0-9_-]` | letras, números, guion, guion bajo | 50 | 24 |
| `[A-Za-z0-9_-]` | los mismos | 80 | 2 |
| `[A-Za-z0-9_./-]` | + punto y barra | 80 | 4 |
| `[A-Za-z0-9_.-]` | + punto | 80 | 1 |

Por eso el texto **vive junto a su regla**, no junto al validador: cada `*ValidationRules` declara su
propio `CodeFormatMessage` al lado de su regex, y los 46 sitios lo referencian.

#### El guardrail que impide que el texto y la regla se separen

`CodeFormatMessageTests` descubre las clases **por reflexión** —una clase nueva con las dos piezas queda
cubierta sin tocar el archivo— y para cada una comprueba contra la regex real:

- que el máximo que promete el texto se acepte, y que uno más se rechace;
- que **cada carácter anunciado** se acepte y que **ninguno no anunciado** pase;
- que el primer carácter tenga que ser letra o número, como dice la frase.

**Rojo verificado en las dos direcciones**, mintiéndole al mensaje a propósito:

| Mentira introducida | Lo que dijo el guardrail |
|---|---|
| «hasta 60 caracteres» con una regla de 50 | `el mensaje promete 60 caracteres pero la regla los rechaza` |
| anunciar barra donde la regla no la admite | `el mensaje anuncia '/' pero la regla lo rechaza` |

#### El segundo hueco: 39 mensajes que el guardrail no podía ver

§4.6 pedía contar los `.WithMessage(...)` sin clave en el `.resx`. La respuesta de los literales fue
**0 de 155**. Pero la cuenta se hizo con la misma regex que usa la prueba de paridad —`\.WithMessage\("`—
y **el `$` de una cadena interpolada no casa**:

```csharp
.WithMessage($"Search must be at least {OrgStructureCatalogValidationRules.MinSearchLength} characters when provided.")
```

**39 mensajes eran invisibles para el guardrail y salían en inglés con la suite en verde.** Un mensaje
interpolado además **no se puede verificar de forma estática**: su clave se deriva del texto ya resuelto.

Se convirtieron en constantes literales (`SearchLengthMessage` en 14 clases, más 2 casos sueltos) con su
clave en los dos `.resx`, y `ValidationMessageCoverageTests` ahora **prohíbe la interpolación** y exige
clave a toda constante que se pase a `.WithMessage(...)`.

#### La trampa del guardrail nuevo, cazada por él mismo

Su primera versión recogía **toda** constante terminada en `Message` y dio **3 falsos positivos**: los
avisos de vacaciones y horas extra se localizan por su `Code` vía `Localize(code, fallback)`, no por la
clave derivada del texto — y **sí tienen traducción**. Se verificó una por una antes de tocar nada, y se
estrechó el guardrail al uso real. Es la misma calibración que B-01: **un guardrail que señala trabajo
correcto acaba desactivado, y entonces no protege nada.**

#### Lo que ve el usuario ahora

```
code: El código debe empezar con letra o número y solo admite letras, números,
      guion y guion bajo, hasta 50 caracteres.
```

**Verificado:** build 0 errores / 0 advertencias · unitarias **3022/3022** · integración `~Localization`
+ `~ValidationErrorKeys` **8/8**, incluida una aserción nueva que exige que el mensaje **nombre los
caracteres permitidos y el largo**, no solo que esté en español.

### 4.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-16 | 🔲 Propuesto | Levantado al cerrar 00002: la sustitución de B-01 por `00003 / B-03` **se llevaba por delante** los puntos 3 y 4 de su propuesta, que no son de localización. Verificado que el mensaje **no tiene clave en el `.resx`**, así que el arreglo del idioma no lo alcanzaría |
| 2026-08-20 | 🟢 Resuelto | **Dos premisas eran falsas**: el mensaje sí tenía clave (se buscó la frase, no la clave normalizada) y eran **46 sitios, no 3** (12 viven en las rutas PATCH). Al medir apareció que las reglas **difieren entre sí**, así que el texto ahora vive junto a cada regex con un guardrail por reflexión que verifica que no mienta. Y se cerró un hueco que nadie veía: **39 mensajes interpolados** que la regex del guardrail de paridad no podía casar. Los nombres de propiedad se separan a **B-04** |

---

## 5. B-04 — Los nombres de propiedad siguen en inglés dentro de mensajes en español

| | |
|---|---|
| **Severidad** | 🔵 Baja |
| **Estado** | 🟢 **Resuelto** — 2026-08-20 (§5.7). Decisión de producto: **salida A**, glosario incremental |
| **Componente** | Application · plantillas por defecto de FluentValidation |
| **Origen** | **Punto 4 de [B-01 §2.4](#24-propuesta--qué-de-esto-resultó-necesario)**, que sobrevivió a [B-03](#4-b-03--el-mensaje-de-formato-no-dice-cuál-es-el-formato-y-filtra-nombres-de-propiedad) |
| **Alcance** | **514 propiedades** de varias palabras, medidas — pero la mayoría no son campos que el usuario escriba |

### 5.1 Evidencia

Con `Accept-Language: es`, la frase está en español y el nombre del campo no:

```
code:      'Code' no debería estar vacío.
sortOrder: 'Sort Order' debe ser mayor o igual que '0'.
```

`'Sort Order'` es el nombre de la propiedad C# `SortOrder` partido en palabras por FluentValidation. No
es un texto que alguien haya escrito: es el default de la librería.

### 5.2 Lo medido — 2026-08-20

De **611 propiedades distintas** en `RuleFor(...)`, **514 son de varias palabras** y por tanto se parten.
Pero el reparto importa más que el total:

| Propiedad | Usos | ¿La escribe el usuario? |
|---|---:|---|
| `ConcurrencyToken` | 392 | ❌ Es el token de `If-Match` |
| `PersonnelFileId` | 383 | ❌ Va en la ruta |
| `CompanyId` | 228 | ❌ Va en la ruta |
| `PageSize` · `PageNumber` | 176 | ❌ Paginación |
| `JobProfileId` | 72 | ❌ Selector, no texto libre |
| **`SortOrder`** | **61** | ✅ **Sí** |

**Más de mil de los usos son identificadores internos y de paginación.** Un error de validación que
nombre `'Concurrency Token'` no es un problema de traducción: es un error interno saliendo a la
superficie, y traducir su etiqueta lo disimularía en vez de arreglarlo.

### 5.3 Por qué no se arregló aquí

Porque las dos salidas razonables producen productos distintos, y elegir por mi cuenta sería sustituir
una decisión de producto:

| Salida | Qué implica |
|---|---|
| **A — Glosario de etiquetas** | `ValidatorOptions.Global.DisplayNameResolver` leyendo `validation.property.*` del `.resx`. El mecanismo son ~20 líneas y es inerte mientras no haya etiqueta, así que **no puede empeorar nada**. Pero hay que decidir cómo se llama cada campo en español, y eso es contenido de UX |
| **B — Que el mensaje no nombre el campo** | La clave del error (`sortOrder`) ya mapea el mensaje a su control, y **el frontend ya pinta su propia etiqueta al lado**. El nombre dentro del texto es redundante. Implica cambiar las plantillas por defecto para todo el producto |

**Recomendación: A, pero solo para los campos que el usuario escribe.** El mecanismo es barato y no
regresivo —un campo sin etiqueta se comporta exactamente como hoy—, así que se puede poblar de forma
incremental empezando por los formularios que ya están probados. La salida B es más limpia
conceptualmente pero toca el 100 % de los mensajes de una vez, para arreglar algo de severidad baja.

### 5.4 Compatibilidad

**No rompe el contrato** en ninguna de las dos salidas: cambia el texto de `errors[campo][]`, no las
claves ni la forma. Medido: solo **8 aserciones** en toda la suite tocan estos textos, y una sola afirma
un mensaje con nombre de propiedad (`'Relationship'`, de una sola palabra: no le afecta el partido).

### 5.5 Vía alterna vigente

El frontend valida en cliente y escribe su propio mensaje (**F-02** del espejo). Sigue funcionando.

### 5.7 Lo que se hizo

**Salida A**, como se decidió: `ValidatorOptions.Global.DisplayNameResolver` alimentado desde el `.resx`.

| Pieza | Dónde |
|---|---|
| El enganche, con el resolvedor detrás de un delegado | `Application/Common/Validation/ValidationDisplayNames.cs` |
| La búsqueda (`validation.property.<propiedad>`) | `Infrastructure/Localization/ResourceBackendMessageLocalizer.cs` |
| Instalación del delegado | `Infrastructure/DependencyInjection.cs` |
| **45 etiquetas** iniciales | `BackendMessages.es.resx` + `.resx` |
| Pruebas del mecanismo (7) | `tests/…/ValidationDisplayNameTests.cs` |
| Prueba por el cable | `ApiIntegrationTests.ValidationLocalization.cs` |

#### Por qué el delegado, y no una llamada directa

FluentValidation solo está referenciado en `CLARIHR.Application`; los recursos viven en
`CLARIHR.Infrastructure`. El enganche se declara en Application y **la búsqueda la instala
Infrastructure**, que sí depende de Application. Así la capa de aplicación no necesita conocer el
`.resx` y no se invierte ninguna dependencia.

#### La garantía que sostiene todo: es inerte sin etiqueta

Un campo sin etiqueta devuelve `null` y FluentValidation usa su nombre partido de siempre. Por eso el
glosario se puede poblar poco a poco **sin dejar el producto a medio traducir**: lo que no está
catalogado se comporta exactamente como antes de que existiera este mecanismo. Hay una prueba dedicada
a esa propiedad, porque es la que hace viable la decisión.

#### Un guardrail que no conocía obligó a cambiar el diseño

El plan era poner las etiquetas **solo en español** y dejar que el inglés cayera al default. Falló
`BackendMessageLocalizationTests.EnglishAndSpanishResources_ShouldHaveTheSameKeys`, que **exige paridad
de claves entre los dos recursos**.

La salida fue poner también las inglesas, pero con un valor que **no cambia nada**: exactamente el
nombre partido que FluentValidation produciría por su cuenta (`SortOrder` → `Sort Order`,
`PayrollPeriodLabel` → `Payroll Period Label`). Y como eso es fácil de romper con un descuido —basta
escribir «Sort order»— hay una prueba que recorre **todas** las etiquetas inglesas y comprueba que cada
valor sea el PascalCase partido de su clave. Cubre también las que se añadan mañana.

#### Rojo antes de verde

Quitando la etiqueta de `SortOrder` de los dos recursos:

```
✗ `SortOrder` debe salir como 'Orden'. Recibido:
    sortOrder: 'Sort Order' debe ser mayor o igual que '0'.
```

Ese rojo prueba dos cosas a la vez: que la prueba es sensible al mecanismo, y que **sin etiqueta el
comportamiento anterior vuelve intacto** — la garantía de inercia, medida en vez de afirmada.

#### Lo que ve el usuario ahora

```
code:      'Código' no debería estar vacío.
sortOrder: 'Orden' debe ser mayor o igual que '0'.
```

**Verificado:** build 0 errores / 0 advertencias · unitarias **3030/3030** · integración **9/9**.

#### Cómo se amplía

Se añade `validation.property.<propiedad en minúsculas>` a los dos recursos: el español con la etiqueta
de negocio, el inglés con el nombre partido. **Nada más** — ni código, ni registro.

Quedan **386 propiedades candidatas** sin etiqueta de las 431 medidas en §5.2. No se poblaron todas a
propósito: la mayoría son campos de pantallas que esta campaña todavía no ha probado, y ponerles nombre
sin ver el formulario sería inventar vocabulario. **Los identificadores internos y la paginación no
llevan etiqueta nunca**: si `'Concurrency Token'` aparece en un mensaje, el defecto es que ese error
esté saliendo a la superficie, no que esté en inglés.

### 5.6 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-20 | ⏸️ Espera decisión | Separado de B-03 al cerrarla. Se levanta **con la medición hecha** para que la decisión sea barata: 514 propiedades se parten, pero >1000 usos son identificadores internos que el usuario nunca escribe. Dos salidas viables (§5.3), con recomendación |
| 2026-08-20 | 🟢 Resuelto | **Salida A elegida.** Mecanismo + 45 etiquetas + 7 pruebas. La paridad de claves obligó a poner también las inglesas: se generan como el split de FluentValidation para que la salida en inglés **no cambie ni un carácter**, con una prueba que lo vigila. Rojo verificado quitando una etiqueta: vuelve `'Sort Order'`, que es justo la garantía de inercia |
