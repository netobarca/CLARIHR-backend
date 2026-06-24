# Guía de Integración Frontend — Reclamos de Seguro Médico (Fase 1)

| | |
|---|---|
| **Tipo de documento** | Guía de integración para el equipo Frontend |
| **Audiencia** | Frontend, UX/UI, QA |
| **Documentos relacionados** | [`docs/business/analisis-reclamos-seguro-medico-empleado.md`](../business/analisis-reclamos-seguro-medico-empleado.md) · [`docs/technical/plan-tecnico-reclamos-seguro-medico.md`](./plan-tecnico-reclamos-seguro-medico.md) |
| **Rama** | `feature/reclamos-seguro-medico-fase1` |
| **Estado del backend** | Implementado, compila, **1901 pruebas unitarias en verde**. Migración `20260624020637_HardenMedicalClaimsAndAttachments`. |
| **País de referencia** | El Salvador (SV) · Mensajes bilingües (ES/EN) |

---

## 1. Resumen del cambio

La sección de **Reclamos de Seguro Médico** del expediente del empleado se endureció. Lo que el Frontend debe saber, en una línea:

1. **El reclamo ahora exige un seguro** (de los del propio empleado) y **distingue al paciente**: titular o un **beneficiario** de esa póliza.
2. **Tipo de reclamo** y **estado** se eligen de **catálogos** (ya no texto libre); la **moneda** es ISO-4217 (3 letras) con **sugerencia automática** según el país.
3. El **tiempo de respuesta** ya **no se captura**: se **deriva** de la fecha del reclamo y la fecha de resolución.
4. Los reclamos viven bajo un **permiso dedicado de salud** (`ViewMedicalClaims` / `ManageMedicalClaims`); sin él se recibe **403**. El **empleado puede ver y registrar** sus propios reclamos (autoservicio).
5. Se agregan **adjuntos** (factura, receta, EOB, informe…) reutilizando el subsistema de archivos existente.

> El módulo es **solo de registro**: no aprueba, no paga, no tramita. El "estado" es informativo.

---

## 2. Permisos y control de acceso

| Permiso (RBAC) | Habilita |
|---|---|
| `PersonnelFiles.ViewMedicalClaims` | **Leer** reclamos (y sus adjuntos) de **cualquier** empleado del tenant. |
| `PersonnelFiles.ManageMedicalClaims` | **Crear / editar / eliminar** reclamos (y adjuntos) de **cualquier** empleado. |
| `PersonnelFiles.Admin` | Superset (incluye ambos). |

**Autoservicio (sin permisos anteriores):** un usuario **vinculado a un expediente** (su `linkedUserPublicId`) puede:
- **Ver** sus propios reclamos y los de sus beneficiarios.
- **Registrar (POST)** un reclamo propio y **adjuntarle** documentos.
- **NO** puede editar/eliminar (eso es solo para RRHH con `ManageMedicalClaims`).

**Comportamiento ante falta de acceso:** **403 Forbidden** (el diagnóstico es dato de salud; **no** se enmascara, se deniega). El Frontend debe ocultar la sección/acciones cuando el usuario no tiene permiso ni es el titular, y manejar 403 con un mensaje claro.

> **Nota:** El diagnóstico (`diagnosis`) es **dato sensible de salud**. Trátelo como tal en la UI (no exponerlo en listados no autorizados, no cachearlo indebidamente).

---

## 3. ⚠️ Cambios contractuales (BREAKING) — acción requerida en el Frontend

Los contratos de los endpoints de reclamos **cambiaron**. Hay que actualizar los formularios:

| Campo | Antes | Ahora |
|---|---|---|
| `insurancePublicId` | opcional (`Guid?`) | **obligatorio** (`Guid`) — debe ser un seguro del empleado |
| `claimantType` | — | **nuevo, obligatorio**: `"TITULAR"` o `"BENEFICIARIO"` |
| `beneficiaryPublicId` | — | **nuevo, condicional**: requerido si `claimantType = "BENEFICIARIO"` |
| `resolutionDateUtc` | — | **nuevo, opcional**: fecha de resolución/pago |
| `claimStatusCode` | — | **nuevo, opcional**: código del catálogo de estados |
| `responseTimeDays` | se enviaba | **ELIMINADO del request** — ahora es **derivado** (solo lectura) |
| `claimTypeCode` | texto libre | **debe** existir en el catálogo `medical-claim-types` |
| `currencyCode` | texto libre | **ISO-4217 (3 letras)**; si se omite y hay monto, el backend lo completa con la moneda de la compañía |

**Nuevos campos en la respuesta** (solo lectura): `insuranceName` (nombre/código del seguro resuelto), `claimantType`, `beneficiaryPublicId`, `patientName`, `kinshipCode`, `resolutionDateUtc`, `claimStatusCode`, y `responseTimeDays` (derivado).

---

## 4. Catálogos a consumir

Se exponen automáticamente vía el endpoint de catálogos generales **country-scoped**:

```
GET /api/v1/general-catalogs/medical-claim-types?countryCode=SV
GET /api/v1/general-catalogs/medical-claim-status?countryCode=SV
```

Cada item: `{ publicId, code, name, isActive, sortOrder }`. **Usar `code`** como valor a enviar (`claimTypeCode`, `claimStatusCode`) y `name` como etiqueta.

**Seed SV (tipos):** `AMBULATORIO`, `HOSPITALARIO`, `EMERGENCIA`, `FARMACIA`, `LABORATORIO`, `DENTAL`, `OFTALMOLOGICO`, `MATERNIDAD`, `OTRO`.
**Seed (estados):** `PRESENTADO`, `EN_REVISION`, `PENDIENTE_DOCUMENTACION`, `APROBADO`, `RECHAZADO`, `PAGADO`, `PAGO_PARCIAL`, `ANULADO`.

**`claimantType`** NO es un catálogo de servidor — es un conjunto fijo de 2 valores: **`TITULAR`** y **`BENEFICIARIO`** (modelarlo como radio/select en el cliente).

**Selección del seguro y del beneficiario** (para los selectores del formulario) se obtienen de los endpoints de seguros del expediente ya existentes:
```
GET /api/v1/personnel-files/{publicId}/insurances
GET /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries
```
El selector de **beneficiario** debe filtrarse al **seguro elegido** (el backend valida que el beneficiario pertenezca a esa póliza).

---

## 5. Endpoints de reclamos

Base: `/api/v1/personnel-files/{publicId}/medical-claims`. Concurrencia con **`If-Match`** (ETag) en `PUT`/`PATCH`/`DELETE`.

### 5.1 Listar / obtener
```
GET  /api/v1/personnel-files/{publicId}/medical-claims
GET  /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}
```
Respuesta (item):
```jsonc
{
  "medicalClaimPublicId": "…",
  "insuranceId": "…",            // seguro asociado (obligatorio)
  "insuranceName": "SEGURO-VIDA", // nombre/código resuelto (solo lectura)
  "accountNumber": "ACC-100",
  "claimantType": "BENEFICIARIO", // TITULAR | BENEFICIARIO
  "beneficiaryPublicId": "…",     // si beneficiario
  "patientName": "María Pérez",   // snapshot (solo lectura)
  "kinshipCode": "CONYUGE",       // snapshot (solo lectura)
  "claimTypeCode": "HOSPITALARIO",
  "diagnosis": "…",               // dato de salud
  "claimAmount": 1200.00,
  "currencyCode": "USD",
  "paidAmount": 900.00,
  "responseTimeDays": 5,          // DERIVADO (solo lectura)
  "notes": "…",
  "claimDateUtc": "2026-03-01T00:00:00Z",
  "resolutionDateUtc": "2026-03-06T00:00:00Z",
  "claimStatusCode": "PAGADO",
  "isActive": true,
  "concurrencyToken": "…"         // usar como If-Match
}
```

