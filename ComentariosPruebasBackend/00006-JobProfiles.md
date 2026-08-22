# 00006 — JobProfiles · Hallazgos de backend

| | |
|---|---|
| **ID** | 00006-JobProfiles |
| **Documento espejo** | [`ComentariosPruebasFrontend/00006-JobProfiles`](../ComentariosPruebasFrontend/00006-JobProfiles.md) |
| **Paso probado** | Paso 6 de 8 — *Job profiles* |
| **Pantalla que los destapó** | `/job-profiles` |
| **Fecha** | 2026-08-15 |
| **Ambiente** | `https://dashboard.clarihr.com` |

---

## 1. Resumen

| ID | Sev. | Hallazgo | Componente | Origen | Alcance | Estado |
|---|---|---|---|---|---|---|
| [B-01](#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy) | 🔴 Alta | Los tres endpoints de transición de estado **no están enrutados**: sin cuerpo devuelven `200` con HTML, con cuerpo `500`. Publicar es imposible desde el navegador | BFF · proxy de `/v1` | Espejo F-01 | **Instancia grave de [00003 / B-05](00003-OrgUnits.md#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500)** | ⏸️ Bloqueado (BFF) |
| [B-02](#3-b-02--el-asistente-declara-disponible-un-paso-cuya-cadena-de-prerrequisitos-está-vacía) | 🟡 Media | El servicio de progreso cuenta filas, no utilidad: marca completado un paso cuyos 32 registros no sirven aguas abajo | Application · progreso del asistente | Espejo §1 | El servicio de progreso | ⛔ **Reclasificado a frontend** |

**B-01 sí bloquea al frontend**, y es el primero de la corrida que lo hace: no hay vía alterna desde el navegador.

### Nota de contraste — lo que este módulo hace muy bien

Es, con diferencia, el módulo mejor diseñado de los seis probados. Vale la pena que conste, porque el hallazgo Alta es de **enrutado**, no de diseño:

- **La máquina de estados está pensada con cuidado.** `Draft` → `Published` **congela el descriptor completo** —el núcleo y sus 9 colecciones— y la reapertura es explícita y exige un motivo. Y hay una excepción razonada: **la matriz de competencias NO se congela**, porque es una capa operativa sobre un descriptor aprobado. Esa distinción está documentada en el propio endpoint.
- **El permiso de publicación es dedicado y no se hereda.** `JobProfiles.Publish` **no** está implicado por `JobProfiles.Admin`. Es una separación deliberada entre quien redacta y quien aprueba.
- **Ya se cerró una escalada de privilegio, y quedó documentada en el código**: `status` se retiró del contrato de `PATCH` porque era la puerta por la que cualquier administrador publicaba sin el permiso. El comentario explica el porqué, no solo el qué. **Ese arreglo es la causa del defecto de cliente F-01** — el backend hizo lo correcto y el frontend no siguió.
- **El contenido mínimo para publicar se valida en el servidor**: objetivo, responsabilidades, al menos un requisito y al menos una función.
- **El guard aguas abajo existe y funciona**: `422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED`, verificado en vivo. Un perfil sin publicar no puede sostener una plaza.

---

## 2. B-01 — Los tres endpoints de transición de estado no están enrutados por el proxy

| | |
|---|---|
| **Severidad** | 🔴 Alta |
| **Estado** | ⏸️ **Bloqueado** — fuera de este repositorio · ver [00900 / ProxyBFF](../ComentariosPruebasFrontend/00900-ProxyBFF.md) |
| **Componente** | BFF · proxy de `/v1` |
| **Dueño** | **Quien mantiene el BFF** — no es deuda del backend · índice accionable en [00900 / ProxyBFF](../ComentariosPruebasFrontend/00900-ProxyBFF.md) |
| **Origen** | Espejo F-01 — al intentar publicar por la interfaz |
| **Alcance** | Tres endpoints · **misma causa que [00003 / B-05](00003-OrgUnits.md#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500)** |

### 2.1 Evidencia

`PATCH /v1/job-profiles/{publicId}/publication`, con `If-Match` válido, según lo que se mande:

| Petición | Respuesta | Tipo de contenido |
|---|---|---|
| Sin cuerpo *(que es lo que el endpoint espera)* | **`200`** | **`text/html`** — el índice del SPA |
| Con `{}` y `Content-Type: application/json` | **`500`** | `application/json` — `{"type":"server-error","title":"Internal server error"}` |
| Sin `If-Match` | **`200`** | **`text/html`** |

Idéntico en `/reopening` y `/archival`.

**Y la ruta base del mismo recurso sí está enrutada.** El intento de publicar de la interfaz —que va contra `PATCH /v1/job-profiles/{id}`— **llega al API y devuelve su error de validación real**:

> *«Status cannot be patched. Use PATCH /job-profiles/{publicId}/publication, /reopening or /archival.»*

Es decir: **`/v1/job-profiles/{id}` se reenvía; `/v1/job-profiles/{id}/publication` no.** El corte está en el sub-camino, no en el recurso.

### 2.2 Causa

No se inspeccionó la configuración del proxy, así que esto es hipótesis: **la tabla de reenvío del BFF enumera caminos y no cubre estos tres**. Lo que no casa cae en el comodín del SPA cuando la petición parece navegable, y revienta cuando no.

Encaja exactamente con [00003 / B-05](00003-OrgUnits.md#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500), donde `PATCH /v1/organization-units/{id}/move` daba `500` mientras el `GET` del mismo nombre funcionaba. **Aquel se descubrió con un nombre de ruta inventado y parecía anecdótico. Este es con la ruta correcta, sobre la acción principal del módulo.**

**La comprobación que lo cierra:** leer la configuración de reenvío del BFF y contar cuántos sub-caminos de escritura quedan fuera. Es una lectura de archivo, sin ambiente.

### 2.3 Impacto

**Rompe la cadena de la configuración guiada, y en silencio.**

```
32 perfiles en Draft
   └─ no se pueden publicar        (este hallazgo)
       └─ no se pueden crear plazas  (422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED, verificado)
           └─ no se pueden crear expedientes  (Paso 8 depende de plazas)
```

**Los Pasos 7 y 8 son inalcanzables.** Y el asistente no lo advierte: marca el 6 como completado y ofrece el 7.

**No hay vía alterna desde el navegador.** Es el primer hallazgo de la corrida del que se puede decir eso: en todos los anteriores el frontend podía apañarse o el usuario podía trabajar de otro modo. Aquí no hay otro modo.

**Y enmascara un error legítimo.** Con 32 perfiles sin requisitos ni funciones, la respuesta correcta sería `422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`, que le diría al usuario exactamente qué le falta. En vez de eso recibe `500` o una página HTML.

### 2.4 Propuesta

1. **Enrutar los tres sub-caminos** en el BFF: `/publication`, `/reopening`, `/archival`.
2. **No parchear solo estos tres.** El patrón ya apareció dos veces en dos módulos distintos; lo que corresponde es **reenviar todo `/v1/**` al API por defecto** y que sea el API quien responda `404` a lo que no exista, en vez de mantener una lista de caminos permitidos que hay que recordar ampliar con cada endpoint nuevo.
3. **Que `/v1/...` nunca caiga en el comodín del SPA** — ya propuesto en B-05, y este hallazgo muestra por qué importa: un `200 text/html` sobre una acción de escritura es indistinguible de un éxito para un cliente descuidado.

**El orden importa:** con (2) hecho, (1) sobra y no vuelve a pasar.

### 2.5 Compatibilidad

**No rompe nada.** Habilita rutas que hoy no funcionan. El riesgo está en el sentido contrario: reenviar todo `/v1/**` podría exponer endpoints que la lista blanca ocultaba sin querer. **Conviene inventariar qué hay bajo `/v1` en el API antes de abrir el reenvío por defecto**, y confirmar que la autorización de cada uno es correcta por sí misma — que es como debería estar de todos modos, sin depender del proxy como capa de seguridad.

### 2.6 Alcance a revisar

**La pregunta a responder de una vez:** cuántos endpoints del API no están cubiertos por la tabla de reenvío. Se responde comparando las rutas declaradas en los controladores con la configuración del BFF. Dos módulos ya dieron positivo sin buscarlo.

### 2.7 Vía alterna vigente

**Ninguna desde el navegador.** Publicar solo es posible llamando al API directamente, fuera de la aplicación.

### 2.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Detectado al pulsar *Publish*. El error visible era del cliente (F-01); al probar el endpoint correcto apareció este, que es el que de verdad bloquea. **Dos defectos independientes sobre el mismo camino: arreglar solo el botón no habría funcionado** |

---

## 3. B-02 — El asistente declara disponible un paso cuya cadena de prerrequisitos está vacía

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | ⛔ **Reclasificado a frontend** — 2026-08-21 → [`00006 / F-07`](../ComentariosPruebasFrontend/00006-JobProfiles.md#-f-07--el-asistente-declara-completo-un-paso-contando-filas-no-filas-utilizables) |
| **Componente** | ~~Application~~ · **el asistente vive en el cliente** |
| **Origen** | Espejo §1 y §2 |
| **Alcance** | El servicio de progreso — afecta a los ocho pasos |

> ⛔ **No es un defecto de backend. El servicio de progreso no existe en este repositorio.**
>
> §3.4 avisaba: «No se leyó el servicio de progreso; lo primero que debe hacer quien tome el hallazgo es
> leerlo». Al buscarlo, no está:
>
> | Búsqueda | Resultado |
> |---|---|
> | Rutas con `setup`, `progress`, `onboarding`, `step` o `wizard` en el contrato publicado | **0 de 568** |
> | Archivos con `wizard`, `checklist` o `SetupProgress` en `src/` | **0** |
> | Texto «Requires completing first» —que el asistente muestra— en el backend | **0 coincidencias** |
>
> El defecto es real y el análisis de §3.1–§3.4 se sostiene entero; solo estaba apuntando al repositorio
> equivocado. **El ajuste se pide en [`00006 / F-07`](../ComentariosPruebasFrontend/00006-JobProfiles.md#-f-07--el-asistente-declara-completo-un-paso-contando-filas-no-filas-utilizables)**, con las dos llamadas exactas que responden la condición correcta.

### 3.0 Lo que sí hizo el backend

**Se comprobó que el frontend PUEDE calcular la condición correcta**, que era la pregunta pendiente de este
lado (README §6: toda ausencia se juzga, no solo se reporta). Puede, con una llamada barata por paso:

| Paso | Llamada | Devuelve |
|---|---|---|
| 6 | `GET /companies/{id}/job-profiles?status=Published&pageSize=1` | `totalCount` |
| 7 | `GET /companies/{id}/position-slots?status=Vacant&isActive=true&pageSize=1` | `totalCount` |

**No había nada que arreglar, pero sí algo que proteger.** El `status` de una plaza es **derivado** —no existe
la columna; el repositorio lo traduce a predicados SQL—, así que ese filtro podría dejar de llegar a la base de
datos sin que ninguna prueba avisara, y el arreglo del asistente se rompería desde este lado sin tocarlo.

`ApiIntegrationTests.WizardReadiness` fija ambos contratos: que `status=Published` cuente **1 de 3** con un
publicado y dos borradores —sin el filtro serían 3, que es exactamente el defecto del asistente— y que el
conteo de plazas siga al estado derivado al suspenderla. **2/2 en verde.**

### 3.1 Evidencia

**Al entrar al Paso 6**, el asistente lo marcaba *Available* con **cinco catálogos de su cadena de prerrequisitos vacíos**:

```
position-function-types            0
position-contract-types            0
position-category-classifications  0
position-categories                0
occupational-pyramid-levels        0
```

El combo *Position category* del formulario mostraba «No results», sin ninguna indicación de dónde poblarlo.

**Al salir del Paso 6**, lo marca *Completed* con 32 perfiles **que no sirven para el paso siguiente**, y abre el Paso 7:

```
POST /v1/position-slots  →  422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED
```

El asistente **no distingue «hay 32 filas» de «hay 32 filas utilizables»**.

### 3.2 Contraste — el mecanismo sí existe

Esto no es una función que falte. Los Pasos 7 y 8 **sí** declaran su bloqueo con precisión:

> *«Requires completing first: Job profiles.»*
> *«Requires completing first: Position slots.»*

**El asistente sabe expresar dependencias.** Lo que hace mal es **cómo evalúa si están satisfechas**: cuenta existencia de filas en lugar de comprobar la condición que el paso siguiente exige de verdad.

Y la condición está escrita, en el propio endpoint de publicación:

> *«Publishing is also what makes the profile usable downstream — a position slot cannot be created against a profile that is not published.»*

### 3.3 Impacto

**El asistente promete una ruta que no lleva a ningún sitio.** Su valor entero es decirle a un cliente nuevo qué hacer y en qué orden; si marca verde lo que no funciona, es peor que no tenerlo, porque el usuario confía y descubre el problema dos pasos más tarde sin saber cuál de los dos falló.

En esta corrida ocurrió exactamente así: el Paso 6 se cerró en verde y el defecto apareció al probar el Paso 7.

**Hoy la causa raíz es B-01** —los perfiles no se pueden publicar—, pero **el defecto del asistente es independiente**: aunque publicar funcionara, seguiría marcando el paso completo con 32 borradores.

### 3.4 Propuesta

Que la condición de cada paso sea **la que el paso siguiente necesita**, no la existencia de filas:

| Paso | Hoy, probablemente | Debería ser |
|---|---|---|
| 6 · Job profiles | ≥ 1 perfil | ≥ 1 perfil **publicado** |
| 7 · Position slots | ≥ 1 plaza | ≥ 1 plaza **vacante y vigente** *(por confirmar contra el Paso 8)* |

Y para *Available*, algo más útil que «el paso anterior está completo»: **si la cadena de catálogos que el paso necesita está vacía, decirlo con el mismo detalle con que ya se dicen los bloqueos** — «Faltan categorías de puesto», con enlace a la pantalla.

⚠️ **No se leyó el servicio de progreso.** La columna «hoy, probablemente» es inferencia a partir del comportamiento observado; **lo primero que debe hacer quien tome el hallazgo es leerlo** y confirmar cuál es la condición real de cada paso.

### 3.5 Compatibilidad

**No rompe el contrato**: cambia el valor de un cálculo, no una forma. Pero **cambia el estado visible de la configuración de cualquier empresa**: la de esta corrida pasaría de `6/8` a `5/8`, que es lo correcto.

### 3.6 Alcance a revisar

**Los ocho pasos, de una vez.** La pregunta para cada uno: *¿la condición que declara completo es la que el paso siguiente necesita?* Se responde leyendo el servicio de progreso junto a los guards de cada recurso aguas abajo, sin ambiente.

### 3.7 Vía alterna vigente

Ninguna. El usuario no tiene forma de saber que un paso verde no es utilizable.

### 3.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Detectado al comprobar por qué el Paso 7 quedaba disponible con 32 perfiles inservibles. **Es independiente de B-01**: aunque publicar funcionara, el asistente seguiría contando borradores como progreso |
| 2026-08-21 | ⛔ Reclasificado | **El servicio de progreso no está en este repositorio**: 0 rutas de asistente en las 568 del contrato y 0 archivos con `wizard`/`checklist`. El defecto es real y el análisis se sostiene; se traslada a **00006 / F-07** con las dos llamadas exactas. Del lado del servidor se verificó que la condición correcta **se puede calcular** y se blindaron los dos filtros con `WizardReadiness` (2/2) — el de plazas importa porque su estado es **derivado** |
