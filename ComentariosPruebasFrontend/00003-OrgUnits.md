# 00003 — OrgUnits · Unidades organizativas

| | |
|---|---|
| **ID** | 00003-OrgUnits |
| **Paso probado** | Configuración guiada (`/setup`) → **Paso 3: Org units** |
| **Pantalla** | `/org-units` (pestaña *Units*) |
| **Fecha** | 2026-08-15 |
| **Ambiente** | Producción — `https://dashboard.clarihr.com` |
| **Empresa de prueba** | `End to End SAS` |
| **Usuario** | christopher canas (OWNER) |
| **Alcance** | **Ciclo completo** con el organigrama del escenario AVIANCA — 26 unidades en 4 niveles |
| **Objetivo** | Verificar que la pantalla expone todo lo que la BD y el endpoint requieren, y que el ciclo completo funciona de punta a punta |

---

## 1. Resumen ejecutivo

🔴 **Cobertura de campos: 6/9. Es el primer paso con carencias reales de cobertura.** El formulario —crear y editar— **no expone el área funcional**, que el escenario asigna a las 26 unidades y de la que depende un indicador del tablero (**F-01**), ni `sortOrder`, y el organigrama sale desordenado por eso (**F-03**). La novena, el jefe de la unidad, **se analizó y su ausencia es correcta hoy** (**F-09**): los expedientes son el Paso 8, el combo estaría vacío.

🔴 **El filtro por columna oculta registros que sí existen.** Con 26 unidades, filtrar `Code = GER` responde «No results» aunque hay **8 unidades `GER-*`**: solo mira las 10 filas cargadas, y el pie de página sigue diciendo «of 26» (**F-02**).

🟡 **Ciclo incompleto por la interfaz.** Crear, leer, editar y mover funcionan. **Inactivar y reactivar no existen en la pantalla**, aunque el backend los ofrece y tiene un guard dedicado (**F-04**).

🟢 **El árbol y sus guards son sólidos.** Ciclos, padre inexistente, código duplicado, hijos activos y centro de costo inválido: los cinco se rechazan correctamente. El organigrama quedó con **una sola raíz, 26 nodos y profundidad 4**, como pide la guía.

🟢 **El buscador general funciona** y manda el parámetro correcto — al contrario que la pestaña vecina del Paso 2.

🟢 **Datos cargados: el organigrama AVIANCA quedó operativo.** El asistente pasó de `2/8` a `3/8` y desbloqueó los Pasos 4 y 6 (§9).

**Frontend** (§6)

| Severidad | Cantidad | IDs |
|---|---|---|
| 🔴 Alta | 3 | F-01, F-02, F-03 |
| 🟡 Media | 4 | F-04, F-05, F-06, F-07 |
| 🔵 Baja | 1 | F-08 |
| ⚪ Informativo | 2 | F-09, F-10 |

**Backend** (§7)

| Severidad | ID | Resumen |
|---|---|---|
| 🔴 Alta | B-01 | `/diagram-export` devuelve `500` con su formato por defecto |
| 🟡 Media | B-02 | **Canal de localización completo que ningún cliente puede activar**: las traducciones existen y el pipeline está cableado, pero la preferencia de idioma nunca llega al servidor |

**Nada quedó sin medir salvo dos escenarios que comparten montaje** —el límite de profundidad y el manejo de `403`—, ambos con plan definido y a la espera de autorización para crear una empresa desechable (§8).

---

## 2. Contrato de referencia (backend)

### 2.1 Endpoints del recurso

```
GET    /api/v1/companies/{companyPublicId}/organization-units
GET    /api/v1/organization-units/{publicId}
GET    /api/v1/companies/{companyPublicId}/organization-units/tree
GET    /api/v1/companies/{companyPublicId}/organization-units/graph
GET    /api/v1/companies/{companyPublicId}/organization-units/export
GET    /api/v1/companies/{companyPublicId}/organization-units/diagram-export
POST   /api/v1/companies/{companyPublicId}/organization-units
PUT    /api/v1/organization-units/{publicId}
PATCH  /api/v1/organization-units/{publicId}              ← JSON Patch
PATCH  /api/v1/organization-units/{publicId}/move
PATCH  /api/v1/organization-units/{publicId}/activate
PATCH  /api/v1/organization-units/{publicId}/inactivate
```

> El frontend los consume vía proxy como `/v1/org-units`. **Confirmado en vivo.**
>
✅ **Hay dos endpoints más desde el 2026-08-16 que esta corrida no conoció:**

```
GET     /api/v1/organization-units/{publicId}/usage
DELETE  /api/v1/organization-units/{publicId}      ← If-Match obligatorio
```

> **Cuando se probó esta pantalla no existían.** El documento decía que no había `DELETE` ni `/usage`, y
> era cierto: cuando la baja se rechazaba, no había forma de saber qué la bloqueaba. Ése era el hallazgo
> [**00003 / B-04**](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca),
> ya resuelto. Ver §2.7.
>
> **La interfaz usa 6 de los 12 endpoints**: listado, detalle, árbol, export, `POST`, `PUT` y `move`. No usa `graph`, `diagram-export`, el `PATCH` de JSON Patch, ni `activate`/`inactivate` (**F-04**).

### 2.2 Cuerpo de `POST`

```jsonc
{
  "code": "GER-VUELO",                  // requerido, máx. 50, formato ^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$
  "name": "Gerencia de Operaciones…",   // requerido, máx. 150
  "orgUnitTypePublicId": "…",           // REQUERIDO
  "functionalAreaPublicId": "…",        // opcional  ← NO está en el formulario (F-01)
  "parentPublicId": "…",                // opcional — vacío = unidad raíz
  "sortOrder": 10,                      // opcional, >= 0  ← NO está en el formulario (F-03)
  "description": "…",                   // opcional, máx. 500
  "costCenterCode": "CC-1000",          // opcional, máx. 100
  "managerEmployeePublicId": "…"        // opcional  ← ausente A PROPÓSITO (F-09)
}
```

`PUT` recibe lo mismo **menos `parentPublicId`**: el padre se cambia con `PATCH /move`, no con la edición.

### 2.3 Respuesta

```jsonc
{
  "publicId": "…",
  "code": "…",
  "name": "…",
  "orgUnitType":   { "publicId": "…", "code": "GERENCIA", "name": "Gerencia" },
  "functionalArea":{ "publicId": "…", "code": "OPS_VUELO", "name": "…" },   // nullable
  "parent":        { "publicId": "…", "code": "VP-OPS",   "name": "…" },    // nullable
  "sortOrder": null,
  "description": null,        // solo en el detalle — el listado lo OMITE
  "costCenterCode": null,
  "managerEmployeeId": null,
  "isActive": true,
  "concurrencyToken": "…",
  "createdAtUtc": "…",
  "modifiedAtUtc": null,
  "allowedActions": { … }
}
```

> **El listado devuelve `functionalArea`, `parent`, `sortOrder`, `costCenterCode`, `managerEmployeeId` e `isActive`.** La tabla de la pantalla **no muestra ninguno** — ver **F-06**.

### 2.4 Autorización

| Operación | Permisos aceptados (cualquiera) |
|---|---|
| `GET` | `OrgUnits.Read` · `OrgUnits.Admin` · `iam.administration.manage` |
| Escrituras | `OrgUnits.Admin` · `iam.administration.manage` |

`ResourceKey`: **`ORG_UNITS`**.

### 2.5 Reglas de negocio que gobiernan el recurso

