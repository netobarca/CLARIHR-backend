# Personnel Files — Convenciones de consumo (frontend)

> Reglas transversales para **todos** los endpoints de Personnel Files y sus sub‑recursos.
> Cada documento de sub‑recurso asume estas reglas y solo documenta lo específico de su contrato.
> Fuente de verdad: el contrato Swagger en runtime (`/swagger/v1/swagger.json`), no los DTOs de C#.

---

## 1. Base URL y versionado

```
https://<host>/api/v1
```

Todas las rutas viven bajo `/api/v1`. La búsqueda y creación del archivo (shell) son **scoped por compañía** (`/companies/{companyPublicId}/personnel-files`); el resto opera sobre un archivo ya existente (`/personnel-files/{publicId}/...`).

## 2. Autenticación y permisos

- **Bearer JWT** en `Authorization: Bearer <token>` en toda request.
- Permisos (RBAC):
  - **Lectura** (`GET`): requiere `PersonnelFiles.Read` (o `Admin`).
  - **Escritura** (`POST` / `PUT` / `PATCH` / `DELETE`): requiere `PersonnelFiles.Manage` / `PersonnelFiles.Admin`.
- Un usuario solo‑lectura recibe **200** en GET y **403** en cualquier escritura.
- Todo está aislado por **tenant**: nunca se puede acceder a un archivo de otra compañía (responde `404`/`403`).

## 3. Identificadores (`publicId`)

- En el wire **siempre** se usan GUIDs `publicId`; los ids internos del backend nunca se exponen.
- El id del archivo padre es `publicId` (en la ruta).
- El id de cada ítem de sub‑recurso se llama `<recurso>PublicId` (ej. `addressPublicId`, `salaryItemPublicId`) tanto en la ruta como en las respuestas.

## 4. Concurrencia optimista — `If-Match` (IMPORTANTE)

Cada recurso/ítem trae un `concurrencyToken` (GUID) en su respuesta y en el header `ETag`.

- **`PUT` / `PATCH` / `DELETE` REQUIEREN** el header **`If-Match`** con el `concurrencyToken` **del propio ítem** (no del padre), citado como ETag:

  ```
  If-Match: "8f3a1c2e-...-d4b5"
  ```

- Es el token **del ítem que estás modificando** (lo obtenés del `GET` del ítem o de la respuesta de la operación anterior). El de un sub‑recurso **no** es el del personnel file padre.
- Si el token no coincide → **409 `CONCURRENCY_CONFLICT`** (alguien lo modificó; recargá y reintentá).
- Si falta o está malformado el header → **400**.
- Cada operación exitosa devuelve el **nuevo** token (en el body y en `ETag`); usá ese para la siguiente operación.
- `DELETE` de un ítem de sub‑recurso devuelve `{ "parentConcurrencyToken": "..." }` (el token del archivo padre tras quitar el ítem).

> El único endpoint que pide el token del archivo **padre** en `If-Match` es la propia mutación del shell (`PUT`/`PATCH /personnel-files/{publicId}`) y `finalize`.

## 5. `PATCH` = JSON Patch (RFC 6902) — formato de array desnudo

> ⚠️ El esquema que muestra Swagger para PATCH (`{ "operations": [...] }`) es **engañoso**. El wire real es un **array desnudo** de operaciones.

- **Content-Type:** `application/json-patch+json`
- **Body:** un array JSON de operaciones RFC 6902, **sin** envoltorio `operations`:

  ```json
  [
    { "op": "replace", "path": "/addressLine", "value": "Calle Nueva 123" },
    { "op": "replace", "path": "/isCurrent", "value": false }
  ]
  ```

- Operaciones soportadas: `add`, `replace`, `remove`. Solo paths de primer nivel (campos raíz del recurso). Límite de operaciones por documento y tamaño de body acotados (excederlos → `400` / `413`).
- `PATCH` aplica cambios parciales; `PUT` reemplaza todos los campos de negocio del ítem.

## 6. Crear (`POST`)

- **No** lleva `If-Match` (es un ítem nuevo, no hay nada con qué chocar).
- Responde **201 Created** con el ítem creado, el header `Location` apuntando al nuevo recurso y el header `ETag` con su `concurrencyToken` inicial.

## 7. Paginación (en endpoints de búsqueda)

Query params: `page` (1‑based, default `1`) y `pageSize` (default `20`, **máx `100`**). Fuera de rango → `400`.

Forma de la respuesta paginada:

```json
{
  "items": [ /* ... */ ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 137
}
```

Las listas de sub‑recursos (`GET /personnel-files/{id}/<recurso>`) **no** son paginadas: devuelven el array completo del archivo.

## 8. Errores (ProblemDetails)

Todos los errores usan `application/problem+json` con un `code` estable:

| HTTP | Cuándo |
|------|--------|
| `400` | Validación, `If-Match` faltante/malformado, JSON Patch inválido |
| `401` | Falta o expiró el token |
| `403` | Sin permiso (o tenant ajeno) |
| `404` | El archivo o el ítem no existe en esta compañía |
| `409` | Conflicto de concurrencia (`CONCURRENCY_CONFLICT`) o regla de negocio |
| `413` | Body / archivo excede el límite |
| `422` | Regla de estado/negocio (ej. operación no válida para el estado actual del archivo) |
| `429` | Rate limit excedido (endpoints de creación/búsqueda/export) |

Ejemplo de cuerpo de error:

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Conflict",
  "status": 409,
  "code": "CONCURRENCY_CONFLICT",
  "detail": "The resource was modified by another request. Refresh and try again."
}
```

## 9. Sub‑recursos de empleado (Talent / Compensation / Employment)

Algunos sub‑recursos (salary‑items, evaluations, employment‑assignments, etc.) solo se pueden crear/editar sobre un personnel file **finalizado** (empleado activo). Sobre un archivo en `Draft` responden **422** (regla de estado). Ver el doc de cada uno y [`personnel-files.md`](./personnel-files.md) (finalize).

---

### Índice

Volvé al [README](./README.md) para la lista completa de documentos por recurso.
