# 00004 — WorkCenterTypes · Hallazgos de frontend

| | |
|---|---|
| **ID** | 00004-WorkCenterTypes |
| **Documento espejo** | **No se creó** — este paso no generó hallazgos de backend propios (§6) |
| **Paso probado** | **Paso 4 de 8** — *Work center types* |
| **Pantalla** | `/work-centers?tab=types` · pestaña 2 de 5 |
| **Fecha** | 2026-08-15 |
| **Ambiente** | `https://dashboard.clarihr.com` · empresa `End to End SAS` |

---

## 1. Resumen ejecutivo

🟢 **Es la mejor pantalla de las cuatro probadas, y por un margen claro.** Cobertura de campos **6 de 6**, ciclo completo por la interfaz, filtro de estado que funciona, buscador con el parámetro correcto y `allowedActions` en el listado.

🟡 **La tabla esconde el estado y las acciones.** Al ancho por defecto hay que descubrir un desplazamiento horizontal para ver las columnas *Status* y *Actions*. Un registro inactivo se ve idéntico a uno activo hasta que el usuario descubre que la tabla se mueve (**F-01**).

🟡 **Sin `maxlength` ni `pattern` en cliente**, con el servidor limitando a 50/150/500 y exigiendo un formato de código. **Cuarto módulo con la misma carencia** (**F-02**).

🟢 **Los cinco tipos del escenario AVIANCA quedaron cargados y activos.** El asistente pasó de `3/8` a **`4/8`** y desbloqueó el Paso 5.

🟢 **La batería de errores del servidor responde bien en los siete casos**: duplicado, dos formatos de código inválidos, `If-Match` ausente, `If-Match` vencido, recurso inexistente y búsqueda demasiado corta (§4).

**Frontend** (§5)

| Severidad | Cantidad | IDs |
|---|---|---|
| 🔴 Alta | 0 | — |
| 🟡 Media | 2 | F-01, F-02 |
| 🔵 Baja | 0 | — |
| ⚪ Informativo | 2 | F-03, F-04 |

**Backend** (§6) — **ninguno nuevo.** El paso confirma tres hallazgos transversales ya levantados y **no añade ninguno propio**. Es el primer paso del que se puede decir eso.

---

## 2. Contrato de la pantalla

### 2.1 Endpoints

```
GET    /v1/work-center-types                    listado paginado
GET    /v1/work-center-types/{publicId}         detalle
POST   /v1/work-center-types                    → 201
PUT    /v1/work-center-types/{publicId}         If-Match obligatorio
PATCH  /v1/work-center-types/{publicId}         JSON Patch · If-Match obligatorio
PATCH  /v1/work-center-types/{publicId}/activate     If-Match obligatorio
PATCH  /v1/work-center-types/{publicId}/inactivate   If-Match obligatorio
```

