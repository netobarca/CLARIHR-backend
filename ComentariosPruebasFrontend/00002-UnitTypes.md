# 00002 — UnitTypes · Tipos de unidad

| | |
|---|---|
| **ID** | 00002-UnitTypes |
| **Paso probado** | Configuración guiada (`/setup`) → **Paso 2: Tipos de unidad** |
| **Pantalla** | `/org-units?tab=unit-types` |
| **Fecha** | 2026-08-14 |
| **Ambiente** | Producción — `https://dashboard.clarihr.com` |
| **Empresa de prueba** | `End to End SAS` |
| **Usuario** | christopher canas (OWNER) |
| **Alcance** | **Ciclo CRUD completo** con datos reales del escenario AVIANCA — la empresa queda operativa |
| **Objetivo** | Verificar que la pantalla expone todo lo que la BD y el endpoint requieren, y que el ciclo completo funciona de punta a punta |

---

## 1. Resumen ejecutivo

🟢 **Cobertura de campos: 4/4.** El formulario expone el contrato completo de escritura.

🟢 **Ciclo completo funcional: crear → leer → editar → inactivar → reactivar.** Los cinco caminos se ejercitaron por la interfaz y todos responden bien. **No existe borrado físico**: el contrato solo tiene baja lógica (§2.1). Esa ausencia se dio por buena en esta corrida y **al revisarla resultó ser un hueco real** — ver [00003 / B-04](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca).

🟢 **Datos cargados: el escenario AVIANCA quedó operativo** — 9 tipos de unidad y 11 áreas funcionales. El asistente pasó de `1/8` a `2/8` y desbloqueó el Paso 3 (§9).

Quedan **8 hallazgos de frontend** y **2 de backend**. El más grave está **medido con datos reales**: el **buscador no filtra nada** — con 9 registros cargados, buscar `GERENCIA` devuelve los 9 (**F-01**).

**Frontend** (§6)

| Severidad | Cantidad | IDs |
|---|---|---|
| 🔴 Alta | 2 | F-01, F-02 |
| 🟡 Media | 2 | F-03, F-04 |
| 🔵 Baja | 3 | F-05, F-06, F-07 |
| ⚪ Informativo | 1 | F-08 |

**Backend** (§7)

| Severidad | ID | Resumen |
|---|---|---|
| 🟡 Media | B-01 | Los mensajes de error de este módulo no están localizados (validación **y** títulos de conflicto) |
| 🔵 Baja | B-02 | La clave del error de búsqueda es `search`, pero el parámetro público es `q` |

Además, esta corrida **confirmó el alcance de [00001 / B-03](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location)**: el proxy no solo pisa el `ETag` — **también descarta la cabecera `Location` del `201`** (§5.2).

---

## 2. Contrato de referencia (backend)

### 2.1 Endpoints del recurso

```
GET    /api/v1/companies/{companyPublicId}/organization-structure-catalogs/unit-types
GET    /api/v1/organization-structure-catalogs/unit-types/{publicId}
POST   /api/v1/companies/{companyPublicId}/organization-structure-catalogs/unit-types
PUT    /api/v1/organization-structure-catalogs/unit-types/{publicId}
PATCH  /api/v1/organization-structure-catalogs/unit-types/{publicId}/activate
PATCH  /api/v1/organization-structure-catalogs/unit-types/{publicId}/inactivate
```

> El frontend los consume vía proxy del mismo origen como `/v1/unit-types`. **Confirmado en vivo.**
>
✅ **Hay dos endpoints más desde el 2026-08-16 que esta corrida no conoció**, y son una capacidad nueva
para la pantalla, no un cambio de lo existente:

```
GET     /api/v1/organization-structure-catalogs/unit-types/{publicId}/usage
DELETE  /api/v1/organization-structure-catalogs/unit-types/{publicId}      ← If-Match obligatorio
```

> **Cuando se probó esta pantalla NO existía el borrado.** El documento decía «no hay `DELETE`» y era
> cierto entonces. Se levantó como [**00003 / B-04**](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca)
> porque el caso sin resolver era **el registro tecleado mal que nadie llegó a referenciar** y que se
> quedaba para siempre en el catálogo, solo inactivable. Ya está resuelto.
>
> **La baja lógica sigue siendo la vía normal.** El borrado es **duro y condicional**: solo pasa si nada
> referencia al tipo, ni activo ni inactivo. Para un tipo en uso, la respuesta correcta sigue siendo
> inactivarlo. Ver §2.8.

### 2.2 Parámetros del listado

```
GET /v1/unit-types
    ?q={texto libre}            ← ojo: se llama q, NO Search (ver F-01)
    &isActive={bool}
    &page=1
    &pageSize=20                ← default 20, máx. 100
    &includeAllowedActions={bool}
```

Respuesta paginada: `{ items, page, pageSize, totalCount, totalPages }`. **Verificado en vivo.**

**⚠️ Rate limit:** política `OrgStructureCatalogs:Search`, particionada por **usuario + tenant**, límite por defecto **120** peticiones (`RateLimiting:OrgStructureCatalogs:Search:PermitLimit`). El buscador con *type-ahead* necesita *debounce*.

### 2.3 Autorización

| Operación | Permisos aceptados (cualquiera) |
|---|---|
| `GET` | `OrgStructureCatalogs.Read` · `OrgStructureCatalogs.Admin` · `OrgUnits.Read` · `OrgUnits.Admin` · `iam.administration.manage` |
| `POST` / `PUT` / `PATCH` | `OrgStructureCatalogs.Admin` · `OrgUnits.Admin` · `iam.administration.manage` |

`ResourceKey`: **`ORG_STRUCTURE_CATALOGS`**.

> **Ojo con la nomenclatura:** `OrgStructureCatalogPolicies.Read/Manage` son **nombres de política**, no códigos de permiso. Los códigos RBAC reales son los de la tabla. La política incluye a propósito el respaldo `OrgUnits.*` — quien administra unidades organizativas administra sus catálogos.

### 2.4 Cuerpo de `POST` y `PUT`

```jsonc
{
  "code": "DEPARTAMENTO",           // requerido, máx. 50, formato ^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$
  "name": "Departamento",           // requerido, máx. 150
  "description": "Texto opcional",  // opcional, máx. 500, nullable
  "sortOrder": 60                   // entero >= 0
}
```

