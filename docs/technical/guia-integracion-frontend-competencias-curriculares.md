# Guía de Integración Frontend + API — Competencias Curriculares del Empleado (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Guía de integración Frontend / Contrato de API |
| **Audiencia** | Equipo Frontend, QA, Integraciones |
| **Módulo** | Expedientes de Personal (`PersonnelFiles` → área *Talent*) |
| **Documentos relacionados** | `docs/business/analisis-competencias-curriculares-empleado.md` (negocio, D-01…D-08) · `docs/technical/plan-tecnico-competencias-curriculares.md` (plan técnico) |
| **Estado** | **Implementado (Fase 1 — endurecimiento).** Backend en `feature/competencias-curriculares-fase1`. |
| **Base URL** | `/api/v1` |
| **País de referencia** | El Salvador (SV) |
| **Idioma de errores** | Bilingüe (ES/EN) según `Accept-Language` |

---

## 1. Resumen y qué cambió en esta fase

La opción **"Competencias curriculares"** del expediente del empleado permite registrar las competencias/requisitos cumplidos por el trabajador. **El CRUD ya existía**; la Fase 1 lo **endurece** enlazando tres campos a **catálogos** y agregando validaciones. **No cambia la forma del contrato REST** (los campos siguen siendo los mismos), pero **tres campos que antes eran texto libre ahora se validan contra catálogo** y el backend rechaza valores fuera de catálogo.

**Qué debe hacer el frontend a partir de ahora:**

1. **Poblar tres dropdowns desde catálogos** (ya no inputs de texto libre):
   - **Tipo de requisito** → catálogo `requirement-types` (Estructura Organizativa, por empresa).
   - **Dominio de la competencia** → catálogo `competency-domains` (**NUEVO**, Estructura Organizativa, por empresa, soporta *escala* ordenada).
   - **Métrica** → catálogo `experience-metrics` (**NUEVO**, por país, con `AÑOS/MESES/DÍAS/HORAS`).
2. **Enviar el `code` del catálogo** (no el nombre) en cada uno de esos campos.
3. **Manejar los nuevos errores** `422`/`409` (sección 8) y mapearlos a mensajes/markers en el formulario.

> **Importante:** en alta/edición se envían **códigos**, no nombres. El backend devuelve también **códigos**; el frontend resuelve el **nombre para mostrar** con las mismas listas de catálogo que cargó para los dropdowns.

---

## 2. Modelo de datos (campos de una competencia curricular)

| Campo | Tipo | Obligatorio | Regla | Notas |
|---|---|---|---|---|
| `requirementTypeCode` | string (≤80) | **Sí** | Debe existir y estar **activo** en `requirement-types` de la empresa | Código de catálogo (canónico, MAYÚSCULAS) |
| `requirementName` | string (≤200) | **Sí** | No vacío · anti-duplicado con `requirementTypeCode` | Texto libre (nombre del requisito) |
| `competencyDomain` | string (≤120) | **Sí** | Debe existir y estar **activo** en `competency-domains` de la empresa | Código de catálogo (canónico, MAYÚSCULAS) |
| `experienceTimeValue` | number (decimal) | No | **≥ 0** (admite 0) | Cantidad de tiempo de experiencia |
| `metricCode` | string (≤80) | No* | Si se informa, debe existir en `experience-metrics`. **Obligatorio si `experienceTimeValue` viene informado** | Unidad (`ANOS`/`MESES`/`DIAS`/`HORAS`) |
| `notes` | string (≤2000) | No | — | Observaciones libres |
| `sourceSystem` / `sourceReference` / `sourceSyncedUtc` | string / string / fecha UTC | No | — | Trazabilidad de origen/integración |
| `concurrencyToken` | GUID | (sistema) | Se usa en `If-Match`/`ETag` | Devuelto por el backend |
| `curricularCompetencyPublicId` | GUID | (sistema) | Id público del registro | Devuelto por el backend |

> \* **Métrica** es opcional **salvo** cuando se informa `experienceTimeValue`: en ese caso es obligatoria (para que el número tenga unidad).

---

## 3. Catálogos que el frontend debe consumir

