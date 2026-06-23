# Guía de Integración Frontend — Equipo o Acceso del Empleado (Nivel A)

| | |
|---|---|
| **Tipo de documento** | Guía de desarrollo, flujo e integración para el Frontend |
| **Audiencia** | Equipo Frontend, UX/UI, QA |
| **Documento de negocio** | [`docs/business/analisis-equipo-acceso-empleado.md`](../business/analisis-equipo-acceso-empleado.md) |
| **Plan técnico** | [`docs/technical/plan-tecnico-equipo-acceso.md`](./plan-tecnico-equipo-acceso.md) |
| **Estado** | Implementado (Nivel A — endurecimiento). Backend compilando, 1852 unit tests verdes, migración aplicada. |
| **País de referencia** | El Salvador (SV) |
| **Fecha** | 2026-06-22 |

---

## 1. Qué se desarrolló (resumen)

La sección **"Equipo o acceso"** del expediente (computadora, teléfono, uniformes, licencias y accesos a sistemas con su nivel) **ya existía** como registro documental con CRUD completo. Esta entrega la **endurece** (Nivel A) para mejorar la calidad de datos, **sin** cambiar la forma de los campos.

Cambios que impactan al Frontend:

1. **El tipo de equipo/acceso es de catálogo.** `assetTypeCode` deja de ser texto libre: ahora debe ser un código del catálogo `asset-access-types`. → **selector (dropdown)**, ya no input de texto.
2. **El estado de entrega es de catálogo.** `deliveryStatusCode` (opcional) debe ser un código de `delivery-statuses` cuando se envía. → **selector**; para licencias/accesos sin entrega física usar `NO_APLICA` o dejarlo vacío.
3. **Coherencia de fechas (validación nueva):** `endDateUtc ≥ startDateUtc` (si hay baja) y `deliveryDateUtc ≥ startDateUtc` (si hay entrega). El backend rechaza fechas incoherentes (422).
4. **El nivel de acceso sigue siendo texto libre** (`accessLevelCode`): se mantiene como **input de texto opcional** (no catalogar).

> **Compatibilidad.** **No** hubo renombres de campos ni campos nuevos obligatorios; `endDateUtc` y `deliveryDateUtc` siguen **opcionales**. El payload no cambió de forma: lo único nuevo es que `assetTypeCode`/`deliveryStatusCode` ahora se validan contra catálogo y las fechas deben ser coherentes. **Acción FE:** convertir los inputs de "tipo" y "estado de entrega" en selectores de catálogo.

---

## 2. Modelo de datos (respuesta del API)

`PersonnelFileAssetAccessResponse` (JSON camelCase):

```jsonc
{
  "assetAccessPublicId": "f3c1…",      // id del registro
  "assetTypeCode": "EQUIPO_COMPUTO",   // código de catálogo (asset-access-types)
  "assetOrAccessName": "Dell Latitude 7440", // nombre/descripción libre (requerido)
  "accessLevelCode": "ADMIN",          // nivel de acceso, TEXTO LIBRE opcional
  "startDateUtc": "2026-07-01T00:00:00Z",    // fecha de alta (requerida)
  "endDateUtc": null,                  // fecha de baja (opcional)
  "deliveryDateUtc": "2026-07-02T00:00:00Z", // fecha de entrega (opcional)
  "deliveryStatusCode": "ENTREGADO",   // código de catálogo (delivery-statuses), opcional
  "isActive": true,
  "notes": "Asignado con cargador y mochila.", // observación (opcional)
  "concurrencyToken": "11aa…"          // usar en If-Match
}
```

- `concurrencyToken` se devuelve también en el header **`ETag`** y es obligatorio en **`If-Match`** de PUT/PATCH/DELETE.
- Campos **requeridos** en alta/edición: `assetTypeCode`, `assetOrAccessName`, `startDateUtc`. El resto es opcional.

---

## 3. Permisos y control de acceso

| Operación | Verbo | Permiso requerido |
|---|---|---|
| Listar / obtener | GET | `PersonnelFiles.Read` (o `Admin` / IAM super-admin) |
| Crear / editar / eliminar | POST/PUT/PATCH/DELETE | `PersonnelFiles.Manage` (o `Admin` / IAM super-admin) |

- Las rutas viven en el controlador de **empleo** (`PersonnelFileEmploymentController`); **no** hay permiso dedicado para equipo/acceso (a diferencia de sustituciones): se usa el `Manage` genérico del expediente.
- Sin permiso de lectura → **401/403**. Sin `Manage` (ni `Admin`) al escribir → **403** (`PERSONNEL_FILES_FORBIDDEN`).
- El expediente debe estar **completado** para gestionar **y** para leer (lectura usa `LoadCompletedEmployeeForRead`).

