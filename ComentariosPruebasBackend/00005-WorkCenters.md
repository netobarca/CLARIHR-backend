# 00005 — WorkCenters · Hallazgos de backend

| | |
|---|---|
| **ID** | 00005-WorkCenters |
| **Documento espejo** | [`ComentariosPruebasFrontend/00005-WorkCenters`](../ComentariosPruebasFrontend/00005-WorkCenters.md) |
| **Paso probado** | Paso 5 de 8 — *Work centers* |
| **Pantalla que los destapó** | `/work-centers` |
| **Fecha** | 2026-08-15 |
| **Ambiente** | `https://dashboard.clarihr.com` |

---

## 1. Resumen

| ID | Sev. | Hallazgo | Componente | Origen | Alcance | Estado |
|---|---|---|---|---|---|---|
| [B-01](#2-b-01--seis-departamentos-de-el-salvador-están-mal-escritos-en-un-catálogo-sembrado-por-geografía) | 🟡 Media | **81 filas en 15 tablas** sin tilde o sin eñe (se levantó como seis departamentos) (`Cabanas` por *Cabañas*) en la división administrativa oficial | Datos sembrados · jerarquía de ubicaciones | Espejo §3.2 | Un catálogo · **revisar los demás sembrados** | 🟢 **Resuelto** |
| [B-02](#3-b-02--la-clave-del-error-nombra-el-campo-interno-y-no-el-público-cuarto-caso-y-el-primero-en-el-cuerpo) | 🟡 Media | La clave del error es `locationGroupId`; el campo público es `locationGroupPublicId` | Application · validadores | Espejo §2.4 | **Transversal** — cuarto caso | 🟢 **Resuelto** |

**Ninguno bloquea al frontend.** B-01 es de datos y B-02 degrada el mapeo de errores a campos.

### Confirmaciones a hallazgos ya abiertos

| Hallazgo | Qué aporta este paso |
|---|---|
| [00003 / B-04](00003-OrgUnits.md#5-b-04--el-servidor-sabe-por-qué-no-se-puede-inactivar-y-no-lo-dice-y-lo-creado-por-error-no-se-puede-borrar-nunca) | **Quinto recurso** sin `DELETE` ni `/usage`. Con centros de trabajo el caso se agrava: un centro tiene dirección, coordenadas y contacto — un registro caro de rehacer y, si se creó mal, imposible de quitar |
| [00003 / B-03](00003-OrgUnits.md#4-b-03--el-producto-tiene-un-canal-de-localización-completo-que-ningún-cliente-puede-activar) | Los siete errores de la batería llegaron en inglés, como se esperaba |

### Nota de contraste — lo que este módulo hace muy bien

- **La validación condicional por tipo está aplicada en el servidor, no solo en el cliente.** `WORK_CENTER_ADDRESS_REQUIRED` y `WORK_CENTER_GEO_REQUIRED` se disparan de verdad, y el segundo exige **la pareja completa** de coordenadas: mandar solo latitud también se rechaza. Es la única regla de negocio de las cinco pantallas probadas con cobertura en las dos capas.
- **El guard de nivel funciona**: anclar un centro a un grupo de nivel departamento da `409 LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`. La jerarquía no se puede corromper desde el contrato.
- **`WORK_CENTER_TYPE_IN_USE` funciona** — medición que el Paso 4 había dejado pendiente y que este paso cierra sin montaje adicional.
- **El listado aplana las referencias** en `workCenterTypeCode`/`Name` y `locationGroupCode`/`Name`, en vez de anidar objetos. Le ahorra al cliente un viaje y una desanidación.

> **Nota de método.** Al leer los validadores de FluentValidation no encontré la aplicación de los flags del tipo y **estuve a punto de levantar como hallazgo que eran declarativos y nadie los aplicaba**. La sonda lo desmintió: la regla vive en el handler (`WorkCenterAdministration.cs:1337` y `:1342`). Un `grep` sobre el bloque equivocado es un instrumento tan falible como un doble de test permisivo.

---

## 2. B-01 — Seis departamentos de El Salvador están mal escritos en un catálogo sembrado por geografía

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-21, verificado sobre una base creada desde cero (§2.9) |
| **Componente** | Datos sembrados · 15 tablas |
| **Origen** | Lectura del catálogo al cargar las sedes del escenario |
| **Alcance** | **81 filas en 15 tablas** — el montaje de §2.6, hecho |

> ⚠️ **El alcance real era catorce veces mayor que el levantado, y una afirmación de §2.2 era falsa.**
>
> | §2.2 afirmaba | Medido el 2026-08-21 |
> |---|---|
> | «el defecto solo aflora en los departamentos» porque los 44 municipios no llevan acentos | ❌ **Los 14 municipios de los seis departamentos heredaban el error**: su nombre se derivaba del código ASCII completo (`CABANAS_ESTE` → «Cabanas Este») |
>
> Y el montaje que §2.6 pedía —escanear **todos** los sembrados de una vez en vez de ir catálogo por
> catálogo— destapó **15 tablas**, no una. Ver §2.9.

### 2.1 Evidencia

Los 14 departamentos, leídos de `GET /v1/location-groups`:

```
Ahuachapan · Cabanas · Chalatenango · Cuscatlan · La Libertad · La Paz · La Union
Morazan · San Miguel · San Salvador · San Vicente · Santa Ana · Sonsonate · Usulutan
```

**Seis están mal escritos:**

| Sembrado | Correcto |
|---|---|
| `Ahuachapan` | Ahuachapán |
| **`Cabanas`** | **Cabañas** |
| `Cuscatlan` | Cuscatlán |
| `La Union` | La Unión |
| `Morazan` | Morazán |
| `Usulutan` | Usulután |

**El caso de `Cabanas` no es el mismo que los otros cinco.** Quitar una tilde produce una palabra mal acentuada; quitar la eñe produce **otra palabra**. Los códigos internos (`CABANAS_ESTE`, `CABANAS_OESTE`) están bien así —son identificadores ASCII—, pero el **nombre para mostrar** no debería heredar esa limitación.

### 2.2 Causa

El nombre para mostrar se sembró con el mismo texto normalizado que el código, en lugar de con el nombre oficial. Es coherente en los 44 municipios (`La Paz Oeste`, `San Salvador Centro`) porque esos nombres no llevan acentos, así que **el defecto solo aflora en los departamentos**, que es donde sí los hay.

### 2.3 Impacto

**Es la división administrativa oficial de un país, en un producto de cumplimiento salvadoreño.** Estos nombres no son decorativos: el centro de trabajo alimenta el reporte de **Planilla Patronal** y la distribución geográfica del tablero. Un reporte que la empresa presenta ante una institución pública no debería escribir «Cabanas».

Y no hay forma de arreglarlo desde la aplicación: son datos sembrados, y la propia guía de configuración dice que la jerarquía de ubicaciones **no se toca**.

**No bloquea nada hoy**, por eso es Media y no Alta: los códigos son correctos y las relaciones funcionan.

### 2.4 Propuesta

Corregir los seis nombres para mostrar en la siembra, **dejando los códigos intactos** — cambiarlos rompería referencias y no aporta nada.

```
Ahuachapan → Ahuachapán     Cabanas → Cabañas     Cuscatlan → Cuscatlán
La Union   → La Unión       Morazan → Morazán     Usulutan  → Usulután
```

### 2.5 Compatibilidad

**No rompe el contrato**: cambia un valor de datos, no una forma. El `code` —que es por donde se referencia— no se toca. Requiere una migración de actualización sobre las filas sembradas; **no hay producción**, así que no hay datos de cliente en riesgo.

⚠️ **Verificar que ningún cliente compare por `name`.** Si alguna pantalla filtra o casa por nombre en vez de por código, el cambio la afectaría. Es una búsqueda en el frontend, no un riesgo de servidor.

### 2.6 Alcance a revisar

La pregunta no es este catálogo, sino **si el mismo aplanado de acentos afectó a otros datos sembrados**. Candidatos por su naturaleza geográfica o legal: tipos documentales, instituciones de AFP, catálogos de educación, causas y motivos por país.

**Montaje:** listar los valores de nombre de las tablas sembradas y filtrar los que contengan secuencias sospechosas —una vocal donde el nombre oficial lleva tilde, o `n` donde va `ñ`—. **Es una consulta, no una corrida**; responde el alcance de una vez en lugar de descubrirlo catálogo por catálogo.

### 2.7 Vía alterna vigente

Ninguna. El frontend **no debe** maquillar los nombres en pantalla: crearía una discrepancia entre lo que se ve y lo que se exporta, que es peor que el defecto.

### 2.9 Lo que se hizo

#### El montaje de §2.6, ejecutado

La consulta cruzó los **1 319 nombres sembrados** de toda la base contra una lista de palabras españolas
que nunca son correctas sin tilde o eñe. Resultado: **82 filas en 15 tablas**, no 6 en una.

| Tabla | Filas | Ejemplo |
|---|---:|---|
| `location_groups` | 20 | «Cabanas Este» |
| `iam_permissions` | 16 | «Ver transacciones fuera de nomina» |
| `municipality_catalog_items` | 14 | «Ahuachapan Centro» |
| `profession_catalog_items` | 7 | «Odontologo/a» |
| `department_catalog_items` | 6 | «Cabanas» |
| `compensation_concept_type_catalog_items` | 4 | «Viaticos», «Dano de equipo» |
| `company_type_catalog_items` | 3 | «Sociedad Anonima de Capital Variable» |
| `bank_catalog_items` | 2 | **«Banco Agricola»**, «Cuscatlan» |
| `language_catalog_items` | 2 | «Espanol», «Ingles» |
| 6 tablas más | 6 | «Dia», «Basico», «Dolar estadounidense», «Unica» |

**Dos hallazgos que nadie habría buscado en una pantalla de sedes:** los nombres de dieciséis permisos
—que el administrador lee al asignar accesos— y **«Banco Agricola»**, que aparece en la cuenta bancaria
de cada empleado.

#### La causa era más profunda que seis literales

Los municipios **no estaban escritos a mano**: `HumanizeCode` los derivaba del código ASCII. Corregir
solo los seis departamentos habría dejado catorce municipios mal y **la próxima siembra los habría
vuelto a generar así**. Ahora el nombre se compone con el nombre del departamento:

```csharp
// «Cabañas» + «ESTE» → «Cabañas Este»
ComposeMunicipalityName(department, municipalityCode)
```

Corregir el departamento corrige sus municipios, y no pueden volver a divergir.

#### Lo que NO se acentuó, y por qué

`normalizedName` en los bancos **se queda en ASCII a propósito**: es la clave de búsqueda, y los
repositorios comparan `NormalizedName.Contains(search.ToUpperInvariant())` **sin plegar acentos**. En
ASCII, escribir «cuscatlan» sigue encontrando «Cuscatlán». Se pasó a argumento con nombre
(`normalizedName:`) para que la intención esté escrita en el sitio de uso y el guardrail pueda saltárselo.

⚠️ **Eso destapa un defecto distinto y vivo** — ver §2.10.

#### La migración cubre lo que EF no andamia

`dotnet ef migrations add` generó **42 `UpdateData`**… para 10 tablas. Las otras **6 no usan `HasData`**:
los catálogos territoriales y de referencia se sembraron con SQL de una migración anterior, y
`location_groups` e `iam_permissions` los crea el aprovisionamiento de cada empresa desde las plantillas
del código. **Arreglar la plantilla solo corrige a las empresas nuevas.** Se añadió SQL a mano para las
seis, generado desde los archivos ya corregidos para que no pueda divergir del código.

> **Trampa que costó una corrida:** `iam_permissions.code` se guarda en **MAYÚSCULAS**
> (`PERSONNELFILES.VIEWCOMPENSATION`) y la constante del código es PascalCase
> (`PersonnelFiles.ViewCompensation`). El `WHERE t.code = v.code` no casó **ni una fila** y el `UPDATE`
> devolvió 0 sin error. Se detectó porque el escaneo posterior seguía dando 16, no porque algo fallara.

#### Verificación sobre una base creada desde cero

No basta con que la migración corra sobre la base de desarrollo: hay que probar que el **encadenado
completo** produce datos correctos. Se creó `clarihr_seedcheck` vacía y se aplicaron todas las
migraciones:

```
Departamentos: Ahuachapán · Cabañas · Chalatenango · Cuscatlán · La Libertad · La Paz · La Unión
               Morazán · San Miguel · San Salvador · Santa Ana · San Vicente · Sonsonate · Usulután
```

| Medición | Antes | Después |
|---|---:|---:|
| Nombres sembrados con tilde o eñe faltante (base de desarrollo) | 82 | **0** |
| Descripciones sembradas | 1 | **0** |
| Base creada **desde cero** | — | **0** |

**Suites:** build 0 errores / 0 advertencias · unitarias **3031/3031** · integración `~WorkCenter` +
`~ValidationLocalization` **25/25**.

> ⚠️ **Esa verificación fue incompleta y dejó dos pruebas en rojo durante tres días.** La rebanada elegida
> no incluía la sección `empresas`, y allí había tres aserciones que afirmaban los nombres **sin tilde**
> (`Sociedad Anonima de Capital Variable`, `Asociacion Civil`). Aparecieron el 2026-08-21 al correr la
> suite completa por lotes para otro hallazgo, y se corrigieron entonces.
>
> La lección es de método, no de contenido: **una corrección de datos sembrados es transversal por
> definición** —cualquier prueba de cualquier sección puede estar afirmando el valor viejo—, así que la
> rebanada del recurso tocado no basta. `SeedDisplayTextTests` protege el sembrado, pero **no** protege a
> quien lo afirme desde otra sección.

#### El guardrail

`SeedDisplayTextTests` lee los cinco archivos de siembra y falla si un texto de presentación vuelve a
escribirse sin su tilde. **Rojo verificado** restaurando el catálogo territorial original: 7 ofensores.

Dos calibraciones, la misma lección que en `00002 / B-01`:

1. **No mira códigos.** Solo literales con espacio o de la forma «Palabra», nunca `MAYUSCULAS_CON_GUION`.
2. **Los países se siembran en inglés a propósito** (`France`, `Brazil`), y `Decision Level` y
   `Diego Garcia` también lo son. La lista excluye toda palabra que exista en los dos idiomas.

Al aplicarlo señaló dos veces trabajo correcto —los `normalizedName` en ASCII—; se verificó que el código
tenía razón y se estrechó el guardrail, no al revés.

### 2.10 Lo que este hallazgo destapó — 🟢 **cerrado el 2026-08-21**

**La búsqueda por nombre no pliega acentos.** Treinta repositorios comparan
`NormalizedName.Contains(search.Trim().ToUpperInvariant())`, y `NormalizeName` es solo `ToUpperInvariant()`:
no quita tildes. Consecuencia: **un nombre con tilde no se encuentra escribiéndolo sin ella**.

**No lo introduce este arreglo, ya está vivo:** en la base de desarrollo hay **14 filas creadas por
usuarios** cuyo `normalized_name` ya lleva tildes. Cualquiera que busque «estacion» no encuentra
«Estación SAL».

Se levantó aparte porque tocaba **30 repositorios** más la normalización del dominio, y plegar solo en
las semillas dejaría dos reglas distintas en la misma tabla. **Producto decidió plegar el 2026-08-21.**

#### El diseño: una sola regla, usada por los dos lados

`CLARIHR.Domain.Common.SearchTextNormalization.Fold` — mayúsculas sin diacríticos. La usan **lo que se
guarda** (12 helpers `NormalizeName`) y **lo que se busca** (49 sitios de construcción del término).

**Tenía que ser una sola implementación.** Plegar solo la entrada dejaría de encontrar lo que hoy sí se
encuentra; plegar solo lo almacenado tampoco casaría. Dos funciones que deben coincidir siempre acaban
divergiendo — es el mismo razonamiento que llevó a unificar el generador de la huella del contrato en
[00950 / B-01](00950-Remediacion.md).

**No es una regla nueva:** `PositionDescriptionCatalogNormalization` e `InternalCatalogValue` ya plegaban.
Esto generaliza lo que ya era convención en parte.

#### Lo medido, que corrige lo que decía este mismo párrafo

| | Se dijo al levantarlo | Medido al arreglarlo |
|---|---|---|
| Filas afectadas | 14 | **337** (las 14 eran de tres tablas muestreadas) |
| Índices únicos sobre columnas normalizadas | 2 | **~130** (la consulta original buscaba `normalized_name` exacto) |
| Colisiones al plegar | — | **0** |

#### Dos cosas que la medición evitó romper

**`normalized_email` habría roto el login.** Se guarda en **minúsculas** y su normalizador (`NormalizeEmail`)
**no** se cambió: si la migración lo hubiera plegado, el correo almacenado dejaría de casar con el que
calcula el inicio de sesión. La migración toca exactamente las **cinco familias** que escriben los helpers
modificados —`normalized_name`, `_full_name`, `_description`, `_requirement_name`, `_title`— y ni el correo
ni los códigos, que son ASCII por su regex.

**Cuatro búsquedas quedan fuera, a propósito.** `PersonnelFileEmployeeRepository` y `SettlementRepository`
comparan **columnas crudas** (`FirstName`, `AddressedTo`) con `ToUpper()` **en SQL**, no un valor normalizado
almacenado. Plegar solo la entrada ahí **rompería lo que hoy funciona**: buscar «José» dejaría de encontrar
«José». Se verificó que ninguno de los 49 reemplazos cayera en un sitio así — los 5 que comparan contra algo
no normalizado son códigos ASCII, donde plegar la entrada solo puede ayudar.

⚠️ **Consecuencia que hay que conocer: la unicidad pasa a ser insensible a acentos.** «Cañas» y «Canas» ya
no pueden coexistir como dos elementos del mismo catálogo. Para un catálogo es defendible —dos entradas que
solo difieren en una tilde son un error de captura— pero es un cambio más allá de la búsqueda. Se midió
antes de tocar nada: **0 colisiones**.

#### Rojo antes de verde

Revertido el plegado **solo en el lado de escritura** de una normalización, las dos búsquedas pasaron de
**1 a 0**. Ese rojo prueba lo que importa: la prueba detecta que los dos lados diverjan.

#### Verificación: la suite completa, por lotes

| Lote | Secciones | Resultado |
|---|---|---|
| 1 | unidades-organizativas · centros-de-costo · centros-de-trabajo · ubicaciones | **108/108** |
| 2 | puestos · competencias · catálogos · representantes-legales | **216/216** |
| 3 | auth · empresas · usuarios · guardrails · auditoría · backoffice · reportes | 209/211 → **211/211** |
| 4 | expedientes · contratación · nómina · ausencias · disciplina | 356/358 → **358/358** |

Unitarias **3047/3047** (16 nuevas del plegado). Migración: **337 → 1**, y esa 1 es un **guion largo**, que
no es diacrítico: `Fold` lo conserva y la migración también.

> **Los 5 fallos NO fueron del plegado.** Los cinco eran aserciones obsoletas del arreglo de acentos en los
> sembrados (§2.9): afirmaban «Sociedad Anonima», «Dano de equipo», «Banco Agricola». Llevaban rojas tres
> días porque la verificación de aquel día usó una rebanada que no las cubría. **El plegado no rompió nada.**

### 2.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Detectado al buscar los municipios del escenario AVIANCA en el catálogo sembrado |
| 2026-08-21 | 🟢 Resuelto | **El alcance era 14× mayor**: 81 filas en 15 tablas, no 6 en una. §2.2 se equivocaba — los 14 municipios heredaban el error porque su nombre se derivaba del código ASCII. Aparecieron «Banco Agricola» y los nombres de 16 permisos. Migración con SQL a mano para las 6 tablas que EF no andamia; **`iam_permissions.code` está en MAYÚSCULAS** y el `UPDATE` no casó ni una fila hasta plegar la caja. Verificado sobre una base creada **desde cero**: 0 pendientes |
| 2026-08-21 | 🟢 Resuelto | **Corrección posterior:** la verificación del día había sido incompleta — la rebanada no cubría `empresas` y dejó **3 aserciones en rojo** que afirmaban los nombres sin tilde. Detectadas al correr la suite completa por lotes; corregidas. Un cambio de datos sembrados es transversal y no se verifica con la rebanada del recurso tocado |

---

## 3. B-02 — La clave del error nombra el campo interno y no el público: cuarto caso, y el primero en el cuerpo

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-16 (§3.9) |
| **Componente** | Api · `ProblemDetailsFactory` — **no** los validadores |
| **Origen** | Batería de errores del Paso 5 |
| **Alcance** | **Transversal** — 50 parámetros medidos + los campos de cuerpo |

### 3.1 Evidencia

```
POST /v1/work-centers   { code, name, workCenterTypePublicId }     ← sin el grupo
→ 400   errors: { "locationGroupId": [ … ] }
```

**El campo del cuerpo se llama `locationGroupPublicId`. La clave del error dice `locationGroupId`.**

### 3.2 Por qué esto deja de ser «un caso» y pasa a ser un patrón

Es el cuarto, y el primero que ocurre sobre un **campo del cuerpo** en lugar de un parámetro de consulta:

| Módulo | Nombre público | Clave del error |
|---|---|---|
| `OrgStructureCatalogs` | `q` | `search` |
| `OrgUnits` | `q` | `search` |
| `WorkCenterTypes` | `q` | `search` |
| **`WorkCenters`** | **`locationGroupPublicId`** | **`locationGroupId`** |

Los tres primeros comparten una sola causa —el nombre de la propiedad del *query object* no coincide con el del parámetro de ruta— y están levantados como [00002 / B-02](00002-UnitTypes.md#3-b-02--la-clave-del-error-de-búsqueda-es-search-pero-el-parámetro-público-es-q).

**Este cuarto cambia el diagnóstico**, y por eso se levanta aparte en vez de citarlo: no es un desajuste del buscador, es **la regla general de que la clave del error se toma del nombre interno del comando**. `CreateWorkCenterCommand` llama al campo `LocationGroupId`; el DTO público lo llama `LocationGroupPublicId`; FluentValidation emite el nombre del comando. La misma traducción `…PublicId → …Id` que el proyecto aplica a propósito en los DTOs **se filtra sin querer a los mensajes de error**.

**Eso amplía muchísimo el alcance.** No son cuatro endpoints: son **todos los comandos cuyo DTO público renombra un campo**, que por la convención `XxxId → xxxPublicId` es prácticamente cualquier referencia a otra entidad.

### 3.3 Impacto

El frontend no puede asociar el error a su control. Con `errors: { "locationGroupId": [...] }` y un control llamado `locationGroupPublicId`, un mapeo genérico **no encuentra el campo** y el mensaje termina suelto al pie del formulario o directamente no se muestra.

Y golpea justo donde más duele: **el campo que el formulario presenta como opcional** ([espejo F-02](../ComentariosPruebasFrontend/00005-WorkCenters.md#-f-02--el-grupo-de-ubicación-se-presenta-como-opcional-y-el-servidor-lo-exige)). El usuario lo salta creyendo que puede, y el error que le explicaría por qué no puede es el que no se logra pintar junto al campo. **Los dos defectos se agravan mutuamente.**

### 3.4 Propuesta

**No parchear caso por caso.** Los tres del buscador se iban a resolver con un `.OverridePropertyName("q")` en cada regla; con el cuarto queda claro que eso no escala.

Lo que corresponde es **que la emisión del nombre de propiedad traduzca al nombre público de forma sistemática**: un resolvedor de nombres para el validador que aplique la misma convención `XxxId → xxxPublicId` que ya usan los DTOs, en un solo sitio.

Así se arreglan los cuatro casos conocidos y los que no se han encontrado, que por §3.2 son muchos.

**Si se prefiere una corrección acotada primero**, el orden por impacto es: los campos de cuerpo (este) antes que los de consulta (los tres del buscador), porque un campo de formulario necesita el mensaje pegado al control y un buscador no.

### 3.5 Compatibilidad

⚠️ **Cambia las claves de `errors` en las respuestas `400`.** Es el contrato que el frontend usa para colocar mensajes.

**Lo que hoy funciona seguirá funcionando**: si el cliente tiene mapeos manuales para las claves actuales, dejarán de casar. Conviene inventariar esos mapeos en el frontend antes de desplegar, y hacerlo en una sola entrega en lugar de módulo a módulo — precisamente porque el cambio es transversal.

Requiere regenerar `openapi.yaml` si los ejemplos de error están documentados.

### 3.6 Alcance a revisar

**La medición que hay que hacer antes de decidir**: contar cuántos comandos tienen un campo cuyo nombre difiere del DTO público. Es una comparación entre los `record` de comando y los `record` de petición de cada controlador. **Se puede hacer sin ambiente**, y da el tamaño real de una vez.

### 3.7 Vía alterna vigente

El frontend puede mantener una tabla de equivalencias clave-de-error → control. **Funciona y es exactamente lo que la propuesta haría innecesario**; conviene no invertir en ampliarla hasta decidir el arreglo de fondo.

### 3.8 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Cuarto caso del patrón y **primero sobre un campo del cuerpo**. Reencuadra [00002 / B-02](00002-UnitTypes.md#3-b-02--la-clave-del-error-de-búsqueda-es-search-pero-el-parámetro-público-es-q): no es un desajuste del buscador, es la convención de nombres filtrándose a los mensajes de error |
| 2026-08-16 | 🟢 Resuelto | Resuelto en un solo sitio (§3.9). **El alcance era el doble de lo reportado** (50 parámetros, no 3) y **la solución propuesta habría arreglado 1 de 4 casos** |
| 2026-08-16 | 🟢 Resuelto | **Verificado tras recuperar la corrida perdida.** Unitarias **2970/2970**; integración dirigida **776/776** en cinco lotes (guardrails·repr.legales·empresas 111 · centros trabajo·unidades·ubicaciones 74 · expedientes·puestos·plazas 200 · nómina·ausencias·reportes 282 · contratación·competencias·centros costo 109). **Cero fallos.** Resultados en `TestResults/lote*.trx` |

### 3.9 Lo que se hizo — y dos correcciones al hallazgo

**El alcance estaba subestimado.** Medido el 2026-08-16:

| Patrón | El hallazgo decía | Medido |
|---|---|---|
| `q` → `search` | 3 módulos | **40** |
| `page` → `pageNumber` | *no aparecía* | **10** |

**Y la solución propuesta no arreglaba lo que decía arreglar.** §3.4 proponía «un resolvedor que aplique la convención `XxxId → xxxPublicId`» y afirmaba que «así se arreglan los cuatro casos conocidos». **No**: `q`→`search` y `page`→`pageNumber` no son renombres de `*Id`. Habría arreglado **1 de 4** y dejado los 50 de consulta intactos.

**Dónde estaba realmente el defecto.** Hay **dos** caminos que producen un `400`, y solo uno normalizaba las claves:

| Camino | ¿Normalizaba? |
|---|---|
| Model-binding de MVC → `ProblemDetailsDefaults.NormalizeValidationErrors` | ✅ sí |
| **FluentValidation vía `Result` → `ProblemDetailsFactory`** | ❌ **no** — y es de donde salen estos errores |

Por eso el defecto no se veía en los errores de binding. El arreglo va en `ProblemDetailsFactory`, no en los validadores: **un solo sitio para todo el producto**.

**`PublicFieldNameMap`** resuelve el nombre público con **tres fuentes de verdad distintas**, ninguna adivinada:

1. **`BinderModelName` de MVC** para los parámetros renombrados (`q`, y los `[FromQuery(Name="page")]`).
2. **El DTO público de la petición** para los campos de cuerpo: sólo traduce `LocationGroupId → locationGroupPublicId` si ese DTO **declara** la propiedad.
3. **`PageNumber → page`** como convención declarada, citando §4.3 de las definiciones técnicas, que está 🔒: *«Todo listado: `page`, `pageSize`»*.

> ⚠️ **Por qué no se aplicó `Id → PublicId` a ciegas**, que es lo que §3.4 proponía: §9 dice que los `Guid *Id` se exponen como `*PublicId`, **pero no es universal** — `companyId` viaja en la ruta llamándose `companyId`. Un reemplazo ciego habría renombrado esa clave a un campo inexistente, **cambiando un desajuste por otro**.

**Verificado con su contrapeso:** un test exige que `q` salga como `q`; el otro, que un parámetro **no** renombrado siga saliendo igual. Sin el segundo, una traducción demasiado agresiva pasaría por buena.