Hay **dos familias** de catálogo involucradas, con endpoints distintos.

### 3.1 Tipo de requisito — `requirement-types` (Estructura Organizativa · por empresa)

Catálogo **configurable por empresa** (tenant), administrado en el módulo de Estructura Organizativa.

```
GET /api/v1/companies/{companyPublicId}/position-description-catalogs/requirement-types/items?isActive=true&pageSize=100
Authorization: Bearer <jwt>
```

- Respuesta: `PagedResponse<PositionDescriptionCatalogItemResponse>` → `items[]` con `publicId`, `code`, `name`, `sortOrder`, `isActive`.
- **El frontend envía `code`** en `requirementTypeCode`.
- Parámetros: `isActive=true` (solo activos para el alta), `q` (búsqueda), `page`, `pageSize` (máx. 100).

### 3.2 Dominio de la competencia — `competency-domains` (Estructura Organizativa · por empresa) — **NUEVO**

Catálogo **nuevo**, configurable por empresa. Cada empresa define sus valores y puede usarlo como **lista plana** (área temática) o como **escala ordenada** (nivel de pericia). El campo `sortOrder` habilita la lectura como escala (p. ej. `Básico → Intermedio → Avanzado → Experto`).

```
GET /api/v1/companies/{companyPublicId}/position-description-catalogs/competency-domains/items?isActive=true&pageSize=100
Authorization: Bearer <jwt>
```

- Misma forma de respuesta que 3.1. **El frontend envía `code`** en `competencyDomain`.
- **Para presentarlo como escala:** ordenar las opciones por `sortOrder` ascendente.
- **Administración** (crear/editar/inactivar ítems) — requiere permiso `PositionDescriptionCatalogs.Admin`:
  ```
  POST /api/v1/companies/{companyPublicId}/position-description-catalogs/competency-domains/items
  { "code": "AVANZADO", "name": "Avanzado", "sortOrder": 30 }
  ```

> ⚠️ **El catálogo `competency-domains` arranca vacío por empresa** (es configurable por tenant). Antes de poder registrar competencias, un administrador debe **crear al menos un ítem** vía el `POST` anterior (o la pantalla de administración de catálogos). Ver sección 11.

### 3.3 Métrica — `experience-metrics` (por país) — **NUEVO**

Catálogo **nuevo**, **por país**, sembrado en todos los entornos. Para **SV** ya viene con: `ANOS` (Años), `MESES` (Meses), `DIAS` (Días), `HORAS` (Horas).

```
GET /api/v1/general-catalogs/experience-metrics?countryCode=SV
Authorization: Bearer <jwt>
```

- Respuesta: array de `PersonnelCatalogItemResponse` → `{ id, category, code, name, isSystem, isActive, sortOrder }`.
- **El frontend envía `code`** en `metricCode` (p. ej. `"ANOS"`).
- Los **códigos son ASCII en mayúsculas** (`ANOS`, `DIAS`); el **nombre** (`Años`, `Días`) es solo para mostrar.

| `code` | `name` (display) |
|---|---|
| `ANOS` | Años |
| `MESES` | Meses |
| `DIAS` | Días |
| `HORAS` | Horas |

---

## 4. Flujo funcional (UI)

**Precondición:** el expediente debe ser de un **empleado completado** (`IsCompletedEmployee`). Sobre un expediente no completado, las operaciones devuelven error de regla de estado.

**Alta de una competencia curricular:**
1. Al abrir la sección, cargar los 3 catálogos (3.1, 3.2, 3.3) para poblar los dropdowns.
2. El gestor selecciona **tipo de requisito** (dropdown), escribe **nombre del requisito**, selecciona **dominio** (dropdown) y, opcionalmente, ingresa **tiempo de experiencia** + **métrica** (dropdown).
3. `POST` con los **códigos** seleccionados (sección 5).
4. Éxito → `201 Created`; mostrar el ítem en la lista. Error → mapear el `code` del error al campo (sección 8).

**Edición / Baja:** obtener el ítem (trae su `concurrencyToken`) → `PUT`/`PATCH`/`DELETE` con header `If-Match: <concurrencyToken>` (sección 7).