> El Frontend debe **ocultar/inhabilitar** crear/editar/eliminar si el usuario no tiene `PersonnelFiles.Manage` ni `PersonnelFiles.Admin`.

---

## 4. Catálogos de apoyo

Ambos son **país-scoped**: requieren el parámetro `countryCode` (p. ej. `SV`). Devuelven solo items **activos**, ordenados por `sortOrder`. Cada item es `PersonnelCatalogItemResponse`:

```jsonc
{ "id": "…", "category": "AssetAccessType", "code": "EQUIPO_COMPUTO", "name": "Equipo de cómputo", "isSystem": false, "isActive": true, "sortOrder": 10 }
```

Usar `code` como valor a enviar y `name` para mostrar.

### 4.1 Tipos de equipo/acceso (RF-102)

```
GET /api/v1/general-catalogs/asset-access-types?countryCode=SV
```

Semilla SV:

| code | name |
|---|---|
| `EQUIPO_COMPUTO` | Equipo de cómputo |
| `TELEFONO_MOVIL` | Teléfono móvil |
| `UNIFORME` | Uniforme |
| `LICENCIA_SOFTWARE` | Licencia de software |
| `ACCESO_SISTEMA` | Acceso a sistema |
| `MOBILIARIO` | Mobiliario |
| `HERRAMIENTA` | Herramienta |
| `OTRO` | Otro |

### 4.2 Estados de entrega (RF-103)

```
GET /api/v1/general-catalogs/delivery-statuses?countryCode=SV
```

Semilla SV:

| code | name |
|---|---|
| `PENDIENTE` | Pendiente |
| `ENTREGADO` | Entregado |
| `EN_USO` | En uso |
| `DEVUELTO` | Devuelto |
| `EXTRAVIADO` | Extraviado |
| `DANADO` | Dañado |
| `NO_APLICA` | No aplica |

> El `countryCode` sale del país de la empresa/tenant. Cachear ambos catálogos por sesión (cambian raramente).

---

## 5. Endpoints REST

Base: `/api/v1/personnel-files/{publicId}/assets-accesses` (donde `{publicId}` es el expediente del empleado, que debe estar **completado**).

| # | Método | Ruta | Permiso | Notas |
|---|---|---|---|---|
| 1 | GET | `/` | Read | Lista todos los equipos/accesos del empleado |
| 2 | GET | `/{assetAccessPublicId}` | Read | Un registro |
| 3 | POST | `/` | Manage | Crear → **201** + `Location` + `ETag` |
| 4 | PUT | `/{id}` | Manage | Reemplaza campos de negocio (no toca `isActive`); requiere `If-Match` |
| 5 | PATCH | `/{id}` | Manage | JSON Patch (incluye `isActive`); requiere `If-Match` |
| 6 | DELETE | `/{id}` | Manage | Requiere `If-Match`; devuelve el token del expediente padre |

### 5.1 Crear (POST)

```jsonc
// POST /api/v1/personnel-files/{empleadoId}/assets-accesses
{
  "assetTypeCode": "EQUIPO_COMPUTO",
  "assetOrAccessName": "Dell Latitude 7440",
  "accessLevelCode": null,                 // texto libre opcional
  "startDateUtc": "2026-07-01T00:00:00Z",
  "endDateUtc": null,                      // opcional
  "deliveryDateUtc": "2026-07-02T00:00:00Z", // opcional
  "deliveryStatusCode": "ENTREGADO",       // opcional (catálogo)
  "isActive": true,
  "notes": "Asignado con cargador."
}
```

Respuesta **201 Created**, header `Location` → recurso creado, header `ETag` → `concurrencyToken` inicial.

### 5.2 Editar (PUT)

```jsonc
// PUT /api/v1/personnel-files/{empleadoId}/assets-accesses/{id}
// Header: If-Match: "{concurrencyToken}"
{
  "assetTypeCode": "EQUIPO_COMPUTO",
  "assetOrAccessName": "Dell Latitude 7440 (reemplazo)",
  "accessLevelCode": null,
  "startDateUtc": "2026-07-01T00:00:00Z",
  "endDateUtc": "2026-12-31T00:00:00Z",
  "deliveryDateUtc": "2026-07-02T00:00:00Z",
  "deliveryStatusCode": "EN_USO",
  "notes": "Se actualiza el equipo."
}
```