### 5.2 Crear (RRHH o autoservicio del titular)
```
POST /api/v1/personnel-files/{publicId}/medical-claims
```
Body:
```jsonc
{
  "insurancePublicId": "…",        // obligatorio
  "accountNumber": "ACC-100",
  "claimantType": "TITULAR",        // o "BENEFICIARIO"
  "beneficiaryPublicId": null,      // requerido si claimantType = BENEFICIARIO
  "claimTypeCode": "AMBULATORIO",   // del catálogo
  "diagnosis": "…",
  "claimAmount": 1200.00,
  "currencyCode": "USD",            // opcional: el backend sugiere por país si hay monto
  "paidAmount": 900.00,
  "notes": "…",
  "claimDateUtc": "2026-03-01T00:00:00Z", // requerida, no futura
  "resolutionDateUtc": null,        // opcional, ≥ claimDateUtc
  "claimStatusCode": "PRESENTADO",  // opcional, del catálogo
  "sourceSystem": null, "sourceReference": null, "sourceSyncedUtc": null
}
```
`201 Created` + header `Location` + `ETag` (token inicial).

### 5.3 Reemplazar / Patch / Eliminar (solo RRHH)
```
PUT    …/medical-claims/{id}      (body = mismo shape que POST; If-Match requerido)
PATCH  …/medical-claims/{id}      (JSON Patch RFC 6902, application/json-patch+json; If-Match)
DELETE …/medical-claims/{id}      (If-Match)
```
- **PUT** reemplaza campos de negocio; **no** cambia `isActive`.
- **PATCH** admite los campos de negocio y `isActive` (activar/desactivar). `insurancePublicId` **no** es removible; `responseTimeDays` **no** es parcheable (derivado).
- **DELETE** devuelve `{ parentConcurrencyToken }` del expediente.

---

## 6. Moneda y tiempo de respuesta

- **Moneda:** validar 3 letras en el cliente. Si el usuario no elige moneda pero captura un monto, el backend la completa con la moneda de la compañía (normalmente `USD` en SV). Recomendado: **pre-seleccionar** esa moneda en el formulario.
- **Tiempo de respuesta (`responseTimeDays`):** **NO** mostrar un campo editable. Mostrarlo como **calculado** = `resolutionDateUtc − claimDateUtc` (en días). Si no hay `resolutionDateUtc`, viene `null` ("pendiente").

---

## 7. Adjuntos (documentos de soporte)

Flujo en **3 pasos**, reutilizando el subsistema de archivos genérico. El `purpose` es **`MedicalClaimDocument`** (PDF/JPG/PNG, hasta 10 MB).

**Paso 1 — abrir sesión de subida:**
```
POST /api/v1/files/upload-session
{ "fileName": "factura.pdf", "contentType": "application/pdf", "sizeBytes": 12345, "purpose": "MedicalClaimDocument", "entityId": null }
→ { filePublicId, uploadUrl, expiresUtc, requiredHeaders, concurrencyToken }
```
**Paso 2 — subir el binario** directo a `uploadUrl` (PUT a Blob con `requiredHeaders`), luego confirmar:
```
PATCH /api/v1/files/{filePublicId}/complete
{ "concurrencyToken": "…" }   // el de la sesión
```
**Paso 3 — vincular al reclamo:**
```
POST /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}/documents
{ "filePublicId": "…", "documentTypeCatalogItemPublicId": "…", "observations": "…" }
→ 201 + ETag
```
`documentTypeCatalogItemPublicId` proviene del catálogo de **tipos de documento** ya existente (reusamos `DocumentTypeCatalogItem`; el seed sugerido incluye `FORMULARIO_RECLAMO`, `FACTURA`, `RECETA`, `EOB`, `INFORME_MEDICO`, `OTRO`).

**Listar / obtener / descargar / eliminar:**
```
GET    …/medical-claims/{id}/documents
GET    …/medical-claims/{id}/documents/{documentPublicId}
GET    …/medical-claims/{id}/documents/{documentPublicId}/read-url   → { readUrl, expiresUtc }
DELETE …/medical-claims/{id}/documents/{documentPublicId}            (If-Match)
```
- **Descarga:** usar **siempre** el endpoint `…/read-url` (devuelve un SAS temporal autorizado por el reclamo). **No** usar el genérico `/files/{id}/read-url` (es owner-only).
- Los adjuntos heredan el **mismo control de acceso** que el reclamo (403 si no autorizado). El **empleado** puede adjuntar a sus propios reclamos; **eliminar** es solo RRHH.

