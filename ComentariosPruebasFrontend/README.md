# ComentariosPruebasFrontend — definición del documento

Carpeta de resultados de las pruebas manuales del frontend de CLARI HR, hechas pantalla por pantalla contra el ambiente real y contrastadas contra el contrato del backend de este repositorio.

El destinatario principal es **el equipo de frontend**. Por eso la regla central de esta carpeta es:

> **Todo hallazgo debe llevar la solución integrada con el backend.**
> No basta con evidenciar el problema en la pantalla: hay que decir **qué endpoints y contratos son los válidos** y **qué reglas de negocio los gobiernan**, para que el frontend pueda ajustar sin tener que leer código C#.

La segunda regla, sobre **cómo** se prueba:

> **Cada paso se prueba con el ciclo completo, no solo de lectura.**
> Crear, leer, editar y dar de baja — y los caminos de error que solo aparecen al escribir (duplicado, concurrencia, validación del servidor). Una pantalla no está probada hasta que se ejercitó todo su ciclo.
>
> **Y se prueba con datos reales**, los del escenario **AVIANCA** documentado en [`docs/technical/operations/guia-configuracion-empresa-avianca-es.md`](../docs/technical/operations/guia-configuracion-empresa-avianca-es.md). No con `prueba1`, `test`, `aaa`.
>
> **La empresa de prueba queda operativa al terminar cada paso**, con los datos cargados y el asistente `/setup` avanzado. Cada corrida deja el terreno listo para la siguiente.

La tercera regla, sobre **qué se puede dejar sin resolver**:

> **Nada queda pendiente de medir.** Todo tiene que poderse **medir, documentar y validar**.
> «No se pudo probar» no es una conclusión: es una tarea sin hacer. Si algo parece no medible, casi siempre falta idear el montaje, no falta la posibilidad.

Ver §7 para el catálogo de montajes que ya funcionaron y para las dos únicas excepciones admitidas.

Y la regla que las complementa:

> **Si durante la prueba se encuentra una falla o una mejora del backend, se registra también.**
> Se prueba el frontend, pero se prueba **contra** el backend: es el momento en que las carencias del servidor se vuelven visibles. Esos hallazgos **no se documentan aquí**: van al **documento espejo** del mismo paso en la carpeta hermana **`../ComentariosPruebasBackend/`**, para poder **analizarlos, proponer y resolver** con su propio ciclo de vida.

Cada paso probado genera como máximo **dos documentos espejo**, con el mismo número y el mismo nombre:

```
ComentariosPruebasFrontend/00001-CompanyLegalProfile.md   ← lo que arregla el frontend
ComentariosPruebasBackend/00001-CompanyLegalProfile.md    ← lo que arregla el backend
```

**Y hay un tercer dueño.** El proxy del mismo origen que traduce `/v1/...` **no está en `CLARIHR-backend`**. Un hallazgo cuyo componente sea el BFF no lo puede tomar ninguna de las dos carpetas: se marca ⏸️ **Bloqueado** con el campo **Dueño** explícito, y se indexa en [`00900-ProxyBFF.md`](00900-ProxyBFF.md), que es el documento que se entrega a quien mantiene el BFF.

> **La regla general:** antes de archivar un hallazgo, preguntar **quién tiene el código que hay que tocar**. Si la respuesta no es ninguna de las dos carpetas, la carpeta no decide el dueño — el componente sí.

```
```

Si el paso no generó hallazgos de backend, el segundo archivo **no se crea**.

| Archivo | Para qué |
|---|---|
| `README.md` | Esta definición |
| `<NNNNN>-<NombrePantalla>.md` | Resultado de la prueba de cada pantalla |
| [`../ComentariosPruebasBackend/`](../ComentariosPruebasBackend/README.md) | Documentos espejo con los hallazgos de backend |

---

## 1. Nomenclatura de archivos

```
<NNNNN>-<NombrePantalla>.md
```

