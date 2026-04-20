# Integracion Frontend - Competencias, Conductas y Matriz por Puesto

## Estado de la API

La API ya cubre el flujo funcional requerido para competencias asociadas a puestos:

- Catalogos base: competencias, tipos de competencia, niveles de comportamiento y comportamientos observables.
- Piramide ocupacional: niveles jerarquicos del puesto.
- Conductas esperadas: relacionan competencia + tipo + nivel + descripcion.
- Behaviors por conducta: comportamientos observables asociados a una conducta.
- Matriz de competencias por perfil de puesto.
- Export de matriz por perfil.

Salvedad actual: `job-catalogs` permite listar, crear, activar e inactivar items, pero no expone endpoint de edicion de `code/name`. Si el frontend necesita editar un catalog item existente, hoy debe solicitarse ese endpoint o manejarlo como inactivar + crear nuevo.

## Conceptos

| Concepto funcional | En API | Uso frontend |
| --- | --- | --- |
| Competencias | `job-catalogs/Competency` | Catalogo de capacidades requeridas. |
| Tipos de competencia | `job-catalogs/CompetencyType` | Clasifica competencias como tecnica, conductual, gerencial, etc. |
| Niveles de comportamiento | `job-catalogs/BehaviorLevel` | Define el nivel esperado: basico, intermedio, avanzado. |
| Comportamientos | `job-catalogs/Behavior` | Conductas observables atomicas. |
| Piramide ocupacional | `occupational-pyramid-levels` | Niveles jerarquicos del puesto. |
| Conductas esperadas | `competency-conducts` | Une competencia + tipo + nivel + descripcion. |
| Matriz del puesto | `job-profiles/{id}/competency-matrix` | Asocia expectativas al perfil de puesto. |

## Permisos

Todas las rutas requieren autenticacion.

Para lectura:

- `CompetencyFramework.Read`
- `CompetencyFramework.Admin`
- `iam.administration.manage`

Para escritura:

- `CompetencyFramework.Admin`
- `iam.administration.manage`

Para `job-catalogs`:

- Lectura: `JobProfiles.Read`, `JobProfiles.Admin`, `JobCatalogs.Admin` o `iam.administration.manage`.
- Escritura: `JobCatalogs.Admin` o `iam.administration.manage`.

## Flujo Recomendado En Frontend

### 1. Mantener catalogos base

El frontend debe tener pantallas o modales para administrar estos catalogos:

- `Competency`
- `CompetencyType`
- `BehaviorLevel`
- `Behavior`

Listar items:

```http
GET /api/v1/companies/{companyId}/job-catalogs/Competency?isActive=true&page=1&pageSize=100
GET /api/v1/companies/{companyId}/job-catalogs/CompetencyType?isActive=true&page=1&pageSize=100
GET /api/v1/companies/{companyId}/job-catalogs/BehaviorLevel?isActive=true&page=1&pageSize=100
GET /api/v1/companies/{companyId}/job-catalogs/Behavior?isActive=true&page=1&pageSize=100
```

Crear item:

```http
POST /api/v1/companies/{companyId}/job-catalogs/Competency
Content-Type: application/json
```

```json
{
  "code": "COMUNICACION_EFECTIVA",
  "name": "Comunicacion efectiva"
}
```

Ejemplos reales de catalogos:

```json
[
  { "category": "Competency", "code": "COMUNICACION_EFECTIVA", "name": "Comunicacion efectiva" },
  { "category": "Competency", "code": "ORIENTACION_SERVICIO", "name": "Orientacion al servicio" },
  { "category": "Competency", "code": "ANALISIS_DATOS", "name": "Analisis de datos" },
  { "category": "CompetencyType", "code": "TECNICA", "name": "Tecnica" },
  { "category": "CompetencyType", "code": "CONDUCTUAL", "name": "Conductual" },
  { "category": "CompetencyType", "code": "GERENCIAL", "name": "Gerencial" },
  { "category": "BehaviorLevel", "code": "BASICO", "name": "Basico" },
  { "category": "BehaviorLevel", "code": "INTERMEDIO", "name": "Intermedio" },
  { "category": "BehaviorLevel", "code": "AVANZADO", "name": "Avanzado" },
  { "category": "Behavior", "code": "ESCUCHA_ACTIVA", "name": "Escucha activamente antes de responder" },
  { "category": "Behavior", "code": "DOCUMENTA_HALLAZGOS", "name": "Documenta hallazgos con evidencia" },
  { "category": "Behavior", "code": "PRIORIZA_CLIENTE", "name": "Prioriza necesidades del cliente interno" }
]
```

