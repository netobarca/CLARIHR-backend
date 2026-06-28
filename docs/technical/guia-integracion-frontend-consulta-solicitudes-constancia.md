# Guía de Integración Frontend — Solicitud y Consulta de Constancias

> **Audiencia:** Equipo Frontend (portal del empleado + back-office de RR. HH.).
> **Backend:** Fase 1 implementada (D-01…D-20). Rama `feature/constancias-fase1`.
> **Documentos base:** [`analisis-consulta-solicitudes-constancia.md`](../business/analisis-consulta-solicitudes-constancia.md) · [`plan-tecnico-consulta-solicitudes-constancia.md`](./plan-tecnico-consulta-solicitudes-constancia.md)

---

## 1. ¿Qué se construyó? (resumen del requerimiento)

Un módulo de **solicitudes de constancia** dentro de la familia "Solicitudes" del portal (junto a reclamos médicos y ayuda económica). Cubre el ciclo completo:

1. **El empleado solicita** una constancia (autoservicio): de **salario**, **laboral**, **para embajada**, **tiempo laborado**, **no descuento** o **carta de recomendación**.
2. **RR. HH. procesa y emite**: al emitir, el sistema **genera el PDF** automáticamente (con membrete, firmante y pie configurables de la empresa) y lo deja disponible para descarga; luego se marca **entregada**.
3. **RR. HH. consulta** todo en una **bandeja a nivel de empresa** (listado + filtros + conteos por estado) y la **exporta a Excel**.

**Tres principios clave para integrar:**
- La constancia **entrega un documento, no dinero** (sin montos/moneda).
- El ciclo es **lineal** (sin aprobaciones): `SOLICITADA → EN_PROCESO → EMITIDA → ENTREGADA`, con `RECHAZADA`/`ANULADA`.
- La **consulta/bandeja es transversal** (toda la empresa, solo RR. HH.); el **ingreso es por expediente** (el empleado ve solo lo suyo).

---

## 2. Convenciones generales (aplican a todos los endpoints)

| Tema | Convención |
|---|---|
| **Prefijo** | `api/v1` (p. ej. `POST /api/v1/personnel-files/{publicId}/certificate-requests`). |
| **Autenticación** | Bearer JWT en `Authorization` (igual que el resto del API). |
| **Concurrencia** | Escrituras (`PUT`/`PATCH`/`DELETE`) requieren el header **`If-Match: "<concurrencyToken>"`**. Falta → `400`; desactualizado → `409` (`CONCURRENCY_CONFLICT`). El nuevo token vuelve en el body **y** en el header `ETag`. |
| **Errores** | `ProblemDetails` con el código en **`extensions.code`** (p. ej. `CERTIFICATE_ADDRESSEE_REQUIRED`). Mensajes localizados es/en por `Accept-Language`. |
| **Enums** | Siempre **strings** (códigos de catálogo en MAYÚSCULAS: `CONSTANCIA_SALARIO`, `SOLICITADA`, …). |
| **Fechas** | ISO-8601 UTC (`2026-06-28T00:00:00Z`). |
| **DELETE** | Devuelve `{ "parentConcurrencyToken": "<guid>" }` (el token refrescado del expediente). |
| **Serialización de GUIDs** | Por convención de la plataforma, las propiedades `Guid` que terminan en `Id` se exponen en el wire con sufijo `PublicId` (p. ej. `issuedByUserId` → `issuedByUserPublicId`). **Usa Swagger/OpenAPI como fuente de verdad de los nombres exactos.** |
| **Autoservicio** | Se resuelve en el servidor por el usuario autenticado (`LinkedUserPublicId`). El frontend **no** envía "soy el dueño"; el backend lo determina. |

---

## 3. Permisos

| Permiso | Habilita |
|---|---|
| `PersonnelFiles.ViewCertificateRequests` | Ver la bandeja de empresa, ver detalle/documentos de cualquiera, exportar, leer la configuración de empresa. |
| `PersonnelFiles.ManageCertificateRequests` | Procesar/emitir/entregar/rechazar/editar/dar de baja, subir override, y **editar la configuración de empresa**. |
| `PersonnelFiles.ViewCompensation` | **Adicional** y **obligatorio** para **emitir** una constancia que imprime salario (`CONSTANCIA_SALARIO`, `CONSTANCIA_EMBAJADA`). |
| *(ninguno — autoservicio)* | El **empleado** crea / consulta / cancela / descarga **lo suyo** sin permiso de gestión (basta estar autenticado y ser el titular del expediente). |