- `NNNNN` — correlativo de 5 dígitos, en el orden en que se hicieron las pruebas.
- `NombrePantalla` — en **PascalCase**, alineado con la ruta del frontend y/o la entidad del backend, para que el nombre sea rastreable de ambos lados.

Ejemplos:

| Archivo | Pantalla | Ruta frontend | Entidad backend |
|---|---|---|---|
| `00001-CompanyLegalProfile.md` | Perfil legal de la empresa | `/personnel-files/company-legal-profile` | `CompanyLegalProfile` |
| `00002-UnitTypes.md` | Tipos de unidad | `/…/unit-types` | `UnitType` |
| `00003-OrgUnits.md` | Unidades organizativas | `/…/org-units` | `OrgUnit` |

Un archivo por pantalla. Si una pantalla se vuelve a probar, se actualiza su archivo y se registra la nueva corrida — no se crea un correlativo nuevo.

---

## 2. Estructura obligatoria

1. **Encabezado** — ID, paso probado, pantalla, fecha, ambiente, empresa de prueba, usuario, **alcance** y objetivo.
2. **Resumen ejecutivo** — veredicto de cobertura, **veredicto del ciclo completo**, **qué datos quedaron cargados**, conteo de hallazgos de frontend por severidad **y conteo de hallazgos de backend**.
3. **Contrato de referencia (backend)** — endpoints, autorización, respuesta observada, **reglas de negocio** y **catálogo de errores**, marcando cuáles se verificaron en vivo.
4. **Cobertura de campos** — tabla campo API ↔ etiqueta ↔ `id` del input ↔ obligatoriedad ↔ regla backend ↔ estado. Más una sección de **lo que está bien hecho**.
5. **Validación en cliente** — batería ejecutada, con una columna explícita de *«¿coincide con el backend?»*.
6. **Ciclo completo** — resultado de crear / leer / editar / dar de baja, más los caminos de error que solo aparecen al escribir: duplicado, concurrencia (`If-Match` ausente y desactualizado), y lo que revelen las cabeceras de respuesta.
7. **Hallazgos de frontend** (`F-NN`) — con el formato de §3.
8. **Hallazgos de backend** (`B-NN`) — con el formato de §4. Si no hubo, se pone «Ninguno en esta corrida».
9. **Qué NO se probó** — explícito, para que nadie lo dé por validado.
10. **Estado en que queda la empresa** — los datos cargados, con sus códigos y valores, y la verificación en el asistente `/setup`.
11. **Prioridad sugerida** — **dos tablas separadas**, una de frontend y otra de backend, indicando en la de frontend si el arreglo depende de algún `B-NN`.

---

## 3. Formato obligatorio de cada hallazgo de frontend

Cada hallazgo lleva un ID correlativo dentro del documento (`F-01`, `F-02`, …), severidad y tipo, y **estas cuatro secciones**:

```markdown
### <severidad> F-NN — <título en una línea>

**Severidad:** Alta | Media | Baja | Informativo · **Tipo:** <categoría>

#### Evidencia (frontend)
Qué se observó, con el dato crudo: fragmento del DOM, del bundle,
de la petición de red, o el mensaje literal en pantalla.

#### Regla de negocio que lo gobierna
Qué dice el backend sobre esto y por qué el comportamiento actual
es incorrecto (o correcto pero confuso).

#### Contrato para el frontend        ← OBLIGATORIO en TODOS los hallazgos
El endpoint exacto, el cuerpo de la petición, la forma de la respuesta,
los códigos de error aplicables y las reglas operativas.
Si no interviene ningún endpoint, hay que decirlo con esas palabras.

#### Ajuste pedido al frontend
Qué hay que cambiar concretamente. Y si el arreglo depende del
backend, decirlo explícitamente y por separado.
```

### El bloque de contrato va en TODOS los hallazgos, sin excepción

Es la razón de ser de esta carpeta: el frontend tiene que poder ajustar **sin leer código C#**. Un hallazgo sin contrato obliga al lector a ir a buscarlo, que es justo lo que estos documentos existen para evitar.

**Cuando el hallazgo toca datos o el servidor**, el bloque lleva las cinco cosas:

| # | Qué | Por qué |
|---|---|---|
| 1 | **Endpoint exacto**, con sus parámetros de consulta | Sin él hay que adivinar la ruta |
| 2 | **Cuerpo de la petición**, con tipos y obligatoriedad | Es donde se ve qué campo falta o sobra |
| 3 | **Forma de la respuesta** | Muchas veces demuestra que el dato **ya llega** y solo falta pintarlo |
| 4 | **Códigos de error aplicables**, y a qué campo corresponde cada uno | Permite mapear el error a su input |
| 5 | **Reglas operativas**: rate limit, paginación, `If-Match`, mínimos de búsqueda | Son las que muerden después de desplegar |

**Cuando el hallazgo es presentación pura** —un `aria-required`, un `min="0"`, un diálogo de confirmación— el bloque dice **«No interviene ningún endpoint»** y, si aplica, señala de dónde sale el valor:

```markdown
#### Contrato para el frontend

**No interviene ningún endpoint** — es presentación pura. Pero la fuente de
verdad del atributo es la regla del validador:
`RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0)` → `min="0"`.
```

Decirlo explícitamente **no es relleno**: distingue «aquí no hace falta contrato» de «se nos olvidó ponerlo», que es la duda que el lector no debería tener que resolver.

### Reglas de redacción

- **Ningún hallazgo se cierra sin su bloque de contrato.** Antes de dar por terminado el documento, se repasa hallazgo por hallazgo que lo tenga — con endpoints, o con la declaración de que no interviene ninguno.
- **Numerar en orden de lectura.** Los hallazgos se ordenan por severidad dentro del documento, y los IDs siguen ese mismo orden: `F-01` es el primero que aparece y `F-NN` el último. Si al agregar un hallazgo cambia el orden, **se renumera** — un ID que no se encuentra donde el lector lo busca es un ID inservible.
- **Separar lo que el frontend puede arreglar hoy** de lo que **requiere un cambio en el backend**. Si un hallazgo tiene las dos cosas, van en párrafos distintos, y el cambio de backend se levanta además como un `B-NN` propio.
- **Citar la fuente de verdad** al enunciar una regla: nombre de la clase, del validador o del archivo de configuración. El frontend debe poder verificarlo sin preguntar.
- **Marcar los endpoints que NO se deben usar** y explicar por qué (p. ej. un endpoint cuya autorización es por propiedad de la empresa y no sirve para usuarios normales). Ahorra implementaciones equivocadas.
- **Documentar las trampas** aunque no haya síntoma, con severidad `Informativo`. Sirven para que nadie «optimice» hacia el error después.
- **Reconocer lo que está bien hecho.** El documento se lee mejor y evita que se rompa algo correcto al arreglar otra cosa.
- **Consignar la causa raíz compartida.** Si dos hallazgos se resuelven con el mismo cambio, decirlo en ambos.

---

## 4. Hallazgos de backend — solo el puntero

Se prueba el frontend, pero se prueba **contra** el backend. Cuando aparezca una falla o una mejora del servidor, se levanta como hallazgo propio — **aunque el frontend tenga una vía alterna y no quede bloqueado**. Casi siempre es deuda transversal que reaparecerá en otras pantallas.

**El detalle NO se escribe en este documento.** Va al **documento espejo del mismo paso** en [`../ComentariosPruebasBackend/`](../ComentariosPruebasBackend/README.md), con el formato, la numeración y los estados definidos allá. Así el hallazgo tiene un solo dueño, un solo lugar donde se actualiza su estado, y no se desincroniza entre dos archivos.

Aquí queda **solo una tabla de punteros**:

```markdown
## <N>. Hallazgos de backend

Detectados desde el frontend durante esta corrida, pero cuya causa y
solución están del lado del servidor.

> El detalle completo vive en el documento espejo
> `ComentariosPruebasBackend/<NNNNN>-<NombrePantalla>.md`.

| ID | Sev. | Hallazgo | Origen | Alcance |
|---|---|---|---|---|
| **B-NN** | 🟡 Media | <resumen de una línea> | F-NN | <Un feature / Transversal> |
```