1. **Formato del código:** el mismo del Paso 2 — `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$` sobre el valor recortado. Los códigos del escenario (`VP-OPS`, `DEP-DESPACHO`) son válidos.
2. **Código único por empresa** (`uq_org_units__tenant_code`). Duplicado → `409`. **Verificado en vivo.**
3. **El padre debe existir.** Un id inexistente → `404 ORG_UNIT_PARENT_NOT_FOUND`. **Verificado en vivo.**
4. **No se admiten ciclos.** Mover una unidad bajo uno de sus propios descendientes → `409 ORG_UNIT_CYCLE_DETECTED`. **Verificado en vivo.**
5. **Profundidad máxima: 15 niveles.** Superarla → `409 ORG_UNIT_DEPTH_LIMIT_EXCEEDED`.

   ⚠️ **El `PATCH /move` cambió de comportamiento el 2026-08-16** ([**00003 / B-02**](../ComentariosPruebasBackend/00003-OrgUnits.md#3-b-02--move-no-valida-la-altura-del-subárbol-el-árbol-supera-el-límite-que-el-propio-servidor-declara)).
   Antes medía **solo la profundidad del nodo movido**; ahora mide **el nodo más la altura de su
   subárbol**. Es decir: mover una rama entera se rechaza si **cualquiera de sus descendientes** quedaría
   por debajo del nivel 15, no solo si el nodo movido lo hace.

   **Consecuencia para el cliente:** algunos movimientos que antes tenían éxito ahora responden `409`
   con el mismo código. **No es un fallo del cliente** — antes el servidor los aceptaba y dejaba el árbol
   por encima de su propio límite. El mensaje que se muestre debe hablar del **subárbol**, no del nodo:
   «mover esta rama dejaría unidades por debajo del nivel 15».

   > Efecto secundario que conviene conocer: **una unidad que hoy ya esté por encima del límite no se
   > puede reorganizar** hasta acortar su rama, porque el movimiento parte de un estado inválido.
6. **No se puede inactivar una unidad con hijos activos** → `409 ORG_UNIT_HAS_ACTIVE_CHILDREN`. Hay que inactivar de abajo hacia arriba. **Verificado en vivo.**

   ⚠️ **Desde el 2026-08-18 inactivar una unidad tiene un efecto más amplio que antes** ([**00950 / B-02**](../ComentariosPruebasBackend/00950-Remediacion.md)):
   con la unidad de baja, **no se pueden crear plazas nuevas** contra ningún perfil de puesto de esa
   unidad → `422 POSITION_SLOT_ORG_UNIT_INACTIVE`.

   **Lo que NO cambia:** las plazas que ya existen siguen funcionando y el histórico se conserva. Se
   bloquea crear futuro, no preservar pasado.

   **Para el cliente:** si se ofrece la acción de inactivar (**F-04**), el diálogo debería decir que la
   unidad dejará de admitir plazas nuevas. Hoy el usuario no tiene forma de saberlo hasta que falla dos
   pantallas más adelante.
7. **El centro de costo se valida por código**, no por id: si no existe o está inactivo → `422 ORG_UNIT_COST_CENTER_INVALID`.
8. **Búsqueda con mínimo de 2 caracteres**, igual que el Paso 2. La razón está documentada en el código: el `LIKE '%x%'` recorre 6 columnas y 4 joins.
9. **Concurrencia obligatoria** en `PUT`, `move`, `activate` e `inactivate`. **Verificada en vivo.**
10. **El catálogo arranca vacío**, igual que el Paso 2.

### 2.6 Catálogo de errores

| Código | HTTP | Cuándo | Verificado |
|---|---|---|---|
| `ORG_UNIT_NOT_FOUND` | `404` | La unidad no existe o es de otro tenant | — |
| `ORG_UNIT_PARENT_NOT_FOUND` | `404` | El padre indicado no existe | ✅ en vivo |
| `ORG_UNIT_CODE_CONFLICT` | `409` | Otra unidad ya usa ese código | ✅ en vivo |
| `ORG_UNIT_CYCLE_DETECTED` | `409` | El movimiento crearía un ciclo | ✅ en vivo |
| `ORG_UNIT_HAS_ACTIVE_CHILDREN` | `409` | Se intenta inactivar con hijos activos | ✅ en vivo |
| `ORG_UNIT_IN_USE` | `409` | Se intenta **borrar** una unidad que algo referencia. **Código distinto del anterior**: inactivar mira hijos activos, borrar mira además perfiles de puesto e hijos inactivos (§2.7) | — |
| `ORG_UNIT_DEPTH_LIMIT_EXCEEDED` | `409` | Se superan los 15 niveles | — |
| `ORG_UNIT_COST_CENTER_INVALID` | `422` | Centro de costo inexistente o inactivo | — |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado | ✅ en vivo |
| `ORG_UNITS_FORBIDDEN` | `403` | Sin permiso | — |

### 2.7 Borrado condicional — capacidad nueva (2026-08-16)

Existe desde después de esta corrida. **La pantalla no la ofrece hoy**, y añadirla es opcional.

**Primero se pregunta qué la referencia:**

```
GET /v1/org-units/{publicId}/usage        → 200
```

```jsonc
{
  "publicId": "…",
  "code": "GER-VUELO",
  "name": "Gerencia de Operaciones de Vuelo",
  "activeChildren": 2,                  // unidades hijas activas
  "inactiveChildren": 0,                //   …e inactivas
  "jobProfileActiveReferences": 5,      // perfiles de puesto que la usan
  "jobProfileInactiveReferences": 1,    //   …inactivos
  "hasActiveReferences": true
}
```

**Y luego se borra, si procede:**

```
DELETE /v1/org-units/{publicId}     If-Match: "{concurrencyToken}"     → 200
```

Devuelve **la unidad eliminada** con `200` (no `204`).

| Situación | Respuesta |
|---|---|
| Nada la referencia | `200` con la unidad borrada |
| Algo la referencia, activo **o** inactivo | `409 ORG_UNIT_IN_USE` |
| `If-Match` ausente / desactualizado | `400` / `409 CONCURRENCY_CONFLICT` |
| Sin permiso `OrgUnits.Admin` | `403` |

> **Ésta es la respuesta al hallazgo original**, que no era «falta un botón de borrar» sino **«el servidor
> sabe por qué no se puede inactivar y no lo dice»**. `/usage` devuelve las cuatro cuentas separadas, así
> que la pantalla puede decir «la usan 2 unidades hijas y 5 perfiles de puesto» en lugar de un `409`
> opaco. **Sirve igual de bien para explicar un rechazo de inactivación**, aunque el borrado no se
> implemente nunca.

⚠️ **Inactivar y borrar no miran lo mismo:**

| | Mira | No mira |
|---|---|---|
| `PATCH /inactivate` | hijos **activos** | hijos inactivos · perfiles de puesto |
| `DELETE` | **todo**: hijos activos e inactivos, perfiles activos e inactivos | — |

Una unidad puede ser inactivable y no borrable a la vez, y eso no es un error.


---

## 3. Cobertura de campos — resultado

### Formulario de creación y edición

| # | Campo API | ¿En el formulario? | Regla backend | Estado |
|---|---|---|---|---|
| 1 | `code` | ✅ `ou-code` | `NotEmpty` + máx. 50 + regex | 🟢 Presente |
| 2 | `name` | ✅ `ou-name` | `NotEmpty` + máx. 150 | 🟢 Presente |
| 3 | `description` | ✅ `ou-desc` | máx. 500 | 🟢 Presente |
| 4 | `orgUnitTypePublicId` | ✅ combo *Unit type* | **Requerido** | 🟢 Presente · filtra correctamente los tipos inactivos |
| 5 | `parentPublicId` | ✅ combo *Parent unit* | opcional | 🟢 Presente, solo en creación (correcto: el `PUT` no lo acepta) |
| 6 | `costCenterCode` | ✅ combo *Cost center* | opcional, máx. 100 | 🟢 Presente |
| 7 | **`functionalAreaPublicId`** | ❌ **AUSENTE** | opcional | 🔴 **F-01** |
| 8 | **`sortOrder`** | ❌ **AUSENTE** | opcional, >= 0 | 🔴 **F-03** — sí debería estar |
| 9 | **`managerEmployeePublicId`** | ❌ **AUSENTE** | opcional | ⚪ **F-09** — **correcto que no esté** hoy |

### Tabla del listado

| Campo de la respuesta | ¿Se muestra? |
|---|---|
| `code`, `name`, `orgUnitType` | 🟢 Sí |
| `parent` | ❌ No — **F-06** |
| `functionalArea` | ❌ No — **F-06** |
| `costCenterCode` | ❌ No — **F-06** |
| `sortOrder` | ❌ No |
| `isActive` | ❌ No — y no hay acción de estado (**F-04**) |

### Extras correctos que vale la pena reconocer

- **`aria-required="true"` está presente** en `Code` y `Name`.
- **El combo de tipo de unidad excluye los inactivos.** Buscando `UNIDAD` —el tipo que inactivamos en el Paso 2— responde «No results». La baja lógica del paso anterior surte el efecto esperado aguas abajo.
- **Los combos tienen buscador propio** y muestran `CÓDIGO - Nombre`, que es la forma correcta de desambiguar catálogos.
- **La vista de árbol funciona** y expone la acción **Move** por nodo, que es la vía correcta para cambiar de padre (el `PUT` no acepta `parentPublicId`).
- **La ayuda contextual es buena**: «Leave empty for top-level unit», «Cost center associated with this unit».
- **Los cuatro guards del árbol responden correctamente** (§5.4).
- **Hay exports CSV y Excel** desde la propia pantalla.

---

## 4. Validación en cliente

| # | Entrada | Resultado | ¿Coincide con el backend? |
|---|---|---|---|
| 1 | `code` y `name` vacíos | *Create* deshabilitado | ✅ |
| 2 | Sin *Unit type* | *Create* deshabilitado · la etiqueta se marca en rojo | ✅ |
| 3 | Datos válidos | *Create* habilitado | ✅ |

> **No se repitió la batería completa de formato y longitudes del Paso 2.** El validador del backend es **el mismo** (mismo regex, `code` 50 / `name` 150 / `description` 500) y el formulario **tampoco declara `maxlength`**, así que **F-02 del Paso 2 aplica igual aquí**. Se registra como **F-08** para no perderlo de vista.

---

## 5. Ciclo completo — resultado

Ejecutado con el organigrama del escenario AVIANCA (guía §5).

### 5.1 Las operaciones

| Operación | Cómo se ejecutó | Resultado |
|---|---|---|
| **Crear** | Interfaz — panel *Create record* | 🟡 `201`, pero **la lista no se refresca** (**F-05**) |
| **Leer** | Listado, detalle, árbol y `graph` | 🟢 Correctos; el detalle trae `description`, que el listado omite |
| **Editar** | Interfaz — *Edit* → *Save* | 🟢 `200` |
| **Mover** | `PATCH /move` (acción *Move* del árbol) | 🟢 Guard de ciclo correcto |
| **Inactivar / reactivar** | API — la interfaz no lo ofrece (**F-04**) | 🟢 Ciclo completo correcto (§5.5) |
| **Borrar** | — | No existe, y es correcto |

### 5.2 Carga del organigrama

26 unidades en 4 niveles. La primera (`DG`) se creó por la interfaz para ejercitar el formulario; las 25 restantes por API, **porque el formulario no permite asignar el área funcional** (**F-01**) y el escenario la exige en todas.

Resultado verificado en `GET /tree`:

```
raíces: 1  →  DG
nodos totales: 26
profundidad máxima: 4
```

Coincide con lo que pide la guía: *«La vista de árbol debe mostrar una sola raíz (DG); si aparecen varias, alguna unidad quedó sin padre.»*

### 5.3 Concurrencia

`PUT` con el token vigente → `200`. Los `PATCH` de estado rotan el token y lo devuelven en el cuerpo (§5.5). El binder de `If-Match` es el mismo que se verificó a fondo en el Paso 2.

### 5.4 Los guards — los cinco correctos

| Prueba | Resultado |
|---|---|
| `POST` con código ya existente | 🟢 `409 ORG_UNIT_CODE_CONFLICT` |
| `POST` con `parentPublicId` inexistente | 🟢 `404 ORG_UNIT_PARENT_NOT_FOUND` |
| `PATCH /move` de `DG` bajo su propio nieto `GER-VUELO` | 🟢 `409 ORG_UNIT_CYCLE_DETECTED` |
| `PATCH /inactivate` de `DG`, que tiene 8 hijos activos | 🟢 `409 ORG_UNIT_HAS_ACTIVE_CHILDREN` |
| `POST` con `costCenterCode` inexistente | 🟢 `422 ORG_UNIT_COST_CENTER_INVALID` — **y no creó nada**: el total siguió en 26 |

Es la parte más sólida de este paso. El árbol se defiende de todas las formas de corromperlo.

> **Ojo con el de hijos activos:** obliga a inactivar **de abajo hacia arriba**. Como la interfaz no ofrece inactivar (**F-04**), hoy no hay forma de dar de baja una rama desde la pantalla.

### 5.5 Ciclo de estado — medido y revertido

Ejecutado sobre una **hoja** (`DEP-TALENTO`), para que fuera reversible sin tocar el resto del árbol:

| Paso | Resultado |
|---|---|
| `PATCH /inactivate` | 🟢 `200` · `isActive: false` · **token rotado** |
| `GET ?isActive=false` | 🟢 `total=1` → `DEP-TALENTO` |
| `GET ?isActive=true` | 🟢 `total=25` |
| `GET /tree` | ⚠️ **26 nodos** — el árbol sigue mostrándola → **F-07** |
| `PATCH /activate` | 🟢 `200` · `isActive: true` · **restaurada** |

### 5.6 El buscador: aquí sí funciona

`q=GERENCIA` sobre las 26 unidades:

```
GET /v1/org-units?Page=1&PageSize=10&IncludeAllowedActions=true&q=GERENCIA   →  200, total=12
```

Y las 12 coincidencias explican **cómo** busca:

| Coincide por | Ejemplos |
|---|---|
| Nombre del tipo de unidad | las 8 `GER-*`, de tipo *Gerencia* |
| **Nombre del padre** | `DEP-COUNTER` y `DEP-RAMPA` (padre *Gerencia de Aeropuertos*) · `DEP-DESPACHO` y `JEF-PILOTOS` (padre *Gerencia de Operaciones de Vuelo*) |

Coincide con lo que documenta el código: el `LIKE '%x%'` recorre **6 columnas y 4 joins**. El marcador de posición dice «Search by code or name…», pero busca bastante más — es una imprecisión menor de la etiqueta, no un defecto.

**Par diferencial que lo prueba:**

| Petición | Resultado |
|---|---|
| `?q=GERENCIA` | `total=12` ✅ filtrado |
| `?Search=GERENCIA` | `total=26` ❌ ignorado |

> 🔑 **Esto reencuadra [00002 / F-01](00002-UnitTypes.md#-f-01--el-buscador-envía-search-y-la-api-espera-q-no-filtra-nada).** En la misma página, la pestaña *Units* manda `q=` y funciona, mientras la pestaña *Unit types* manda `Search=` y no. Las dos peticiones conviven en el registro de red. **No es desconocimiento del contrato: es una inconsistencia interna**, y el arreglo tiene implementación de referencia a una pestaña de distancia.

### 5.7 Exports y diagramas

| Petición | Resultado |
|---|---|
| `/export?format=csv` | `200` · `text/csv` · **`Content-Disposition` AUSENTE** |
| `/export` (xlsx por defecto) | `200` · `…spreadsheetml.sheet` · **AUSENTE** |
| `/graph` | `200` · 26 nodos, 25 aristas · incluye `isActive` por nodo |
| **`/diagram-export`** (sin `format`) | 🔴 **`500 Internal server error`** → **B-01** |
| `/diagram-export?format=svg\|png\|pdf` | `400 REPORT_FORMAT_NOT_SUPPORTED` — correcto |
| 8 peticiones seguidas al export | `429 common.too_many_requests` — hay rate limit propio |

**El nombre del archivo descargado — medido sin descargar.** Se envolvieron `URL.createObjectURL` y `HTMLAnchorElement.click` para capturar lo que la interfaz *haría*, y luego se restauraron:

```
createObjectURL  →  mime: text/csv · 8114 bytes
a.click          →  download: "org-units.csv"
```

> **El frontend fija el nombre por su cuenta**, con un blob y el atributo `download`. Por tanto, **la ausencia de `Content-Disposition` no tiene síntoma visible en esta pantalla**.
>
> Eso **cierra la última pregunta de [00001 / B-03](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location)** y **matiza su impacto**: van tres cabeceras descartadas en tres módulos —`ETag`, `Location`, `Content-Disposition`—, pero **ninguna rompe al cliente actual hoy**, porque el frontend no depende de ninguna. El riesgo es latente: cualquier otro consumidor —un enlace directo, un cliente que siga la convención HTTP— sí se rompería.

---

## 6. Hallazgos

---

### 🔴 F-01 — El formulario no permite asignar el área funcional

**Severidad:** Alta · **Tipo:** Cobertura de campos

#### Evidencia (frontend)

Los paneles *Create record* y *Edit record* tienen exactamente seis controles:

```
Identification : Code * · Name * · Description
Classification : Unit type *
Relationships  : Parent unit · Cost center
```

No hay ningún control para el área funcional. La unidad creada por la interfaz quedó con `functionalArea: null`.

#### Regla de negocio que lo gobierna

El backend lo soporta **completo**, en las cuatro capas:

| Capa | Evidencia |
|---|---|
| Petición | `CreateOrgUnitRequest.FunctionalAreaPublicId` y `UpdateOrgUnitRequest.FunctionalAreaPublicId` |
| Validación | `RuleFor(c => c.FunctionalAreaId).NotEqual(Guid.Empty).When(…)` |
| Respuesta | `OrgUnitResponse.FunctionalArea` **y** `OrgUnitListItemResponse.FunctionalArea` |
| Dominio | `OrgUnit.FunctionalAreaCatalogItemId` |

#### Impacto

1. **El escenario documentado no se puede cargar desde la interfaz.** La guía asigna un área funcional a **las 26 unidades**.
2. **Rompe un indicador del tablero.** La preferencia «Área funcional de RRHH» (`GENTE`) *«alimenta el indicador de ratio de RRHH»*. Sin unidades marcadas, no hay de dónde contar.
3. **El catálogo de áreas funcionales queda huérfano.** La pestaña *Functional areas* mantiene áreas que después no se pueden asignar a nada desde la interfaz.

#### Contrato para el frontend

**a) Alimentar el combo**

```
GET /v1/functional-areas?isActive=true&page=1&pageSize=20&q={texto}
```

```jsonc
{ "items": [ { "publicId": "…", "code": "GENTE", "name": "Gente y Cultura",
               "sortOrder": 90, "isActive": true, "concurrencyToken": "…" } ],
  "page": 1, "pageSize": 20, "totalCount": 11, "totalPages": 1 }
```

| Regla operativa | Valor |
|---|---|
| `pageSize` | default 20, **máx. 100** (`101` → `400`) |
| `q` | mínimo **2 caracteres** tras `Trim`; vacío = sin filtro |
| Rate limit | ~120 peticiones por usuario+tenant → *debounce* de 300 ms |
| Filtrar inactivos | `isActive=true` — como ya hace bien el combo de tipo de unidad |

**b) Enviarlo en la escritura**

```jsonc
POST /v1/org-units            PUT /v1/org-units/{publicId}   ← If-Match obligatorio
{
  "code": "DEP-ADMPER",
  "name": "Administración de Personal",
  "orgUnitTypePublicId": "…",
  "functionalAreaPublicId": "…",   // ← el campo nuevo; opcional, admite null
  "parentPublicId": "…",           // solo en POST
  "sortOrder": null,
  "description": null,
  "costCenterCode": null,
  "managerEmployeePublicId": null
}
```

**c) Leerlo de vuelta** — ya viene, en listado y detalle:

```jsonc
"functionalArea": { "publicId": "…", "code": "GENTE", "name": "Gente y Cultura" }   // nullable
```

| Código de error | HTTP | Cuándo |
|---|---|---|
| *(validación)* | `400` | `functionalAreaPublicId` presente pero `Guid.Empty` → clave `functionalAreaId` |

#### Ajuste pedido al frontend

Agregar el combo en *Classification*, junto a *Unit type*. **Opcional en el contrato**, así que el control no debe ser obligatorio.

**No requiere cambio en el backend.**

---

### 🔴 F-02 — El filtro por columna oculta registros que sí existen

**Severidad:** Alta · **Tipo:** Funcional · **Medido con 26 registros**

#### Evidencia (frontend)

La tabla tiene tres cuadros `Filter...` (Code, Name, Unit type). Con los 26 registros cargados y `pageSize=10`:

| Acción | Pie de página | Filas mostradas |
|---|---|---|
| Sin filtro | `1–10 of 26` | 10: `DEP-ADMPER, DEP-ALMACEN, DEP-TALENTO, DEP-COMPRAS, DEP-CONTA, DEP-COUNTER, DEP-DESPACHO, DG, DIR-LEGAL, DIR-GENTE` |
| `Code = DEP` | `1–10 of 26` | 7 — todas de la página 1 |
| **`Code = GER`** | `1–10 of 26` | **«No results»** |

