# Integracion Frontend - Catalogo de Clase Salarial

## Proposito

Esta guia define como debe integrar el frontend el catalogo de **Clase Salarial** para soportar la creacion y uso de lineas del tabulador salarial.

La Clase Salarial no se captura como texto libre dentro del tabulador. Es un catalogo administrable por tenant y el tabulador consume su `id` publico.

## Conceptos

| Concepto funcional | En API | Uso frontend |
| --- | --- | --- |
| Clase Salarial | `salary-classes` | Catalogo administrable de clases salariales. |
| Linea de tabulador | `salary-tabulator/lines` | Linea salarial activa aprobada. |
| Change request de tabulador | `salary-tabulator/change-requests` | Flujo de aprobacion que materializa lineas salariales. |

## Permisos

Todas las rutas requieren autenticacion y tenant activo.

Para leer clases salariales:

- `PositionDescriptionCatalogs.Read`
- `PositionDescriptionCatalogs.Admin`
- `iam.administration.manage`

Para crear, editar, activar o inactivar clases salariales:

- `PositionDescriptionCatalogs.Admin`
- `iam.administration.manage`

Para operar change requests del tabulador salarial se requieren permisos del modulo de tabulador salarial segun la accion del usuario.

## Endpoints del Catalogo

### Listar clases salariales

Usar este endpoint para llenar selects, filtros y pantallas administrativas:

```http
GET /api/v1/companies/{companyId}/salary-classes?isActive=true&page=1&pageSize=100
```

Filtros soportados:

| Parametro | Tipo | Uso |
| --- | --- | --- |
| `isActive` | `boolean?` | Filtrar activos o inactivos. |
| `q` | `string?` | Buscar por `code` o `name`. |
| `page` | `number` | Pagina. |
| `pageSize` | `number` | Tamano de pagina. |
| `includeAllowedActions` | `boolean` | Incluir acciones permitidas por item cuando la UI las necesite. |

Respuesta:

```json
{
  "items": [
    {
      "id": "0e686a93-30e4-4c5f-bc39-2c060710e63e",
      "catalogType": "SalaryClass",
      "code": "CLS-A",
      "name": "Clase Salarial A",
      "description": "Puestos administrativos junior.",
      "sortOrder": 1,
      "isActive": true,
      "concurrencyToken": "c8dfa0fb-e6b5-4721-ba9d-b57181f9ccea",
      "createdAtUtc": "2026-01-01T00:00:00Z",
      "modifiedAtUtc": null
    }
  ],
  "pageNumber": 1,
  "pageSize": 100,
  "totalCount": 1
}
```

### Obtener una clase salarial

```http
GET /api/v1/salary-classes/{salaryClassId}
```

Usar este endpoint cuando la UI necesite refrescar un item puntual antes de editar, activar o inactivar.

### Crear una clase salarial

```http
POST /api/v1/companies/{companyId}/salary-classes
Content-Type: application/json
```

```json
{
  "code": "CLS-A",
  "name": "Clase Salarial A",
  "description": "Puestos administrativos junior.",
  "sortOrder": 1
}
```

Reglas de UI recomendadas:

- `code` debe ser estable y legible para negocio.
- `name` es el texto principal visible en selects y tablas.
- `description` es opcional.
- `sortOrder` controla el orden funcional.

### Actualizar una clase salarial

```http
PUT /api/v1/salary-classes/{salaryClassId}
Content-Type: application/json
```

```json
{
  "code": "CLS-A",
  "name": "Clase Salarial A",
  "description": "Puestos administrativos junior.",
  "sortOrder": 1,
  "concurrencyToken": "c8dfa0fb-e6b5-4721-ba9d-b57181f9ccea"
}
```

El `concurrencyToken` debe venir del ultimo `GET` o `search`.

### Activar o inactivar una clase salarial

```http
PATCH /api/v1/salary-classes/{salaryClassId}/activate
Content-Type: application/json
```

```json
{
  "concurrencyToken": "c8dfa0fb-e6b5-4721-ba9d-b57181f9ccea"
}
```

```http
PATCH /api/v1/salary-classes/{salaryClassId}/inactivate
Content-Type: application/json
```

```json
{
  "concurrencyToken": "c8dfa0fb-e6b5-4721-ba9d-b57181f9ccea"
}
```