Los IDs `B-NN` son correlativos **dentro del documento espejo**, igual que los `F-NN` lo son dentro de este. Para citarlos desde otro paso se antepone el ID del paso: **`00001 / B-02`**.

Si no hubo hallazgos de backend, se pone **«Ninguno en esta corrida»** — la sección no se omite, para que se note que sí se buscaron.

---

## 5. Severidades

| | Criterio |
|---|---|
| 🔴 **Alta** | Bloquea acceso, o hay divergencia de contrato entre cliente y servidor que produce un fallo que el usuario no puede diagnosticar |
| 🟡 **Media** | No bloquea, pero degrada de forma medible el uso o la accesibilidad |
| 🔵 **Baja** | Pulido, riesgo poco probable, o mensaje mejorable |
| ⚪ **Informativo** | Sin defecto observado; se documenta una trampa o una regla no evidente |

---

## 6. Toda ausencia se juzga, no solo se reporta

**Decir «este campo no está» o «este verbo no existe» es media observación.** La otra media —la que sirve— es **si debería estar**. Sin ella, el hallazgo le traslada el trabajo al lector, y el lector no tiene el contexto que tuvo quien lo midió.

La regla aplica a **todo lo ausente**: campos del formulario, columnas de la tabla, verbos del contrato, endpoints, acciones de la pantalla, estados, validaciones.

### Las tres salidas, y las tres se escriben

| Veredicto | Qué se escribe |
|---|---|
| **Sí debería estar** | Un hallazgo, con **la razón por la que hace falta** — no solo la constancia de que el contrato lo acepta |
| **No debería estar hoy** | Un hallazgo ⚪ **Informativo** con la **justificación** y, si la ausencia es temporal, **de qué depende** para dejar de serlo |
| **No se sabe** | Un pendiente de medición con su montaje (§8). **No se rellena con una suposición** |

La segunda fila no es opcional. Una ausencia correcta **documentada** es una decisión; sin documentar es indistinguible de un olvido, y en la corrida siguiente alguien la vuelve a levantar.

### Justificar no es razonar hacia atrás

El riesgo real de esta regla es escribir la conclusión primero y buscarle la razón después. Se distingue así:

> **Análisis**: se busca el caso que la ausencia *deja sin resolver*, y se comprueba si existe.
> **Racionalización**: se busca un caso que la ausencia *protege*, y se declara suficiente.

**Caso real de este repositorio.** En el Paso 2 se escribió que la falta de `DELETE` en tipos de unidad era «a propósito», porque borrar un tipo referenciado dejaría huérfanas sus referencias. El argumento es cierto y es irrelevante: **para el elemento referenciado ya existe un guard** (`ORG_STRUCTURE_CATALOG_IN_USE`), y el caso que la ausencia deja sin resolver es el contrario —el registro tecleado mal que nadie referenció nunca y que no se puede quitar jamás—. Quedó levantado como [00003 / B-04](../ComentariosPruebasBackend/00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca).

### Las cuatro preguntas antes de dar una ausencia por buena

1. **¿Qué caso concreto queda sin resolver?** Si se puede nombrar —«un código tecleado mal en la configuración inicial»—, la ausencia **no** es correcta.
2. **¿El producto lo resuelve en otro lado?** Un módulo hermano con el campo, el verbo o el endpoint es la prueba más fuerte que hay: **elimina el argumento de «no se puede» y da la implementación de referencia**. Se busca con `grep` sobre los controladores antes de concluir.
3. **¿La pantalla vecina lo tiene?** Más fuerte todavía. Si dos pestañas de la misma pantalla difieren, no es diseño: es inconsistencia. Pasó dos veces en esta corrida — con `q`/`Search` y con `/usage`.
4. **¿La ausencia depende de un paso posterior?** Entonces es correcta **hoy** y se anota con la dependencia explícita, no como defecto. Es el único caso en que «no está» y «está bien» conviven.

### Cómo se cierra el hallazgo