### 2.5 Respuesta

```jsonc
{
  "publicId": "…",
  "code": "…",
  "name": "…",
  "description": null,        // solo en el detalle — el listado lo OMITE
  "sortOrder": 0,
  "isActive": true,
  "concurrencyToken": "…",
  "createdAtUtc": "…",
  "modifiedAtUtc": null,      // nullable — se puebla en la primera edición
  "allowedActions": { … }     // solo si se pide includeAllowedActions=true
}
```

> **El listado no trae `description`** — es carga solo de detalle. **Verificado en vivo:** la clave no aparece en los ítems del listado, y el panel de edición sí la carga correctamente al abrir.

### 2.6 Reglas de negocio que gobiernan el recurso

1. **Formato del código:** `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$` — empieza con letra o dígito; luego letras, dígitos, `_` y `-`. **Se valida sobre el valor recortado (`Trim`)**, pero `MaximumLength(50)` se mide sobre el valor crudo.
2. **Código único por empresa**, y la unicidad es **insensible a mayúsculas**: con `GERENCIA` ya creado, tanto `GERENCIA` como `direccion_general` en minúsculas dan `409`. **Verificado en vivo.** Índice `uq_org_unit_type_catalog_items__tenant_code` sobre `tenant_id` + código normalizado.
3. **Búsqueda con mínimo de 2 caracteres** después de `Trim`. Vacío o solo espacios = «sin filtro» (válido). Con 1 carácter → `400`. Es a propósito: evita el escaneo `LIKE '%x%'` no sargable.
4. **La inactivación está protegida:** falla con `409` si el tipo sigue en uso por unidades organizativas o por clasificaciones de categoría de plaza. *(No verificable en esta corrida — ver §8.)*
5. **Concurrencia obligatoria** en `PUT`, `PATCH /activate` y `PATCH /inactivate`. **Verificado en vivo** (§5.3).
6. **`POST` devuelve `201`** con cabecera `Location` y `ETag`. ⚠️ **Ninguna de las dos llega al navegador** — ver §5.2.
7. **El catálogo arranca vacío.** El aprovisionamiento **no** siembra tipos de unidad — ver **F-08**.

### 2.7 Catálogo de errores

| Código | HTTP | Cuándo | Verificado |
|---|---|---|---|
| `ORG_STRUCTURE_CATALOG_NOT_FOUND` | `404` | El ítem no existe, o es de otro tenant | — |
| `ORG_STRUCTURE_CATALOG_CODE_CONFLICT` | `409` | Otro ítem ya usa ese código | ✅ en vivo |
| `ORG_STRUCTURE_CATALOG_IN_USE` | `409` | Se intenta inactivar un tipo en uso | — |
| `ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE` | `409` | Se intenta **borrar** un tipo que algo referencia. **Código distinto del anterior a propósito**: inactivar solo mira referencias activas, borrar mira también las inactivas | — |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado | ✅ en vivo |
| `ORG_STRUCTURE_CATALOG_FORBIDDEN` | `403` | Sin permiso | — |
| `common.validation` | `400` | Falla de validación | ✅ en vivo |

**Forma del `400` — volcado literal del servidor, verificado el 2026-08-21** con `Accept-Language: es`,
enviando `code = "AB CD"` (espacio no permitido), `name = ""` y `sortOrder = -1`:

```jsonc
{
  "type": "https://httpstatuses.com/400",
  "title": "Se encontraron uno o más errores de validación.",
  "status": 400,
  "detail": "Se encontraron uno o más errores de validación.",
  "errors": {
    "code":      ["El código debe empezar con letra o número y solo admite letras, números, guion y guion bajo, hasta 50 caracteres."],
    "name":      ["'Nombre' no debería estar vacío."],
    "sortOrder": ["'Orden' debe ser mayor o igual que '0'."]
  },
  "code": "common.validation",
  "traceId": "0HNNVTK6TPK03"
}
```

> ⚠️ **Esto corrige dos cosas que decía este documento.**
>
> **(1) Los mensajes ya NO llegan en inglés.** La versión anterior mostraba `'Code' must not be empty.` y
> advertía *«llegan en inglés aunque se pida `Accept-Language: es`»*. Eso era `00002 / B-01`, **resuelto
> el 2026-08-18**: la causa no era que faltaran traducciones sino que el token emitía siempre un claim de
> idioma que tapaba la cabecera. Ahora llegan en español, con tildes.
>
> **(2) El mensaje de formato ya dice cuál es el formato.** Antes era `Code format is invalid.` —que no
> le sirve al usuario para corregir— y ahora enumera los caracteres permitidos y el largo. Era
> `00002 / B-03`, resuelto el 2026-08-20.
>
> **(3) Las etiquetas de campo salen en español** (`'Nombre'`, `'Orden'`). Era `00002 / B-04`. ⚠️ **No
> todas están traducidas todavía**: el glosario se está poblando de forma incremental y un campo sin
> etiqueta sigue mostrando su nombre en inglés partido (`'Sort Order'`). **Mostrar el mensaje tal cual**;
> no intentar traducirlo en cliente, o dejará de coincidir cuando se añada la etiqueta.

> 🟢 **Y lo que ya era cierto sigue siéndolo:** aquí los errores **vienen con la clave del campo** en
> camelCase, porque estos validadores usan expresiones directas (`RuleFor(c => c.Code)`). El frontend
> puede mapear cada mensaje a su input; hoy no lo hace — ver **F-03**.

**Falta de `If-Match` — verificada en vivo:**

```jsonc
{"errors":{"If-Match":["The 'If-Match' header is required and must contain the current resource concurrency token."]}}
```

### 2.8 Borrado condicional — capacidad nueva (2026-08-16)

Existe desde después de esta corrida. **La pantalla no la ofrece hoy**, y añadirla es opcional: la baja
lógica sigue siendo la vía normal.

**Primero se pregunta qué lo referencia:**

```
GET /v1/unit-types/{publicId}/usage        → 200
```

```jsonc
{
  "publicId": "…",
  "code": "DEPARTAMENTO",
  "name": "Departamento",
  "orgUnitActiveReferences": 3,                              // unidades organizativas activas
  "orgUnitInactiveReferences": 1,                            //   …e inactivas
  "positionCategoryClassificationActiveReferences": 0,       // clasificaciones de categoría activas
  "positionCategoryClassificationInactiveReferences": 0,     //   …e inactivas
  "hasActiveReferences": true
}
```

