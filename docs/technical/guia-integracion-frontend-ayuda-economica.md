# Guía de Integración Frontend — Ayuda / Asistencia Económica del Empleado

| | |
|---|---|
| **Módulo** | Expediente de Personal → Ayuda económica (`economic-aid-requests`) |
| **Audiencia** | Equipo Frontend |
| **Base URL** | `/api/v1` |
| **Documentos fuente** | `docs/business/analisis-ayuda-economica-empleado.md` · `docs/technical/plan-tecnico-ayuda-economica.md` |
| **Estado** | Implementado (Fase 1) · País de referencia: El Salvador (SV) |
| **Fecha** | 2026-06-28 |

---

## 1. Resumen y flujo general

Permite que un **empleado solicite ayuda económica** a la empresa por una emergencia (médica, fúnebre, desastre natural, etc.) y que **RR. HH. la valide** (aprobar / rechazar / requerir documentación), registre el **desembolso** informativo y gestione **adjuntos** de respaldo.

```
Empleado (autoservicio)            RR. HH. (permiso Manage)
─────────────────────              ────────────────────────
POST  …/economic-aid-requests  ─►  SOLICITADA
  (adjunta respaldo, opcional)
                                   PATCH …/{id}/resolution
                                     ├─ EN_REVISION / PENDIENTE_DOCUMENTACION (intermedio)
                                     ├─ APROBADA (approvedAmount > 0)
                                     └─ RECHAZADA
                                   PATCH …/{id}/disbursement (desde APROBADA) ─► DESEMBOLSADA
PATCH …/{id}/cancel (propia, si está pendiente) ─► ANULADA
```

**Fase 1:** subsidio **no reembolsable**; la validación es de **un solo paso** (sin flujo de aprobación multinivel); el desembolso es **informativo** (no ejecuta pago ni nómina).

---

## 2. Permisos y autoservicio

| Acción | Quién | Requisito |
|---|---|---|
| Crear solicitud | **Empleado (sobre su propio expediente)** o RR. HH. | Autoservicio (usuario vinculado) **o** `PersonnelFiles.ManageEconomicAidRequests` |
| Consultar (listar / detalle / adjuntos) | **Empleado (solo lo suyo)** o RR. HH. | Autoservicio **o** `PersonnelFiles.ViewEconomicAidRequests` |
| Validar (`resolution`) / desembolsar / editar / dar de baja / borrar adjunto | **Solo RR. HH.** | `PersonnelFiles.ManageEconomicAidRequests` |
| Cancelar la propia solicitud pendiente | **Empleado (lo suyo)** o RR. HH. | Autoservicio **o** Manage |
| Adjuntar documento | **Empleado (a lo suyo)** o RR. HH. | Autoservicio **o** Manage |

- El **autoservicio** se resuelve en el servidor comparando el usuario autenticado con el empleado vinculado al expediente (`LinkedUserPublicId`). El frontend **no** envía ningún identificador de "soy el dueño": basta el token.
- **Sin auto-aprobación:** un usuario de RR. HH. **no puede validar su propia** solicitud → `403 ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN`.
- El **motivo** de la emergencia es **dato sensible**: quien no tiene el permiso de vista ni es el titular recibe `403` (sin enmascarar).

---

## 3. Convenciones transversales

- **Prefijo:** todas las rutas bajo `/api/v1`.
- **Concurrencia optimista:** cada solicitud y cada adjunto traen un `concurrencyToken`. Para **PUT/PATCH/DELETE** envíalo en el header **`If-Match`** (valor crudo del GUID).
  - `If-Match` ausente → **400**; token desactualizado → **409** (`CONCURRENCY_CONFLICT`). El nuevo token vuelve en el header **`ETag`** y en el cuerpo de la respuesta.
- **Enums como strings:** los códigos de estado/tipo viajan como texto (`"APROBADA"`, `"EMERGENCIA_MEDICA"`).
- **Errores:** `application/problem+json`; el código legible por máquina está en **`extensions.code`** (ver §10).
- **Fechas:** UTC ISO-8601; las fechas de negocio se normalizan a fecha (sin hora).

