# 00005 — WorkCenters · Hallazgos de frontend

| | |
|---|---|
| **ID** | 00005-WorkCenters |
| **Documento espejo** | [`ComentariosPruebasBackend/00005-WorkCenters`](../ComentariosPruebasBackend/00005-WorkCenters.md) |
| **Paso probado** | **Paso 5 de 8** — *Work centers* |
| **Pantalla** | `/work-centers` · pestaña 1 de 5 · formulario en `/work-centers/new` |
| **Fecha** | 2026-08-15 |
| **Ambiente** | `https://dashboard.clarihr.com` · empresa `End to End SAS` |

---

## 1. Resumen ejecutivo

🔴 **La dirección se escribe a ciegas en tema oscuro.** El campo usa un componente web de Google que **no hereda el tema**: el texto se pinta oscuro sobre fondo oscuro y es invisible. El campo es obligatorio, parece vacío cuando no lo está, y el usuario reescribe encima — produciendo texto duplicado sin darse cuenta (**F-01**). **Probado con par diferencial**: en tema claro el mismo texto se ve perfectamente.

🔴 **El grupo de ubicación se presenta como opcional y el servidor lo exige.** No lleva asterisco, no tiene `aria-required` y su ayuda dice literalmente «Optional grouping for location-based policies». El `POST` sin él responde `400` (**F-02**).

🟡 **El contador de notas promete el doble de lo que el servidor acepta**: muestra `78 / 2000` con `maxlength="2000"`, y el servidor corta en **1000** (**F-03**). Es peor que no declarar límite: invita a escribir un valor que será rechazado.

🟡 **La etiqueta de la dirección apunta a un `<div>`.** `<label for="wc-address">` referencia un elemento no rotulable, y el campo real no tiene identificador (**F-04**).

🟢 **La validación reactiva por tipo funciona muy bien.** Al elegir `OFICINA`, las etiquetas de dirección, latitud y longitud **se marcan obligatorias solas**, según los flags del tipo. Y **el servidor las exige de verdad** — no es decoración de cliente (§4.1).

🟢 **La integración con Google Maps rellena las coordenadas** al elegir una sugerencia, y el mapa con marcador arrastrable refleja el punto. Es la pieza más pulida de las cinco pantallas probadas.

🟢 **Ciclo completo ejercitado por la interfaz**: crear → leer → editar → inactivar → reactivar, más buscar y filtrar por estado. Nueve sondas de error, las nueve correctas (§7).

🟢 **Las cinco sedes del escenario AVIANCA quedaron cargadas y activas.** El asistente pasó de `4/8` a **`5/8`** y desbloqueó el Paso 6.

**Frontend** (§5)

| Severidad | Cantidad | IDs |
|---|---|---|
| 🔴 Alta | 2 | F-01, F-02 |
| 🟡 Media | 3 | F-03, F-04, F-05 |
| 🔵 Baja | 0 | — |
| ⚪ Informativo | 1 | F-06 — *medido y correcto* |

**Backend** (§6) — 1 hallazgo nuevo: la clave del error nombra el campo interno (`locationGroupId`) y no el público (`locationGroupPublicId`). Es el **cuarto caso** del mismo patrón y el primero sobre un campo del cuerpo.

---

## 2. Contrato de la pantalla

### 2.1 Endpoints

```
GET    /v1/work-centers                          listado paginado
GET    /v1/work-centers/{publicId}               detalle
POST   /v1/work-centers                          → 201
PUT    /v1/work-centers/{publicId}               If-Match obligatorio
PATCH  /v1/work-centers/{publicId}               JSON Patch · If-Match obligatorio
PATCH  /v1/work-centers/{publicId}/reassign-group   If-Match obligatorio
PATCH  /v1/work-centers/{publicId}/activate         If-Match obligatorio
PATCH  /v1/work-centers/{publicId}/inactivate       If-Match obligatorio
```

✅ **Ya hay `DELETE` y `/usage`** desde el 2026-08-21:

```
GET     /v1/work-centers/{publicId}/usage
DELETE  /v1/work-centers/{publicId}      ← If-Match obligatorio
```

> Cuando se probó esta pantalla no existían. Era el cuarto recurso del hueco levantado como
> [00003 / B-04](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca),
> ya resuelto en los cinco. Ver §2.5.
>
> **Hay un `reassign-group` dedicado** y el `PUT` también acepta `locationGroupPublicId`. **Los dos validan el nivel igual** — medido, no hay puerta trasera (**F-06**). `reassign-group` es preferible cuando solo cambia el grupo: evita reenviar los diez campos.

### 2.2 Cuerpo de `POST` y `PUT`

```jsonc
{
  "code": "SS-CORP",                    // requerido · máx. 50 · ^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$
  "name": "Oficina Corporativa…",       // requerido · máx. 150
  "workCenterTypePublicId": "…",        // REQUERIDO
  "locationGroupPublicId": "…",         // ⚠️ REQUERIDO — el formulario dice que es opcional (F-02)
  "address": "…",                       // máx. 300 · CONDICIONALMENTE requerido (§4.1)
  "geoLat": 13.6906464,                 // decimal? · −90 a 90  · CONDICIONALMENTE requerido
  "geoLong": -89.2386296,               // decimal? · −180 a 180 · CONDICIONALMENTE requerido
  "phone": "+50322090000",              // máx. 50
  "email": "…@…",                       // formato de correo validado
  "notes": "…"                          // ⚠️ máx. 1000 — el cliente permite 2000 (F-03)
}
```

### 2.3 Respuesta del listado