**Existen 8 unidades `GER-*`**: `GER-AEROP`, `GER-CARGA`, `GER-ING`, `GER-MTTO`, `GER-VUELO`, `GER-ABORDO`, `GER-TI`, `GER-VENTAS`. Están en las páginas 2 y 3.

Dos defectos en la misma medición:

1. **El filtro solo mira las 10 filas cargadas.** No consulta al servidor: el `totalCount` sigue en 26 y no se emite ninguna petición nueva.
2. **El pie de página no refleja el filtro.** Dice `1–10 of 26` mientras la tabla muestra «No results». Se contradicen entre sí.

#### Regla de negocio que lo gobierna

El listado **no admite filtros por campo**. Estos son todos sus parámetros:

```
?q= · ?isActive= · ?page= · ?pageSize= · ?includeAllowedActions=
```

No hay `code=`, ni `name=`, ni `orgUnitTypeId=`. Un filtro por columna **no tiene contraparte en el contrato**.

#### Impacto

**El usuario concluye que el registro no existe.** Es el peor resultado posible de un buscador: no falla, no avisa — afirma que no hay nada. Y el pie de página, que dice «26», refuerza la idea de que está viendo todo.

Empeora con el tamaño: cuantas más unidades, mayor la proporción invisible.

#### Contrato para el frontend

**No existe endpoint que soporte lo que la interfaz ofrece.** Las dos salidas honestas:

**Opción A (recomendada) — mapear los tres cuadros al parámetro que sí existe:**

```
GET /v1/org-units?q={texto}&page=1&pageSize=10&includeAllowedActions=true
```

El `q` del servidor **ya cubre los tres campos y más**: busca en código, nombre, nombre del tipo y nombre del padre (§5.6). Un solo cuadro de búsqueda hace el trabajo de los tres, y bien.

**Opción B — retirar los filtros por columna** y dejar solo el buscador general, que sí funciona.

Lo que **no** es opción es dejarlos filtrando la página cargada.

#### Ajuste pedido al frontend

Retirar los tres cuadros o mapearlos a `q`. Y en cualquier caso, **hacer que el pie de página refleje el resultado**: si la tabla muestra 0 filas, el pie no puede decir «of 26».

**No requiere cambio en el backend.**

---

### 🔴 F-03 — Falta `sortOrder`, y el orden del organigrama lo demuestra

**Severidad:** Alta · **Tipo:** Cobertura de campos

#### Evidencia (frontend)

Ni el panel de creación ni el de edición ofrecen control para `sortOrder`.

El síntoma se ve en el organigrama cargado. Bajo `DG`, los hijos aparecen así:

```
DIR-GENTE · DIR-SEG · DIR-LEGAL · GER-TI · VP-COM · VP-FIN · VP-OPS · VP-TEC
```

**Las direcciones y una gerencia salen antes que las cuatro vicepresidencias.** No es el orden jerárquico real de la empresa.

#### Regla de negocio que lo gobierna

`sortOrder` es `int?`, validado `GreaterThanOrEqualTo(0)` cuando viene, y **está en la respuesta del listado**. Existe precisamente para ordenar la presentación de los hermanos: es la única forma que da el contrato de decir «las vicepresidencias van antes que las direcciones».

**Sin él, el orden queda a merced del criterio por defecto del servidor**, que es alfabético por código y no tiene por qué coincidir con la jerarquía.

#### ¿Debería estar? Sí

En el Paso 2, el mismo campo **sí está** en el formulario de tipos de unidad, y allí se usó para ordenar los nueve tipos por nivel jerárquico (10, 20, 30…). Aquí, donde el orden importa más —porque se dibuja un organigrama—, falta.