---

## 5. Endpoints CRUD de competencias curriculares

Todos bajo `/api/v1`, autenticados (`Authorization: Bearer <jwt>`). **Lecturas** requieren `PersonnelFiles.Read`; **escrituras** requieren `PersonnelFiles.Manage`.

| Operación | Método | Ruta | Cuerpo | Éxito | Headers |
|---|---|---|---|---|---|
| Listar | `GET` | `/personnel-files/{publicId}/curricular-competencies` | — | `200` (array) | — |
| Obtener | `GET` | `/personnel-files/{publicId}/curricular-competencies/{id}` | — | `200` | — |
| Crear | `POST` | `/personnel-files/{publicId}/curricular-competencies` | `AddCurricularCompetencyRequest` | `201` | `Location`, `ETag` |
| Reemplazar | `PUT` | `/personnel-files/{publicId}/curricular-competencies/{id}` | `UpdateCurricularCompetencyRequest` | `200` | `If-Match` (req) → `ETag` |
| Parcial | `PATCH` | `/personnel-files/{publicId}/curricular-competencies/{id}` | JSON Patch (RFC 6902) | `200` | `If-Match` (req) → `ETag` |
| Eliminar | `DELETE` | `/personnel-files/{publicId}/curricular-competencies/{id}` | — | `200` (token del padre) | `If-Match` (req) |

> `{publicId}` = id público del **expediente**; `{id}` = `curricularCompetencyPublicId`.
> `PATCH` usa `Content-Type: application/json-patch+json`.

---

## 6. Contrato request / response (JSON)

### Request — crear (`POST`) / reemplazar (`PUT`)

```jsonc
{
  "requirementTypeCode": "PROF",          // code de requirement-types (obligatorio)
  "requirementName": "Excel Avanzado",     // nombre del requisito (obligatorio)
  "competencyDomain": "AVANZADO",          // code de competency-domains (obligatorio)
  "experienceTimeValue": 3,                 // opcional, >= 0
  "metricCode": "ANOS",                    // code de experience-metrics; obligatorio si hay experienceTimeValue
  "notes": "Certificado interno 2025",     // opcional
  "sourceSystem": null,                     // opcional (trazabilidad)
  "sourceReference": null,                  // opcional
  "sourceSyncedUtc": null                   // opcional (fecha UTC)
}
```

### Response — un ítem

```jsonc
{
  "curricularCompetencyPublicId": "f1d2…",
  "requirementTypeCode": "PROF",           // code canónico (MAYÚSCULAS)
  "requirementName": "Excel Avanzado",
  "competencyDomain": "AVANZADO",          // code canónico (MAYÚSCULAS)
  "experienceTimeValue": 3,
  "metricCode": "ANOS",                    // code canónico (MAYÚSCULAS) o null
  "notes": "Certificado interno 2025",
  "sourceSystem": null,
  "sourceReference": null,
  "sourceSyncedUtc": null,
  "concurrencyToken": "9b7c…"              // usar en If-Match para PUT/PATCH/DELETE
}
```

> El backend **canoniza** los códigos (los persiste en MAYÚSCULAS, sin espacios sobrantes). El `GET` de lista devuelve el array completo; usar el `concurrencyToken` de cada ítem para las mutaciones.

---

## 7. Concurrencia optimista (`If-Match` / `ETag`)

- Cada `GET` devuelve `concurrencyToken` por ítem (y el `POST` lo devuelve en el header `ETag`).
- `PUT`/`PATCH`/`DELETE` **exigen** el header `If-Match: <concurrencyToken>` actual.
- Token desactualizado → `409` `CONCURRENCY_CONFLICT`. Falta el header → `428 Precondition Required`.
- Tras un `PUT`/`PATCH` exitoso, el **nuevo** token llega en el header `ETag`; tras `DELETE` se devuelve el token **refrescado del expediente padre** en el cuerpo (`parentConcurrencyToken`) para seguir mutando sin un round-trip extra.

---

## 8. Validaciones y códigos de error