Todo hallazgo de ausencia lleva una línea que empiece por **«¿Debería estar?»** con la respuesta y su razón. Si la respuesta es que no, el hallazgo baja a ⚪ Informativo y **cambia de título**: deja de nombrar una carencia y pasa a nombrar una decisión.

> **Un campo legítimamente diferido nunca se mete dentro de un hallazgo 🔴 Alta.** Contradice su propia conclusión y contamina la severidad. Se separa en su propio ⚪ Informativo, aunque el campo salga del mismo formulario.

---

## 7. No se documenta lo que otra pantalla hace bien

Es frecuente descubrir que **una pantalla resuelve bien lo que otra resuelve mal**. La tentación es anotarlo en el documento de la pantalla buena. **No se hace.**

El motivo es de ubicación, no de contenido: **quien vaya a arreglar `00003 / F-04` abre el documento `00003`.** Nunca va a abrir el `00004` para enterarse de que existe una implementación de referencia. La nota queda donde el único que la necesita no la va a ver, y a cambio infla el documento con material que no le dice nada a quien sí lo está leyendo.

### Qué hacer en su lugar

| Situación | Dónde va |
|---|---|
| Otra pantalla ya implementa el arreglo | **En el hallazgo**, dentro de «Ajuste pedido al frontend»: una línea con la pantalla y qué copiar |
| El patrón se repite en varias pantallas | **En el hallazgo**, dentro de «Alcance a revisar» |
| La pantalla nueva simplemente funciona bien | **En ningún lado.** Que algo funcione no es un hallazgo |

> **La prueba a aplicar antes de escribir un párrafo:** *¿quién lo va a leer, y qué va a hacer distinto después de leerlo?* Si la respuesta es «nadie en particular» o «nada», sobra. Un documento de hallazgos no es una crónica de la corrida.

**Y en ningún caso el estado de un hallazgo cambia desde otro documento.** Vive en el suyo, y solo se cierra volviendo a medir su propia pantalla — ni la evidencia cruzada, ni la lectura del código, ni que alguien diga que ya se arregló.

---

## 8. Todo se mide — cómo, y qué hacer cuando parece imposible

La sección «Qué NO se probó» **no es una lista de excusas**. Cada línea lleva **cómo se va a medir** y **cuándo**. Un pendiente sin plan es un hallazgo que nadie va a volver a mirar.

### El montaje casi siempre existe

Estos son los que ya funcionaron en las corridas 1 a 3. Antes de declarar algo no medible, hay que descartarlos todos:

| Montaje | Para qué sirve | Ejemplo real |
|---|---|---|
| **Sonda garantizada a fallar** | Medir el contrato de error **sin escribir nada**: un `400`/`409`/`422` no persiste | `POST` con `code:""` y `sortOrder:-1` reveló la forma exacta del `400` sin crear un registro |
| **Par diferencial** | Aislar la causa: dos peticiones que difieren en **una sola cosa** | `q=a` → `400` y `Search=a` → `200` probó que el servidor descarta `Search`, sin ambigüedad |
| **Leer cabeceras sin descargar** | Verificar `Content-Disposition`, `ETag`, `Location` sin guardar archivos | `fetch()` al export y leer `r.headers` — así se cerró 00001 / B-03 |
| **Interceptar la API del navegador** | Observar lo que la interfaz **haría** sin dejar que ocurra | Envolver `HTMLAnchorElement.click` para capturar el nombre de archivo sin bajarlo |
| **Ciclo reversible** | Probar una transición de estado dejando todo como estaba | Inactivar una **hoja** → verificar → reactivar. No sirve con un nodo con hijos: elegir el caso reversible |
| **Dato desechable con vuelta atrás** | Cuando hace falta persistir para medir | Crear, medir, y devolver el registro a su estado original con un `PUT` |
| **Leer la respuesta que ya se emite** | Muchas veces el dato **ya llega** y solo falta mirarlo | El listado de unidades ya trae `parent`, `functionalArea` e `isActive` |
| **Empresa desechable** | Cuando el montaje ensuciaría la empresa buena | Cadenas profundas, catálogos rotos, usuarios con permisos recortados |