Activar o inactivar item:

```http
PATCH /api/v1/job-catalogs/{catalogItemId}/inactivate
Content-Type: application/json
```

```json
{
  "concurrencyToken": "catalog-item-concurrency-token"
}
```

### 2. Mantener piramide ocupacional

Listar:

```http
GET /api/v1/companies/{companyId}/occupational-pyramid-levels?isActive=true&page=1&pageSize=100
```

Crear:

```http
POST /api/v1/companies/{companyId}/occupational-pyramid-levels
Content-Type: application/json
```

```json
{
  "code": "ANALISTA",
  "name": "Analista",
  "levelOrder": 2,
  "description": "Puestos tecnicos o profesionales de ejecucion y analisis."
}
```

Ejemplo de niveles:

```json
[
  {
    "code": "GERENCIAL",
    "name": "Gerencial",
    "levelOrder": 1,
    "description": "Puestos con responsabilidad de direccion y toma de decisiones."
  },
  {
    "code": "ANALISTA",
    "name": "Analista",
    "levelOrder": 2,
    "description": "Puestos de analisis, seguimiento y ejecucion especializada."
  },
  {
    "code": "OPERATIVO",
    "name": "Operativo",
    "levelOrder": 3,
    "description": "Puestos de ejecucion operativa directa."
  }
]
```

Actualizar:

```http
PUT /api/v1/occupational-pyramid-levels/{levelId}
Content-Type: application/json
```

```json
{
  "code": "ANALISTA",
  "name": "Analista",
  "levelOrder": 2,
  "description": "Puestos de analisis y ejecucion especializada.",
  "concurrencyToken": "level-concurrency-token"
}
```

### 3. Crear conductas esperadas por competencia

Una conducta esperada relaciona:

- Competencia
- Tipo de competencia
- Nivel de comportamiento
- Descripcion del conducto

Listar conductas:

```http
GET /api/v1/companies/{companyId}/competency-conducts?isActive=true&page=1&pageSize=20
GET /api/v1/companies/{companyId}/competency-conducts?competencyId={competencyId}&competencyTypeId={typeId}&behaviorLevelId={levelId}
```

Crear conducta:

```http
POST /api/v1/companies/{companyId}/competency-conducts
Content-Type: application/json
```

```json
{
  "competencyPublicId": "id-comunicacion-efectiva",
  "competencyTypePublicId": "id-conductual",
  "behaviorLevelPublicId": "id-intermedio",
  "description": "Comunica informacion clara, oportuna y adaptada al interlocutor.",
  "sortOrder": 1
}
```

Actualizar conducta:

```http
PUT /api/v1/competency-conducts/{conductId}
Content-Type: application/json
```

```json
{
  "competencyPublicId": "id-comunicacion-efectiva",
  "competencyTypePublicId": "id-conductual",
  "behaviorLevelPublicId": "id-intermedio",
  "description": "Comunica informacion clara, oportuna y adaptada al interlocutor.",
  "sortOrder": 1,
  "concurrencyToken": "conduct-concurrency-token"
}
```

Reglas importantes:

- No puede existir otra conducta con la misma combinacion `competencia + tipo + nivel + descripcion`.
- Solo se pueden usar catalog items activos.
- No se puede inactivar una conducta si esta siendo usada por matrices activas.

### 4. Asociar comportamientos observables a una conducta

Este paso permite decir que una conducta esperada se evidencia por comportamientos observables concretos.

```http
PUT /api/v1/competency-conducts/{conductId}/behaviors
Content-Type: application/json
```

```json
{
  "behaviors": [
    {
      "behaviorPublicId": "id-escucha-activa",
      "notes": "Escucha y confirma entendimiento antes de responder.",
      "sortOrder": 1
    },
    {
      "behaviorPublicId": "id-comunica-claro",
      "notes": "Explica acuerdos, riesgos y proximos pasos.",
      "sortOrder": 2
    }
  ],
  "concurrencyToken": "conduct-concurrency-token"
}
```