Los errores traen un `code` estable (para mapear en UI) y un `message` localizado (ES/EN según `Accept-Language`).

### Errores específicos de competencias curriculares

| `code` | HTTP | Disparador | Acción UI sugerida |
|---|---|---|---|
| `CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID` | `422` | `requirementTypeCode` no existe/activo en `requirement-types` | Marcar el dropdown de tipo; recargar catálogo |
| `CURRICULAR_COMPETENCY_DOMAIN_INVALID` | `422` | `competencyDomain` no existe/activo en `competency-domains` | Marcar el dropdown de dominio; recargar catálogo |
| `CURRICULAR_COMPETENCY_METRIC_INVALID` | `422` | `metricCode` informado fuera de `experience-metrics` | Marcar el dropdown de métrica |
| `CURRICULAR_COMPETENCY_METRIC_REQUIRED` | `422` | Hay `experienceTimeValue` pero falta `metricCode` | Exigir métrica cuando se ingresa experiencia |
| `CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE` | `422` | `experienceTimeValue < 0` | Validar ≥ 0 en el input |
| `CURRICULAR_COMPETENCY_DUPLICATE` | `409` | Mismo `requirementTypeCode` + `requirementName` (normalizado, sin distinguir mayúsculas) en el expediente | Avisar duplicado; no permitir guardar |

### Errores estándar que también aplican

| `code` / situación | HTTP | Disparador |
|---|---|---|
| Validación de campos (vacío obligatorio, longitud) | `400` | `requirementTypeCode`/`requirementName`/`competencyDomain` vacíos, longitudes excedidas |
| `CONCURRENCY_CONFLICT` | `409` | `If-Match` desactualizado |
| Precondición requerida | `428` | Falta el header `If-Match` en `PUT`/`PATCH`/`DELETE` |
| No encontrado | `404` | Expediente o competencia inexistente |
| Regla de estado | `422`/`409` | Expediente no completado |
| No autorizado / prohibido | `401` / `403` | Falta token / falta permiso (`Read`/`Manage`) |

> **Mapeo recomendado:** enrutar por el campo `code` del error (no por el texto del mensaje). El `message` es para mostrar al usuario; el `code` es el contrato estable.

---

## 9. Ejemplos end-to-end (curl)

**Cargar catálogos (dropdowns):**
```bash
# tipo de requisito
curl -H "Authorization: Bearer $JWT" \
  "$BASE/api/v1/companies/$COMPANY/position-description-catalogs/requirement-types/items?isActive=true&pageSize=100"
# dominio (NUEVO)
curl -H "Authorization: Bearer $JWT" \
  "$BASE/api/v1/companies/$COMPANY/position-description-catalogs/competency-domains/items?isActive=true&pageSize=100"
# métrica (NUEVO)
curl -H "Authorization: Bearer $JWT" \
  "$BASE/api/v1/general-catalogs/experience-metrics?countryCode=SV"
```

**Alta (happy path):**
```bash
curl -i -X POST -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  "$BASE/api/v1/personnel-files/$FILE/curricular-competencies" \
  -d '{"requirementTypeCode":"PROF","requirementName":"Excel Avanzado","competencyDomain":"AVANZADO","experienceTimeValue":3,"metricCode":"ANOS"}'
# → 201 Created, Location: …/curricular-competencies/{id}, ETag: "{concurrencyToken}"
```

**Alta rechazada (dominio fuera de catálogo):**
```bash
curl -i -X POST … -d '{"requirementTypeCode":"PROF","requirementName":"X","competencyDomain":"NO_EXISTE"}'
# → 422  { "code": "CURRICULAR_COMPETENCY_DOMAIN_INVALID", "message": "El dominio de la competencia no es valido en el catalogo activo." }
```

**Edición (con If-Match):**
```bash
curl -i -X PUT -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -H "If-Match: $TOKEN" \
  "$BASE/api/v1/personnel-files/$FILE/curricular-competencies/$ID" \
  -d '{"requirementTypeCode":"PROF","requirementName":"Excel Avanzado","competencyDomain":"EXPERTO","experienceTimeValue":5,"metricCode":"ANOS"}'
# → 200 OK, ETag: "{nuevo concurrencyToken}"
```