Respuesta de un documento:
```jsonc
{
  "id": "…",
  "documentTypeCatalogItemPublicId": "…",
  "documentTypeCode": "FACTURA",
  "documentTypeName": "Factura",
  "observations": "…",
  "filePublicId": "…",
  "fileName": "factura.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 12345,
  "isActive": true,
  "concurrencyToken": "…",
  "createdAtUtc": "…",
  "modifiedAtUtc": null
}
```

---

## 8. Concurrencia (If-Match / ETag)

Cada reclamo y cada documento traen `concurrencyToken`. Para `PUT`/`PATCH`/`DELETE`, enviarlo en el header **`If-Match`**. La respuesta devuelve el **nuevo** token en `ETag`. Si no coincide → **409 Conflict** (recargar y reintentar).

---

## 9. Mapa de errores

| Situación | HTTP | Código / detalle |
|---|---|---|
| Falta seguro, claimant inválido, beneficiario faltante (si BENEFICIARIO), monto negativo, moneda ≠ 3, fecha futura, resolución < reclamo | **400** | `common.validation` (por campo) |
| Seguro inexistente / no es del empleado | **422** | `MEDICAL_CLAIM_INSURANCE_NOT_FOUND` |
| Beneficiario no pertenece al seguro | **422** | `MEDICAL_CLAIM_BENEFICIARY_NOT_OWNED` |
| Tipo de reclamo fuera de catálogo | **422** | `MEDICAL_CLAIM_TYPE_CODE_INVALID` |
| Estado fuera de catálogo | **422** | `MEDICAL_CLAIM_STATUS_CODE_INVALID` |
| Pagado > reclamado | **(aceptado)** | reembolso — no bloquea |
| Sin permiso y no titular | **403** | acceso denegado (no enmascarado) |
| `If-Match` no coincide | **409** | conflicto de concurrencia |
| Expediente no completado | **409/422** | regla de estado |
| Adjunto: tipo/tamaño/purpose no permitido | **400/413** | reglas del `FilePurpose` |

Todos los mensajes están **localizados (ES/EN)**.

---

## 10. Notas de migración / datos

- El cambio es **breaking** a nivel de datos: `insurance_public_id` pasó a **obligatorio** y se agregó `claimant_type` (default `TITULAR` para filas existentes). En entornos con datos previos sin seguro, RRHH deberá **completar el seguro** de esos reclamos.
- Los registros antiguos quedan con `claimantType = TITULAR` por defecto.
- El "nombre del seguro" se **toma del seguro** (`InsuranceCode`) y se **fija como snapshot** en el reclamo, de modo que se conserva aunque el seguro cambie luego.

---

## 11. Checklist Frontend

- [ ] Selector de **seguro** (obligatorio) + selector de **beneficiario** dependiente del seguro, visible solo si `claimantType = BENEFICIARIO`.
- [ ] Radio/select **`claimantType`** (TITULAR / BENEFICIARIO).
- [ ] Combos de **tipo** y **estado** desde los catálogos `medical-claim-types` / `medical-claim-status`.
- [ ] **Moneda** ISO(3) pre-sugerida; quitar el input manual de **tiempo de respuesta** (mostrarlo calculado).
- [ ] Campo **fecha de resolución** (opcional, ≥ fecha de reclamo).
- [ ] Manejo de **403** (ocultar acciones / mensaje) y de **409** (recargar token).
- [ ] Flujo de **adjuntos** (upload-session → complete → attach; descarga vía `read-url`).
- [ ] Autoservicio: el empleado ve/crea lo suyo; ocultar editar/eliminar si no es RRHH.
- [ ] Tratar **diagnóstico** y **adjuntos** como datos de salud sensibles.