- **PUT no modifica `isActive`** (se preserva). Para activar/desactivar usar PATCH.
- Devuelve **200** + nuevo `ETag`.

### 5.3 PATCH (JSON Patch, RFC 6902)

`Content-Type: application/json-patch+json`, header `If-Match` obligatorio.

```jsonc
[
  { "op": "replace", "path": "/deliveryStatusCode", "value": "DEVUELTO" },
  { "op": "replace", "path": "/endDateUtc", "value": "2026-12-31T00:00:00Z" },
  { "op": "replace", "path": "/isActive", "value": false }
]
```

Rutas soportadas: `/assetTypeCode`, `/assetOrAccessName`, `/accessLevelCode`, `/startDateUtc`, `/endDateUtc`, `/deliveryDateUtc`, `/deliveryStatusCode`, `/notes`, `/isActive`.

- **No removibles**: `/startDateUtc` y `/isActive` (un `remove` → 400). `assetTypeCode`/`assetOrAccessName` son requeridos: un `remove` los deja vacíos y la validación responde 400.
- **Removibles** (quedan en `null`): `/endDateUtc`, `/deliveryDateUtc`, `/accessLevelCode`, `/deliveryStatusCode`, `/notes`.
- Los nombres de ruta coinciden 1:1 con los del response (camelCase, sufijo `Utc` en fechas) — **sin** inconsistencias de naming.

### 5.4 DELETE

```
DELETE /api/v1/personnel-files/{empleadoId}/assets-accesses/{id}
Header: If-Match: "{concurrencyToken}"
```

Devuelve **200** con `{ "parentConcurrencyToken": "…" }` (token refrescado del expediente padre, útil para seguir editando sin un GET extra).

---

## 6. Concurrencia (If-Match / ETag)

1. GET el registro → leer `concurrencyToken` (o el header `ETag`).
2. En PUT/PATCH/DELETE enviar el header **`If-Match: "{concurrencyToken}"`**.
3. Si otro usuario lo modificó entre tanto → **409** `CONCURRENCY_CONFLICT`: refrescar (GET) y reintentar.
4. Tras una escritura exitosa, tomar el nuevo token del header `ETag` (o del body) para la siguiente operación.

---

## 7. Flujo de creación (perspectiva Frontend)

1. Abrir el expediente del empleado (debe estar **completado**; si no, el alta dará `PERSONNEL_FILE_STATE_RULE_VIOLATION`).
2. **Tipo** (`assetTypeCode`): selector cargado de `asset-access-types`.
3. **Nombre/descripción** (`assetOrAccessName`): texto libre requerido (marca/modelo, nombre del sistema, etc.).
4. **Nivel de acceso** (`accessLevelCode`): texto libre opcional (p. ej. para accesos a sistemas).
5. **Fecha de alta** (`startDateUtc`, requerida); **fecha de baja** y **fecha de entrega** opcionales.
6. **Estado de entrega** (`deliveryStatusCode`): selector de `delivery-statuses` (opcional; `NO_APLICA` para licencias/accesos).
7. **Observación** (`notes`) opcional. Enviar POST. Manejar errores según §8.

---

## 8. Validación y mapa de errores

Mensajes **bilingües (ES/EN)** con `code` estable; el body sigue ProblemDetails (`code`, `title`, `traceId`).

| Disparador | `code` | HTTP | Manejo sugerido en UI |
|---|---|---|---|
| `assetTypeCode` o `assetOrAccessName` vacío | `common.validation` | **400** | Marcar campos requeridos |
| Tipo fuera de catálogo | `ASSET_ACCESS_TYPE_CODE_INVALID` | **422** | Recargar catálogo / elegir un tipo válido |
| Estado de entrega fuera de catálogo | `ASSET_ACCESS_DELIVERY_STATUS_CODE_INVALID` | **422** | Recargar catálogo / elegir un estado válido |
| `endDateUtc < startDateUtc` | `ASSET_ACCESS_DATE_RANGE_INVALID` | **422** | "La fecha de baja no puede ser anterior al alta." |
| `deliveryDateUtc < startDateUtc` | `ASSET_ACCESS_DELIVERY_DATE_INVALID` | **422** | "La fecha de entrega no puede ser anterior al alta." |
| Empleado no completado | `PERSONNEL_FILE_STATE_RULE_VIOLATION` | **422** | Completar el expediente primero |
| Sin permiso de escritura | `PERSONNEL_FILES_FORBIDDEN` | **403** | Ocultar acciones de gestión |
| `If-Match` no coincide / ausente | `CONCURRENCY_CONFLICT` | **409** | Refrescar y reintentar |
| Registro no encontrado | `PERSONNEL_FILE_ITEM_NOT_FOUND` | **404** | — |