> Ruta real del API: `companies/{companyId}/work-center-types` para listar y crear, `work-center-types/{id}` para el resto. **El frontend usa el alias `/v1/work-center-types` y el proxy resuelve la empresa.** No mezclar los dos nombres — ver [00003 / B-05](../ComentariosPruebasBackend/00003-OrgUnits.md#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500).
>
✅ **Ya hay `DELETE` y `/usage`** desde el 2026-08-21:

```
GET     /v1/work-center-types/{publicId}/usage
DELETE  /v1/work-center-types/{publicId}      ← If-Match obligatorio
```

> **Cuando se probó esta pantalla no existían.** Este documento fue el que subió el alcance de
> [00003 / B-04](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca)
> de dos recursos a cuatro; **acabó cubriendo cinco** y ya está resuelto. Ver §2.6.
>
> **No hay exportación**, y **es coherente**: los catálogos del producto (tipos de unidad, tipos de centro) no exportan; los recursos principales (unidades organizativas, centros de costo) sí. Verificado en los cuatro controladores. **No se levanta hallazgo.**

### 2.2 Cuerpo de `POST` y `PUT`

```jsonc
{
  "code": "ESTACION_AEROPUERTO",   // requerido · máx. 50 · ^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$
  "name": "Estación aeroportuaria",// requerido · máx. 150
  "description": "…",              // opcional  · máx. 500
  "requiresAddress":  true,        // bool NO nullable
  "requiresGeo":      true,        // bool NO nullable
  "allowsBiometric":  true         // bool NO nullable
}
```

> ⚠️ **Los tres booleanos no son nullable.** Omitirlos en el JSON **no da error: los deja en `false`**. No hay forma de distinguir «no lo mandé» de «lo mandé en false», así que **el `PUT` debe enviar siempre los tres**, o una edición parcial los apagará en silencio.
>
> El `PUT` reemplaza el recurso completo: **manda también `description`**, o se borrará.

### 2.3 Respuesta

```jsonc
{
  "publicId": "…", "code": "…", "name": "…", "description": "…",
  "requiresAddress": true, "requiresGeo": true, "allowsBiometric": true,
  "isActive": true,
  "concurrencyToken": "…",         // ← el que va en If-Match, del CUERPO
  "createdAtUtc": "…", "modifiedAtUtc": null,
  "allowedActions": { … }          // con IncludeAllowedActions=true
}
```

### 2.4 Parámetros del listado

| Parámetro | Valor | Nota |
|---|---|---|
| `Page`, `PageSize` | `1`, `10` | máximo **100** |
| `q` | texto | **mínimo 2 caracteres**; con 1 → `400` |
| `IncludeAllowedActions` | `true` | opt-in, el listado no los trae por defecto |
| `status` *(en la URL de pantalla)* | `active` / `inactive` | el filtro de estado |

### 2.5 Errores

| Código | HTTP | Cuándo | Clave |
|---|---|---|---|
| `WORK_CENTER_TYPE_CODE_CONFLICT` | `409` | Código repetido | — |
| `WORK_CENTER_TYPE_NOT_FOUND` | `404` | No existe o es de otra empresa | — |
| `WORK_CENTER_TYPE_IN_USE` | `409` | Al inactivar con centros que lo usan | — |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado | — |
| `common.validation` | `400` | Formato de código inválido | `code` |
| `common.validation` | `400` | Falta `If-Match` | **`If-Match`** |
| `common.validation` | `400` | Búsqueda de 1 carácter | **`q`** ✅ |
| `LOCATIONS_FORBIDDEN` | `403` | Sin permiso | — |

> ✅ **La clave del error de búsqueda es `q`**, el nombre con el que el cliente envía el parámetro —
> verificado por el cable el 2026-08-21.
>
> Cuando se probó esta pantalla devolvía `search`, que es el nombre interno del objeto de consulta del
> servidor: no corresponde a ningún control del formulario, así que no había forma de colocar el mensaje
> junto a la caja de búsqueda. [00005 / B-02](../ComentariosPruebasBackend/00005-WorkCenters.md) lo
> normalizó en 40 de los 45 endpoints; los **cinco** restantes —tipos de centro de trabajo · centros de
> trabajo · tipos de centro de costo · grupos de ubicación · unidades organizativas— quedaron cerrados
> el 2026-08-21.
>
> | Forma | Clave que devuelve | Cuántos endpoints |
> |---|---|---|
> | Todo el producto | **`q`** ✅ | **45** |
>
> **Si mapeaste `search` y `q` al mismo control mientras faltaban cinco, puedes dejarlo tal cual**:
> `search` ya no aparece. Si aún no lo hiciste, basta con `q`.
>
> 🟢 **En cambio, la clave del `If-Match` sí nombra la cabecera.** Es la forma correcta, y contrasta con el [`400` de clave vacía del Paso 1](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#3-b-02--el-400-de-validación-agrupa-todos-los-mensajes-bajo-la-clave-vacía).

### 2.6 Borrado condicional — capacidad nueva (2026-08-21)

Existe desde después de esta corrida. **La pantalla no lo ofrece hoy**, y añadirlo es opcional.

```
GET     /v1/work-center-types/{publicId}/usage     → 200
DELETE  /v1/work-center-types/{publicId}           If-Match obligatorio → 200
```

```jsonc
// respuesta de /usage
{
  "publicId": "…",
  "code": "ESTACION_AEROPUERTO",
  "name": "Estación aeroportuaria",
  "workCenterActiveReferences": 3,      // centros de trabajo activos que lo usan
  "workCenterInactiveReferences": 1,    //   …e inactivos
  "hasActiveReferences": true
}
```

El `DELETE` devuelve **el elemento eliminado** con `200` (no `204`).

| Situación | Respuesta |
|---|---|
| Ningún centro lo referencia | `200` con el elemento borrado |
| Algún centro lo referencia, activo **o** inactivo | `409 WORK_CENTER_TYPE_IN_USE_FOR_DELETE` |
| `If-Match` ausente / desactualizado | `400` / `409 CONCURRENCY_CONFLICT` |
| Sin permiso | `403 LOCATIONS_FORBIDDEN` |

⚠️ **Inactivar y borrar no miran lo mismo, y tienen códigos distintos:**

| Acción | Código si falla | Qué mira |
|---|---|---|
| `PATCH /inactivate` | `WORK_CENTER_TYPE_IN_USE` | centros **activos** |
| `DELETE` | `WORK_CENTER_TYPE_IN_USE_FOR_DELETE` | centros activos **e inactivos** |

Un tipo puede ser inactivable y no borrable a la vez. **Son dos códigos distintos a propósito**: decir «en
uso» en las dos situaciones ocultaría que no son la misma.

**Para qué sirve aquí.** Esta pantalla carga 4 tipos en la configuración inicial. Un tipo tecleado mal
—que nadie llegó a usar— hoy solo se puede inactivar, y queda en el catálogo para siempre ensuciando el
combo de la pantalla siguiente.


---

## 3. Cobertura de campos — 6 de 6

| # | Campo API | ¿En el formulario? | Estado |
|---|---|---|---|
| 1 | `code` | ✅ `wct-code` | 🟢 `aria-required="true"` |
| 2 | `name` | ✅ `wct-name` | 🟢 `aria-required="true"` |
| 3 | `description` | ✅ `wct-description` | 🟢 Presente |
| 4 | `requiresAddress` | ✅ `wct-requires-address` | 🟢 Casilla |
| 5 | `requiresGeo` | ✅ `wct-requires-geo` | 🟢 Casilla |
| 6 | `allowsBiometric` | ✅ `wct-allows-biometric` | 🟢 Casilla |

**Cobertura completa.** Es la primera pantalla de la corrida que no deja ningún campo del contrato fuera — el Paso 3 exponía 6 de 9.

**El panel de edición precarga los seis**, booleanos incluidos, y **permite cambiar el código**, que es coherente con que el `PUT` lo acepte.

### Columnas de la tabla

| Campo | ¿Se muestra? |
|---|---|
| `code`, `name` | 🟢 Sí |
| `requiresAddress`, `requiresGeo`, `allowsBiometric` | 🟢 Sí — como *Yes* / *No* |
| `isActive` | 🟡 Sí, pero **fuera de la vista** — ver **F-01** |
| *(acciones)* | 🟡 Sí, pero **fuera de la vista** — ver **F-01** |
| `description` | ❌ No — **y es correcto**: 500 caracteres no caben en una tabla y el detalle está a un clic |

---

## 4. Ciclo completo — resultado

Ejecutado con los cinco tipos de la guía AVIANCA (guía §4.2).

| Operación | Vía | Resultado |
|---|---|---|
| **Crear** | Interfaz — panel *Create record* | 🟢 `201` · **la lista se refresca** |
| **Crear (lote)** | API — los otros cuatro | 🟢 `201` ×4 |
| **Leer** | Interfaz — tabla y buscador | 🟢 Correcto · `q=` bien enviado |
| **Editar** | Interfaz — panel *Edit record* | 🟢 Guardado y reflejado en la tabla |
| **Inactivar** | Interfaz — *Deactivate* | 🟢 Inmediato, sin diálogo (ver **F-04**) |
| **Reactivar** | Interfaz — *Activate* | 🟢 Ida y vuelta completa |
| **Filtrar por estado** | Interfaz — `All / Active / Inactive` | 🟢 `?status=inactive` → 1 de 5 |

### 4.1 Batería de errores — 7 de 7 correctos

| # | Sonda | Respuesta | ¿Correcta? |
|---|---|---|---|
| 1 | `POST` con código existente | `409 WORK_CENTER_TYPE_CODE_CONFLICT` | 🟢 |
| 2 | Código con espacio (`MAL CODIGO`) | `400` · clave `code` | 🟢 |
| 3 | Código que empieza con `_` | `400` · clave `code` | 🟢 |
| 4 | `PUT` sin `If-Match` | `400` · clave **`If-Match`** | 🟢 |
| 5 | `PUT` con `If-Match` vencido | `409 CONCURRENCY_CONFLICT` | 🟢 |
| 6 | `GET` de un id inexistente | `404 WORK_CENTER_TYPE_NOT_FOUND` | 🟢 |
| 7 | Búsqueda de 1 carácter | `400` · clave `search` | 🟡 correcto salvo la clave — **hoy devuelve `q`** (§2.5) |

**Los siete se comportan según el contrato.** El único reparo era la clave del caso 7, el hallazgo
transversal que se cerró el 2026-08-21. La columna conserva lo que la corrida observó; §2.5 dice lo que
el servidor devuelve hoy.

### 4.2 El buscador — probado en ciclo cerrado

| Paso | Filas | Pie |
|---|---|---|
| A · recién cargada | **5** | `1–5 of 5` |
| B · buscando `HANGAR` | **1** | `1–1 of 1` |
| C · búsqueda limpiada | **5** | `1–5 of 5` |

🟢 **Ciclo cerrado correcto**, sin residuos de filtro.

> **Nota de método.** Una observación anterior parecía mostrar que limpiar la búsqueda dejaba la lista en 1 de 5. **Era un artefacto de la medición**: cuatro de los cinco registros se habían creado por API y la pantalla, que no puede enterarse de escrituras que no pasan por ella, seguía mostrando lo que conocía. La prueba en ciclo cerrado —recargar, filtrar, limpiar— la descartó. **Segundo falso positivo del instrumento en esta sesión**; el otro fue un `415` provocado por un ayudante de `fetch` que pisaba las cabeceras.

### 4.3 Datos cargados

| Código | Nombre | Dirección | Geo | Biométrico |
|---|---|---|---|---|
| `ESTACION_AEROPUERTO` | Estación aeroportuaria | Sí | Sí | Sí |
| `HANGAR` | Hangar de mantenimiento | Sí | Sí | Sí |
| `OFICINA` | Oficina corporativa | Sí | Sí | Sí |
| `TERMINAL_CARGA` | Terminal de carga | Sí | Sí | Sí |
| `CENTRO_ENTRENAMIENTO` | Centro de entrenamiento | Sí | No | No |

**Los cinco activos.** Los valores de los tres booleanos no los fija la guía; se eligieron por criterio operativo y quedan documentados aquí para que el Paso 5 los pueda contrastar.

---

## 5. Hallazgos de frontend

### 🟡 F-01 — La tabla esconde el estado y las acciones detrás de un desplazamiento horizontal

**Severidad:** Media · **Tipo:** Presentación

#### Evidencia

Al ancho por defecto (1 024 px de contenido), la tabla muestra:

```
Code · Name · Requires address · Requires geolocation · Allows biometric │ ▓▓▓▓ →
                                                                          Status · Actions
```

Las columnas **Status** y **Actions** quedan fuera. Solo aparecen desplazando la tabla a la derecha, y **vuelve sola a la izquierda** al recargar o al cambiar el filtro.

Se midió el efecto real: tras inactivar `CENTRO_ENTRENAMIENTO`, **la captura de pantalla no mostraba ninguna diferencia** entre esa fila y las cuatro activas. El cambio de estado había ocurrido —se confirmó por API— pero era invisible.

#### ¿Debería mostrarse el estado? Sí, y es el campo más importante de la fila

Un catálogo con registros inactivos que se ven igual que los activos **es un catálogo en el que no se puede confiar a simple vista**. Y las acciones que no se ven no se usan: un usuario que no descubra el desplazamiento concluirá que la pantalla no permite editar ni dar de baja — que es exactamente lo que se registró como carencia en el Paso 3, aquí producido por presentación en lugar de por ausencia.

#### Contrato para el frontend

**No interviene ningún endpoint.** El dato ya viaja: `isActive` viene en cada elemento del listado y `allowedActions` indica qué acciones caben. Es un problema de reparto de ancho.

#### Ajuste pedido al frontend

Que **Status y Actions no participen del desplazamiento** —fijas a la derecha, o la fila entera reflejando el estado (atenuada, con distintivo junto al código)—. Las tres columnas de sí/no son las que deben ceder espacio: son las menos consultadas y las más estrechas.

**No requiere cambio en el backend.**

---

### 🟡 F-02 — Sin `maxlength` ni `pattern` en cliente, con el servidor limitando los tres campos

**Severidad:** Media · **Tipo:** Validación en cliente

#### Evidencia

Ningún campo del formulario declara restricción de longitud ni de formato:

| Campo | Límite del servidor | `maxlength` | `pattern` |
|---|---|---|---|
| `wct-code` | 50 + regex | ❌ | ❌ |
| `wct-name` | 150 | ❌ | ❌ |
| `wct-description` | 500 | ❌ | — |

**Cuarto módulo con la misma carencia** (Pasos 2, 3 y ahora 4). El servidor sí valida: se comprobó con `MAL CODIGO` y con `_INICIA`, y los dos dan `400` con la clave `code`.

#### ¿Debería estar? Sí — el servidor ya declara los límites

No se trata de duplicar la validación sino de **no dejar que el usuario escriba 300 caracteres para enterarse al enviar**. El regex del código es además poco intuitivo —prohíbe empezar por `_` o `-`, prohíbe espacios— y sin ayuda en el campo el usuario no tiene forma de deducirlo.

Es especialmente visible aquí porque **los códigos del escenario llevan guion bajo** (`ESTACION_AEROPUERTO`, `TERMINAL_CARGA`), que sí está permitido *en medio* pero no *al inicio*. Es justo el tipo de matiz que un `pattern` comunica y un `400` no.

#### Contrato para el frontend

```
code         máx. 50   ·  ^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$
name         máx. 150
description  máx. 500
```

Al violarlos: `400` con la clave del campo en `errors`.

#### Ajuste pedido al frontend

`maxlength` en los tres, `pattern` en el código y un texto de ayuda que diga la regla en palabras: *«Letras, números, guion y guion bajo. Debe empezar con letra o número.»*

**No requiere cambio en el backend.** Es el mismo ajuste que el Paso 2 y el Paso 3, así que conviene hacerlo de una vez en el componente compartido, no pantalla por pantalla.

---

### ⚪ F-03 — La pantalla no exporta, y **es correcto**

**Severidad:** Informativo · **Tipo:** Cobertura de funciones — **no es un defecto**

#### Evidencia

No hay botones de exportación, mientras la pantalla de unidades organizativas del Paso 3 ofrece *Export CSV* y *Export Excel*.

#### ¿Debería estar? No — el patrón es coherente

Verificado en los cuatro controladores:

| Recurso | ¿Exporta? |
|---|---|
| Unidades organizativas | ✅ |
| Centros de costo | ✅ |
| **Tipos de unidad** | ❌ |
| **Tipos de centro de trabajo** | ❌ |

**La línea divide recursos principales de catálogos de clasificación**, y está trazada de forma consistente en los cuatro casos. Exportar un catálogo de cinco filas que solo sirve para clasificar otra cosa no tiene destinatario.

#### Contrato para el frontend

**No interviene ningún endpoint** — no existe, y no debe crearse.

#### Ajuste pedido al frontend

**Ninguno.** Se documenta para que la asimetría no se levante como hallazgo en una corrida futura.

---

### ⚪ F-04 — Inactivar no pide confirmación, y **es correcto**

**Severidad:** Informativo · **Tipo:** Acciones destructivas — **no es un defecto**

#### Evidencia

*Deactivate* actúa de inmediato. No hay diálogo modal ni nativo — verificado interceptando `confirm`, `alert` y `prompt`: **ninguno se disparó**.

#### ¿Debería pedirla? No

Una confirmación se justifica cuando la acción es difícil de deshacer o su alcance no es evidente. Aquí no se cumple ninguna de las dos:

- **Es reversible en el mismo sitio**: el botón se convierte en *Activate* en la misma fila.
- **No destruye nada**: es baja lógica; no hay borrado en este recurso.
- **El alcance es una fila**, visible y señalada.

Pedir confirmación sería fricción sin beneficio, y educaría al usuario a descartar diálogos sin leerlos — que es lo que hace peligrosos a los que sí importan.

> **Distinto sería si la inactivación arrastrara dependencias.** El servidor tiene `WORK_CENTER_TYPE_IN_USE` para ese caso y **lo rechaza en vez de arrastrar**, que es la decisión correcta: no hay efecto colateral que anunciar. Ese guard queda por medir — ver §7.

#### Contrato para el frontend

```
PATCH /v1/work-center-types/{publicId}/inactivate     If-Match obligatorio
PATCH /v1/work-center-types/{publicId}/activate       If-Match obligatorio
```

Ambos devuelven el recurso con el token renovado. `409 WORK_CENTER_TYPE_IN_USE` si hay centros de trabajo usando el tipo.

#### Ajuste pedido al frontend

**Ninguno.** Se documenta como decisión, no como omisión.

---

## 6. Hallazgos de backend

**Ninguno nuevo.** Es el primer paso de la corrida que no genera hallazgos propios de servidor, así que **no se creó documento espejo**.

Lo que sí hace es **confirmar y ampliar tres transversales ya abiertos**:

| Hallazgo | Qué aporta este paso |
|---|---|
| [00003 / B-04](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca) — sin `DELETE` ni `/usage` | **Este paso subió el alcance de 2 recursos a 4**; acabó cubriendo **5**. 🟢 **Resuelto**: el 2026-08-16 los dos primeros, el 2026-08-21 los tres restantes —incluido este tipo de centro— (§2.6) |
| [00003 / B-03](../ComentariosPruebasBackend/00003-OrgUnits.md#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar) — canal de localización | **Este paso lo re-diagnosticó por completo** y acertó: las traducciones existían y el canal estaba cableado; fallaba que la preferencia nunca llegaba. 🟢 **Resuelto el 2026-08-16.** Verificado por el cable en esta pantalla el 2026-08-21: los mensajes llegan en español |
| [00002 / B-02](../ComentariosPruebasBackend/00002-UnitTypes.md#3-b-02--la-clave-del-error-de-búsqueda-es-search-pero-el-parámetro-público-es-q) — clave `search` vs parámetro `q` | **Tercer módulo** con el desajuste idéntico. 🟢 **Resuelto en los 45 endpoints**: 40 en [00005 / B-02](../ComentariosPruebasBackend/00005-WorkCenters.md) y los 5 restantes —esta pantalla entre ellos— el 2026-08-21 — §2.5 |

---

## 7. Qué NO se probó, y cómo se va a medir

| Pendiente | Montaje | Cuándo |
|---|---|---|
| **`409 WORK_CENTER_TYPE_IN_USE`** | Necesita un centro de trabajo que use un tipo. **Se crea en el Paso 5 por definición**: basta intentar inactivar `OFICINA` con `SS-CORP` ya creado | **Paso 5**, sin montaje adicional |
| **`403 LOCATIONS_FORBIDDEN`** | Segundo usuario con rol restringido, en la empresa desechable. **Ya no está bloqueado** — la empresa existe desde hoy | Próxima sesión |
| **`PATCH` de JSON Patch** | La interfaz no lo usa; el `PUT` cubre la edición. Se probará por API con `application/json-patch+json` sobre `/name` | Al cerrar la corrida |
| **Paginación con más de 100** | `PageSize` máximo es 100 y el catálogo tiene 5. **Se medirá donde haya volumen real** (unidades organizativas ya tiene 26; plazas y expedientes tendrán más) | Pasos 7–8 |

**Nada queda sin plan, y ninguno depende de un insumo externo.** Los dos primeros se cierran en la propia secuencia.

---

## 8. Estado del asistente al cerrar el paso

```
Setup progress: 4 / 8 complete
```

| Paso | Estado |
|---|---|
| 1 · Legal profile | ✅ Completed |
| 2 · Unit types | ✅ Completed |
| 3 · Org units | ✅ Completed |
| **4 · Work center types** | ✅ **Completed** |
| 5 · Work centers | 🟢 **Available** — siguiente |
| 6 · Job profiles | 🟢 Available |
| 7 · Position slots | 🔒 Bloqueado — requiere perfiles de puesto |
| 8 · Personnel files | 🔒 Bloqueado — requiere plazas |

**La empresa quedó funcional**: los cinco tipos activos son el insumo exacto que el Paso 5 necesita para registrar las cinco sedes de la guía (guía §4.3).

---

## 9. Para reintegrar y publicar — revisión de esta pantalla

> **Cómo leer esta lista.** No sabemos qué tiene hoy el cliente, así que **no está escrita como «cambios»
> sino como comprobaciones**. Si ya coincide, no hay nada que tocar.
>
> **Esta es la pantalla que menos cambia de toda la corrida.** Nada del contrato se rompe; hay una
> capacidad nueva y dos mejoras que se notan solas.

### 9.1 Lo que puede romper algo — nada

Rutas, verbos, cuerpos, códigos de error y claves son **idénticos** a los probados el 2026-08-15.

Con una sola salvedad, y solo si el cliente hace algo poco habitual: **si compara el texto de un mensaje
de error** para decidir, dejará de coincidir — los mensajes cambiaron de idioma y de redacción (§9.2).
Lo correcto es decidir por `code`.

### 9.2 Lo que mejoró solo, sin tocar el cliente

Verificado por el cable en **esta misma pantalla** el 2026-08-21:

| # | Antes | Ahora |
|---|---|---|
| 1 | `'Name' must not be empty.` | `'Nombre' no debería estar vacío.` — **español, con etiqueta traducida** |
| 2 | `Code format is invalid.` | *«El código debe empezar con letra o número y solo admite letras, números, guion y guion bajo, hasta 50 caracteres.»* — **dice la regla** |
| 3 | El buscador distinguía acentos | `q=estacion` encuentra «Estación» |

⚠️ **Y por eso mismo:** si el formulario hoy sustituye el mensaje del servidor por uno propio —porque
venía en inglés o porque no explicaba el formato—, ahora habrá **dos textos en español diciendo lo mismo
con distintas palabras**. Conviene revisarlo. El punto 2 afecta directamente a **F-02**.

### 9.3 La clave del buscador — ya es `q` en todas partes

Esta pantalla devolvía `search` como clave del error de búsqueda; desde el 2026-08-21 devuelve `q`, igual
que los otros 44 endpoints del producto (§2.5).

**Basta con leer `q`.** Si el mapeo defensivo de las dos claves ya está escrito, no estorba — `search` no
volverá a aparecer.

### 9.4 Capacidad nueva, opcional — el borrado condicional

`GET /usage` + `DELETE` (§2.6). **La pantalla no lo ofrece hoy y no está obligada.**

Aquí tiene un valor concreto: la configuración inicial carga 4 tipos, y un tipo tecleado mal —que nadie
llegó a usar— hoy solo se puede inactivar. Se queda en el catálogo ensuciando el combo del Paso 5.

Si se implementa, llamar a `/usage` al abrir el menú de acciones y mostrar *Eliminar* solo con las dos
cuentas en cero, para que el botón destructivo no aparezca cuando va a fallar.

### 9.5 Lo que NO cambió y no hay que tocar

- Los siete endpoints originales, sus verbos y su autorización (`LOCATIONS_FORBIDDEN`).
- **Los tres booleanos siguen sin ser nullable**: omitirlos en el `PUT` los deja en `false` sin avisar.
  Sigue siendo obligatorio enviarlos siempre, junto con `description`.
- El mínimo de 2 caracteres en la búsqueda.
- `If-Match` obligatorio en `PUT`, `PATCH`, `activate` e `inactivate`, tomado **del cuerpo**.
- **F-03 y F-04 no son defectos**: la ausencia de exportación y la de confirmación al inactivar se
  analizaron y se consideraron correctas. No hay nada que hacer con ellos.

### 9.6 Orden sugerido para volver a probar de cero

1. **F-02** (`maxlength` y `pattern` en cliente): el servidor ya declara los límites **y ahora los
   explica en el mensaje**, así que basta con que el cliente coincida con ese texto en vez de inventar
   otro.
2. **F-01** (la tabla esconde estado y acciones tras un desplazamiento horizontal): es de presentación y
   no depende de nadie.
3. §9.3 (mapear las dos claves) si el cliente mapea errores a controles.
4. §2.6 (borrado) es opcional y puede ir en una versión posterior.

> ⚠️ **Este documento no se ha vuelto a probar contra el ambiente.** Lo revalidado el 2026-08-21 es **el
> contrato del servidor**, con dos volcados reales tomados contra este mismo recurso. Los hallazgos
> F-01…F-04 siguen tal como se observaron: no se han añadido ni retirado, porque no se ha repetido la
> corrida.
