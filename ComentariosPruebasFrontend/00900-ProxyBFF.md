# 00900 — Proxy / BFF · Hallazgos que **no** puede arreglar el backend

| | |
|---|---|
| **ID** | 00900-ProxyBFF |
| **Dueño** | **Quien mantiene el BFF / proxy del mismo origen** — no está en el repositorio `CLARIHR-backend` |
| **Detalle completo** | En `ComentariosPruebasBackend/`, donde se levantaron. Este documento es el índice accionable |
| **Fecha** | 2026-08-16 · **revalidado el 2026-08-21** |
| **Ambiente** | `https://dashboard.clarihr.com` |

> **Revalidación del 2026-08-21.** Se comprobó el lado del backend de los cuatro hallazgos contra el
> código y el contrato publicado. **Los cuatro siguen vigentes y ninguno ha cambiado de dueño.** Lo que
> sí cambió es que **la predicción del hallazgo 3 se cumplió**: hay 13 rutas nuevas bajo `/v1` que el
> proxy tendrá que reenviar, y probablemente ya están rotas. Ver §5.

---

## Por qué existe este documento

Las dos carpetas de esta corrida se reparten por **quién arregla**:

```
ComentariosPruebasFrontend/  ← lo que arregla el frontend
ComentariosPruebasBackend/   ← lo que arregla el backend
```

**Hay un tercer dueño que no tenía sitio.** **Cuatro** hallazgos apuntan al **proxy del mismo origen** que traduce `/v1/...` a la API. Ese proxy **no está en `CLARIHR-backend`**: el repositorio contiene `src/`, `tests/` y `docs/`, y no hay ninguna configuración de reenvío en él.

Estaban archivados en la carpeta de backend, que es justo quien **no puede** tomarlos. Uno llevaba la marca de «otro repositorio»; los demás figuraban como `🔲 Propuesto`, lo que los hacía parecer deuda del backend.

> **El cuarto llegó por reclasificación.** `/diagram-export` nació como defecto del backend y **se descartó midiendo**: el código no cambia desde el 7 de junio y sirve los tres formatos con datos realistas. Ver el bloque 2.

> **El backend hace su parte bien en los cuatro casos.** Emite las cabeceras correctas, declara las rutas correctas, responde los códigos correctos y genera los tres formatos de diagrama. Lo que falla está entre el navegador y la API.

**Uno de los cuatro bloquea la configuración guiada en el Paso 7, y otro deja sin funcionar la exportación del organigrama.**

---

## Los cuatro, por gravedad