> El back-office debe ocultar las acciones de gestión a quien no tenga `ManageCertificateRequests`, y deshabilitar **Emitir** en constancias de salario/embajada si el usuario no tiene `ViewCompensation` (de lo contrario recibirá `403 CERTIFICATE_COMPENSATION_FORBIDDEN`).

---

## 4. Catálogos (parametrizados, *country-scoped*, seed SV)

Se leen con el endpoint genérico de catálogos por **key** (no requieren permiso de gestión; se cargan al montar los formularios):

```
GET /api/v1/general-catalogs/{catalogKey}?countryCode=SV
→ 200 [ { "id": "...", "code": "CONSTANCIA_SALARIO", "name": "Constancia de salario", "isActive": true, "sortOrder": 10 }, ... ]
```

| `catalogKey` | Uso | Códigos seed SV |
|---|---|---|
| `certificate-types` | Tipo de constancia | `CONSTANCIA_SALARIO`, `CONSTANCIA_LABORAL`, `CONSTANCIA_EMBAJADA`, `CONSTANCIA_TIEMPO_LABORADO`, `CONSTANCIA_NO_DESCUENTO`, `CARTA_RECOMENDACION` |
| `certificate-request-statuses` | Estado del ciclo | `SOLICITADA`, `EN_PROCESO`, `EMITIDA`, `ENTREGADA`, `RECHAZADA`, `ANULADA` |
| `certificate-delivery-methods` | Medio de entrega | `PRESENCIAL`, `CORREO_ELECTRONICO`, `PORTAL` |
| `certificate-purposes` | Propósito | `TRAMITE_BANCARIO`, `CREDITO`, `VISA_EMBAJADA`, `TRAMITE_MIGRATORIO`, `USO_PERSONAL`, `OTRO` |

> Envía siempre los **códigos** (no los nombres) en los requests. Los catálogos son extensibles: usa el `code` que devuelve el endpoint, no una lista hardcodeada.

---

## 5. Ciclo de vida (máquina de estados)

```
                 (RR.HH.)        (RR.HH.: genera PDF)     (RR.HH.)
  SOLICITADA ──► EN_PROCESO ─────────► EMITIDA ───────────► ENTREGADA
      │              │                    
      │ (RR.HH.)     │ (RR.HH.)           
      ├──────────────┴──► RECHAZADA       
      │                                   
      └──(titular o RR.HH.)──► ANULADA     
```

- **Pendiente** = `SOLICITADA` o `EN_PROCESO`. Desde pendiente se puede: procesar, emitir, rechazar o cancelar.
- `EMITIDA` solo puede **entregarse** (o re-emitirse / cargar override).
- `ENTREGADA`/`RECHAZADA`/`ANULADA` son terminales.
- **Cancelar** solo desde pendiente; lo puede hacer el **titular** (autoservicio) o RR. HH.

---

## 6. Flujo del EMPLEADO (autoservicio)

### 6.1 Solicitar una constancia
```
POST /api/v1/personnel-files/{publicId}/certificate-requests
Body:
{
  "typeCode": "CONSTANCIA_EMBAJADA",
  "purposeCode": "VISA_EMBAJADA",
  "addressedTo": "Embajada de los Estados Unidos",   // obligatorio si typeCode = CONSTANCIA_EMBAJADA
  "deliveryMethodCode": "CORREO_ELECTRONICO",
  "languageCode": "en",                               // "es" (default) | "en"
  "copies": 1,                                        // opcional, ≥1
  "requestDateUtc": "2026-06-28T00:00:00Z",           // no futura
  "neededByDateUtc": null                             // opcional, ≥ requestDateUtc
}
→ 201 Created · ETag: "<concurrencyToken>" · body = la solicitud en estado SOLICITADA
```
- `{publicId}` es el **PublicId del expediente** del empleado autenticado.
- El backend valida tipo/propósito/medio contra catálogo (`422 CERTIFICATE_*_CODE_INVALID`) y exige `addressedTo` para embajada (`422 CERTIFICATE_ADDRESSEE_REQUIRED`).

