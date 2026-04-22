# Integracion Frontend - Catalogos de Competencias

## Objetivo

El frontend debe permitir administrar los catalogos base necesarios para construir el modulo de competencias, conductas esperadas y matriz de competencias por perfil de puesto.

Estos catalogos son administrables por empresa/tenant y deben crearse desde pantalla antes de configurar conductas o matrices.

## Pantalla Sugerida

Ruta sugerida en frontend:

```text
Configuracion > Catalogos de puesto
```

La pantalla debe permitir administrar estas categorias:

| Catalogo | Category API | Uso |
| --- | --- | --- |
| Competencias | `Competency` | Capacidades o habilidades requeridas para un puesto. |
| Tipos de competencia | `CompetencyType` | Clasificacion de la competencia: tecnica, conductual, gerencial, transversal, etc. |
| Niveles de comportamiento | `BehaviorLevel` | Nivel esperado del comportamiento: basico, intermedio, avanzado, experto, etc. |
| Comportamientos observables | `Behavior` | Evidencias observables concretas asociables a una conducta. |

## Endpoints

### Listar Items De Un Catalogo

```http
GET /api/v1/companies/{companyId}/job-catalogs/{category}?isActive=true&page=1&pageSize=100
```

Ejemplos:

```http
GET /api/v1/companies/{companyId}/job-catalogs/Competency?isActive=true&page=1&pageSize=100
GET /api/v1/companies/{companyId}/job-catalogs/CompetencyType?isActive=true&page=1&pageSize=100
GET /api/v1/companies/{companyId}/job-catalogs/BehaviorLevel?isActive=true&page=1&pageSize=100
GET /api/v1/companies/{companyId}/job-catalogs/Behavior?isActive=true&page=1&pageSize=100
```

### Buscar Dentro Del Catalogo

```http
GET /api/v1/companies/{companyId}/job-catalogs/{category}?q={search}&page=1&pageSize=20
```

El parametro `q` busca por `code` y `name`.

### Crear Item

```http
POST /api/v1/companies/{companyId}/job-catalogs/{category}
Content-Type: application/json
```

Body:

```json
{
  "code": "COMUNICACION_EFECTIVA",
  "name": "Comunicacion efectiva"
}
```

Respuesta conceptual:

```json
{
  "id": "public-id-del-item",
  "category": "Competency",
  "code": "COMUNICACION_EFECTIVA",
  "name": "Comunicacion efectiva",
  "isSystem": false,
  "isActive": true,
  "concurrencyToken": "token-de-concurrencia"
}
```

### Activar Item

```http
PATCH /api/v1/job-catalogs/{catalogItemId}/activate
Content-Type: application/json
```

```json
{
  "concurrencyToken": "token-de-concurrencia"
}
```

### Inactivar Item

```http
PATCH /api/v1/job-catalogs/{catalogItemId}/inactivate
Content-Type: application/json
```

```json
{
  "concurrencyToken": "token-de-concurrencia"
}
```

## Catalogos Iniciales Recomendados

Estos valores pueden cargarse manualmente desde la pantalla de administracion. El frontend debe crear cada item usando el endpoint `POST /api/v1/companies/{companyId}/job-catalogs/{category}` con la categoria correspondiente.

### Competencias

```json
[
  {
    "category": "Competency",
    "code": "COMUNICACION_EFECTIVA",
    "name": "Comunicacion efectiva"
  },
  {
    "category": "Competency",
    "code": "ORIENTACION_SERVICIO",
    "name": "Orientacion al servicio"
  },
  {
    "category": "Competency",
    "code": "ANALISIS_DATOS",
    "name": "Analisis de datos"
  },
  {
    "category": "Competency",
    "code": "LIDERAZGO_ESTRATEGICO",
    "name": "Liderazgo estrategico"
  },
  {
    "category": "Competency",
    "code": "TRABAJO_EQUIPO",
    "name": "Trabajo en equipo"
  },
  {
    "category": "Competency",
    "code": "RESOLUCION_PROBLEMAS",
    "name": "Resolucion de problemas"
  }
]
```

### Tipos De Competencia

```json
[
  {
    "category": "CompetencyType",
    "code": "TECNICA",
    "name": "Tecnica"
  },
  {
    "category": "CompetencyType",
    "code": "CONDUCTUAL",
    "name": "Conductual"
  },
  {
    "category": "CompetencyType",
    "code": "GERENCIAL",
    "name": "Gerencial"
  },
  {
    "category": "CompetencyType",
    "code": "TRANSVERSAL",
    "name": "Transversal"
  }
]
```

### Niveles De Comportamiento

```json
[
  {
    "category": "BehaviorLevel",
    "code": "BASICO",
    "name": "Basico"
  },
  {
    "category": "BehaviorLevel",
    "code": "INTERMEDIO",
    "name": "Intermedio"
  },
  {
    "category": "BehaviorLevel",
    "code": "AVANZADO",
    "name": "Avanzado"
  },
  {
    "category": "BehaviorLevel",
    "code": "EXPERTO",
    "name": "Experto"
  }
]
```

### Comportamientos Observables