```jsonc
{
  "publicId": "…", "code": "…", "name": "…",
  "workCenterTypeCode": "OFICINA",      // ← plano, no anidado
  "workCenterTypeName": "Oficina corporativa",
  "locationGroupCode": "SAN_SALVADOR_CENTRO",
  "locationGroupName": "San Salvador Centro",
  "address": "…", "geoLat": 13.69, "geoLong": -89.23,
  "phone": "…", "email": "…", "notes": "…",
  "isActive": true, "concurrencyToken": "…",
  "createdAtUtc": "…", "modifiedAtUtc": null,
  "allowedActions": { … }
}
```

> 🟢 **El listado aplana el tipo y el grupo en `…Code` + `…Name`.** Es la forma cómoda para una tabla: no obliga a un segundo viaje ni a desanidar.

### 2.4 Errores

| Código | HTTP | Cuándo | Clave |
|---|---|---|---|
| `WORK_CENTER_CODE_CONFLICT` | `409` | Código repetido | — |
| `WORK_CENTER_NOT_FOUND` | `404` | No existe o es de otra empresa | — |
| `LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER` | `409` | El grupo no está en un nivel que admita centros | — |
| `WORK_CENTER_ADDRESS_REQUIRED` | `400` | El tipo exige dirección y no vino | — |
| `WORK_CENTER_GEO_REQUIRED` | `400` | El tipo exige geo y falta **latitud o longitud** | — |
| `WORK_CENTER_HAS_ACTIVE_DEPENDENCIES` | `409` | Al inactivar con dependencias activas | — |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado | — |
| *(validación)* | `400` | Falta el grupo | **`locationGroupPublicId`** ✅ |
| *(validación)* | `400` | Correo mal formado | `email` |
| *(validación)* | `400` | Latitud fuera de −90..90 | `geoLat` |
| *(validación)* | `400` | Notas de más de 1000 | `notes` |

✅ **La clave del grupo ya es `locationGroupPublicId`** — el nombre público del campo. Verificado por el
cable el 2026-08-21.

> Cuando se probó, el servidor devolvía `locationGroupId` —el nombre interno— y el frontend no podía
> casarlo con ningún control. Este documento levantó el caso como
> [00005 / B-02](../ComentariosPruebasBackend/00005-WorkCenters.md), que lo normalizó en **40 endpoints**
> del producto. **Éste está entre los corregidos.**
>
> Quedaban cinco endpoints devolviendo `search` —el listado de esta misma pantalla entre ellos—;
> cerrados el 2026-08-21. Ver §2.6.

✅ **`title` y `detail` también salen en el idioma pedido.** Verificado por el cable el 2026-08-21.

> Cuando se probó, los dos caminos que producen un `400` **no localizaban igual**: los mensajes de
> `errors` salían en español y el texto de cabecera en inglés, así que la misma respuesta mezclaba dos
> idiomas. Medido en esta pantalla: enviar `locationGroupPublicId: "no-es-un-guid"` devolvía
> `"errors": { "locationGroupPublicId": ["El valor debe ser un UUID válido."] }` junto a
> `"title": "One or more validation errors occurred."`.
>
> | Camino | `errors[campo]` | `title` / `detail` |
> |---|---|---|
> | Validación de negocio (FluentValidation) | español ✅ | español ✅ |
> | Model-binding (tipo mal formado, JSON inválido) | español ✅ | español ✅ *(antes inglés)* |

**Aun así conviene seguir mostrando los mensajes de `errors` y no `title`/`detail`** — y ahora por una
razón distinta de la traducción: `title` es un texto genérico («Se encontraron uno o más errores de
validación.») que no dice **qué** campo falló. `errors[campo]` es lo que el usuario necesita leer junto
a su control.

### 2.5 Borrado condicional — capacidad nueva (2026-08-21)

```
GET     /v1/work-centers/{publicId}/usage     → 200
DELETE  /v1/work-centers/{publicId}           If-Match obligatorio → 200
```

```jsonc
// respuesta de /usage
{
  "publicId": "…", "code": "SS-CORP", "name": "Oficina Corporativa San Salvador",
  "positionSlotActiveReferences": 4,        // plazas activas ancladas al centro
  "positionSlotInactiveReferences": 1,      //   …e inactivas
  "employmentAssignmentReferences": 12,     // ⚠️ asignaciones de expediente — ver abajo
  "hasActiveReferences": true
}
```

| Situación | Respuesta |
|---|---|
| Nada lo referencia | `200` con el centro borrado |
| Plazas o asignaciones lo referencian | `409 WORK_CENTER_IN_USE_FOR_DELETE` |
| `If-Match` ausente / desactualizado | `400` / `409 CONCURRENCY_CONFLICT` |

> ⚠️ **`employmentAssignmentReferences` es la cuenta que la base de datos NO protege.** La asignación de
> expediente guarda el `publicId` del centro **sin clave foránea**, así que el motor no lo impediría por
> sí solo: el guard es de aplicación. Es la razón de que este contador exista y de que valga la pena
> mostrarlo — un centro sin plazas puede seguir teniendo doce empleados asignados.

### 2.6 La clave del buscador en esta pantalla

✅ **El listado ya devuelve `q`** —el nombre con el que el cliente envía el parámetro—. Verificado por
el cable el 2026-08-21.

> Cuando se probó devolvía `search`, que es el nombre interno del objeto de consulta del servidor y no
> corresponde a nada que el cliente conozca. El caso se había normalizado en 40 de los 45 endpoints; los
> **cinco** que faltaban —centros de trabajo · tipos de centro de trabajo · tipos de centro de costo ·
> grupos de ubicación · unidades organizativas— quedaron cerrados el 2026-08-21.
>
> | Forma | Clave | Endpoints |
> |---|---|---|
> | Todos | **`q`** ✅ | **45** |

**Si ya mapeaste `search` y `q` al mismo control, no hace falta deshacerlo**: `search` simplemente no
volverá a aparecer. Si aún no lo hiciste, basta con `q`.