#### Contrato para el frontend

Viaja en el **mismo cuerpo** que ya se envía; no hay endpoint nuevo.

```jsonc
POST /v1/org-units            PUT /v1/org-units/{publicId}   ← If-Match obligatorio
{
  "…": "…",
  "sortOrder": 10     // int?  ·  >= 0 cuando viene  ·  null = sin orden
}
```

Vuelve en la respuesta del listado y del detalle como `"sortOrder": 10`.

| Código de error | HTTP | Cuándo |
|---|---|---|
| *(validación)* | `400` | `sortOrder` negativo → clave `sortOrder` |

#### Ajuste pedido al frontend

Agregar el campo numérico con `min="0"`, igual que en tipos de unidad.

**No requiere cambio en el backend.**

---

### 🟡 F-04 — No se puede inactivar ni reactivar una unidad desde la interfaz

**Severidad:** Media · **Tipo:** Cobertura funcional

#### Evidencia (frontend)

| Vista | Acciones por fila/nodo |
|---|---|
| Lista | `Edit` |
| Árbol | `Edit` · `Move` |

No hay ninguna acción de estado, y la tabla tampoco muestra la columna de estado pese a que el listado devuelve `isActive`.

#### Regla de negocio que lo gobierna

El backend expone `PATCH /activate` e `/inactivate`, y tiene un guard **diseñado específicamente** para este flujo:

```
409 ORG_UNIT_HAS_ACTIVE_CHILDREN
```

Ese guard existe porque el flujo esperado es inactivar ramas **de abajo hacia arriba**. **Verificado en vivo** (§5.4 y §5.5).

#### Impacto