Regla importante: este endpoint reemplaza todo el set de behaviors. Si el frontend manda una lista vacia, limpia los comportamientos asociados.

### 5. Integrar matriz de competencias en el perfil de puesto

En la pantalla de detalle del puesto se recomienda una pestana llamada `Matriz de competencias`.

Al abrir la pestana:

```http
GET /api/v1/job-profiles/{jobProfileId}/competency-matrix
```

Respuesta conceptual:

```json
{
  "jobProfileId": "id-job-profile",
  "jobProfileCode": "JP-002",
  "jobProfileTitle": "Analista de RRHH",
  "jobProfileStatus": "Draft",
  "jobProfileVersion": 22,
  "concurrencyToken": "matrix-concurrency-token",
  "items": []
}
```

El frontend debe mostrar una tabla editable con estas columnas:

| Campo UI | Campo API |
| --- | --- |
| Nivel piramide | `occupationalPyramidLevelPublicId` |
| Competencia | `competencyPublicId` |
| Tipo | `competencyTypePublicId` |
| Nivel comportamiento | `behaviorLevelPublicId` |
| Conductas esperadas | `conductPublicIds` |
| Evidencia esperada | `expectedEvidence` |
| Orden | `sortOrder` |

Guardar matriz:

```http
PUT /api/v1/job-profiles/{jobProfileId}/competency-matrix
Content-Type: application/json
```

```json
{
  "items": [
    {
      "occupationalPyramidLevelPublicId": "id-nivel-analista",
      "competencyPublicId": "id-comunicacion-efectiva",
      "competencyTypePublicId": "id-conductual",
      "behaviorLevelPublicId": "id-intermedio",
      "conductPublicIds": [
        "id-conducto-comunicacion-intermedio"
      ],
      "expectedEvidence": "Redacta comunicaciones claras a empleados y jefaturas, explicando politicas de RRHH sin ambiguedad.",
      "sortOrder": 1
    },
    {
      "occupationalPyramidLevelPublicId": "id-nivel-analista",
      "competencyPublicId": "id-analisis-datos",
      "competencyTypePublicId": "id-tecnica",
      "behaviorLevelPublicId": "id-basico",
      "conductPublicIds": [
        "id-conducto-analisis-basico"
      ],
      "expectedEvidence": "Genera reportes basicos de ausentismo, rotacion y dotacion con datos consistentes.",
      "sortOrder": 2
    }
  ],
  "concurrencyToken": "matrix-concurrency-token"
}
```

Reglas importantes:

- `PUT /competency-matrix` reemplaza toda la matriz, no hace merge parcial.
- Si el frontend manda `items: []`, limpia toda la matriz.
- No puede repetirse la combinacion `occupationalPyramidLevelPublicId + competencyPublicId + competencyTypePublicId + behaviorLevelPublicId`.
- No puede repetirse un `conductPublicId` dentro del mismo item.
- Cada conducta seleccionada debe pertenecer exactamente a la misma competencia, tipo y nivel declarados en el item.
- Si el perfil esta archivado, la API no permite modificar la matriz.
- Al guardar la matriz, el backend incrementa la version del `JobProfile` y regenera el `ConcurrencyToken`.

### 6. Exportar matriz

```http
GET /api/v1/job-profiles/{jobProfileId}/competency-matrix/export?format=xlsx
GET /api/v1/job-profiles/{jobProfileId}/competency-matrix/export?format=csv
GET /api/v1/job-profiles/{jobProfileId}/competency-matrix/export?format=json
```

Formatos soportados:

- `xlsx`
- `csv`
- `json`

## Ejemplo Funcional Completo

### Caso: Analista de RRHH

Catalogos:

```json
{
  "competency": "COMUNICACION_EFECTIVA",
  "competencyType": "CONDUCTUAL",
  "behaviorLevel": "INTERMEDIO",
  "behavior": "ESCUCHA_ACTIVA",
  "occupationalLevel": "ANALISTA"
}
```

Conducta:

```json
{
  "description": "Comunica informacion clara, oportuna y adaptada al interlocutor."
}
```

