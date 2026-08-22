# 00950 — Remediación · Hallazgos que aparecieron al arreglar, no al probar

| | |
|---|---|
| **ID** | 00950-Remediacion |
| **Documento espejo** | **No tiene** — ninguno de estos se ve desde una pantalla |
| **Origen** | Trabajo de remediación de los hallazgos de backend, 2026-08-16 |
| **Fecha** | 2026-08-16 |
| **Ambiente** | Repositorio `CLARIHR-backend` |

---

## Por qué este documento existe

Los ocho documentos numerados de esta carpeta siguen la corrida de pruebas: un paso del asistente, una pantalla, sus hallazgos. Estos dos **no salieron de probar la aplicación, salieron de arreglarla**.

No pertenecen a ninguna pantalla y no se detectan desde ninguna. Meterlos en el documento del paso donde casualmente se trabajaba los haría invisibles para quien busque por pantalla, y falsos para quien busque por origen.

> **Se numera `00950`** para que quede después de los pasos y antes del índice del BFF, sin desplazar nada publicado.

---

## 1. Resumen

| ID | Sev. | Hallazgo | Componente | Alcance | Estado |
|---|---|---|---|---|---|
| [B-01](#2-b-01--el-contrato-publicado-puede-desfasarse-del-código-sin-que-nada-lo-detecte) | 🔴 Alta | `openapi.yaml` puede desfasarse del código sin que ninguna prueba lo detecte — **y ya causó daño medible** | Docs · Guardrails · CI | **Transversal** | 🟢 Resuelto |
| [B-02](#3-b-02--se-puede-dotar-de-personal-un-departamento-inactivo) | 🟡 Media | Se puede dotar de personal un departamento inactivo: la cadena unidad → perfil → plaza no tiene ni un guard | Application · OrgUnits · JobProfiles · PositionSlots | Tres saltos | 🟢 Resuelto |

**B-01 es el más valioso de toda la corrida de remediación**, y no por su severidad nominal: es la **causa probable** de un bloqueo que se arrastró toda la sesión.

---

## 2. B-01 — El contrato publicado puede desfasarse del código sin que nada lo detecte

| | |
|---|---|
| **Severidad** | 🔴 Alta |
| **Estado** | 🟢 **Resuelto** — 2026-08-16, verificado (§2.10) |
| **Componente** | `docs/technical/api/openapi.yaml` · guardrails de contrato · CI |
| **Origen** | Al sincronizar el contrato tras implementar [00003 / B-04](00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca) |
| **Alcance** | **Transversal** — todo el contrato público |

### 2.1 Evidencia

Al comparar el swagger que genera el código con el archivo versionado, el 2026-08-16:

| | |
|---|---|
| Rutas en el código | 568 |
| Rutas en `openapi.yaml` | **559** |
| **Ausentes del archivo** | **9 rutas · 14 esquemas** |
| Rutas eliminadas del código pero aún documentadas | 0 |

Y **entre las nueve ausentes estaban estas tres**:

```
/api/v1/job-profiles/{publicId}/publication
/api/v1/job-profiles/{publicId}/reopening
/api/v1/job-profiles/{publicId}/archival
```

**Son exactamente las tres que el proxy no enruta** y que bloquean los Pasos 7 y 8 de la configuración guiada — el hallazgo [00006 / B-01](00006-JobProfiles.md#2-b-01--los-tres-endpoints-de-transición-de-estado-no-están-enrutados-por-el-proxy).

### 2.2 La cadena causal, verificada

```
2026-08-09  commit 13907ca «Implement job profile state transitions»
            └─ crea JobProfileResolutionController.cs entero (126 líneas, 3 endpoints)
            └─ actualiza endpoint-reference.md y definiciones-tecnicas-backend.md
            └─ NO toca openapi.yaml                                    ← el desfase nace aquí
                └─ ninguna prueba lo detecta                            ← el defecto de este hallazgo
                    └─ el BFF no enruta lo que no está en el contrato   ← causa probable de 00006/B-01
                        └─ Pasos 7 y 8 inalcanzables
```

Verificado: `git show --name-only 13907ca | grep -c openapi.yaml` → **0**. Y de los cuatro commits recientes revisados, **dos tocaron `openapi.yaml` y dos no**, sin criterio aparente.

> ⚠️ **El último eslabón sigue siendo hipótesis.** Que el BFF mantenga su tabla de reenvío contra este documento es lo coherente, pero no está comprobado — el proxy vive en otro repositorio. Lo que **sí** está probado es que el contrato publicado omitía esos tres endpoints durante siete días.

### 2.3 Causa

**El guardrail que existe no compara lo que hay que comparar.** `OpenApiContractGuardrailsIntegrationTests` pide `/swagger/v1/swagger.json` **al propio host de pruebas** y verifica etiquetas y resúmenes del documento **generado**. Es decir: compara el código consigo mismo. Nunca abre `docs/technical/api/openapi.yaml`.

Y no hay ninguna otra prueba que lo haga:

```bash
$ grep -rn "openapi.yaml" tests/ --include="*.cs"
(sin resultados)
```

**El archivo es salida de compilación versionada como si fuera fuente.** Versionar salidas siempre acaba así: se desfasa, nadie la revisa, y crece sin techo.

### 2.4 El tamaño convierte el problema en estructural

Medido sobre el archivo actual:

| | Hoy | Extrapolado a 10 000 operaciones |
|---|---|---|
| Operaciones | 901 | 10 000 |
| Tamaño | 2.7 MB | **~30 MB** |
| Líneas | 88 964 | **~987 000** |

A esa escala el documento **no es revisable por nadie**: ni en una revisión de código, ni en un diff, ni abriéndolo. Cualquier estrategia que dependa de que alguien lo mire falla por construcción.

### 2.5 Propuesta

**Versionar una huella del contrato, no el documento.** Una línea por operación:

```
GET /api/v1/account/companies 200/400/401 9402638e597c
POST /api/v1/account/companies 201/400/401/409 3b1c7f0a8d42
```

Ruta, verbo, códigos de respuesta y un hash de la forma —parámetros, cuerpo y respuestas—. Medido sobre el contrato real:

| | Documento | Huella | |
|---|---|---|---|
| Hoy | 2.7 MB | **95 KB** | **29× menor** |
| 10 000 operaciones | ~30 MB | **~1 MB** | |

Y con eso **el guardrail sí puede verificar**: regenera la huella desde el código y la compara con la versionada. Un endpoint nuevo sin actualizar la huella **rompe la corrida**, con un diff de una línea que dice exactamente qué falta.

**Las alternativas consideradas, y por qué no:**

| Estrategia | Escala | ¿Detecta desfase? |
|---|---|---|
| **Huella versionada + guardrail** | ✅ 1 MB a 10k | ✅ **sí** |
| Generar en CI y publicar sin commitear | ✅ | ❌ no por sí sola |
| Partir por dominio con `$ref` externos | ⚠️ 30 MB repartidos | ❌ **no** — hace el diff revisable, no verificable |
| Subconjuntos por consumidor | ✅ | ✅ para lo que se usa |

La de partir por dominio es la que suele proponerse primero y es la que menos resuelve: seguiríamos sin detectar que falta un endpoint.

La de subconjuntos por consumidor tiene una virtud propia: **escala con el uso, no con el tamaño del API**. El frontend consume del orden de 300 de las 901 operaciones. Es complementaria, no sustituta.

**Recomendado:** la huella ahora; el documento completo se conserva como referencia legible pero **deja de ser autoritativo**.

### 2.6 El procedimiento de regeneración tampoco existía

Al ir a sincronizar el contrato no había forma documentada de hacerlo: sin manifiesto de herramientas, sin nota en las definiciones técnicas, sin script. La memoria del proyecto ya lo tenía anotado como pendiente — *«herramienta original no documentada»*.

Se resolvió con un volcador (`SwaggerDumpTests`, **inerte** salvo con `CLARIHR_DUMP_SWAGGER=1`), pero **el procedimiento hay que escribirlo**: forma parte del arreglo, no es un extra.

### 2.7 Compatibilidad

**No rompe nada.** Añade un archivo de huella y un guardrail. El `openapi.yaml` se conserva.

⚠️ **La primera corrida del guardrail nuevo va a fallar** si el archivo versionado no está sincronizado — que es exactamente su propósito. Hay que sincronizar antes de activarlo.

### 2.8 Vía alterna vigente

Ninguna. Hoy el desfase solo se detecta si alguien lo compara a mano, y en siete días nadie lo hizo.

### 2.10 Lo que se hizo

**Cuatro piezas.** La huella es la que protege; el resto la hace utilizable.

| Pieza | Qué es |
|---|---|
| `docs/technical/api/contract-fingerprint.txt` | **901 operaciones · 95 KB.** Versionada y autoritativa |
| `tests/…/ContractFingerprint.cs` | **Implementación única** de generación y verificación |
| `tests/…/ContractFingerprintGuardrailsTests.cs` | Compara contra el código y **rompe la corrida** si hay desfase, con el diff línea por línea |
| `docs/technical/operations/regenerar-contrato.md` | El procedimiento, que no existía |

**El hash excluye `summary`, `description` y `tags` a propósito.** Reescribir una descripción no cambia el contrato; si lo contara, cada ajuste de redacción obligaría a regenerar la huella y el guardrail se volvería ruido que la gente aprende a ignorar — peor que no tenerlo. Está medido: al comparar el `openapi.yaml`, de **15 rutas «distintas» 12 eran reflujo de línea** en descripciones y solo 3 tenían diferencia real de contrato.

**Un endpoint sin actualizar la huella rompe la corrida.** Eso es lo que no existía: el guardrail anterior pedía el swagger al propio host y comprobaba etiquetas — comparaba el código consigo mismo.

#### La trampa que casi se cuela, y que este hallazgo denuncia

El primer intento tenía **la generación en un script de Python y la verificación en C#**. Las 901 operaciones salieron como ausentes. Verificado sobre la misma operación:

```
GET /api/v1/account/companies 200/400/401 893bc0af8150   ← Python: serializa los objetos parseados
GET /api/v1/account/companies 200/400/401 9402638e597c   ← C#: serializa el texto JSON crudo
```

**Dos implementaciones del mismo hash divergen siempre**, que es exactamente el defecto que la huella existe para evitar. Si no hubiera fallado ruidosamente, el guardrail habría comparado cosas distintas y alguien lo habría desactivado la primera vez que rompiera sin motivo.

El arreglo: el **mismo test** escribe (`CLARIHR_WRITE_FINGERPRINT=1`) y verifica. El archivo no se puede generar por una vía distinta de la que lo comprueba.

#### Lo que queda recomendado y no hecho

**Dejar de versionar `openapi.yaml`.** La huella cubre la verificación; el documento solo hace falta como referencia, y una referencia se sirve, no se commitea. Mientras se versione, sincronizarlo seguirá siendo manual: el generador original no está identificado y su estilo no se reproduce — la conversión directa da **87 908 líneas** de reformateo, y ajustando estilo baja a ~1 200 pero sigue sin coincidir.

**Publicar subconjuntos por consumidor.** El frontend usa del orden de 300 de las 901 operaciones. Escala con el uso, no con el tamaño del API.

### 2.9 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-16 | 🔲 Propuesto | Detectado al sincronizar el contrato tras implementar 00003 / B-04. **No es un hueco teórico: los tres endpoints de publicación llevaban ausentes desde el 9 de agosto**, y es la causa probable de que el BFF no los enrute |
| 2026-08-16 | 🟢 Resuelto | Huella versionada + guardrail + procedimiento (§2.10). **La primera versión tenía dos implementaciones del hash y fallaba las 901 operaciones**; resuelto con implementación única |

---

## 3. B-02 — Se puede dotar de personal un departamento inactivo

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-18, verificado (§3.9). Decisión de producto tomada: **no se permiten plazas nuevas contra unidad inactiva** |
| **Componente** | Application · `OrgUnits` · `JobProfiles` · `PositionSlots` |
| **Origen** | Al contar los perfiles de puesto para el `/usage` de [00003 / B-04](00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca) |
| **Alcance** | **Tres saltos de la cadena**, ninguno protegido |

### 3.1 Evidencia

Los tres eslabones que llevan de una unidad organizativa a una persona asignada, y qué comprueba cada uno:

| Salto | Guard existente | ¿Mira el estado de la unidad? |
|---|---|---|
| **Inactivar una unidad** | `HasActiveChildrenAsync` — solo hijos activos | — |
| **Crear un perfil sobre una unidad** | Solo que la unidad exista | ❌ **no** |
| **Crear una plaza contra el perfil** | Solo `JobProfileStatus == Published` | ❌ **no** |

Verificado en el código:

- El handler de inactivación (`OrgUnitAdministration.cs`) comprueba **únicamente** `HasActiveChildrenAsync`. No consulta perfiles de puesto.
- No existe ningún error tipo `JOB_PROFILE_ORG_UNIT_INACTIVE`: el catálogo de `JobProfiles` solo tiene `JOB_PROFILE_ORG_UNIT_NOT_FOUND` y `JOB_PROFILE_ORG_UNIT_REQUIRED`.
- El lookup de plazas (`PositionSlotAdministration.cs:1512`) devuelve éxito o `JobProfileNotPublished`, y nada más.

**Consecuencia:** se inactiva un departamento, sus perfiles siguen válidos y publicados, y se pueden crear plazas nuevas contra ellos. Personal asignado a un departamento que oficialmente no opera.

### 3.2 Lo que NO se ha comprobado, y es la mitad de la pregunta

**Puede que el primer salto sea deliberado.** Una unidad inactiva podría entenderse como «archivada pero históricamente válida», y que sus plazas existentes sigan funcionando sería entonces correcto — cerrar un departamento no debería invalidar el histórico de quien trabajó en él.

Lo que **no** tiene defensa evidente es el tercer salto: **crear plazas nuevas** contra una unidad de baja. Eso no preserva histórico, crea futuro.

Por eso se levanta con la pregunta abierta y no como defecto cerrado. **La decisión de producto es previa al arreglo:**

| Pregunta | Quién responde |
|---|---|
| ¿Una unidad inactiva puede conservar plazas ocupadas? | Producto |
| ¿Puede aceptar plazas **nuevas**? | Producto — aquí la respuesta esperable es no |
| ¿Inactivar una unidad debería bloquearse si tiene perfiles activos, o arrastrarlos? | Producto |

### 3.3 Impacto

**No hay evidencia de que nadie lo haya topado**, y por eso es Media y no Alta. El daño es de integridad futura: una estructura organizativa donde el estado de una unidad no significa lo que parece.

Y tiene un efecto colateral medible: **el tablero de indicadores y los reportes de planilla agrupan por unidad**. Una plaza activa bajo una unidad inactiva es una fila que no cuadra con ninguna lectura del organigrama.

### 3.4 Propuesta

**Depende de la decisión de §3.2.** Si se confirma que crear plazas nuevas contra una unidad inactiva no debe permitirse, el arreglo mínimo es un guard en el salto donde el daño se materializa:

```
POST /position-slots   →  422 POSITION_SLOT_ORG_UNIT_INACTIVE
```

Es el punto más barato y el más preciso: no toca la inactivación —que puede ser legítima— ni invalida perfiles existentes, y bloquea exactamente lo que no tiene defensa.

**El dato ya está disponible sin consultas nuevas:** el lookup de plazas ya trae el perfil y su unidad para validar la publicación.

### 3.5 Compatibilidad

**Rompe escrituras que hoy pasan** — que es el objetivo. Ninguna de ellas era válida bajo la regla que se propone.

⚠️ **Antes de desplegarlo hay que contar los datos que ya están así.** Una consulta que liste plazas activas cuya unidad esté inactiva, por empresa. Sin eso, se podría dejar a alguien con plazas que ya no se pueden editar.

### 3.6 Alcance a revisar — **medido el 2026-08-18**

**El patrón, no el caso.** La pregunta general era: *¿qué otras cadenas del producto permiten crear un hijo activo bajo un padre inactivo?*

**Respuesta: el patrón ya es convención en el repositorio, y esta cadena era la excepción.** Existen cuatro guards de esta forma ya construidos:

| Código de error existente | Bloquea |
|---|---|
| `COST_CENTER_TYPE_INACTIVE` | centro de costo bajo tipo de baja |
| `JOB_CATALOG_ITEM_INACTIVE` | referencia a ítem de catálogo de baja |
| `LOCATION_GROUP_PARENT_INACTIVE` | grupo de ubicación bajo padre de baja |
| `WORK_CENTER_TYPE_INACTIVE` | centro de trabajo bajo tipo de baja |

Eso reencuadra el hallazgo: `POSITION_SLOT_ORG_UNIT_INACTIVE` **no introduce una regla nueva, cierra una omisión** en la única cadena que no la tenía. Y elimina la objeción de coherencia: el frontend ya recibe 422 de esta forma en cuatro sitios.

#### La misma brecha, un padre distinto — 🟢 **cerrada el 2026-08-21**

`ResolveWorkCenterInternalIdAsync` (`PositionSlotAdministration.cs:1538`) resuelve el centro de trabajo llamando a `ResolveWorkCenterIdAsync`, y esa consulta filtra **solo por tenant y `PublicId`**:

```csharp
.Where(center => center.TenantId == tenantId && center.PublicId == workCenterId)
```

**No consulta `IsActive`.** Se puede crear una plaza apuntando a un centro de trabajo de baja, exactamente igual que se podía con la unidad organizativa.

**Se levantó sin arreglar a propósito**, porque cerrar un centro de trabajo es un hecho físico —una sede que dejó de operar— y podía tener otra semántica que dar de baja una unidad organizativa. **Producto decidió lo mismo el 2026-08-21: no permitir plazas nuevas contra un centro de baja.**

La asimetría que delataba la omisión: `WORK_CENTER_TYPE_INACTIVE` ya existía, así que el **tipo** de centro estaba protegido y el **centro** no.

##### Lo que se hizo

| Pieza | Dónde |
|---|---|
| Error `POSITION_SLOT_WORK_CENTER_INACTIVE` (422) | `PositionSlots/Common/PositionSlotCommon.cs` |
| `ResolveWorkCenterIdAsync` devuelve también `IsActive` | `IPositionSlotRepository` + `PositionSlotRepository.cs:96` |
| El guard | `ResolveWorkCenterInternalIdAsync` |
| Mensajes en inglés y español | `BackendMessages.resx` · `.es.resx` |
| Diagnóstico previo al despliegue (§4 del script) | `diagnostico-plazas-unidad-inactiva.sql` |

**El resolvedor tuvo que cambiar de forma, no solo de lógica.** Devolvía `Task<long?>`: con eso no se puede distinguir «no existe» de «existe pero está de baja». Ahora devuelve `(long Id, bool IsActive)?` en la misma consulta. Un solo call site, así que el cambio es contenido — y como vive en el único resolvedor, **cubre crear y actualizar** sin depender de que alguien lo recuerde en un call site nuevo.

**Rojo antes de verde:** sin el guard la plaza se creaba (`Expected: UnprocessableEntity · Actual: Created`) y el contrapeso con centro activo pasaba.

**Verificado:** build 0 errores / 0 advertencias · unitarias **3031/3031** · integración `~PositionSlot` + huella del contrato **46/46**. La huella no cambió: el `422` ya estaba declarado en esa operación.

⚠️ **Antes de desplegar**, §4 del diagnóstico. Sobre `clarihr_dev` da 0 filas, con la misma salvedad de siempre: los 5 centros de esa base están **todos activos**, así que el vacío dice «no hay a quién romper aquí», no «se revisó un universo poblado».

Los tres dobles de prueba que implementan el repositorio se actualizaron a la firma nueva. El de `PositionSlotAdministrationTests` sigue devolviendo `null` —«ese centro no existe», que es lo que ejercita— y **no** inventa un centro inactivo: eso lo cubre la prueba de integración contra la consulta real, donde un valor fabricado solo diría que el tuple compila.

Pendiente de medir con la misma forma: perfil sobre categoría inactiva, expediente sobre plaza suspendida.

### 3.7 Vía alterna vigente

Ninguna en el servidor. El frontend puede filtrar unidades inactivas en el combo de perfiles —cosa que conviene igualmente— pero eso no cierra el API.

### 3.9 Lo que se hizo

**La decisión de producto llegó primero**, como exigía §3.2: *no permitir plazas nuevas contra unidad inactiva*. Los otros dos saltos quedan como estaban — inactivar una unidad sigue siendo legítimo y los perfiles existentes siguen válidos.

| Pieza | Archivo |
|---|---|
| Error `POSITION_SLOT_ORG_UNIT_INACTIVE` (422) | `PositionSlots/Common/PositionSlotCommon.cs:162` |
| Campo `bool? OrgUnitIsActive` en el lookup | `PositionSlots/PositionSlotAdministration.cs` |
| Proyección que lo alimenta | `Infrastructure/PositionSlots/PositionSlotRepository.cs:396` |
| El guard | `ResolveJobProfileLookupAsync` |
| Mensajes en inglés y español | `Localization/BackendMessages.resx` · `.es.resx` |
| Diagnóstico previo al despliegue | `docs/technical/operations/scripts/diagnostico-plazas-unidad-inactiva.sql` |

#### Tres decisiones de implementación que conviene conocer

**El guard vive en el resolvedor del lookup, no en el handler de creación.** §3.4 proponía `POST /position-slots`. Se puso un nivel más arriba porque el comentario de ese método ya documenta que sus únicos dos llamadores son crear y actualizar, «so the rule has a single home and cannot be forgotten on a new call site».

⚠️ **Consecuencia que va más allá de lo propuesto: actualizar también queda bloqueado.** Apuntar una plaza existente a un perfil de unidad inactiva devuelve el mismo 422. Se considera coherente —es la misma puerta por otro verbo— pero es una ampliación real del alcance de §3.4 y se declara como tal.

**Va después de la comprobación de publicación, no antes.** Si el perfil tampoco está publicado, ése es el problema que hay que contar primero: decir «la unidad está inactiva» cuando además el perfil está en borrador manda a arreglar lo secundario.

**El campo es `bool?` y el guard compara `== false`.** El join es `LEFT`: un perfil sin unidad resuelve a `null` y no dispara. Un perfil sin unidad es otro problema y no es éste.

#### Rojo antes de verde

| Prueba | Sin el arreglo | Con el arreglo |
|---|---|---|
| `PositionSlots_Create_WithInactiveOrgUnit_ShouldReturn422` | ❌ `Expected: UnprocessableEntity · Actual: Created` — la plaza **se creaba** | ✅ |
| `PositionSlots_Create_WithActiveOrgUnit_ShouldSucceed` (contrapeso) | ✅ `201` | ✅ |

El contrapeso importa: sin él, un guard demasiado ancho —que rechazara también las unidades activas— pasaría el rojo igual de bien.

**Verificado:** integración `~PositionSlot` **42/42** · unitarias **2979/2979**.

#### El guardrail que estaba en rojo y no lo había visto

Al correr las unitarias apareció `BackendMessageLocalizationTests`, que exige que **todo código de error tenga mensaje en inglés y español**. Faltaban tres:

```
ORG_STRUCTURE_CATALOG_IN_USE_FOR_DELETE
ORG_UNIT_IN_USE
POSITION_SLOT_ORG_UNIT_INACTIVE
```

**Dos de los tres son de [00003 / B-04](00003-OrgUnits.md), que ya había dado por cerrado.** Ese trabajo dejó el guardrail en rojo y no lo detecté porque solo corrí la sección de integración, no las unitarias. La regla queda escrita en `definiciones-tecnicas-backend.md` §10 para que no dependa de acordarse.

De paso quedó medido algo que le faltaba a **00002 / B-01**: `BackendMessages.es-SV.resx` tiene **23 claves de 1 051** (2,2 %) y **nada lo vigila** — el guardrail solo mira el `es` genérico.

#### Antes de desplegar

`diagnostico-plazas-unidad-inactiva.sql`, tres consultas: plazas que quedarían sin poder editarse, su detalle con la unidad culpable, y unidades de baja con perfiles publicados sin plazas todavía (el «no me deja crear» que llegará por soporte).

Corrida sobre `clarihr_dev`: **0 filas en las tres**. ⚠️ Con una salvedad que hay que leer entera: en esa base **no hay ninguna unidad inactiva** (27 unidades, todas activas). El vacío dice «no hay a quién romper aquí», no «se revisó un universo poblado y salió limpio». Hay que volver a correrlo contra la base destino.

#### La trampa que casi se cuela

El campo nuevo rompió los dobles de prueba, que usan argumentos con nombre. El regex que los actualizó emparejó por el campo vecino (`OrgUnitName:`) y **también insertó el campo en un `PositionSlotResponse`**, que no lo tiene. Se detectó al revisar los seis sitios uno por uno — el contador decía 6 y los constructores del lookup eran 5. Un regex sobre nombres de campo empareja parientes: hay que contar los sitios esperados antes de aplicarlo.

Los cinco dobles legítimos quedaron con `OrgUnitIsActive: true` **explícito**, no con un default: un doble permisivo convierte un guardrail en decoración.

### 3.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-16 | 🔲 Propuesto | Detectado al escribir el contador de perfiles del `/usage` de 00003 / B-04. Se levanta **con pregunta abierta de producto**: el primer salto puede ser deliberado, el tercero no tiene defensa evidente |
| 2026-08-18 | 🟢 Resuelto | Decisión de producto: **no permitir plazas nuevas contra unidad inactiva**. Guard en `ResolveJobProfileLookupAsync` → cubre crear **y** actualizar. Rojo verificado (la plaza se creaba), verde 42/42 + 2979/2979. Descubierto de paso el guardrail de localización en rojo por 00003 / B-04, y medido el 2,2 % de `es-SV` para 00002 / B-01. §3.6 medida: el patrón **ya es convención** (4 precedentes) y el centro de trabajo tiene la misma brecha, **abierta a propósito** |
| 2026-08-21 | 🟢 Resuelto | **El segundo padre, cerrado.** Producto decidió lo mismo para el centro de trabajo. `ResolveWorkCenterIdAsync` devolvía `Task<long?>` y no podía distinguir «no existe» de «está de baja»: ahora devuelve `(Id, IsActive)`. Rojo verificado (la plaza se creaba), verde 46/46 + 3031/3031. Diagnóstico ampliado con §4 |