### 6.2 Ver mis solicitudes y el detalle
```
GET /api/v1/personnel-files/{publicId}/certificate-requests          → 200 [ ...respuestas... ]
GET /api/v1/personnel-files/{publicId}/certificate-requests/{id}     → 200 { ...detalle... }
```
El empleado solo obtiene **las suyas** (el backend filtra por titular). Un tercero sin permiso → `403`.

### 6.3 Descargar la constancia emitida
Cuando el estado es `EMITIDA`/`ENTREGADA`, hay un documento del sistema. Para descargarlo:
```
GET  .../certificate-requests/{id}/documents                         → lista de documentos (el PDF emitido)
GET  .../certificate-requests/{id}/documents/{docId}/read-url        → { "readUrl": "<SAS>", "expiresUtc": "..." }
```
Descarga el binario directamente desde `readUrl` (URL pre-firmada, corta duración). El documento del sistema tiene `isSystemGenerated: true`.

### 6.4 Cancelar mi solicitud (mientras esté pendiente)
```
PATCH .../certificate-requests/{id}/cancel
Header: If-Match: "<concurrencyToken>"
→ 200 · estado ANULADA
```
Solo desde `SOLICITADA`/`EN_PROCESO`; sobre una emitida → `422 CERTIFICATE_STATE_RULE_VIOLATION`.

---

## 7. Flujo de RR. HH. (back-office)

### 7.1 Bandeja de empresa (la "consulta")
```
POST /api/v1/companies/{companyId}/certificate-requests/query
Body (todos los filtros son opcionales):
{
  "typeCode": "CONSTANCIA_SALARIO",
  "statusCode": "SOLICITADA",
  "purposeCode": null,
  "employeeId": null,                 // PublicId del expediente
  "fromUtc": "2026-06-01T00:00:00Z",
  "toUtc":   "2026-06-30T23:59:59Z",
  "search": "García",                 // nombre del empleado o "dirigida a"
  "pageNumber": 1,
  "pageSize": 25                      // 1..100
}
→ 200
{
  "items": [
    {
      "certificateRequestPublicId": "...",
      "personnelFilePublicId": "...",
      "employeeFullName": "Ana García",
      "certificateTypeCode": "CONSTANCIA_SALARIO",
      "typeName": "Constancia de salario",
      "purposeCode": "TRAMITE_BANCARIO",
      "requestStatusCode": "SOLICITADA",
      "addressedTo": "Banco Agrícola",
      "deliveryMethodCode": "PRESENCIAL",
      "requestDateUtc": "2026-06-25T00:00:00Z",
      "issuedDateUtc": null,
      "deliveredDateUtc": null,
      "issuedByUserId": null,
      "responseTimeDays": null
    }
  ],
  "pageNumber": 1,
  "pageSize": 25,
  "totalCount": 42,
  "statusCounts": { "SOLICITADA": 12, "EN_PROCESO": 5, "EMITIDA": 20, "ENTREGADA": 5 }
}
```
- Solo RR. HH. (`ViewCertificateRequests`). El empleado **no** usa este endpoint (usa §6.2).
- `statusCounts` sirve para los chips/contadores de la bandeja.

### 7.2 Exportar a Excel
```
GET /api/v1/companies/{companyId}/certificate-requests/export?format=xlsx&typeCode=&statusCode=&purposeCode=&employeeId=&fromUtc=&toUtc=&q=García
→ 200 (archivo .xlsx) · 413 si supera el límite síncrono de filas
```
Formatos: `xlsx` (default), `csv`, `json`. Aplica **los mismos filtros** que la bandeja (`q` = búsqueda). Columnas: Empleado, Tipo, Proposito, Estado, DirigidaA, MedioEntrega, FechaSolicitud, FechaEmision, FechaEntrega, TiempoRespuestaDias.