---

## 4. Catálogos (parametrizables, semillados SV)

Obtén las opciones de los selectores con los endpoints genéricos de catálogos (country-scoped):

```
GET /api/v1/general-catalogs/economic-aid-types?countryCode=SV
GET /api/v1/general-catalogs/economic-aid-statuses?countryCode=SV
```

**Tipos (seed SV):** `EMERGENCIA_MEDICA`, `GASTOS_FUNEBRES`, `DESASTRE_NATURAL`, `INCENDIO_VIVIENDA`, `CALAMIDAD_DOMESTICA`, `ACCIDENTE`, `OTRA`.

**Estados (seed SV):** `SOLICITADA`, `EN_REVISION`, `PENDIENTE_DOCUMENTACION`, `APROBADA`, `RECHAZADA`, `DESEMBOLSADA`, `ANULADA`.

> Cada ítem trae `code`, `name` (descripción para mostrar), `isActive`, `sortOrder`. Usa `code` para enviar y `name` para mostrar. Son administrables: pueden agregarse más por país.

(Para el desembolso, la forma de pago opcional usa el catálogo existente `GET /api/v1/general-catalogs/payment-methods?countryCode=SV`.)

---

## 5. Ciclo de vida del estado

```
                 ┌───────────────► RECHAZADA (terminal)
SOLICITADA ──► EN_REVISION ──► APROBADA ──► DESEMBOLSADA (terminal)
     │         PENDIENTE_DOCUMENTACION
     └────────────────► ANULADA (cancelación, terminal)
```

- **Pendientes** (se pueden resolver o cancelar): `SOLICITADA`, `EN_REVISION`, `PENDIENTE_DOCUMENTACION`.
- **`resolution`** mueve a `EN_REVISION` / `PENDIENTE_DOCUMENTACION` (intermedios) o a `APROBADA` / `RECHAZADA` (terminales).
- **`disbursement`** solo desde `APROBADA` → `DESEMBOLSADA`.
- **`cancel`** solo desde un estado pendiente → `ANULADA`.

---

## 6. Endpoints

| Método | Ruta (`/api/v1/personnel-files/{publicId}/economic-aid-requests…`) | Política | Body | If-Match |
|---|---|---|---|---|
| GET | `` | View / self | — | — |
| GET | `/{id}` | View / self | — | — |
| POST | `` | View(authn) → gate self/Manage | `AddEconomicAidRequestRequest` | — |
| PUT | `/{id}` | Manage | `UpdateEconomicAidRequestRequest` | ✔ |
| DELETE | `/{id}` | Manage | — | ✔ |
| PATCH | `/{id}/resolution` | Manage | `ResolveEconomicAidRequestRequest` | ✔ |
| PATCH | `/{id}/disbursement` | Manage | `DisburseEconomicAidRequestRequest` | ✔ |
| PATCH | `/{id}/cancel` | View(authn) → gate self/Manage | — | ✔ |
| GET | `/{id}/documents` | View / self | — | — |
| GET | `/{id}/documents/{docId}` | View / self | — | — |
| GET | `/{id}/documents/{docId}/read-url` | View / self | — | — |
| POST | `/{id}/documents` | View(authn) → gate self/Manage | `AddEconomicAidRequestDocumentRequest` | — |
| DELETE | `/{id}/documents/{docId}` | Manage | — | ✔ |

`{id}` = `economicAidRequestPublicId`.

---

## 7. Contratos

### Respuesta — solicitud (`PersonnelFileEconomicAidRequestResponse`)
```jsonc
{
  "economicAidRequestPublicId": "guid",
  "economicAidTypeCode": "EMERGENCIA_MEDICA",
  "typeName": "Emergencia médica",          // snapshot de la descripción del tipo
  "requestStatusCode": "SOLICITADA",
  "description": "Necesito apoyo por...",     // motivo (dato sensible)
  "requestedAmount": 500.00,
  "currencyCode": "USD",
  "requestDateUtc": "2026-06-25T00:00:00Z",
  "requestedByUserId": "guid",
  "approvedAmount": null,                     // se llena al aprobar (> 0)
  "resolvedByUserId": null,                   // quién validó
  "resolutionDateUtc": null,
  "resolutionNotes": null,                    // observaciones / motivo de rechazo
  "responseTimeDays": null,                   // derivado (resolución − solicitud)
  "disbursedAmount": null,
  "disbursementDateUtc": null,
  "paymentMethodCode": null,
  "isActive": true,
  "concurrencyToken": "guid"                  // úsalo en If-Match
}
```

