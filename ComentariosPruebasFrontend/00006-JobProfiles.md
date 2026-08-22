# 00006 — JobProfiles · Hallazgos de frontend

| | |
|---|---|
| **ID** | 00006-JobProfiles |
| **Documento espejo** | [`ComentariosPruebasBackend/00006-JobProfiles`](../ComentariosPruebasBackend/00006-JobProfiles.md) |
| **Paso probado** | **Paso 6 de 8** — *Job profiles* |
| **Pantalla** | `/job-profiles` · formulario en `/job-profiles/new` · detalle con **10 pestañas** |
| **Fecha** | 2026-08-15 |
| **Ambiente** | `https://dashboard.clarihr.com` · empresa `End to End SAS` |

---

## 1. Resumen ejecutivo

🔴 **La configuración guiada se rompe aquí, y no se nota hasta el paso siguiente.** El asistente marca el Paso 6 como completado con los 32 perfiles en `Draft`, y abre el Paso 7. **Pero una plaza no se puede crear contra un perfil sin publicar** (`422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED`, verificado), **y publicar es imposible desde el navegador** por dos causas independientes que se suman (**F-01** y [00006 / B-01](../ComentariosPruebasBackend/00006-JobProfiles.md#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy)).

🔴 **Dos campos del formulario tiran lo que el usuario escribe.** *Benefits summary* y *Working conditions summary* no existen en el contrato. Se rellenan, se guardan sin error, y el detalle los muestra vacíos (**F-02**). Probado con marcadores rastreables.

🔴 **El combo de unidad organizativa —campo obligatorio— tiene techo de 100 y no busca por código.** Carga `PageSize=100`, que es el máximo del servidor, y filtra en cliente sobre el nombre. Buscar `DG` no encuentra nada; `Direcci` encuentra cuatro (**F-03**).

🟡 **El error del servidor se muestra al usuario tal cual**, con rutas internas del API en el mensaje (**F-05**).

🟢 **La máquina de estados está bien diseñada** —`Draft` → `Published` congela el descriptor, con reapertura y archivado— y **el modal de confirmación al publicar es correcto**: a diferencia de una baja lógica reversible, publicar congela y merece confirmación.

🟢 **Los 32 perfiles del escenario AVIANCA quedaron cargados**, con su unidad y su categoría. El asistente pasó de `5/8` a **`6/8`**.

**Frontend** (§5)

| Severidad | Cantidad | IDs |
|---|---|---|
| 🔴 Alta | 3 | F-01, F-02, F-03 |
| 🟡 Media | 3 | F-04, F-05, **F-07** |
| ⚪ Informativo | 1 | F-06 |

**Backend** (§6) — 2 hallazgos nuevos, **uno de ellos 🔴 Alta**.

---

## 2. La cadena de prerrequisitos que la guía no menciona

Antes de poder clasificar un solo perfil hay que llenar **cuatro catálogos**, y la guía de AVIANCA solo nombra uno de ellos.

```
position-function-types  ─┐
position-contract-types  ─┼→ position-category-classifications → position-categories ─┐
unit-types (✅ del Paso 2)─┘                                                           ├→ job-profiles
occupational-pyramid-levels ─────────────→ (matriz de competencias, NO el perfil) ─────┘
```

**Los cinco estaban vacíos** al entrar al paso, y el asistente marcaba el Paso 6 como disponible igualmente.

| Catálogo | ¿Lo menciona la guía? | Estado inicial |
|---|---|---|
| `position-function-types` | ❌ No | 0 |
| `position-contract-types` | ❌ No | 0 |
| `position-category-classifications` | ❌ No | 0 |
| `position-categories` | ✅ §7.2, con 5 filas | 0 |
| `occupational-pyramid-levels` | ✅ §7.1, con 7 filas | 0 |

> **El nivel ocupacional no es un campo del perfil.** La guía asigna un «Nivel» a cada uno de sus 32 puestos, pero `CreateJobProfileRequest` no lo acepta: el nivel vive en `JobProfileCompetencyResponse`, es decir en la **matriz de competencias**, que es otra pestaña. Los 7 niveles se cargaron porque la guía los pide, pero **no quedaron enlazados a ningún perfil**.

**Lo que se sembró para desbloquear la cadena**, marcado como inventado porque la guía no lo especifica:

| Catálogo | Valor | Origen |
|---|---|---|
| Tipo de función | `OPERATIVA` | ⚠️ **Inventado** |
| Tipo de contrato | `PERMANENTE` | ⚠️ **Inventado** |
| Clasificación | `GENERAL` (función `OPERATIVA` × contrato `PERMANENTE` × tipo de unidad `DEPARTAMENTO`) | ⚠️ **Inventada** |
| 5 categorías | `OPERATIVO_AEREO`, `TECNICO_AERONAUTICO`, `OPERATIVO_TIERRA`, `COMERCIAL`, `ADMINISTRATIVO` | ✅ Guía §7.2 |
| 7 niveles | `DIRECTIVO` … `APOYO` | ✅ Guía §7.1 |

Se eligió **una sola clasificación genérica** en lugar de cinco, para minimizar el dato fabricado: las cinco categorías de AVIANCA son familias de negocio y no se corresponden con ninguna combinación evidente de (función × contrato × tipo de unidad).

---

## 3. Contrato de la pantalla

### 3.1 Endpoints

```
GET    /v1/job-profiles                          listado paginado
GET    /v1/job-profiles/{publicId}               detalle
GET    /v1/job-profiles/catalog-manifest         manifiesto de catálogos
POST   /v1/job-profiles                          → 201  (nace en Draft)
PUT    /v1/job-profiles/{publicId}               If-Match obligatorio
PATCH  /v1/job-profiles/{publicId}               JSON Patch · solo en Draft

PATCH  /v1/job-profiles/{publicId}/publication   Draft → Published   ⚠️ NO ENRUTADO (B-01)
PATCH  /v1/job-profiles/{publicId}/reopening     Published → Draft   ⚠️ NO ENRUTADO (B-01)
PATCH  /v1/job-profiles/{publicId}/archival      → Archived          ⚠️ NO ENRUTADO (B-01)
```

> **`status` NO se puede modificar por el `PATCH` genérico.** Se retiró del contrato a propósito: era la puerta por la que cualquier administrador de perfiles podía publicar sin tener el permiso dedicado. **El frontend todavía la usa** — ver **F-01**.
>
> ✅ **Los tres endpoints de transición EXISTEN en el backend** — verificado en el contrato publicado el
> 2026-08-21, con esos nombres exactos. **El problema no es que falten: es que el proxy no los reenvía**
> ([00006 / B-01](../ComentariosPruebasBackend/00006-JobProfiles.md#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy)).
> Sigue **sin resolver**, y vive en otro repositorio: [00900 · Proxy / BFF](00900-ProxyBFF.md).
>
> **Consecuencia práctica que no ha cambiado:** publicar sigue siendo imposible desde el navegador, y sin
> publicar no se pueden crear plazas. El Paso 7 sigue bloqueado de hecho.

### 3.2 Cuerpo de `POST` / `PUT`

```jsonc
{
  "code": "P-DG",                                    // requerido
  "title": "Director General",                       // requerido
  "objective": "…",                                  // opcional
  "orgUnitPublicId": "…",                            // REQUERIDO
  "reportsToJobProfilePublicId": "…",                // opcional — otro perfil
  "positionCategoryPublicId": "…",                   // opcional
  "strategicObjectiveCatalogItemPublicId": "…",      // opcional — ❌ NO está en el formulario
  "assignedWorkEquipmentCatalogItemPublicId": "…",   // opcional — ❌ NO está en el formulario
  "responsibilityCatalogItemPublicId": "…",          // opcional — ❌ NO está en el formulario
  "decisionScope": "…", "assignedResources": "…", "responsibilities": "…",
  "marketSalaryReference": "…", "valuationNotes": "…",
  "effectiveFromUtc": null, "effectiveToUtc": null,  // ⚠️ SIGUEN siendo date-time — ver nota abajo
  "allowInlineCatalogCreate": false                  // permite crear catálogos al vuelo
}
```

> ⚠️ **`benefitsSummary` y `workingConditionSummary` NO EXISTEN en este contrato**, aunque el formulario los ofrezca. Ver **F-02**. **Verificado de nuevo el 2026-08-21: siguen sin existir.**

> ⚠️ **Las fechas de este recurso NO cambiaron, y conviene no asumir lo contrario.** El 2026-08-16 se
> renombraron `appointmentDateUtc` → `appointmentDate`, `effectiveFromUtc` → `effectiveFrom` y
> `effectiveToUtc` → `effectiveTo` **solo en el representante legal**
> ([00000 / B-02](../ComentariosPruebasBackend/00000-CreateCompany.md#3-b-02--tres-fechas-de-calendario-viajan-como-datetime-contra-la-convención-dateonly-del-propio-producto)).
>
> **Aquí siguen llamándose `effectiveFromUtc` / `effectiveToUtc` y siguen siendo `date-time`**, no `date`
> — verificado en el contrato publicado el 2026-08-21. Un cliente que aplique el renombrado a esta
> pantalla recibirá `400`.

### 3.3 Estados

| Estado | Qué permite |
|---|---|
| `Draft` | Editable. **No sirve para crear plazas.** |
| `Published` | **Descriptor congelado**: ni el núcleo ni sus 9 colecciones se pueden tocar (`422 JOB_PROFILE_STATE_RULE_VIOLATION`). Habilita la creación de plazas. La matriz de competencias **sí** sigue editable, a propósito |
| `Archived` | Fin de vida |

**Publicar exige:** el permiso dedicado `JobProfiles.Publish` (**`JobProfiles.Admin` NO lo implica**), `If-Match`, y un contenido mínimo: objetivo, responsabilidades, **al menos un requisito y al menos una función** (`422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`).

> Los requisitos y las funciones son **pestañas aparte** del detalle. El asistente no las menciona, así que un usuario que siga solo la ruta guiada llegará a publicar sin ellas.

### 3.4 Las 10 pestañas del detalle

```
General · Requirements · Functions · Relations · Competency Matrix · Training
Compensation · Benefits · Working Conditions · Dependent Positions
```

Nueve son colecciones con su propio controlador. **Esta corrida cubre solo la pestaña General**, que es la que el Paso 6 del asistente exige; las otras nueve quedan fuera de alcance y se anotan en §7.

---

## 4. Cobertura de campos

| # | Campo API | ¿En el formulario? | Estado |
|---|---|---|---|
| 1 | `code` | ✅ `jp-code` | 🟢 `aria-required` · sin `maxlength` |
| 2 | `title` | ✅ `jp-title` | 🟢 `aria-required` · sin `maxlength` |
| 3 | `objective` | ✅ | 🟢 |
| 4 | `orgUnitPublicId` | ✅ `jp-orgUnit` | 🔴 **F-03** |
| 5 | `reportsToJobProfilePublicId` | ✅ | 🟢 |
| 6 | `positionCategoryPublicId` | ✅ | 🟢 |
| 7–9 | Los tres `…CatalogItemPublicId` | ❌ **AUSENTES** | ⚪ **F-06** |
| 10–12 | `decisionScope`, `assignedResources`, `responsibilities` | ✅ | 🟢 Guardan bien |
| 13–14 | `marketSalaryReference`, `valuationNotes` | ✅ | 🟢 |
| 15–16 | `effectiveFromUtc`, `effectiveToUtc` | ✅ | 🟢 |
| — | **`benefitsSummary`** | ⚠️ **En el formulario, NO en el contrato** | 🔴 **F-02** |
| — | **`workingConditionSummary`** | ⚠️ **En el formulario, NO en el contrato** | 🔴 **F-02** |

---

## 5. Hallazgos de frontend

### 🔴 F-01 — El botón *Publish* llama a un endpoint que el backend cerró a propósito

**Severidad:** Alta · **Tipo:** Divergencia de contrato

#### Evidencia

Pulsar *Publish* en el detalle abre un modal de confirmación correcto. Al confirmar, la pantalla muestra:

> ❌ *One or more validation errors occurred. **Status cannot be patched. Use PATCH /job-profiles/{publicId}/publication, /reopening or /archival.***

El perfil sigue en `Draft`, versión `v0`. **La acción principal del módulo no funciona.**

#### Causa

El frontend intenta cambiar el estado enviando `status` por el `PATCH` genérico `/v1/job-profiles/{id}`. Ese campo **se retiró del contrato deliberadamente**, y el propio código lo documenta:

```csharp
// H-01: `status` was removed from the patch contract. Keeping it would advertise a field the applier
// now rejects, and it was the door through which any profile administrator could publish.
//                                          — JobProfilesController.cs:267
```

Fue un **arreglo de seguridad**: publicar exige el permiso dedicado `JobProfiles.Publish`, y el `PATCH` genérico lo saltaba. El backend cerró la puerta y **el frontend nunca se actualizó al endpoint nuevo**.

#### Impacto

**Corta la cadena de la configuración guiada.** Sin publicar no hay plazas, y sin plazas no hay expedientes — los Pasos 7 y 8. Verificado de punta a punta:

```
POST /v1/position-slots   (contra un perfil en Draft)
→ 422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED
   "The selected job profile is not published. Publish the job profile before creating…"
```

**Y el asistente no lo advierte**: marca el Paso 6 como completado y abre el Paso 7, donde no se puede crear nada.

> ⚠️ **Arreglar este botón no basta.** El endpoint correcto **tampoco es alcanzable** desde el navegador — ver [00006 / B-01](../ComentariosPruebasBackend/00006-JobProfiles.md#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy). Son dos defectos independientes sobre el mismo camino, y hay que corregir los dos.

#### Contrato para el frontend

```
PATCH /v1/job-profiles/{publicId}/publication   If-Match obligatorio · SIN cuerpo
PATCH /v1/job-profiles/{publicId}/reopening     If-Match obligatorio · { "reason": "…" }
PATCH /v1/job-profiles/{publicId}/archival      If-Match obligatorio
```

| Código | HTTP | Cuándo |
|---|---|---|
| `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` | `422` | Falta objetivo, responsabilidades, un requisito o una función |
| `JOB_PROFILE_STATE_RULE_VIOLATION` | `422` | La transición no aplica al estado actual |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado |
| — | `403` | Sin el permiso `JobProfiles.Publish` |

#### Ajuste pedido al frontend

Cambiar las tres acciones de estado a sus endpoints dedicados y **dejar de enviar `status` en el `PATCH`**. `reopening` además necesita un campo de motivo en el modal, que hoy no existe.

Conviene también **anticipar el `422` de contenido mínimo**: deshabilitar *Publish* mientras el perfil no tenga objetivo, responsabilidades, un requisito y una función, indicando cuál falta. El dato está en las propias pestañas.

---

### 🔴 F-02 — Dos campos del formulario descartan en silencio lo que el usuario escribe

**Severidad:** Alta · **Tipo:** Pérdida de datos

#### Evidencia — sonda rastreable

Se creó `P-DG` por la interfaz escribiendo marcadores identificables en los dos campos:

```
Benefits summary            → SONDA-BENEFICIOS-XYZ
Working conditions summary  → SONDA-CONDICIONES-XYZ
```

Guardado **sin error**. Leyendo el registro desde la API:

```js
JSON.stringify(perfil).includes('SONDA-BENEFICIOS-XYZ')   // → false
JSON.stringify(perfil).includes('SONDA-CONDICIONES-XYZ')  // → false
```

Y la vista de detalle lo confirma al usuario, aunque tarde:

```
Decision scope             Aprobaciones hasta USD 250,000 y representacion legal ante la DGAC.
Assigned resources         Vehiculo asignado, equipo de computo y telefono corporativo.
Responsibilities           Definir la estrategia local, garantizar el cumplimiento aeronautico…
Benefits summary           —          ← ❌
Working conditions summary —          ← ❌
```

**Todo lo demás se guardó. Solo esos dos desaparecieron.**

#### Causa probable

El contrato **no tiene** esos campos: no aparecen en `JobProfileMutationRequest` ni en ningún punto del API. Y el detalle tiene **pestañas dedicadas de *Benefits* y *Working Conditions***, con sus propios controladores (`JobProfileBenefitsController`, `JobProfileWorkingConditionsController`).

Lo más probable es que sean **restos de una versión anterior**, de antes de que esos datos se convirtieran en colecciones propias, y que nadie los quitara del formulario.

#### Impacto

**Es pérdida de datos silenciosa, que es la peor clase.** No hay error, el guardado responde bien, y el usuario solo puede notarlo si vuelve al detalle y compara. Con 32 perfiles, nadie compara.

Y el daño es doble: el usuario cree que ya documentó los beneficios del puesto, así que **no va a la pestaña que sí los guarda**.

#### Contrato para el frontend

**No hay endpoint que acepte esos campos en el perfil.** Los datos equivalentes viven en:

```
…/job-profiles/{publicId}/benefits            (pestaña Benefits)
…/job-profiles/{publicId}/working-conditions  (pestaña Working Conditions)
```

*(Rutas por confirmar: no se ejercitaron en esta corrida — §7.)*

#### Ajuste pedido al frontend

**Quitar los dos campos del formulario.** No hay nada que arreglar en el backend: el sitio correcto para ese dato ya existe y es otra pestaña. Si se quiere conservar la comodidad de escribirlo desde General, tendría que ser un resumen de solo lectura alimentado por esas colecciones.

---

### 🔴 F-03 — El combo de unidad organizativa tiene techo de 100 y no busca por código

**Severidad:** Alta · **Tipo:** Escala y usabilidad · Campo **obligatorio**

#### Evidencia — par diferencial

El combo carga una sola vez al montar y filtra en cliente. **No emite ninguna petición al escribir** (verificado con el registro de red limpio: cero peticiones al teclear).

Con las 26 unidades cargadas:

| Se escribe | Es | Resultado |
|---|---|---|
| `DG` | el **código** de Dirección General | ❌ **No results** |
| `Direcci` | parte del **nombre** | ✅ 4 coincidencias |

**Solo filtra por nombre.** Y las opciones se muestran únicamente con el nombre —«Dirección General», «Dirección Legal», «Dirección de Gente y Cultura»—, sin el código, a diferencia del resto de combos del producto, que muestran `CÓDIGO - Nombre`.

#### El techo de 100 es real

```
GET /v1/org-units?PageSize=100 → 200   (el combo pide exactamente esto)
GET /v1/org-units?PageSize=101 → 400   clave `pageSize`
```

**100 es el máximo del servidor.** El combo pide el máximo y filtra en cliente, así que **en una empresa con más de 100 unidades, las que caigan fuera de la primera página son inalcanzables desde un campo obligatorio** — sin ningún aviso de que la lista está truncada.

Hoy con 26 unidades no se manifiesta. Una aerolínea real lo supera sin esfuerzo.

#### ¿Debería buscar por código? Sí, y el escenario lo demuestra

La guía identifica cada perfil por el código de su unidad (`DG`, `VP-OPS`, `DEP-RAMPA`). Con 26 unidades cuyos nombres empiezan igual —cuatro «Dirección…», ocho «Gerencia…»—, **el código es el desambiguador**, y es justo lo que no se puede escribir.

#### Contrato para el frontend

```
GET /v1/org-units?Page=1&PageSize=100&q={texto}
```

**El servidor ya tiene búsqueda, y es más amplia de lo que este documento suponía.** Verificado en el
repositorio el 2026-08-21: `q` recorre **siete columnas**, con mínimo de 2 caracteres.

| Columna | Incluye el caso `DG` |
|---|---|
| Código de la unidad | ✅ **sí — es justo el que hoy falla** |
| Nombre de la unidad | ✅ |
| Código del tipo de unidad | ✅ |
| Nombre del tipo de unidad | ✅ |
| Código del área funcional | ✅ |
| Nombre del área funcional | ✅ |
| Nombre del padre | ✅ |

✅ **Y desde el 2026-08-21 la búsqueda ignora los acentos**: `q=direccion` encuentra «Dirección». El
filtrado en cliente de hoy **no hace eso** — obliga a teclear la tilde. Así que pasar a búsqueda de
servidor no solo quita el techo y hace buscable el código: también arregla un tercer problema que este
documento no llegó a medir.

**El combo tiene además cuatro filtros del servidor sin usar**: `isActive`, `orgUnitTypePublicId`,
`functionalAreaPublicId` y `parentPublicId`.

#### Ajuste pedido al frontend

Pasar a **búsqueda en servidor** con `q`, con rebote, en vez de cargar 100 y filtrar en cliente. Eso resuelve las dos mitades de una vez: quita el techo y hace buscable el código.

Y mostrar `CÓDIGO - Nombre` en la opción, como ya hacen los combos de tipo de unidad, tipo de centro y grupo de ubicación.

**No requiere cambio en el backend.**

---

### 🟡 F-04 — El combo se puede abrir antes de tener datos, y dice «No results»

**Severidad:** Media · **Tipo:** Estados de carga

#### Evidencia

Abriendo *Org. unit* inmediatamente después de cargar la página, el desplegable muestra **«No results»**. Esperando unos segundos y reabriéndolo, muestra las 26 unidades. La petición de carga (`PageSize=100`) tarda en resolverse y **el combo no distingue «todavía no sé» de «no hay nada»**.

Costó una medición equivocada durante esta misma corrida: se registró que el combo no encontraba `DG`, cuando en ese momento simplemente no había cargado.

#### Impacto

«No results» sobre un campo obligatorio es una afirmación falsa que **invita a abandonar**. El usuario concluye que no hay unidades —y en un producto donde el paso anterior pudo dejarlas vacías, es una conclusión creíble— en vez de esperar.

#### Contrato para el frontend

**No interviene ningún endpoint.**

#### Ajuste pedido al frontend

Estado de carga explícito mientras la petición está en vuelo, y «No results» solo cuando haya respuesta con cero elementos. Aplica a todos los combos con carga asíncrona, no solo a este.

---

### 🟡 F-05 — El error del servidor se muestra crudo, con rutas internas del API

**Severidad:** Media · **Tipo:** Mensajes de error

#### Evidencia

El fallo de **F-01** se presenta al usuario final así, literalmente:

> *One or more validation errors occurred. Status cannot be patched. Use PATCH /job-profiles/{publicId}/publication, /reopening or /archival.*

Es un mensaje escrito para quien programa el cliente, mostrado a quien administra recursos humanos. Menciona verbos HTTP, rutas y un marcador de plantilla (`{publicId}`).

#### Impacto

No le dice al usuario **qué hacer**, y expone la forma interna del API en la interfaz. Es además la única señal de que la publicación falló, así que el usuario se queda con un texto que no puede accionar.

#### Contrato para el frontend

El cuerpo del error trae `extensions.code`, que es lo que el cliente debe usar para elegir su propio mensaje. **El `detail` del servidor no está pensado para mostrarse tal cual** — y menos mientras siga en inglés ([00003 / B-03](../ComentariosPruebasBackend/00003-OrgUnits.md#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar)).

#### Ajuste pedido al frontend

Mostrar mensaje propio por código de error, y dejar el `detail` del servidor para el registro de diagnóstico, no para la pantalla.

---

### ⚪ F-06 — Tres referencias a catálogo ausentes del formulario: **correcto hoy**

**Severidad:** Informativo · **Tipo:** Cobertura de campos — **no es un defecto**

#### Evidencia

El contrato acepta `strategicObjectiveCatalogItemPublicId`, `assignedWorkEquipmentCatalogItemPublicId` y `responsibilityCatalogItemPublicId`. El formulario no los ofrece.

#### ¿Deberían estar? No, y hay tres razones

1. **Sus catálogos están vacíos** y no forman parte de la ruta guiada. Un combo permanentemente vacío es peor que su ausencia — es la misma conclusión que en [00003 / F-09](00003-OrgUnits.md).
2. **Hay campos de texto libre equivalentes ya presentes** —*Assigned resources*, *Responsibilities*— que sí guardan y que cubren la necesidad sin obligar a poblar un catálogo antes.
3. **El contrato tiene `allowInlineCatalogCreate`** para crear la entrada de catálogo al vuelo, lo que sugiere que el flujo con catálogo es el avanzado y no el de configuración inicial.

#### Contrato para el frontend

```jsonc
{ "strategicObjectiveCatalogItemPublicId": "…",
  "assignedWorkEquipmentCatalogItemPublicId": "…",
  "responsibilityCatalogItemPublicId": "…",
  "allowInlineCatalogCreate": true }
```

Los tres son `Guid?`. Con `allowInlineCatalogCreate: true` el servidor crea la entrada si no existe.

#### Ajuste pedido al frontend

**Ninguno.** Se documenta como decisión, con la condición de revisarla si algún día esos catálogos entran en la ruta guiada.

---

### 🟡 F-07 — El asistente declara completo un paso contando filas, no filas utilizables

> **Reclasificado desde [00006 / B-02](../ComentariosPruebasBackend/00006-JobProfiles.md#3-b-02--el-asistente-declara-disponible-un-paso-cuya-cadena-de-prerrequisitos-está-vacía) el 2026-08-21.** Se levantó como defecto de backend; la lógica de progreso **no vive en el servidor**.

#### Evidencia de dónde vive

| Búsqueda | Resultado |
|---|---|
| Rutas con `setup`, `progress`, `onboarding`, `step` o `wizard` en el contrato publicado | **0 de 568** |
| Archivos con `wizard`, `checklist` o `SetupProgress` en `src/` | **0** |
| Texto «Requires completing first» en el backend | **0 coincidencias** |

El asistente y su cálculo de progreso son del cliente. El servidor no sabe que existe.

#### El defecto

Al salir del Paso 6 se marca *Completed* con 32 perfiles **en `Draft`**, y abre el Paso 7. Pero:

```
POST /v1/position-slots  →  422 POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED
```

**El asistente no distingue «hay 32 filas» de «hay 32 filas utilizables».** Su valor entero es decirle a un
cliente nuevo qué hacer y en qué orden; marcar verde lo que no funciona es peor que no tenerlo, porque el
usuario descubre el problema dos pasos más tarde sin saber cuál de los dos falló.

#### Ajuste pedido al frontend

La condición de cada paso debe ser **la que el paso siguiente exige**, no la existencia de filas. El backend
ya responde ambas preguntas con **una llamada barata** —basta `totalCount`, no hacen falta las filas:

| Paso | Condición correcta | Llamada |
|---|---|---|
| 6 · Job profiles | ≥ 1 perfil **publicado** | `GET /v1/companies/{id}/job-profiles?status=Published&pageSize=1` → `totalCount` |
| 7 · Position slots | ≥ 1 plaza **vacante y vigente** | `GET /v1/companies/{id}/position-slots?status=Vacant&isActive=true&pageSize=1` → `totalCount` |

Y para *Available*, si la cadena de catálogos que el paso necesita está vacía, **decirlo con el mismo detalle
con que ya se dicen los bloqueos** («Requires completing first: Job profiles») — «Faltan categorías de puesto»,
con enlace a la pantalla. El mecanismo de dependencias ya existe en el asistente; lo que falla es la condición
que evalúa.

#### Lo que el backend hizo por este hallazgo

Nada que arreglar —los dos filtros ya funcionaban— pero sí **quedaron blindados** para que el arreglo del
frontend no se pueda romper desde el servidor: `ApiIntegrationTests.WizardReadiness` verifica que
`status=Published` cuente **1 de 3** cuando hay un publicado y dos borradores, y que el filtro de plazas siga
al **estado derivado** (no hay columna de estado: se calcula, así que el filtro podría dejar de llegar a la
base de datos sin que nada avisara). 2/2 en verde.

#### Alcance

**Los ocho pasos, de una vez.** La pregunta para cada uno: *¿la condición que declara completo es la que el
paso siguiente necesita?* La configuración de esta corrida pasaría de `6/8` a `5/8`, que es lo correcto.

---


## 6. Hallazgos de backend

> ⚠️ **Hay hallazgos de esta pantalla que NO arregla el backend.** El proxy que traduce `/v1/...` vive fuera del repositorio `CLARIHR-backend`; su índice accionable, con qué medir y cómo verificarlo, está en [**00900 · Proxy / BFF**](00900-ProxyBFF.md).


> El detalle vive en [`ComentariosPruebasBackend/00006-JobProfiles`](../ComentariosPruebasBackend/00006-JobProfiles.md).

| ID | Sev. | Hallazgo | Origen | Alcance |
|---|---|---|---|---|
| [**B-01**](../ComentariosPruebasBackend/00006-JobProfiles.md#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy) | 🔴 Alta | **Los tres endpoints de transición de estado no están enrutados**: sin cuerpo devuelven `200` con HTML, con cuerpo `500`. La publicación es inalcanzable desde el navegador | F-01 | Instancia grave de [00003 / B-05](../ComentariosPruebasBackend/00003-OrgUnits.md#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500) |
| [**B-02**](../ComentariosPruebasBackend/00006-JobProfiles.md#3-b-02--el-asistente-declara-disponible-un-paso-cuya-cadena-de-prerrequisitos-está-vacía) | 🟡 Media | ⛔ **Reclasificado a frontend el 2026-08-21 → [F-07](#-f-07--el-asistente-declara-completo-un-paso-contando-filas-no-filas-utilizables).** El análisis se sostiene entero, pero **el servicio de progreso no vive en el backend**: 0 rutas de asistente en las 568 del contrato publicado | §2, §1 | El cliente |

---

## 7. Qué NO se probó, y cómo se va a medir

| Pendiente | Montaje | Cuándo |
|---|---|---|
| **Las 9 pestañas de colección** (Requirements, Functions, Relations, Competency Matrix, Training, Compensation, Benefits, Working Conditions, Dependent Positions) | Cada una es un recurso con su controlador. **Quedan fuera del Paso 6**, que solo exige la pestaña General. Requirements y Functions **son obligatorias para publicar**, así que entran por necesidad al resolver F-01 | Al desbloquear la publicación |
| **La máquina de estados completa** | `publication` → `reopening` → `archival` con contenido mínimo. **Bloqueada por B-01**: los endpoints no son alcanzables | Tras B-01 |
| **`403` sin el permiso `JobProfiles.Publish`** | Segundo usuario con rol restringido. Este módulo es el mejor sitio para medirlo: tiene un permiso dedicado que `Admin` **no** implica | Próxima sesión |
| **El nivel ocupacional enlazado al perfil** | Vive en la matriz de competencias. Los 7 niveles están creados pero sin enlazar | Con la pestaña Competency Matrix |
| **Corregir la guía §7** | Añadir los tres catálogos que faltan en la cadena y aclarar que el «Nivel» no es un campo del perfil | Edición documental |

---

## 8. Estado al cerrar el paso

```
Setup progress: 6 / 8 complete
```

| | |
|---|---|
| Perfiles creados | **32**, todos en `Draft` |
| Catálogos sembrados | 5 categorías · 7 niveles ocupacionales · 3 registros de cadena (inventados) |
| Paso 7 · Position slots | 🟢 *Available* — **pero inoperable**: ninguna plaza se puede crear contra un perfil en `Draft` |
| Paso 8 · Personnel files | 🔒 Bloqueado |

> ⚠️ **La empresa NO quedó funcional en el sentido del escenario.** Los 32 perfiles existen y están bien formados, pero **ninguno es utilizable aguas abajo** hasta que se resuelvan **F-01** y **B-01**. Es la primera vez en la corrida que un paso se cierra sin dejar la empresa operativa, y no es por falta de datos sino por dos defectos.

---

## 9. Para reintegrar y publicar — revisión de esta pantalla

> **Cómo leer esta lista.** No sabemos qué tiene hoy el cliente, así que **no está escrita como «cambios»
> sino como comprobaciones**. Si ya coincide, no hay nada que tocar.
>
> ⚠️ **Esta es la única pantalla de la corrida que sigue bloqueada de hecho**, y el bloqueo **no es del
> backend**. Ver §9.1.

### 9.1 Lo que sigue sin resolverse, y por qué importa antes que todo lo demás

**Publicar sigue siendo imposible desde el navegador.** Los tres endpoints de transición —`publication`,
`reopening`, `archival`— **existen en el backend**, verificado en el contrato el 2026-08-21. **El proxy no
los reenvía** ([00006 / B-01](../ComentariosPruebasBackend/00006-JobProfiles.md#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy),
detalle en [00900 · Proxy / BFF](00900-ProxyBFF.md)).

**Sin publicar no hay plazas**, así que el Paso 7 sigue cerrado y los Pasos 7 y 8 siguen sin poder
probarse. **Esto se arregla en el repositorio del proxy, no aquí y no en el backend.**

Y hay una segunda causa que sí es de esta pantalla: **F-01** — el botón *Publish* llama al `PATCH`
genérico con `status`, una puerta que el backend cerró a propósito. **Aunque el proxy se arregle mañana,
el botón seguiría sin funcionar** hasta que apunte al endpoint dedicado.

> **Las dos causas son independientes y hay que resolver las dos.** Arreglar solo una deja la publicación
> igual de inalcanzable.

### 9.2 Una suposición que hay que evitar: **las fechas de esta pantalla NO cambiaron**

El 2026-08-16 se renombraron tres campos de fecha **solo en el representante legal** (`00000 / B-02`):
`effectiveFromUtc` → `effectiveFrom`, y de instante a día.

**Aquí no.** El perfil de puesto sigue con `effectiveFromUtc` / `effectiveToUtc`, y siguen siendo
`date-time`. Verificado en el contrato el 2026-08-21.

⚠️ **Un cliente que aplique el renombrado «a todas las fechas» recibirá `400` en esta pantalla.**

### 9.3 Lo que mejoró solo, sin tocar el cliente

| # | Qué | Efecto |
|---|---|---|
| 1 | Los mensajes de error llegan en español | **Afecta a F-05**: el error que hoy se muestra crudo ya viene traducido. Sigue habiendo que dejar de mostrar rutas internas |
| 2 | La búsqueda del combo de unidades **ignora acentos** | `q=direccion` encuentra «Dirección». **Refuerza F-03** |

### 9.4 Lo que hace F-03 más fácil de lo que parecía

El servidor **ya busca por código** — y por seis columnas más (§F-03). El combo solo tiene que dejar de
filtrar en cliente y empezar a enviar `q`.

Eso resuelve **tres** problemas de una vez:

1. El techo de 100 unidades.
2. Que `DG` no encuentre nada.
3. Que haya que teclear la tilde para encontrar «Dirección».

Y hay cuatro filtros del servidor sin aprovechar: `isActive`, `orgUnitTypePublicId`,
`functionalAreaPublicId`, `parentPublicId`.

### 9.5 Lo que NO cambió y no hay que tocar

- Los endpoints, sus verbos y su autorización.
- **`benefitsSummary` y `workingConditionSummary` siguen sin existir** en el contrato — F-02 sigue
  vigente, y sigue tirando en silencio lo que el usuario escribe.
- **`status` sigue fuera del `PATCH` genérico**: es deliberado, no un olvido.
- **Publicar sigue exigiendo `JobProfiles.Publish`**, que `JobProfiles.Admin` **no** implica.
- El contenido mínimo para publicar: objetivo, responsabilidades, **al menos un requisito y al menos una
  función** — y esas dos viven en pestañas que el asistente no menciona.
- **F-06 no es un defecto**: las tres referencias a catálogo ausentes se analizaron y su ausencia se
  consideró correcta hoy.

### 9.6 Orden sugerido para volver a probar de cero

1. **F-01 y el enrutado del proxy, en paralelo.** Son las dos causas de que la publicación no funcione, y
   **hasta que las dos estén, no se puede probar el Paso 7 ni el 8**. Es el desbloqueo de la corrida
   entera.
2. **F-02** (los dos campos que se tiran en silencio): el usuario cree que guardó algo que no existe.
3. **F-03** con búsqueda de servidor — §9.4.
4. **F-07** (el asistente cuenta filas, no filas utilizables): las dos llamadas exactas están en el propio
   hallazgo.
5. F-04 y F-05 después.

> ⚠️ **Este documento no se ha vuelto a probar contra el ambiente.** Lo revalidado el 2026-08-21 es **el
> contrato del servidor**: rutas, esquemas, permisos y las columnas que recorre la búsqueda, leídos del
> código y del contrato publicado. Los hallazgos F-01…F-07 siguen tal como se observaron: no se han
> añadido ni retirado, porque no se ha repetido la corrida.