```json
[
  {
    "category": "Behavior",
    "code": "ESCUCHA_ACTIVA",
    "name": "Escucha activamente antes de responder"
  },
  {
    "category": "Behavior",
    "code": "COMUNICA_CLARO",
    "name": "Comunica informacion de forma clara y oportuna"
  },
  {
    "category": "Behavior",
    "code": "DOCUMENTA_HALLAZGOS",
    "name": "Documenta hallazgos con evidencia"
  },
  {
    "category": "Behavior",
    "code": "PRIORIZA_CLIENTE",
    "name": "Prioriza necesidades del cliente interno"
  },
  {
    "category": "Behavior",
    "code": "ANALIZA_DATOS",
    "name": "Analiza datos antes de proponer decisiones"
  },
  {
    "category": "Behavior",
    "code": "ALINEA_EQUIPOS",
    "name": "Alinea equipos hacia objetivos comunes"
  },
  {
    "category": "Behavior",
    "code": "DA_SEGUIMIENTO",
    "name": "Da seguimiento oportuno a compromisos y acuerdos"
  },
  {
    "category": "Behavior",
    "code": "RESUELVE_CONFLICTOS",
    "name": "Resuelve conflictos con criterio y respeto"
  }
]
```

## Reglas De Frontend

- Usar siempre el `id` publico devuelto por la API para relacionar catalogos con conductas y matrices.
- No hardcodear IDs.
- El `code` debe enviarse en mayusculas, sin espacios, usando `_` como separador.
- El `name` debe ser el texto visible para el usuario.
- Guardar el `concurrencyToken` para activar o inactivar items.
- Mostrar solamente items activos en selects funcionales.
- Permitir ver items inactivos en la pantalla administrativa mediante filtro.
- No eliminar items; usar inactivar.
- Si un item se inactiva, ya no debe aparecer como opcion nueva en conductas o matrices.
- La API no tiene endpoint de edicion de `code/name` para estos catalogos; si se requiere corregir un valor, la opcion actual es inactivar y crear uno nuevo.

## Flujo Recomendado De Pantalla

1. Mostrar tabs o selector por categoria:
   - Competencias
   - Tipos de competencia
   - Niveles de comportamiento
   - Comportamientos observables

2. Al seleccionar una categoria:
   - Consultar `GET /api/v1/companies/{companyId}/job-catalogs/{category}`.
   - Mostrar tabla con `code`, `name`, `isActive`.
   - Permitir busqueda por `q`.
   - Permitir filtro activos/inactivos.

3. Para crear:
   - Solicitar `code`.
   - Solicitar `name`.
   - Enviar `POST /job-catalogs/{category}`.

4. Para activar o inactivar:
   - Usar el `id` y `concurrencyToken` del item.
   - Refrescar listado despues de la operacion.

## Uso Posterior En Competencias

Despues de crear estos catalogos, el frontend podra usarlos en las pantallas de conductas esperadas y matriz de competencias.

### Crear Conductas Esperadas

```http
POST /api/v1/companies/{companyId}/competency-conducts
Content-Type: application/json
```

```json
{
  "competencyPublicId": "id-competency",
  "competencyTypePublicId": "id-competency-type",
  "behaviorLevelPublicId": "id-behavior-level",
  "description": "Comunica informacion clara, oportuna y adaptada al interlocutor.",
  "sortOrder": 1
}
```

### Asociar Comportamientos Observables A Una Conducta

```http
PUT /api/v1/competency-conducts/{conductId}/behaviors
Content-Type: application/json
```

```json
{
  "behaviors": [
    {
      "behaviorPublicId": "id-behavior",
      "notes": "Escucha y confirma entendimiento antes de responder.",
      "sortOrder": 1
    }
  ],
  "concurrencyToken": "conduct-concurrency-token"
}
```

### Usar En Matriz De Competencias Del Perfil

```http
PUT /api/v1/job-profiles/{jobProfileId}/competency-matrix
Content-Type: application/json
```

Los selects de la matriz deben usar:

- `Competency`
- `CompetencyType`
- `BehaviorLevel`
- conductas filtradas por competencia + tipo + nivel

Para filtrar conductas disponibles:

```http
GET /api/v1/companies/{companyId}/competency-conducts?competencyId={competencyId}&competencyTypeId={competencyTypeId}&behaviorLevelId={behaviorLevelId}&isActive=true&page=1&pageSize=100
```

## Permisos

Para leer estos catalogos, el usuario debe tener alguno de:

- `JobProfiles.Read`
- `JobProfiles.Admin`
- `JobCatalogs.Admin`
- `iam.administration.manage`

Para crear, activar o inactivar:

- `JobCatalogs.Admin`
- `iam.administration.manage`

## Notas Importantes

- Estos catalogos son tenant-scoped: cada empresa administra sus propios valores.
- No son catalogos globales fijos.
- Actualmente no hay seed automatico especifico para `Competency`, `CompetencyType`, `BehaviorLevel` y `Behavior`.
- Si se quiere que cada empresa nazca con estos valores precargados, debe implementarse un proceso de seed/onboarding tenant-scoped en backend.
- La pantalla frontend debe considerar estado vacio y permitir crear los valores iniciales.