> La asimetría que tenía esta pantalla desapareció: el **campo del cuerpo** (`locationGroupPublicId`) y
> el **parámetro del buscador** (`q`) usan ya los dos su nombre público.


---

## 3. La jerarquía de ubicaciones — lo que hay que saber antes de cargar datos

El centro de trabajo **no se ancla a un departamento ni a un municipio sueltos**: se ancla a un **grupo de ubicación**, y solo a uno cuyo nivel lo permita.

| Nivel | Qué es | ¿Admite centros? | Cuántos |
|---|---|---|---|
| 1 | País | ❌ | 1 — *El Salvador* |
| 2 | Departamento | ❌ | 14 |
| **3** | **Municipio** | ✅ | **44** |

Total: **59 grupos sembrados**. El guard funciona: anclar a un grupo de nivel 2 responde `409 LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`.

### 3.1 ⚠️ El catálogo usa la reorganización municipal de 2023

Los 44 municipios llevan nombres de zona cardinal —`La Paz Oeste`, `San Salvador Centro`, `La Libertad Este`—, **no los nombres municipales históricos**. Esto tiene una consecuencia práctica inmediata:

**La guía de configuración de AVIANCA nombra municipios que ya no existen en el catálogo.** Ninguna de sus cinco filas se puede cargar tal cual:

| La guía dice | En el catálogo | Resuelto como |
|---|---|---|
| San Luis Talpa, La Paz | ❌ no existe | **La Paz Oeste** |
| San Salvador, San Salvador | ❌ no existe con ese nombre | **San Salvador Centro** |
| Antiguo Cuscatlán, **San Salvador** | ❌ no existe · **y el departamento estaba mal**: Antiguo Cuscatlán es de La Libertad | **La Libertad Este** |

**Las tres equivalencias las confirmó el usuario en la corrida.** Quedan registradas aquí porque la guía dice explícitamente que *«que el municipio sea el correcto importa»* para la Planilla Patronal.

> **La guía necesita corrección**, y no es cosmética: como está, nadie puede seguirla. Se anota en §7.

> ℹ️ **Las tres equivalencias de arriba siguen siendo válidas.** `La Paz Oeste`, `San Salvador Centro` y
> `La Libertad Este` no llevan tilde en ninguna de sus palabras, así que la corrección de §3.2 no las
> tocó. Sus **códigos** tampoco cambiaron.

### 3.2 Los nombres de departamento — 🟢 **corregidos el 2026-08-21**

Cuando se probó esta pantalla, los seis departamentos con acento venían sin él:

```
Ahuachapan · Cabanas · Cuscatlan · La Union · Morazan · Usulutan
```

**Ya no.** Verificado contra el sembrado real:

```
Ahuachapán · Cabañas · Cuscatlán · La Unión · Morazán · Usulután
```

> El caso grave era **`Cabanas`**: sin la eñe no era una variante sin tilde, era **otra palabra**. Se
> levantó como [00005 / B-01](../ComentariosPruebasBackend/00005-WorkCenters.md).

⚠️ **Y el alcance real era mucho mayor de lo que este documento vio.** Al arreglarlo se midió todo el
sembrado: eran **81 filas en 15 tablas**, no 6 en una. Los **catorce municipios** de esos seis
departamentos heredaban el error, porque su nombre se derivaba del código ASCII:

| Antes | Ahora |
|---|---|
| `Cabanas Este` · `Cabanas Oeste` | **`Cabañas Este`** · **`Cabañas Oeste`** |
| `Ahuachapan Centro` · `Ahuachapan Norte` · `Ahuachapan Sur` | **`Ahuachapán …`** |
| `Cuscatlan Norte` · `Cuscatlan Sur` | **`Cuscatlán …`** |
| `La Union Norte` · `La Union Sur` | **`La Unión …`** |
| `Morazan Norte` · `Morazan Sur` | **`Morazán …`** |
| `Usulutan Este` · `Usulutan Norte` · `Usulutan Oeste` | **`Usulután …`** |

**Fuera de esta pantalla también cambiaron**: «Banco Agrícola», «Español», «Día», los nombres de 16
permisos y varios catálogos más.

> 🔴 **Consecuencia para el cliente, y es la única de este documento que puede romper algo:** cualquier
> código que **compare por `name`** dejó de casar. `if (name === "Cabanas")` no encuentra nada, **sin dar
> error**. Los códigos (`CABANAS`, `CABANAS_ESTE`) **no cambiaron**: comparar por `code` sigue siendo
> correcto y es lo que hay que hacer.

✅ **Y buscar ya ignora los acentos**: `q=cabanas` encuentra «Cabañas» y `q=ahuachapan` encuentra
«Ahuachapán». Así que un buscador que envíe lo que el usuario teclee funciona con o sin tilde.

---

## 4. Cobertura de campos — 10 de 10

| # | Campo API | ¿En el formulario? | Estado |
|---|---|---|---|
| 1 | `code` | ✅ `wc-code` | 🟢 `aria-required` · ⚠️ sin `maxlength` |
| 2 | `name` | ✅ `wc-name` | 🟢 `aria-required` · ⚠️ sin `maxlength` |
| 3 | `workCenterTypePublicId` | ✅ `wc-type` | 🟢 `aria-required` · combo `CÓDIGO - Nombre` |
| 4 | `locationGroupPublicId` | ✅ `wc-location-group` | 🔴 **F-02** — sin asterisco y el servidor lo exige |
| 5 | `address` | ✅ `wc-address` | 🔴 **F-01** invisible en oscuro · 🟡 **F-04** etiqueta rota |
| 6 | `geoLat` | ✅ `wc-lat` | 🟡 **F-05** — asterisco sin `aria-required` |
| 7 | `geoLong` | ✅ `wc-lng` | 🟡 **F-05** |
| 8 | `phone` | ✅ `wc-phone` | 🟢 Presente · selector de país |
| 9 | `email` | ✅ `wc-email` | 🟢 Presente |
| 10 | `notes` | ✅ `wc-notes` | 🟡 **F-03** — contador a 2000, servidor a 1000 |