**Una rama del organigrama no se puede dar de baja desde la pantalla.** Cuando la empresa cierre un departamento, no hay camino por la interfaz. Y como no hay borrado, la baja lógica es la **única** vía — que además falla a ciegas, sin decir qué hijo la bloquea ([**B-03**(../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca)).

Contrasta con el Paso 2, donde los tipos de unidad **sí** tienen *Deactivate* / *Activate*. Misma pantalla, otra pestaña.

#### Contrato para el frontend

```
PATCH /v1/org-units/{publicId}/inactivate     If-Match: "{concurrencyToken}"
PATCH /v1/org-units/{publicId}/activate       If-Match: "{concurrencyToken}"
```

**Sin cuerpo.** Ambos devuelven `200` con la unidad completa y el token **rotado** — **verificado en vivo** en §5.5:

```jsonc
{ "publicId": "…", "code": "DEP-TALENTO", "isActive": false,
  "concurrencyToken": "…",   // ← NUEVO: hay que reemplazarlo en memoria
  "modifiedAtUtc": "…" }
```

> ⚠️ El token se toma **del cuerpo**, nunca del `ETag`: el proxy lo sustituye (00001 / B-03).

| Código de error | HTTP | Cuándo | Qué hacer |
|---|---|---|---|
| `ORG_UNIT_HAS_ACTIVE_CHILDREN` | `409` | La unidad tiene hijas activas | Mensaje explicativo — ver abajo |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado | Recargar la unidad y reintentar |
| *(falta `If-Match`)* | `400` | Cabecera ausente | Error de programación |
| `ORG_UNIT_NOT_FOUND` | `404` | Id inexistente o de otro tenant | Refrescar el listado |

Para listar las inactivas: `GET /v1/org-units?isActive=false` — **verificado**, devuelve exactamente las dadas de baja.

#### Ajuste pedido al frontend

1. Añadir las acciones de estado y **la columna de estado**.
2. **Manejar `409 ORG_UNIT_HAS_ACTIVE_CHILDREN` con un mensaje útil**: «No se puede inactivar *Dirección General* porque tiene unidades hijas activas. Inactive primero las que dependen de ella.» El código de error permite reconocer el caso sin depender del texto.

   ✅ **Y ahora se puede decir CUÁNTAS**, sin adivinar: `GET /v1/org-units/{publicId}/usage` (§2.7)
   devuelve `activeChildren`, `inactiveChildren`, `jobProfileActiveReferences` y
   `jobProfileInactiveReferences`. Llamarlo al abrir el diálogo permite pasar de «tiene unidades hijas
   activas» a «la usan **2 unidades hijas** y **5 perfiles de puesto**». **Era exactamente el hallazgo de
   backend que este documento levantó** (`00003 / B-04`), y ya está resuelto.

3. ⚠️ **Advertir del efecto que la inactivación tiene aguas abajo.** Desde el 2026-08-18, una unidad
   inactiva **no admite plazas nuevas** contra sus perfiles de puesto (§2.5, regla 6). El diálogo de
   confirmación debería decirlo: hoy el usuario lo descubriría dos pantallas más adelante, con un `422`
   que no menciona la unidad que él mismo dio de baja.

**No requiere cambio en el backend.**

---

### 🟡 F-05 — La lista no se refresca después de crear

**Severidad:** Media · **Tipo:** UX / estado de la interfaz

#### Evidencia (frontend)

Se creó `DG` desde el panel. El servidor respondió `201` y el registro quedó guardado —verificado por API: `totalCount: 1`—, pero **la pantalla siguió mostrando el estado vacío**: «No records yet».

La secuencia de red lo confirma: **tras el `POST` no se emitió ninguna petición de listado**. El registro solo apareció al recargar a mano.

#### Contraste

En el Paso 2 la lista **sí** se refresca tras crear, y muestra un *toast*. Aquí no ocurre ninguna de las dos cosas.

#### Impacto

El usuario cree que la operación falló y **vuelve a intentarlo**. El segundo intento choca con `409 ORG_UNIT_CODE_CONFLICT` — de modo que acaba viendo un error de duplicado sobre un registro que él mismo acaba de crear sin saberlo.

#### Contrato para el frontend

El `POST` **ya devuelve todo lo necesario** para refrescar sin releer:

```jsonc
POST /v1/org-units  →  201
{
  "publicId": "…", "code": "DG", "name": "Dirección General",
  "orgUnitType":    { "publicId": "…", "code": "DIRECCION_GENERAL", "name": "…" },
  "functionalArea": null,
  "parent":         null,
  "sortOrder": null, "costCenterCode": null, "managerEmployeeId": null,
  "isActive": true, "concurrencyToken": "…",
  "createdAtUtc": "…", "modifiedAtUtc": null
}
```

Y si se prefiere releer —más seguro por el orden—:

```
GET /v1/org-units?page=1&pageSize=10&includeAllowedActions=true   ← vista de lista
GET /v1/org-units/tree                                            ← vista de árbol
```

> ⚠️ **No usar `Location` para esto**: no llega al navegador (00001 / B-03). El cuerpo sí trae el `publicId`.

#### Ajuste pedido al frontend

Tras un `201`, refrescar listado y árbol, y mostrar la confirmación.

**No requiere cambio en el backend.**

---

### 🟡 F-06 — La tabla oculta las tres columnas que dan sentido a una unidad

**Severidad:** Media · **Tipo:** UX

#### Evidencia (frontend)

La tabla tiene **Code · Name · Unit type · Actions**. El listado devuelve además `parent`, `functionalArea`, `costCenterCode`, `sortOrder` e `isActive` — **ninguno se muestra**.

#### Impacto

**Sin la columna de padre, la vista de lista no dice nada sobre la estructura.** Con 26 unidades es un catálogo plano. Y el área funcional y el centro de costo —los dos ejes con los que después se agrupa la planilla— son invisibles.

#### Contrato para el frontend

**No hace falta ninguna petición adicional.** La que ya se emite trae todo:

```
GET /v1/org-units?page=1&pageSize=10&includeAllowedActions=true
```

```jsonc
{ "items": [ {
    "publicId": "…",
    "code": "DEP-RAMPA",
    "name": "Rampa y Equipaje",
    "orgUnitType":    { "publicId": "…", "code": "DEPARTAMENTO", "name": "Departamento" },  // ya se muestra
    "functionalArea": { "publicId": "…", "code": "AEROPUERTOS",  "name": "…" },  // ← columna nueva
    "parent":         { "publicId": "…", "code": "GER-AEROP",    "name": "…" },  // ← columna nueva
    "sortOrder": null,                  // ← columna opcional
    "costCenterCode": null,             // ← columna opcional
    "managerEmployeeId": null,
    "isActive": true,                   // ← columna nueva (necesaria para F-04)
    "concurrencyToken": "…",            // necesario para las acciones de estado
    "allowedActions": { "canEdit": true, "reasons": [] }
  } ],
  "page": 1, "pageSize": 10, "totalCount": 26, "totalPages": 3 }
```

> `description` **no** viene en el listado — es solo de detalle.

#### Ajuste pedido al frontend

Agregar **Parent**, **Functional area** y **Status**; `costCenterCode` y `sortOrder` como opcionales.

**No requiere cambio en el backend.**

---

### 🟡 F-07 — El árbol muestra las unidades inactivas sin distinguirlas

**Severidad:** Media · **Tipo:** Corrección de la información · **Medido**

#### Evidencia (frontend y contrato)

Se inactivó la hoja `DEP-TALENTO` y se volvió a consultar el árbol:

```
GET /v1/org-units?isActive=false   →  total=1   (DEP-TALENTO)
GET /v1/org-units?isActive=true    →  total=25
GET /v1/org-units/tree             →  26 nodos      ← sigue apareciendo
```

El árbol **no filtra por estado y no expone `isActive` en el nodo**, así que la unidad dada de baja se dibuja en el organigrama exactamente igual que las activas.

#### Impacto

**El organigrama afirma algo falso.** Una unidad inactiva aparece como parte de la estructura vigente, sin ninguna marca. Quien lo use para tomar una decisión —o lo exporte— está viendo una estructura que ya no existe.

Hoy no se nota porque la interfaz no permite inactivar (**F-04**). **En cuanto se corrija F-04, este defecto se vuelve visible de inmediato**, así que los dos hay que resolverlos juntos.

#### Contrato para el frontend

```
GET /v1/org-units/tree
```

El nodo del árbol **no trae `isActive`**. Pero el endpoint `graph` **sí lo trae**:

```jsonc
GET /v1/org-units/graph  →  200
{
  "nodes": [ { "publicId": "…", "label": "…",
               "orgUnitTypePublicId": "…", "orgUnitTypeCode": "GERENCIA",
               "orgUnitTypeName": "Gerencia",
               "isActive": true } ],          // ← aquí sí está
  "edges": [ { "fromPublicId": "…", "toPublicId": "…" } ]
}
```

**Verificado en vivo:** 26 nodos y 25 aristas, coherente con el árbol.

Tres salidas posibles, en orden de preferencia:

| Opción | Cómo |
|---|---|
| **A** | Cruzar el árbol con `GET /v1/org-units?isActive=false` y marcar las que aparezcan ahí |
| **B** | Construir la vista desde `graph`, que ya trae `isActive` por nodo |
| **C** | Pedir al backend que `tree` incluya `isActive` — **es la única que requiere cambio de servidor** |

#### Ajuste pedido al frontend

Marcar visualmente las inactivas en el árbol, u ofrecer un conmutador para ocultarlas. **Las opciones A y B no requieren cambio en el backend.**

---

### 🔵 F-08 — Sin validación de formato ni de longitud en cliente

**Severidad:** Baja *(el defecto es real; se marca bajo porque su arreglo es el mismo que el del Paso 2)* · **Tipo:** Divergencia cliente/servidor

Ningún input declara `maxlength`, y no hay validación del patrón del código. El validador del backend es **el mismo** que el del Paso 2.

#### Contrato para el frontend

```
POST /v1/org-units            PUT /v1/org-units/{publicId}   ← If-Match obligatorio
```

| Campo | Regla | Mensaje sugerido en cliente |
|---|---|---|
| `code` | `NotEmpty` · máx. **50** · `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$` sobre el valor **recortado** | «Debe empezar con letra o número y solo admite letras, números, guion y guion bajo» |
| `name` | `NotEmpty` · máx. **150** | «Máximo 150 caracteres» |
| `description` | máx. **500** | «Máximo 500 caracteres» |
| `sortOrder` | `>= 0` cuando viene | «No admite valores negativos» |

> **Trampa:** el formato se valida sobre el valor **recortado**, pero `MaximumLength` se mide sobre el **crudo**. Un código pegado con espacios da error de *longitud*, no de formato.

Si aun así llega el `400`, viene con la clave del campo:

```jsonc
"errors": { "code": ["Code format is invalid."], "name": ["'Name' must not be empty."] }
```

**Es el mismo hallazgo que [00002 / F-02](00002-UnitTypes.md#-f-02--sin-validación-de-formato-ni-de-longitud-en-cliente).** Debe resolverse una sola vez, en el componente de formulario compartido.

---

### ⚪ F-09 — `managerEmployeePublicId`: ausencia analizada y considerada correcta

**Severidad:** Informativo · **Tipo:** Cobertura de campos — **no es un defecto**

#### Evidencia (frontend)

El contrato acepta `managerEmployeePublicId` (jefe de la unidad) y lo devuelve como `managerEmployeeId`. El formulario no lo ofrece.

#### Por qué su ausencia es correcta hoy

**No hay a quién seleccionar.** Los expedientes de empleado son el **Paso 8** del asistente, el último. Un combo de jefes en el Paso 3 estaría permanentemente vacío durante toda la configuración inicial, que es exactamente el momento en que se usa esta pantalla.

Poner un control que no puede tener contenido sería peor que no ponerlo: sugiere que falta un dato cuando en realidad todavía no existe.

#### Lo que sí hay que asegurar

Que sea una **decisión registrada y no un olvido**. Cuando el Paso 8 esté cubierto, esta pantalla necesitará el control — si no, no habrá forma de nombrar al responsable de una unidad desde ninguna parte.

#### Contrato para el frontend — para cuando llegue el momento

```jsonc
PUT /v1/org-units/{publicId}     If-Match: "{concurrencyToken}"
{ "…": "…", "managerEmployeePublicId": "…" }   // Guid? · expediente de la misma empresa
```

Vuelve como:

```jsonc
"managerEmployeeId": null    // ⚠️ en la respuesta pierde el "Public" del nombre
```

El combo se alimentaría de `GET /v1/personnel-files?…` *(endpoint por confirmar en la corrida del Paso 8)*.

#### Ajuste pedido al frontend

**Ninguno ahora.** Anotarlo en el backlog de la pantalla con dependencia explícita del Paso 8.

---

### ⚪ F-10 — El paso desbloquea dos rutas a la vez

**Severidad:** Informativo

Al completar el Paso 3, el asistente desbloqueó **el Paso 4 (Work center types)** y **el Paso 6 (Job profiles)**, y propone continuar por el 4.

Es coherente con las dependencias declaradas. **Se anota porque cambia el orden de las corridas siguientes**: hay dos ramas paralelas y conviene decidir cuál se prueba primero.

#### Contrato para el frontend

**Ninguno.** Es una observación de planificación de las pruebas, no un defecto.

---

## 7. Hallazgos de backend

> ⚠️ **Hay hallazgos de esta pantalla que NO arregla el backend.** El proxy que traduce `/v1/...` vive fuera del repositorio `CLARIHR-backend`; su índice accionable, con qué medir y cómo verificarlo, está en [**00900 · Proxy / BFF**](00900-ProxyBFF.md).


> El **detalle completo vive en el documento espejo** [`ComentariosPruebasBackend/00003-OrgUnits`](../ComentariosPruebasBackend/00003-OrgUnits.md).

| ID | Sev. | Hallazgo | Origen | Alcance |
|---|---|---|---|---|
| [**B-01**](../ComentariosPruebasBackend/00003-OrgUnits.md#2-b-01--diagram-export-devuelve-500-con-su-formato-por-defecto) | 🔴 Alta | **`/diagram-export` devuelve `500` con su formato por defecto** — ⏸️ **reclasificado al BFF**: el backend quedó descartado midiendo. Ver [00900 · Proxy / BFF](00900-ProxyBFF.md) | §5.7 | **Otro repositorio** |
| [**B-02**](../ComentariosPruebasBackend/00003-OrgUnits.md#3-b-02--move-no-valida-la-altura-del-subárbol-el-árbol-supera-el-límite-que-el-propio-servidor-declara) | 🔴 Alta | 🟢 **Resuelto 2026-08-16.** `move` ya mide **el nodo más la altura de su subárbol**. ⚠️ **Cambia comportamiento observable**: movimientos que antes devolvían `200` ahora dan `409` — ver §2.5 regla 5 | Empresa desechable | Un endpoint |
| [**B-03**](../ComentariosPruebasBackend/00003-OrgUnits.md#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar) | 🟡 Media | 🟢 **Resuelto 2026-08-16.** El token emitía siempre un claim de idioma que tapaba `Accept-Language`. **Los mensajes ya llegan en español** — verificado por el cable en el Paso 2 | §2.6, §5.4 · re-diagnosticado | **Transversal** |
| [**B-04**](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca) | 🟡 Media | 🟢 **Resuelto 2026-08-16 y ampliado el 2026-08-21.** `/usage` + `DELETE` condicional en **5 recursos**, no 2: tipos de unidad, unidades, tipos de centro de trabajo, centros de trabajo y áreas funcionales. **Capacidad nueva para esta pantalla** — §2.7 | Revisión de contrato | **5 recursos** |
| [**B-05**](../ComentariosPruebasBackend/00003-OrgUnits.md#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500) | 🟡 Media | El espacio `/v1` responde `200` con HTML a rutas inexistentes y `500` a un `PATCH` sobre el nombre real del recurso | Sondas de ruta | **Transversal** |

### Mediciones que esta corrida aporta a hallazgos anteriores

| Hallazgo | Qué se midió | Efecto |
|---|---|---|
| [**00001 / B-03**](../ComentariosPruebasBackend/00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location) | `Content-Disposition` **también se descarta** · pero el frontend fija el nombre de archivo por su cuenta | Pregunta cerrada. **Sin síntoma visible hoy** — el impacto es latente, no actual |
| [**00002 / F-01**](00002-UnitTypes.md#-f-01--el-buscador-envía-search-y-la-api-espera-q-no-filtra-nada) | **Esta pantalla manda `q=` correctamente** | El defecto del Paso 2 es una **inconsistencia entre pestañas**, no desconocimiento. Hay implementación de referencia en el mismo código |
| [**00002 / B-02**](../ComentariosPruebasBackend/00002-UnitTypes.md#3-b-02--la-clave-del-error-de-búsqueda-es-search-pero-el-parámetro-público-es-q) | El desajuste `search` / `q` **se repite en `OrgUnits`** | Dejó de ser «un endpoint»: era un patrón. 🟢 **Cerrado en los 45 endpoints el 2026-08-21** — el listado de unidades organizativas era uno de los 5 últimos. La clave del error de búsqueda es hoy `q` en todo el producto |

---

## 8. Qué NO se probó — y cómo se va a medir

Tras la ronda de medición, **queda un solo pendiente**, y con montaje definido.

### Medido en esta corrida

| Escenario | Resultado | Dónde |
|---|---|---|
| `422 ORG_UNIT_COST_CENTER_INVALID` | ✅ `422` con `costCenterCode` inexistente · **no creó nada** | §5.4 |
| Reactivar una unidad | ✅ Ciclo completo en una hoja, restaurada al final | §5.5 |
| Buscador general | ✅ **Funciona**: `q=GERENCIA` filtra de 26 a 12 | §5.6 |
| Filtros por columna | ✅ Medido — **están rotos** | **F-02** |
| Vista `graph` | ✅ `200`, 26 nodos y 25 aristas, con `isActive` por nodo | **F-07** |
| Vista `diagram-export` | ✅ Medido — **`500` con el formato por defecto** | **B-01** |
| Nombre del archivo descargado | ✅ El frontend lo fija (`download="org-units.csv"`) · sin descargar nada | §5.7 |
| Rate limit del export | ✅ `429 common.too_many_requests` a las ~8 peticiones seguidas | §5.7 |

### Pendiente, con plan

| Escenario | Cómo se va a medir | Cuándo |
|---|---|---|
| `409 ORG_UNIT_DEPTH_LIMIT_EXCEEDED` | Cadena de 16 niveles en una **empresa desechable** — 12 unidades temporales ensuciarían la empresa del escenario, y los catálogos no tienen borrado físico | Corrida dedicada, **pendiente de autorización** para crear la empresa |
| Manejo de `403` | Rol sin `OrgUnits.Admin` asignado a un segundo usuario, en la **misma empresa desechable** | La misma corrida |

> Los dos comparten montaje: **una empresa desechable**. No son excepciones a la regla de medirlo todo — son una tarea con fecha, que espera el visto bueno para crear la empresa.

### Sin superficie que probar

| Endpoint | Por qué |
|---|---|
| `PATCH` de JSON Patch | El frontend usa `PUT`. El endpoint existe, pero ninguna pantalla lo consume |


---

## 9. Estado en que queda la empresa

**`End to End SAS` queda con el organigrama AVIANCA operativo, y en el mismo estado en que lo dejó la carga**: todas las mediciones de esta ronda fueron reversibles o no persistentes.

| Medición | Efecto sobre los datos |
|---|---|
| Sonda `422` de centro de costo | Ninguno — el `422` rechaza antes de persistir. Verificado: `totalCount` siguió en 26 |
| Inactivar / reactivar `DEP-TALENTO` | Ninguno — restaurada a activa al terminar |
| `graph`, `tree`, exports, búsquedas | Solo lectura |
| Nombre de archivo | Interceptado en el navegador · **no se descargó ningún archivo** |

### Organigrama — 26 unidades, 4 niveles, una sola raíz

```
DG  Dirección General                          [DIRECCION_GENERAL · FINANZAS]
├── VP-OPS   Vicepresidencia de Operaciones    [VICEPRESIDENCIA · OPS_VUELO]
│   ├── GER-VUELO   Gerencia de Operaciones de Vuelo   [GERENCIA · OPS_VUELO]
│   │   ├── JEF-PILOTOS    Jefatura de Pilotos          [JEFATURA · OPS_VUELO]
│   │   └── DEP-DESPACHO   Despacho y Control de Vuelo  [DEPARTAMENTO · OPS_VUELO]
│   ├── GER-ABORDO  Gerencia de Servicio a Bordo        [GERENCIA · SERV_ABORDO]
│   └── GER-AEROP   Gerencia de Aeropuertos             [GERENCIA · AEROPUERTOS]
│       ├── DEP-RAMPA      Rampa y Equipaje             [DEPARTAMENTO · AEROPUERTOS]
│       └── DEP-COUNTER    Counter y Sala de Abordaje   [DEPARTAMENTO · AEROPUERTOS]
├── VP-TEC   Vicepresidencia Técnica           [VICEPRESIDENCIA · MANTENIMIENTO]
│   ├── GER-MTTO    Gerencia de Mantenimiento en Línea  [GERENCIA · MANTENIMIENTO]
│   ├── GER-ING     Gerencia de Ingeniería y Planeación [GERENCIA · MANTENIMIENTO]
│   └── DEP-ALMACEN Almacén Técnico                     [DEPARTAMENTO · MANTENIMIENTO]
├── VP-COM   Vicepresidencia Comercial         [VICEPRESIDENCIA · COMERCIAL]
│   ├── GER-VENTAS  Gerencia de Ventas                  [GERENCIA · COMERCIAL]
│   └── GER-CARGA   Gerencia de Carga                   [GERENCIA · CARGA]
├── VP-FIN   Vicepresidencia de Finanzas y Adm.[VICEPRESIDENCIA · FINANZAS]
│   ├── DEP-CONTA   Contabilidad                        [DEPARTAMENTO · FINANZAS]
│   ├── DEP-TESO    Tesorería                           [DEPARTAMENTO · FINANZAS]
│   └── DEP-COMPRAS Compras                             [DEPARTAMENTO · FINANZAS]
├── DIR-GENTE Dirección de Gente y Cultura     [DIRECCION · GENTE]
│   ├── DEP-ADMPER  Administración de Personal          [DEPARTAMENTO · GENTE]
│   └── DEP-TALENTO Atracción de Talento                [DEPARTAMENTO · GENTE]
├── DIR-SEG   Dirección de Seguridad Operacional [DIRECCION · SEG_OPERACIONAL]
├── DIR-LEGAL Dirección Legal                  [DIRECCION · LEGAL]
└── GER-TI    Gerencia de Tecnología           [GERENCIA · TECNOLOGIA]
```

**Las 26 activas. Todas con su área funcional asignada** — cargada por API, porque el formulario no lo permite (**F-01**).

**Sin `sortOrder`**: no se pudo fijar desde la interfaz (**F-03**), así que los hermanos se ordenan por el criterio por defecto.

**Sin centro de costo**: el catálogo (guía §6) todavía no existe.

### Verificación en el asistente

`/setup` pasó de **2/8** a **3/8 complete**. El Paso 3 quedó *Completed*, y se desbloquearon **dos** rutas: Paso 4 (Work center types) y Paso 6 (Job profiles).

---

## 10. Prioridad sugerida

### Frontend

| # | Hallazgo | Depende de backend | Esfuerzo |
|---|---|---|---|
| 1 | **F-02** — filtros por columna que ocultan registros | No | Bajo (retirarlos) / Medio (mapear a `q`) |
| 2 | **F-01** — combo de área funcional en crear y editar | No | Bajo |
| 3 | **F-05** — refrescar la lista tras crear | No | Muy bajo |
| 4 | **F-03** — campo `sortOrder` | No | Muy bajo |
| 5 | **F-04** + **F-07** — acciones de estado, columna de estado y marcar inactivas en el árbol | No (opciones A y B) | Medio |
| 6 | **F-06** — columnas de padre, área funcional y estado | No | Bajo |
| 7 | **F-08** — `maxlength` y patrón del código | Junto con **00002 / F-02** | Bajo |

> **F-04 y F-07 van juntos.** Hoy el árbol no distingue inactivas porque no hay forma de crear una desde la interfaz. En cuanto se añadan las acciones de estado, el organigrama empezaría a mostrar unidades dadas de baja como si estuvieran vigentes.

### Backend

| # | Hallazgo | Cambio | Estado |
|---|---|---|---|
| 1 | **B-01** | `/diagram-export` revienta con su propio formato por defecto | 🔲 Propuesto |
| 2 | **B-02** | Localizar los mensajes de `OrgUnits` | 🔲 Propuesto |
| 3 | **00001 / B-03** | Tres cabeceras confirmadas en tres módulos — aunque sin síntoma visible hoy | 🔲 Propuesto |

---

## 11. Para reintegrar y publicar — revisión de esta pantalla

> **Cómo leer esta lista.** No sabemos qué tiene hoy el cliente, así que **no está escrita como «cambios»
> sino como comprobaciones**. Si ya coincide, no hay nada que tocar.

### 11.1 Lo único que cambia de comportamiento: `PATCH /move`

**El resto del contrato de esta pantalla es idéntico.** Rutas, verbos, cuerpos, claves de error y códigos
son los mismos que se probaron el 2026-08-15.

| | Antes | Ahora (desde 2026-08-16) |
|---|---|---|
| Qué mide el guard de profundidad | **solo el nodo movido** | **el nodo más la altura de su subárbol** |
| Mover una rama de 3 niveles bajo el nivel 14 | `200` — y el árbol quedaba en profundidad **17** | `409 ORG_UNIT_DEPTH_LIMIT_EXCEEDED` |

**No es una regresión: es el guard haciendo lo que decía.** Antes el servidor aceptaba movimientos que
dejaban el árbol por encima de su propio límite declarado.

**Qué revisar en el cliente:**

1. **El mensaje del `409` debe hablar del subárbol**, no del nodo. «Mover esta rama dejaría unidades por
   debajo del nivel 15» explica el rechazo; «esta unidad supera la profundidad máxima» no, porque puede
   que la unidad movida quede en el nivel 8 y el problema esté tres niveles más abajo.
2. **Si el cliente precalcula la validez del arrastre** para habilitar o deshabilitar un destino, ese
   cálculo debe medir la altura del subárbol. Si solo miraba la profundidad del nodo, ahora permitirá
   soltar en destinos que el servidor rechazará.
3. ⚠️ **Puede haber árboles ya inválidos.** La empresa desechable de esta corrida quedó con **profundidad
   16** a propósito, como evidencia del defecto. Una unidad que ya esté por encima del límite **no se
   puede reorganizar** hasta acortar su rama: el movimiento parte de un estado inválido y se rechaza. Es
   esperado, no un fallo nuevo.

### 11.2 Lo que mejoró solo, sin tocar el cliente

| # | Qué | Efecto visible |
|---|---|---|
| 1 | **Los mensajes llegan en español** (`00003 / B-03` resuelto) | El usuario deja de ver texto en inglés. **No hay nada que hacer**: si el cliente ya envía `Accept-Language`, funciona |
| 2 | La búsqueda **ignora acentos** | `q=direccion` encuentra «Dirección». El buscador de esta pantalla ya funciona (§5.6) y ahora es además más tolerante |

### 11.3 Capacidad nueva, opcional pero muy pertinente aquí

`GET /usage` + `DELETE` condicional (§2.7).

**Esta pantalla es la que levantó el hallazgo**, y el valor no está en el borrado sino en `/usage`:
permite pasar de *«no se puede inactivar porque tiene unidades hijas activas»* a *«la usan **2 unidades
hijas** y **5 perfiles de puesto**»*. **Sirve aunque no se implemente el borrado nunca**, solo para
explicar por qué falló una inactivación.

### 11.4 Un efecto nuevo que conviene advertir al usuario

Desde el 2026-08-18, **inactivar una unidad impide crear plazas nuevas** contra sus perfiles de puesto
(§2.5, regla 6). Las plazas existentes no se tocan.

Si se implementa **F-04** (acciones de estado desde la interfaz), el diálogo de confirmación debería
mencionarlo. Sin eso, el usuario da de baja una unidad aquí y descubre el efecto dos pantallas más
adelante, con un `422` que no menciona la unidad que él mismo inactivó.

### 11.5 Lo que NO cambió y no hay que tocar

- Los doce endpoints originales, sus verbos y su autorización.
- El cuerpo de `POST`/`PUT` y los nueve campos, incluidos los tres que faltan en el formulario
  (`functionalAreaPublicId`, `sortOrder`, `managerEmployeePublicId`).
- `PUT` **no** acepta `parentPublicId`: el padre se cambia con `PATCH /move`.
- El mínimo de 2 caracteres en la búsqueda.
- `If-Match` obligatorio en `PUT`, `move`, `activate` e `inactivate`, tomado **del cuerpo** y no del `ETag`.
- El listado **no trae `description`**.
- **`/diagram-export` sigue devolviendo `500`** en 2 de sus 3 formatos: el backend quedó descartado
  midiendo y el hallazgo vive en [00900 · Proxy / BFF](00900-ProxyBFF.md). **No se ha arreglado.**

### 11.6 Orden sugerido para volver a probar de cero

1. **§11.1 primero**: si el cliente precalcula la validez de un arrastre, hay que ajustar el cálculo o el
   usuario verá rechazos que su interfaz decía permitir.
2. **F-02** (el filtro por columna que oculta registros) es el hallazgo de mayor impacto: hoy la pantalla
   **oculta unidades que existen**.
3. **F-01 y F-03** (área funcional y `sortOrder` ausentes del formulario): el organigrama se ordena mal
   sin el segundo, y el primero deja un campo del contrato sin superficie.
4. **F-04** con `/usage` y la advertencia de §11.4.
5. El resto (F-05…F-08) después.
6. **F-09 y F-10 no son defectos**: son ausencias analizadas y consideradas correctas.

> ⚠️ **Este documento no se ha vuelto a probar contra el ambiente.** Lo revalidado el 2026-08-21 es **el
> contrato del servidor**: rutas, verbos, esquemas, reglas y códigos de error, leídos del código y del
> contrato publicado. Los hallazgos F-01…F-10 siguen tal como se observaron el 2026-08-15: no se han
> añadido ni retirado, porque no se ha repetido la corrida.