**Y luego se borra, si procede:**

```
DELETE /v1/unit-types/{publicId}     If-Match: "{concurrencyToken}"     → 200
```

Devuelve **el elemento eliminado** con `200` (no `204`): así el cliente tiene el estado final sin haberlo
guardado antes.

| Situación | Respuesta |
|---|---|
| Nada lo referencia | `200` con el elemento borrado |
| Algo lo referencia, activo **o** inactivo | `409 ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE` |
| `If-Match` ausente | `400` |
| `If-Match` desactualizado | `409 CONCURRENCY_CONFLICT` |
| Sin permiso `OrgStructureCatalogs.Admin` | `403` |

> **Por qué `/usage` y no solo el `409`.** El `409` dice «no se puede», pero no **qué** lo impide. `/usage`
> devuelve las cuatro cuentas separadas, así que la pantalla puede decir «lo usan 3 unidades activas y 1
> inactiva» en vez de un mensaje genérico. Ése fue el hallazgo original: **el servidor sabía el porqué y
> no lo decía**.
>
> ⚠️ **Ojo con la diferencia entre inactivar y borrar**: inactivar solo se bloquea por referencias
> **activas**; borrar se bloquea también por las **inactivas**, porque la clave foránea es `RESTRICT`. Un
> tipo puede ser inactivable y no borrable a la vez, y eso no es un error.

**Sugerencia de uso, si se decide ofrecerlo:** llamar a `/usage` al abrir el menú de acciones del ítem y
mostrar *Eliminar* solo cuando las cuatro cuentas son cero. Así el botón destructivo no aparece cuando va
a fallar.

---

## 3. Cobertura de campos — resultado

| # | Campo API | Etiqueta | `id` del input | Oblig. | Regla backend | Estado |
|---|---|---|---|---|---|---|
| 1 | `code` | Code | `ut-code` | **Sí** | `NotEmpty` + máx. 50 + regex | 🟢 Presente · ⚠️ formato y longitud NO validados en cliente (**F-02**) |
| 2 | `name` | Name | `ut-name` | **Sí** | `NotEmpty` + máx. 150 | 🟢 Presente · ⚠️ longitud NO validada (**F-02**) |
| 3 | `description` | Description | `ut-description` | No (nullable) | máx. 500 | 🟢 Presente y persiste correctamente · ⚠️ longitud NO validada (**F-02**) |
| 4 | `sortOrder` | Sort Order | `ut-sort-order` | No (default 0) | entero >= 0 | 🟢 Presente y validado |

**No hay campos faltantes. No hay campos de más.**

### Extras correctos que vale la pena reconocer

- **Los obligatorios se marcan con `*`** (`Code *`, `Name *`).
- **El ciclo completo funciona**: crear, editar, inactivar y reactivar responden bien, con confirmación visible en cada caso (§5).
- **El panel de edición carga el detalle completo**, incluida la `description` que el listado omite.
- **El filtro de estado funciona de verdad** (`All` / `Active` / `Inactive`): con `UNIDAD` inactiva, el filtro *Inactive* devolvió exactamente esa fila. Es el contraste que hace tan visible el defecto del buscador (**F-01**): el filtro que sí manda el nombre correcto de parámetro funciona; el que no, se ignora.
- **La acción de estado se invierte según la fila**: *Deactivate* en activas, *Activate* en inactivas.
- **No se ofrece ninguna acción de borrado**, en línea con el contrato.
- **El banner de error lleva `role="alert"`**, así que se anuncia a lectores de pantalla.
- **El estado vacío distingue** «catálogo sin datos» de «filtro sin coincidencias».
- **El frontend ya pide `IncludeAllowedActions=true`**. Este recurso sí los expone, lo que refuerza que la propuesta de **00001 / B-01** encaja con lo que el cliente ya sabe consumir.
- **La paginación es completa**: «1–9 of 9», selector *Per page*, navegación y salto a página.

---

## 4. Validación en cliente — batería ejecutada

| # | Entrada | Resultado observado | ¿Coincide con el backend? |
|---|---|---|---|
| 1 | `code` = `"DEPTO"`, `name` = `"Departamento"` | Sin error · *Create* habilitado | ✅ |
| 2 | `code` = `""` | *Create* deshabilitado | ✅ |
| 3 | `name` = `""` | *Create* deshabilitado | ✅ |
| 4 | `sortOrder` = `-1` | *Create* deshabilitado | ✅ |
| 5 | `code` = `"A_B-1"` | Sin error · *Create* habilitado | ✅ |
| 6 | `code` = `"-ABC"` (arranca con guion) | **Sin error · *Create* HABILITADO** | ❌ **F-02** |
| 7 | `code` = `"AB C"` (con espacio) | **Sin error · *Create* HABILITADO** | ❌ **F-02** |
| 8 | `code` = `"AB@C"` | **Sin error · *Create* HABILITADO** | ❌ **F-02** |
| 9 | `code` con 51 caracteres | **Sin error · *Create* HABILITADO** | ❌ **F-02** |
| 10 | `name` con 151 caracteres | **Sin error · *Create* HABILITADO** | ❌ **F-02** |
| 11 | `description` con 501 caracteres | **Sin error · *Create* HABILITADO** | ❌ **F-02** |

### Sondas directas al contrato

| Petición | Resultado | Lectura |
|---|---|---|
| `?…&Search=a` | **`200`** | El parámetro `Search` **no existe** para la API: se ignora en silencio |
| `?…&q=a` | **`400`** `{"search":["Search must be at least 2 characters when provided."]}` | `q` sí se enlaza y valida el mínimo |
| `?…&PageSize=101` | **`400`** `{"pageSize":["…between 1 and 100."]}` | Tope de 100 confirmado |
| `?…&IsActive=true` | **`200`** | El filtro de estado sí se enlaza |

> **Prueba decisiva de F-01:** el mismo valor de un carácter da `400` como `q` y `200` como `Search`. Si el proxy renombrara el parámetro, ambos darían `400`. No lo renombra: **`Search` se descarta**.

> ℹ️ **Las respuestas de la tabla son las de la corrida.** El fondo —qué parámetro enlaza y cuál se
> descarta— no ha cambiado y F-01 sigue en pie, pero **la forma del `400` sí**: hoy la clave es `q` y el
> mensaje llega en español (§2.8). Al repetir estas sondas, esperar
> `{"q":["La búsqueda debe tener al menos 2 caracteres cuando se envía."]}`.