> La validación de catálogo aplica **solo en escritura**. Registros antiguos con tipo/estado de texto libre pueden seguir visibles en lecturas; al **editarlos**, el FE debe forzar la selección de un código de catálogo válido (un guardado con el valor viejo dará 422).

---

## 9. Estado efectivo (cálculo en cliente)

El API expone `isActive` + `startDateUtc` + `endDateUtc`. Sugerencia de estado derivado respecto a "hoy":

- `INACTIVO` — `isActive == false`.
- `VIGENTE` — `isActive == true` y (`endDateUtc` nulo **o** `hoy ≤ endDateUtc`).
- `DADO_DE_BAJA` — `isActive == true` pero `hoy > endDateUtc` (inconsistencia: conviene marcar `isActive=false`).

> A diferencia de plazas o sustituciones, **varios equipos/accesos coexisten** legítimamente (laptop + teléfono + uniforme + accesos a la vez): **no** hay reglas de solape ni "único activo". No filtres ni bloquees por superposición de fechas.

---

## 10. Fuera de alcance (Nivel B — diferido)

Estas mejoras **no** están implementadas; no las asumas en la UI (ver preguntas abiertas P-03…P-09 del análisis):

- **Devolución** con fecha + condición del activo (más allá de `DEVUELTO` en estado de entrega).
- **Identificación del activo**: número de serie/IMEI, etiqueta de inventario, cantidad, valor monetario.
- **Acta de entrega / responsiva** firmada y adjunto documental.
- **Integración con egreso** (listar pendientes de devolución / accesos por revocar).
- **Provisión/revocación real** de accesos (IAM) — el registro es **documental**.
- **Permiso dedicado**: hoy se usa `PersonnelFiles.Manage` genérico.
- **Catalogar el nivel de acceso**: `accessLevelCode` permanece texto libre.

---

## 11. Checklist de QA / casos de prueba

- [ ] Alta feliz (empleado completado, tipo de catálogo, nombre, fecha de alta) → 201.
- [ ] Tipo fuera de catálogo → 422 `ASSET_ACCESS_TYPE_CODE_INVALID`.
- [ ] Estado de entrega fuera de catálogo → 422 `ASSET_ACCESS_DELIVERY_STATUS_CODE_INVALID`.
- [ ] Estado de entrega vacío (licencia/acceso) → 201 (opcional); con `NO_APLICA` → 201.
- [ ] `endDateUtc < startDateUtc` → 422 `ASSET_ACCESS_DATE_RANGE_INVALID`.
- [ ] `deliveryDateUtc < startDateUtc` → 422 `ASSET_ACCESS_DELIVERY_DATE_INVALID`.
- [ ] Fechas opcionales nulas (sin baja / sin entrega) → 201.
- [ ] Dos equipos del mismo empleado con fechas solapadas → **permitido** (201).
- [ ] Empleado no completado → 422 `PERSONNEL_FILE_STATE_RULE_VIOLATION`.
- [ ] PUT no cambia `isActive`; PATCH `isActive=false` sí.
- [ ] PATCH `remove` sobre `/startDateUtc` o `/isActive` → 400; sobre `/endDateUtc` → lo deja nulo.
- [ ] PUT/PATCH/DELETE sin `If-Match` o con token viejo → 409.
- [ ] Usuario con solo `Read` intenta crear → 403; consulta → 200.
- [ ] `nivel de acceso` con texto arbitrario → aceptado (sigue libre).

---

## 12. Trazabilidad

| Frontend | RF |
|---|---|
| Selector de tipo (catálogo `asset-access-types`) | RF-102 |
| Selector de estado de entrega (catálogo `delivery-statuses`) | RF-103 |
| Validación de coherencia de fechas | RF-101 |
| Nivel de acceso como texto libre | RF-104 (Nivel B, no aplicado) |
| Concurrencia If-Match/ETag | RNF |

> **Cobertura de pruebas backend.** Reglas puras de fechas (`AssetAccessRulesTests`), paridad de localización de los 4 códigos `ASSET_ACCESS_*` y bijección de catálogos (`GeneralCatalogKeyMap`) pasan (1852 unit tests verdes). Los casos de §11 quedan como verificación de integración cuando se siembre un empleado completado en el harness.