### 7.3 Procesar y emitir (genera el PDF)
```
PATCH .../certificate-requests/{id}/processing      If-Match → 200 (EN_PROCESO)   [opcional]
PATCH .../certificate-requests/{id}/issue           If-Match
   Body: { "notes": "Generada y lista." }            → 200 (EMITIDA, genera el PDF)
PATCH .../certificate-requests/{id}/delivery         If-Match
   Body: { "deliveredDateUtc": "2026-06-29T00:00:00Z" }  → 200 (ENTREGADA)
PATCH .../certificate-requests/{id}/reject           If-Match
   Body: { "notes": "Datos incompletos." }           → 200 (RECHAZADA)
```
**Emisión (`/issue`) — lo más importante:**
- Genera el PDF **server-side** con los datos del expediente (nombre, documento, cargo de la plaza activa, antigüedad, y **salario** si el tipo lo imprime) + la **configuración de empresa** (membrete/firmante/pie).
- Para `CONSTANCIA_SALARIO` / `CONSTANCIA_EMBAJADA` requiere **`ViewCompensation`** → si falta, `403 CERTIFICATE_COMPENSATION_FORBIDDEN`.
- Si faltan datos para generar (sin plaza activa, sin cargo, o sin salario en una de salario): `422 CERTIFICATE_GENERATION_DATA_UNAVAILABLE`. **Mensaje sugerido al usuario:** "No se puede emitir: faltan datos del empleado (plaza/cargo/salario)."
- Tras emitir, el PDF queda accesible vía el endpoint de documentos (§6.3).

### 7.4 Editar / dar de baja (RR. HH.)
```
PUT    .../certificate-requests/{id}    If-Match  Body = mismos campos que el POST  → 200
DELETE .../certificate-requests/{id}    If-Match                                    → 200 { parentConcurrencyToken }
```

### 7.5 Override manual del documento (opcional)
Si RR. HH. necesita subir un PDF firmado/escaneado en lugar del generado:
1. Sube el archivo con el flujo genérico de archivos con **`purpose = CertificateRequestDocument`**:
   `POST /api/v1/files/upload-session` → subir al `uploadUrl` → `PATCH /api/v1/files/{fileId}/complete`.
2. Vincúlalo:
```
POST .../certificate-requests/{id}/documents
Body: { "filePublicId": "<fileId>", "observations": "Versión firmada." }
→ 201 · documento con isSystemGenerated: false
DELETE .../certificate-requests/{id}/documents/{docId}   If-Match → 200
```

---

## 8. Configuración de constancias de la empresa (RR. HH.)

Define el membrete/logo, ciudad, firmante y pie que el sistema fusiona en cada PDF generado.
```
GET /api/v1/companies/{companyId}/certificate-settings
→ 200 { "logoFilePublicId": null, "issuingCity": null, "signatoryName": null,
        "signatoryTitle": null, "footerText": null, "concurrencyToken": "00000000-0000-0000-0000-000000000000" }
```
Si nunca se configuró, devuelve valores vacíos y `concurrencyToken` **en ceros** (úsalo como `If-Match` en el primer guardado).
```
PUT /api/v1/companies/{companyId}/certificate-settings
Header: If-Match: "<concurrencyToken>"   // los ceros la primera vez
Body:
{
  "logoFilePublicId": "<fileId de un archivo purpose=CompanyLogo>",  // opcional
  "issuingCity": "San Salvador",
  "signatoryName": "Lic. María Pérez",
  "signatoryTitle": "Jefa de Recursos Humanos",
  "footerText": "Documento informativo. Para verificación, contactar a RR. HH."
}
→ 200 · ETag con el nuevo token
```
- El logo, si se envía, debe ser un archivo **activo** con `purpose = CompanyLogo` de la empresa (si no, `422/404`).
- El **cuerpo** de la constancia (texto por tipo) **no** es editable: es estructural y bilingüe (es/en) en el backend.

---

## 9. Forma del recurso (response por expediente)