---

## 5. Ciclo completo — resultado

Ejecutado con los datos reales del escenario AVIANCA (`docs/technical/operations/guia-configuracion-empresa-avianca-es.md` §3.1).

### 5.1 Las cinco operaciones

| Operación | Cómo se ejecutó | Resultado |
|---|---|---|
| **Crear** | Interfaz — panel *Create record* | 🟢 `201` · toast «Unit type created» · la fila aparece con estado *Active* |
| **Leer** | Listado + panel de edición | 🟢 Listado ordenado por `sortOrder`; el detalle carga `description`, que el listado omite |
| **Editar** | Interfaz — *Edit* → *Save* | 🟢 `200` · toast «Unit type updated» · `description` persistida · `modifiedAtUtc` poblado |
| **Inactivar** | Interfaz — *Deactivate* | 🟢 `200` · la fila pasa a *Inactive* y la acción se invierte a *Activate* |
| **Reactivar** | Interfaz — *Activate* | 🟢 `200` · la fila vuelve a *Active* |
| **Borrar** | — | **No existe, y es correcto** (§2.1). La interfaz no ofrece la acción |

### 5.2 Lo que reveló la escritura: dos cabeceras que no llegan

En los nueve `POST` que devolvieron `201`:

```
Location : (ausente)
ETag     : W/"142-f/6gfZ4AQQkBwkEX8IFPPeN859M"   ← weak ETag del proxy, no el concurrencyToken
```

El backend emite las dos (`ToCreatedAtActionResult` fija `Location` y `ETag`), y el swagger las documenta. **Ninguna llega al navegador.**

> Esto **confirma y amplía [00001 / B-03](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location)**, cuyo «alcance a revisar» pedía exactamente comprobar si el proxy pisa otras cabeceras. **Ya está medido: sí, también descarta `Location`.** El hallazgo pasa de «sospecha» a transversal confirmado en dos módulos.
>
> **Impacto práctico hoy: ninguno.** El frontend toma el `concurrencyToken` del cuerpo y no necesita `Location` porque refresca el listado. Pero cualquier cliente que siga la convención HTTP estándar se rompería.

### 5.3 Concurrencia — verificada de punta a punta

| Prueba | Resultado |
|---|---|
| `PUT` con el token vigente | 🟢 `200` |
| `PUT` reusando el **mismo** token (ya rotado) | 🟢 `409 CONCURRENCY_CONFLICT` |
| `PUT` **sin** cabecera `If-Match` | 🟢 `400` con clave `If-Match` |

El token rota en cada escritura, tal como documenta el contrato.

### 5.4 Código duplicado

| Prueba | Resultado |
|---|---|
| `POST` con `GERENCIA` ya existente | 🟢 `409 ORG_STRUCTURE_CATALOG_CODE_CONFLICT` |
| `POST` con `direccion_general` (minúsculas, ya existe en mayúsculas) | 🟢 `409` — **la unicidad ignora mayúsculas** |

**Presentación en la interfaz:** banner rojo con «Another catalog item already uses the requested code.» El campo `Code` —que es el culpable— **no se marca**. Mismo patrón que **F-03**, y el texto en inglés refuerza **B-01**.

---

## 6. Hallazgos

---

### 🔴 F-01 — El buscador envía `Search=` y la API espera `q=`: no filtra nada

**Severidad:** Alta · **Tipo:** Contrato / funcional · **Impacto ahora MEDIDO con datos reales**

#### Evidencia (frontend)

Con los **9 tipos de unidad** cargados, se teclea `GERENCIA` en el buscador:

```
GET /v1/unit-types?Page=1&PageSize=10&Search=GERENCIA&IncludeAllowedActions=true  →  200
```

**La tabla muestra los 9 registros** —«1–9 of 9»—, incluidos `DIRECCION_GENERAL`, `VICEPRESIDENCIA`, `AREA`, `BASE` y `UNIDAD`, que no contienen el término.

Queda descartada la hipótesis de un filtrado en cliente: **el frontend tampoco filtra**. El buscador no hace absolutamente nada.

#### Regla de negocio que lo gobierna

El controller declara el parámetro con nombre explícito:

```csharp
[FromQuery(Name = "q")] string? search
```

Solo se enlaza **`q`**. Cualquier otro nombre se descarta sin error: ASP.NET ignora los parámetros de consulta desconocidos. Por eso `Search=GERENCIA` devuelve `200` con la lista **sin filtrar**, en vez de fallar.

**Contraste dentro de la misma pantalla:** el filtro de estado sí funciona, porque el frontend envía `IsActive`, que **sí** coincide con el parámetro del backend. Con `UNIDAD` inactiva, filtrar por *Inactive* devolvió exactamente esa fila. Mismo componente, mismo endpoint: el que manda el nombre correcto funciona, el otro no.

#### Impacto

El buscador es decorativo. Con `PageSize=10`, cualquier catálogo con más de 10 tipos deja registros inalcanzables — y el usuario no recibe ninguna señal de que su búsqueda se ignoró: ve una lista, simplemente no es la que pidió.

#### Solución integrada — endpoint y contrato válidos

Renombrar el parámetro a **`q`**:

```
GET /v1/unit-types?q={texto}&isActive={bool}&page=1&pageSize=20&includeAllowedActions=true
```

**⚠️ Dos reglas que empiezan a aplicar en cuanto se corrija:**

1. **Mínimo de 2 caracteres** — hoy el `400` no se ve porque el parámetro se ignora. Ver **F-05**.
2. **Rate limit** de ~120 peticiones por usuario+tenant: *debounce* de al menos 300 ms y cancelación de la petición anterior.

**No requiere cambio en el backend.**

---

### 🔴 F-02 — Sin validación de formato ni de longitud en cliente

**Severidad:** Alta · **Tipo:** Divergencia de validación cliente/servidor

#### Evidencia (frontend)

*Create* queda **habilitado** con todos estos valores, que el servidor rechaza: `code = "-ABC"`, `"AB C"`, `"AB@C"`, código de 51 caracteres, nombre de 151, descripción de 501. Ningún input declara `maxlength`.

#### Regla de negocio que lo gobierna