**Baja:**
```bash
curl -i -X DELETE -H "Authorization: Bearer $JWT" -H "If-Match: $TOKEN" \
  "$BASE/api/v1/personnel-files/$FILE/curricular-competencies/$ID"
# → 200 OK  { "parentConcurrencyToken": "…" }
```

---

## 10. Actualización parcial (`PATCH`, RFC 6902)

`Content-Type: application/json-patch+json`, header `If-Match` obligatorio. Rutas soportadas (propiedades raíz): `/requirementTypeCode`, `/requirementName`, `/competencyDomain`, `/experienceTimeValue`, `/metricCode`, `/notes`, `/sourceSystem`, `/sourceReference`, `/sourceSyncedUtc`.

```bash
curl -i -X PATCH -H "Authorization: Bearer $JWT" \
  -H "Content-Type: application/json-patch+json" -H "If-Match: $TOKEN" \
  "$BASE/api/v1/personnel-files/$FILE/curricular-competencies/$ID" \
  -d '[{"op":"replace","path":"/competencyDomain","value":"EXPERTO"},
       {"op":"replace","path":"/experienceTimeValue","value":5},
       {"op":"replace","path":"/metricCode","value":"ANOS"}]'
```

> El `PATCH` aplica las mismas validaciones de catálogo/coherencia/anti-duplicado que el `PUT` sobre el estado resultante. Quitar la métrica (`remove /metricCode`) cuando hay `experienceTimeValue` produce `CURRICULAR_COMPETENCY_METRIC_REQUIRED`.

---

## 11. Notas operativas y de despliegue

- **Métrica (`experience-metrics`)**: se siembra automáticamente vía migración en **todos los entornos** (SV: `ANOS/MESES/DIAS/HORAS`). No requiere acción manual.
- **Dominio (`competency-domains`)**: **configurable por empresa y arranca vacío**. Antes de habilitar la pantalla, un administrador (permiso `PositionDescriptionCatalogs.Admin`) debe **crear los valores** por empresa (p. ej. una escala `BASICO/INTERMEDIO/AVANZADO/EXPERTO` con `sortOrder` 10/20/30/40, o una lista plana). El frontend debería **mostrar un estado vacío con call-to-action** ("Configure los dominios de competencia") cuando el catálogo no tenga ítems.
- **Tipo de requisito (`requirement-types`)**: ya existía; se administra en Estructura Organizativa.
- **Datos legados**: si existieran competencias previas con códigos de texto libre fuera de catálogo, las ediciones serán rechazadas hasta que esos códigos existan en los catálogos. La migración rellena la columna interna `normalized_requirement_name`; el saneamiento de catálogos (sembrar los códigos en uso) es un paso operativo previo en entornos con datos reales.
- **Códigos canónicos**: enviar el `code` tal cual lo expone el catálogo. El backend normaliza (MAYÚSCULAS, trim), de modo que `prof` y `PROF` resuelven al mismo ítem; la respuesta siempre trae la forma canónica.

---

## 12. Checklist de integración Frontend

- [ ] Reemplazar el input de **tipo de requisito** por un dropdown alimentado por `requirement-types` (enviar `code`).
- [ ] Agregar dropdown de **dominio** alimentado por `competency-domains`; ordenar por `sortOrder` para presentación de escala (enviar `code`).
- [ ] Agregar dropdown de **métrica** alimentado por `experience-metrics?countryCode=…` (enviar `code`).
- [ ] Exigir **métrica** cuando se ingresa **tiempo de experiencia**; validar **≥ 0** en cliente.
- [ ] Manejar los 6 `code` de error nuevos (sección 8) y mapearlos al campo correspondiente.
- [ ] Mantener el flujo de **concurrencia** (`If-Match`/`ETag`) en `PUT`/`PATCH`/`DELETE`.
- [ ] Mostrar **estado vacío con CTA** cuando `competency-domains` no tenga ítems para la empresa.
- [ ] Resolver **code → name** para mostrar usando las listas de catálogo ya cargadas (la respuesta del recurso trae solo códigos).