### Cuando medir ensucia la empresa buena

Algunas medidas exigen datos que después **no se pueden borrar** —los catálogos solo tienen baja lógica—. La respuesta **no es saltarse la medición**: es **hacerla en una empresa desechable** y anotar en qué empresa se midió.

**Nunca se ensucia la empresa del escenario con datos de prueba** que no se puedan revertir.

### Las dos únicas excepciones admitidas

1. **Depende de un insumo externo que no controlamos** — una plantilla oficial, una credencial de un tercero, un dato que solo tiene el cliente. Se nombra **el insumo y quién lo tiene**.
2. **Depende de un paso posterior de la propia secuencia** — no se puede probar el `409` de «tipo en uso» antes de que exista algo que lo use. Se nombra **el paso concreto** que lo desbloquea, y ese paso hereda la medición como tarea.

Cualquier otra cosa —«requiere muchos registros», «el usuario es OWNER», «la interfaz no lo expone»— **no es excepción, es un montaje que falta**.

### Cómo se escribe un pendiente legítimo

```markdown
| Escenario | Cómo se va a medir | Cuándo |
|---|---|---|
| `409 ORG_UNIT_IN_USE` | Crear una plaza que use el tipo y reintentar la baja | Paso 7 |
| Manejo de `403` | Rol sin `OrgUnits.Admin` en una empresa desechable | Corrida dedicada |
```

Nunca así:

```markdown
| Manejo de `403` | El usuario es OWNER |          ← esto es una excusa, no un plan
```

---

## 9. Reglas de la corrida de pruebas

### Cómo se ejecuta

- **Ciclo completo, no solo lectura.** Crear, leer, editar y dar de baja por la **interfaz**, que es lo que se está probando. Cargar datos en volumen por API es aceptable **después** de haber ejercitado el formulario al menos una vez, y se declara en el documento.
- **Datos del escenario AVIANCA**, con sus códigos y nombres reales. Nada de `test`, `aaa` ni `prueba1`: los datos de relleno esconden defectos que solo aparecen con longitudes, acentos y caracteres reales.
- **Ejercitar los caminos de error que solo existen al escribir**: código duplicado, `If-Match` ausente y desactualizado, validación del servidor. Y **mirar las cabeceras de respuesta** — ahí aparecieron dos hallazgos que el cuerpo no mostraba.
- **La empresa queda operativa.** Al terminar el paso se verifica en `/setup` que el progreso avanzó y que el paso siguiente quedó disponible.

### Qué no se hace

- **No se tocan las filas de catálogo de sistema** (IDs negativos `-9000…-9969`). Están sembradas por migración, las comparte todo el país y el motor de planilla las referencia por código. Ver §0 de la guía AVIANCA.
- **No se borra desde la base de datos.** Si un catálogo necesita limpieza, se usa la inactivación lógica que la pantalla ofrece.
- Si se cambia alguna preferencia local para probar (idioma, tema), **se restaura** y se deja constancia en el documento.

### Al cerrar

- **Repasar que cada `F-NN` lleve su «Contrato para el frontend».** Es el error más fácil de cometer: se escribe la evidencia, se escribe el ajuste, y el contrato se queda fuera porque «se entiende». No se entiende — el lector no tiene el código delante.
- Lo que no se pudo probar **se declara** en la sección «Qué NO se probó», con el motivo. Nunca se infiere un resultado.
- **Los hallazgos de backend se escriben en el documento espejo `../ComentariosPruebasBackend/<mismo nombre>.md`** y se enlazan desde la tabla de punteros. El documento no se da por terminado con un `B-NN` citado que no exista.
- **Si la corrida mide el alcance de un hallazgo anterior, se actualiza aquella ficha** —su §«Alcance a revisar», su bitácora y, si corresponde, su severidad— y se deja constancia en el documento nuevo. Un hallazgo cuyo alcance se confirma deja de ser sospecha y cambia de prioridad.