`PersonnelFileCertificateRequestResponse` (GET/POST/PUT/PATCH por expediente):
```jsonc
{
  "certificateRequestPublicId": "…",
  "certificateTypeCode": "CONSTANCIA_EMBAJADA",
  "typeName": "Constancia para embajada",     // snapshot del nombre del tipo
  "requestStatusCode": "EMITIDA",
  "purposeCode": "VISA_EMBAJADA",
  "addressedTo": "Embajada de los Estados Unidos",
  "deliveryMethodCode": "CORREO_ELECTRONICO",
  "languageCode": "en",
  "copies": 1,
  "requestDateUtc": "2026-06-25T00:00:00Z",
  "neededByDateUtc": null,
  "requestedByUserId": "…",      // (wire: requestedByUserPublicId)
  "issuedByUserId": "…",         // (wire: issuedByUserPublicId) — null hasta emitir
  "issuedDateUtc": "2026-06-28T00:00:00Z",
  "deliveredDateUtc": null,
  "resolutionNotes": "Generada y lista.",
  "responseTimeDays": 3,         // derivado: emisión − solicitud
  "isActive": true,
  "concurrencyToken": "…"        // úsalo en If-Match
}
```

---

## 10. Errores (códigos en `extensions.code`)

| HTTP | `code` | Cuándo |
|---|---|---|
| 422 | `CERTIFICATE_TYPE_CODE_INVALID` | Tipo inexistente/inactivo en catálogo. |
| 422 | `CERTIFICATE_PURPOSE_CODE_INVALID` | Propósito inválido. |
| 422 | `CERTIFICATE_DELIVERY_METHOD_CODE_INVALID` | Medio de entrega inválido. |
| 422 | `CERTIFICATE_REQUEST_STATUS_CODE_INVALID` | Estado destino inválido. |
| 422 | `CERTIFICATE_ADDRESSEE_REQUIRED` | Falta `addressedTo` en una constancia de embajada. |
| 422 | `CERTIFICATE_DATE_INCOHERENT` | Entrega antes de emisión (o emisión antes de solicitud). |
| 422 | `CERTIFICATE_STATE_RULE_VIOLATION` | Transición inválida (p. ej. emitir una anulada, entregar una no emitida). |
| 422 | `CERTIFICATE_GENERATION_DATA_UNAVAILABLE` | Al emitir: sin plaza activa / cargo / salario requerido. |
| 403 | `CERTIFICATE_COMPENSATION_FORBIDDEN` | Emitir constancia con salario sin permiso `ViewCompensation`. |
| 400 | `REPORT_EXPORT_FORMAT_INVALID` | Formato de export no soportado. |
| 409 | `CONCURRENCY_CONFLICT` | `If-Match` desactualizado. |
| 400 | (validación) | Falta `If-Match`, body inválido (copies ≤ 0, fecha futura, idioma ≠ es/en, etc.). |
| 403 | `Forbidden` | Sin permiso, o el empleado intenta operar sobre un expediente ajeno. |
| 404 | `ItemNotFound` / `DocumentNotFound` | Solicitud/documento inexistente. |

---

## 11. Checklist de integración (orden sugerido)

**Portal del empleado:**
1. Cargar catálogos (`certificate-types`, `certificate-purposes`, `certificate-delivery-methods`).
2. Formulario "Solicitar constancia" → `POST …/certificate-requests` (mostrar `addressedTo` obligatorio si tipo = embajada).
3. Vista "Mis solicitudes" → `GET …/certificate-requests` + detalle; botón **Descargar** (read-url) cuando esté `EMITIDA`/`ENTREGADA`; botón **Cancelar** si está pendiente.

**Back-office RR. HH.:**
4. **Bandeja** → `POST …/companies/{companyId}/certificate-requests/query` con filtros + chips de `statusCounts`; botón **Exportar** → `GET …/export`.
5. Detalle con acciones según estado: **Procesar / Emitir / Entregar / Rechazar / Editar / Dar de baja** (todas con `If-Match`). Deshabilitar **Emitir** de salario/embajada sin `ViewCompensation`.
6. Pantalla **Configuración de constancias** → `GET`/`PUT …/certificate-settings` (+ subida de logo con `purpose=CompanyLogo`).

**Cross-cutting:** manejar `If-Match`/`ETag` en toda escritura; mapear `extensions.code` a mensajes de usuario; tratar enums como strings; consultar Swagger para los nombres exactos de campos `...Id`/`...PublicId`.

---

> **Estado del backend:** Fase 1 completa y verificada (build limpio, 2047 pruebas unitarias en verde, migración sin drift). Generación de PDF por **layout en código** (es/en); firma electrónica/QR, notificaciones y plantillas editables quedan para Fase 2 (ver el análisis §4).