## Donde Consumirlo en Frontend

### 1. Pantalla de catalogos

Agregar o reutilizar una seccion de **Catalogos de descripcion de puesto** para administrar **Clases Salariales**.

La UI deberia permitir:

- listar clases salariales;
- buscar por codigo o nombre;
- crear una nueva clase salarial;
- editar una clase existente;
- activar o inactivar con control de concurrencia.

### 2. Pantalla de tabulador salarial

En el formulario de creacion o modificacion de change request del tabulador, el campo **Clase Salarial** debe ser un selector alimentado por:

```http
GET /api/v1/companies/{companyId}/salary-classes?isActive=true&page=1&pageSize=100
```

El selector debe mostrar preferiblemente:

```text
{code} - {name}
```

El valor seleccionado que se envia al backend es:

```text
item.id -> salaryClassPublicId
```

### 3. Filtros de lineas del tabulador

Para filtrar lineas por clase salarial, reutilizar el mismo catalogo activo.

```http
GET /api/v1/companies/{companyId}/salary-tabulator/lines?salaryClassId={salaryClassId}&page=1&pageSize=20
```

Nota: el filtro de lineas usa `salaryClassId` en query string, pero corresponde al mismo `id` publico retornado por `salary-classes`.

## Flujo Recomendado

### Crear una clase salarial y usarla en el tabulador

1. El usuario entra a catalogos y crea la clase salarial.
2. El frontend llama:

```http
POST /api/v1/companies/{companyId}/salary-classes
```

3. La API responde el item creado con `id`.
4. El usuario entra al tabulador salarial.
5. El frontend lista clases activas:

```http
GET /api/v1/companies/{companyId}/salary-classes?isActive=true&page=1&pageSize=100
```

6. El usuario selecciona la clase salarial.
7. El frontend crea un change request usando `salaryClassPublicId`.

```http
POST /api/v1/companies/{companyId}/salary-tabulator/change-requests
Content-Type: application/json
```

```json
{
  "effectiveFromUtc": "2026-01-01T00:00:00Z",
  "effectiveToUtc": null,
  "items": [
    {
      "salaryClassPublicId": "0e686a93-30e4-4c5f-bc39-2c060710e63e",
      "salaryScaleCode": "S1",
      "currencyCode": "USD",
      "changeType": "Create",
      "proposedBaseAmount": 1000,
      "proposedMinAmount": 900,
      "proposedMaxAmount": 1200,
      "notes": "Linea inicial para Clase Salarial A."
    }
  ]
}
```

## Reglas Importantes

- No enviar `name` ni `code` de la clase salarial al tabulador como fuente canonica.
- Enviar las lineas del `POST` de creacion dentro de `items[]`; cada item usa el mismo contrato de linea que el `PUT`.
- No enviar `reason` en el `POST` de creacion; el backend conserva una razon interna por defecto.
- No permitir texto libre para Clase Salarial en el formulario de tabulador.
- El backend resuelve internamente el `code` desde `salaryClassPublicId`.
- Si el catalogo esta vacio, la UI debe dirigir al usuario a crear clases salariales antes de crear lineas del tabulador.
- Solo usar clases salariales activas para nuevos change requests.
- Refrescar el item si la API responde conflicto de concurrencia al editar, activar o inactivar.

## Errores Comunes

| Caso | Respuesta esperada | Accion sugerida en UI |
| --- | --- | --- |
| Clase salarial inexistente o inactiva al crear change request | `SALARY_CLASS_NOT_FOUND` | Pedir al usuario seleccionar una clase activa. |
| Codigo duplicado al crear catalogo | `POSITION_DESCRIPTION_CATALOG_CODE_CONFLICT` | Mostrar que ya existe una clase con ese codigo. |
| Token de concurrencia desactualizado | `CONCURRENCY_CONFLICT` | Recargar el item y pedir reintento. |
| Usuario sin permisos de lectura | `POSITION_DESCRIPTION_CATALOG_FORBIDDEN` | Ocultar selector o mostrar acceso denegado. |
| Usuario sin permisos de escritura | `POSITION_DESCRIPTION_CATALOG_FORBIDDEN` | Deshabilitar acciones de crear/editar/inactivar. |