Behavior asociado:

```json
{
  "behavior": "Escucha activamente antes de responder",
  "notes": "Confirma entendimiento antes de entregar una respuesta formal."
}
```

Matriz del puesto:

```json
{
  "nivel": "ANALISTA",
  "competencia": "COMUNICACION_EFECTIVA",
  "tipo": "CONDUCTUAL",
  "nivelComportamiento": "INTERMEDIO",
  "evidenciaEsperada": "Atiende consultas de colaboradores y jefaturas con claridad, oportunidad y trazabilidad."
}
```

### Caso: Gerente General

Catalogos:

```json
{
  "competency": "LIDERAZGO_ESTRATEGICO",
  "competencyType": "GERENCIAL",
  "behaviorLevel": "AVANZADO",
  "behavior": "ALINEA_EQUIPOS",
  "occupationalLevel": "GERENCIAL"
}
```

Conducta:

```json
{
  "description": "Define prioridades organizacionales y alinea equipos hacia objetivos medibles."
}
```

Matriz del puesto:

```json
{
  "nivel": "GERENCIAL",
  "competencia": "LIDERAZGO_ESTRATEGICO",
  "tipo": "GERENCIAL",
  "nivelComportamiento": "AVANZADO",
  "evidenciaEsperada": "Traduce objetivos estrategicos en planes anuales con responsables, indicadores y seguimiento ejecutivo."
}
```

## Recomendacion De UI

Pantallas sugeridas:

- `Configuracion > Catalogos de puesto`: administrar `Competency`, `CompetencyType`, `BehaviorLevel`, `Behavior`.
- `Configuracion > Piramide ocupacional`: administrar niveles jerarquicos.
- `Configuracion > Conductas de competencia`: crear conductas y asociar behaviors.
- `Perfiles de puesto > Detalle > Matriz de competencias`: asignar competencias esperadas al puesto.

Para los selects de la matriz:

- Primero cargar niveles ocupacionales activos.
- Luego cargar competencias activas.
- Luego cargar tipos activos.
- Luego cargar niveles de comportamiento activos.
- Al seleccionar competencia + tipo + nivel, filtrar conductas disponibles con:

```http
GET /api/v1/companies/{companyId}/competency-conducts?competencyId={competencyId}&competencyTypeId={competencyTypeId}&behaviorLevelId={behaviorLevelId}&isActive=true&page=1&pageSize=100
```

## Manejo De Errores En Frontend

Errores relevantes:

- `CONCURRENCY_CONFLICT`: refrescar datos y pedir al usuario reintentar.
- `COMPETENCY_CONDUCT_DUPLICATE`: ya existe una conducta igual.
- `OCCUPATIONAL_PYRAMID_LEVEL_ORDER_CONFLICT`: ya existe un nivel con ese orden.
- `OCCUPATIONAL_PYRAMID_LEVEL_IN_USE`: no se puede inactivar porque se usa en una matriz.
- `RESOURCE_IN_USE`: no se puede inactivar una conducta usada.
- `COMPETENCY_NOT_FOUND`: competencia inexistente o inactiva.
- `COMPETENCY_TYPE_NOT_FOUND`: tipo inexistente o inactivo.
- `BEHAVIOR_LEVEL_NOT_FOUND`: nivel de comportamiento inexistente o inactivo.
- `BEHAVIOR_NOT_FOUND`: comportamiento inexistente o inactivo.
- `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT`: matriz duplicada, conducta incompatible o perfil archivado.
- `COMPETENCY_FRAMEWORK_EXPORT_FORMAT_INVALID`: formato de export invalido.

## Checklist Para Frontend

- Usar siempre `id` publicos devueltos por la API.
- Guardar y reenviar `concurrencyToken` en updates, activate/inactivate y matriz.
- No hacer updates parciales de matriz: enviar siempre la matriz completa.
- Antes de guardar matriz, validar duplicados en UI por combinacion nivel + competencia + tipo + nivel de comportamiento.
- Al elegir conductas, filtrar por la misma competencia + tipo + nivel seleccionados.
- No permitir editar matriz si el perfil esta `Archived`.
- Si el usuario limpia todos los items y confirma, enviar `items: []`.
