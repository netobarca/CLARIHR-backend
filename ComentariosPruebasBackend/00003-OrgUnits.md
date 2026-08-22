# 00003 — OrgUnits · Hallazgos de backend

| | |
|---|---|
| **ID** | 00003-OrgUnits |
| **Documento espejo** | [`ComentariosPruebasFrontend/00003-OrgUnits`](../ComentariosPruebasFrontend/00003-OrgUnits.md) |
| **Paso probado** | Configuración guiada (`/setup`) → **Paso 3: Org units** |
| **Pantalla que los destapó** | `/org-units` (pestaña *Units*) |
| **Fecha** | 2026-08-15 |
| **Ambiente** | Producción — `https://dashboard.clarihr.com` |

Hallazgos de **backend** detectados durante la prueba del Paso 3. El comportamiento del cliente y su ajuste están en el documento espejo; aquí va solo lo que se resuelve del lado del servidor.

---

## 1. Resumen

| ID | Sev. | Hallazgo | Componente | Origen | Alcance | Estado |
|---|---|---|---|---|---|---|
| [B-01](#2-b-01--diagram-export-devuelve-500-con-su-formato-por-defecto) | 🔴 Alta → **BFF** | `/diagram-export` devuelve `500` en 2 de sus 3 formatos, incluido el por defecto | API · `OrgUnitDiagramWriter` | §5.7 | Un endpoint · **revisar el gemelo de plazas** | ⏸️ Bloqueado (BFF) |
| [B-02](#3-b-02--move-no-valida-la-altura-del-subárbol-el-árbol-supera-el-límite-que-el-propio-servidor-declara) | 🔴 Alta | **`move` no valida la altura del subárbol**: el árbol llega a profundidad 17 con `MaxDepth = 15` | Application · handler de `move` | Empresa desechable | Un endpoint · **revisar toda jerarquía con reparentado** | 🟢 Resuelto |
| [B-03](#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar) | 🟡 Media | **Canal de localización que ningún cliente puede activar**: las traducciones existen y no se usan | Api · resolución de idioma | §2.6, §5.4 · re-diagnosticado en el Paso 4 | **Transversal** | 🟢 **Resuelto** (backend) |
| [B-04](#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca) | 🟡 Media | Sin `DELETE` condicional ni `/usage`: lo creado por error queda para siempre, y el rechazo «está en uso» no dice por quién | Api · ambos controladores | Revisión de contrato | **Dos recursos** · decidir la regla del producto | 🟢 Resuelto |
| [B-05](#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500) | 🟡 Media | El espacio `/v1` responde `200` con HTML a rutas inexistentes, y `500` a un `PATCH` sobre el nombre de ruta real del API | BFF · proxy de `/v1` | Sondas de ruta | **Transversal** a todo `/v1` | ⏸️ Bloqueado (BFF) |

**B-02 es el hallazgo que desbloqueó la empresa desechable**: el guard de profundidad existe, se prueba en la creación y **no se aplica al movimiento**. La medición dejó el árbol de esa empresa en profundidad 17 a propósito, como caso de regresión.

**B-04 nace de corregir una conclusión propia**: en el Paso 2 se dio por bueno que no hubiera borrado. El producto ya implementa el patrón seguro dos pestañas más allá (§4.2).

**B-01 no afecta al frontend hoy** —ninguna pantalla consume ese endpoint—, pero es un `500` en un endpoint público y documentado. **B-03 tampoco bloquea.**

### Mediciones que este paso aporta a hallazgos anteriores

| Hallazgo | Qué se midió | Efecto |
|---|---|---|
| [**00001 / B-03**](00001-CompanyLegalProfile.md#4-b-03--el-proxy-descarta-cabeceras-del-upstream-etag-y-location) | `Content-Disposition` **también se descarta** · **pero el frontend fija el nombre de archivo por su cuenta** (`download="org-units.csv"` sobre un blob) | Pregunta cerrada. Tres cabeceras en tres módulos, **ninguna con síntoma visible hoy**: el impacto es latente |
| [**00002 / B-02**](00002-UnitTypes.md#3-b-02--la-clave-del-error-de-búsqueda-es-search-pero-el-parámetro-público-es-q) | El desajuste `search` / `q` **se repite idéntico** en `OrgUnits` | Deja de ser «un endpoint»: es un patrón |

### Nota de contraste — lo que este módulo hace muy bien

Es el módulo más defendido de los tres probados:

- **Los cinco guards funcionan**: código duplicado (`409`), padre inexistente (`404`), ciclo (`409 CYCLE_DETECTED`), hijos activos (`409 HAS_ACTIVE_CHILDREN`) y centro de costo inválido (`422 COST_CENTER_INVALID`). Los cinco verificados en vivo, y el `422` **sin persistir nada**.
- **El diseño separa `PUT` de `move`**: la edición no acepta `parentPublicId`, así que un cambio de padre pasa obligatoriamente por el endpoint que valida ciclos y profundidad. No hay puerta trasera para corromper el árbol.
- **El buscador es potente y correcto**: `q` recorre código, nombre, nombre del tipo y nombre del padre — 6 columnas y 4 joins, con el mínimo de 2 caracteres que protege el `LIKE '%x%'`.
- **`graph` es un contrato limpio y completo**: `nodes` con `isActive` incluido, `edges` con origen y destino. Es la pieza que le falta a `tree` (ver **F-07** del espejo).
- **Los `400` traen la clave del campo**, igual que en el Paso 2.
- **Hay rate limit propio en los exports**, verificado: `429 common.too_many_requests` a las ~8 peticiones seguidas.

---

## 2. B-01 — `/diagram-export` devuelve `500` con su formato por defecto

| | |
|---|---|
| **Severidad** | 🔴 Alta |
| **Estado** | ⏸️ **Bloqueado — reclasificado al BFF el 2026-08-16.** El backend queda **descartado con evidencia** (§2.9) · índice accionable en [00900 / ProxyBFF](../ComentariosPruebasFrontend/00900-ProxyBFF.md) |
| **Componente** | ~~API · `OrganizationUnitsController.DiagramExport` / `OrgUnitDiagramWriter`~~ → **BFF · proxy de `/v1`** *(causa por atribuir, backend descartado)* |
| **Dueño** | **Quien mantiene el BFF** — no es deuda del backend · [00900 / ProxyBFF](../ComentariosPruebasFrontend/00900-ProxyBFF.md) |
| **Origen** | §5.7 del documento espejo |
| **Alcance** | Un endpoint · **revisar `PositionSlotsController` / `PositionSlotDiagramWriter`**, que son su gemelo |

### 2.1 Evidencia

Los tres formatos que el propio swagger declara (`graphml`, `json`, `dot`), medidos uno a uno con pausa entre ellos para no chocar con el rate limit:

| Petición | Resultado |
|---|---|
| `GET /v1/org-units/diagram-export` *(sin `format` → default `graphml`)* | 🔴 **`500 Internal server error`** |
| `GET /v1/org-units/diagram-export?format=graphml` | 🔴 **`500 Internal server error`** |
| `GET /v1/org-units/diagram-export?format=dot` | 🔴 **`500 Internal server error`** |
| `GET /v1/org-units/diagram-export?format=json` | 🟢 `200` · 8011 bytes |
| `GET /v1/org-units/diagram-export?format=svg` | 🟢 `400 REPORT_FORMAT_NOT_SUPPORTED` |
| `GET /v1/org-units/diagram-export?format=png\|pdf\|mermaid` | 🟢 `400 REPORT_FORMAT_NOT_SUPPORTED` |

El `500` **no trae `code`**, solo `title: "Internal server error"` — es una excepción no controlada, no un error de dominio.

**Dato revelador:** un formato **inválido** se comporta mejor que el formato **por defecto**. Quien pruebe el endpoint con `?format=svg` recibe un `400` correcto y pensará que funciona; quien lo llame sin parámetros —que es el uso natural— recibe un `500`.

### 2.2 Causa

No verificada en ejecución; lo que la medición **sí acota**:

- **Falla en el formateo, no en la consulta.** `GET /graph` responde `200` con los mismos 26 nodos y 25 aristas, y `diagram-export?format=json` también responde `200`. La consulta `GetOrgUnitGraphQuery` es común a los tres formatos y funciona.
- **Falla exactamente en las dos ramas que no usan `JsonSerializer`**: `WriteGraphMl` (vía `XmlWriter`) y `WriteDot`. La rama `WriteJson` es la única sana.

Eso deja el fallo dentro de `OrgUnitDiagramWriter`, en las dos rutas de serialización no-JSON.

Un candidato a revisar primero, en `WriteGraphMl`:

```csharp
writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
```

Declarar un prefijo de espacio de nombres con `WriteAttributeString` y `ns = null` es una de las formas de `XmlWriter` que lanza según el estado del escritor. **Es una hipótesis, no un diagnóstico**: hay que reproducirlo en una prueba unitaria del writer, que para eso se extrajo como clase pura y sin estado (lo dice su propio comentario: *«unit-testable in isolation»*).

Para `WriteDot` no hay hipótesis: hace falta leer el método.

### 2.3 Impacto

**Hoy, para el usuario: ninguno.** El frontend no consume `diagram-export` — usa `export` (CSV/Excel), que funciona.

**Para el producto:**

1. **Es un `500` en un endpoint público y documentado en el swagger.** Cualquier integrador que lo llame como indica la documentación —sin `format`, tomando el valor por defecto— se topa con un error de servidor.
2. **Bloquea la funcionalidad antes de que exista.** Si mañana el frontend quiere ofrecer «descargar el organigrama», dos de los tres formatos no están disponibles.
3. **Ensucia la telemetría.** Un `500` no controlado entra en las alertas de error del servicio.

### 2.4 Propuesta

1. **Reproducir en una prueba unitaria** de `OrgUnitDiagramWriter` con el grafo real de 26 nodos —lo permite el diseño de la clase—, para capturar la excepción concreta en lugar de conjeturar.
2. **Arreglar `WriteGraphMl` y `WriteDot`.**
3. **Añadir una prueba por formato** que cubra los tres, no solo el que hoy pasa. Que dos de tres estén rotos sugiere que la cobertura actual solo ejercita `json`.
4. **Revisar el gemelo**: `PositionSlotDiagramWriter` tiene los mismos tres métodos y el mismo controlador de despacho por formato. Si el defecto está en el patrón compartido, está roto igual — y esa pantalla llega en el **Paso 7**.

### 2.5 Compatibilidad

**No rompe nada: hoy está roto.** Arreglarlo solo puede mejorar la respuesta. No cambia la forma del contrato ni afecta `openapi.yaml`.

### 2.6 Alcance a revisar

**`PositionSlotsController` + `PositionSlotDiagramWriter`**, que exponen `WriteGraphMl` / `WriteJson` / `WriteDot` con la misma estructura. Conviene medirlo **antes** del Paso 7 para saber si es un defecto o dos.

### 2.7 Vía alterna vigente

Para quien necesite el diagrama hoy: **`?format=json` funciona**, y `GET /v1/org-units/graph` devuelve la misma estructura (`nodes` + `edges`) como JSON directo, sin pasar por el export ni por su rate limit.

### 2.9 Por qué se descarta el backend — medido el 2026-08-16

§2.2 apuntaba a `WriteGraphMl` y `WriteDot` como sospechosos, con una hipótesis explícita sobre `WriteAttributeString`. **La hipótesis era falsa y el backend no tiene el defecto.**

**Lo que se comprobó:**

| Comprobación | Resultado |
|---|---|
| `git log` de `OrgUnitDiagramWriter.cs` y del controlador | Sin cambios desde el **7 de junio** — el código local **es** el desplegado |
| Cobertura existente: los tres formatos, 2 nodos ASCII | 🟢 ya pasaba |
| **Prueba nueva**: los tres formatos con **26 unidades acentuadas**, `&` y `<>` | 🟢 **pasa** |
| **Sin `format`** — el caso exacto que se midió como `500` | 🟢 **pasa** |
| Caracteres no-ASCII como causa | ❌ **descartado midiendo**: GraphML y DOT los manejan bien |

> **Nota de método.** El primer intento de reproducir esto con acentos «falló» y estuvo a punto de darse por confirmado. **El fallo era de la aserción, no del código**: comparaba contra la cadena JSON cruda, y `JsonSerializer` escapa lo no-ASCII como `\uXXXX` por diseño. Corregida para comparar el valor deserializado.

**El hueco de cobertura sí era real y queda cerrado.** La prueba existente usaba 2 nodos ASCII; el defecto se midió con 26 acentuados. Es el mecanismo que en este repositorio ya costó cuatro hallazgos con cobertura que nunca probó la condición que importaba.

#### Lo que queda por atribuir

**No está probado que la causa sea el proxy.** Es lo coherente —el `500` **no traía `code`**, o sea excepción no controlada, y `?format=json` pasaba mientras los otros dos reventaban, que es la firma de [B-05](#6-b-05--el-espacio-v1-no-se-comporta-como-un-espacio-de-api-una-ruta-inexistente-responde-200-con-html-y-un-patch-válido-responde-500)— pero coherencia no es prueba.

**La medición que lo cierra**, pendiente de sesión en el ambiente desplegado: pedir los tres formatos **a través del proxy** y compararlos con la misma petición **directa al API**. Si el directo responde `200` y el proxy `500`, queda atribuido.

### 2.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Detectado al medir los pendientes del Paso 3. `graphml` y `dot` → `500`; `json` → `200` |

---

## 3. B-02 — `move` no valida la altura del subárbol: el árbol supera el límite que el propio servidor declara

| | |
|---|---|
| **Severidad** | 🔴 Alta |
| **Estado** | 🟢 **Resuelto** — 2026-08-16, verificado (§3.9) |
| **Componente** | Application · `MoveOrgUnitCommandHandler` y `OrgUnitHierarchyBuilder` |
| **Origen** | Montaje de empresa desechable (§5.2) |
| **Alcance** | Un endpoint · **revisar todo árbol con `move`** |

**El guard de profundidad de `move` mide el nodo que se mueve, no lo que cuelga de él.** Mover un subárbol de tres niveles bajo un padre que ya está en el nivel 14 devuelve `200` y deja el árbol en profundidad **17**, con `MaxDepth = 15`.

### 3.1 Evidencia

`POST` **sí** respeta el límite. Cadena de unidades encadenadas una bajo otra, en empresa recién creada:

```
N01 … N15   → 201  (15 niveles)
N16         → 409  ORG_UNIT_DEPTH_LIMIT_EXCEEDED
                   "The requested hierarchy depth exceeds the maximum supported levels."
```

> Semántica confirmada de paso: **`MaxDepth = 15` son 15 niveles TOTALES contando la raíz**, no 15 por debajo de ella.

`PATCH /move` **no**. Con un subárbol aparte de tres niveles (`M01 → M02 → M03`):

```
PATCH /v1/org-units/{M01}/move        If-Match: "{token}"
{ "newParentPublicId": "{N14}" }      ← N14 está en profundidad 14

→ 200 OK
```

Profundidades reales leídas del listado **después** del movimiento:

| Unidad | Profundidad |
|---|---|
| `N14` | 14 |
| `N15`, `M01` | 15 |
| **`M02`** | **16** ❌ |
| **`M03`** | **17** ❌ |

**Máxima profundidad del árbol: 17. Límite declarado: 15.**

#### El par diferencial que lo prueba

El mismo guard, dos movimientos, resultados opuestos:

| Qué se mueve | Altura | Destino | Prof. del destino | Resultado |
|---|---|---|---|---|
| `M03` — **una hoja** | 1 | bajo `M02` | 16 | **`409`** ✅ rechazado |
| `M01` — **subárbol de 3** | 3 | bajo `N14` | 14 | **`200`** ❌ aceptado → deja 17 |

La hoja, que solo añadiría un nivel, **se bloquea**. El subárbol, que añade tres, **pasa**. No es que el guard esté ausente: **mide dónde aterriza el nodo y no lo que arrastra consigo**, así que cuanto más grande es lo que se mueve, más se equivoca.

Y la documentación del endpoint promete justo lo contrario:

> *«A move that would create a cycle or exceed the depth limit yields `409`.»*
> — `OrganizationUnitsController.cs:380`

El ciclo sí lo detecta. La profundidad no.

### 3.2 Causa

`OrgUnitAdministration.cs:900`, en el handler de `move`:

```csharp
var newDepth = OrgUnitHierarchyBuilder.CalculateDepth(parent.Id, byInternalId);
if (newDepth > OrgUnitValidationRules.MaxDepth)
{
    return Result<OrgUnitResponse>.Failure(OrgUnitErrors.DepthLimitExceeded);
}
```

Es **la misma línea que la creación** (`:652`). En creación es correcta: un nodo recién creado no tiene descendientes, así que su profundidad es la del árbol resultante. **Copiada al `move`, donde el nodo sí los tiene, mide la mitad del problema.**

`CalculateDepth` recorre **hacia arriba** (`cursor = parent.ParentInternalId`). Devuelve dónde quedaría el nodo movido. Nunca mira hacia abajo.

No es un descuido aislado del método: **los tres helpers de `OrgUnitHierarchyBuilder` que participan en el guard suben por la cadena de padres.** `WouldCreateCycle` sube, `CalculateDepth` sube. No existe ningún helper que calcule la altura de un subárbol, así que la comprobación que falta no estaba disponible para escribirse.

### 3.3 Impacto

**El invariante de profundidad no se sostiene.** `MaxDepth` deja de ser una garantía y pasa a ser una barrera que solo cubre una de las dos puertas. Cualquier consumidor que asuma «como mucho 15 niveles» —una vista de árbol, una exportación, un recorrido recursivo, un `diagram-export`— puede recibir 17 o más.

**El límite se puede rebasar sin límite.** El movimiento no es de una sola vez: mover un subárbol de altura *h* bajo el nivel 15 deja `15 + h − 1`. Repitiendo la operación se llega arbitrariamente lejos.

**Y el camino barato queda bloqueado mientras el caro pasa.** Crear el nivel 16 se rechaza; moverlo allí no. Un usuario que topa con el `409` de creación tiene, sin saberlo, una vía abierta que produce exactamente lo que el `409` intentaba impedir.

### 3.4 Propuesta

Sumar la **altura del subárbol movido** al cálculo, en el mismo punto:

```csharp
var newDepth = OrgUnitHierarchyBuilder.CalculateDepth(parent.Id, byInternalId);
var subtreeHeight = OrgUnitHierarchyBuilder.CalculateSubtreeHeight(orgUnit.Id, byInternalId); // 1 = hoja
if (newDepth + subtreeHeight - 1 > OrgUnitValidationRules.MaxDepth)
{
    return Result<OrgUnitResponse>.Failure(OrgUnitErrors.DepthLimitExceeded);
}
```

**No cuesta consultas.** `byInternalId` ya trae la jerarquía completa de la empresa en memoria —es la misma estructura que usan `WouldCreateCycle` y `CalculateDepth`—. `CalculateSubtreeHeight` es un recorrido en anchura sobre un índice hijos-por-padre construido en una pasada sobre ese mismo diccionario.

Conviene que el nuevo helper **tope su recorrido en `MaxDepth + 1`**, igual que hace `CalculateDepth`, para que un ciclo preexistente en los datos no lo cuelgue.

**El error a devolver es el que ya existe**, `ORG_UNIT_DEPTH_LIMIT_EXCEEDED` (`409`): el frontend no necesita conocer un código nuevo.

### 3.5 Compatibilidad

**No rompe el contrato**: mismo endpoint, mismo verbo, mismo código de error. Rechaza movimientos que hoy pasan — que es el objetivo — y **ninguno de ellos era válido según la regla declarada**.

⚠️ **Hay que decidir qué hacer con los árboles que ya rebasaron el límite.** Como el defecto no estaba diagnosticado, puede haber datos por encima de 15 en cualquier empresa. Sugerido: una consulta de diagnóstico que liste las unidades con profundidad > 15 antes de desplegar el guard, para no dejar árboles que ya no se puedan reorganizar sin bajarlos primero.

### 3.6 Alcance a revisar

**El patrón «guard de creación reutilizado en el movimiento» es el que hay que buscar, no este método.** Cualquier jerarquía del producto con reparentado —centros de trabajo, catálogos jerárquicos de país, la estructura de puestos— repite la forma. La pregunta para cada una: *¿el guard mira solo el nodo, o también lo que arrastra?*

### 3.7 Vía alterna vigente

Ninguna. **El frontend no puede suplirlo**: para anticipar el rechazo tendría que conocer la altura del subárbol que mueve, y eso exige recorrer el árbol completo en el cliente. Hoy la interfaz ofrece **Move** por nodo en la vista de árbol sin ninguna advertencia.

### 3.9 Lo que se hizo — y el aviso que sigue vigente

**El arreglo, en el mismo punto que fallaba** (`OrgUnitAdministration.cs`, handler de `move`):

```csharp
var newDepth      = OrgUnitHierarchyBuilder.CalculateDepth(parent.Id, byInternalId);        // donde aterriza
var subtreeHeight = OrgUnitHierarchyBuilder.CalculateSubtreeHeight(orgUnit.Id, byInternalId); // que arrastra
if (newDepth + subtreeHeight - 1 > OrgUnitValidationRules.MaxDepth)
```

El `- 1` es porque la altura cuenta la raíz: una hoja tiene altura 1 y no suma nada al nivel donde cae.

**`CalculateSubtreeHeight` es nuevo** y es la contraparte que §3.2 decía que no existía: los tres helpers del builder recorrían hacia arriba. Este baja en anchura sobre **la jerarquía que el handler ya tiene en memoria** — no cuesta ni una consulta más, como anticipaba §3.4 — y **topa en `MaxDepth + 1`**, igual que `CalculateDepth`, para que un árbol ya corrupto no cuelgue el recorrido.

**Rojo antes de verde, con el par diferencial.** Las dos pruebas de integración se corrieron contra el código sin arreglar:

```
FAILED   OrgUnits_Move_WhenSubtreeHeightWouldExceedDepthLimit  →  Expected: Conflict · Actual: OK
PASSED   OrgUnits_Move_WhenLeafLandsOnLastAllowedLevel
```

Falló **por la razón esperada**, y la hoja pasó — lo que prueba que el fixture no acopla los dos casos que la regla distingue. Sin la segunda, un arreglo que rechazara cualquier movimiento hacia el fondo habría pasado por bueno.

**Verificación:**

| Suite | Resultado |
|---|---|
| Sección `OrgUnits_` completa | 🟢 **20/20** — las 17 preexistentes siguen pasando |
| Unitarias | 🟢 **2975/2975** (eran 2970; +5 casos nuevos) |
| Build | sin avisos |

Las tres unitarias nuevas cubren un modo de fallo cada una: que la altura **cuente la raíz**; que se mida **el subárbol propio y no el árbol entero** —el error natural aquí, que se manifestaría como «no puedo mover nada hacia abajo»—; y que **no se cuelgue con datos cíclicos**.

### 3.10 ⚠️ Pendiente antes de desplegar — datos que ya rebasaron el límite

§3.5 lo advertía y **sigue vigente**: el guard nuevo impide crear el problema, pero **no arregla los árboles que ya lo tienen**. Una unidad por encima de 15 no se podrá reorganizar sin bajar la rama antes, porque cualquier movimiento suyo parte de un estado inválido.

**Hay al menos un caso conocido**: la empresa desechable `ZZZ DESECHABLE - Medicion de limites` quedó en profundidad 16 al medir este hallazgo.

Queda escrita la consulta de diagnóstico:

```
docs/technical/operations/scripts/diagnostico-profundidad-org-units.sql
```

Lista por empresa cuántas unidades están sobre el límite, cuántas activas, y el detalle con **la ruta completa desde la raíz** para saber por dónde cortar. No modifica nada. Verificada contra `clarihr_dev`: **0 filas**, nada que bajar en local.

**Correrla contra cada ambiente antes de desplegar el guard.**

### 3.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Medido en la empresa desechable `ZZZ DESECHABLE - Medicion de limites`, creada para este montaje |
| 2026-08-15 | 🔲 Propuesto | **Estado final del árbol: profundidad máxima 16**, con `M02` por encima del límite. Llegó a 17 durante la medición; al intentar reconstruir ese estado moviendo la hoja `M03`, el servidor lo **rechazó con `409`** — que es justo el par diferencial de §3.1. El caso de regresión se conserva a 16 |
| 2026-08-16 | 🟢 Resuelto | Guard corregido sumando la altura del subárbol (§3.9). Rojo verificado antes del arreglo. `OrgUnits_` 20/20 · unitarias 2975/2975. **Queda la consulta de diagnóstico de §3.10 antes de desplegar** |
## 4. B-03 — El producto tiene un canal de localización completo que ningún cliente puede activar

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto en el backend** — 2026-08-16 (§4.7) · queda el punto 3 (calidad de traducciones) |
| **Componente** | Api · resolución de idioma · **y el control de idioma del frontend** |
| **Origen** | §2.6, §5.4 — reabierto y **re-diagnosticado** en la corrida del Paso 4 |
| **Alcance** | **Transversal** — todos los módulos |

> ⚠️ **Este hallazgo se escribió mal la primera vez.** Decía «los mensajes de error de `OrgUnits` no están localizados», y daba por hecho que faltaban las traducciones. **Faltaba lo contrario: las traducciones están y no se usan.** La corrección y cómo se llegó a ella están en §4.2.

### 4.1 Evidencia

Toda respuesta de error llega en inglés, **incluso pidiendo español de forma explícita**:

```
GET /v1/work-center-types/{guid-inexistente}     Accept-Language: es-SV,es;q=0.9
→ 404  "The work center type could not be found."

GET /v1/org-units/{guid-inexistente}             Accept-Language: es
→ 404  "The organization unit could not be found."
```

Medido en tres módulos —`Compliance`, `OrgUnits`, `WorkCenterTypes`— y con tres variantes de cabecera (`es`, `es-SV`, sin cabecera). **Inglés en los nueve casos.**

### 4.2 Causa — la corrección

**Las traducciones existen.** En `BackendMessages.es.resx`, para las claves exactas que se probaron:

```xml
<data name="ORG_UNIT_NOT_FOUND">
  <value>No se pudo encontrar organization unit.</value>
</data>
<data name="WORK_CENTER_TYPE_CODE_CONFLICT">
  <value>Otro work center type ya usa code solicitado.</value>
</data>
```

**Y el canal está conectado de punta a punta:**

| Pieza | Dónde |
|---|---|
| Registro en DI | `DependencyInjection.cs:121` — `IBackendMessageLocalizer → ResourceBackendMessageLocalizer` |
| Middleware de cultura | `RequestLocaleMiddleware.cs:14-16` |
| Consumo en las respuestas de error | `ProblemDetailsDefaults.cs:45` |
| Resolución de idioma | `RequestLanguageResolver.cs` |

El orden de resolución es explícito:

```
claim «language» del JWT  →  cabecera Accept-Language  →  «en»
```

**Y aquí está el eslabón roto.** El único control de idioma del producto vive en *Mi cuenta → Interface*, y la propia tarjeta lo dice:

> *«Applies immediately after saving. **Stored on this device**.»*

**Es una preferencia del dispositivo.** Se guarda en `localStorage` (`clarihr-ui-lang`) y **nunca llega al servidor**, así que el claim del JWT jamás se establece desde la interfaz. El primer eslabón de la cadena está permanentemente vacío.

#### Lo que queda sin distinguir, y cómo se cierra

Con el claim vacío, el resolvedor debería usar la cabecera — y aun mandándola explícita responde en inglés. Quedan dos explicaciones y **no se distinguieron**:

| Hipótesis | Cómo se descarta |
|---|---|
| **(a)** El claim `language` **sí** existe en el JWT con valor `en`, y gana sobre la cabecera | Inspeccionar el emisor del token: buscar dónde se escribe `RequestLanguageResolver.LanguageClaimType` |
| **(b)** El proxy del BFF **descarta o reescribe** `Accept-Language` antes de reenviar | Leer la configuración de reenvío del BFF |

**Las dos son búsquedas en el repositorio, sin ambiente.** Es lo primero que debe hacer quien tome el hallazgo, porque el arreglo depende de cuál sea.

> **Nota de método.** El error original fue concluir «no está localizado» desde el síntoma sin comprobar si los recursos existían. Bastaba un `grep` al `.resx`. La cuarta pregunta de §6 del README —*¿el producto lo resuelve en otro lado?*— habría bastado para evitarlo.

### 4.3 Impacto

**Hay trabajo de traducción ya hecho y pagado que no llega a ningún usuario.** No es que falte el español: es que el producto no tiene forma de pedirlo.

Y el frontend queda con una obligación que no puede cumplir bien: **traducir por su cuenta cada código de error**, manteniendo un catálogo paralelo que duplica el del servidor y que se desincronizará. Es lo que ya hace —por eso la validación en cliente sí sale en español mientras los `409` salen en inglés—, y produce **una pantalla en dos idiomas**: los mensajes que el cliente conoce en español, los que solo conoce el servidor en inglés.

### 4.4 Propuesta

Tres piezas; **la primera es la que desbloquea todo lo demás**:

1. **Que la preferencia de idioma viaje al servidor.** O se persiste en el perfil del usuario y se emite en el claim `language`, o —lo más barato— el cliente HTTP del frontend manda `Accept-Language` con el valor de `clarihr-ui-lang` en cada petición. **Lo segundo es un interceptor de una línea y no requiere cambios de esquema.**
2. **Resolver la hipótesis (a)/(b)** de §4.2 y arreglar lo que corresponda.
3. **Revisar la calidad de las traducciones existentes**, que están a medio hacer:
   - `"No se pudo encontrar organization unit."` — la entidad sigue en inglés
   - `"Otro work center type ya usa code solicitado."` — dos términos sin traducir y falta el artículo

   Sirven para el mecanismo pero no para enseñárselas a un usuario. **Que el canal funcione dejaría estos textos a la vista**, así que conviene revisarlos **antes** de activar el paso 1, no después.

### 4.5 Compatibilidad

**No rompe nada.** Los códigos de error (`extensions.code`) no cambian, y son lo que el frontend usa para decidir. Solo cambia el texto legible, y solo para quien pida español.

### 4.6 Alcance a revisar

**Es transversal por construcción**: la resolución de idioma es una sola, en el pipeline. Arreglarla los arregla todos a la vez — y por eso **no tiene sentido levantarlo pantalla por pantalla**. El conteo que en la versión anterior de este hallazgo se proponía («cuántos módulos localizan y cuántos no») **ya no aplica**: la respuesta es que ninguno lo hace, porque el problema no está en los módulos.

Lo que sí conviene contar es **cuántas claves del `.resx` tienen traducción a medio hacer**, para dimensionar el punto 3.

**Medido — 2026-08-16:**

```
claves EN: 1051   ·   claves ES: 1051   ·   sin traducir: 0
traducciones con términos de dominio en inglés: 65
```

La cobertura es total; lo que falta es **calidad** en 65 de ellas (`"Otro commercial addon ya usa code solicitado."`).

### 4.7 Lo que se hizo — y la hipótesis que quedó resuelta

§4.2 dejaba dos hipótesis sin distinguir y decía que era «lo primero que debe hacer quien tome el hallazgo». **Es la (a), y la (b) ni siquiera hace falta para explicar el síntoma.**

```csharp
// JwtTokenService.cs:268 — ANTES
var language = await userPreferenceRepository.ResolveLanguageAsync(user.Id, ct) ?? DefaultLanguage;
claims.Add(new Claim(LanguageClaimType, language));
```

Con `?? "en"`, **«no eligió idioma» y «eligió inglés» eran indistinguibles**. Y como el resolvedor va `claim → Accept-Language → "en"`, un claim siempre presente hacía que la cabecera **nunca** se consultara.

> ⚠️ **Esto invalida la opción barata de §4.4.** El hallazgo proponía que el frontend mandara `Accept-Language` y lo llamaba «un interceptor de una línea». **No habría funcionado**: el claim gana siempre. Se habría gastado una entrega en descubrirlo.

**El arreglo:** emitir el claim **solo si el usuario eligió idioma**. Sin claim, el resolvedor cae a la cabecera tal y como está documentado; quien sí elige, lo sigue recibiendo.

**Rojo primero, y en el sitio correcto.** El harness de integración **no emite el claim**, así que un test de integración con `Accept-Language: es` habría salido verde sin probar nada — el doble sustituía justo la pieza rota. El rojo válido estaba en `JwtTokenServiceTests`, y allí había **un test verde que fijaba el defecto**: `…ShouldEmitDefaultLanguageClaim` exigía el claim `"en"` para un usuario sin preferencia. Invertido y renombrado.

**Verde end-to-end:** `Localization_WhenSpanishIsRequested_ShouldAnswerInSpanish` (`es` y `es-SV,es;q=0.9`) más su contrapeso `…ShouldFallBackToEnglish`, que impide que «localiza siempre en español» pase por bueno.

**Lo que queda abierto:** el punto 3 de §4.4 — revisar las 65 traducciones a medio hacer. **Ahora es urgente**, porque el canal funcionando las pone a la vista.

> **Corrección de §4.5:** dice «los códigos de error (`extensions.code`)». Según §5.3 de las definiciones técnicas, en el wire **no existe** un objeto `extensions`: `code` es miembro **raíz**. La afirmación de fondo es correcta —los códigos no cambian—, la ruta estaba mal.

### 4.7 Vía alterna vigente

El frontend traduce por su cuenta a partir de `extensions.code`. Funciona, y **es exactamente lo que la propuesta 1 haría innecesario**.

### 4.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Levantado en el Paso 3 como «los mensajes no están localizados» |
| 2026-08-15 | 🔲 Propuesto | **Re-diagnosticado en el Paso 4.** El `.resx` sí tiene las traducciones y el canal está cableado; lo que falta es que la preferencia de idioma llegue al servidor. **Sustituye también a [00002 / B-01](00002-UnitTypes.md#2-b-01--el-español-que-se-sirve-estaba-roto-199-mensajes-en-dos-idiomas-y-320-sin-tildes)**, que tenía el mismo diagnóstico equivocado |
| 2026-08-16 | 🟢 Resuelto | **Verificado tras recuperar la corrida perdida.** Unitarias **2970/2970**; integración dirigida **776/776** en cinco lotes (guardrails·repr.legales·empresas 111 · centros trabajo·unidades·ubicaciones 74 · expedientes·puestos·plazas 200 · nómina·ausencias·reportes 282 · contratación·competencias·centros costo 109). **Cero fallos.** Resultados en `TestResults/lote*.trx` |
| 2026-08-21 | 🟢 Resuelto **por completo** | Quedaba media respuesta sin traducir: en un `400` de *model-binding* los mensajes salían en español y `title`/`detail` en inglés (§4.9). **Verificado**: unitarias **3047/3047** · integración **990/990**, cero fallos |

### 4.9 La mitad de la respuesta que seguía en inglés — 🟢 **cerrado el 2026-08-21**

El canal quedó abierto el 2026-08-16 y los mensajes empezaron a llegar traducidos. Pero la revalidación de los documentos de frontend midió la respuesta **entera** y encontró que una parte no había cambiado nunca:

```jsonc
// POST /companies/{id}/work-centers  ·  Accept-Language: es
{
  "title":  "One or more validation errors occurred.",        // ⚠️ inglés
  "detail": "One or more validation errors occurred.",        // ⚠️ inglés
  "errors": { "locationGroupPublicId": ["El valor debe ser un UUID válido."] }   // ✅ español
}
```

**La misma respuesta en dos idiomas.**

**La causa, verificada en el código.** `ProblemDetailsDefaults` intentaba traducir así:

```csharp
problemDetails.Title ??= localizer?.Localize(ValidationCode, ValidationTitle) ?? ValidationTitle;
```

`??=` asigna **sólo si el valor es nulo**, y ASP.NET ya había puesto el título por defecto antes de llegar aquí. La traducción estaba escrita, cableada y correcta — **y no se ejecutaba nunca**. No es una traducción que falte: es una línea que no dispara.

**Por qué se mantuvo invisible.** Hay dos caminos que producen un `400` y sólo uno pasa por aquí:

| Camino | Traducía `title`/`detail` |
|---|---|
| Validación de negocio (FluentValidation → `ProblemDetailsFactory`) | ✅ sí |
| **Model-binding** (tipo mal formado, JSON inválido) → `ProblemDetailsDefaults` | ❌ **no** |

Y el test que cubría este caso —`ApiIntegrationTests.cs`, el `400` de `dependentJobProfilePublicId = "not-a-guid"`— aceptaba **cualquiera de los dos idiomas**:

```csharp
var expectedValidationMessages = new[]
{
    "One or more validation errors occurred.",
    "Se encontraron uno o más errores de validación."
};
Assert.Contains(title, expectedValidationMessages);
```

Escrito así para no acoplarse al idioma, el test **no podía distinguir** el comportamiento correcto del defectuoso. Es el mismo patrón que ya se registró en [00002 / B-01](00002-UnitTypes.md#2-b-01--el-español-que-se-sirve-estaba-roto-199-mensajes-en-dos-idiomas-y-320-sin-tildes) y en el doble de test de los endpoints anónimos: **una aserción permisiva no es cobertura, es permiso**.

**El arreglo.** Traducir también cuando el título **es** el texto por defecto de ASP.NET —el que puso el framework, no una decisión de nadie—, y respetar cualquier otro:

```csharp
if (string.IsNullOrWhiteSpace(problemDetails.Title) ||
    string.Equals(problemDetails.Title, ValidationTitle, StringComparison.Ordinal))
{
    problemDetails.Title = localizer?.Localize(ValidationCode, ValidationTitle) ?? ValidationTitle;
}
```

Lo mismo para `detail`. **Verificado con su contrapeso**: un test exige el título en español con `Accept-Language: es`; el otro, que **sin** idioma pedido siga saliendo en inglés. Sin el segundo, traducir a lo bruto pasaría por bueno.

---

## 5. B-04 — El servidor sabe *por qué* no se puede inactivar, y no lo dice; y lo creado por error no se puede borrar nunca

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-16, verificado (§5.9) · **con tres correcciones a la propuesta** |
| **Componente** | Api · `OrganizationUnitsController`, `OrganizationStructureCatalogsController` |
| **Origen** | Revisión de contrato del Paso 2 y del Paso 3, contrastada con módulos vecinos |
| **Alcance** | **Dos recursos** — tipos de unidad y unidades organizativas |

> **Nota de método.** En la corrida anterior esto quedó escrito como «no hay `DELETE`, y es correcto». **Eso fue una racionalización, no un análisis.** Al contrastarlo con el resto del producto, la conclusión se invierte: el propio producto ya implementa el patrón seguro dos pestañas más allá, en la misma pantalla.

### 5.1 Evidencia

Ninguno de los dos controladores expone borrado ni consulta de uso:

```bash
$ grep -c "HttpDelete\|usage" src/CLARIHR.Api/Controllers/OrganizationUnitsController.cs
0
$ grep -c "HttpDelete\|usage" src/CLARIHR.Api/Controllers/OrganizationStructureCatalogsController.cs
0
```

**El servidor calcula la información que le falta al usuario, y la descarta.** En el handler de inactivación de tipos de unidad:

```csharp
// OrgStructureCatalogsAdministration.cs:651-655
if (await repository.HasOrgUnitsUsingOrgUnitTypeAsync(entity.Id, cancellationToken) ||
    await repository.HasPositionCategoryClassificationsUsingOrgUnitTypeAsync(entity.Id, cancellationToken))
{
    return Result<OrgUnitTypeCatalogItemResponse>.Failure(OrgStructureCatalogErrors.CatalogInUse);
}
```

Se resuelven **dos preguntas distintas** —¿lo usan unidades? ¿lo usan clasificaciones de categoría?— y las dos colapsan en un único error que no distingue cuál falló:

```
ORG_STRUCTURE_CATALOG_IN_USE  ·  409
"The catalog item cannot be inactivated while it is in use."
```

Lo mismo en unidades organizativas: `ORG_UNIT_HAS_ACTIVE_CHILDREN` dice que hay hijos activos, **no cuáles ni cuántos**, y la interfaz no ofrece inactivar (espejo **F-04**), así que ni siquiera hay dónde leerlo.

### 5.2 El producto ya resolvió esto — en la misma pantalla

La pantalla del Paso 3 tiene tres pestañas. **La tercera, centros de costo, sí tiene el endpoint:**

```csharp
// CostCentersController.cs:81
[HttpGet("cost-centers/{id:guid}/usage")]
// "Returns the active/inactive reference counts for the cost center across
//  organization units and position slots, indicating whether it is safe to inactivate."
```

```csharp
public sealed record CostCenterUsageResponse(
    Guid Id, string Code, string Name,
    int OrgUnitActiveReferences,      int OrgUnitInactiveReferences,
    int PositionSlotActiveReferences, int PositionSlotInactiveReferences,
    bool HasActiveReferences);
```

Y el borrado condicional también existe, en `JobCatalogsController`:

> *«Requires the `If-Match` header with the current `concurrencyToken`. System items are rejected, and an item still referenced by existing job profiles cannot be deleted (a usage check is enforced).»*

Es decir: **borrado duro, protegido por verificación de uso y por la marca de elemento de sistema.** Exactamente lo que hace falta aquí.

Quedan entonces tres niveles en un mismo producto:

| Nivel | Recursos | Puede limpiar un error | Sabe qué lo bloquea |
|---|---|---|---|
| Borrado condicional + verificación de uso | `JobCatalogs` | ✅ | ✅ |
| Baja lógica + `/usage` | `CostCenters`, `LegalRepresentatives`, `LocationGroups` | ❌ | ✅ |
| **Baja lógica sola** | **`OrgUnitTypes`, `OrgUnits`** | ❌ | ❌ |

Los dos recursos del nivel más pobre son **justo los que se llenan en la configuración inicial**, que es el momento con más probabilidad de error de captura.

### 5.3 Impacto

**Un registro creado por equivocación no se puede quitar jamás.** Un tipo de unidad con el código mal escrito, una unidad duplicada por un doble clic: nunca fueron referenciados por nadie, y aun así quedan para siempre en el catálogo, inactivos. En una configuración inicial de 9 tipos y 26 unidades, dos o tres errores de captura son lo esperable, y ensucian de forma permanente los combos y los exports.

**Y cuando la baja lógica se rechaza, el usuario queda sin salida.** Recibe «está en uso» y no tiene ningún endpoint que le diga en uso *por quién*. Con 26 unidades se puede buscar a mano; con varios cientos, no. El dato existe en el servidor en el momento exacto del rechazo.

### 5.4 Propuesta

**Dos cambios, independientes entre sí. Se recomienda empezar por el primero:** es el más barato, no rompe nada y resuelve el problema diario.

**(a) `GET /org-units/{publicId}/usage` y `GET /org-unit-types/{publicId}/usage`** — calcados de `CostCenterUsageResponse`. Las consultas ya están escritas y ya se ejecutan en los handlers de inactivación; solo hay que exponerlas como cuentas en vez de booleanos.

```jsonc
GET /v1/org-unit-types/{publicId}/usage        → 200
{
  "id": "…", "code": "GERENCIA", "name": "Gerencia",
  "orgUnitActiveReferences": 8,  "orgUnitInactiveReferences": 0,
  "positionCategoryActiveReferences": 0, "positionCategoryInactiveReferences": 0,
  "hasActiveReferences": true
}

GET /v1/org-units/{publicId}/usage             → 200
{
  "id": "…", "code": "VP-OPS", "name": "…",
  "activeChildren": 3, "inactiveChildren": 0,
  "positionSlotActiveReferences": 0,
  "hasActiveReferences": true
}
```

**(b) `DELETE` condicional**, con el guard de `JobCatalogsController` tal cual: exige `If-Match`, rechaza elementos de sistema (`-9000…-9969`, ver guía §0) y rechaza cualquier elemento con referencias —activas o inactivas—. Con esa triple protección, lo único borrable es exactamente lo que nadie llegó a usar, que es el caso a resolver.

| Código | HTTP | Cuándo |
|---|---|---|
| `…_NOT_FOUND` | `404` | No existe o es de otra empresa |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado |
| `…_IS_SYSTEM_ITEM` | `409` | Elemento sembrado |
| `…_IN_USE` | `409` | Tiene referencias — **con `/usage` disponible para saber cuáles** |

### 5.5 Compatibilidad

**(a) no rompe nada:** endpoints nuevos, sin tocar los existentes. **(b) tampoco:** un verbo nuevo sobre una ruta ya existente. Ambos requieren regenerar `openapi.yaml`. Sin migración de datos ni de clientes.

### 5.6 Alcance a revisar

De 142 controladores, **40 exponen `HttpDelete`** y solo **3 exponen `/usage`**. Conviene decidir la regla del producto —qué catálogos merecen borrado condicional y cuáles solo baja lógica— **una vez**, en lugar de resolverlo pantalla por pantalla. El criterio que sugiere la evidencia: **todo catálogo que se llena durante la configuración inicial necesita las dos cosas**, porque es donde se cometen los errores de captura.

### 5.7 Vía alterna vigente

Para el borrado, **ninguna**. Para el uso: el frontend puede aproximarlo con `GET /org-units?orgUnitTypePublicId=…` y contar, pero **solo cubre una de las dos referencias** que revisa el servidor en tipos de unidad — las clasificaciones de categoría de puesto no son consultables desde esa pantalla. La aproximación diría «no está en uso» en un caso en que el servidor rechaza.

### 5.9 Lo que se hizo — y dos correcciones a la propuesta

Se implementaron **las dos partes**, (a) y (b), en los dos recursos que nombra §5.4.

#### (a) Los endpoints de uso

```
GET /organization-units/{id}/usage
GET /organization-structure-catalogs/unit-types/{id}/usage
```

Exponen **los contadores que el servidor ya calculaba y descartaba**. El de tipos de unidad **separa las dos preguntas** que el guard de inactivación colapsa en un único `ORG_STRUCTURE_CATALOG_IN_USE` sin decir cuál disparó — que era el núcleo del hallazgo.

**Dos desvíos deliberados respecto a §5.4:**

| §5.4 proponía | Se hizo | Por qué |
|---|---|---|
| `positionSlotActiveReferences` en unidades | `jobProfile…References` | Una plaza **no referencia** a la unidad: referencia a un perfil, y el perfil cuelga de la unidad. Contar plazas exigiría un doble salto y respondería otra pregunta |
| Solo lo que bloquea | Activas **e** inactivas | El guard solo mira activas, pero la diferencia entre «está dormido» y «no lo usa nadie» es justo lo que hace falta para decidir si se puede borrar |

#### (b) El borrado condicional

```
DELETE /organization-units/{id}                                  If-Match obligatorio
DELETE /organization-structure-catalogs/unit-types/{id}           If-Match obligatorio
```

**El guard lee la misma fuente que `/usage`, a propósito.** El defecto que este hallazgo corrige es que el servidor sabía por qué bloqueaba y no lo decía; si el bloqueo y la explicación se calcularan por separado, podrían divergir y volveríamos al mismo sitio.

**Corrección 1 — aquí no hay elementos de sistema.** §5.4 proponía «rechaza elementos de sistema (`-9000…-9969`)». **No aplica:** ninguna de las dos entidades tiene `IsSystem`, ninguna se siembra con `HasData`, y la base no tiene un solo id negativo (**0 de 8** en `org_unit_type_catalog_items`). Son datos de empresa, creados por el usuario. Ese guard habría sido código muerto.

> Lo confirma la corrida del Paso 6: la empresa desechable arrancó con **0 tipos de unidad**. Si estuvieran sembrados, habría traído los del país.

**Corrección 2 — el borrado necesita su propio código de error.** §5.4 proponía reutilizar `…_IN_USE`. No sirve: el existente dice *«no se puede **inactivar** mientras esté en uso»* y **solo cuenta referencias activas**. El borrado es más estricto por una razón que está en la base de datos:

```
org_units.parent_id                 → RESTRICT
job_profiles.org_unit_id            → RESTRICT
position_category_classifications.org_unit_type_catalog_item_id → RESTRICT
```

Con las FK en `RESTRICT`, **cualquier** referencia rompe el borrado, activa o no. Sin el guard el usuario recibiría una violación de integridad en crudo. Se añadieron `ORG_UNIT_IN_USE` y `ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE`.

**Corrección 3 — el verbo devuelve el recurso, no `204`.** Se escribió primero como `204 No Content`, y el framework de CQRS del proyecto **no admite comandos sin respuesta**. El borrado hermano de `JobCatalogs` —el que §5.4 manda copiar— devuelve el elemento eliminado con `200`. Se siguió esa convención: es menos ortodoxa en REST puro pero es la del producto, y de paso le da al cliente el estado final sin haberlo tenido que guardar antes.

#### Verificación

| Suite | Resultado |
|---|---|
| Integración · `/usage` | 🟢 **9/9** — 4 nuevas + 5 preexistentes de centros de costo y grupos |
| Integración · `DELETE` | 🟢 **4/4** |
| Guardrails de contrato público | 🟢 **10/10** — los endpoints nuevos pasan sin ajustes |
| Build | sin avisos |
| `openapi.yaml` | 🟢 sincronizado — 2 rutas y 2 verbos nuevos, 0 ausencias |

**No hubo rojo previo, y es una excepción justificada:** estos endpoints no existían, así que el rojo habría sido un `404`, que no prueba nada sobre el comportamiento. La regla del rojo sirve para arreglos de defectos, no para superficie nueva. Lo que la sustituye es el **par diferencial** en cada caso: con referencias y sin ellas. Un endpoint que respondiera siempre lo mismo falla una de las dos necesariamente.

La prueba de borrado con referencias **inactiva al hijo antes de intentarlo**: verifica que la baja lógica **no** desbloquea el borrado. Es el error natural aquí —copiar el guard de inactivación sin pensar— y sin esa prueba pasaría desapercibido.

### 5.10 Lo que sigue abierto

**El alcance sube de 2 recursos a 5, y los 5 están hechos.** §5.6 pedía decidir la regla del producto
de una vez; se completó el 2026-08-21 con **tipos de centro de trabajo**, **centros de trabajo** y
**áreas funcionales**.

#### La extensión no fue mecánica: dos referencias no tienen clave foránea

Se esperaba replicar el patrón y ya. Al mapear qué apunta a cada recurso apareció que **el grafo de
claves foráneas no cuenta la historia completa**:

| Recurso | Referencias con FK | Referencias **sin** FK |
|---|---|---|
| Tipo de centro de trabajo | `work_centers` (RESTRICT) | — |
| Centro de trabajo | `position_slots` (RESTRICT) | ⚠️ **`personnel_file_employment_assignments.work_center_public_id`** |
| Área funcional | `org_units` (RESTRICT) | ⚠️ **`company_preferences.hr_functional_area_code`** (apunta por **código**) |

**Un borrado guiado solo por las claves foráneas habría pasado limpiamente en los dos casos**, dejando
expedientes apuntando a un centro inexistente y el indicador de RRHH del tablero apuntando a un código
que ya no existe — sin que la base de datos protestara, porque no tiene con qué.

Las dos se cuentan en `/usage` y las dos bloquean el `DELETE`. Cada una tiene su prueba de integración
dedicada, que es lo único que las protege.

#### Verificación

Build 0 errores / 0 advertencias · unitarias **3031/3031** · guardrails de contrato + los 6 borrados
nuevos **28/28**. Huella regenerada: **918 operaciones** (+6). `openapi.yaml` sincronizado por inserción
quirúrgica — **564 líneas añadidas, 0 eliminadas**, y el contraste contra el volcado da 571 = 571 rutas,
0 verbos y 0 esquemas ausentes.

> Una trampa del camino: el `normalizedName` de los bancos y el `code` de `iam_permissions` enseñaron
> que hay que mirar cómo se guarda cada clave antes de emparejar. Aquí el equivalente fue el nombre real
> de la tabla: la primera consulta del grafo buscó `work_center_type_catalog_items` —que no existe— y
> devolvió «sin referencias» para un recurso que sí las tiene. La tabla es `work_center_types`.

**Y salió un hallazgo nuevo al implementar esto**, levantado aparte: el guard de inactivación de una unidad **solo mira hijos, no perfiles de puesto**, así que una unidad con perfiles activos se puede inactivar — y después crear plazas contra esos perfiles.

### 5.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Levantado al revisar la conclusión «no hay `DELETE`, y es correcto» del Paso 2, que resultó ser una racionalización. El contraste con `JobCatalogs` y `CostCenters` —dos pestañas de la misma pantalla— la invierte |
| 2026-08-16 | 🟢 Resuelto | Implementadas las partes (a) y (b) en los dos recursos (§5.9). **Tres correcciones a la propuesta**: no hay elementos de sistema aquí, el borrado necesita su propio código de error, y el verbo devuelve el recurso porque el framework no admite comandos sin respuesta. Integración 13/13 · guardrails 10/10 · `openapi.yaml` sincronizado |
| 2026-08-21 | 🟢 Resuelto | **Extensión completada: los 5 recursos.** No fue mecánico — **dos referencias no tienen clave foránea** (la asignación de expediente apunta al centro por `publicId`; la preferencia del tablero apunta al área por `code`) y un borrado guiado solo por el grafo de FKs las habría dejado colgando en silencio. 6 endpoints nuevos, 6 pruebas de integración, huella 918 (+6), `openapi.yaml` +564/-0 |

---


---

## 6. B-05 — El espacio `/v1` no se comporta como un espacio de API: una ruta inexistente responde `200` con HTML y un `PATCH` válido responde `500`

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | ⏸️ **Bloqueado** — fuera de este repositorio · ver [00900 / ProxyBFF](../ComentariosPruebasFrontend/00900-ProxyBFF.md) |
| **Componente** | BFF · proxy de `/v1` |
| **Dueño** | **Quien mantiene el BFF** — no es deuda del backend · índice accionable en [00900 / ProxyBFF](../ComentariosPruebasFrontend/00900-ProxyBFF.md) |
| **Origen** | Sondas de ruta durante el montaje de la empresa desechable |
| **Alcance** | **Transversal** — todo el espacio `/v1` |

### 6.1 Evidencia

Dos comportamientos distintos, medidos con cabeceras correctas:

**(a) Una ruta que no existe devuelve `200` con la página de la aplicación.**

```
GET /v1/unit-typez            → 200   (cuerpo: el HTML del SPA)
GET /v1/ruta-que-no-existe    → 200   (cuerpo: el HTML del SPA)
```

El comodín del SPA atrapa `/v1/...`. Un cliente que se equivoque en un nombre no recibe `404`: recibe `200` y un HTML que reventará al intentar interpretarlo como JSON.

> Fue exactamente lo que pasó al buscar los diccionarios de traducción: `/i18n/es.json` respondió `200` y resultó ser el HTML del SPA.

**(b) El mismo recurso responde distinto según el nombre de ruta que se use, y solo en los verbos de escritura.**

El API real llama al recurso `organization-units`; el frontend lo consume como `org-units`. Los dos nombres llegan:

| Petición | `org-units` (alias) | `organization-units` (nombre real) |
|---|---|---|
| `GET /{id}` | `200` | **`200`** |
| `PATCH /{id}/activate` | `409` *(ya activa — correcto)* | **`500`** |
| `PATCH /{id}/move` | `200` | **`500`** |

**La lectura funciona con los dos nombres; la escritura solo con uno**, y el otro no da `404` sino `500`.

### 6.2 Causa probable

No se inspeccionó la configuración del proxy, así que esto es hipótesis: hay una **tabla de alias por verbo** que cubre `GET` para ambos nombres y las escrituras solo para el alias, y el camino no cubierto termina en una excepción no manejada en vez de en un `404`. **Se confirma leyendo la configuración de reenvío del BFF** — es la primera comprobación que debería hacer quien tome el hallazgo.

### 6.3 Impacto

**Un `500` miente sobre de quién es el problema.** Le dice al cliente «el servidor se rompió» cuando lo correcto es «esa ruta no existe». Quien integre contra el API perderá tiempo buscando una falla del servidor que no está ahí. Y en los tableros de operación, estos `500` se mezclan con los reales.

**El `200` con HTML es peor de diagnosticar**: no hay error, hay un cuerpo que no encaja. El síntoma aparece lejos del origen, en el parseo.

**Hoy no golpea al frontend** —usa siempre el alias y las rutas correctas—, pero sí golpea a cualquiera que pruebe el API a mano, que es lo que se hace en estas corridas.

### 6.4 Propuesta

1. **Que `/v1/...` nunca caiga en el comodín del SPA.** Lo que empiece por el prefijo del API y no case con ninguna ruta debe responder `404` con el cuerpo de error estándar (`application/problem+json`), no HTML.
2. **Decidir el nombre canónico y cerrar el otro.** O el alias cubre todos los verbos, o el nombre real deja de aceptarse en cualquier verbo. **Que `GET` acepte los dos y `PATCH` solo uno no es una decisión, es un descuido**: cualquiera que descubra el recurso leyendo asumirá que puede escribir igual.
3. Que la ruta no cubierta dé `404` en vez de `500`.

### 6.5 Compatibilidad

**No rompe al frontend**: usa el alias en todos los verbos. Sí cambia respuestas que hoy son `200`/`500` por `404`, que es la corrección buscada. Conviene confirmar que ninguna herramienta interna dependa del nombre real en escrituras antes de cerrarlo.

### 6.6 Alcance a revisar

Es **transversal a todo `/v1`**, no de `OrgUnits`. La pregunta a responder de una vez: **¿cuántos recursos tienen dos nombres de ruta, y en cuántos verbos coincide cada par?** Se responde comparando la tabla de alias del BFF con las rutas declaradas en los controladores.

### 6.7 Vía alterna vigente

El frontend no necesita ninguna: usa el alias correcto. Para las pruebas manuales, la vía alterna es **usar siempre el nombre que usa el frontend** y desconfiar de un `200` cuyo cuerpo no sea JSON.

### 6.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Detectado al equivocar el nombre de ruta durante el montaje de profundidad. **Un `415` que apareció en las primeras sondas resultó ser un defecto del instrumento** —el ayudante de `fetch` pisaba las cabeceras— y se descartó; lo que queda aquí está medido con cabeceras verificadas |