### Request — crear / editar
```jsonc
// AddEconomicAidRequestRequest / UpdateEconomicAidRequestRequest
{
  "typeCode": "EMERGENCIA_MEDICA",   // requerido, catálogo activo
  "description": "…",                // requerido, ≤ 2000, motivo de la emergencia
  "requestedAmount": 500.00,         // requerido, > 0
  "currencyCode": "USD",             // opcional (3 letras ISO-4217); si se omite, default de la empresa
  "requestDateUtc": "2026-06-25"     // requerido, no futura
}
```

### Request — validar (`ResolveEconomicAidRequestRequest`)
```jsonc
{
  "targetStatusCode": "APROBADA",    // EN_REVISION | PENDIENTE_DOCUMENTACION | APROBADA | RECHAZADA
  "approvedAmount": 300.00,          // requerido (> 0) SOLO cuando targetStatusCode = APROBADA; parcial permitido
  "notes": "Aprobado parcial"        // opcional, ≤ 2000
}
```

### Request — desembolsar (`DisburseEconomicAidRequestRequest`)
```jsonc
{
  "disbursedAmount": 300.00,                 // ≥ 0
  "disbursementDateUtc": "2026-06-30",       // ≥ fecha de resolución
  "paymentMethodCode": "TRANSFERENCIA"       // opcional, catálogo payment-methods
}
```

### Request — adjuntar documento (`AddEconomicAidRequestDocumentRequest`)
```jsonc
{
  "filePublicId": "guid",                       // archivo ya subido (purpose=EconomicAidRequestDocument)
  "documentTypeCatalogItemPublicId": null,      // opcional (clasificación)
  "observations": null                          // opcional
}
```

---

## 8. Flujos paso a paso

### 8.1 El empleado solicita (autoservicio)
```
POST /api/v1/personnel-files/{publicId}/economic-aid-requests
Body: AddEconomicAidRequestRequest
→ 201 Created · ETag: <concurrencyToken> · body = solicitud (status SOLICITADA)
```
Errores típicos: `422 ECONOMIC_AID_TYPE_CODE_INVALID`, `422 ECONOMIC_AID_ELIGIBILITY_NOT_MET` (no cumple antigüedad mínima, §9), `400` validación (monto ≤ 0, fecha futura…).

### 8.2 Adjuntar respaldo (reutiliza el subsistema de archivos)
```
1) POST /api/v1/files/upload-session   { purpose: "EconomicAidRequestDocument", fileName, contentType, sizeBytes }
   → { filePublicId, uploadUrl, … }
2) (subir binario a uploadUrl)  →  PATCH /api/v1/files/{filePublicId}/complete
3) POST …/economic-aid-requests/{id}/documents   { filePublicId }
   → 201 Created · documento
4) Descargar:  GET …/economic-aid-requests/{id}/documents/{docId}/read-url  → { readUrl, expiresUtc }
```

### 8.3 RR. HH. valida
```
PATCH …/economic-aid-requests/{id}/resolution
If-Match: <concurrencyToken>
Body: { "targetStatusCode": "APROBADA", "approvedAmount": 300, "notes": "…" }
→ 200 OK · ETag nuevo · body actualizado (resolvedByUserId, resolutionDateUtc, responseTimeDays)
```
- Aprobar sin `approvedAmount` o con 0 → `422 ECONOMIC_AID_APPROVED_AMOUNT_INVALID`.
- El titular validando lo suyo → `403 ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN`.
- Solicitud ya resuelta → `422 ECONOMIC_AID_STATE_RULE_VIOLATION`.