```csharp
RuleFor(command => command.Code).NotEmpty().MaximumLength(50).Must(IsValidCode);
RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
RuleFor(command => command.Description).MaximumLength(500);
RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
```

con `IsValidCode` = `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$` sobre el valor **recortado**.

#### Impacto

Se confirmó en vivo: se llenó el formulario con `code = "AB C"`, se pulsó *Create* y el servidor respondió `400`. Se agrava con **F-03** (el error no señala el campo) y con **B-01** (llega en inglés y no dice cuál es el formato válido).

#### Contrato para el frontend

Las reglas a replicar en cliente, y el endpoint donde se aplican:

```jsonc
POST /v1/unit-types              PUT /v1/unit-types/{publicId}   ← If-Match obligatorio
{ "code": "DEPARTAMENTO", "name": "Departamento", "description": null, "sortOrder": 60 }
```

| Campo | Regla del validador | Mensaje sugerido en cliente |
|---|---|---|
| `code` | `NotEmpty` · máx. **50** · `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$` sobre el valor **recortado** | «Debe empezar con letra o número y solo admite letras, números, guion y guion bajo» |
| `name` | `NotEmpty` · máx. **150** | «Máximo 150 caracteres» |
| `description` | máx. **500** | «Máximo 500 caracteres» |
| `sortOrder` | `>= 0` | «No admite valores negativos» |

> **Trampa:** el formato se valida sobre el valor **recortado**, pero `MaximumLength` se mide sobre el **crudo**. Un código pegado con espacios da error de *longitud*, no de formato.

Si aun así llega el `400`, viene con la clave del campo — ver **F-03**.

#### Ajuste pedido al frontend

1. **`maxlength`**: `code` 50, `name` 150, `description` 500.
2. **Patrón en cliente** para `code`. ✅ **El mensaje del servidor ya dice cuál es el formato** desde el
   2026-08-20 (`00002 / B-03`): *«El código debe empezar con letra o número y solo admite letras, números,
   guion y guion bajo, hasta 50 caracteres.»* La validación en cliente sigue valiendo la pena por la
   respuesta inmediata, pero **conviene que los dos textos digan lo mismo** — si el cliente inventa uno
   distinto, el usuario ve dos redacciones para la misma regla.
3. **`trim()` en `blur`** para `code`: el servidor valida el formato sobre el valor recortado pero mide `maxlength` sobre el crudo, así que un espacio pegado al copiar produciría un error de longitud engañoso. Misma trampa que **00001 / F-06**.

**No requiere cambio en el backend.**

---

### 🟡 F-03 — Los errores del servidor se aplanan en un banner global

**Severidad:** Media · **Tipo:** UX / manejo de errores

#### Evidencia (frontend)

Dos casos verificados, ambos con el mismo comportamiento:

| Caso | Banner mostrado | Campo culpable marcado |
|---|---|---|
| `code = "AB C"` → `400` | «One or more validation errors occurred. Code format is invalid.» | ❌ No |
| `code = "GERENCIA"` duplicado → `409` | «Another catalog item already uses the requested code.» | ❌ No |

```
Input Code: aria-invalid = null · aria-describedby = null · sin estilo de error
```

#### Regla de negocio que lo gobierna

En el `400`, el servidor **sí dice qué campo falló**: `"errors": { "code": [...] }`. A diferencia del perfil legal (**00001 / B-02**, clave vacía `""`), aquí la clave es el nombre del campo. La información está disponible y se desperdicia.

En el `409` el servidor no puede señalar el campo —no es un error de validación—, pero el frontend sí sabe que un conflicto de código se refiere al campo `Code`.

#### Contrato para el frontend

```
POST  /v1/unit-types
PUT   /v1/unit-types/{publicId}              If-Match: "{concurrencyToken}"
PATCH /v1/unit-types/{publicId}/inactivate   If-Match: "{concurrencyToken}"
```

Las dos formas de error que devuelven:

```jsonc
// 400 — validación   (textos verificados el 2026-08-21 con Accept-Language: es)
{ "status": 400, "code": "common.validation",
  "errors": {
    "code":      ["El código debe empezar con letra o número y solo admite letras, números, guion y guion bajo, hasta 50 caracteres."],
    "sortOrder": ["'Orden' debe ser mayor o igual que '0'."]
  } }

// 409 — conflicto de negocio (sin "errors": el campo se deduce del "code")
{ "status": 409, "code": "ORG_STRUCTURE_CATALOG_CODE_CONFLICT",
  "title": "Otro elemento de catálogo ya usa el código solicitado." }
```

| Código | HTTP | Campo al que corresponde |
|---|---|---|
| `common.validation` | `400` | El de cada clave de `errors` |
| `ORG_STRUCTURE_CATALOG_CODE_CONFLICT` | `409` | `code` |
| `ORG_STRUCTURE_CATALOG_IN_USE` | `409` | ninguno — error de formulario (al **inactivar**) |
| `ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE` | `409` | ninguno — error de formulario (al **borrar**, §2.8) |
| `CONCURRENCY_CONFLICT` | `409` | ninguno — recargar el registro |

#### Ajuste pedido al frontend

1. Mapear `errors[<campo>]` al control:

   | Clave del servidor | Input |
   |---|---|
   | `code` | `ut-code` |
   | `name` | `ut-name` |
   | `description` | `ut-description` |
   | `sortOrder` | `ut-sort-order` |
   | `q` | el buscador. ✅ **La clave ya es `q`, el nombre público del parámetro.** Antes el servidor devolvía `search` —el nombre interno— y no había forma de casarlo con ningún control. Resuelto el 2026-08-16 |
   | `If-Match` | no es un campo — mostrar como error de formulario y **recargar el registro** |

2. Marcar el input con `aria-invalid="true"` + `aria-describedby`.
3. Para `ORG_STRUCTURE_CATALOG_CODE_CONFLICT`, marcar `ut-code` por código de error.
4. Conservar el banner como resumen para lo que no corresponda a ningún campo.

**No requiere cambio en el backend.**

---

### 🟡 F-04 — Obligatoriedad marcada solo visualmente

**Severidad:** Media · **Tipo:** Accesibilidad

#### Evidencia (frontend)

```
ut-code:  required = false · aria-required = null   (etiqueta "Code *")
ut-name:  required = false · aria-required = null   (etiqueta "Name *")
```