**Cobertura completa**, la segunda pantalla consecutiva sin campos ausentes.

### 4.1 La validación condicional por tipo — probada en las dos capas

Es lo mejor de esta pantalla y merece constar con detalle, porque **la primera lectura del código me hizo predecir lo contrario**.

**En el cliente**, al elegir el tipo las etiquetas cambian solas:

```
Antes de elegir tipo:   Address     Latitude     Longitude
Tras elegir OFICINA:    Address *   Latitude *   Longitude *
```

**En el servidor**, medido con tres sondas sobre el tipo `HANGAR` (`requiresAddress` y `requiresGeo` en `true`):

| Sonda | Respuesta |
|---|---|
| Sin dirección ni geo | `400 WORK_CENTER_ADDRESS_REQUIRED` |
| Con dirección, sin geo | `400 WORK_CENTER_GEO_REQUIRED` |
| Con dirección y **solo latitud** | `400 WORK_CENTER_GEO_REQUIRED` |
| Completo | `201` |

🟢 **Las dos capas coinciden**, y el servidor exige la pareja de coordenadas completa, no una suelta. Es la única regla de negocio de las cinco pantallas probadas que está aplicada en cliente **y** en servidor.

> **Nota de método.** Al leer el código busqué la regla en los validadores de FluentValidation y no estaba, así que concluí que los flags eran declarativos y nadie los aplicaba. **Iba a levantarlo como hallazgo.** La sonda lo desmintió: la regla vive en el handler (`WorkCenterAdministration.cs:1337` y `:1342`), fuera del bloque que había mirado. **Tercer falso positivo de instrumento en esta corrida** — los otros dos fueron un ayudante de `fetch` que pisaba cabeceras y una lista que parecía desactualizada. Los tres los detuvo medir antes de escribir.

---

## 5. Hallazgos de frontend

### 🔴 F-01 — La dirección es invisible en tema oscuro

**Severidad:** Alta · **Tipo:** Integración de terceros / contraste

#### Evidencia — par diferencial

El campo de dirección es un componente web de Google (`<gmp-place-autocomplete>`) con shadow DOM propio. Su valor se lee correctamente por programación:

```js
document.getElementById('wc-address').querySelector('input')   // → null (está en shadow DOM)
document.activeElement.value                                    // → "Bulevar Del Hipodromo, San Salvad…"
```

**El valor está. No se ve.** Mismo formulario, mismo contenido, lo único que cambia es el tema:

| Tema | Qué muestra el campo |
|---|---|
| 🌙 Oscuro | *(vacío a la vista)* |
| ☀️ Claro | `Bulevar Del Hipodromo, San SalvadBoulevard del Hipodromo, Colonia S` |

El texto duplicado del tema claro **es la prueba del daño**: se escribió una vez, no se vio nada, se volvió a escribir encima. Es exactamente lo que hará cualquier usuario.

#### Causa

El componente de Google trae su propia hoja de estilos y **el tema oscuro de la aplicación no la alcanza** —está en shadow DOM—, así que el color de texto se queda en el oscuro por defecto sobre el fondo oscuro del formulario.

#### En edición es peor que en creación

Se midió también el panel de edición, sobre `SS-CAP`, que **sí tiene dirección guardada**:

```js
document.getElementById('wc-address')
        .querySelector('gmp-place-autocomplete').value
// → "Antiguo Cuscatlan, La Libertad"        ← el valor está
```

Y el campo se ve vacío. **En creación el usuario escribe a ciegas; en edición ve vacío un campo obligatorio que sí tiene contenido**, lo que no solo confunde: **invita a rellenarlo**, sustituyendo en silencio una dirección correcta.

🟢 **Lo que sí funciona:** guardar sin tocar el campo **conserva la dirección**. Verificado editando el nombre de `SS-CAP` y comprobando en el servidor que `address` seguía intacta. El riesgo es únicamente que el usuario escriba encima — que es justo lo que la apariencia de vacío provoca.

#### Impacto

**Es un campo obligatorio que aparenta estar vacío, en creación y en edición.** Y el modo oscuro es el que trae la aplicación por defecto, así que es el caso normal, no el excepcional. El usuario escribe sin retroalimentación, duda, reescribe, y guarda una dirección corrupta que **nadie revisará** porque tampoco se ve al volver a abrir el registro.

Y contamina la integración entera: al no verse lo escrito, tampoco se entiende por qué aparecen o desaparecen las sugerencias.

#### Contrato para el frontend

**No interviene ningún endpoint.** El valor llega bien al `POST` cuando se escribe una sola vez; el defecto es puramente de presentación.

#### Ajuste pedido al frontend

Aplicar el tema al componente atravesando el shadow DOM —`::part()` si el componente expone partes, o las variables CSS que Google documente para él—. **Y añadir una comprobación de contraste sobre los componentes de terceros**, que es donde el tema deja de propagarse solo.

**Verificación:** escribir en el campo con tema oscuro y comprobar que el texto se lee.

---

### 🔴 F-02 — El grupo de ubicación se presenta como opcional y el servidor lo exige

**Severidad:** Alta · **Tipo:** Divergencia de contrato

#### Evidencia

En el formulario:

- **Sin asterisco**: la etiqueta dice `Location group`, no `Location group *`
- **Sin `aria-required`**: `document.getElementById('wc-location-group').getAttribute('aria-required')` → `null`
- **La ayuda lo dice explícitamente**: *«Optional grouping for location-based policies.»*

En el servidor:

```
POST /v1/work-centers   { code, name, workCenterTypePublicId }     ← sin el grupo
→ 400   errors: { "locationGroupId": [...] }
```

Y el contrato lo confirma: `Guid LocationGroupPublicId` **no es nullable**, con `RuleFor(c => c.LocationGroupId).NotEmpty()`.

#### ¿Debería ser opcional? No — y el propio diseño lo demuestra

El grupo **es lo que ancla el centro a la geografía**, y de él dependen la regla de nivel (`LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`), la distribución geográfica del tablero y el reporte de Planilla Patronal. Además existe un endpoint dedicado solo a cambiarlo (`reassign-group`). Nada de eso tendría sentido sobre un campo prescindible.

**No es que la ayuda esté mal redactada: describe un comportamiento que el servidor no tiene.**

#### Impacto

El usuario recorre el formulario, lee «Optional», lo salta, y recibe un `400` al guardar — con una clave (`locationGroupId`) que **ni siquiera coincide con el nombre del campo**, así que el frontend probablemente no sepa a qué control asociar el error y el mensaje aparecerá suelto o en ninguna parte.

#### Contrato para el frontend

```jsonc
POST /v1/work-centers
{ "…": "…", "locationGroupPublicId": "…" }   // Guid · REQUERIDO
```

Solo se aceptan grupos de un nivel con `allowsWorkCenters = true` — hoy, el nivel 3 (municipio). Un grupo de nivel 1 o 2 responde `409`.

| Error | HTTP | Clave |
|---|---|---|
| *(validación)* | `400` | `locationGroupId` ⚠️ *(no `locationGroupPublicId`)* |
| `LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER` | `409` | — |

#### Ajuste pedido al frontend

Marcarlo obligatorio: asterisco, `aria-required="true"` y **cambiar la ayuda**, que hoy afirma lo contrario. Sugerido: *«Municipio donde opera el centro. Determina las reglas de ubicación aplicables.»*

Y filtrar el combo a los grupos cuyo nivel admita centros, para que el `409` no sea alcanzable desde la interfaz.

---

### 🟡 F-03 — El contador de notas promete 2000 caracteres y el servidor acepta 1000

**Severidad:** Media · **Tipo:** Validación en cliente

#### Evidencia

El campo declara `maxlength="2000"` y muestra un contador en vivo:

```
78 / 2000
```

El servidor valida `RuleFor(c => c.Notes).MaximumLength(1000)`. Probado:

```
POST /v1/work-centers   notes: "x" × 1500
→ 400   errors: { "notes": [...] }
```

#### Por qué esto es peor que no declarar límite

En los pasos 2, 3 y 4 el reproche era la **ausencia** de `maxlength`: el usuario podía pasarse sin aviso. Aquí hay algo peor: **la interfaz afirma un límite y es el equivocado**, con un contador que va tranquilizando al usuario mientras escribe hacia un rechazo seguro. Un usuario que llene 1500 caracteres los perderá, y el contador le habrá dicho durante todo el rato que le sobraba espacio.

#### Contrato para el frontend

```
notes   máx. 1000   → 400 con la clave `notes`
```

#### Ajuste pedido al frontend

`maxlength="1000"` y contador sobre 1000.

**Vale la pena confirmar de dónde salió el 2000** antes de cambiarlo: si el número vino de una versión anterior del contrato, puede haber otros campos con el mismo desfase. Es una comparación de los `maxlength` del formulario contra los `MaximumLength` del validador, campo por campo. **En esta pantalla ya está hecha**: `notes` es el único desajustado; `code`, `name` y `address` simplemente no declaran nada.

---

### 🟡 F-04 — La etiqueta de la dirección apunta a un `<div>`

**Severidad:** Media · **Tipo:** Accesibilidad

#### Evidencia

```html
<label for="wc-address">Address *</label>
```

```js
document.getElementById('wc-address').tagName        // → "DIV"
document.getElementById('wc-address').querySelector('input')   // → null
```

El identificador `wc-address` pertenece a un `<div>` contenedor. El control real es el componente de Google, que **no tiene identificador propio** y cuyo campo de texto vive en shadow DOM.

Un `for` que apunta a un elemento no rotulable **no asocia nada**: el lector de pantalla anuncia un campo sin nombre, y hacer clic en la etiqueta no enfoca el campo.

Es el único de los diez campos con este problema — los otros nueve (`wc-code`, `wc-name`, `wc-type`, `wc-location-group`, `wc-lat`, `wc-lng`, `wc-phone`, `wc-email`, `wc-notes`) están correctamente asociados. **Es consecuencia de meter un componente de terceros en un patrón de formulario propio**, no de descuido general.

#### Contrato para el frontend

**No interviene ningún endpoint.**

#### Ajuste pedido al frontend

Poner el `for` sobre el control real, o —si el componente no lo permite— sustituirlo por `aria-labelledby` en el propio componente apuntando al `id` de la etiqueta. Se verifica igual que se detectó: comprobando que al hacer clic en «Address» se enfoca el campo.

---

### 🟡 F-05 — El asterisco condicional no llega a la capa de accesibilidad

**Severidad:** Media · **Tipo:** Accesibilidad

#### Evidencia

Al elegir un tipo con `requiresGeo`, las etiquetas pasan a `Latitude *` y `Longitude *`. Pero:

```js
document.getElementById('wc-lat').getAttribute('aria-required')   // → null
document.getElementById('wc-lng').getAttribute('aria-required')   // → null
```

Mientras que los obligatorios fijos sí lo tienen:

```js
document.getElementById('wc-code').getAttribute('aria-required')  // → "true"
document.getElementById('wc-type').getAttribute('aria-required')  // → "true"
```

**La obligatoriedad condicional se comunica solo visualmente.** Quien no vea el asterisco —lector de pantalla— no se entera de que el campo pasó a ser requerido, y solo lo descubrirá con el `400`.

#### ¿Debería estar? Sí, y el propio formulario prueba que sabe hacerlo

No es una función que falte: el mismo formulario pone `aria-required` en los tres obligatorios fijos. Lo que falta es **actualizarlo cuando la obligatoriedad cambia**, que es justo el caso en que más falta hace, porque el usuario no lo puede anticipar.

#### Contrato para el frontend

Los campos condicionales y su disparador:

| Campo | Se vuelve obligatorio cuando el tipo tiene | Error del servidor |
|---|---|---|
| `address` | `requiresAddress = true` | `400 WORK_CENTER_ADDRESS_REQUIRED` |
| `geoLat` **y** `geoLong` | `requiresGeo = true` | `400 WORK_CENTER_GEO_REQUIRED` |

Los tres flags vienen en el listado de tipos (`GET /v1/work-center-types`), así que el cliente ya tiene el dato sin pedir nada extra.

#### Ajuste pedido al frontend

Alternar `aria-required` junto con el asterisco, en la misma reacción al cambio de tipo. **Y anunciar el cambio** con una región viva, porque cambiar un atributo no notifica por sí solo a quien ya tiene el foco en el formulario.

---

### ⚪ F-06 — Dos caminos cambian el grupo, y **los dos validan igual**

**Severidad:** Informativo · **Tipo:** Coherencia de contrato — **medido y correcto**

#### La duda

Hay un endpoint dedicado a reasignar el grupo:

```
PATCH /v1/work-centers/{publicId}/reassign-group     { locationGroupPublicId }
```

Pero `UpdateWorkCenterRequest` **también** incluye `LocationGroupPublicId`, así que el `PUT` puede cambiarlo. Eso contrasta con las unidades organizativas, donde `UpdateOrgUnitRequest` **excluye** `parentPublicId` a propósito para forzar el paso por `/move`, que es quien valida ciclos y profundidad.

