# Guia de integracion Frontend — Catalogos internos de requisitos para Job Profiles

## 1. Proposito

Este documento explica como debe integrarse frontend con los nuevos catalogos internos globales usados en `Job Profiles > Requirements`.

Fuentes canonicas del contrato:

- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

Esta guia no reemplaza esos documentos. Su objetivo es explicar el flujo recomendado de integracion, el comportamiento esperado de la UI y como debe consumir frontend la nueva superficie.

## 2. Que cambio

El backend ahora expone una superficie global y autenticada para resolver valores reutilizables de requisitos.

Esto afecta estos tipos de requisito de `Job Profile`:

- `Education` -> `Search`
- `Knowledge` -> `Search`
- `Certification` -> `Search`
- `Experience` -> `FreeText`
- `Other` -> `FreeText`

Reglas importantes:

- estos catalogos son globales para toda la plataforma
- no se separan por empresa
- no requieren `tenantId` activo
- solo requieren un token `core` autenticado
- el payload actual de `create/update job profile` no cambia en v1

## 3. Endpoints que frontend debe usar

### 3.1 Obtener manifest

Este endpoint sirve para que frontend sepa como renderizar cada tipo de requisito.

`GET /api/account/internal-catalogs?context=job-profile.requirements`

Ejemplo de respuesta:

```json
[
  {
    "context": "job-profile.requirements",
    "identifier": "Education",
    "label": "Education",
    "renderType": "Search",
    "catalogKey": "job-profile.requirements.education",
    "allowCreate": true,
    "minQueryLength": 2
  },
  {
    "context": "job-profile.requirements",
    "identifier": "Experience",
    "label": "Experience",
    "renderType": "FreeText",
    "catalogKey": null,
    "allowCreate": false,
    "minQueryLength": 0
  }
]
```

### 3.2 Buscar valores del catalogo

Usar solo cuando `renderType = "Search"` o en el futuro cuando se use `Select`.

`GET /api/account/internal-catalogs/{catalogKey}/values?q={text}&limit=10`

Ejemplo:

`GET /api/account/internal-catalogs/job-profile.requirements.certification/values?q=Azure AI Fund&limit=10`

Ejemplo de respuesta:

```json
[
  {
    "id": "6d3f8517-97d5-4b80-8d87-d8c3f598f0f9",
    "value": "Azure AI Fundamentals",
    "score": 0.93
  }
]
```

### 3.3 Crear valor de forma explicita

Este endpoint es opcional. Solo usarlo si frontend quiere dar feedback inmediato antes de guardar el `Job Profile`.

`POST /api/account/internal-catalogs/{catalogKey}/values`

Body:

```json
{
  "value": "Azure AI Fundamentals"
}
```

Posibles resultados:

- `200 OK`: el valor ya existia exactamente y se reutilizo
- `201 Created`: el valor se creo
- `409 Conflict`: ya existe un valor muy parecido

Ejemplo de `409`:

```json
{
  "title": "A similar internal catalog value already exists.",
  "status": 409,
  "code": "internal_catalogs.similar_value_conflict",
  "suggestions": [
    {
      "id": "6d3f8517-97d5-4b80-8d87-d8c3f598f0f9",
      "value": "Azure AI Fundamentals A",
      "score": 0.95
    }
  ]
}
```

## 4. Flujo recomendado para frontend

Flujo recomendado para v1:

1. Cargar el manifest cuando se abre la pantalla de crear o editar `Job Profile`.
2. Guardarlo en estado de pagina o cache de sesion.
3. Cuando el usuario selecciona un `requirementType`, resolver su configuracion por `identifier`.
4. Renderizar el input segun `renderType`.
5. Para tipos `Search`, seguir enviando el valor final en `requirements[].description`.
6. Guardar el `Job Profile` normalmente.
7. Despues del save, tomar la respuesta del backend como fuente de verdad.

Este es el flujo recomendado porque:

- no rompe el contrato actual de `Job Profile`
- deja al backend decidir reuse, similitud y creacion
- evita una llamada extra de creacion antes de cada save

## 5. Reglas de render segun `renderType`

### `Search`

Renderizar un autocomplete asincrono.

Comportamiento recomendado:

1. No buscar antes de `minQueryLength`.
2. Hacer debounce entre `250ms` y `400ms`.
3. Buscar contra el endpoint de valores.
4. Mostrar las sugerencias tal como las devuelve backend.
5. Si no hay una opcion suficiente y `allowCreate = true`, mostrar una accion tipo:
   `Agregar "Azure AI Fundamentals"`

Buenas practicas:

- hacer `trim()` antes de enviar
- no buscar con texto vacio
- usar `limit` entre `5` y `10`
- respetar el orden devuelto por backend

### `FreeText`

Renderizar input de texto normal.

No llamar endpoints de catalogos internos.

### `Select`

El contrato ya lo soporta, pero hoy no se usa en `job-profile.requirements`.

Frontend puede dejar el soporte preparado, pero no es obligatorio para esta primera integracion.

## 6. Patrones validos para campos `Search`

Frontend tiene 2 formas validas de trabajar.

### Patron A — Recomendado

No llamar `POST /api/account/internal-catalogs/{catalogKey}/values` directamente.

En este patron frontend:

- deja el texto elegido o escrito en `description`
- guarda el `Job Profile`
- deja que backend resuelva el valor final automaticamente

Comportamiento de backend durante `create/update job profile`:

- match exacto -> reutiliza el valor existente
- similitud `>= 0.90` -> reutiliza el valor similar existente
- sin match cercano -> crea un valor global nuevo

Importante:

Como backend puede reemplazar el texto escrito por un valor canonico ya existente, frontend debe refrescar su estado local con la respuesta del save.

### Patron B — Opcional con UX mas rica

Llamar `POST /api/account/internal-catalogs/{catalogKey}/values` cuando el usuario haga click en `Agregar`.

En este patron frontend debe manejar:

- `200`: usar el `value` devuelto
- `201`: usar el `value` devuelto
- `409`: mostrar sugerencias y dejar que el usuario elija una

Este patron sirve si producto quiere confirmacion explicita antes de guardar el perfil.

## 7. Integracion con payload de Job Profile

El payload no cambia.

Frontend debe seguir enviando algo como esto:

```json
{
  "requirements": [
    {
      "requirementType": "Certification",
      "catalogItemPublicId": null,
      "catalogCode": null,
      "catalogName": null,
      "description": "Azure AI Fundamentals",
      "sortOrder": 1
    }
  ]
}
```

Notas importantes:

- `description` sigue siendo el valor que backend usa para el flujo del catalogo interno global
- `allowInlineCatalogCreate` no aplica para esta nueva funcionalidad
- no hay que activar `allowInlineCatalogCreate` para este caso
- el flujo viejo de `job-catalogs` tenant-scoped sigue separado

## 8. Reglas por tipo de requisito

### `Education`

- renderizar como `Search`
- usar `catalogKey` del manifest
- permitir agregar si no existe

### `Knowledge`

- renderizar como `Search`
- usar `catalogKey` del manifest
- permitir agregar si no existe

### `Certification`

- renderizar como `Search`
- usar `catalogKey` del manifest
- permitir agregar si no existe

### `Experience`

- renderizar como `FreeText`
- no llamar endpoints de catalogos internos

### `Other`

- renderizar como `FreeText`
- no llamar endpoints de catalogos internos

## 9. Manejo de errores

### `400`

Tratarlo como validacion normal y mostrar mensaje de formulario.

### `401`

El usuario no esta autenticado. Redirigir o refrescar el flujo de auth.

### `404`

El `context` o el `catalogKey` no existe. Tratarlo como error de configuracion. Si hace falta, frontend puede hacer fallback temporal a input libre.

### `409`

Solo se espera cuando frontend usa el create explicito.

UX recomendada:

1. Mostrar mensaje indicando que ya existe un valor muy parecido.
2. Mostrar `suggestions`.
3. Permitir que el usuario seleccione una sugerencia en vez de crear otro valor.

## 10. Cache recomendada

- cachear el manifest por sesion
- no cachear permanentemente resultados de busqueda entre usuarios
- si se desea, cachear localmente por `catalogKey + q` dentro de la sesion actual del formulario

## 11. Checklist de implementacion

- cargar manifest al abrir create/edit de `Job Profile`
- mapear `requirementType` -> definicion por `identifier`
- renderizar dinamicamente segun `renderType`
- agregar debounce a inputs `Search`
- bloquear busqueda antes de `minQueryLength`
- seguir enviando el valor final en `description`
- despues de guardar, reemplazar el estado local con la respuesta del backend
- si se implementa create explicito, manejar `200`, `201` y `409`

## 12. Recomendacion final

Para la primera iteracion de frontend, implementar solo:

- carga del manifest
- render dinamico por `renderType`
- autocomplete para tipos `Search`
- save normal de `Job Profile` usando `description`

Con eso ya se obtiene todo el valor principal del cambio, sin aumentar complejidad innecesaria en frontend y dejando al backend como fuente unica de verdad para similitud, reuse y creacion.