El asterisco está —mejora frente al Paso 1—, pero es **solo visual**.

#### Contrato para el frontend

**No interviene ningún endpoint** — es presentación pura. Pero la **fuente de verdad** de qué es obligatorio sí es el contrato:

```jsonc
POST /v1/unit-types
{ "code": "…",          // NotEmpty  → obligatorio
  "name": "…",          // NotEmpty  → obligatorio
  "description": null,  // opcional
  "sortOrder": 0 }      // opcional (default 0)
```

#### Ajuste pedido al frontend

Añadir `aria-required="true"` a `ut-code` y `ut-name`. **No requiere cambio en el backend.**

---

### 🔵 F-05 — El buscador no respeta el mínimo de 2 caracteres

**Severidad:** Baja · **Tipo:** Contrato · **Depende de F-01**

#### Regla de negocio que lo gobierna

`OrgStructureCatalogValidationRules.IsValidSearchLength`: mínimo **2 caracteres tras `Trim`**; vacío cuenta como «sin filtro» y es válido. Es deliberado — impide el escaneo `LIKE '%x%'` no sargable.

#### Por qué hoy no se ve

Porque el parámetro se ignora (**F-01**). **En cuanto se corrija F-01, este `400` empezará a aparecer.** Hay que arreglar los dos juntos.

#### Contrato para el frontend

```
GET /v1/unit-types?q={texto}&isActive={bool}&page=1&pageSize=20&includeAllowedActions={bool}
```

| Valor de `q` | Resultado |
|---|---|
| ausente, vacío o solo espacios | `200` — «sin filtro», es válido |
| 1 carácter tras `Trim` | **`400`** `{"q":["La búsqueda debe tener al menos 2 caracteres cuando se envía."]}` |
| 2 o más | `200` filtrado |

> **La regla no cambió; el `400` sí se ve distinto que en la corrida.** Entonces devolvía
> `{"search":["Search must be at least 2 characters when provided."]}` — clave interna y mensaje en
> inglés. Hoy la clave es `q` (**B-02**, cerrado en los 45 endpoints el 2026-08-21) y el mensaje llega
> traducido (**00003 / B-03**). El comportamiento —qué se acepta y qué no— es el mismo.

#### Ajuste pedido al frontend

No emitir la petición mientras el término recortado tenga menos de 2 caracteres; con 0 caracteres, emitirla **sin** el parámetro `q`.

---

### 🔵 F-06 — La inactivación no pide confirmación

**Severidad:** Baja · **Tipo:** UX

#### Evidencia (frontend)

Pulsar *Deactivate* inactiva el registro **de inmediato**, sin diálogo intermedio.

#### Por qué es baja y no media

La acción es **reversible en un clic**: la misma celda pasa a mostrar *Activate*. No hay pérdida de datos.

#### Por qué documentarlo igual

Cuando el catálogo tenga uso real, inactivar un tipo puede fallar con `409 ORG_STRUCTURE_CATALOG_IN_USE`, o —si no está en uso— sacarlo de los selectores de creación de unidades. Un clic accidental en una tabla densa tiene consecuencias visibles aunque se puedan deshacer.

#### Contrato para el frontend

Las dos acciones son simétricas y **sin cuerpo**:

```
PATCH /v1/unit-types/{publicId}/inactivate     If-Match: "{concurrencyToken}"
PATCH /v1/unit-types/{publicId}/activate       If-Match: "{concurrencyToken}"
```

Devuelven `200` con el ítem completo y el **token rotado** —hay que reemplazarlo en memoria— tomándolo **del cuerpo**, no del `ETag` (00001 / B-03).

| Código de error | HTTP | Cuándo |
|---|---|---|
| `ORG_STRUCTURE_CATALOG_IN_USE` | `409` | El tipo está en uso por unidades organizativas o clasificaciones de plaza |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado |
| *(falta `If-Match`)* | `400` | Cabecera ausente |

> El `409 IN_USE` es justamente lo que hace útil un **Deshacer**: si la inactivación falla, el usuario necesita entender por qué sin haber perdido el contexto.

#### Ajuste pedido al frontend

Confirmación ligera («¿Inactivar *Unidad*?») o, como mínimo, un *toast* con acción **Deshacer**. **No requiere cambio en el backend.**

---

### 🔵 F-07 — `Sort Order` sin `min="0"` en el input

**Severidad:** Baja · **Tipo:** UX

```
ut-sort-order: type=number · min=null
```

El formulario **sí** deshabilita *Create* con `-1`, así que el valor inválido no llega al servidor. Es solo pulido: sin `min="0"` el navegador permite teclear el negativo y las flechas bajan de cero.

#### Contrato para el frontend

**No interviene ningún endpoint.** El valor del atributo sale de la regla del validador: `RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0)` → `min="0"`.

**Ajuste:** agregar `min="0"`. **No requiere cambio en el backend.**

---

### ⚪ F-08 — El catálogo arranca vacío, y la guía de configuración dice lo contrario

**Severidad:** Informativo · **Tipo:** Documentación desactualizada

#### Evidencia

En `End to End SAS` el catálogo estaba en **`totalCount: 0`**. **Hubo que crear los 9 tipos**, no solo los 6 que la guía marca como «agregar».

#### Qué dice la documentación

`guia-configuracion-empresa-avianca-es.md` §0 y §3.1 afirman que el aprovisionamiento siembra `GERENCIA`, `DEPARTAMENTO` y `UNIDAD`, y marca esas tres filas como «**ya existe** — solo cambiar orden» / «inactivar si no la usás».

Lo mismo en §3.2 con las áreas funcionales `ADMIN`, `OPS`, `SALES`, marcadas para inactivar: **tampoco existían**.

Y `guia-integracion-frontend-perfil-legal-patronal.md` §7 repite la premisa para justificar una revisión del asistente.

**Ninguna de las tres es cierta hoy.** Toda empresa nueva arranca con los catálogos vacíos.

#### Consecuencia práctica

Quien siga la guía al pie de la letra buscará filas que no están y saltará pasos de creación. **Las tres guías necesitan corrección**, y la nota del asistente sobre «dar por bueno un catálogo de plantilla» ya no aplica: sin filas sembradas, el paso solo se marca *Completado* cuando alguien crea algo de verdad — que es justo el comportamiento que la nota pedía.

#### Contrato para el frontend