La pregunta era si el `PUT` sería una puerta trasera sin validar — el mismo patrón que hizo grave a [00003 / B-02](../ComentariosPruebasBackend/00003-OrgUnits.md#3-b-02--move-no-valida-la-altura-del-subárbol-el-árbol-supera-el-límite-que-el-propio-servidor-declara).

#### Medición — no lo es

Se intentó por las dos vías anclar `SAL-CRG` a un grupo de **nivel 2** (departamento `AHUACHAPAN`), que no admite centros:

| Vía | Respuesta |
|---|---|
| `PUT /v1/work-centers/{id}` con el grupo inválido | **`409 LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`** |
| `PATCH /v1/work-centers/{id}/reassign-group` con el mismo grupo | **`409 LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`** |

**Idénticas.** Y el registro quedó intacto en `LA_PAZ_OESTE`, verificado tras las dos sondas.

🟢 **No hay puerta trasera.** `reassign-group` es un endpoint de conveniencia —permite cambiar solo el grupo sin reenviar el recurso entero—, no el único camino validado.

#### Contrato para el frontend

Los dos caminos son seguros. **`reassign-group` es preferible** cuando solo cambia el grupo: evita reenviar los diez campos y el riesgo de borrar sin querer un opcional que no se incluyó.

```
PATCH /v1/work-centers/{publicId}/reassign-group    If-Match obligatorio
{ "locationGroupPublicId": "…" }
```

#### Ajuste pedido al frontend

**Ninguno.** Se documenta como decisión verificada para que no se levante como sospecha en otra corrida.

---

## 6. Hallazgos de backend

> El detalle completo vive en [`ComentariosPruebasBackend/00005-WorkCenters`](../ComentariosPruebasBackend/00005-WorkCenters.md).

| ID | Sev. | Hallazgo | Origen | Alcance |
|---|---|---|---|---|
| [**B-01**](../ComentariosPruebasBackend/00005-WorkCenters.md#2-b-01--seis-departamentos-de-el-salvador-están-mal-escritos-en-un-catálogo-sembrado-por-geografía) | 🟡 Media | 🟢 **Resuelto 2026-08-21.** El alcance real era **81 filas en 15 tablas**, no 6 en una: los 14 municipios heredaban el error porque su nombre se derivaba del código ASCII, y aparecieron «Banco Agricola», «Espanol» y los nombres de 16 permisos | §3.2 | **15 tablas** |
| [**B-02**](../ComentariosPruebasBackend/00005-WorkCenters.md#3-b-02--la-clave-del-error-nombra-el-campo-interno-y-no-el-público-cuarto-caso-y-el-primero-en-el-cuerpo) | 🟡 Media | 🟡 **Resuelto en 40 de 45 endpoints.** El campo del cuerpo de esta pantalla ✅ está corregido; **el parámetro del buscador aún no** — §2.6 | §2.4 | **45 endpoints** |

Además confirmaba dos transversales, **hoy los dos resueltos**:

| Transversal | Estado |
|---|---|
| Sin `DELETE` ni `/usage` ([00003 / B-04](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca)) | 🟢 **Resuelto en los 5 recursos** — §2.5 |
| Mensajes en inglés ([00003 / B-03](../ComentariosPruebasBackend/00003-OrgUnits.md#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar)) | 🟢 **Resuelto por completo.** El matiz que quedaba —`title` y `detail` en inglés en un `400` de *model-binding*— se cerró el 2026-08-21: hoy la respuesta entera va en el idioma pedido — §2.4 |

**Y hay un efecto nuevo que esta pantalla no conoció:** desde el 2026-08-21, un centro de trabajo
**inactivo no admite plazas nuevas** → `422 POSITION_SLOT_WORK_CENTER_INACTIVE`
([00950 / B-02](../ComentariosPruebasBackend/00950-Remediacion.md)). Las plazas existentes no se tocan.
Si la pantalla ofrece inactivar, conviene decirlo en el diálogo.

---

## 7. Ciclo cubierto y batería de errores

| Operación | Vía | Resultado |
|---|---|---|
| **Crear** | Interfaz — formulario de página completa | 🟢 `201` · lista refrescada |
| **Crear (lote)** | API — las otras cuatro sedes | 🟢 `201` ×4 |
| **Leer** | Interfaz — tabla con tipo, dirección y estado visibles | 🟢 Correcto |
| **Editar** | Interfaz — `SS-CAP` renombrada a *Centro de Entrenamiento y Formación* | 🟢 Guardado · **dirección conservada** pese a verse vacía (**F-01**) |
| **Inactivar** | Interfaz — *Deactivate* | 🟢 Inmediato, sin diálogo nativo (verificado interceptando `confirm`) |
| **Reactivar** | Interfaz — *Activate* | 🟢 Ciclo cerrado, las cinco activas |
| **Reasignar grupo** | API — `reassign-group` y `PUT` | 🟢 Los dos validan el nivel (**F-06**) |
| **Buscar** | Interfaz — `q=SAL` | 🟢 4 de 5 · casa por código **y** por nombre (`SS-CORP` entra por «San Salvador») |
| **Filtrar por estado** | Interfaz — `Inactive` | 🟢 `?status=inactive` → 0 filas con estado vacío correcto |
| **Validación condicional** | Ambas capas | 🟢 §4.1 |

### 7.1 Batería de errores — 9 de 9 correctos

| # | Sonda | Respuesta |
|---|---|---|
| 1 | Código duplicado | `409 WORK_CENTER_CODE_CONFLICT` |
| 2 | Sin grupo de ubicación | `400` · clave `locationGroupId` |
| 3 | Grupo de nivel 2 (departamento) | `409 LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER` |
| 4 | Tipo exige dirección, sin dirección | `400 WORK_CENTER_ADDRESS_REQUIRED` |
| 5 | Tipo exige geo, solo latitud | `400 WORK_CENTER_GEO_REQUIRED` |
| 6 | Correo mal formado | `400` · clave `email` |
| 7 | Latitud 95 | `400` · clave `geoLat` |
| 8 | `PUT` sin `If-Match` | `400` · clave **`If-Match`** |
| 9 | `PUT` con `If-Match` vencido | `409 CONCURRENCY_CONFLICT` |

🟢 **Y se cerró la medición que el Paso 4 dejó pendiente**: con `SS-CORP` usando el tipo `OFICINA`, intentar inactivar ese tipo responde **`409 WORK_CENTER_TYPE_IN_USE`**. El guard funciona.

### 7.2 Datos cargados

| Código | Nombre | Tipo | Grupo (municipio) |
|---|---|---|---|
| `SAL-EST` | Estación SAL — Aeropuerto Int. Mons. Óscar A. Romero | `ESTACION_AEROPUERTO` | La Paz Oeste |
| `SAL-HGR` | Hangar de Mantenimiento SAL | `HANGAR` | La Paz Oeste |
| `SAL-CRG` | Terminal de Carga SAL | `TERMINAL_CARGA` | La Paz Oeste |
| `SS-CORP` | Oficina Corporativa San Salvador | `OFICINA` | San Salvador Centro |
| `SS-CAP` | Centro de Entrenamiento | `CENTRO_ENTRENAMIENTO` | La Libertad Este |

**Las cinco activas**, con dirección, coordenadas, teléfono `+503`, correo y notas.

---

## 8. Qué NO se probó, y cómo se va a medir

| Pendiente | Montaje | Cuándo |
|---|---|---|
| **`WORK_CENTER_HAS_ACTIVE_DEPENDENCIES`** | Necesita plazas ancladas a un centro. **Se produce solo en el Paso 7**, sin montaje propio | Paso 7 |
| **`403 LOCATIONS_FORBIDDEN`** | Segundo usuario con rol restringido en la empresa desechable | Próxima sesión |
| **`PATCH` de JSON Patch** | La interfaz no lo usa; el `PUT` cubre la edición. Se probará por API con `application/json-patch+json` | Al cerrar la corrida |
| **Corregir la guía de AVIANCA** (§3.1) | **No es medición**: es corrección documental de los cinco municipios y del departamento de Antiguo Cuscatlán | Antes de la próxima carga de escenario |

**Nada depende de un insumo externo**, y los dos primeros se cierran dentro de la propia secuencia.

> **Nota de método — el ciclo se cerró en una segunda pasada.** En la primera redacción de este documento se dieron por no ejecutados *editar*, *inactivar* y *reactivar*, justificándolo con que «el ciclo se ejercitó completo en el Paso 4 sobre el mismo patrón de pantalla». **Ese argumento es exactamente el que §7 del README prohíbe**: otra pantalla no cubre a esta, y aquí menos todavía —el formulario es de página completa en vez de panel lateral, y el recurso tiene un endpoint (`reassign-group`) que el otro no tiene—. Se ejecutaron los tres, más la concurrencia, la búsqueda, el filtro y la duda de **F-06**.

---

## 9. Estado del asistente al cerrar el paso

```
Setup progress: 5 / 8 complete
```

| Paso | Estado |
|---|---|
| 1 · Legal profile | ✅ Completed |
| 2 · Unit types | ✅ Completed |
| 3 · Org units | ✅ Completed |
| 4 · Work center types | ✅ Completed |
| **5 · Work centers** | ✅ **Completed** |
| 6 · Job profiles | 🟢 **Available** — siguiente |
| 7 · Position slots | 🔒 Requiere perfiles de puesto |
| 8 · Personnel files | 🔒 Requiere plazas |

---

## 10. Para reintegrar y publicar — revisión de esta pantalla

> **Cómo leer esta lista.** No sabemos qué tiene hoy el cliente, así que **no está escrita como «cambios»
> sino como comprobaciones**. Si ya coincide, no hay nada que tocar.

### 10.1 Lo único que puede romper algo: comparar catálogos por `name`

**Los nombres de los grupos de ubicación cambiaron el 2026-08-21** (§3.2). Seis departamentos y catorce
municipios ganaron su tilde o su eñe:

```
Cabanas       →  Cabañas
Cabanas Este  →  Cabañas Este
Ahuachapan    →  Ahuachapán
…
```

**Los códigos NO cambiaron** (`CABANAS`, `CABANAS_ESTE`, `AHUACHAPAN`…).

| Si el cliente… | Qué pasa |
|---|---|
| Compara o busca por **`code`** | ✅ Nada. Sigue funcionando |
| Compara por **`name`** para preseleccionar, mapear o rotular | ⚠️ **Deja de casar, sin dar error** |
| Tiene una tabla de equivalencias guía → catálogo escrita con los nombres viejos | ⚠️ Hay que revisarla |

**Es el único punto de este documento que puede romper algo, y falla en silencio.** Merece una búsqueda
de literales en el cliente antes de publicar.

> ✅ **La búsqueda del servidor ya no distingue acentos**: `q=cabanas` encuentra «Cabañas». Un buscador
> que envíe lo tecleado funciona con o sin tilde, y **es más tolerante que un filtrado en cliente**.

### 10.2 Lo que mejoró solo, sin tocar el cliente

| # | Qué | Efecto |
|---|---|---|
| 1 | La clave del error del grupo ya es **`locationGroupPublicId`** | Si el cliente mapea `errors[campo]` → control, **ahora encuentra el control**. Antes recibía `locationGroupId` y no casaba con nada. **Afecta directamente a F-02** |
| 2 | Los mensajes llegan en español | Nada que hacer |
| 3 | Los nombres del catálogo se muestran bien acentuados | Lo que el usuario ve mejora sin tocar la pantalla |

### 10.3 Dos reglas de presentación de errores que conviene fijar

1. **Mostrar los mensajes de `errors`, no `title` ni `detail`.** Los dos caminos que producen un `400`
   traducen ya la respuesta entera (§2.4), así que esto dejó de ser una cuestión de idioma: `title` es
   un texto genérico que no dice **qué** campo falló, y `errors[campo]` sí.
2. **La clave del buscador es `q` en los 45 endpoints** (§2.6). Si mapeaste `search` y `q` al mismo
   control mientras faltaban cinco, puedes dejarlo: `search` ya no aparece.

### 10.4 Capacidad nueva, opcional — el borrado condicional

`GET /usage` + `DELETE` (§2.5). Con una particularidad que no tienen los otros recursos:

**`employmentAssignmentReferences` cuenta algo que la base de datos no protege.** La asignación de
expediente guarda el `publicId` del centro sin clave foránea. Un centro **sin plazas** puede seguir
teniendo doce empleados asignados, y el contador es la única forma de saberlo antes de intentar el
borrado.

### 10.5 Un efecto nuevo que conviene advertir al usuario

Desde el 2026-08-21, **un centro inactivo no admite plazas nuevas** → `422
POSITION_SLOT_WORK_CENTER_INACTIVE`. Las plazas existentes siguen funcionando.

Si la pantalla ofrece inactivar, el diálogo debería mencionarlo: sin eso, el usuario da de baja una sede
aquí y descubre el efecto al intentar crear una plaza, con un error que no menciona la sede.

### 10.6 Lo que NO cambió y no hay que tocar

- Los ocho endpoints originales, sus verbos y su autorización.
- **`locationGroupPublicId` sigue siendo requerido** — F-02 sigue vigente: el formulario lo presenta como opcional.
- **`notes` sigue aceptando 1000**, no 2000 — F-03 sigue vigente.
- La validación condicional por tipo (`requiresAddress` / `requiresGeo`) y sus dos códigos de error.
- **Los 44 municipios siguen siendo los de la reorganización de 2023** (§3.1): la guía de AVIANCA sigue
  nombrando municipios que no existen en el catálogo, y **sus tres equivalencias siguen siendo válidas**.
- `If-Match` obligatorio en las cinco escrituras, tomado **del cuerpo**.
- **F-06 no es un defecto**: `PUT` y `reassign-group` validan igual, medido.

### 10.7 Orden sugerido para volver a probar de cero

1. **§10.1 primero**: buscar literales de nombres de catálogo en el cliente. Es lo único que falla en
   silencio.
2. **F-02** (el grupo presentado como opcional): es el hallazgo de mayor impacto y **ahora es más fácil**,
   porque la clave del error ya nombra el campo.
3. **F-01** (dirección invisible en tema oscuro) — de presentación, no depende de nadie.
4. **F-03** (contador que promete 2000 y el servidor acepta 1000).
5. F-04 y F-05 (accesibilidad) después.
6. §2.5 (borrado) es opcional.

> ⚠️ **Este documento no se ha vuelto a probar contra el ambiente.** Lo revalidado el 2026-08-21 es **el
> contrato del servidor** y **el contenido real del catálogo sembrado**, con volcados tomados contra este
> mismo recurso. Los hallazgos F-01…F-06 siguen tal como se observaron: no se han añadido ni retirado,
> porque no se ha repetido la corrida.