| # | Hallazgo | Sev. | Qué rompe hoy | Detalle |
|---|---|---|---|---|
| 1 | Los endpoints de transición de estado no se reenvían | 🔴 **Alta** | **Bloquea los Pasos 7 y 8** del asistente | [00006 / B-01](../ComentariosPruebasBackend/00006-JobProfiles.md#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy) |
| 2 | **`/diagram-export` devuelve `500`** en 2 de sus 3 formatos | 🔴 **Alta** | La exportación del organigrama no funciona | [00003 / B-01](../ComentariosPruebasBackend/00003-OrgUnits.md#2-b-01--diagram-export-devuelve-500-con-su-formato-por-defecto) |
| 3 | El espacio `/v1` no se comporta como espacio de API | 🟡 Media | Diagnóstico engañoso para cualquiera que integre | [00003 / B-05](../ComentariosPruebasBackend/00003-OrgUnits.md#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500) |
| 4 | El proxy descarta cabeceras del upstream | 🟡 Media | Latente — el frontend actual se las apaña | [00001 / B-03](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location) |

**Los cuatro comparten causa probable**: una tabla de reenvío que enumera caminos permitidos, y todo lo que no casa cae en el comodín del SPA. Arreglar (1) con un parche puntual deja (2) vivo y garantiza que el próximo endpoint nuevo repita el problema.

---

## 1 · 🔴 Los tres endpoints de transición de estado no se reenvían

**Este es el que bloquea la configuración guiada.** Sin él no se puede publicar un perfil de puesto; sin perfil publicado no hay plazas; sin plazas no hay expedientes.

### Qué se midió

`PATCH /v1/job-profiles/{publicId}/publication`, con `If-Match` válido:

| Cómo se llama | Respuesta | Tipo de contenido |
|---|---|---|
| **Sin cuerpo** *(que es lo que el endpoint espera)* | **`200`** | **`text/html`** — el índice del SPA |
| Con `{}` y `Content-Type: application/json` | **`500`** | `application/json` |
| Sin `If-Match` | **`200`** | **`text/html`** |

Idéntico en `/reopening` y `/archival`.

**Y la ruta base del mismo recurso sí se reenvía.** `PATCH /v1/job-profiles/{id}` llega a la API y devuelve su error de validación real. El corte está en el **sub-camino**, no en el recurso.

### Consecuencia verificada de punta a punta

```
POST /v1/position-slots   (contra un perfil en Draft)
→ 422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED
   "The selected job profile is not published. Publish the job profile before creating…"
```

Con 32 perfiles cargados, **todos en `Draft`**, el Paso 7 aparece disponible en el asistente y no permite crear ni una plaza.

### Qué hay que reenviar

```
PATCH  /v1/job-profiles/{publicId}/publication    → sin cuerpo · If-Match obligatorio
PATCH  /v1/job-profiles/{publicId}/reopening      → { "reason": "…" } · If-Match obligatorio
PATCH  /v1/job-profiles/{publicId}/archival       → sin cuerpo · If-Match obligatorio
```

**Las tres existen y están bien escritas en la API** (`JobProfileResolutionController.cs:37`, `:68`, `:99`).

> **Revalidado el 2026-08-21, y con una comprobación que faltaba:** el controller se implementó en el
> commit `13907ca` del **2026-08-09**, siete días **antes** de la medición. No es que las rutas no
> existieran todavía cuando se probó — existían, y siguen existiendo hoy en el contrato publicado. **El
> corte está en el proxy**, y la evidencia lo sostiene.

### ⚠️ Hay un segundo defecto en el mismo camino, y ese sí es del frontend

El botón **Publish** de la pantalla **llama al endpoint equivocado**: manda `status` por el `PATCH` genérico. El servidor responde:

> *Status cannot be patched. Use PATCH /job-profiles/{publicId}/publication, /reopening or /archival.*

Ese campo se retiró del contrato **a propósito**, y el código lo documenta: era la puerta por la que cualquier administrador de perfiles publicaba **sin tener el permiso dedicado** `JobProfiles.Publish`. Fue un arreglo de seguridad y el cliente no se actualizó.

**Arreglar solo el proxy no basta, ni arreglar solo el botón.** Están en [00006 / F-01](00006-JobProfiles.md#-f-01--el-botón-publish-llama-a-un-endpoint-que-el-backend-cerró-a-propósito) y hay que hacer los dos.

---

## 2 · 🔴 `/diagram-export` devuelve `500` en 2 de sus 3 formatos

**Lo que ve el usuario:** la exportación del organigrama no funciona. El formato **por defecto** es uno de los rotos, así que basta pulsar el botón sin elegir nada.

### Qué se midió (contra el ambiente desplegado, 26 unidades)

| Petición | Resultado |
|---|---|
| `GET /v1/org-units/diagram-export` *(sin `format` → default `graphml`)* | 🔴 **`500`** |
| `GET /v1/org-units/diagram-export?format=graphml` | 🔴 **`500`** |
| `GET /v1/org-units/diagram-export?format=dot` | 🔴 **`500`** |
| `GET /v1/org-units/diagram-export?format=json` | 🟢 `200` · 8 011 bytes |
| `?format=svg` y otros inválidos | 🟢 `400 REPORT_FORMAT_NOT_SUPPORTED` |

**El `500` no trae `code`**, solo `title: "Internal server error"` — excepción no controlada, no error de dominio.

**Dato revelador:** un formato **inválido** se comporta mejor que el **por defecto**. Quien pruebe con `?format=svg` recibe un `400` correcto y concluirá que el endpoint funciona.

### El backend está descartado, con evidencia

Este hallazgo nació clasificado como defecto del backend. **No lo es**, y se comprobó midiendo:

| Comprobación | Resultado |
|---|---|
| `git log` del writer y del controlador | Sin cambios desde el **7 de junio** — el código local **es** el desplegado |
| Los tres formatos con **26 unidades acentuadas**, `&` y `<>`, contra el API | 🟢 **pasan** |
| **Sin `format`** — el caso exacto del `500` | 🟢 **pasa** |
| Caracteres no-ASCII como causa | ❌ descartado |

La prueba vive en `OrgUnits_DiagramExport_WithRealisticGraph_ShouldSucceedInEveryFormat`.

### Por qué apunta aquí

Encaja con el patrón del hallazgo 3: **el `500` no trae `code`** —excepción no controlada, como los otros del proxy— y **`?format=json` pasa mientras `graphml` y `dot` revientan**. La diferencia entre esas ramas no es la consulta ni los datos: es **el tipo de contenido de la respuesta**.

| Formato | Tipo de contenido | A través del proxy |
|---|---|---|
| `json` | `application/json` | 🟢 |
| `graphml` | `application/graphml+xml` | 🔴 |
| `dot` | `text/vnd.graphviz` | 🔴 |

> ⚠️ **Esto es coherencia, no prueba.** Hay un dato que no encaja del todo: la exportación CSV del mismo módulo devuelve `text/csv` y **sí** atraviesa el proxy. Conviene tenerlo presente al diagnosticar.

### La medición que lo cierra

Pedir los tres formatos **a través del proxy** y compararlos con la misma petición **directa al API**:

| Si el directo da… | Y el proxy da… | Entonces |
|---|---|---|
| `200` | `500` | Es del proxy — confirmado |
| `500` | `500` | Vuelve al backend, y la prueba local no reproduce la condición |

**Es una petición por formato, sin montaje.** Solo hace falta acceso directo al API además del navegador.

---

## 3 · 🟡 El espacio `/v1` no se comporta como un espacio de API

### Qué se midió

**(a) Una ruta que no existe devuelve `200` con la página de la aplicación.**

```
GET /v1/unit-typez             → 200   text/html   (el HTML del SPA)
GET /v1/ruta-que-no-existe     → 200   text/html
```

**Esto ya produjo un diagnóstico falso durante la corrida.** Al mapear los prerrequisitos del Paso 6 se probaron `/v1/position-function-types` y `/v1/position-contract-types`: las dos respondieron `200` y se dieron por catálogos existentes y vacíos. **No existían con ese nombre.** Solo se detectó al mirar el `Content-Type`.

**(b) El mismo recurso responde distinto según el nombre de ruta, y solo en escritura.**

La API llama al recurso `organization-units`; el frontend lo consume como `org-units`. Los dos nombres llegan:

| Petición | `org-units` (alias) | `organization-units` (nombre real) |
|---|---|---|
| `GET /{id}` | `200` | **`200`** |
| `PATCH /{id}/activate` | `409` *(correcto)* | **`500`** |
| `PATCH /{id}/move` | `200` | **`500`** |

**La lectura funciona con los dos nombres; la escritura solo con uno**, y el otro no da `404` sino `500`.

### Qué se pide

1. **Que `/v1/...` nunca caiga en el comodín del SPA.** Lo que empiece por el prefijo de la API y no case debe responder `404` con cuerpo `application/problem+json`, no HTML.
2. **Decidir el nombre canónico y cerrar el otro.** Que `GET` acepte los dos y `PATCH` solo uno no es una decisión, es un descuido.
3. **Reenviar todo `/v1/**` por defecto** y dejar que la API responda `404` a lo que no exista, en vez de mantener una lista de caminos que hay que acordarse de ampliar con cada endpoint nuevo. **Con esto, el hallazgo 1 se arregla solo y no vuelve a repetirse.**

> ⚠️ **Antes de abrir el reenvío por defecto**, conviene inventariar qué hay bajo `/v1` en la API y confirmar que la autorización de cada endpoint se sostiene por sí sola — que es como debe estar de todos modos: **el proxy no debe ser una capa de seguridad.**

---

## 4 · 🟡 El proxy descarta cabeceras del upstream

### Qué se midió

El backend emite `ETag`, `Location` y `Content-Disposition` correctamente —vía `ToActionResultWithETag` y `ToCreatedAtActionResult`, y el swagger las documenta—. **Al navegador no llegan**, y en su lugar aparece un `ETag` propio del proxy.

Ejemplo del perfil legal:

```
concurrencyToken del cuerpo  = 32bffe02-ad74-4521-89ee-f32b7495838b   ← el que exige If-Match
ETag que llega al navegador  = (uno distinto, generado por el proxy)
```

### Impacto

**Hoy es latente, no actual**: el frontend usa el `concurrencyToken` del **cuerpo** de la respuesta, así que no depende del `ETag`. Y en las descargas fija el nombre de archivo por su cuenta.

Pero es real para **cualquier consumidor que no sea el frontend actual**: un cliente que siga el contrato tal como está documentado usaría el `ETag`, y recibiría el del proxy — que no sirve para `If-Match`.

### Qué se pide

Desactivar el `ETag` propio del proxy en las rutas *proxied* y reenviar las cabeceras del upstream sin tocarlas.

---

## 5 · La predicción del hallazgo 3 ya se cumplió — 13 rutas nuevas

El hallazgo 3 pedía **reenviar todo `/v1/**` por defecto** en vez de mantener una lista de caminos, con
este argumento:

> *«…en vez de mantener una lista de caminos que hay que acordarse de ampliar con cada endpoint nuevo.»*

**Entre el 2026-08-16 y el 2026-08-21 el backend añadió 10 rutas nuevas**, y hay 3 más que ya existían.
Si el proxy enumera caminos, **las 13 están rotas hoy** y nadie lo ha notado porque ninguna pantalla las
usa todavía.

### Las 13 rutas

**Diez son nuevas** (resolución de [00003 / B-04](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca)
y su extensión):

```
GET     /v1/organization-structure-catalogs/unit-types/{publicId}/usage
DELETE  /v1/organization-structure-catalogs/unit-types/{publicId}
GET     /v1/organization-structure-catalogs/functional-areas/{publicId}/usage
DELETE  /v1/organization-structure-catalogs/functional-areas/{publicId}
GET     /v1/organization-units/{publicId}/usage
DELETE  /v1/organization-units/{publicId}
GET     /v1/work-center-types/{publicId}/usage
DELETE  /v1/work-center-types/{publicId}
GET     /v1/work-centers/{publicId}/usage
DELETE  /v1/work-centers/{publicId}
```

**Tres ya existían** y conviene comprobarlas de paso:

```
GET     /v1/cost-centers/{publicId}/usage
GET     /v1/legal-representatives/{publicId}/usage
GET     /v1/location-groups/{publicId}/usage
```

### Por qué estas dos formas son exactamente las que fallan

Reproducen **los dos cortes ya medidos** en este documento:

| Forma | Ya falló antes en | Riesgo |
|---|---|---|
| **Sub-camino nuevo** (`…/{id}/usage`) | Las tres transiciones de estado (§1) — la ruta base sí llegaba, el sub-camino no | Alto |
| **Verbo nuevo sobre ruta existente** (`DELETE …/{id}`) | No medido, pero si la tabla enumera método+camino, el `DELETE` cae en el comodín igual que el sub-camino | Alto |

⚠️ **El `DELETE` es el caso peligroso.** Si cae en el comodín del SPA, la respuesta es **`200` con HTML**
(§3a): el cliente creerá que **borró algo que sigue existiendo**. Un `500` se nota; un `200` falso, no.

### Qué se pide aquí

**Nada nuevo: es el mismo arreglo del hallazgo 3.** Estas 13 rutas son la razón concreta para hacerlo por
la raíz y no con parches. Un parche puntual por cada una son 13 entradas hoy y las que vengan mañana.


---

## Cómo verificar que quedó arreglado

Las tres se comprueban con peticiones sueltas, sin montaje:

| # | Comprobación | Debe dar |
|---|---|---|
| 1 | `PATCH /v1/job-profiles/{id}/publication` con `If-Match`, sin cuerpo | Respuesta **JSON** de la API — `200`, o `422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` si falta contenido mínimo. **Nunca HTML** |
| 1b | `GET /v1/org-units/diagram-export` (sin `format`) | Un archivo GraphML, **nunca `500`** |
| 2a | `GET /v1/ruta-que-no-existe` | **`404`** con `application/problem+json` |
| 2b | `PATCH /v1/organization-units/{id}/activate` | `404` o el mismo resultado que el alias — **nunca `500`** |
| 3 | `GET /v1/legal-profile` y comparar | El `ETag` de la respuesta **coincide** con el `concurrencyToken` del cuerpo |
| 4 | `GET /v1/organization-units/{id}/usage` | **JSON** con `activeChildren`, `jobProfileActiveReferences`… — **nunca HTML** |
| 5 | `DELETE /v1/work-center-types/{id}` **sin** `If-Match` | **`400`** de la API pidiendo la cabecera. ⚠️ **Si devuelve `200` con HTML, el cliente creerá que borró algo que sigue existiendo** |

Y la prueba de que el Paso 7 quedó desbloqueado: publicar un perfil desde la pantalla y crear una plaza contra él.

> **La comprobación 5 es la más valiosa de la tabla**, porque es la única cuyo fallo **no se ve**. Las
> demás producen un `500` o un HTML donde se esperaba JSON, y eso salta. Un `DELETE` que devuelve `200`
> con la página del SPA se parece demasiado a un borrado correcto.

---

## Estado

Los cuatro quedan **⏸️ Bloqueados desde la perspectiva de este repositorio** — abiertos, no descartados. El impacto está medido y es real; lo que no está aquí es el código que hay que tocar.

**Revalidados el 2026-08-21 contra el código y el contrato publicado:**

| # | Hallazgo | Sigue vigente | Qué cambió desde el 2026-08-16 |
|---|---|---|---|
| 1 | Transiciones de estado no reenviadas | ✅ **sí** | Se confirmó que el controller existe desde el **2026-08-09**, siete días antes de la medición. La evidencia se refuerza |
| 2 | `/diagram-export` responde `500` | ✅ **sí** | El endpoint sigue declarando sus tres formatos en el contrato. **El backend sigue descartado** |
| 3 | El espacio `/v1` no se comporta como API | ✅ **sí** | **Su predicción se cumplió**: 13 rutas nuevas (§5) |
| 4 | Cabeceras del upstream descartadas | ✅ **sí** | Sin cambios |

⚠️ **El hallazgo 1 sigue bloqueando los Pasos 7 y 8 de la corrida.** Es el único de los cuatro que impide
seguir probando el producto, y su arreglo **no depende del backend ni del frontend**: depende de este
repositorio de proxy. Mientras no se resuelva, la campaña de pruebas no puede continuar más allá del
Paso 6.

> **Y recordar la segunda causa:** aunque el proxy se arregle, el botón *Publish* seguiría llamando al
> endpoint equivocado ([00006 / F-01](00006-JobProfiles.md#-f-01--el-botón-publish-llama-a-un-endpoint-que-el-backend-cerró-a-propósito)).
> **Hay que hacer las dos cosas** para desbloquear el Paso 7.

**Su detalle completo, con evidencia y bitácora, sigue en `ComentariosPruebasBackend/`**, donde se levantaron. No se movieron para no romper las referencias cruzadas de los demás documentos; este índice es el que hay que entregar a quien mantiene el BFF.