**Ninguno.** No hay endpoint ni comportamiento que ajustar: es una corrección de documentación del backend.

**Nada que hacer del lado del frontend.**

---

## 7. Hallazgos de backend

> El **detalle completo vive en el documento espejo** [`ComentariosPruebasBackend/00002-UnitTypes`](../ComentariosPruebasBackend/00002-UnitTypes.md).

| ID | Sev. | Hallazgo | Origen | Alcance |
|---|---|---|---|---|
| [**B-01**](../ComentariosPruebasBackend/00002-UnitTypes.md#2-b-01--el-español-que-se-sirve-estaba-roto-199-mensajes-en-dos-idiomas-y-320-sin-tildes) | 🟡 Media | 🟢 **Resuelto 2026-08-18.** El idioma ya funcionaba; el defecto real era la **calidad** del español (199 mensajes en dos idiomas, 320 sin tildes). Los mensajes que pinta el cliente cambiaron de texto — las claves y el contrato, no | §2.7, §5.4, F-02 | 519 mensajes |
| [**B-02**](../ComentariosPruebasBackend/00002-UnitTypes.md#3-b-02--la-clave-del-error-de-búsqueda-es-search-pero-el-parámetro-público-es-q) | 🔵 Baja | ⛔ **Descartado** — no se abandona: **se resolvió en [00005 / B-02](../ComentariosPruebasBackend/00005-WorkCenters.md)**, que normalizó la clave en los 50 sitios del producto. **La clave ya es `q`** | F-01, F-03 | 50 sitios |
| [**B-03**](../ComentariosPruebasBackend/00002-UnitTypes.md#4-b-03--el-mensaje-de-formato-no-dice-cuál-es-el-formato-y-filtra-nombres-de-propiedad) | 🔵 Baja | 🟢 **Resuelto 2026-08-20.** El mensaje de formato **ya dice cuál es el formato**. Eran **46 sitios**, no 3 — y las reglas no eran iguales entre sí (50 y 80 caracteres, juegos de caracteres distintos), así que cada uno declara la suya | Residual de B-01 | 46 sitios |
| [**B-04**](../ComentariosPruebasBackend/00002-UnitTypes.md#5-b-04--los-nombres-de-propiedad-siguen-en-inglés-dentro-de-mensajes-en-español) | 🔵 Baja | 🟢 **Resuelto 2026-08-20.** Las etiquetas de campo salen en español (`'Nombre'`, `'Orden'`). ⚠️ **Poblado incremental**: 45 de 431 campos tienen etiqueta; el resto sigue mostrando el nombre inglés partido | Punto 4 de B-01 | Mecanismo + 45 etiquetas |
| [**00003 / B-04**](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca) | 🟡 Media | 🟢 **Resuelto 2026-08-16.** **Capacidad nueva para esta pantalla**: `GET /usage` + `DELETE` condicional (§2.8) | §2.1 de este documento | 5 recursos |

### Mediciones que esta corrida aporta a hallazgos anteriores

| Hallazgo | Qué se midió |
|---|---|
| [**00001 / B-02**](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#3-b-02--el-400-de-validación-agrupa-todos-los-mensajes-bajo-la-clave-vacía) | **Alcance acotado.** `OrgStructureCatalogs` usa expresiones directas y **sí** devuelve claves de campo. El defecto de la clave `""` **no es general del backend**: es del patrón de accesores lambda compartidos |
| [**00001 / B-03**](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location) | **Alcance confirmado y ampliado.** El proxy no solo pisa el `ETag`: **también descarta `Location`** en el `201`. Transversal confirmado en dos módulos (§5.2) |

---

## 8. Qué NO se probó en esta corrida

| Escenario | Motivo |
|---|---|
| `409 ORG_STRUCTURE_CATALOG_IN_USE` (inactivar un tipo en uso) | Requiere unidades organizativas que referencien un tipo — **se podrá probar en el Paso 3** |
| `404 ORG_STRUCTURE_CATALOG_NOT_FOUND` | Requiere un id inexistente o de otro tenant |
| Manejo de `403` (sin permiso) | El usuario es OWNER |
| Paginación con más de una página | 9 registros caben en la página de 10. Es justo donde **F-01** duele más |
| Comportamiento con `allowedActions` restringidos | El usuario es OWNER: todas las acciones vienen permitidas |
| `PATCH activate/inactivate` con `If-Match` desactualizado | Se verificó la concurrencia en `PUT`; los `PATCH` comparten el mismo binder |

---

## 9. Estado en que queda la empresa

**`End to End SAS` queda operativa para continuar al Paso 3.** Datos cargados según el escenario AVIANCA:

### Tipos de unidad — 9 (guía §3.1)

| Código | Nombre | Orden | Estado |
|---|---|---|---|
| `DIRECCION_GENERAL` | Dirección General | 10 | Activo |
| `VICEPRESIDENCIA` | Vicepresidencia | 20 | Activo |
| `DIRECCION` | Dirección | 30 | Activo |
| `GERENCIA` | Gerencia | 40 | Activo · con descripción |
| `JEFATURA` | Jefatura | 50 | Activo |
| `DEPARTAMENTO` | Departamento | 60 | Activo |
| `AREA` | Área | 70 | Activo |
| `BASE` | Base / Estación | 80 | Activo |
| `UNIDAD` | Unidad | 90 | **Inactivo** — la guía indica inactivarlo si no se usa |

### Áreas funcionales — 11 (guía §3.2)

`OPS_VUELO` · `SERV_ABORDO` · `MANTENIMIENTO` · `AEROPUERTOS` · `CARGA` · `SEG_OPERACIONAL` · `COMERCIAL` · `FINANZAS` · `GENTE` · `TECNOLOGIA` · `LEGAL`

> Se cargaron aquí aunque no son un paso del asistente: viven en la misma pantalla, el Paso 3 las necesita para armar el organigrama, y `GENTE` es la que alimenta el indicador de ratio de RRHH según §2.3 de la guía.
>
> Los genéricos `ADMIN` / `OPS` / `SALES` que la guía manda inactivar **no existían** (ver **F-08**), así que no hubo nada que inactivar.

### Verificación en el asistente

`/setup` pasó de **1/8** a **2/8 complete**. El Paso 2 quedó *Completed* y el **Paso 3 (Org units) es ahora el siguiente paso disponible**.

---

## 10. Prioridad sugerida

### Frontend

| # | Hallazgo | Depende de backend | Esfuerzo |
|---|---|---|---|
| 1 | **F-01** — `Search=` → `q=` (+ debounce por el rate limit) | No | Bajo |
| 2 | **F-05** — guarda de 2 caracteres | Va junto con **F-01** | Muy bajo |
| 3 | **F-02** — `maxlength`, patrón de código y `trim()` | Mejor mensaje con **B-01** | Bajo |
| 4 | **F-03** — mapear `errors[<campo>]` al input | No — el contrato ya lo permite | Medio |
| 5 | **F-04** — `aria-required` | No | Muy bajo |
| 6 | **F-06** — confirmación o *deshacer* al inactivar | No | Bajo |
| 7 | **F-07** — `min="0"` | No | Muy bajo |
| 8 | **F-08** | Nada que hacer; corregir las guías del backend | — |

### Backend

| # | Hallazgo | Cambio | Estado |
|---|---|---|---|
| 1 | **B-01** | Localizar los mensajes de `OrgStructureCatalogs` (validación y títulos) y explicitar el formato del código | 🔲 Propuesto |
| 2 | **B-02** | Alinear la clave del error de búsqueda (`search`) con el parámetro público (`q`) | 🔲 Propuesto |
| 3 | **00001 / B-03** | Sube de prioridad: confirmado transversal en dos módulos y afecta a dos cabeceras | 🔲 Propuesto |
| 4 | Documentación | Corregir §0/§3.1/§3.2 de la guía AVIANCA y §7 de la guía de perfil legal (**F-08**) | 🔲 Propuesto |

---

## 11. Para reintegrar y publicar — revisión de esta pantalla

> **Cómo leer esta lista.** No sabemos qué tiene hoy el cliente, así que **no está escrita como «cambios»
> sino como comprobaciones**. Si ya coincide, no hay nada que tocar.
>
> **Es la pantalla con más movimiento del backend de toda la corrida.** Cuatro hallazgos de servidor
> aterrizaron aquí, pero **ninguno rompe el contrato**: cambian textos y añaden capacidad.

### 11.1 Lo que puede romper algo — nada

**No hay ningún cambio de forma en esta pantalla.** Las rutas, los verbos, los cuerpos, las claves de
error y los códigos son los mismos que se probaron el 2026-08-15.

La única salvedad, y solo si el cliente hace algo poco habitual:

| Si el cliente… | Entonces… |
|---|---|
| Compara el **texto** de un mensaje de error para decidir algo | Dejará de coincidir: los mensajes cambiaron de idioma y de redacción |
| Compara el **nombre** de un elemento de catálogo en vez de su `code` | Los nombres del producto ganaron tildes y eñes el 2026-08-21 |

Lo correcto en ambos casos es decidir por `code`, nunca por texto visible.

### 11.2 Lo que mejoró solo, sin tocar el cliente

Estas cuatro cosas ya funcionan y **el usuario las nota sin que el frontend haga nada**:

| # | Antes | Ahora |
|---|---|---|
| 1 | `'Code' must not be empty.` | `'Nombre' no debería estar vacío.` — **en español** |
| 2 | `Code format is invalid.` | *«El código debe empezar con letra o número y solo admite letras, números, guion y guion bajo, hasta 50 caracteres.»* — **dice la regla** |
| 3 | Clave de error `search` para el buscador | Clave `q`, **el nombre público del parámetro** |
| 4 | Etiquetas de campo en inglés | `'Nombre'`, `'Orden'` en español (45 campos poblados; el resto llegará) |

⚠️ **Por eso mismo conviene revisar si el cliente estaba compensando alguno.** Si el formulario hoy
sustituye el mensaje del servidor por uno propio porque venía en inglés, ahora hay **dos textos en
español diciendo lo mismo con distintas palabras**. La recomendación es dejar pasar el del servidor.

### 11.3 Capacidad nueva, opcional — el borrado condicional

`GET /usage` + `DELETE` (§2.8). **La pantalla no lo ofrece hoy y no está obligada a ofrecerlo.**

Vale la pena porque resuelve un caso real que la baja lógica deja abierto: **el tipo tecleado mal que
nadie llegó a usar** se queda para siempre en el catálogo, ensuciando los combos. En una configuración
inicial de 9 tipos, dos o tres errores de captura son lo esperable.

Si se implementa: llamar a `/usage` al abrir el menú de acciones y mostrar *Eliminar* solo con las cuatro
cuentas en cero, para que el botón destructivo no aparezca cuando va a fallar.

### 11.4 Lo que NO cambió y no hay que tocar

- Las seis rutas originales, sus verbos y su autorización.
- El cuerpo de `POST`/`PUT` y las cuatro reglas de validación.
- **`q` sigue siendo el nombre del parámetro de búsqueda** — F-01 sigue vigente: el cliente envía `Search=`.
- El mínimo de 2 caracteres en la búsqueda, y el `400` si se manda 1.
- `If-Match` obligatorio en `PUT`, `PATCH /activate` y `PATCH /inactivate`.
- El listado **no trae `description`**: es carga solo de detalle.
- El catálogo **arranca vacío** (F-08): sigue siendo el estado inicial correcto.

### 11.5 Orden sugerido para volver a probar de cero

1. **F-01 primero, sin duda.** El buscador **no filtra nada** hoy porque envía `Search=` en vez de `q=`.
   Es un cambio de una palabra y es el hallazgo de mayor impacto de la pantalla.
2. **F-02** (validación en cliente): con el servidor ya diciendo la regla, basta con que el cliente
   coincida con ella.
3. **F-03** (mapear errores al input): el servidor ya da todo lo necesario — clave por campo, en español.
4. F-04, F-05, F-06, F-07 después: ninguno impide operar.
5. **F-08 no es un defecto**, es documentación desalineada: el catálogo arranca vacío a propósito.
6. §2.8 (borrado) es opcional y puede ir en una versión posterior.

> ⚠️ **Este documento no se ha vuelto a probar contra el ambiente.** Lo revalidado el 2026-08-21 es **el
> contrato del servidor**: rutas, verbos, esquemas y la forma literal del `400`, con un volcado real.
> Los hallazgos F-01…F-08 siguen tal como se observaron el 2026-08-15: no se han añadido ni retirado,
> porque no se ha repetido la corrida.