### 8.4 RR. HH. desembolsa
```
PATCH …/economic-aid-requests/{id}/disbursement
If-Match: <concurrencyToken>
Body: { "disbursedAmount": 300, "disbursementDateUtc": "2026-06-30", "paymentMethodCode": "TRANSFERENCIA" }
→ 200 OK · status DESEMBOLSADA
```
Solo desde `APROBADA` (si no → `422 ECONOMIC_AID_STATE_RULE_VIOLATION`); fecha < resolución → `422 ECONOMIC_AID_DATE_INCOHERENT`.

### 8.5 Cancelar (autoservicio del titular, si está pendiente)
```
PATCH …/economic-aid-requests/{id}/cancel   ·   If-Match: <concurrencyToken>   →   200 OK · status ANULADA
```

### 8.6 Editar / dar de baja (RR. HH.)
```
PUT    …/economic-aid-requests/{id}   (If-Match)  → reemplaza campos de negocio
DELETE …/economic-aid-requests/{id}   (If-Match)  → baja lógica; devuelve { parentConcurrencyToken }
```

---

## 9. Elegibilidad por antigüedad (D-08) — configuración

La empresa puede exigir una **antigüedad mínima (meses)** para solicitar. Se administra en las preferencias de empresa:

```
GET /api/v1/companies/{companyId}/preferences
PUT /api/v1/companies/{companyId}/preferences   (If-Match)
Body incluye:  "minimumSeniorityMonthsForEconomicAid": 6   // null o ausente = sin restricción
```
Si el empleado no alcanza la antigüedad, el `POST` de creación responde `422 ECONOMIC_AID_ELIGIBILITY_NOT_MET`. El frontend puede leer la preferencia para mostrar el requisito antes de enviar.

---

## 10. Catálogo de errores (`extensions.code`)

| Código | HTTP | Cuándo |
|---|---|---|
| `ECONOMIC_AID_TYPE_CODE_INVALID` | 422 | Tipo inexistente/inactivo |
| `ECONOMIC_AID_STATUS_CODE_INVALID` | 422 | Estado destino inválido/no permitido en `resolution` |
| `ECONOMIC_AID_CURRENCY_REQUIRED` | 422 | Sin moneda y empresa sin moneda por defecto |
| `ECONOMIC_AID_APPROVED_AMOUNT_INVALID` | 422 | Aprobar con monto ≤ 0 |
| `ECONOMIC_AID_DATE_INCOHERENT` | 422 | Desembolso anterior a la resolución |
| `ECONOMIC_AID_ELIGIBILITY_NOT_MET` | 422 | No cumple la antigüedad mínima (D-08) |
| `ECONOMIC_AID_STATE_RULE_VIOLATION` | 422 | Transición inválida (resolver no-pendiente, desembolsar no-aprobada, cancelar resuelta) |
| `ECONOMIC_AID_PAYMENT_METHOD_INVALID` | 422 | Forma de pago inexistente/inactiva |
| `ECONOMIC_AID_SELF_APPROVAL_FORBIDDEN` | 403 | El titular intenta validar su propia solicitud |
| `common.validation` | 400 | Campos inválidos (monto ≤ 0, moneda ≠ 3, fecha futura, descripción vacía…) |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` desactualizado |
| `STATE_RULE_VIOLATION` | 409/422 | Expediente no es "empleado completado" |
| (forbidden) | 403 | Sin permiso y no es el titular |
| (not found) | 404 | Solicitud o adjunto inexistente |

Todos los mensajes están **localizados** (es / en) según el `Accept-Language` / cultura de la petición.

---

## 11. Notas y futuro (Fase 2)

- El **flujo de aprobación multinivel** (ruteo a aprobadores, umbrales por monto, notificaciones, delegación) **no** está en Fase 1, pero el modelo está preparado: el estado es un **catálogo configurable** y la validación es una **acción** (`resolution`). El frontend debería tratar la lista de estados como dinámica (desde el catálogo), no hardcodear los 7.
- **Ayudas reembolsables** (anticipos/préstamos), **topes/presupuesto** y el **pago real** (nómina/tesorería) están fuera de Fase 1; el desembolso es solo informativo.
```
